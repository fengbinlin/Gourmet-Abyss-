using UnityEngine;

public class ItemFloatAndRotate : MonoBehaviour
{
    [Header("悬浮设置")]
    [Tooltip("悬浮高度")]
    [Range(0.1f, 2f)]
    public float floatHeight = 0.5f;
    
    [Tooltip("悬浮速度")]
    [Range(0.5f, 5f)]
    public float floatSpeed = 2f;
    
    [Tooltip("悬浮幅度")]
    [Range(0.1f, 1f)]
    public float floatAmount = 0.3f;
    
    [Header("旋转设置")]
    [Tooltip("旋转速度")]
    [Range(0f, 360f)]
    public float rotateSpeed = 90f;
    
    [Tooltip("旋转轴 (XYZ)")]
    public Vector3 rotationAxis = Vector3.up;
    
    [Tooltip("是否随机旋转轴")]
    public bool randomRotationAxis = true;
    
    [Header("减速设置")]
    [Tooltip("减速强度 (值越大减速越快)")]
    [Range(0.5f, 5f)]
    public float decelerationStrength = 1f;
    
    [Tooltip("减速时间 (秒)")]
    [Range(0f, 3f)]
    public float decelerationTime = 3f;
    
    [Tooltip("是否自动移除物理组件")]
    public bool removePhysicsAfterStopped = true;
    
    [Tooltip("是否始终朝向玩家")]
    public bool alwaysFacePlayer = false;
    
    [Header("发光效果")]
    [Tooltip("是否添加发光效果")]
    public bool enableGlow = true;
    
    [Tooltip("发光颜色")]
    public Color glowColor = Color.yellow;
    
    [Tooltip("发光强度")]
    [Range(0.1f, 2f)]
    public float glowIntensity = 0.5f;
    
    [Header("调试")]
    [SerializeField] private bool debugMode = false;
    
    // 私有变量
    private Vector3 targetPosition;  // 目标悬浮位置
    private float physicsStartTime;
    private Rigidbody rb;
    private Collider itemCollider;
    private Renderer itemRenderer;
    private Material originalMaterial;
    private Material glowMaterial;
    
    // 状态标志
    private bool isDecelerating = true;
    private bool isFloating = false;
    private float decelerationProgress = 0f;
    private float originalDrag = 0f;
    private float originalAngularDrag = 0.05f;
    
    private void Start()
    {
        // 获取组件
        rb = GetComponent<Rigidbody>();
        itemCollider = GetComponent<Collider>();
        itemRenderer = GetComponent<Renderer>();
        
        // 记录物理开始时间
        physicsStartTime = Time.time;
        
        // 设置随机旋转轴
        if (randomRotationAxis)
        {
            rotationAxis = new Vector3(
                Random.Range(-1f, 1f),
                Random.Range(0.5f, 1f), // 确保Y轴有较大分量，保持主要向上旋转
                Random.Range(-1f, 1f)
            ).normalized;
        }
        
        // 保存原始的阻力值
        if (rb != null)
        {
            originalDrag = rb.drag;
            originalAngularDrag = rb.angularDrag;
            
            // 立即开始减速过程
            StartDeceleration();
        }
        
        // 如果一开始就没有物理组件，直接开始悬浮
        if (rb == null)
        {
            StartFloating();
        }
    }
    
    private void Update()
    {
        // 处理减速阶段
        if (isDecelerating && rb != null)
        {
            HandleDeceleration();
        }
        // 处理悬浮阶段
        else if (isFloating)
        {
            FloatAnimation();
            RotateAnimation();
            
            if (alwaysFacePlayer)
            {
                FacePlayer();
            }
        }
    }
    
    /// <summary>
    /// 开始减速过程
    /// </summary>
    private void StartDeceleration()
    {
        isDecelerating = true;
        decelerationProgress = 0f;
        
        // 立即增加阻力，快速减速
        rb.drag = originalDrag + 2f;
        rb.angularDrag = originalAngularDrag + 2f;
        
        // 初始化目标悬浮位置
        InitializeTargetPosition();
        
        if (debugMode) Debug.Log($"{gameObject.name}: 开始减速，目标位置: {targetPosition}");
    }
    
