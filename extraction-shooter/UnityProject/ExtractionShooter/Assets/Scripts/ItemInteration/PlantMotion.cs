using UnityEngine;

public class PlantMotion : MonoBehaviour
{
    [Header("Target Mesh（指定被变形的子物体）")]
    public GameObject targetMesh;
    
    [Header("粒子效果")]
    public ParticleSystem touchParticles;  // 角色接触时播放的粒子效果
    public Vector3 particleOffset = new Vector3(0, 0.2f, 0);  // 粒子位置偏移

    [Header("风参数")]
    public float windStrength = 10f;    // 风摆动幅度（度）
    public float windSpeed = 1f;        // 风速度
    public float windScale = 0.5f;      // 世界坐标影响强度
    public float windTiltForward = 5f;  // 前后轻微倾斜幅度

    [Header("触发放大参数")]
    public float maxRotationAngle = 15f;   // 最大倾斜角度
    public float maxScaleIncrease = 0.3f;  // 最大缩放增加比例（改为正数表示整体放大）
    public float pressSpeed = 2f;         // 触发速度
    public float scaleRecoverySpeed = 1.5f; // 恢复速度

    [Header("回弹参数")]
    public float bounceDuration = 1f;    // 回弹持续时间
    public float bounceFrequency = 8f;   // 回弹频率
    public float bounceScaleFactor = 0.1f; // 回弹时缩放幅度

    [Header("优化设置")]
    [Tooltip("距离相机多远时停止更新（降低性能消耗）")]
    public float maxUpdateDistance = 50f;  // 最大更新距离
    
    [Tooltip("在相机视野外的更新间隔（秒）")]
    public float offScreenUpdateInterval = 0.5f;  // 视野外更新间隔
    
    [Tooltip("在相机视野内但距离较远时的更新间隔（秒）")]
    public float farUpdateInterval = 0.1f;  // 远距离更新间隔
    
    [Tooltip("完全停止更新的距离（用于优化大量植物）")]
    public float stopUpdateDistance = 100f;  // 完全停止更新距离

    // 私有变量
    private Quaternion originalRotation;
    private Vector3 originalPosition;
    private Vector3 originalScale;
    private bool isPlayerInside = false;
    private float pressBlend = 0f; // 0=原状, 1=最大变形
    private Vector3 impactDirection;
    private float bounceTimer = 0f;
    private bool hasPlayedParticles = false;  // 标记是否已播放过粒子
    
    // 优化相关变量
    private Camera mainCamera;
    private float updateTimer = 0f;
    private float currentUpdateInterval = 0f;
    private float distanceToCamera = 0f;
    private bool isInCameraView = false;
    private bool shouldUpdate = true;
    private Bounds plantBounds;  // 植物的包围盒，用于视锥体检测
    private Renderer plantRenderer;  // 用于检查可见性

