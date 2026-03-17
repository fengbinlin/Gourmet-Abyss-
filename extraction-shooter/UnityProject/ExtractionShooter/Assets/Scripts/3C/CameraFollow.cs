using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    [Header("目标设置")]
    [SerializeField] private Transform target; 
    [SerializeField] private TopDownController playerController;

    [Header("跟随参数")]
    [SerializeField] private float smoothTime = 0.3f; 
    [SerializeField] private Vector3 offset; 
    [SerializeField] private bool autoOffset = true; 

    private Vector3 velocity = Vector3.zero; 
    private Transform defaultTarget;
    private Transform overrideTarget;

    // --- 新增：震动参数 ---
    private float shakeTimer = 0f;
    private float shakeMagnitude = 0f;

    void Start()
    {
        if (target == null) return;
        defaultTarget = target;
        if (autoOffset) offset = transform.position - target.position;
        AutoBindPlayerControllerIfNeeded();
    }

    private void AutoBindPlayerControllerIfNeeded()
    {
        if (playerController != null) return;
        if (defaultTarget == null) return;
        playerController = defaultTarget.GetComponent<TopDownController>();
    }

    void LateUpdate()
    {
        // 如果玩家开始移动，强制回到默认跟随
        if (overrideTarget != null && playerController != null && playerController.IsMoving())
        {
            ClearOverrideTarget();
        }

        Transform currentTarget = overrideTarget != null ? overrideTarget : target;
        if (currentTarget == null) return;

        // 1. 计算基础的跟随位置 (平滑处理)
        Vector3 targetPosition = currentTarget.position + offset;
        Vector3 smoothedPosition = Vector3.SmoothDamp(transform.position, targetPosition, ref velocity, smoothTime);

        // 2. 叠加震动效果 (如果有震动时间剩余)
        if (shakeTimer > 0)
        {
            // 在球体内随机取一个点作为偏移
            Vector3 shakeOffset = Random.insideUnitSphere * shakeMagnitude;
            smoothedPosition += shakeOffset;

            shakeTimer -= Time.deltaTime;
        }

        // 3. 应用最终位置
        transform.position = smoothedPosition;
    }

    /// <summary>
    /// 临时切换相机跟随目标（例如 UI 选中某个点位）。
    /// 注意：offset 不会改变，保持当前相机相对位移。
    /// </summary>
    public void SetOverrideTarget(Transform newTarget)
    {
        if (newTarget == null) return;
        overrideTarget = newTarget;
    }

    /// <summary>
    /// 清除临时跟随目标，回到默认 target（通常是 Player）。
    /// </summary>
    public void ClearOverrideTarget()
    {
        overrideTarget = null;
    }

    /// <summary>
    /// 允许外部在运行时重设默认目标（如换人/重生）。
    /// </summary>
    public void SetDefaultTarget(Transform newDefault)
    {
        if (newDefault == null) return;
        defaultTarget = newDefault;
        target = newDefault;
        AutoBindPlayerControllerIfNeeded();
        if (autoOffset) offset = transform.position - newDefault.position;
    }

    /// <summary>
    /// 公开方法：触发屏幕震动
    /// </summary>
    /// <param name="duration">震动持续时间 (秒)</param>
    /// <param name="magnitude">震动强度 (位移距离)</param>
    public void Shake(float duration, float magnitude)
    {
        shakeTimer = duration;
        shakeMagnitude = magnitude;
    }
}