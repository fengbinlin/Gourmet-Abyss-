using UnityEngine;

public class InteractiveFeedback : MonoBehaviour
{
    [Header("动画设置")]
    [SerializeField] private float scaleMultiplier = 1.2f;  // 缩放倍数
    [SerializeField] private float animationDuration = 0.2f;  // 动画总时长
    [SerializeField] private AnimationCurve animationCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);  // 动画曲线

    [Header("自动设置")]
    [SerializeField] private Vector3 originalScale;  // 原始大小
    [SerializeField] private FeedbackState currentState = FeedbackState.Idle;  // 当前状态
    [SerializeField] private float animationTimer = 0f;  // 动画计时器

    // 动画状态枚举
    private enum FeedbackState
    {
        Idle,        // 空闲
        Expanding,   // 放大中
        Shrinking    // 缩小中
    }

    private void Start()
    {
        // 记录原始大小
        originalScale = transform.localScale;
    }

    private void Update()
    {
        // 如果没有在播放动画，直接返回
        if (currentState == FeedbackState.Idle) return;
        
        // 更新计时器
        animationTimer += Time.deltaTime;
        float halfDuration = animationDuration * 0.5f;
        
        if (currentState == FeedbackState.Expanding)
        {
            // 放大阶段
            float progress = Mathf.Clamp01(animationTimer / halfDuration);
            float curveValue = animationCurve.Evaluate(progress);
            float currentScale = Mathf.Lerp(1f, scaleMultiplier, curveValue);
            transform.localScale = originalScale * currentScale;
            
            // 检查是否需要切换到缩小阶段
            if (progress >= 1f)
            {
                currentState = FeedbackState.Shrinking;
                animationTimer = 0f;  // 重置计时器
            }
        }
        else if (currentState == FeedbackState.Shrinking)
        {
            // 缩小阶段
            float progress = Mathf.Clamp01(animationTimer / halfDuration);
            float curveValue = animationCurve.Evaluate(progress);
            float currentScale = Mathf.Lerp(scaleMultiplier, 1f, curveValue);
            transform.localScale = originalScale * currentScale;
            
            // 检查动画是否完成
            if (progress >= 1f)
            {
                currentState = FeedbackState.Idle;
                transform.localScale = originalScale;  // 确保精确回到原始大小
            }
        }
    }

    /// <summary>
    /// 播放交互反馈动画
    /// </summary>
    public void PlayFeedback()
    {
        // 重置动画状态
        currentState = FeedbackState.Expanding;
        animationTimer = 0f;
    }

    /// <summary>
    /// 播放交互反馈动画（可自定义参数）
    /// </summary>
    /// <param name="customScaleMultiplier">自定义缩放倍数</param>
    /// <param name="customDuration">自定义动画总时长</param>
    public void PlayFeedback(float customScaleMultiplier, float customDuration)
    {
        scaleMultiplier = customScaleMultiplier;
        animationDuration = customDuration;
        PlayFeedback();
    }

    /// <summary>
    /// 播放交互反馈动画（可自定义所有参数）
    /// </summary>
    /// <param name="customScaleMultiplier">自定义缩放倍数</param>
    /// <param name="expandDuration">放大时长</param>
    /// <param name="shrinkDuration">缩小时长</param>
    public void PlayFeedback(float customScaleMultiplier, float expandDuration, float shrinkDuration)
    {
        scaleMultiplier = customScaleMultiplier;
        animationDuration = expandDuration + shrinkDuration;
        PlayFeedback();
    }

    /// <summary>
    /// 立即停止当前动画
    /// </summary>
    public void StopFeedback()
    {
        currentState = FeedbackState.Idle;
        transform.localScale = originalScale;
    }

    /// <summary>
    /// 平滑停止动画（完成当前阶段）
    /// </summary>
    public void StopFeedbackSmoothly()
    {
        if (currentState == FeedbackState.Expanding)
        {
            // 如果正在放大，继续完成放大后缩小
            currentState = FeedbackState.Shrinking;
            animationTimer = 0f;
        }
        else if (currentState == FeedbackState.Shrinking)
        {
            // 如果正在缩小，让其自然完成
            // 不需要做任何操作，动画会自然完成
        }
    }

    /// <summary>
    /// 检查是否正在播放动画
    /// </summary>
    /// <returns>是否正在播放动画</returns>
    public bool IsPlaying()
    {
        return currentState != FeedbackState.Idle;
    }

    // 在编辑器模式下可视化动画曲线
    private void OnValidate()
    {
        // 确保值合理
        scaleMultiplier = Mathf.Max(1.01f, scaleMultiplier);
        animationDuration = Mathf.Max(0.05f, animationDuration);
    }

    /// <summary>
    /// 重置为原始大小
    /// </summary>
    [ContextMenu("Reset to Original Scale")]
    public void ResetToOriginalScale()
    {
        originalScale = transform.localScale;
    }
}