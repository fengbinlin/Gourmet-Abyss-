using Game.Core;
using UnityEngine;
using UnityEngine.Events;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using DG.Tweening;

public class ShopInteraction : MonoSingleton<ShopInteraction>
{
    // 每个场景各一份，后加载的接管——沿用原来的裸赋值语义。
    protected override DuplicatePolicy Duplicate => DuplicatePolicy.OverwriteReference;

    [Header("商店设置")]
    [SerializeField] private float interactionRange = 3f;
    [SerializeField] private KeyCode interactKey = KeyCode.E;
    [SerializeField] private float interactCooldown = 0.5f;

    [Header("UI设置")]
    [SerializeField] private GameObject shopUICanvas;
    [SerializeField] private TextMeshProUGUI interactionText;
    [SerializeField] private string interactionMessage = "按 E 出售物品";
    [SerializeField] private string shopEmptyMessage = "商店已空";
    [SerializeField] private string noItemsMessage = "背包为空";
    [SerializeField] private string shopFullMessage = "商店已满";

    [Header("UI动画设置")]
    [SerializeField] private float fadeSpeed = 5f;
    [SerializeField] private float showScaleMultiplier = 1.2f;
    [SerializeField] private float hideScaleMultiplier = 1.1f;
    [SerializeField] private float showAnimationDuration = 0.5f;
    [SerializeField] private float hideAnimationDuration = 0.3f;
    [SerializeField] private Ease showEase = Ease.OutBack;
    [SerializeField] private Ease hideEase = Ease.InBack;

    [Header("音频")]
    [SerializeField] private AudioClip transferSound;
    [SerializeField] private AudioClip errorSound;

    [Header("进入餐厅范围：首锅缩放提示")]
    [SerializeField] private float firstPotEnterPulsePeak = 1.14f;
    [SerializeField] private float firstPotEnterPulseUp = 0.14f;
    [SerializeField] private float firstPotEnterPulseDown = 0.22f;

    [Header("事件")]
    public UnityEvent OnPlayerEnterRange;
    public UnityEvent OnPlayerExitRange;
    public UnityEvent OnItemTransferred;
    public UnityEvent OnShopEmpty;
    public UnityEvent OnShopNotEmpty;

    [Header("发射器引用")]
    [SerializeField] private ProjectileLauncher projectileLauncher;

    private InventoryManager playerInventory;
    private ShopManager shopManager;
    private Transform playerTransform;
    private CanvasGroup shopCanvasGroup;
    private AudioSource audioSource;
    private RectTransform shopUIRectTransform;

    public bool playerInRange = false;
    private readonly HashSet<int> playerColliderIdsInTrigger = new HashSet<int>();
    private bool canInteract = true;
    private bool shopHasItems = false;
    private Tween currentUItween;
    public bool isUIShowing = false;
    private Vector3 originalUIScale;
    /// <summary>父级 RectTransform 不要用 scale=0：子 ScrollRect 在 0→非0 后 normalizedPosition 会错乱。用极小非零代替。</summary>
    private const float MinShopUiScaleFactor = 0.001f;

    private Vector3 GetMinShopUiScale()
    {
        return new Vector3(
            Mathf.Max(originalUIScale.x * MinShopUiScaleFactor, 1e-6f),
            Mathf.Max(originalUIScale.y * MinShopUiScaleFactor, 1e-6f),
            Mathf.Max(originalUIScale.z * MinShopUiScaleFactor, 1e-6f));
    }

    protected override void OnAwake()
    {
        playerInventory = FindObjectOfType<InventoryManager>();
        shopManager = GetComponent<ShopManager>();
        audioSource = GetComponent<AudioSource>();

        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();

        if (shopUICanvas != null)
        {
            shopUICanvas.SetActive(true);
            shopUIRectTransform = shopUICanvas.GetComponent<RectTransform>();
            shopCanvasGroup = shopUICanvas.GetComponent<CanvasGroup>();
            if (shopCanvasGroup == null)
                shopCanvasGroup = shopUICanvas.AddComponent<CanvasGroup>();

            originalUIScale = shopUIRectTransform.localScale;
            shopUIRectTransform.localScale = GetMinShopUiScale();
            shopCanvasGroup.alpha = 0f;
            shopUICanvas.SetActive(false);
        }

        if (shopManager != null)
            shopManager.OnShopStateChanged.AddListener(HandleShopStateChanged);
    }

