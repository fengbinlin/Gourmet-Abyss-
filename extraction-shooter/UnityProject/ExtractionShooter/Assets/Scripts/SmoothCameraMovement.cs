using UnityEngine;

public class SmoothCameraMovement : MonoBehaviour
{
    [Header("移动设置")]
    [SerializeField] private float minX = -5f;     // 最小X坐标
    [SerializeField] private float maxX = 5f;      // 最大X坐标
    [SerializeField] private float moveSpeed = 1f; // 移动速度
    
    [Header("插值方法")]
    [SerializeField] private InterpolationType interpolationType = InterpolationType.SmoothStep;
    
    [Header("阻尼效果")]
    [SerializeField] private bool useDamping = false;  // 是否使用阻尼
    [SerializeField] private float damping = 5f;       // 阻尼强度
    
    [Header("缓动曲线")]
    [SerializeField] private AnimationCurve customCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    
    [Header("调试")]
    [SerializeField] private bool showDebugInfo = true;
    
    private Vector3 initialPosition;
    private float currentLerpTime = 0f;
    private float direction = 1f; // 1表示从min到max，-1表示从max到min
    
    public enum InterpolationType
    {
        Linear,
        SmoothStep,
        SmootherStep,
        Sine,
        CustomCurve,
        PingPong
    }
    
    private void Start()
    {
        initialPosition = transform.position;
        
        // 初始化自定义曲线（如果没有设置的话）
        if (customCurve.length == 0)
        {
            customCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
        }
    }
    
    private void Update()
    {
        switch (interpolationType)
        {
            case InterpolationType.PingPong:
                PingPongMovement();
                break;
            default:
                LerpMovement();
                break;
        }
        
        // 显示调试信息
        if (showDebugInfo)
        {
            //Debug.Log($"当前X: {transform.position.x:F2}, 插值时间: {currentLerpTime:F2}");
        }
    }
    
    private void LerpMovement()
    {
        // 更新插值时间
        currentLerpTime += Time.deltaTime * moveSpeed * direction;
        currentLerpTime = Mathf.Clamp01(currentLerpTime);
        
        // 在两端反转方向
        if (currentLerpTime >= 1f) direction = -1f;
        if (currentLerpTime <= 0f) direction = 1f;
        
        // 根据选择的插值方法计算插值因子
        float t = CalculateInterpolationFactor(currentLerpTime);
        
        // 计算目标位置
        float targetX = Mathf.Lerp(minX, maxX, t);
        Vector3 targetPosition = new Vector3(targetX, initialPosition.y, initialPosition.z);
        
        // 应用移动
        if (useDamping)
        {
            // 使用阻尼（平滑阻尼）移动
            transform.position = Vector3.Lerp(transform.position, targetPosition, Time.deltaTime * damping);
        }
        else
        {
            // 直接移动到目标位置
            transform.position = targetPosition;
        }
    }
    
    private void PingPongMovement()
    {
        // 使用Mathf.PingPong获取在minX和maxX之间的值
        float pingPongValue = Mathf.PingPong(Time.time * moveSpeed, 1f);
        float targetX = Mathf.Lerp(minX, maxX, pingPongValue);
        Vector3 targetPosition = new Vector3(targetX, initialPosition.y, initialPosition.z);
        
        if (useDamping)
        {
            transform.position = Vector3.Lerp(transform.position, targetPosition, Time.deltaTime * damping);
        }
        else
        {
            transform.position = targetPosition;
        }
    }
    
    private float CalculateInterpolationFactor(float t)
    {
        switch (interpolationType)
        {
            case InterpolationType.Linear:
                return t;
                
            case InterpolationType.SmoothStep:
                return Mathf.SmoothStep(0f, 1f, t);
                
            case InterpolationType.SmootherStep:
                return t * t * t * (t * (6f * t - 15f) + 10f);
                
            case InterpolationType.Sine:
                return (Mathf.Sin((t - 0.5f) * Mathf.PI) + 1f) * 0.5f;
                
            case InterpolationType.CustomCurve:
                return customCurve.Evaluate(t);
                
            default:
                return t;
        }
    }
    
    // 公开方法，用于外部控制
    public void SetTargetXRange(float newMinX, float newMaxX)
    {
        minX = Mathf.Min(newMinX, newMaxX);
        maxX = Mathf.Max(newMinX, newMaxX);
    }
    
    public void SetSpeed(float newSpeed)
    {
        moveSpeed = Mathf.Max(0.1f, newSpeed);
    }
    
    public float GetCurrentX()
    {
        return transform.position.x;
    }
    
    // 在Scene视图中显示范围
    private void OnDrawGizmosSelected()
    {
        if (showDebugInfo)
        {
            Vector3 minPos = new Vector3(minX, transform.position.y, transform.position.z);
            Vector3 maxPos = new Vector3(maxX, transform.position.y, transform.position.z);
            
            Gizmos.color = Color.green;
            Gizmos.DrawLine(minPos, maxPos);
            Gizmos.DrawSphere(minPos, 0.2f);
            Gizmos.DrawSphere(maxPos, 0.2f);
            
            Gizmos.color = Color.yellow;
            Gizmos.DrawSphere(transform.position, 0.15f);
        }
    }
}