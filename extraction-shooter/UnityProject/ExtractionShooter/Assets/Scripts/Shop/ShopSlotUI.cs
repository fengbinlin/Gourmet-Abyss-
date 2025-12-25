using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections.Generic;

public class ShopSlotUI : MonoBehaviour, IPointerClickHandler
{
    [Header("UI Components")]
    [SerializeField] private Image slotBackground; // 槽位背景
    [SerializeField] private Image itemIcon;       // 物品图标
    [SerializeField] private Text countText;  // 物品数量
    [SerializeField] private Image progressFill;   // 售卖进度条
    [SerializeField] private Text timerText;  // 计时器文本
    
    [Header("图标设置")]
    [SerializeField] private List<ResourceIcon> resourceIcons = new List<ResourceIcon>();
    
    [System.Serializable]
    public class ResourceIcon
    {
        public ResourceType type;
        public Sprite icon;
    }
    
    [Header("颜色设置")]
    [SerializeField] private Color emptySlotColor = new Color(0.2f, 0.2f, 0.2f, 0.5f);
    [SerializeField] private Color fullSlotColor = Color.white;
    [SerializeField] private Color progressFillColor = Color.yellow;
    [SerializeField] private Color timerTextColor = Color.white;
    
    [Header("售卖设置")]
    [SerializeField] private float sellTime = 5f; // 售卖所需时间(秒)
    
    // 商店格子的数据
    public class ShopSlotData
    {
        public ResourceType itemType = ResourceType.Money;
        public int currentCount = 0;
        public int maxCapacity = 4;
        public bool isEmpty = true;
        public int slotIndex = -1;
        public float sellTimer = 0f; // 当前售卖计时
        public float totalSellTime = 5f; // 总售卖时间
        public bool isSelling = false; // 是否正在售卖
    }
    
    // 当前格子的数据
    private ShopSlotData slotData = new ShopSlotData();
    
    // 事件
    public System.Action<int> OnSlotClicked; // 参数：格子索引
    public System.Action<int, ResourceType, int> OnItemSold; // 参数：格子索引，物品类型，数量
    
    #region Unity生命周期
    
    private void Awake()
    {
        UpdateUI();
    }
    
    private void Update()
    {
        if (slotData.isSelling && !slotData.isEmpty)
        {
            UpdateSellTimer();
        }
    }
    
    #endregion
    
    #region 初始化
    
    // 初始化格子
    public void Initialize(int slotIndex, int maxCapacity = 4, float sellTime = 5f)
    {
        slotData.slotIndex = slotIndex;
        slotData.maxCapacity = maxCapacity;
        slotData.totalSellTime = sellTime;
        
        // 初始化进度条颜色
        if (progressFill != null)
        {
            progressFill.color = progressFillColor;
        }
        
        // 初始化计时器文本颜色
        if (timerText != null)
        {
            timerText.color = timerTextColor;
        }
        
        UpdateUI();
    }
    
    #endregion
    
    #region 售卖逻辑
    
    // 更新售卖计时器
    private void UpdateSellTimer()
    {
        slotData.sellTimer += Time.deltaTime;
        
        // 更新UI
        UpdateProgressUI();
        
        // 检查是否售卖完成
        if (slotData.sellTimer >= slotData.totalSellTime)
        {
            // 售卖单个物品
            SellSingleItem();
        }
    }
    
    // 更新进度UI
    private void UpdateProgressUI()
    {
        if (progressFill != null)
        {
            progressFill.fillAmount = slotData.sellTimer / slotData.totalSellTime;
        }
        
        if (timerText != null)
        {
            float remainingTime = Mathf.Max(0, slotData.totalSellTime - slotData.sellTimer);
            timerText.text = remainingTime.ToString("F1") + "s";
        }
    }
    
    // 售卖单个物品
    private void SellSingleItem()
    {
        if (slotData.isEmpty) return;
        
        // 售卖一个物品
        int sellAmount = 1;
        ResourceType sellType = slotData.itemType;
        
        // 触发售卖事件
        OnItemSold?.Invoke(slotData.slotIndex, sellType, sellAmount);
        
        // 减少物品数量
        slotData.currentCount -= sellAmount;
        
        // 如果数量为0，清空格子
        if (slotData.currentCount <= 0)
        {
            ClearSlot();
        }
        else
        {
            // 重置计时器，继续售卖下一个物品
            slotData.sellTimer = 0f;
            slotData.isSelling = true;
            
            // 更新UI
            UpdateUI();
        }
    }
    