    private void Start()
    {
        UpdateShopUIState();
        ShowShopUI();
    }

    private void Update()
    {
        // if (playerInRange && playerTransform != null)
        // {
        //     float distance = Vector3.Distance(transform.position, playerTransform.position);
        //     if (interactionText != null)
        //         UpdateInteractionText();

        //     // 改成按住检测
        //     if (Input.GetKey(interactKey) && canInteract)
        //         TryTransferItem();
        // }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            // GetComponent<InteractiveFeedback>()?.PlayFeedback();
            int id = other.GetInstanceID();
            if (!playerColliderIdsInTrigger.Add(id)) return;
            if (playerColliderIdsInTrigger.Count > 1) return; // 多 Collider：只在第一次进入时触发
            playerInRange = true;
            TryPlayFirstPotEnterPulse();
            //PlayerEnterRange(other.transform);
            // 进入范围：强制餐厅按钮激活（不管原来是否激活）
            // if (UIFloatingButtonGroup.Instance != null)
            //     UIFloatingButtonGroup.Instance.SetSelectedIndex(1);
            // // 兜底确保 UI 显示（防止 SetSelectedIndex 因为“已选中”提前 return）
            // ShowShopUI();
        }
    }

    private void OnTriggerExit(Collider other)
    {
       
        if (other.CompareTag("Player"))
        {
            int id = other.GetInstanceID();
            if (!playerColliderIdsInTrigger.Remove(id)) return;
            if (playerColliderIdsInTrigger.Count > 0) return; // 仍有 Collider 在触发器内：不算真正离开
            playerInRange=false;
            TryCancelFirstPotAttentionPulse();
            // PlayerExitRange();
            // // 离开范围：将餐厅按钮设为未激活（仅当当前正选中餐厅时才取消，避免误清除其它按钮选中）
            // if (UIFloatingButtonGroup.Instance != null)
            //     UIFloatingButtonGroup.Instance.DeselectIfSelected(1);
        }
    }

    // public void PlayerEnterRange(Transform player)
    // {
    //     playerTransform = player;
    //     playerInRange = true;
    //     // 进范围会强制激活餐厅按钮并显示 UI（逻辑在触发器里做一次，这里保持显示即可）
    //     ShowShopUI();
    //     OnPlayerEnterRange?.Invoke();
    // }

    public void PlayerExitRange()
    {
        playerInRange = false;
        playerTransform = null;
        pendingItemCount = 0; // 重置待处理计数
        playerColliderIdsInTrigger.Clear();

        TryCancelFirstPotAttentionPulse();

        if (interactionText != null)
            interactionText.text = "";

        OnPlayerExitRange?.Invoke();
    }

    private void TryPlayFirstPotEnterPulse()
    {
        RestaurantPanel panel = RestaurantPanel.instance;
        if (panel == null || panel.potsList == null || panel.potsList.Count == 0) return;
        Pot pot = panel.potsList[0];
        if (pot == null || !pot.isActiveAndEnabled) return;
        pot.PlayAttentionScalePulse(firstPotEnterPulsePeak, firstPotEnterPulseUp, firstPotEnterPulseDown);
    }

    private void TryCancelFirstPotAttentionPulse()
    {
        RestaurantPanel panel = RestaurantPanel.instance;
        if (panel == null || panel.potsList == null || panel.potsList.Count == 0) return;
        Pot pot = panel.potsList[0];
        if (pot == null) return;
        pot.CancelAttentionScalePulse();
    }

    public void ShowShopUI()
    {
        if (shopUICanvas == null || shopUIRectTransform == null || shopCanvasGroup == null) return;
        if (isUIShowing) return;
        isUIShowing = true;

        shopUICanvas.SetActive(true);

        if (currentUItween != null && currentUItween.IsActive())
            currentUItween.Kill();

        shopUIRectTransform.localScale = GetMinShopUiScale();
        shopCanvasGroup.alpha = 0f;
        Vector3 targetScale = originalUIScale;
        Vector3 overshootScale = originalUIScale * showScaleMultiplier;
        Sequence showSequence = DOTween.Sequence();
        showSequence.Append(shopUIRectTransform.DOScale(overshootScale, showAnimationDuration * 0.6f).SetEase(Ease.OutBack));
        showSequence.Join(shopCanvasGroup.DOFade(1f, showAnimationDuration * 0.4f));
        showSequence.Append(shopUIRectTransform.DOScale(targetScale, showAnimationDuration * 0.4f).SetEase(Ease.OutBack));
        showSequence.OnComplete(() => { isUIShowing = true; });
        currentUItween = showSequence;
    }

    public void HideShopUI()
    {
        // 餐厅 UI 常驻显示，保留空实现以兼容旧调用点。
    }

    private int pendingItemCount = 0; // 追踪正在飞行中的物品数量
    private void TryTransferItem()
    {
        if (playerInventory == null || shopManager == null || !canInteract)
        {
            Debug.Log($"[TryTransfer] 提前返回: inventory={playerInventory != null}, shop={shopManager != null}, canInteract={canInteract}");
            return;
        }

        InventoryItemUI firstSlot = FindFirstNonEmptyInventorySlot();
        if (firstSlot == null)
        {
            Debug.Log("[TryTransfer] 背包为空");
            return;
        }

        ResourceType itemType = firstSlot.GetItemType();
        int amountToTransfer = 1;

        bool canReceive = shopManager.CanReceiveItem(itemType, amountToTransfer + pendingItemCount);
        Debug.Log($"[TryTransfer] 检查: 类型={itemType}, 请求={amountToTransfer + pendingItemCount}, pending={pendingItemCount}, 可接收={canReceive}");

        if (!canReceive)
        {
            // 商店已满，播放错误音效但不进入冷却
            if (errorSound != null && audioSource != null)
            {
                audioSource.PlayOneShot(errorSound, 0.3f);
            }
            return; // 这里直接返回，不进入冷却
        }

        if (RemoveItemFromInventory(firstSlot, itemType, amountToTransfer, out int removedCount))
        {
            pendingItemCount += removedCount;
            Debug.Log($"[TryTransfer] 发射! pendingItemCount 增加到 {pendingItemCount}");
            AudioManager.Instance.PlayAudio("3");
            PlaySound(transferSound);

            if (projectileLauncher != null)
            {
                projectileLauncher.SpawnProjectile(
                    playerTransform,
                    shopUICanvas.transform,
                    itemType,
                    removedCount,
                    () =>
                    {
                        pendingItemCount -= removedCount;
                        Debug.Log($"[回调] 到达! pendingItemCount 减少到 {pendingItemCount}");

                        shopManager.ReceiveItemFromPlayer(itemType, removedCount);
                        ShowMessage($"已出售 {removedCount} 个 {GetItemName(itemType)}", Color.green);
                        UpdateShopUIState();
                        OnItemTransferred?.Invoke();

                        if (shopUICanvas != null && shopUICanvas.activeSelf && shopUIRectTransform != null)
                        {
                            Vector3 feedbackScale = originalUIScale * 1.1f;
                            Sequence feedbackSequence = DOTween.Sequence();
                            feedbackSequence.Append(shopUIRectTransform.DOScale(feedbackScale, 0.1f));
                            feedbackSequence.Append(shopUIRectTransform.DOScale(originalUIScale, 0.2f).SetEase(Ease.OutBack));
                        }

                        // 增加：物品到达后尝试立即触发下一次发射
                        if (playerInRange && Input.GetKey(interactKey))
                        {
                            StartCoroutine(TriggerImmediate());
                        }
                    }
                );
            }
            else
            {
                pendingItemCount -= removedCount;
                shopManager.ReceiveItemFromPlayer(itemType, removedCount);
                ShowMessage($"已出售 {removedCount} 个 {GetItemName(itemType)}", Color.green);
                UpdateShopUIState();
                OnItemTransferred?.Invoke();

                // 增加：无弹道时也立即触发
                if (playerInRange && Input.GetKey(interactKey))
                {
                    StartCoroutine(TriggerImmediate());
                }
            }

            // 只在成功发射后进入冷却
            StartCoroutine(InteractionCooldown());
        }
    }

    // 新增：立即触发下一次检测
    private IEnumerator TriggerImmediate()
    {
        yield return new WaitForEndOfFrame(); // 等待一帧，确保UI状态已更新
        if (playerInRange && Input.GetKey(interactKey))
        {
            TryTransferItem();
        }
    }

    private bool RemoveItemFromInventory(InventoryItemUI slot, ResourceType itemType, int itemCount, out int removedCount)
    {
        slot.RemoveItem(itemCount, out removedCount);
        if (removedCount > 0)
        {
            playerInventory.ReorganizeInventory();
            return true;
        }
        return false;
    }

    private InventoryItemUI FindFirstNonEmptyInventorySlot()
    {
        if (playerInventory == null) return null;
        for (int i = 0; i < playerInventory.GetSlotCount(); i++)
        {
            InventoryItemUI slot = playerInventory.GetSlot(i);
            if (slot != null && !slot.IsEmpty())
                return slot;
        }
        return null;
    }

    private IEnumerator InteractionCooldown()
    {
        canInteract = false;
        yield return new WaitForSeconds(interactCooldown);
        canInteract = true;
    }

    private void HandleShopStateChanged(bool hasItems)
    {
        shopHasItems = hasItems;
        UpdateShopUIState();
    }

    private void UpdateShopUIState()
    {
        if (shopManager == null) return;
        bool newHasItems = shopManager.HasItemsInShop();

        if (newHasItems != shopHasItems)
        {
            shopHasItems = newHasItems;
            if (shopHasItems)
            {
                if (!shopUICanvas.activeSelf)
                {
                    ShowShopUI();
                }
                else
                {
                    if (shopUICanvas != null && shopUICanvas.activeSelf && shopUIRectTransform != null)
                    {
                        Vector3 fullScale = originalUIScale * 1.2f;
                        Sequence itemsAddedSequence = DOTween.Sequence();
                        itemsAddedSequence.Append(shopUIRectTransform.DOScale(fullScale, 0.2f));
                        itemsAddedSequence.Append(shopUIRectTransform.DOScale(originalUIScale, 0.3f).SetEase(Ease.OutBack));
                    }
                }
                OnShopNotEmpty?.Invoke();
            }
            else
            {
                if (playerInRange && shopUICanvas != null && shopUICanvas.activeSelf && shopUIRectTransform != null)
                {
                    Vector3 emptyScale = originalUIScale * 0.8f;
                    Sequence emptySequence = DOTween.Sequence();
                    emptySequence.Append(shopUIRectTransform.DOScale(emptyScale, 0.3f).SetEase(Ease.OutBack));
                }
                OnShopEmpty?.Invoke();
            }
        }
    }

    private void UpdateInteractionText()
    {
        if (playerInventory == null || shopManager == null) return;
        InventoryItemUI firstSlot = FindFirstNonEmptyInventorySlot();

        if (firstSlot == null)
        {
            interactionText.text = noItemsMessage;
            interactionText.color = Color.gray;
        }
        else
        {
            ResourceType itemType = firstSlot.GetItemType();
            int amountToTransfer = 1;

            if (!shopManager.CanReceiveItem(itemType, amountToTransfer))
            {
                interactionText.text = shopFullMessage;
                interactionText.color = Color.red;
            }
            else
            {
                string itemName = GetItemName(itemType);
                interactionText.text = $"{interactionMessage} (按住 E 连续出售)";
                interactionText.color = Color.yellow;
            }
        }
    }

    private void ShowMessage(string message, Color color)
    {
        if (interactionText != null)
        {
            interactionText.text = message;
            interactionText.color = color;
            CancelInvoke(nameof(UpdateInteractionText));
            Invoke(nameof(UpdateInteractionText), 2f);
        }
    }

    private void PlaySound(AudioClip clip)
    {
        if (clip != null && audioSource != null)
            audioSource.PlayOneShot(clip);
    }

    private string GetItemName(ResourceType type)
    {
        if (GameValManager.Instance != null)
            return GameValManager.Instance.GetResourceDisplayName(type);
        return type.ToString();
    }

    public int GetItemPrice(ResourceType type, int amount)
    {
        if (shopManager != null)
        {
            int pricePerUnit = shopManager.GetResourcePrice(type);
            return pricePerUnit * amount;
        }
        return 0;
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();

        if (shopManager != null)
            shopManager.OnShopStateChanged.RemoveListener(HandleShopStateChanged);
        if (currentUItween != null && currentUItween.IsActive())
            currentUItween.Kill();
    }
}