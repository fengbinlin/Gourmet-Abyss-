using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;

[RequireComponent(typeof(RectTransform))]
public class UITapBounce : MonoBehaviour, IPointerDownHandler
{
    public static UITapBounce Instance;
    public Animator animator;
    [Header("弹起设置")]

    [SerializeField] private float bounceHeight = 50f;      // 弹起高度
    [SerializeField] private float bounceDuration = 0.3f;   // 弹起动画时间
    [SerializeField] private float bounceOvershoot = 1.1f;  // 过冲效果（弹跳感）
    
    [Header("缓动曲线")]
    [SerializeField] private AnimationCurve bounceCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    
    [Header("点击区域")]
    [SerializeField] private bool clickAnywhere = false;    // 是否点击屏幕任意位置触发
    
    [Header("事件")]
    public UnityEvent onBounceUp;    // 弹起时触发
    public UnityEvent onBounceDown;  // 落下时触发
    
    private RectTransform rectTransform;
    private Vector2 originalPosition;
    private bool isBounced = false;
    private Coroutine bounceCoroutine;
    
    private void Awake()
    {
        Instance = this;
        rectTransform = GetComponent<RectTransform>();
        originalPosition = rectTransform.anchoredPosition;
    }
    
    private void Update()
    {
        // 如果启用点击任意位置触发
        if (Input.GetKeyDown(KeyCode.Tab))
        {
            
            OnTap();
        }
    }
    
    public void OnPointerDown(PointerEventData eventData)
    {
        if (!clickAnywhere)
        {
            
            OnTap();
        }
    }
    
    public void OnTap()
    {
        // 停止正在进行的动画
        if (bounceCoroutine != null)
        {
            StopCoroutine(bounceCoroutine);
        }
        
        if (!isBounced)
        {
            
            // 弹起
            bounceCoroutine = StartCoroutine(BounceUp());
            onBounceUp?.Invoke();
        }
        else
        {
            // 落下
            bounceCoroutine = StartCoroutine(BounceDown());
            onBounceDown?.Invoke();
            
        }
        
        isBounced = !isBounced;
    }
    
    public System.Collections.IEnumerator BounceUp()
    {
        animator.cullingMode=AnimatorCullingMode.CullCompletely;
        Vector2 startPos = rectTransform.anchoredPosition;
        Vector2 targetPos = originalPosition + Vector2.up * bounceHeight;
        
        // 添加过冲效果
        Vector2 overshootPos = targetPos + Vector2.up * (bounceHeight * 0.2f * bounceOvershoot);
        
        float elapsedTime = 0f;
        
        // 第一阶段：快速上升到过冲位置
        while (elapsedTime < bounceDuration * 0.3f)
        {
            elapsedTime += Time.deltaTime;
            float t = elapsedTime / (bounceDuration * 0.3f);
            t = bounceCurve.Evaluate(t);
            rectTransform.anchoredPosition = Vector2.Lerp(startPos, overshootPos, t);
            yield return null;
        }
        
        // 第二阶段：回弹到目标位置
        float remainingTime = bounceDuration * 0.7f;
        while (elapsedTime < bounceDuration)
        {
            elapsedTime += Time.deltaTime;
            float t = (elapsedTime - bounceDuration * 0.3f) / (bounceDuration * 0.7f);
            t = bounceCurve.Evaluate(t);
            rectTransform.anchoredPosition = Vector2.Lerp(overshootPos, targetPos, t);
            yield return null;
        }
        
        rectTransform.anchoredPosition = targetPos;
    }
    
   public System.Collections.IEnumerator BounceDown()
    {
        Vector2 startPos = rectTransform.anchoredPosition;
        Vector2 targetPos = originalPosition;
        
        // 添加轻微的下冲效果
        Vector2 undershootPos = targetPos - Vector2.up * (bounceHeight * 0.1f * bounceOvershoot);
        
        float elapsedTime = 0f;
        
        // 第一阶段：快速下降到下冲位置
        while (elapsedTime < bounceDuration * 0.4f)
        {
            elapsedTime += Time.deltaTime;
            float t = elapsedTime / (bounceDuration * 0.4f);
            t = bounceCurve.Evaluate(t);
            rectTransform.anchoredPosition = Vector2.Lerp(startPos, undershootPos, t);
            yield return null;
        }
        
        // 第二阶段：轻微回弹到原始位置
        float remainingTime = bounceDuration * 0.6f;
        while (elapsedTime < bounceDuration)
        {
            elapsedTime += Time.deltaTime;
            float t = (elapsedTime - bounceDuration * 0.4f) / (bounceDuration * 0.6f);
            t = bounceCurve.Evaluate(t);
            rectTransform.anchoredPosition = Vector2.Lerp(undershootPos, targetPos, t);
            yield return null;
        }
        
        rectTransform.anchoredPosition = targetPos;
        animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
    }
    
    // 公开方法，可以从其他脚本调用
    public void Bounce()
    {
        OnTap();
    }
    
    public void ResetPosition()
    {
        isBounced = false;
        rectTransform.anchoredPosition = originalPosition;
        
        if (bounceCoroutine != null)
        {
            StopCoroutine(bounceCoroutine);
        }
    }
    
    // 在编辑器中测试用
    [ContextMenu("测试弹起")]
    private void TestBounceUp()
    {
        if (!Application.isPlaying)
        {
            Debug.Log("请在运行模式下测试");
            return;
        }
        
        if (!isBounced)
        {
            OnTap();
        }
    }
    
    [ContextMenu("测试落下")]
    private void TestBounceDown()
    {
        if (!Application.isPlaying)
        {
            Debug.Log("请在运行模式下测试");
            return;
        }
        
        if (isBounced)
        {
            OnTap();
        }
    }
}