using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class ResourceBarController : MonoBehaviour
{
    public Animator mainUIAnimator;
    [Header("进度条设置")]
    [SerializeField] private Image progressBarImage;
    [SerializeField] private Transform barTransform;  // 用于跳动效果

    [Header("颜色设置")]
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color lowResourceColor = Color.red;
    [SerializeField] [Range(0f, 1f)] private float lowResourceThreshold = 0.3f; // 低于此阈值开始闪烁

    [Header("颜色闪烁设置")]
    [SerializeField] private float colorBlinkSpeed = 2f; // 颜色闪烁速度
    [SerializeField] private float minAlpha = 0.5f;      // 最小透明度
    [SerializeField] private float maxAlpha = 1f;        // 最大透明度

    [Header("跳动效果设置")]
    [SerializeField] private float pulseSpeed = 8f;      // 跳动速度
    [SerializeField] private float minPulseScale = 0.9f; // 最小缩放
    [SerializeField] private float maxPulseScale = 1.1f; // 最大缩放
    [SerializeField] [Range(0f, 1f)] private float pulseThreshold = 0.2f; // 低于此阈值开始跳动

    [Header("状态")]
    [SerializeField] private float currentPercentage = 1f;

    private Color originalColor;
    private Vector3 originalScale;
    private Coroutine pulseCoroutine;
    private Coroutine colorBlinkCoroutine;
    private bool isPulsing = false;
    private bool isColorBlinking = false;

    // 记录是否在跳动状态
    public bool IsPulsing => isPulsing;
    public bool IsColorBlinking => isColorBlinking;

    private void Awake()
    {
        // 记录初始状态
        if (progressBarImage != null)
        {
            originalColor = progressBarImage.color;
        }

        if (barTransform != null)
        {
            originalScale = barTransform.localScale;
        }
        else
        {
            originalScale = transform.localScale;
        }

        // 确保normalColor是初始颜色
        normalColor = originalColor;

        // 初始化为100%
        currentPercentage = 1f;
    }

    private void Start()
    {
        // 确保开始时是正确的状态
        ResetBar();
    }

    private void OnEnable()
    {
        StopAllCoroutines();
        isPulsing = false;
        isColorBlinking = false;
        ResetScale();
    }

    private void OnDisable()
    {
        StopAllCoroutines();
        ResetScale();
    }

    /// <summary>
    /// 重置进度条到初始状态
    /// </summary>
    public void ResetBar()
    {
        // 停止所有协程
        StopAllCoroutines();
        isPulsing = false;
        isColorBlinking = false;

        // 重置颜色
        if (progressBarImage != null)
        {
            progressBarImage.color = originalColor;
        }

        // 重置缩放
        ResetScale();

        // 重置百分比
        currentPercentage = 1f;
    }

    /// <summary>
    /// 更新进度条
    /// </summary>
    /// <param name="percentage">当前百分比 (0-1)</param>
    public void UpdateProgress(float percentage)
    {
        if (percentage < 0) percentage = 0;
        if (percentage > 1) percentage = 1;

        currentPercentage = percentage;

        // 控制颜色闪烁效果
        ControlColorBlinkEffect(percentage);

        // 控制跳动效果
        ControlPulseEffect(percentage);
    }

    /// <summary>
    /// 控制颜色闪烁效果
    /// </summary>
    private void ControlColorBlinkEffect(float percentage)
    {
        if (percentage <= lowResourceThreshold && !isColorBlinking)
        {
            // 开始颜色闪烁
            StartColorBlink();
        }
        else if (percentage > lowResourceThreshold && isColorBlinking)
        {
            // 停止颜色闪烁
            StopColorBlink();
        }
    }

    /// <summary>
    /// 控制跳动效果
    /// </summary>
    private void ControlPulseEffect(float percentage)
    {
        if (percentage <= pulseThreshold && !isPulsing)
        {
            // 开始跳动
            StartPulse();
        }
        else if (percentage > pulseThreshold && isPulsing)
        {
            // 停止跳动
            StopPulse();
        }
    }

    /// <summary>
    /// 开始颜色闪烁效果
    /// </summary>
    private void StartColorBlink()
    {
        if (isColorBlinking || progressBarImage == null) return;

        isColorBlinking = true;
        if (colorBlinkCoroutine != null)
        {
            StopCoroutine(colorBlinkCoroutine);
        }
        colorBlinkCoroutine = StartCoroutine(ColorBlinkAnimation());
    }

    /// <summary>
    /// 停止颜色闪烁效果
    /// </summary>
    private void StopColorBlink()
    {
        if (!isColorBlinking) return;

        isColorBlinking = false;
        if (colorBlinkCoroutine != null)
        {
            StopCoroutine(colorBlinkCoroutine);
            colorBlinkCoroutine = null;
        }

        // 重置颜色
        if (progressBarImage != null)
        {
            progressBarImage.color = normalColor;
        }
    }

    /// <summary>
    /// 开始跳动效果
    /// </summary>
    private void StartPulse()
    {
        if (isPulsing) return;

        isPulsing = true;
        if (pulseCoroutine != null)
        {
            StopCoroutine(pulseCoroutine);
        }
        pulseCoroutine = StartCoroutine(PulseAnimation());
    }

    /// <summary>
    /// 停止跳动效果
    /// </summary>
    private void StopPulse()
    {
        if (!isPulsing) return;

        isPulsing = false;
        if (pulseCoroutine != null)
        {
            StopCoroutine(pulseCoroutine);
            pulseCoroutine = null;
        }

        ResetScale();
    }

    /// <summary>
    /// 颜色闪烁动画协程
    /// </summary>
    private IEnumerator ColorBlinkAnimation()
    {
        if (progressBarImage == null) yield break;

        float timer = 0f;
        Color startColor = Color.white; // 从白色开始
        Color targetColor = lowResourceColor; // 到低资源颜色

        while (isColorBlinking)
        {
            // 计算正弦波值，在0-1之间变化
            float t = (Mathf.Sin(timer * colorBlinkSpeed) + 1f) * 0.5f;

            // 在白色和低资源颜色之间插值
            progressBarImage.color = Color.Lerp(startColor, targetColor, t);

            // 可以同时控制透明度变化，如果需要的话
            // Color lerpedColor = Color.Lerp(startColor, targetColor, t);
            // lerpedColor.a = Mathf.Lerp(minAlpha, maxAlpha, t);
            // progressBarImage.color = lerpedColor;

            timer += Time.deltaTime;
            yield return null;
        }

        // 确保停止闪烁后颜色恢复正常
        progressBarImage.color = normalColor;
    }

    /// <summary>
    /// 跳动动画协程
    /// </summary>
    private IEnumerator PulseAnimation()
    {


        float timer = 0f;
        Transform targetTransform = barTransform != null ? barTransform : transform;

        while (isPulsing)
        {
            // 使用正弦波创建跳动效果
            float pulseValue = Mathf.Sin(timer * pulseSpeed);
            float scaleFactor = Mathf.Lerp(minPulseScale, maxPulseScale, (pulseValue + 1f) / 2f);

            // 应用缩放
            targetTransform.localScale = originalScale * scaleFactor;
            if (mainUIAnimator != null)
            {
                mainUIAnimator.cullingMode = AnimatorCullingMode.CullCompletely;
            }
            timer += Time.deltaTime;
            yield return null;
        }

        ResetScale();
    }

    /// <summary>
    /// 重置缩放
    /// </summary>
    private void ResetScale()
    {
        Transform targetTransform = barTransform != null ? barTransform : transform;
        targetTransform.localScale = originalScale;
    }

    /// <summary>
    /// 设置进度条颜色
    /// </summary>
    public void SetNormalColor(Color color)
    {
        normalColor = color;
        if (currentPercentage > lowResourceThreshold)
        {
            progressBarImage.color = normalColor;
        }
    }

    /// <summary>
    /// 获取原始颜色
    /// </summary>
    public Color GetOriginalColor()
    {
        return originalColor;
    }

    /// <summary>
    /// 设置低资源颜色
    /// </summary>
    public void SetLowResourceColor(Color color)
    {
        lowResourceColor = color;
        // 如果当前正在闪烁，需要重新开始闪烁以使用新颜色
        if (isColorBlinking)
        {
            StopColorBlink();
            StartColorBlink();
        }
    }

    /// <summary>
    /// 设置颜色闪烁速度
    /// </summary>
    public void SetColorBlinkSpeed(float speed)
    {
        colorBlinkSpeed = speed;
    }

    /// <summary>
    /// 手动控制颜色闪烁
    /// </summary>
    public void SetColorBlink(bool active)
    {
        if (active && !isColorBlinking)
        {
            StartColorBlink();
        }
        else if (!active && isColorBlinking)
        {
            StopColorBlink();
        }
    }
}