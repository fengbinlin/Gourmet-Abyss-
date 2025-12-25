using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Video;
using DG.Tweening; // 添加DoTween命名空间

public class HomeCavecar : MonoBehaviour
{
    public static HomeCavecar homeCavecar;
    public bool canUse = true;
    public GameObject MapUI;
    public bool isPlayerEnter = false;
    
    [Header("UI动画设置")]
    [SerializeField] private float showAnimationDuration = 0.5f; // 显示动画时长
    [SerializeField] private float hideAnimationDuration = 0.3f; // 隐藏动画时长
    [SerializeField] private float showScaleMultiplier = 1.2f; // 显示动画的缩放倍数
    [SerializeField] private float hideScaleMultiplier = 1.1f; // 隐藏动画的缩放倍数
    [SerializeField] private Ease showEase = Ease.OutBack; // 显示动画缓动类型
    [SerializeField] private Ease hideEase = Ease.InBack; // 隐藏动画缓动类型
    
    [Header("按键设置")]
    [SerializeField] private KeyCode openKey = KeyCode.E; // 打开面板按键
    [SerializeField] private KeyCode closeKey = KeyCode.Escape; // 关闭面板按键
    
    // 私有变量
    private RectTransform mapUIRectTransform; // UI的RectTransform
    private CanvasGroup mapUICanvasGroup; // UI的CanvasGroup
    private Vector3 originalUIScale; // UI原始大小
    private Tween currentUITween; // 当前UI动画
    private bool isUIActive = false; // UI是否激活
    private bool isAnimating = false; // 是否正在播放动画

    // Start is called before the first frame update
    void Start()
    {
        homeCavecar = this;
        
        // 初始化UI动画
        if (MapUI != null)
        {
            // 确保UI处于激活状态以便获取组件
            MapUI.SetActive(true);
            
            // 获取RectTransform组件
            mapUIRectTransform = MapUI.GetComponent<RectTransform>();
            if (mapUIRectTransform == null)
            {
                Debug.LogWarning("MapUI没有RectTransform组件，无法播放动画！");
            }
            
            // 获取或添加CanvasGroup组件
            mapUICanvasGroup = MapUI.GetComponent<CanvasGroup>();
            if (mapUICanvasGroup == null)
            {
                mapUICanvasGroup = MapUI.AddComponent<CanvasGroup>();
            }
            
            // 保存原始大小
            originalUIScale = mapUIRectTransform.localScale;
            
            // 初始化UI状态
            mapUIRectTransform.localScale = Vector3.zero;
            mapUICanvasGroup.alpha = 0f;
            
            // 关闭UI
            MapUI.SetActive(false);
        }
    }

    // Update is called once per frame
    void Update()
    {
        // 如果玩家进入范围，可以按E键打开/关闭面板
        if (isPlayerEnter && Input.GetKeyDown(openKey) && !isAnimating)
        {
            if (!isUIActive)
            {
                ShowMapUI();
            }
            else
            {
                HideMapUI();
            }
        }
        
        // 无论玩家是否在范围内，按下ESC键都可以关闭面板
        if (Input.GetKeyDown(closeKey) && isUIActive && !isAnimating)
        {
            HideMapUI();
        }
    }
    
