using UnityEngine;
using UnityEngine.Events;
using System.Collections;
using TMPro;
using DG.Tweening; // 添加DoTween命名空间

public class ShopInteraction : MonoBehaviour
{
    [Header("商店设置")]
    [SerializeField] private float interactionRange = 3f; // 交互范围
    [SerializeField] private KeyCode interactKey = KeyCode.E; // 交互按键
    [SerializeField] private float interactCooldown = 0.5f; // 交互冷却时间
    
    [Header("UI设置")]
    [SerializeField] private GameObject shopUICanvas; // 商店UI Canvas
    [SerializeField] private TextMeshProUGUI interactionText; // 交互提示文本
    [SerializeField] private string interactionMessage = "按 E 出售物品";
    [SerializeField] private string shopEmptyMessage = "商店已空";
    [SerializeField] private string noItemsMessage = "背包为空";
    [SerializeField] private string shopFullMessage = "商店已满";
    
    [Header("UI动画设置")]
    [SerializeField] private float fadeSpeed = 5f; // UI淡入淡出速度
    [SerializeField] private float showScaleMultiplier = 1.2f; // 弹出动画的最大缩放倍数
    [SerializeField] private float hideScaleMultiplier = 1.1f; // 收回动画的初始缩放倍数
    [SerializeField] private float showAnimationDuration = 0.5f; // 显示动画时长
    [SerializeField] private float hideAnimationDuration = 0.3f; // 隐藏动画时长
    [SerializeField] private Ease showEase = Ease.OutBack; // 显示动画缓动类型
    [SerializeField] private Ease hideEase = Ease.InBack; // 隐藏动画缓动类型
    
    [Header("音频")]
    [SerializeField] private AudioClip transferSound; // 物品转移音效
    [SerializeField] private AudioClip errorSound; // 错误音效
    
    [Header("事件")]
    public UnityEvent OnPlayerEnterRange;
    public UnityEvent OnPlayerExitRange;
    public UnityEvent OnItemTransferred;
    public UnityEvent OnShopEmpty;
    public UnityEvent OnShopNotEmpty;
    
    // 引用
    private InventoryManager playerInventory;
    private ShopManager shopManager;
    private Transform playerTransform;
    private CanvasGroup shopCanvasGroup;
    private AudioSource audioSource;
    private RectTransform shopUIRectTransform; // UI的RectTransform引用
    
    // 状态
    private bool playerInRange = false;
    private bool canInteract = true;
    private bool shopHasItems = false;
    private Coroutine fadeCoroutine;
    private Tween currentUItween; // 当前UI动画的引用
    private bool isUIShowing = false; // UI是否正在显示
    private Vector3 originalUIScale; // 保存UI的原始大小
    
    private void Awake()
    {
        // 获取组件
        playerInventory = FindObjectOfType<InventoryManager>();
        shopManager = GetComponent<ShopManager>();
        audioSource = GetComponent<AudioSource>();
        
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }
        
        if (shopUICanvas != null)
        {
            // 确保UI处于激活状态以便获取原始大小
            shopUICanvas.SetActive(true);
            
            // 获取RectTransform和CanvasGroup组件
            shopUIRectTransform = shopUICanvas.GetComponent<RectTransform>();
            shopCanvasGroup = shopUICanvas.GetComponent<CanvasGroup>();
            
            if (shopCanvasGroup == null)
            {
                shopCanvasGroup = shopUICanvas.AddComponent<CanvasGroup>();
            }
            
            // 保存UI的原始大小
            originalUIScale = shopUIRectTransform.localScale;
            
            // 初始化UI状态（缩小到0，完全透明）
            shopUIRectTransform.localScale = Vector3.zero;
            shopCanvasGroup.alpha = 0f;
            
            // 立即隐藏
            shopUICanvas.SetActive(false);
        }
        