    // 售卖物品（原方法，用于手动售卖）
    public void SellItem()
    {
        if (slotData.isEmpty) return;
        
        int sellAmount = slotData.currentCount;
        ResourceType sellType = slotData.itemType;
        
        // 触发售卖事件
        OnItemSold?.Invoke(slotData.slotIndex, sellType, sellAmount);
        
        // 清空格子
        ClearSlot();
    }
    
    #endregion
    
    #region 物品管理
    
    // 添加物品到格子
    public bool AddItem(ResourceType itemType, int amount, out int addedAmount)
    {
        addedAmount = 0;
        
        // 如果格子为空，设置物品类型
        if (slotData.isEmpty)
        {
            slotData.itemType = itemType;
            slotData.isEmpty = false;
            
            int canAdd = Mathf.Min(amount, slotData.maxCapacity);
            slotData.currentCount = canAdd;
            addedAmount = canAdd;
            
            // 开始售卖计时
            slotData.sellTimer = 0f;
            slotData.isSelling = true;
            
            UpdateUI();
            return true;
        }
        // 如果格子已经有物品，检查是否是同类型
        else if (slotData.itemType == itemType)
        {
            // 如果已经满了，不能添加
            if (IsFull())
            {
                addedAmount = 0;
                return false;
            }
            
            int canAdd = Mathf.Min(amount, slotData.maxCapacity - slotData.currentCount);
            slotData.currentCount += canAdd;
            addedAmount = canAdd;
            
            UpdateUI();
            return true;
        }
        // 类型不匹配，不能添加
        else
        {
            addedAmount = 0;
            return false;
        }
    }
    
    // 从格子移除物品
    public bool RemoveItem(int amount, out int actualRemoved)
    {
        actualRemoved = 0;
        
        if (slotData.isEmpty || slotData.currentCount <= 0)
            return false;
        
        actualRemoved = Mathf.Min(amount, slotData.currentCount);
        slotData.currentCount -= actualRemoved;
        
        // 如果数量为0，清空格子
        if (slotData.currentCount <= 0)
        {
            ClearSlot();
        }
        else
        {
            UpdateUI();
        }
        
        return actualRemoved > 0;
    }
    
    // 清空格子
    public void ClearSlot()
    {
        slotData.currentCount = 0;
        slotData.isEmpty = true;
        slotData.isSelling = false;
        slotData.sellTimer = 0f;
        UpdateUI();
    }
    
    #endregion
    
    #region UI更新
    
    // 更新UI显示
    private void UpdateUI()
    {
        if (slotData.isEmpty)
        {
            UpdateEmptyUI();
        }
        else
        {
            UpdateOccupiedUI();
        }
    }
    
    // 更新空槽位UI
    private void UpdateEmptyUI()
    {
        // 背景
        if (slotBackground != null)
        {
            slotBackground.color = emptySlotColor;
        }
        
        // 隐藏图标
        if (itemIcon != null)
        {
            itemIcon.gameObject.SetActive(false);
        }
        
        // 隐藏数量文本
        if (countText != null)
        {
            countText.gameObject.SetActive(false);
        }
        
        // 隐藏进度条
        if (progressFill != null)
        {
            progressFill.fillAmount = 0f;
            progressFill.gameObject.SetActive(false);
        }
        
        // 隐藏计时器
        if (timerText != null)
        {
            timerText.gameObject.SetActive(false);
        }
    }
    
