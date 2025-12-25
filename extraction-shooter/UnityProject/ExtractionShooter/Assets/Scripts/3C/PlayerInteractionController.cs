using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Collider))]
public class PlayerInteractionController : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Canvas interactionCanvas;           // World Space Canvas
    [SerializeField] private RectTransform popupImage;          // 需要弹出的Image物体
    [SerializeField] private Image popupIcon;                   // 可选的图标Image
    
    [Header("Popup Settings")]
    [SerializeField] private float popupHeight = 50f;          // 弹出高度
    [SerializeField] private float popupDuration = 0.3f;       // 弹出动画时长
    [SerializeField] private AnimationCurve popupCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    [SerializeField] private float resetDuration = 0.2f;       // 回弹动画时长
    
    [Header("Detection Settings")]
    [SerializeField] private LayerMask buildingLayer;          // 建筑层级
    [SerializeField] private string buildingTag = "InteractableBuilding"; // 建筑标签
    
    // 私有变量
    private Vector3 popupOriginalPosition;
    private Coroutine popupCoroutine;
    private bool isCanvasActive = false;
    private int buildingCount = 0;  // 追踪进入的建筑数量

    private void Awake()
    {
        // // 确保Collider是Trigger
        // if (TryGetComponent<Collider>(out var col))
        // {
        //     col.isTrigger = true;
        // }
        
        // 初始化Canvas状态
        if (interactionCanvas != null)
        {
            interactionCanvas.gameObject.SetActive(false);
        }
        
        // 记录原始位置
        if (popupImage != null)
        {
            popupOriginalPosition = popupImage.anchoredPosition;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        // 检查是否是可交互建筑
        if (IsInteractableBuilding(other))
        {
            buildingCount++;
            
            // 如果Canvas还未激活，激活并播放弹出动画
            if (!isCanvasActive)
            {
                ShowInteractionCanvas();
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        // 检查是否离开可交互建筑
        if (IsInteractableBuilding(other))
        {
            buildingCount = Mathf.Max(0, buildingCount - 1);
            
            // 如果离开了所有建筑，隐藏Canvas
            if (buildingCount == 0 && isCanvasActive)
            {
                HideInteractionCanvas();
            }
        }
    }

    private bool IsInteractableBuilding(Collider other)
    {
        // 通过层级和标签双重检查
        bool isInLayer = (buildingLayer.value & (1 << other.gameObject.layer)) != 0;
        bool hasTag = other.CompareTag(buildingTag);
        
        return isInLayer || hasTag;
    }

    private void ShowInteractionCanvas()
    {
        if (interactionCanvas == null || popupImage == null) return;
        
        isCanvasActive = true;
        interactionCanvas.gameObject.SetActive(true);
        
        // 停止之前的动画（如果有）
        if (popupCoroutine != null)
        {
            StopCoroutine(popupCoroutine);
        }
        
        // 开始弹出动画
        popupCoroutine = StartCoroutine(PopupAnimation(true));
    }

    private void HideInteractionCanvas()
    {
        if (interactionCanvas == null || popupImage == null) return;
        
        isCanvasActive = false;
        
        // 停止之前的动画（如果有）
        if (popupCoroutine != null)
        {
            StopCoroutine(popupCoroutine);
        }
        
        // 开始回弹动画
        popupCoroutine = StartCoroutine(PopupAnimation(false));
    }

    private IEnumerator PopupAnimation(bool isPopup)
    {
        Vector2 startPos = popupImage.anchoredPosition;
        Vector2 targetPos = isPopup ? 
            popupOriginalPosition + Vector3.up * popupHeight : 
            popupOriginalPosition;
        
        float duration = isPopup ? popupDuration : resetDuration;
        float elapsedTime = 0f;
        
        while (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;
            float t = Mathf.Clamp01(elapsedTime / duration);
            float curveValue = popupCurve.Evaluate(t);
            
            popupImage.anchoredPosition = Vector2.Lerp(startPos, targetPos, curveValue);
            yield return null;
        }
        
        popupImage.anchoredPosition = targetPos;
        
        // 如果是回弹动画，隐藏Canvas
        if (!isPopup)
        {
            interactionCanvas.gameObject.SetActive(false);
        }
        
        popupCoroutine = null;
    }

    // 公共方法，供外部调用
    public void SetPopupIcon(Sprite icon)
    {
        if (popupIcon != null && icon != null)
        {
            popupIcon.sprite = icon;
        }
    }
    
    public void ForceHideCanvas()
    {
        buildingCount = 0;
        HideInteractionCanvas();
    }
    
    public void ForceShowCanvas()
    {
        buildingCount = 1;
        ShowInteractionCanvas();
    }
    
    public bool IsCanvasActive => isCanvasActive;
}