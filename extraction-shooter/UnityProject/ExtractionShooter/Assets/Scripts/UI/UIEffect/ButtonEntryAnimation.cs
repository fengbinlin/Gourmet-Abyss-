using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class ButtonEntryAnimation : MonoBehaviour
{
    [Header("入场动画设置")]
    [SerializeField] private float entryDelay = 0.1f;
    [SerializeField] private float slideDuration = 0.7f;
    [SerializeField] private float rotationDuration = 0.7f;
    [SerializeField] private float startRotationAngle = 85f;
    
    [Header("动画曲线")]
    [SerializeField] private AnimationCurve slideCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    [SerializeField] private AnimationCurve rotationCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    
    private RectTransform rectTransform;
    private Vector2 targetPosition;
    private Vector2 startPosition;
    
    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        targetPosition = rectTransform.anchoredPosition;
    }
    
    public void PlayEntryAnimation(float delay = 0f)
    {
        StartCoroutine(EntryAnimation(delay));
    }
    
    private IEnumerator EntryAnimation(float delay)
    {
        if (delay > 0)
        {
            yield return new WaitForSeconds(delay);
        }
        
        startPosition = new Vector2(300f, targetPosition.y);
        rectTransform.anchoredPosition = startPosition;
        rectTransform.localRotation = Quaternion.Euler(startRotationAngle, 0f, 0f);
        
        float slideTime = 0f;
        float rotationTime = 0f;
        
        // 使用弹性曲线计算位置动画
        while (slideTime < slideDuration || rotationTime < rotationDuration)
        {
            if (slideTime < slideDuration)
            {
                slideTime += Time.deltaTime;
                float t = slideCurve.Evaluate(slideTime / slideDuration);
                
                // 添加弹性效果
                float elasticT = ElasticOut(t, 1.5f, 0.8f);
                rectTransform.anchoredPosition = Vector2.Lerp(startPosition, targetPosition, elasticT);
            }
            
            if (rotationTime < rotationDuration)
            {
                rotationTime += Time.deltaTime;
                float t = rotationCurve.Evaluate(rotationTime / rotationDuration);
                
                // 添加弹性效果
                float elasticT = ElasticOut(t, 1.5f, 0.8f);
                float rotationAngle = Mathf.Lerp(startRotationAngle, 0f, elasticT);
                rectTransform.localRotation = Quaternion.Euler(rotationAngle, 0f, 0f);
            }
            
            yield return null;
        }
        
        // 确保最终状态
        rectTransform.anchoredPosition = targetPosition;
        rectTransform.localRotation = Quaternion.identity;
    }
    
    // 弹性动画函数
    private float ElasticOut(float t, float amplitude, float period)
    {
        if (t == 0) return 0;
        if (t == 1) return 1;
        
        float s = period / (2 * Mathf.PI) * Mathf.Asin(1 / amplitude);
        return amplitude * Mathf.Pow(2, -10 * t) * Mathf.Sin((t - s) * (2 * Mathf.PI) / period) + 1;
    }
    
    public void ResetToStart()
    {
        if (rectTransform != null)
        {
            rectTransform.anchoredPosition = new Vector2(300f, targetPosition.y);
            rectTransform.localRotation = Quaternion.Euler(startRotationAngle, 0f, 0f);
        }
    }
}