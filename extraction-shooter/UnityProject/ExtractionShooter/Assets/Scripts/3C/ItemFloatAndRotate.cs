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
    
    [Header("物理设置")]
    [Tooltip("物理运动的最长时间")]
    [Range(0.5f, 5f)]
    public float maxPhysicsTime = 2f;
    
    [Tooltip("速度低于此值时认为物体静止")]
    [Range(0.01f, 0.5f)]
    public float minVelocityThreshold = 0.1f;
    
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
    private bool isPhysicsActive = true;
    private bool isInitialized = false;
    private float timeStopped = 0f;
    private const float STOP_CONFIRM_TIME = 0.5f; // 确认停止的时间
    
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
        
        // 如果一开始就没有物理组件，直接初始化悬浮
        if (rb == null)
        {
            InitializeFloating();
        }
    }
    
    private void Update()
    {
        // 处理物理运动阶段
        if (isPhysicsActive && rb != null)
        {
            CheckPhysicsCompletion();
        }
        // 处理悬浮阶段
        else if (isInitialized)
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
    /// 检查物理运动是否完成
    /// </summary>
    private void CheckPhysicsCompletion()
    {
        float currentTime = Time.time;
        
        // 检查是否超过最大物理时间
        if (currentTime - physicsStartTime > maxPhysicsTime)
        {
            if (debugMode) Debug.Log($"{gameObject.name}: 超过最大物理时间，强制停止物理");
            CompletePhysics();
            return;
        }
        
        // 检查速度是否低于阈值
        if (rb.velocity.magnitude < minVelocityThreshold)
        {
            timeStopped += Time.deltaTime;
            
            // 确认物体已经静止一段时间
            if (timeStopped >= STOP_CONFIRM_TIME)
            {
                if (debugMode) Debug.Log($"{gameObject.name}: 物体已静止，停止物理");
                CompletePhysics();
            }
        }
        else
        {
            // 如果又开始运动，重置计时
            timeStopped = 0f;
        }
    }
    
    /// <summary>
    /// 完成物理运动，开始悬浮
    /// </summary>
    private void CompletePhysics()
    {
        isPhysicsActive = false;
        
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
        
        // 初始化悬浮
        InitializeFloating();
    }
    
    /// <summary>
    /// 初始化悬浮
    /// </summary>
    private void InitializeFloating()
    {
        // 记录当前位置作为目标位置
        targetPosition = transform.position;
        
        // 调整Y轴位置，确保不在地下
        RaycastHit hit;
        if (Physics.Raycast(transform.position + Vector3.up * 2f, Vector3.down, out hit, 5f))
        {
            // 确保物品在地面上方
            targetPosition.y = hit.point.y + floatHeight;
            if (debugMode) Debug.Log($"{gameObject.name}: 调整悬浮高度到 {targetPosition.y}");
        }
        else
        {
            // 如果没有检测到地面，使用当前位置加上悬浮高度
            targetPosition.y += floatHeight;
        }
        
        // 初始化发光效果
        if (enableGlow && itemRenderer != null)
        {
            SetupGlowEffect();
        }
        
        isInitialized = true;
        
        if (debugMode) Debug.Log($"{gameObject.name}: 开始悬浮，目标位置: {targetPosition}");
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
        // 注意：实际项目中可能需要使用专门的发光shader
        glowMaterial = new Material(originalMaterial);
        glowMaterial.EnableKeyword("_EMISSION");
        glowMaterial.SetColor("_EmissionColor", glowColor * glowIntensity);
        itemRenderer.material = glowMaterial;
    }
    
    /// <summary>
    /// 悬浮动画
    /// </summary>
    private void FloatAnimation()
    {
        if (!isInitialized) return;
        
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
        if (!isInitialized) return;
        
        // 应用旋转
        transform.Rotate(rotationAxis * rotateSpeed * Time.deltaTime);
    }
    
    /// <summary>
    /// 朝向玩家
    /// </summary>
    private void FacePlayer()
    {
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
        
        InitializeFloating();
        
        if (debugMode) Debug.Log($"{gameObject.name}: 立即开始悬浮");
    }
    
    /// <summary>
    /// 当被拾取时调用
    /// </summary>
    public void OnPickup()
    {
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
        if (isInitialized)
        {
            targetPosition.y = targetPosition.y - floatHeight + height;
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
        
        if (isInitialized)
        {
            // 绘制目标位置
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(targetPosition, 0.2f);
            Gizmos.DrawLine(transform.position, targetPosition);
            
            // 绘制悬浮范围
            Gizmos.color = Color.cyan;
            Vector3 minPos = new Vector3(targetPosition.x, targetPosition.y - floatAmount, targetPosition.z);
            Vector3 maxPos = new Vector3(targetPosition.x, targetPosition.y + floatAmount, targetPosition.z);
            Gizmos.DrawLine(minPos, maxPos);
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