    /// <summary>
    /// 处理减速逻辑
    /// </summary>
    private void HandleDeceleration()
    {
        if (!isDecelerating || rb == null) return;
        
        // 更新减速进度
        decelerationProgress = Mathf.Clamp01((Time.time - physicsStartTime) / decelerationTime);
        
        // 应用向目标位置的吸引力
        Vector3 toTarget = targetPosition - transform.position;
        float distanceToTarget = toTarget.magnitude;
        
        // 根据距离调整吸引力强度
        float attractionStrength = Mathf.Clamp(distanceToTarget * 5f, 0.5f, 10f) * decelerationStrength;
        
        // 施加向目标位置的速度
        if (distanceToTarget > 0.1f)
        {
            Vector3 targetVelocity = toTarget.normalized * attractionStrength * decelerationProgress;
            rb.velocity = Vector3.Lerp(rb.velocity, targetVelocity, Time.deltaTime * 5f);
        }
        
        // 逐渐增加阻力
        rb.drag = Mathf.Lerp(originalDrag + 2f, originalDrag + 5f, decelerationProgress);
        rb.angularDrag = Mathf.Lerp(originalAngularDrag + 2f, originalAngularDrag + 5f, decelerationProgress);
        
        // 应用旋转减速
        rb.angularVelocity *= Mathf.Lerp(1f, 0.1f, decelerationProgress);
        
        // 检查是否应该切换到悬浮状态
        if (decelerationProgress >= 0.7f || distanceToTarget < 0.2f)
        {
            float velocityMagnitude = rb.velocity.magnitude;
            
            // 速度足够低时切换到悬浮状态
            if (decelerationProgress >= 1f || velocityMagnitude < 0.5f || distanceToTarget < 0.1f)
            {
                CompleteDeceleration();
            }
        }
        
        // 在减速阶段就应用轻微的悬浮和旋转
        FloatAnimationDuringDeceleration();
        RotateAnimation();
    }
    
    /// <summary>
    /// 减速阶段的悬浮动画
    /// </summary>
    private void FloatAnimationDuringDeceleration()
    {
        if (!isDecelerating || rb == null) return;
        
        // 计算轻微的悬浮偏移
        float yOffset = Mathf.Sin(Time.time * floatSpeed * 0.5f) * floatAmount * 0.3f * decelerationProgress;
        
        // 应用轻微的悬浮力
        Vector3 floatForce = Vector3.up * yOffset * Time.deltaTime * 5f;
        rb.AddForce(floatForce, ForceMode.VelocityChange);
    }
    
    /// <summary>
    /// 完成减速，开始悬浮
    /// </summary>
    private void CompleteDeceleration()
    {
        isDecelerating = false;
        
        if (debugMode) Debug.Log($"{gameObject.name}: 减速完成，开始悬浮");
        
        // 停止物理运动
        if (rb != null)
        {
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            
            if (removePhysicsAfterStopped)
            {
                Destroy(rb);
                rb = null;
                
                // 可以设置为触发器，避免碰撞干扰
                if (itemCollider != null)
                {
                    itemCollider.isTrigger = true;
                }
            }
        }
        
        // 开始悬浮
        StartFloating();
    }
    
    /// <summary>
    /// 开始悬浮
    /// </summary>
    private void StartFloating()
    {
        isFloating = true;
        
        // 调整位置到精确的悬浮高度
        AdjustTargetPosition();
        
        // 初始化发光效果
        if (enableGlow && itemRenderer != null)
        {
            SetupGlowEffect();
        }
        
        if (debugMode) Debug.Log($"{gameObject.name}: 开始悬浮");
    }
    
    /// <summary>
    /// 初始化目标位置
    /// </summary>
    private void InitializeTargetPosition()
    {
        targetPosition = transform.position;
        
        // 调整Y轴位置，确保不在地下
        RaycastHit hit;
        if (Physics.Raycast(transform.position + Vector3.up * 2f, Vector3.down, out hit, 5f))
        {
            // 确保物品在地面上方
            targetPosition.y = hit.point.y + floatHeight;
        }
        else
        {
            // 如果没有检测到地面，使用当前位置加上悬浮高度
            targetPosition.y += floatHeight;
        }
    }
    
    /// <summary>
    /// 调整目标位置
    /// </summary>
    private void AdjustTargetPosition()
    {
        // 重新计算精确的目标位置
        RaycastHit hit;
        if (Physics.Raycast(transform.position + Vector3.up * 2f, Vector3.down, out hit, 5f))
        {
            targetPosition.y = hit.point.y + floatHeight;
        }
        else
        {
            targetPosition.y = transform.position.y;
        }
    }
    
    /// <summary>
    /// 设置发光效果
    /// </summary>
    private void SetupGlowEffect()
    {
        // 保存原始材质
        if (originalMaterial == null)
        {
            originalMaterial = itemRenderer.material;
        }
        
        // 创建一个简单的发光材质
        glowMaterial = new Material(originalMaterial);
        glowMaterial.EnableKeyword("_EMISSION");
        glowMaterial.SetColor("_EmissionColor", glowColor * glowIntensity);
        itemRenderer.material = glowMaterial;
        
        // 发光效果逐渐增强
        StartCoroutine(GradualGlow());
    }
    
    /// <summary>
    /// 逐渐增强发光效果
    /// </summary>
    private System.Collections.IEnumerator GradualGlow()
    {
        float glowTime = 1f;
        float elapsedTime = 0f;
        Color startColor = glowColor * 0.1f;
        Color targetColor = glowColor * glowIntensity;
        
        while (elapsedTime < glowTime)
        {
            elapsedTime += Time.deltaTime;
            float t = elapsedTime / glowTime;
            Color currentColor = Color.Lerp(startColor, targetColor, t);
            
            if (glowMaterial != null)
            {
                glowMaterial.SetColor("_EmissionColor", currentColor);
            }
            
            yield return null;
        }
    }
    
