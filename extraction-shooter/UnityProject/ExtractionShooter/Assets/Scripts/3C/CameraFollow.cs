using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    [Header("目标设置")]
    [SerializeField] private Transform target; 

    [Header("跟随参数")]
    [SerializeField] private float smoothTime = 0.3f; 
    [SerializeField] private Vector3 offset; 
    [SerializeField] private bool autoOffset = true; 

    private Vector3 velocity = Vector3.zero; 

    // --- 新增：震动参数 ---
    private float shakeTimer = 0f;
    private float shakeMagnitude = 0f;

    void Start()
    {
        if (target == null) return;
        if (autoOffset) offset = transform.position - target.position;
    }

    void LateUpdate()
    {
        if (target == null) return;

        // 1. 计算基础的跟随位置 (平滑处理)
        Vector3 targetPosition = target.position + offset;
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