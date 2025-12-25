using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using System.Collections;

public class SaturationTransition : MonoBehaviour
{
    [Header("过渡设置")]
    [Tooltip("过渡持续时间（秒）")]
    [SerializeField] private float transitionDuration = 2.0f;
    
    [Tooltip("起始饱和度")]
    [SerializeField] private float startSaturation = 0f;
    
    [Tooltip("目标饱和度")]
    [SerializeField] private float targetSaturation = 3f;
    
    [Header("延迟设置")]
    [Tooltip("在设置开始饱和度后，延迟多久开始渐变（秒）")]
    [SerializeField] private float startDelay = 1.0f;
    
    [Header("引用")]
    [Tooltip("后处理体积组件")]
    [SerializeField] private Volume volume;
    
    // 私有变量
    private ColorAdjustments colorAdjustments;
    private float currentSaturation;
    private float transitionTimer;
    private bool isTransitioning = false;
    private float transitionFrom;
    private float transitionTo;
    private Coroutine delayedTransitionCoroutine;
    
    void Start()
    {
        // 如果没有指定Volume，尝试从当前游戏对象获取
        if (volume == null)
        {
            volume = GetComponent<Volume>();
        }
        
        // 检查Volume组件
        if (volume == null)
        {
            Debug.LogError("未找到Volume组件！请将此脚本附加到带有Volume组件的游戏对象上。");
            return;
        }
        
        // 获取或添加ColorAdjustments覆盖
        if (!volume.profile.TryGet(out colorAdjustments))
        {
            Debug.LogError("Volume配置文件中未找到ColorAdjustments覆盖！请确保已添加Color Adjustments效果。");
            return;
        }
        
        // 确保ColorAdjustments被激活
        colorAdjustments.active = true;
        
        // 初始化饱和度
        currentSaturation = startSaturation;
        colorAdjustments.saturation.value = startSaturation;
        
        // 开始带延迟的过渡
        StartDelayedTransition();
    }
    
    void Update()
    {
        if (!isTransitioning || colorAdjustments == null) return;
        
        // 更新计时器
        transitionTimer += Time.deltaTime;
        
        // 计算过渡进度（0到1之间）
        float progress = Mathf.Clamp01(transitionTimer / transitionDuration);
        
        // 使用线性插值更新饱和度
        currentSaturation = Mathf.Lerp(transitionFrom, transitionTo, progress);
        colorAdjustments.saturation.value = currentSaturation;
        
        // 检查过渡是否完成
        if (progress >= 1f)
        {
            isTransitioning = false;
            //Debug.Log($"饱和度过渡完成！当前饱和度: {currentSaturation}");
        }
    }
    
    /// <summary>
    /// 开始带延迟的饱和度过渡
    /// </summary>
    public void StartDelayedTransition()
    {
        if (delayedTransitionCoroutine != null)
        {
            StopCoroutine(delayedTransitionCoroutine);
        }
        delayedTransitionCoroutine = StartCoroutine(DelayedTransitionCoroutine());
    }
    
    /// <summary>
    /// 延迟过渡协程
    /// </summary>
    private IEnumerator DelayedTransitionCoroutine()
    {
        // 设置起始值
        currentSaturation = startSaturation;
        colorAdjustments.saturation.value = startSaturation;
        
        // 等待延迟时间
        yield return new WaitForSeconds(startDelay);
        
        // 开始过渡
        StartTransition(startSaturation, targetSaturation);
    }
    
    /// <summary>
    /// 开始饱和度过渡（从当前饱和度到目标饱和度）
    /// </summary>
    public void StartTransition()
    {
        StartTransition(currentSaturation, targetSaturation);
    }
    
    /// <summary>
    /// 开始饱和度过渡（从当前饱和度到指定值）
    /// </summary>
    public void StartTransition(float toSaturation)
    {
        StartTransition(currentSaturation, toSaturation);
    }
    
    /// <summary>
    /// 开始饱和度过渡（从指定值到指定值）
    /// </summary>
    public void StartTransition(float fromSaturation, float toSaturation)
    {
        if (colorAdjustments == null) return;
        
        transitionTimer = 0f;
        transitionFrom = fromSaturation;
        transitionTo = toSaturation;
        isTransitioning = true;
        
        // 设置起始值
        currentSaturation = fromSaturation;
        colorAdjustments.saturation.value = fromSaturation;
        
        //Debug.Log($"开始饱和度过渡: 从 {fromSaturation} 到 {toSaturation}");
    }
    
