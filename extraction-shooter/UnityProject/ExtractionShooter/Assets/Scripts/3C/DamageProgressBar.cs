using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class DamageProgressBar : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Image whiteFill;    // 白色填充
    [SerializeField] private Image redFill;      // 红色填充
    [SerializeField] private float maxWidth = 300f;  // 进度条最大宽度
    [SerializeField] private float height = 20f;     // 进度条高度

    [Header("动画设置")]
    [SerializeField] private float whiteShowTime = 0.3f;  // 白色显示时间
    [SerializeField] private float redDelayTime = 0.2f;   // 红色延迟时间
    [SerializeField] private float redTransitionTime = 0.3f;  // 红色过渡时间

    private RectTransform whiteRect;
    private RectTransform redRect;
    private float currentDamageRatio = 0f;  // 当前累计伤害比例
    private Coroutine animationCoroutine;

    private void Awake()
    {
        if (whiteFill != null)
        {
            whiteRect = whiteFill.GetComponent<RectTransform>();
            whiteRect.sizeDelta = new Vector2(0, height);
        }
        
        if (redFill != null)
        {
            redRect = redFill.GetComponent<RectTransform>();
            redRect.sizeDelta = new Vector2(0, height);
        }
    }

    // 受到伤害时调用
    public void AddDamage(float damage, float maxHealth)
    {
        float damageRatio = damage / maxHealth;
        float newDamageRatio = currentDamageRatio + damageRatio;
        
        if (newDamageRatio > 1f) newDamageRatio = 1f;
        
        if (animationCoroutine != null)
        {
            StopCoroutine(animationCoroutine);
        }
        
        animationCoroutine = StartCoroutine(DamageAnimation(currentDamageRatio, newDamageRatio));
        currentDamageRatio = newDamageRatio;
    }

    private IEnumerator DamageAnimation(float fromRatio, float toRatio)
    {
        float fromWidth = fromRatio * maxWidth;
        float toWidth = toRatio * maxWidth;
        float currentWhiteWidth = 0f;
        
        // 白色条从0扩展到目标宽度
        float elapsedTime = 0f;
        while (elapsedTime < whiteShowTime)
        {
            elapsedTime += Time.deltaTime;
            float t = elapsedTime / whiteShowTime;
            currentWhiteWidth = Mathf.Lerp(0, toWidth, t);
            whiteRect.sizeDelta = new Vector2(currentWhiteWidth, height);
            yield return null;
        }
        
        whiteRect.sizeDelta = new Vector2(toWidth, height);
        
        // 等待一段时间
        yield return new WaitForSeconds(redDelayTime);
        
        // 红色条从当前宽度扩展到目标宽度
        elapsedTime = 0f;
        while (elapsedTime < redTransitionTime)
        {
            elapsedTime += Time.deltaTime;
            float t = elapsedTime / redTransitionTime;
            float currentRedWidth = Mathf.Lerp(fromWidth, toWidth, t);
            redRect.sizeDelta = new Vector2(currentRedWidth, height);
            yield return null;
        }
        
        redRect.sizeDelta = new Vector2(toWidth, height);
        
        // 白色条收缩消失
        elapsedTime = 0f;
        while (elapsedTime < 0.1f)
        {
            elapsedTime += Time.deltaTime;
            float t = elapsedTime / 0.1f;
            float newWhiteWidth = Mathf.Lerp(toWidth, 0, t);
            whiteRect.sizeDelta = new Vector2(newWhiteWidth, height);
            yield return null;
        }
        
        whiteRect.sizeDelta = new Vector2(0, height);
    }

    // 重置进度条
    public void Reset()
    {
        if (animationCoroutine != null)
        {
            StopCoroutine(animationCoroutine);
        }
        
        if (whiteRect != null)
        {
            whiteRect.sizeDelta = new Vector2(0, height);
        }
        
        if (redRect != null)
        {
            redRect.sizeDelta = new Vector2(0, height);
        }
        
        currentDamageRatio = 0f;
    }
}