    /// <summary>
    /// 悬浮动画
    /// </summary>
    private void FloatAnimation()
    {
        if (!isFloating) return;
        
        // 计算悬浮偏移
        float yOffset = Mathf.Sin(Time.time * floatSpeed) * floatAmount;
        Vector3 newPosition = new Vector3(
            targetPosition.x,
            targetPosition.y + yOffset,
            targetPosition.z
        );
        
        // 平滑移动到悬浮位置
        transform.position = Vector3.Lerp(transform.position, newPosition, Time.deltaTime * 5f);
    }
    
    /// <summary>
    /// 旋转动画
    /// </summary>
    private void RotateAnimation()
    {
        if (!isFloating && !isDecelerating) return;
        
        // 在减速阶段旋转较慢，悬浮阶段正常速度
        float currentRotateSpeed = rotateSpeed;
        if (isDecelerating)
        {
            currentRotateSpeed = rotateSpeed * Mathf.Lerp(0.3f, 1f, decelerationProgress);
        }
        
        // 应用旋转
        transform.Rotate(rotationAxis * currentRotateSpeed * Time.deltaTime);
    }
    
    /// <summary>
    /// 朝向玩家
    /// </summary>
    private void FacePlayer()
    {
        if (!isFloating) return;
        
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            Vector3 lookDirection = player.transform.position - transform.position;
            lookDirection.y = 0; // 保持水平旋转
            
            if (lookDirection != Vector3.zero)
            {
                Quaternion targetRotation = Quaternion.LookRotation(lookDirection);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * 5f);
            }
        }
    }
    
    /// <summary>
    /// 立即开始悬浮（可用于测试）
    /// </summary>
    [ContextMenu("立即开始悬浮")]
    public void StartFloatingImmediately()
    {
        if (rb != null)
        {
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            Destroy(rb);
        }
        
        isDecelerating = false;
        StartFloating();
        
        if (debugMode) Debug.Log($"{gameObject.name}: 立即开始悬浮");
    }
    
    /// <summary>
    /// 当被拾取时调用
    /// </summary>
    public void OnPickup()
    {
        // 停止所有协程
        StopAllCoroutines();
        
        // 恢复原始材质
        if (itemRenderer != null && originalMaterial != null)
        {
            itemRenderer.material = originalMaterial;
        }
        
        // 可以在这里添加拾取特效或音效
        if (debugMode) Debug.Log($"{gameObject.name} 被拾取！");
        
        // 销毁物体
        Destroy(gameObject);
    }
    
    /// <summary>
    /// 设置悬浮参数
    /// </summary>
    public void SetFloatParameters(float height, float speed, float amount)
    {
        floatHeight = height;
        floatSpeed = speed;
        floatAmount = amount;
        
        // 如果已经初始化，重新计算目标位置
        if (isFloating || isDecelerating)
        {
            AdjustTargetPosition();
        }
    }
    
    /// <summary>
    /// 设置旋转参数
    /// </summary>
    public void SetRotateParameters(float speed, Vector3 axis)
    {
        rotateSpeed = speed;
        rotationAxis = axis;
    }
    
    /// <summary>
    /// 设置发光效果
    /// </summary>
    public void SetGlowEffect(Color color, float intensity)
    {
        glowColor = color;
        glowIntensity = intensity;
        
        if (glowMaterial != null)
        {
            glowMaterial.SetColor("_EmissionColor", glowColor * glowIntensity);
        }
    }
    
    /// <summary>
    /// 在编辑器中绘制调试信息
    /// </summary>
    private void OnDrawGizmosSelected()
    {
        if (!debugMode) return;
        
        // 绘制目标位置
        Gizmos.color = isDecelerating ? Color.yellow : Color.green;
        Gizmos.DrawWireSphere(targetPosition, 0.2f);
        Gizmos.DrawLine(transform.position, targetPosition);
        
        // 绘制悬浮范围
        Gizmos.color = Color.cyan;
        Vector3 minPos = new Vector3(targetPosition.x, targetPosition.y - floatAmount, targetPosition.z);
        Vector3 maxPos = new Vector3(targetPosition.x, targetPosition.y + floatAmount, targetPosition.z);
        Gizmos.DrawLine(minPos, maxPos);
        
        // 绘制减速进度
        if (isDecelerating)
        {
            Gizmos.color = Color.Lerp(Color.red, Color.green, decelerationProgress);
            Gizmos.DrawWireSphere(transform.position, 0.3f);
        }
    }
    
    private void OnDestroy()
    {
        // 清理材质实例
        if (glowMaterial != null && Application.isPlaying)
        {
            Destroy(glowMaterial);
        }
    }
}