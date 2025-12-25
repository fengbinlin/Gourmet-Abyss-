using UnityEngine;
using UnityEngine.EventSystems;
using TMPro;
using System.Collections;

public class MenuButtonHoverEffect : MonoBehaviour, 
    IPointerEnterHandler, 
    IPointerExitHandler, 
    IPointerClickHandler
{
    [Header("位置设置")]
    [SerializeField] private float hoverOffset = 25f;
    [SerializeField] private float hoverMoveDuration = 0.2f;
    
    [Header("旋转设置")]
    [SerializeField] private float hoverRotationAngle = -5f;
    [SerializeField] private float rotationDuration = 0.2f;
    [SerializeField] private float bounceIntensity = 8f;
    
    [Header("颜色设置")]
    [SerializeField] private Color hoverColor = new Color(1.2f, 1.2f, 1.2f, 1f);
    
    [Header("点击设置")]
    [SerializeField] private float clickScale = 0.95f;
    [SerializeField] private float clickDuration = 0.1f;
    
    [Header("动画曲线")]
    [SerializeField] private AnimationCurve moveCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    [SerializeField] private AnimationCurve bounceCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    
    // 组件引用
    private RectTransform rectTransform;
    private TextMeshProUGUI buttonText;
    
    // 状态
    private Color normalColor;
    private Vector2 originalPosition;
    private Quaternion originalRotation;
    private Vector3 originalScale;
    private Coroutine currentAnimation;
    private bool isHovering = false;
    
    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        buttonText = GetComponentInChildren<TextMeshProUGUI>();
        
        if (buttonText != null)
        {
            normalColor = buttonText.color;
        }
        
        originalPosition = rectTransform.anchoredPosition;
        originalRotation = rectTransform.localRotation;
        originalScale = rectTransform.localScale;
    }
    
    public void OnPointerEnter(PointerEventData eventData)
    {
        isHovering = true;
        
        if (currentAnimation != null)
        {
            StopCoroutine(currentAnimation);
        }
        
        currentAnimation = StartCoroutine(HoverEnterAnimation());
    }
    
    public void OnPointerExit(PointerEventData eventData)
    {
        isHovering = false;
        
        if (currentAnimation != null)
        {
            StopCoroutine(currentAnimation);
        }
        
        currentAnimation = StartCoroutine(HoverExitAnimation());
    }
    
    public void OnPointerClick(PointerEventData eventData)
    {
        if (currentAnimation != null)
        {
            StopCoroutine(currentAnimation);
        }
        
        currentAnimation = StartCoroutine(ClickAnimation());
    }
    
    private IEnumerator HoverEnterAnimation()
    {
        Vector2 startPos = rectTransform.anchoredPosition;
        Vector2 hoverPos = originalPosition + new Vector2(hoverOffset, 0f);
        Color startColor = buttonText != null ? buttonText.color : Color.white;
        Quaternion startRotation = rectTransform.localRotation;
        Quaternion hoverRotation = Quaternion.Euler(hoverRotationAngle, 0f, 0f);
        
        // 第一步：移动到悬停位置并旋转
        float elapsedTime = 0f;
        
        while (elapsedTime < hoverMoveDuration)
        {
            elapsedTime += Time.unscaledDeltaTime;
            float t = moveCurve.Evaluate(elapsedTime / hoverMoveDuration);
            
            // 移动
            rectTransform.anchoredPosition = Vector2.Lerp(startPos, hoverPos, t);
            
            // 旋转
            rectTransform.localRotation = Quaternion.Lerp(startRotation, hoverRotation, t);
            
            // 颜色变化
            if (buttonText != null)
            {
                buttonText.color = Color.Lerp(startColor, hoverColor, t);
            }
            
            yield return null;
        }
        
        // 第二步：弹性旋转回弹
        elapsedTime = 0f;
        Vector2 currentPos = rectTransform.anchoredPosition;
        startRotation = rectTransform.localRotation;
        startColor = buttonText != null ? buttonText.color : Color.white;
        
        while (elapsedTime < rotationDuration)
        {
            elapsedTime += Time.unscaledDeltaTime;
            float t = elapsedTime / rotationDuration;
            
            // 弹性旋转
            float bounceT = Mathf.Sin(t * Mathf.PI * bounceIntensity) * (1f - t);
            rectTransform.localRotation = Quaternion.Lerp(startRotation, originalRotation, bounceT);
            
            // 颜色保持
            if (buttonText != null)
            {
                buttonText.color = hoverColor;
            }
            
            yield return null;
        }
        
        // 确保最终状态
        rectTransform.anchoredPosition = hoverPos;
        rectTransform.localRotation = originalRotation;
        if (buttonText != null) buttonText.color = hoverColor;
        
        currentAnimation = null;
    }
    
    private IEnumerator HoverExitAnimation()
    {
        Vector2 startPos = rectTransform.anchoredPosition;
        Color startColor = buttonText != null ? buttonText.color : Color.white;
        
        float elapsedTime = 0f;
        
        while (elapsedTime < hoverMoveDuration)
        {
            elapsedTime += Time.unscaledDeltaTime;
            float t = moveCurve.Evaluate(elapsedTime / hoverMoveDuration);
            
            // 移动回原位置
            rectTransform.anchoredPosition = Vector2.Lerp(startPos, originalPosition, t);
            
            // 颜色恢复
            if (buttonText != null)
            {
                buttonText.color = Color.Lerp(startColor, normalColor, t);
            }
            
            yield return null;
        }
        
        // 确保最终状态
        rectTransform.anchoredPosition = originalPosition;
        rectTransform.localRotation = originalRotation;
        if (buttonText != null) buttonText.color = normalColor;
        
        currentAnimation = null;
    }
    
    private IEnumerator ClickAnimation()
    {
        Vector3 startScale = rectTransform.localScale;
        Vector3 targetScale = originalScale * clickScale;
        
        float elapsedTime = 0f;
        
        // 点击缩小
        while (elapsedTime < clickDuration)
        {
            elapsedTime += Time.unscaledDeltaTime;
            float t = elapsedTime / clickDuration;
            float scaleT = Mathf.Sin(t * Mathf.PI); // 正弦曲线实现弹性效果
            float currentScale = Mathf.Lerp(startScale.x, targetScale.x, scaleT);
            rectTransform.localScale = new Vector3(currentScale, currentScale, 1f);
            yield return null;
        }
        
        // 恢复
        elapsedTime = 0f;
        while (elapsedTime < clickDuration)
        {
            elapsedTime += Time.unscaledDeltaTime;
            float t = elapsedTime / clickDuration;
            float scaleT = Mathf.Sin(t * Mathf.PI); // 正弦曲线实现弹性效果
            float currentScale = Mathf.Lerp(targetScale.x, originalScale.x, scaleT);
            rectTransform.localScale = new Vector3(currentScale, currentScale, 1f);
            yield return null;
        }
        
        rectTransform.localScale = originalScale;
        currentAnimation = null;
    }
    
    public void SetOriginalState(Vector2 position, Quaternion rotation)
    {
        originalPosition = position;
        originalRotation = rotation;
        
        if (!isHovering)
        {
            rectTransform.anchoredPosition = position;
            rectTransform.localRotation = rotation;
        }
    }
    
    public void ResetToOriginalState()
    {
        if (currentAnimation != null)
        {
            StopCoroutine(currentAnimation);
            currentAnimation = null;
        }
        
        rectTransform.anchoredPosition = originalPosition;
        rectTransform.localRotation = originalRotation;
        rectTransform.localScale = originalScale;
        
        if (buttonText != null)
        {
            buttonText.color = normalColor;
        }
    }
}