// HomeCavecar.cs
using UnityEngine;
using DG.Tweening;

public class HomeCavecar : MonoBehaviour
{
    public static HomeCavecar homeCavecar;
    public bool canUse = true;
    public GameObject MapUI;
    public bool isPlayerEnter = false;
    
    [Header("UI动画设置")]
    [SerializeField] private float showAnimationDuration = 0.5f;
    [SerializeField] private float hideAnimationDuration = 0.3f;
    [SerializeField] private float showScaleMultiplier = 1.2f;
    
    // 颜色过渡组件引用
    private VehicleColorTransition colorTransition;
    
    private RectTransform mapUIRectTransform;
    private CanvasGroup mapUICanvasGroup;
    private Vector3 originalUIScale;
    private Tween currentUITween;
    private bool isUIActive = false;
    private bool isAnimating = false;

    private void Start()
    {
        homeCavecar = this;
        
        // 获取颜色过渡组件
        colorTransition = GetComponent<VehicleColorTransition>();
        
        InitializeMapUI();
    }

    private void Update()
    {
        if (isPlayerEnter && Input.GetKeyDown(KeyCode.E) && !isAnimating && canUse)
        {
            ToggleMapUI();
        }
        
        if (Input.GetKeyDown(KeyCode.Escape) && isUIActive && !isAnimating)
        {
            HideMapUI();
        }
    }
    
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player") && canUse)
        {
            GetComponent<InteractiveFeedback>()?.PlayFeedback();
            isPlayerEnter = true;
        }
    }
    
    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            isPlayerEnter = false;
            HideMapUI();
        }
    }
    
    private void InitializeMapUI()
    {
        if (MapUI == null) return;
        
        MapUI.SetActive(true);
        
        mapUIRectTransform = MapUI.GetComponent<RectTransform>();
        mapUICanvasGroup = MapUI.GetComponent<CanvasGroup>() ?? MapUI.AddComponent<CanvasGroup>();
        
        originalUIScale = mapUIRectTransform.localScale;
        mapUIRectTransform.localScale = Vector3.zero;
        mapUICanvasGroup.alpha = 0f;
        MapUI.SetActive(false);
    }
    
    private void ToggleMapUI()
    {
        if (isUIActive)
        {
            HideMapUI();
        }
        else
        {
            ShowMapUI();
        }
    }
    
    private void ShowMapUI()
    {
        if (MapUI == null || isUIActive || isAnimating) return;
        
        isAnimating = true;
        isUIActive = true;
        MapUI.SetActive(true);
        
        if (currentUITween != null && currentUITween.IsActive())
        {
            currentUITween.Kill();
        }
        
        Vector3 overshootScale = originalUIScale * showScaleMultiplier;
        
        Sequence sequence = DOTween.Sequence();
        sequence.Append(mapUIRectTransform.DOScale(overshootScale, showAnimationDuration * 0.6f)
            .SetEase(Ease.OutBack));
        sequence.Join(mapUICanvasGroup.DOFade(1f, showAnimationDuration * 0.4f));
        sequence.Append(mapUIRectTransform.DOScale(originalUIScale, showAnimationDuration * 0.4f)
            .SetEase(Ease.OutBack));
        sequence.OnComplete(() => isAnimating = false);
        
        currentUITween = sequence;
    }
    
    private void HideMapUI()
    {
        if (MapUI == null || !isUIActive || isAnimating) return;
        
        isAnimating = true;
        isUIActive = false;
        
        if (currentUITween != null && currentUITween.IsActive())
        {
            currentUITween.Kill();
        }
        
        Vector3 initialScale = mapUIRectTransform.localScale * 1.1f;
        
        Sequence sequence = DOTween.Sequence();
        sequence.Append(mapUIRectTransform.DOScale(initialScale, hideAnimationDuration * 0.2f)
            .SetEase(Ease.InBack));
        sequence.Join(mapUICanvasGroup.DOFade(0.8f, hideAnimationDuration * 0.2f));
        sequence.Append(mapUIRectTransform.DOScale(Vector3.zero, hideAnimationDuration * 0.8f)
            .SetEase(Ease.InBack));
        sequence.Join(mapUICanvasGroup.DOFade(0f, hideAnimationDuration * 0.6f));
        sequence.OnComplete(() => {
            MapUI.SetActive(false);
            isAnimating = false;
        });
        
        currentUITween = sequence;
    }
    
    public void CloseMapUI()
    {
        HideMapUI();
    }
    
    public void OpenMapUI()
    {
        ShowMapUI();
    }
    
    public bool IsMapUIActive()
    {
        return isUIActive;
    }
    
    private void OnDestroy()
    {
        if (currentUITween != null && currentUITween.IsActive())
        {
            currentUITween.Kill();
        }
    }
}