    void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.CompareTag("Player"))
        {
            if (canUse)
            {
                isPlayerEnter = true;
                // 这里可以添加进入范围的提示效果
            }
        }
    }
    
    void OnTriggerExit(Collider other)
    {
        if (other.gameObject.CompareTag("Player"))
        {
            isPlayerEnter = false;
            HideMapUI();
        }
    }
    
    // 显示地图UI - 使用DoTween动画
    private void ShowMapUI()
    {
        if (MapUI == null || mapUIRectTransform == null || mapUICanvasGroup == null) return;
        if (isUIActive || isAnimating) return;
        
        isAnimating = true;
        isUIActive = true;
        
        // 激活UI
        MapUI.SetActive(true);
        
        // 停止任何正在进行的动画
        if (currentUITween != null && currentUITween.IsActive())
        {
            currentUITween.Kill();
        }
        
        // 设置初始状态
        mapUIRectTransform.localScale = Vector3.zero;
        mapUICanvasGroup.alpha = 0f;
        
        // 计算动画目标大小
        Vector3 targetScale = originalUIScale;
        Vector3 overshootScale = originalUIScale * showScaleMultiplier;
        
        // 创建显示动画序列
        Sequence showSequence = DOTween.Sequence();
        
        // 第一步：弹出到稍大的尺寸
        showSequence.Append(mapUIRectTransform.DOScale(overshootScale, showAnimationDuration * 0.6f)
            .SetEase(showEase));
        
        showSequence.Join(mapUICanvasGroup.DOFade(1f, showAnimationDuration * 0.4f));
        
        // 第二步：回弹到原始大小
        showSequence.Append(mapUIRectTransform.DOScale(targetScale, showAnimationDuration * 0.4f)
            .SetEase(Ease.OutBack));
        
        // 设置动画完成回调
        showSequence.OnComplete(() => {
            isAnimating = false;
        });
        
        currentUITween = showSequence;
    }
    
    // 隐藏地图UI - 使用DoTween动画
    private void HideMapUI()
    {
        if (MapUI == null || mapUIRectTransform == null || mapUICanvasGroup == null) return;
        if (!isUIActive || isAnimating) return;
        
        isAnimating = true;
        isUIActive = false;
        
        // 停止任何正在进行的动画
        if (currentUITween != null && currentUITween.IsActive())
        {
            currentUITween.Kill();
        }
        
        // 获取当前大小
        Vector3 currentScale = mapUIRectTransform.localScale;
        
        // 计算初始隐藏缩放
        Vector3 initialHideScale = currentScale * hideScaleMultiplier;
        
        // 创建隐藏动画序列
        Sequence hideSequence = DOTween.Sequence();
        
        // 第一步：先稍微放大一点
        hideSequence.Append(mapUIRectTransform.DOScale(initialHideScale, hideAnimationDuration * 0.2f)
            .SetEase(hideEase));
        
        hideSequence.Join(mapUICanvasGroup.DOFade(0.8f, hideAnimationDuration * 0.2f));
        
        // 第二步：缩小到0
        hideSequence.Append(mapUIRectTransform.DOScale(Vector3.zero, hideAnimationDuration * 0.8f)
            .SetEase(hideEase));
        
        hideSequence.Join(mapUICanvasGroup.DOFade(0f, hideAnimationDuration * 0.6f));
        
        // 设置动画完成回调
        hideSequence.OnComplete(() => {
            MapUI.SetActive(false);
            isAnimating = false;
        });
        
        currentUITween = hideSequence;
    }
    
    // 强制立即隐藏UI（不播放动画）
    public void ForceHideUI()
    {
        if (currentUITween != null && currentUITween.IsActive())
        {
            currentUITween.Kill();
        }
        
        if (MapUI != null)
        {
            if (mapUIRectTransform != null)
            {
                mapUIRectTransform.localScale = Vector3.zero;
            }
            if (mapUICanvasGroup != null)
            {
                mapUICanvasGroup.alpha = 0f;
            }
            MapUI.SetActive(false);
        }
        
        isUIActive = false;
        isAnimating = false;
    }
    
    // 公开方法：外部调用关闭面板
    public void CloseMapUI()
    {
        HideMapUI();
    }
    
    // 公开方法：外部调用打开面板
    public void OpenMapUI()
    {
        ShowMapUI();
    }
    
    // 检查UI是否处于激活状态
    public bool IsMapUIActive()
    {
        return isUIActive;
    }
    
    // 在销毁对象时清理动画
    private void OnDestroy()
    {
        if (currentUITween != null && currentUITween.IsActive())
        {
            currentUITween.Kill();
        }
    }
}