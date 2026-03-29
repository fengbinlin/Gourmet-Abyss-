using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using System;
using UnityEngine.EventSystems;
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
    [SerializeField] private bool blockNpcClickWhenPointerOverUI = true;  // 鼠标在UI上时不触发NPC点击
    [SerializeField] private bool debugNpcClickRaycast = false;            // 输出点击命中调试日志

    [Header("Hold Progress Settings")]
    [SerializeField] private Image holdProgress;               // 长按进度条
    [SerializeField] private float holdTime = 2f;              // 需要长按的时间
    [SerializeField] private float fillSpeed = 1f;            // 填充速度
    [SerializeField] private float decreaseSpeed = 2f;         // 减少速度


    // 1. 通用交互键按下事件（任何情况下按下E都会触发）
    public event Action OnInteractionPressed;
    // 私有变量
    private Vector3 popupOriginalPosition;
    private Coroutine popupCoroutine;
    private bool isCanvasActive = false;
    private readonly HashSet<Collider> activeBuildingColliders = new HashSet<Collider>(); // 当前仍在触发中的建筑Collider
    private bool isHolding = false; // 是否正在长按
    private float currentProgress = 0f; // 当前进度

    // 当前宝箱引用（普通宝箱）
    private Treasure currentTreasure = null;
    // 当前食谱宝箱引用
    private CookBookTreasure currentCookBookTreasure = null;

    private void Awake()
    {
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

        // 初始化进度条
        if (holdProgress != null)
        {
            holdProgress.fillAmount = 0f;
        }
    }

    void OnEnable()
    {
        isCanvasActive = false;
        isHolding = false;
        currentProgress = 0f;
        activeBuildingColliders.Clear();

        // 停止之前的动画（如果有）
        if (popupCoroutine != null)
        {
            StopCoroutine(popupCoroutine);
        }

        Vector2 targetPos = popupOriginalPosition;
        popupImage.anchoredPosition = targetPos;
        interactionCanvas.gameObject.SetActive(false);
        popupCoroutine = null;

        // 重置进度条
        if (holdProgress != null)
        {
            holdProgress.fillAmount = 0f;
        }
    }

    void OnDisable()
    {
        isCanvasActive = false;
        isHolding = false;
        currentProgress = 0f;
        activeBuildingColliders.Clear();

        // 停止之前的动画（如果有）
        if (popupCoroutine != null)
        {
            StopCoroutine(popupCoroutine);
        }

        Vector2 targetPos = popupOriginalPosition;
        popupImage.anchoredPosition = targetPos;
        interactionCanvas.gameObject.SetActive(false);
        popupCoroutine = null;

        // 重置进度条
        if (holdProgress != null)
        {
            holdProgress.fillAmount = 0f;
        }

        // 清除宝箱引用
        currentTreasure = null;
        currentCookBookTreasure = null;
    }

    private void OnTriggerEnter(Collider other)
    {
        // 检查是否是可交互建筑
        if (IsInteractableBuilding(other))
        {
            activeBuildingColliders.Add(other);
            AudioManager.Instance.PlayAudio("3");
            // 如果Canvas还未激活，激活并播放弹出动画
            if (!isCanvasActive)
            {
                ShowInteractionCanvas();
            }
        }
        // 靠近宝箱逻辑（普通宝箱）
        Treasure treasure = other.gameObject.GetComponent<Treasure>();
        if (treasure)
        {
            if (treasure.isOpen == true)
            {
                return;
            }
            print("进入宝箱");
            currentTreasure = treasure;
            holdTime = currentTreasure.timeNeedToHold;
            AudioManager.Instance.PlayAudio("3");
            // 如果Canvas还未激活，激活并播放弹出动画
            if (!isCanvasActive)
            {
                ShowInteractionCanvas();
            }
        }

        // 靠近食谱宝箱逻辑（CookBookTreasure）
        CookBookTreasure cookBookTreasure = other.gameObject.GetComponent<CookBookTreasure>();
        if (cookBookTreasure)
        {
            if (cookBookTreasure.isOpen == true)
            {
                return;
            }
            print("进入食谱宝箱");
            currentCookBookTreasure = cookBookTreasure;
            holdTime = currentCookBookTreasure.timeNeedToHold;
            AudioManager.Instance.PlayAudio("3");
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
            activeBuildingColliders.Remove(other);

            // 如果离开了所有建筑，强制隐藏Canvas（无论是否被E切换过）
            if (activeBuildingColliders.Count == 0)
                HideInteractionCanvas();
        }
        // 离开宝箱逻辑（普通宝箱）
        if (other.gameObject.GetComponent<Treasure>())
        {
            // 重置长按状态
            isHolding = false;
            currentProgress = 0f;

            if (holdProgress != null)
            {
                holdProgress.fillAmount = 0f;
            }

            currentTreasure = null;

            // 离开该宝箱区域：强制隐藏Canvas，并清理长按状态
            HideInteractionCanvas();
        }

        // 离开食谱宝箱逻辑（CookBookTreasure）
        if (other.gameObject.GetComponent<CookBookTreasure>())
        {
            // 重置长按状态
            isHolding = false;
            currentProgress = 0f;

            if (holdProgress != null)
            {
                holdProgress.fillAmount = 0f;
            }

            currentCookBookTreasure = null;

            // 离开该宝箱区域：强制隐藏Canvas，并清理长按状态
            HideInteractionCanvas();
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
        isHolding = false;
        currentProgress = 0f;

        // 停止之前的动画（如果有）
        if (popupCoroutine != null)
        {
            StopCoroutine(popupCoroutine);
        }

        // 开始回弹动画
        popupCoroutine = StartCoroutine(PopupAnimation(false));
    }

    /// <summary>
    /// E 只用于“可交互建筑提示 Canvas”的显示/隐藏切换。
    /// 宝箱长按开箱仍保留原逻辑（因为宝箱也需要用 E 进行长按）。
    /// </summary>
    private bool CanToggleCanvasWithE()
    {
        // 仅当在可交互建筑区域内时才允许切换
        if (activeBuildingColliders.Count <= 0) return false;

        // 当前在宝箱长按交互时，不用 E 去切换 Canvas（避免冲突）
        if (currentTreasure != null) return false;
        if (currentCookBookTreasure != null) return false;

        return interactionCanvas != null;
    }

    private void ToggleInteractionCanvasWithE()
    {
        if (isCanvasActive) HideInteractionCanvas();
        else ShowInteractionCanvas();
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
        activeBuildingColliders.Clear();
        HideInteractionCanvas();
    }

    public void ForceShowCanvas()
    {
        activeBuildingColliders.Clear();
        ShowInteractionCanvas();
    }

    void Update()
    {
        CleanupInvalidBuildingColliders();

        if (Input.GetMouseButtonDown(0))
        {
            if (blockNpcClickWhenPointerOverUI && EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            {
                if (debugNpcClickRaycast) Debug.Log("[NPC Click] Pointer is over UI, skip NPC raycast.");
                return;
            }

            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            RaycastHit[] hits = Physics.RaycastAll(ray, 10000f);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            foreach (var hit in hits)
            {
                CustomerNPC npc = null;
                if (hit.collider != null)
                {
                    // 兼容：点击命中的是子物体Collider时，从父节点查找CustomerNPC
                    npc = hit.collider.GetComponent<CustomerNPC>();
                    if (npc == null) npc = hit.collider.GetComponentInParent<CustomerNPC>();
                    if (npc == null && hit.collider.attachedRigidbody != null)
                    {
                        npc = hit.collider.attachedRigidbody.GetComponent<CustomerNPC>();
                        if (npc == null) npc = hit.collider.attachedRigidbody.GetComponentInParent<CustomerNPC>();
                    }
                }

                if (debugNpcClickRaycast && hit.collider != null)
                {
                    Debug.Log($"[NPC Click] hit={hit.collider.name}, layer={LayerMask.LayerToName(hit.collider.gameObject.layer)}, npc={(npc != null ? npc.name : "null")}");
                }

                if (npc != null)
                {
                    npc.ClickCustomer();
                    break;
                }
            }
        }
        
        // 可交互建筑提示：E 进行 Canvas 显示/隐藏切换（宝箱长按场景不切）
        bool didToggleCanvasByE = false;
        if (Input.GetKeyDown(KeyCode.E) && CanToggleCanvasWithE())
        {
            ToggleInteractionCanvasWithE();
            didToggleCanvasByE = true;
        }

        // 通用交互事件：仅当没有作为“切换Canvas提示”时才触发
        if (!didToggleCanvasByE && Input.GetKeyDown(KeyCode.E))
            OnInteractionPressed?.Invoke();

        // 处理宝箱长按交互（普通宝箱）
        if (currentTreasure != null && holdProgress != null)
        {
            if (currentTreasure.isOpen == true)
            {
                return;
            }
            // 检测按键按下
            if (Input.GetKeyDown(KeyCode.E))
            {
                isHolding = true;
            }

            // 检测按键抬起
            if (Input.GetKeyUp(KeyCode.E))
            {
                isHolding = false;
            }

            // 更新进度
            if (isHolding)
            {
                // 增加进度
                currentProgress += fillSpeed * Time.deltaTime;
                currentProgress = Mathf.Clamp(currentProgress, 0f, holdTime);

                // 更新UI
                holdProgress.fillAmount = currentProgress / holdTime;

                // 检查是否完成
                if (currentProgress >= holdTime)
                {
                    // 打开宝箱
                    currentTreasure.Open();

                    // 重置状态
                    isHolding = false;
                    currentProgress = 0f;
                    holdProgress.fillAmount = 0f;
                }
            }
            else
            {
                // 减少进度
                if (currentProgress > 0f)
                {
                    currentProgress -= decreaseSpeed * Time.deltaTime;
                    currentProgress = Mathf.Clamp(currentProgress, 0f, holdTime);

                    // 更新UI
                    holdProgress.fillAmount = currentProgress / holdTime;
                }
            }
        }
        // 处理食谱宝箱长按交互（CookBookTreasure）
        else if (currentCookBookTreasure != null && holdProgress != null)
        {
            if (currentCookBookTreasure.isOpen == true)
            {
                return;
            }

            // 检测按键按下
            if (Input.GetKeyDown(KeyCode.E))
            {
                isHolding = true;
            }

            // 检测按键抬起
            if (Input.GetKeyUp(KeyCode.E))
            {
                isHolding = false;
            }

            // 更新进度
            if (isHolding)
            {
                // 增加进度
                currentProgress += fillSpeed * Time.deltaTime;
                currentProgress = Mathf.Clamp(currentProgress, 0f, holdTime);

                // 更新UI
                holdProgress.fillAmount = currentProgress / holdTime;

                // 检查是否完成
                if (currentProgress >= holdTime*0.9f)
                {
                    // 打开食谱宝箱
                    currentCookBookTreasure.Open();

                    // 重置状态
                    isHolding = false;
                    currentProgress = 0f;
                    holdProgress.fillAmount = 0f;
                }
            }
            else
            {
                // 减少进度
                if (currentProgress > 0f)
                {
                    currentProgress -= decreaseSpeed * Time.deltaTime;
                    currentProgress = Mathf.Clamp(currentProgress, 0f, holdTime);

                    // 更新UI
                    holdProgress.fillAmount = currentProgress / holdTime;
                }
            }
        }
        else if (holdProgress != null)
        {
            // 不在宝箱区域时重置进度
            if (currentProgress > 0f)
            {
                currentProgress = 0f;
                holdProgress.fillAmount = 0f;
            }
        }
    }

    public bool IsCanvasActive => isCanvasActive;

    private void CleanupInvalidBuildingColliders()
    {
        if (activeBuildingColliders.Count == 0) return;

        // 触发器被禁用/销毁/改层时可能不会触发 OnTriggerExit，这里兜底清理
        activeBuildingColliders.RemoveWhere(c =>
            c == null ||
            !c.enabled ||
            !c.gameObject.activeInHierarchy ||
            !IsInteractableBuilding(c));

        if (activeBuildingColliders.Count == 0 && isCanvasActive)
        {
            HideInteractionCanvas();
        }
    }
}