        if (shopManager != null)
        {
            // 监听商店物品变化
            shopManager.OnShopStateChanged.AddListener(HandleShopStateChanged);
        }
    }
    
    private void Start()
    {
        // 初始检查商店状态
        UpdateShopUIState();
    }
    
    private void Update()
    {
        if (playerInRange && playerTransform != null)
        {
            // 检查距离
            float distance = Vector3.Distance(transform.position, playerTransform.position);
            
            // 显示交互提示
            if (interactionText != null)
            {
                UpdateInteractionText();
            }
            
            // 检测按键交互
            if (Input.GetKeyDown(interactKey) && canInteract)
            {
                TryTransferItem();
            }
        }
    }
    
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
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
    
    // 玩家进入范围
    private void PlayerEnterRange(Transform player)
    {
        playerTransform = player;
        playerInRange = true;
        
        // 显示商店UI
        ShowShopUI();
        
        // 触发事件
        OnPlayerEnterRange?.Invoke();
    }
    
    // 玩家离开范围
    private void PlayerExitRange()
    {
        playerInRange = false;
        playerTransform = null;
        
        // 如果没有物品，隐藏UI
        if (!shopHasItems)
        {
            HideShopUI();
        }
        
        // 清除交互提示
        if (interactionText != null)
        {
            interactionText.text = "";
        }
        
        // 触发事件
        OnPlayerExitRange?.Invoke();
    }
    
    // 显示商店UI - 使用DoTween动画
    private void ShowShopUI()
    {
        if (shopUICanvas == null || shopUIRectTransform == null || shopCanvasGroup == null) return;
        
        // 如果已经在显示，则返回
        if (isUIShowing) return;
        
        isUIShowing = true;
        
        // 激活UI
        shopUICanvas.SetActive(true);
        
        // 停止任何正在进行的动画
        if (currentUItween != null && currentUItween.IsActive())
        {
            currentUItween.Kill();
        }
        
        // 设置初始状态
        shopUIRectTransform.localScale = Vector3.zero;
        shopCanvasGroup.alpha = 0f;
        
        // 计算动画目标大小
        Vector3 targetScale = originalUIScale;
        Vector3 overshootScale = originalUIScale * showScaleMultiplier;
        
        // 创建显示动画序列
        Sequence showSequence = DOTween.Sequence();
        
        // 第一步：快速弹出到稍大于原始大小
        showSequence.Append(shopUIRectTransform.DOScale(overshootScale, showAnimationDuration * 0.6f)
            .SetEase(Ease.OutBack));
        
        showSequence.Join(shopCanvasGroup.DOFade(1f, showAnimationDuration * 0.4f));
        
        // 第二步：回弹到原始大小
        showSequence.Append(shopUIRectTransform.DOScale(targetScale, showAnimationDuration * 0.4f)
            .SetEase(Ease.OutBack));
        
        // 设置动画完成后的回调
        showSequence.OnComplete(() => {
            isUIShowing = true;
        });
        
        currentUItween = showSequence;
    }
    
    // 隐藏商店UI - 使用DoTween动画
    private void HideShopUI()
    {
        if (shopUICanvas == null || shopUIRectTransform == null || shopCanvasGroup == null) return;
        
        // 如果没有在显示，则返回
        if (!isUIShowing && !shopUICanvas.activeSelf) return;
        
        isUIShowing = false;
        
        // 停止任何正在进行的动画
        if (currentUItween != null && currentUItween.IsActive())
        {
            currentUItween.Kill();
        }
        
        // 计算初始隐藏缩放
        Vector3 initialHideScale = originalUIScale * hideScaleMultiplier;
        Vector3 currentScale = shopUIRectTransform.localScale;
        
        // 创建隐藏动画序列
        Sequence hideSequence = DOTween.Sequence();
        
        // 第一步：先稍微放大一点（如果当前不是原始大小，则从当前大小开始）
        hideSequence.Append(shopUIRectTransform.DOScale(initialHideScale, hideAnimationDuration * 0.2f)
            .From(currentScale)
            .SetEase(Ease.InBack));
        
        hideSequence.Join(shopCanvasGroup.DOFade(0.8f, hideAnimationDuration * 0.2f));
        
        // 第二步：缩小到0
        hideSequence.Append(shopUIRectTransform.DOScale(Vector3.zero, hideAnimationDuration * 0.8f)
            .SetEase(Ease.InBack));
        
        hideSequence.Join(shopCanvasGroup.DOFade(0f, hideAnimationDuration * 0.6f));
        
        // 设置动画完成后的回调
        hideSequence.OnComplete(() => {
            shopUICanvas.SetActive(false);
        });
        
        currentUItween = hideSequence;
    }
    
    // 尝试转移物品
    private void TryTransferItem()
    {
        if (playerInventory == null || shopManager == null || !canInteract)
        {
            return;
        }
        
        // 检查玩家背包是否有物品
        InventoryItemUI firstSlot = FindFirstNonEmptyInventorySlot();
        if (firstSlot == null)
        {
            // 背包为空
            ShowMessage(noItemsMessage, Color.red);
            PlaySound(errorSound);
            return;
        }
        
        // 检查商店是否有空间
        ResourceType itemType = firstSlot.GetItemType();
        
        // 修改这里：每次只转移1个物品
        int amountToTransfer = 1;
        
        if (!shopManager.CanReceiveItem(itemType, amountToTransfer))
        {
            // 商店已满
            ShowMessage(shopFullMessage, Color.red);
            PlaySound(errorSound);
            return;
        }
        
        // 执行转移
        TransferItem(firstSlot, itemType, amountToTransfer);
        
        // 开始冷却
        StartCoroutine(InteractionCooldown());
    }
    
    // 转移物品
    private void TransferItem(InventoryItemUI slot, ResourceType itemType, int itemCount)
    {
        // 从背包移除物品
        slot.RemoveItem(itemCount, out int removedCount);
        
        // 添加到商店
        bool success = shopManager.ReceiveItemFromPlayer(itemType, removedCount);
        
        if (success)
        {
            // 播放音效
            PlaySound(transferSound);
            
            // 显示成功消息
            ShowMessage($"已出售 {removedCount} 个 {GetItemName(itemType)}", Color.green);
            
            // 重新整理背包
            playerInventory.ReorganizeInventory();
            
            // 添加UI缩放反馈动画
            if (shopUICanvas != null && shopUICanvas.activeSelf && shopUIRectTransform != null)
            {
                // 计算缩放值
                Vector3 feedbackScale = originalUIScale * 1.1f;
                
                Sequence feedbackSequence = DOTween.Sequence();
                feedbackSequence.Append(shopUIRectTransform.DOScale(feedbackScale, 0.1f));
                feedbackSequence.Append(shopUIRectTransform.DOScale(originalUIScale, 0.2f).SetEase(Ease.OutBack));
            }
            
            // 触发事件
            OnItemTransferred?.Invoke();
            
            // 更新商店UI状态
            UpdateShopUIState();
        }
        else
        {
            // 如果添加失败，将物品退回背包
            playerInventory.AddItem(itemType, removedCount);
            ShowMessage("出售失败", Color.red);
            PlaySound(errorSound);
            
            // 添加错误反馈动画
            if (shopUICanvas != null && shopUICanvas.activeSelf && shopUIRectTransform != null)
            {
                Sequence errorSequence = DOTween.Sequence();
                errorSequence.Append(shopUIRectTransform.DOShakePosition(0.3f, 5f, 10, 90f, false, true));
            }
        }
    }
    
    // 找到背包中第一个非空槽位
    private InventoryItemUI FindFirstNonEmptyInventorySlot()
    {
        if (playerInventory == null) return null;
        
        for (int i = 0; i < playerInventory.GetSlotCount(); i++)
        {
            InventoryItemUI slot = playerInventory.GetSlot(i);
            if (slot != null && !slot.IsEmpty())
            {
                return slot;
            }
        }
        
        return null;
    }
    
    // 交互冷却
    private IEnumerator InteractionCooldown()
    {
        canInteract = false;
        yield return new WaitForSeconds(interactCooldown);
        canInteract = true;
    }
    
    // 处理商店状态变化
    private void HandleShopStateChanged(bool hasItems)
    {
        shopHasItems = hasItems;
        UpdateShopUIState();
    }
    
    // 更新商店UI状态
    private void UpdateShopUIState()
    {
        if (shopManager == null) return;
        
        bool newHasItems = shopManager.HasItemsInShop();
        
        if (newHasItems != shopHasItems)
        {
            shopHasItems = newHasItems;
            
            if (shopHasItems)
            {
                // 商店有物品，确保UI显示
                if (!shopUICanvas.activeSelf)
                {
                    ShowShopUI();
                }
                else
                {
                    // 如果UI已经显示，添加物品进入时的UI缩放动画
                    if (shopUICanvas != null && shopUICanvas.activeSelf && shopUIRectTransform != null)
                    {
                        // 计算缩放值
                        Vector3 fullScale = originalUIScale * 1.2f;
                        
                        Sequence itemsAddedSequence = DOTween.Sequence();
                        itemsAddedSequence.Append(shopUIRectTransform.DOScale(fullScale, 0.2f));
                        itemsAddedSequence.Append(shopUIRectTransform.DOScale(originalUIScale, 0.3f).SetEase(Ease.OutBack));
                    }
                }
                
                // 触发事件
                OnShopNotEmpty?.Invoke();
            }
            else
            {
                // 商店为空
                if (!playerInRange)
                {
                    HideShopUI();
                }
                else
                {
                    // 如果玩家还在范围内，但商店为空，播放空状态动画
                    if (shopUICanvas != null && shopUICanvas.activeSelf && shopUIRectTransform != null)
                    {
                        // 计算空状态缩放
                        Vector3 emptyScale = originalUIScale * 0.8f;
                        Sequence emptySequence = DOTween.Sequence();
                        emptySequence.Append(shopUIRectTransform.DOScale(emptyScale, 0.3f).SetEase(Ease.OutBack));
                    }
                }
                
                // 触发事件
                OnShopEmpty?.Invoke();
            }
        }
    }
    
    // 更新交互提示文本
    private void UpdateInteractionText()
    {
        if (playerInventory == null || shopManager == null) return;
        
        // 检查玩家背包是否有物品
        InventoryItemUI firstSlot = FindFirstNonEmptyInventorySlot();
        
        if (firstSlot == null)
        {
            interactionText.text = noItemsMessage;
            interactionText.color = Color.gray;
        }
        else
        {
            // 检查商店是否有空间
            ResourceType itemType = firstSlot.GetItemType();
            
            // 修改这里：只检查1个物品
            int amountToTransfer = 1;
            
            if (!shopManager.CanReceiveItem(itemType, amountToTransfer))
            {
                interactionText.text = shopFullMessage;
                interactionText.color = Color.red;
            }
            else
            {
                string itemName = GetItemName(itemType);
                // 修改提示信息
                interactionText.text = $"{interactionMessage} (按住 E 连续出售)";
                interactionText.color = Color.yellow;
            }
        }
    }
    
    // 显示消息
    private void ShowMessage(string message, Color color)
    {
        if (interactionText != null)
        {
            interactionText.text = message;
            interactionText.color = color;
            
            // 2秒后恢复
            CancelInvoke(nameof(UpdateInteractionText));
            Invoke(nameof(UpdateInteractionText), 2f);
        }
    }
    
    // 播放音效
    private void PlaySound(AudioClip clip)
    {
        if (clip != null && audioSource != null)
        {
            audioSource.PlayOneShot(clip);
        }
    }
    
    // 获取物品名称
    private string GetItemName(ResourceType type)
    {
        if (GameValManager.Instance != null)
        {
            return GameValManager.Instance.GetResourceDisplayName(type);
        }
        
        return type.ToString();
    }
    
    // 获取物品价格
    public int GetItemPrice(ResourceType type, int amount)
    {
        if (shopManager != null)
        {
            int pricePerUnit = shopManager.GetResourcePrice(type);
            return pricePerUnit * amount;
        }
        return 0;
    }
    
    // 清理
    private void OnDestroy()
    {
        if (shopManager != null)
        {
            shopManager.OnShopStateChanged.RemoveListener(HandleShopStateChanged);
        }
        
        // 清理所有动画
        if (currentUItween != null && currentUItween.IsActive())
        {
            currentUItween.Kill();
        }
    }
}