    /// <summary>
    /// 带延迟开始饱和度过渡（从指定值到指定值）
    /// </summary>
    public void StartTransitionWithDelay(float fromSaturation, float toSaturation, float delay)
    {
        if (delayedTransitionCoroutine != null)
        {
            StopCoroutine(delayedTransitionCoroutine);
        }
        delayedTransitionCoroutine = StartCoroutine(StartTransitionWithDelayCoroutine(fromSaturation, toSaturation, delay));
    }
    
    /// <summary>
    /// 带延迟的过渡协程
    /// </summary>
    private IEnumerator StartTransitionWithDelayCoroutine(float fromSaturation, float toSaturation, float delay)
    {
        // 设置起始值
        currentSaturation = fromSaturation;
        colorAdjustments.saturation.value = fromSaturation;
        
        // 等待延迟时间
        yield return new WaitForSeconds(delay);
        
        // 开始过渡
        StartTransition(fromSaturation, toSaturation);
    }
    
    /// <summary>
    /// 从饱和状态过渡到不饱和状态
    /// </summary>
    public void TransitionToUnsaturated()
    {
        StartTransition(currentSaturation, startSaturation);
    }
    
    /// <summary>
    /// 从不饱和状态过渡到饱和状态
    /// </summary>
    public void TransitionToSaturated()
    {
        StartTransition(startSaturation, targetSaturation);
    }
    
    /// <summary>
    /// 带延迟从饱和状态过渡到不饱和状态
    /// </summary>
    public void TransitionToUnsaturatedWithDelay(float delay)
    {
        StartTransitionWithDelay(currentSaturation, startSaturation, delay);
    }
    
    /// <summary>
    /// 带延迟从不饱和状态过渡到饱和状态
    /// </summary>
    public void TransitionToSaturatedWithDelay(float delay)
    {
        StartTransitionWithDelay(currentSaturation, targetSaturation, delay);
    }
    
    /// <summary>
    /// 反向过渡：从当前饱和度反向过渡
    /// </summary>
    public void ReverseTransition()
    {
        // 交换起始和目标值
        float temp = transitionFrom;
        transitionFrom = transitionTo;
        transitionTo = temp;
        
        transitionTimer = 0f;
        isTransitioning = true;
        
        //Debug.Log($"反向过渡: 从 {transitionFrom} 到 {transitionTo}");
    }
    
    /// <summary>
    /// 切换到不饱和状态
    /// </summary>
    public void SetToUnsaturated()
    {
        if (colorAdjustments == null) return;
        
        isTransitioning = false;
        currentSaturation = startSaturation;
        colorAdjustments.saturation.value = startSaturation;
        Debug.Log($"切换到不饱和状态: {startSaturation}");
    }
    
    /// <summary>
    /// 切换到饱和状态
    /// </summary>
    public void SetToSaturated()
    {
        if (colorAdjustments == null) return;
        
        isTransitioning = false;
        currentSaturation = targetSaturation;
        colorAdjustments.saturation.value = targetSaturation;
        Debug.Log($"切换到饱和状态: {targetSaturation}");
    }
    
    /// <summary>
    /// 重置过渡到起始状态
    /// </summary>
    public void ResetTransition()
    {
        if (colorAdjustments == null) return;
        
        transitionTimer = 0f;
        isTransitioning = false;
        currentSaturation = startSaturation;
        colorAdjustments.saturation.value = startSaturation;
    }
    
    /// <summary>
    /// 立即设置饱和度到目标值
    /// </summary>
    public void SetSaturationImmediate(float saturation)
    {
        if (colorAdjustments == null) return;
        
        isTransitioning = false;
        currentSaturation = saturation;
        colorAdjustments.saturation.value = saturation;
    }
    
    /// <summary>
    /// 切换饱和状态
    /// </summary>
    public void ToggleSaturation()
    {
        if (currentSaturation >= targetSaturation - 0.1f)
        {
            TransitionToUnsaturated();
        }
        else
        {
            StartTransition();
        }
    }
    
    /// <summary>
    /// 停止当前的延迟过渡
    /// </summary>
    public void StopDelayedTransition()
    {
        if (delayedTransitionCoroutine != null)
        {
            StopCoroutine(delayedTransitionCoroutine);
            delayedTransitionCoroutine = null;
        }
    }
}