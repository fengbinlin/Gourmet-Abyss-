using UnityEngine;
using UnityEngine.Events;
using System.Collections;
using TMPro;
using DG.Tweening;

public class ShopInteraction : MonoBehaviour
{
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

    private bool playerInRange = false;
    private bool canInteract = true;
    private bool shopHasItems = false;
    private Tween currentUItween;
    private bool isUIShowing = false;
    private Vector3 originalUIScale;

    private void Awake()
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
            shopUIRectTransform.localScale = Vector3.zero;
            shopCanvasGroup.alpha = 0f;
            shopUICanvas.SetActive(false);
        }

        if (shopManager != null)
            shopManager.OnShopStateChanged.AddListener(HandleShopStateChanged);
    }

    private void Start()
    {
        UpdateShopUIState();
    }

    private void Update()
    {
        if (playerInRange && playerTransform != null)
        {
            float distance = Vector3.Distance(transform.position, playerTransform.position);
            if (interactionText != null)
                UpdateInteractionText();

            // 改成按住检测
            if (Input.GetKey(interactKey) && canInteract)
                TryTransferItem();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            GetComponent<InteractiveFeedback>()?.PlayFeedback();
            PlayerEnterRange(other.transform);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            PlayerExitRange();
        }
    }

    private void PlayerEnterRange(Transform player)
    {
        playerTransform = player;
        playerInRange = true;
        ShowShopUI();
        OnPlayerEnterRange?.Invoke();
    }

    private void PlayerExitRange()
    {
        playerInRange = false;
        playerTransform = null;

        if (!shopHasItems)
            HideShopUI();

        if (interactionText != null)
            interactionText.text = "";

        OnPlayerExitRange?.Invoke();
    }

    private void ShowShopUI()
    {
        if (shopUICanvas == null || shopUIRectTransform == null || shopCanvasGroup == null) return;
        if (isUIShowing) return;
        isUIShowing = true;
        shopUICanvas.SetActive(true);

        if (currentUItween != null && currentUItween.IsActive())
            currentUItween.Kill();

        shopUIRectTransform.localScale = Vector3.zero;
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

    private void HideShopUI()
    {
        if (shopUICanvas == null || shopUIRectTransform == null || shopCanvasGroup == null) return;
        if (!isUIShowing && !shopUICanvas.activeSelf) return;
        isUIShowing = false;

        if (currentUItween != null && currentUItween.IsActive())
            currentUItween.Kill();

        Vector3 initialHideScale = originalUIScale * hideScaleMultiplier;
        Vector3 currentScale = shopUIRectTransform.localScale;
        Sequence hideSequence = DOTween.Sequence();
        hideSequence.Append(shopUIRectTransform.DOScale(initialHideScale, hideAnimationDuration * 0.2f).From(currentScale).SetEase(Ease.InBack));
        hideSequence.Join(shopCanvasGroup.DOFade(0.8f, hideAnimationDuration * 0.2f));
        hideSequence.Append(shopUIRectTransform.DOScale(Vector3.zero, hideAnimationDuration * 0.8f).SetEase(Ease.InBack));
        hideSequence.Join(shopCanvasGroup.DOFade(0f, hideAnimationDuration * 0.6f));
        hideSequence.OnComplete(() => { shopUICanvas.SetActive(false); });
        currentUItween = hideSequence;
    }

    private void TryTransferItem()
    {
        if (playerInventory == null || shopManager == null || !canInteract)
            return;

        InventoryItemUI firstSlot = FindFirstNonEmptyInventorySlot();
        if (firstSlot == null)
        {
            ShowMessage(noItemsMessage, Color.red);
            PlaySound(errorSound);
            return;
        }

        ResourceType itemType = firstSlot.GetItemType();
        int amountToTransfer = 1;

        if (!shopManager.CanReceiveItem(itemType, amountToTransfer))
        {
            ShowMessage(shopFullMessage, Color.red);
            PlaySound(errorSound);
            return;
        }

        if (RemoveItemFromInventory(firstSlot, itemType, amountToTransfer, out int removedCount))
        {
            PlaySound(transferSound);

            // 发射抛射物
            if (projectileLauncher != null)
            {
                projectileLauncher.SpawnProjectile(
    playerTransform,
    shopUICanvas.transform,
    itemType,
    removedCount,
    () =>
    {
        // 实际加入商店
        shopManager.ReceiveItemFromPlayer(itemType, removedCount);
        ShowMessage($"已出售 {removedCount} 个 {GetItemName(itemType)}", Color.green);
        UpdateShopUIState();
        OnItemTransferred?.Invoke();

        // 面板弹动动画恢复
        if (shopUICanvas != null && shopUICanvas.activeSelf && shopUIRectTransform != null)
        {
            Vector3 feedbackScale = originalUIScale * 1.1f;
            Sequence feedbackSequence = DOTween.Sequence();
            feedbackSequence.Append(shopUIRectTransform.DOScale(feedbackScale, 0.1f));
            feedbackSequence.Append(shopUIRectTransform.DOScale(originalUIScale, 0.2f).SetEase(Ease.OutBack));
        }
    }
);
            }
            else
            {
                // 如果没有发射器，直接加入商店
                shopManager.ReceiveItemFromPlayer(itemType, removedCount);
                ShowMessage($"已出售 {removedCount} 个 {GetItemName(itemType)}", Color.green);
                UpdateShopUIState();
                OnItemTransferred?.Invoke();
            }

            StartCoroutine(InteractionCooldown());
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
                if (!playerInRange)
                {
                    HideShopUI();
                }
                else
                {
                    if (shopUICanvas != null && shopUICanvas.activeSelf && shopUIRectTransform != null)
                    {
                        Vector3 emptyScale = originalUIScale * 0.8f;
                        Sequence emptySequence = DOTween.Sequence();
                        emptySequence.Append(shopUIRectTransform.DOScale(emptyScale, 0.3f).SetEase(Ease.OutBack));
                    }
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

    private void OnDestroy()
    {
        if (shopManager != null)
            shopManager.OnShopStateChanged.RemoveListener(HandleShopStateChanged);
        if (currentUItween != null && currentUItween.IsActive())
            currentUItween.Kill();
    }
}