    // 更新有物品的槽位UI
    private void UpdateOccupiedUI()
    {
        // 背景
        if (slotBackground != null)
        {
            slotBackground.color = fullSlotColor;
        }
        
        // 显示图标
        if (itemIcon != null)
        {
            itemIcon.gameObject.SetActive(true);
            
            // 获取对应资源的图标
            Sprite iconSprite = GetIconForType(slotData.itemType);
            if (iconSprite != null)
            {
                itemIcon.sprite = iconSprite;
                itemIcon.color = Color.white;
            }
            else
            {
                // 如果没有设置图标，使用颜色作为后备
                itemIcon.color = GetColorForType(slotData.itemType);
            }
        }
        
        // 显示数量文本
        if (countText != null)
        {
            countText.gameObject.SetActive(true);
            countText.text = slotData.currentCount.ToString();
            
            // 如果达到上限，显示为红色
            if (IsFull())
            {
                countText.color = Color.red;
            }
            else
            {
                countText.color = Color.white;
            }
        }
        
        // 显示进度条和计时器
        if (progressFill != null)
        {
            progressFill.gameObject.SetActive(true);
            progressFill.fillAmount = slotData.sellTimer / slotData.totalSellTime;
        }
        
        if (timerText != null)
        {
            timerText.gameObject.SetActive(true);
            float remainingTime = Mathf.Max(0, slotData.totalSellTime - slotData.sellTimer);
            timerText.text = remainingTime.ToString("F1") + "s";
        }
    }
    
    // 获取物品类型对应的图标
    private Sprite GetIconForType(ResourceType type)
    {
        foreach (var resourceIcon in resourceIcons)
        {
            if (resourceIcon.type == type)
            {
                return resourceIcon.icon;
            }
        }
        return null;
    }
    
    // 获取物品类型对应的颜色（备用）
    private Color GetColorForType(ResourceType type)
    {
        return type switch
        {
            ResourceType.Money => Color.yellow,
            ResourceType.watermelonJ => Color.red,
            ResourceType.orangeJ => new Color(1f, 0.5f, 0f), // 橙色
            ResourceType.tomatoJ => Color.red,
            ResourceType.mushroom => Color.white,
            _ => Color.white
        };
    }
    
    #endregion
    
    #region 获取方法
    
    // 获取格子的数据
    public ShopSlotData GetSlotData()
    {
        return slotData;
    }
    
    // 检查格子是否已满
    public bool IsFull()
    {
        return slotData.currentCount >= slotData.maxCapacity;
    }
    
    // 检查格子是否为空
    public bool IsEmpty()
    {
        return slotData.isEmpty || slotData.currentCount <= 0;
    }
    
    // 获取物品类型
    public ResourceType GetItemType()
    {
        return slotData.itemType;
    }
    
    // 获取当前数量
    public int GetCurrentCount()
    {
        return slotData.currentCount;
    }
    
    // 获取最大容量
    public int GetMaxCapacity()
    {
        return slotData.maxCapacity;
    }
    
    // 获取剩余容量
    public int GetRemainingCapacity()
    {
        return slotData.maxCapacity - slotData.currentCount;
    }
    
    // 获取售卖进度
    public float GetSellProgress()
    {
        if (slotData.isEmpty || slotData.totalSellTime <= 0)
            return 0f;
        
        return slotData.sellTimer / slotData.totalSellTime;
    }
    
    // 获取剩余时间
    public float GetRemainingTime()
    {
        if (slotData.isEmpty)
            return 0f;
        
        return Mathf.Max(0, slotData.totalSellTime - slotData.sellTimer);
    }
    
    // 是否正在售卖
    public bool IsSelling()
    {
        return slotData.isSelling;
    }
    
    #endregion
    
    #region 控制方法
    
    // 手动触发售卖
    public void ForceSell()
    {
        SellItem();
    }
    
    // 设置售卖时间
    public void SetSellTime(float time)
    {
        slotData.totalSellTime = Mathf.Max(0.1f, time);
    }
    
    // 暂停/继续售卖
    public void SetSelling(bool isSelling)
    {
        slotData.isSelling = isSelling;
    }
    
    #endregion
    
    #region 数据设置方法
    
    // 设置槽位数据（用于重排时转移数据）
    public void SetSlotData(ResourceType itemType, int count, float sellTimer, bool isSelling)
    {
        if (count <= 0)
        {
            ClearSlot();
            return;
        }
        
        slotData.itemType = itemType;
        slotData.currentCount = count;
        slotData.sellTimer = sellTimer;
        slotData.isSelling = isSelling;
        slotData.isEmpty = false;
        
        UpdateUI();
    }
    
    #endregion
    
    #region 事件处理
    
    // 点击事件处理
    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left)
        {
            OnSlotClicked?.Invoke(slotData.slotIndex);
        }
    }
    
    #endregion
}