    void Start()
    {
        if (targetMesh == null)
        {
            Debug.LogError("PlantMotion: 请在 Inspector 里指定 targetMesh！");
            enabled = false;
            return;
        }
        
        // 随机化初始大小
        targetMesh.transform.localScale *= Random.Range(0.8f, 1.2f);
        originalRotation = targetMesh.transform.localRotation;
        originalPosition = targetMesh.transform.localPosition;
        originalScale = targetMesh.transform.localScale;
        
        // 获取主相机引用
        mainCamera = Camera.main;
        if (mainCamera == null)
        {
            Debug.LogWarning("PlantMotion: 未找到主相机，视野优化将不可用");
        }
        
        // 尝试获取渲染器用于可见性检查
        plantRenderer = targetMesh.GetComponent<Renderer>();
        if (plantRenderer == null)
        {
            // 如果目标物体没有渲染器，尝试在子物体中查找
            plantRenderer = targetMesh.GetComponentInChildren<Renderer>();
        }
        
        // 计算植物的包围盒
        if (plantRenderer != null)
        {
            plantBounds = plantRenderer.bounds;
        }
        else
        {
            // 如果找不到渲染器，使用粗略估计
            plantBounds = new Bounds(transform.position, Vector3.one * 2f);
        }
        
        // 如果粒子系统存在，确保它不会自动播放
        if (touchParticles != null)
        {
            touchParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
        
        // 初始设置为立即更新
        currentUpdateInterval = 0f;
    }

    void Update()
    {
        // 检查是否应该更新
        CheckShouldUpdate();
        
        // 如果不需要更新，直接返回
        if (!shouldUpdate)
        {
            // 如果不在视野内且玩家不在内部，可以完全跳过更新
            if (!isPlayerInside && bounceTimer <= 0f)
            {
                return;
            }
        }
        
        // 如果设置了更新间隔，使用间隔更新
        if (currentUpdateInterval > 0)
        {
            updateTimer += Time.deltaTime;
            if (updateTimer < currentUpdateInterval)
            {
                return; // 未到更新时间，跳过
            }
            updateTimer = 0f; // 重置计时器
        }
        
        // 正常的更新逻辑
        PerformUpdate();
    }
    
    /// <summary>
    /// 检查是否需要更新以及更新频率
    /// </summary>
    private void CheckShouldUpdate()
    {
        // 如果有玩家交互，强制实时更新
        if (isPlayerInside || bounceTimer > 0f)
        {
            shouldUpdate = true;
            currentUpdateInterval = 0f; // 实时更新
            return;
        }
        
        // 如果相机不存在，使用默认更新
        if (mainCamera == null)
        {
            shouldUpdate = true;
            currentUpdateInterval = 0f;
            return;
        }
        
        // 计算到相机的距离
        distanceToCamera = Vector3.Distance(transform.position, mainCamera.transform.position);
        
        // 如果距离超过完全停止更新的距离，完全不更新
        if (distanceToCamera > stopUpdateDistance)
        {
            shouldUpdate = false;
            return;
        }
        
        // 如果距离超过最大更新距离，停止更新
        if (distanceToCamera > maxUpdateDistance)
        {
            shouldUpdate = false;
            return;
        }
        
        // 检查是否在相机视野内
        isInCameraView = IsVisibleToCamera();
        
        if (isInCameraView)
        {
            shouldUpdate = true;
            // 在视野内，根据距离设置更新间隔
            if (distanceToCamera > maxUpdateDistance * 0.7f)
            {
                // 距离较远，降低更新频率
                currentUpdateInterval = farUpdateInterval;
            }
            else
            {
                // 距离较近，实时更新
                currentUpdateInterval = 0f;
            }
        }
        else
        {
            // 不在视野内，但玩家可能正在接近，使用较低的更新频率
            shouldUpdate = true;
            currentUpdateInterval = offScreenUpdateInterval;
            
            // 如果距离很远且不在视野内，可以完全停止更新
            if (distanceToCamera > maxUpdateDistance * 0.9f)
            {
                shouldUpdate = false;
            }
        }
    }
    
    /// <summary>
    /// 检查植物是否在相机视野内
    /// </summary>
    private bool IsVisibleToCamera()
    {
        if (mainCamera == null) return true; // 没有相机时默认可见
        
        // 方法1: 使用渲染器的isVisible属性（最简单但需要渲染器）
        if (plantRenderer != null)
        {
            return plantRenderer.isVisible;
        }
        
        // 方法2: 使用视锥体检查
        Plane[] planes = GeometryUtility.CalculateFrustumPlanes(mainCamera);
        return GeometryUtility.TestPlanesAABB(planes, plantBounds);
    }
    
    /// <summary>
    /// 执行实际的更新逻辑
    /// </summary>
    private void PerformUpdate()
    {
        if (isPlayerInside)
        {
            // 渐变到最大形变
            pressBlend = Mathf.MoveTowards(pressBlend, 1f, Time.deltaTime * pressSpeed);
            ApplyPressPose(pressBlend);
        }
        else if (bounceTimer > 0)
        {
            // 离开后果冻回弹
            float progress = 1f - bounceTimer / bounceDuration;
            float damping = Mathf.Exp(-3f * progress);
            float oscillation = Mathf.Sin(progress * bounceFrequency) * damping;

            // 回弹时缩放效果
            float blend = Mathf.Lerp(1f, 0f, progress);
            ApplyPressPose(blend);
            
            // 添加缩放抖动效果
            float bounceScale = 1f + oscillation * bounceScaleFactor;
            targetMesh.transform.localScale = originalScale * bounceScale;

            bounceTimer -= Time.deltaTime;
            if (bounceTimer <= 0)
                ResetToOriginal();
        }
        else
        {
            // 风吹状态
            pressBlend = Mathf.MoveTowards(pressBlend, 0f, Time.deltaTime * scaleRecoverySpeed);

            if (pressBlend > 0f)
            {
                ApplyPressPose(pressBlend);
            }
            else
            {
                float noiseOffset = transform.position.x * windScale + transform.position.z * windScale;
                float sway = Mathf.Sin(Time.time * windSpeed + noiseOffset) * windStrength;
                float forwardTilt = Mathf.Cos(Time.time * windSpeed * 0.8f + noiseOffset) * windTiltForward;
                
                // 绕Z轴左右摇 + 绕X轴前后轻微摆
                targetMesh.transform.localRotation =
                    originalRotation * Quaternion.Euler(forwardTilt, 0f, sway);

                targetMesh.transform.localPosition = originalPosition;
                targetMesh.transform.localScale = originalScale;
            }
        }
    }

    /// <summary>
    /// 根据变形比例应用旋转 + 整体放大
    /// </summary>
    private void ApplyPressPose(float blend)
    {
        float rotAmount = blend * maxRotationAngle;
        float scaleIncrease = blend * maxScaleIncrease;
        
        // 整体缩放因子，从1到1+maxScaleIncrease
        float totalScaleFactor = 1f + scaleIncrease;

        // 计算倾斜方向
        Vector3 axis = Vector3.Cross(Vector3.up, impactDirection.normalized);
        Quaternion rot = Quaternion.AngleAxis(rotAmount, axis);
        targetMesh.transform.localRotation = originalRotation * rot;

        // 整体均匀放大
        targetMesh.transform.localScale = originalScale * totalScaleFactor;

        // 轻微向下偏移，使效果更自然
        targetMesh.transform.localPosition = originalPosition - Vector3.up * (blend * 0.05f);
    }

    private void ResetToOriginal()
    {
        targetMesh.transform.localRotation = originalRotation;
        targetMesh.transform.localPosition = originalPosition;
        targetMesh.transform.localScale = originalScale;
        pressBlend = 0f;
        hasPlayedParticles = false;  // 重置粒子播放标记
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            isPlayerInside = true;
            hasPlayedParticles = false;  // 重置标记，允许再次播放
            shouldUpdate = true;  // 强制更新
            currentUpdateInterval = 0f;  // 实时更新

            Vector3 dir = transform.position - other.transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.0001f)
                impactDirection = dir.normalized;
            else
                impactDirection = Vector3.forward;
        }
    }

    private void OnTriggerStay(Collider other)
    {
        if (other.CompareTag("Player") && !hasPlayedParticles)
        {
            // 当角色在触发区域内且未播放粒子时，播放粒子效果
            PlayTouchParticles(other.transform.position);
            hasPlayedParticles = true;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            isPlayerInside = false;
            bounceTimer = bounceDuration;
            shouldUpdate = true;  // 确保回弹动画能正常播放
            currentUpdateInterval = 0f;  // 回弹期间实时更新
        }
    }

    /// <summary>
    /// 播放接触粒子效果
    /// </summary>
    private void PlayTouchParticles(Vector3 playerPosition)
    {
        if (touchParticles != null)
        {
            // 计算粒子播放位置
            Vector3 particlePosition = transform.position + particleOffset;
            touchParticles.transform.position = particlePosition;
            
            // 播放粒子效果
            touchParticles.Play();
        }
    }
    
    /// <summary>
    /// 在编辑器模式下可视化更新范围
    /// </summary>
    private void OnDrawGizmosSelected()
    {
        if (!Application.isPlaying) return;
        
        // 绘制更新范围
        Gizmos.color = shouldUpdate ? Color.green : Color.gray;
        Gizmos.DrawWireSphere(transform.position, maxUpdateDistance);
        
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, stopUpdateDistance);
        
        // 绘制到相机的距离
        if (mainCamera != null)
        {
            Gizmos.color = isInCameraView ? Color.yellow : Color.blue;
            Gizmos.DrawLine(transform.position, mainCamera.transform.position);
            
            // 显示当前更新间隔
            #if UNITY_EDITOR
            UnityEditor.Handles.Label(transform.position + Vector3.up * 2f, 
                $"距离: {distanceToCamera:F1}\n间隔: {currentUpdateInterval:F2}s\n状态: {(shouldUpdate ? "更新" : "停止")}");
            #endif
        }
    }
}