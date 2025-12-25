using UnityEngine;
using UnityEngine.Events;
using System.Collections;
using TMPro;

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
    
    [Header("视觉效果")]
    [SerializeField] private float fadeSpeed = 5f; // UI淡入淡出速度
    [SerializeField] private float uiScaleWhenEmpty = 0.8f; // 商店空时的UI缩放
    [SerializeField] private float uiScaleWhenFull = 1.2f; // 商店有物品时的UI缩放
    
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
    
    // 状态
    private bool playerInRange = false;
    private bool canInteract = true;
    private bool shopHasItems = false;
    private Coroutine fadeCoroutine;
    
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
    
    // 显示商店UI
    private void ShowShopUI()
    {
        if (shopUICanvas == null) return;
        shopUICanvas.SetActive(true);
    }
    
    // 隐藏商店UI
    private void HideShopUI()
    {
        if (shopUICanvas == null || shopHasItems) return;
        shopUICanvas.SetActive(false);
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
    }
}