using UnityEngine;
using DG.Tweening;

/// <summary>
/// 通用 UI 面板动画控制器。
/// 参考 ShopInteraction 的 ShowShopUI/HideShopUI，提供同样的动态效果。
/// 将本脚本挂在任意对象上，拖入需要显示/隐藏的 UI 根节点，然后在其他脚本或按钮事件中调用 ShowUI()/HideUI()。
/// </summary>
public class UIAnimatedPanelController : MonoBehaviour
{
    [Header("目标 UI 根节点")]
    [SerializeField] public GameObject targetUI;

    [Header("动画设置")]
    [SerializeField] private float showScaleMultiplier = 1.2f;
    [SerializeField] private float hideScaleMultiplier = 1.1f;
    [SerializeField] private float showAnimationDuration = 0.5f;
    [SerializeField] private float hideAnimationDuration = 0.3f;
    [SerializeField] private Ease showEase = Ease.OutBack;
    [SerializeField] private Ease hideEase = Ease.InBack;

    private RectTransform uiRectTransform;
    private CanvasGroup canvasGroup;
    private Tween currentTween;
    private Vector3 originalScale;
    private bool isShowing = false;

    private void Awake()
    {
        if (targetUI == null) return;

        targetUI.SetActive(true);
        uiRectTransform = targetUI.GetComponent<RectTransform>();
        if (uiRectTransform == null)
        {
            uiRectTransform = targetUI.AddComponent<RectTransform>();
        }

        canvasGroup = targetUI.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = targetUI.AddComponent<CanvasGroup>();
        }

        originalScale = uiRectTransform.localScale == Vector3.zero ? Vector3.one : uiRectTransform.localScale;
        uiRectTransform.localScale = Vector3.zero;
        canvasGroup.alpha = 0f;
        targetUI.SetActive(false);
    }

    public void ShowUI()
    {
        if (targetUI == null || uiRectTransform == null || canvasGroup == null) return;
        if (isShowing) return;

        isShowing = true;
        targetUI.SetActive(true);

        if (currentTween != null && currentTween.IsActive())
        {
            currentTween.Kill();
        }

        uiRectTransform.localScale = Vector3.zero;
        canvasGroup.alpha = 0f;

        Vector3 targetScale = originalScale;
        Vector3 overshootScale = originalScale * showScaleMultiplier;

        Sequence seq = DOTween.Sequence();
        seq.Append(uiRectTransform.DOScale(overshootScale, showAnimationDuration * 0.6f).SetEase(showEase));
        seq.Join(canvasGroup.DOFade(1f, showAnimationDuration * 0.4f));
        seq.Append(uiRectTransform.DOScale(targetScale, showAnimationDuration * 0.4f).SetEase(showEase));
        seq.OnComplete(() => { isShowing = true; });

        currentTween = seq;
    }

    public void HideUI()
    {
        Debug.LogError("关闭UI");
        if (targetUI == null || uiRectTransform == null || canvasGroup == null) return;
        if (!isShowing && !targetUI.activeSelf) return;

        isShowing = false;

        if (currentTween != null && currentTween.IsActive())
        {
            currentTween.Kill();
        }

        Vector3 initialHideScale = originalScale * hideScaleMultiplier;
        Vector3 currentScale = uiRectTransform.localScale;

        Sequence seq = DOTween.Sequence();
        seq.Append(uiRectTransform
            .DOScale(initialHideScale, hideAnimationDuration * 0.2f)
            .From(currentScale)
            .SetEase(hideEase));
        seq.Join(canvasGroup.DOFade(0.8f, hideAnimationDuration * 0.2f));
        seq.Append(uiRectTransform
            .DOScale(Vector3.zero, hideAnimationDuration * 0.8f)
            .SetEase(hideEase));
        seq.Join(canvasGroup.DOFade(0f, hideAnimationDuration * 0.6f));
        seq.OnComplete(() => { targetUI.SetActive(false); });

        currentTween = seq;
    }

    private void OnDestroy()
    {
        if (currentTween != null && currentTween.IsActive())
        {
            currentTween.Kill();
        }
    }
}

