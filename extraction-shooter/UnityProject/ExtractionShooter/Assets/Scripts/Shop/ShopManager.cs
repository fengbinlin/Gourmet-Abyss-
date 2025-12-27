using UnityEngine;
using UnityEngine.Events;
using System.Collections.Generic;
using System.Linq;
using System.Collections;

public class ShopManager : MonoBehaviour
{
    [Header("商店显示和隐藏")]
    private Vector3 originPosition; // 记录初始位置
    [Header("商店设置")]
    [SerializeField] private Transform gridParent; // Grid Layout Group的父物体
    [SerializeField] private GameObject shopSlotPrefab; // 商店格子预制体
    [SerializeField] private int shopSlotCount = 4; // 商店格子数量
    [SerializeField] private int slotCapacity = 4; // 每个格子的容量
    [SerializeField] private float sellTime = 5f; // 售卖所需时间(秒)

    [Header("售卖价格")]
    [SerializeField] private List<ResourcePrice> resourcePrices = new List<ResourcePrice>();

    [System.Serializable]
    public class ResourcePrice
    {
        public ResourceType type;
        public int pricePerUnit;   // 每个物品的价格
        public float sellTimePerUnit = 5f; // 每个物品售卖时间
    }

    [Header("UI引用")]
    [SerializeField] private GameObject shopCanvas; // 商店Canvas

    [Header("商店事件")]
    public UnityEvent<bool> OnShopStateChanged; // 商店状态变化事件（参数：是否有物品）
    public UnityEvent<ResourceType, int, int> OnItemSold; // 物品售出事件（类型，数量，总价）

    [Header("音效")]
    [SerializeField] private AudioClip sellSound; // 售卖音效

    [Header("调试")]
    [SerializeField] private bool debugMode = false;

    // 商店格子列表
    private List<ShopSlotUI> shopSlots = new List<ShopSlotUI>();

    // 状态
    private bool hasItems = false;
    private Coroutine autoUpdateCoroutine;
    private AudioSource audioSource;

    // 单例模式
    public static ShopManager Instance { get; private set; }
    // 添加：记录原始值的变量
    private int originalShopSlotCount = 4;
    private int originalSlotCapacity = 4;
    private float originalSellTime = 5f;
    private Dictionary<ResourceType, int> originalPrices = new Dictionary<ResourceType, int>();
    #region 初始化

    private void Awake()
    {
        DontDestroyOnLoad(gameObject);
        // 单例设置
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
        // 记录原始值
        originalShopSlotCount = shopSlotCount;
        originalSlotCapacity = slotCapacity;
        originalSellTime = sellTime;

        // 记录原始价格
        foreach (var price in resourcePrices)
        {
            originalPrices[price.type] = price.pricePerUnit;
        }
        // 检查MoneyBox是否存在
        if (MoneyChest.Instance == null)
        {
            Debug.LogWarning("MoneyBox实例不存在，金币将直接添加到GameValManager");
        }

        // 获取或添加AudioSource
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }

        // 初始化事件
        if (OnShopStateChanged == null)
        {
            OnShopStateChanged = new UnityEvent<bool>();
        }
        // 订阅 WeaponStatsManager 的事件
        if (WeaponStatsManager.Instance != null)
        {
            WeaponStatsManager.Instance.OnShopStatsChanged += UpdateShopStatsFromManager;
        }

        InitializeShop();
    }

    private void Start()
    {
        originPosition = this.transform.position;
        // 初始检查商店状态
        CheckAndUpdateShopState();

        // 开始自动更新状态
        if (autoUpdateCoroutine != null)
        {
            StopCoroutine(autoUpdateCoroutine);
        }
        autoUpdateCoroutine = StartCoroutine(AutoUpdateShopState());
        UpdateShopStatsFromManager();
    }
    public void HideShop()
    {
        print("隐藏商店");
        this.transform.position = new Vector3(0, -1000, 0);
    }
    public void ShowShop()
    {
        print("显示商店:" + originPosition.ToString());
        this.transform.position = originPosition;
    }
    // 初始化商店
    private void InitializeShop()
    {
        if (gridParent == null)
        {
            Debug.LogError("ShopManager: gridParent 未设置！");
            return;
        }

        if (shopSlotPrefab == null)
        {
            Debug.LogError("ShopManager: shopSlotPrefab 未设置！");
            return;
        }

        // 清除现有格子
        foreach (Transform child in gridParent)
        {
            Destroy(child.gameObject);
        }
        shopSlots.Clear();

        // 生成商店格子
        for (int i = 0; i < shopSlotCount; i++)
        {
            CreateShopSlot(i);
        }

        // 检查售卖价格是否包含所有资源类型
        ValidateResourcePrices();
    }

    // 验证资源价格
    private void ValidateResourcePrices()
    {
        foreach (ResourceType type in System.Enum.GetValues(typeof(ResourceType)))
        {
            if (!resourcePrices.Exists(p => p.type == type))
            {
                //Debug.LogWarning($"资源 {type} 没有设置售卖价格，将使用默认价格");
            }
        }
    }

    // 创建商店格子
    private ShopSlotUI CreateShopSlot(int slotIndex)
    {
        GameObject slotObj = Instantiate(shopSlotPrefab, gridParent);
        slotObj.name = $"ShopSlot_{slotIndex}";

        ShopSlotUI shopSlot = slotObj.GetComponent<ShopSlotUI>();
        if (shopSlot != null)
        {
            shopSlot.Initialize(slotIndex, slotCapacity, sellTime);
            shopSlot.OnItemSold += HandleItemSold;
        }
        else
        {
            Debug.LogError($"商店格子预制体缺少 ShopSlotUI 组件: {shopSlotPrefab.name}");
        }

        shopSlots.Add(shopSlot);
        return shopSlot;
    }

    #endregion

    #region 物品管理

    // 从玩家背包接收物品
    public bool ReceiveItemFromPlayer(ResourceType itemType, int amount)
    {
        if (amount <= 0)
        {
            Debug.LogWarning($"接收物品数量必须为正数: {itemType} {amount}");
            return false;
        }

        int remainingAmount = amount;

        // 第一步：尝试添加到已有的同类型格子里
        remainingAmount = AddToExistingSlots(itemType, remainingAmount);

        // 第二步：如果还有剩余，尝试添加到空格子
        if (remainingAmount > 0)
        {
            remainingAmount = AddToEmptySlots(itemType, remainingAmount);
        }

        // 如果还有剩余物品，表示商店已满
        if (remainingAmount > 0)
        {
            if (debugMode) Debug.LogWarning($"商店已满，无法接收 {itemType}，剩余: {remainingAmount}");
            return false;
        }

        // 更新商店状态
        CheckAndUpdateShopState();

        // 整理物品（向前移动）
        RearrangeItems();

        if (debugMode) Debug.Log($"成功接收 {amount} 个 {itemType} 到商店");
        return true;
    }

    // 添加到已有的同类型格子
    private int AddToExistingSlots(ResourceType itemType, int amount)
    {
        int remaining = amount;

        // 找到所有同类型且未满的格子
        var matchingSlots = shopSlots.Where(slot =>
            slot != null &&
            !slot.IsEmpty() &&
            slot.GetItemType() == itemType &&
            !slot.IsFull()
        ).ToList();

        // 按照当前数量从大到小排序，优先填满数量多的格子
        matchingSlots = matchingSlots.OrderByDescending(slot => slot.GetCurrentCount()).ToList();

        // 按顺序填满格子
        foreach (var slot in matchingSlots)
        {
            if (remaining <= 0) break;

            int canAdd = slot.GetRemainingCapacity();
            if (canAdd > 0)
            {
                float sellTimeForType = GetResourceSellTime(itemType) * WeaponStatsManager.Instance.sellTimeMultiplier;

                slot.AddItem(itemType, remaining, sellTimeForType, out int added);
                remaining -= added;
            }
        }

        return remaining;
    }
    public float GetResourceSellTime(ResourceType itemType)
    {
        ResourcePrice priceInfo = resourcePrices.Find(p => p.type == itemType);
        if (priceInfo != null)
            return priceInfo.sellTimePerUnit;

        return originalSellTime; // 默认值
    }
    // 添加到空格子
    private int AddToEmptySlots(ResourceType itemType, int amount)
    {
        int remaining = amount;

        // 找到所有空格子
        var emptySlots = shopSlots.Where(slot => slot != null && slot.IsEmpty()).ToList();

        // 添加到空格子
        foreach (var slot in emptySlots)
        {
            if (remaining <= 0) break;

            float sellTimeForType = GetResourceSellTime(itemType) * WeaponStatsManager.Instance.sellTimeMultiplier;

            slot.AddItem(itemType, remaining, sellTimeForType, out int added);
            remaining -= added;
        }

        return remaining;
    }

    // 整理物品（向前移动）
    public void RearrangeItems()
    {
        if (debugMode) Debug.Log("开始整理商店物品");

        // 保存所有有物品的槽位数据
        List<SlotInfo> slotInfos = new List<SlotInfo>();

        for (int i = 0; i < shopSlots.Count; i++)
        {
            ShopSlotUI slot = shopSlots[i];
            if (slot != null && !slot.IsEmpty())
            {
                // 获取槽位的完整数据
                ShopSlotUI.ShopSlotData data = slot.GetSlotData();
                slotInfos.Add(new SlotInfo
                {
                    originalIndex = i,
                    type = data.itemType,
                    count = data.currentCount,
                    sellTimer = data.sellTimer,
                    isSelling = data.isSelling
                });

                if (debugMode) Debug.Log($"保存槽位 {i} 数据: {data.itemType} x{data.currentCount}, 计时: {data.sellTimer:F2}, 售卖中: {data.isSelling}");
            }
        }

        // 清空所有槽位
        for (int i = 0; i < shopSlots.Count; i++)
        {
            if (shopSlots[i] != null)
            {
                shopSlots[i].ClearSlot();
            }
        }

        // 重新填充物品到前面的槽位，并恢复倒计时信息
        for (int i = 0; i < slotInfos.Count && i < shopSlots.Count; i++)
        {
            SlotInfo info = slotInfos[i];
            if (info.count > 0)
            {
                // 使用新的方法设置数据，包括倒计时信息
                shopSlots[i].SetSlotData(info.type, info.count, info.sellTimer, info.isSelling);

                if (debugMode) Debug.Log($"填充槽位 {i}: {info.type} x{info.count}, 计时: {info.sellTimer:F2}, 售卖中: {info.isSelling}");
            }
        }

        if (debugMode) Debug.Log($"商店物品整理完成，共 {slotInfos.Count} 个物品槽位有物品");
    }

    // 槽位信息（用于整理时保存数据）
    private class SlotInfo
    {
        public int originalIndex;
        public ResourceType type;
        public int count;
        public float sellTimer;
        public bool isSelling;
    }

    #endregion

    #region 售卖系统

    // 处理物品售出
    // 处理物品售出
    private void HandleItemSold(int slotIndex, ResourceType itemType, int amount)
    {
        if (debugMode) Debug.Log($"处理售卖: 槽位{slotIndex}, {itemType} x{amount}");

        // 计算总价
        int totalPrice = CalculatePrice(itemType, amount);

        // 将金币添加到存钱箱（而不是直接给玩家）
        if (MoneyChest.Instance != null)
        {
            MoneyChest.Instance.AddMoney(totalPrice);

            if (debugMode) Debug.Log($"金币已存入存钱箱: {totalPrice} 金币");
        }
        else
        {
            Debug.LogWarning("MoneyBox实例不存在，无法存入金币");
            // 回退到原来的逻辑，直接给玩家金币
            if (GameValManager.Instance != null)
            {
                GameValManager.Instance.AddResource(ResourceType.Money, totalPrice);
            }
        }

        // 播放音效
        if (sellSound != null)
        {
            PlaySound(sellSound);
        }

        // 触发售出事件
        OnItemSold?.Invoke(itemType, amount, totalPrice);

        if (debugMode) Debug.Log($"物品售出: {amount} 个 {itemType}, 获得 {totalPrice} 金币，已存入存钱箱");

        // 售出后检查是否需要重排
        CheckAndRearrangeAfterSell();
    }

    // 售出后检查并重排
    private void CheckAndRearrangeAfterSell()
    {
        // 检查是否有空槽位
        bool hasEmptySlot = false;
        bool hasItemAfterEmpty = false;

        for (int i = 0; i < shopSlots.Count; i++)
        {
            if (shopSlots[i] == null) continue;

            if (shopSlots[i].IsEmpty())
            {
                hasEmptySlot = true;
            }
            else if (hasEmptySlot)
            {
                hasItemAfterEmpty = true;
                break;
            }
        }

        // 如果前面有空槽位，且后面有物品，则重排
        if (hasEmptySlot && hasItemAfterEmpty)
        {
            RearrangeItems();
        }
    }

    // 计算价格
    public int CalculatePrice(ResourceType itemType, int amount)
    {
        ResourcePrice priceInfo = resourcePrices.Find(p => p.type == itemType);
        if (priceInfo != null)
        {
            return priceInfo.pricePerUnit * amount;
        }

        // 默认价格
        return GetDefaultPrice(itemType, amount);
    }

    // 获取默认价格
    private int GetDefaultPrice(ResourceType itemType, int amount)
    {
        int pricePerUnit = itemType switch
        {
            ResourceType.Money => 1,
            ResourceType.watermelonJ => 10,
            ResourceType.orangeJ => 15,
            ResourceType.tomatoJ => 12,
            ResourceType.mushroom => 20,
            _ => 5
        };

        return pricePerUnit * amount;
    }

    // 手动售卖指定槽位的物品
    public bool SellSlotItem(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= shopSlots.Count)
        {
            Debug.LogWarning($"槽位索引无效: {slotIndex}");
            return false;
        }

        ShopSlotUI slot = shopSlots[slotIndex];
        if (slot == null || slot.IsEmpty())
        {
            Debug.LogWarning($"槽位 {slotIndex} 为空，无法售卖");
            return false;
        }

        ResourceType itemType = slot.GetItemType();
        int amount = slot.GetCurrentCount();

        slot.ForceSell();
        return true;
    }

    #endregion

    #region 状态管理

    // 自动更新商店状态
    private IEnumerator AutoUpdateShopState()
    {
        while (true)
        {
            yield return new WaitForSeconds(1f); // 每秒检查一次
            CheckAndUpdateShopState();
        }
    }

    // 检查并更新商店状态
    public void CheckAndUpdateShopState()
    {
        bool newHasItems = false;

        // 检查是否有物品
        foreach (var slot in shopSlots)
        {
            if (slot != null && !slot.IsEmpty())
            {
                newHasItems = true;
                break;
            }
        }

        // 如果状态变化，触发事件
        if (newHasItems != hasItems)
        {
            hasItems = newHasItems;
            OnShopStateChanged?.Invoke(hasItems);

            if (debugMode) Debug.Log($"商店状态变化: {(hasItems ? "有物品" : "空")}");
        }
    }

    // 检查商店是否能接收指定数量的物品
    public bool CanReceiveItem(ResourceType itemType, int amount)
    {
        if (amount <= 0) return false;

        int remainingCapacity = 0;

        // 计算同类型格子的剩余容量
        var matchingSlots = shopSlots.Where(slot =>
            slot != null &&
            !slot.IsEmpty() &&
            slot.GetItemType() == itemType
        ).ToList();

        foreach (var slot in matchingSlots)
        {
            if (!slot.IsFull())
            {
                remainingCapacity += slot.GetRemainingCapacity();
            }
        }

        // 计算空格子的总容量
        var emptySlots = shopSlots.Where(slot => slot != null && slot.IsEmpty()).ToList();
        remainingCapacity += emptySlots.Count * slotCapacity;

        if (debugMode) Debug.Log($"商店可接收容量检查: {itemType} x{amount}, 可接收容量: {remainingCapacity}");

        return remainingCapacity >= amount;
    }

    #endregion

    #region 查询方法

    // 获取商店中指定类型物品的总数量
    public int GetItemCountInShop(ResourceType itemType)
    {
        return shopSlots.Where(slot => slot != null && !slot.IsEmpty() && slot.GetItemType() == itemType)
                       .Sum(slot => slot.GetCurrentCount());
    }

    // 获取指定索引的格子
    public ShopSlotUI GetShopSlot(int index)
    {
        if (index >= 0 && index < shopSlots.Count)
        {
            return shopSlots[index];
        }
        return null;
    }

    // 获取物品价格
    public int GetResourcePrice(ResourceType itemType)
    {
        ResourcePrice priceInfo = resourcePrices.Find(p => p.type == itemType);
        if (priceInfo != null)
        {
            return priceInfo.pricePerUnit;
        }

        return GetDefaultPrice(itemType, 1);
    }

    // 检查商店是否有物品
    public bool HasItemsInShop()
    {
        return hasItems;
    }

    // 获取商店中物品的总价值
    public int GetTotalShopValue()
    {
        int totalValue = 0;

        foreach (var slot in shopSlots)
        {
            if (slot != null && !slot.IsEmpty())
            {
                totalValue += CalculatePrice(slot.GetItemType(), slot.GetCurrentCount());
            }
        }

        return totalValue;
    }

    // 获取商店中所有物品的列表
    public List<ShopItem> GetAllItemsInShop()
    {
        List<ShopItem> items = new List<ShopItem>();

        for (int i = 0; i < shopSlots.Count; i++)
        {
            if (shopSlots[i] != null && !shopSlots[i].IsEmpty())
            {
                items.Add(new ShopItem
                {
                    slotIndex = i,
                    type = shopSlots[i].GetItemType(),
                    count = shopSlots[i].GetCurrentCount(),
                    sellProgress = shopSlots[i].GetSellProgress(),
                    remainingTime = shopSlots[i].GetRemainingTime()
                });
            }
        }

        return items;
    }

    #endregion

    #region UI控制

    // 显示/隐藏商店UI
    public void SetShopUIVisible(bool visible)
    {
        if (shopCanvas != null)
        {
            shopCanvas.SetActive(visible);

            if (debugMode) Debug.Log($"设置商店UI可见性: {visible}");
        }
    }

    // 获取商店UI可见性
    public bool IsShopUIVisible()
    {
        return shopCanvas != null && shopCanvas.activeSelf;
    }

    // 设置商店UI透明度
    public void SetShopUIAlpha(float alpha)
    {
        if (shopCanvas != null)
        {
            CanvasGroup canvasGroup = shopCanvas.GetComponent<CanvasGroup>();
            if (canvasGroup != null)
            {
                canvasGroup.alpha = Mathf.Clamp01(alpha);
            }
        }
    }

    #endregion

    #region 辅助类和方法

    // 商店物品信息
    public class ShopItem
    {
        public int slotIndex;
        public ResourceType type;
        public int count;
        public float sellProgress;
        public float remainingTime;
    }

    // 播放音效
    private void PlaySound(AudioClip clip)
    {
        if (clip != null && audioSource != null)
        {
            audioSource.PlayOneShot(clip);
        }
    }

    #endregion

    #region 调试和工具方法

    // 打印商店状态
    public void PrintShopStatus()
    {
        Debug.Log("=== 商店状态 ===");
        Debug.Log($"总槽位: {shopSlotCount}");
        Debug.Log($"有物品: {hasItems}");
        Debug.Log($"当前物品数量: {GetAllItemsInShop().Count}");
        Debug.Log($"总价值: {GetTotalShopValue()}");

        for (int i = 0; i < shopSlots.Count; i++)
        {
            if (shopSlots[i] != null)
            {
                if (shopSlots[i].IsEmpty())
                {
                    Debug.Log($"槽位 {i}: 空");
                }
                else
                {
                    Debug.Log($"槽位 {i}: {shopSlots[i].GetItemType()} x{shopSlots[i].GetCurrentCount()} (进度: {shopSlots[i].GetSellProgress():P0})");
                }
            }
        }
        Debug.Log("================");
    }

    // 添加测试物品
    public void AddTestItem(ResourceType type, int amount)
    {
        ReceiveItemFromPlayer(type, amount);
    }
    private void UpdateShopStatsFromManager()
    {
        var wsm = WeaponStatsManager.Instance;
        if (wsm == null) return;

        // 1. 保存当前商店的所有物品数据
        List<ShopSlotUI.ShopSlotData> savedItems = SaveCurrentShopData();

        // 2. 更新商店属性
        shopSlotCount = wsm.shopSlotCount;
        slotCapacity = wsm.slotCapacity;
        sellTime = originalSellTime * wsm.sellTimeMultiplier;

        // 更新售卖价格
        foreach (var price in resourcePrices)
        {
            if (originalPrices.ContainsKey(price.type))
            {
                price.pricePerUnit = Mathf.RoundToInt(originalPrices[price.type] * wsm.sellPriceMultiplier);
            }
        }

        // 3. 重新初始化商店格子
        InitializeShop();

        // 4. 恢复物品数据
        RestoreShopData(savedItems);

        if (debugMode) Debug.Log("商店数值已根据 WeaponStatsManager 更新");
    }

    // 保存当前商店数据
    private List<ShopSlotUI.ShopSlotData> SaveCurrentShopData()
    {
        List<ShopSlotUI.ShopSlotData> savedData = new List<ShopSlotUI.ShopSlotData>();

        for (int i = 0; i < shopSlots.Count; i++)
        {
            ShopSlotUI slot = shopSlots[i];
            if (slot != null && !slot.IsEmpty())
            {
                // 保存每个有物品的槽位数据
                ShopSlotUI.ShopSlotData slotData = slot.GetSlotData();
                savedData.Add(slotData);

                if (debugMode) Debug.Log($"保存槽位 {i} 数据: {slotData.itemType} x{slotData.currentCount}");
            }
        }

        if (debugMode) Debug.Log($"保存了 {savedData.Count} 个槽位的物品数据");
        return savedData;
    }

    // 恢复商店数据
    private void RestoreShopData(List<ShopSlotUI.ShopSlotData> savedData)
    {
        if (savedData == null || savedData.Count == 0)
        {
            if (debugMode) Debug.Log("没有需要恢复的商店数据");
            return;
        }

        int slotsRestored = 0;
        int dataIndex = 0;

        // 遍历新商店的所有槽位
        for (int i = 0; i < shopSlots.Count && dataIndex < savedData.Count; i++)
        {
            ShopSlotUI slot = shopSlots[i];
            if (slot == null) continue;

            ShopSlotUI.ShopSlotData savedSlotData = savedData[dataIndex];

            // 计算可恢复的数量（不超过新容量）
            int restoreAmount = Mathf.Min(savedSlotData.currentCount, slotCapacity);

            if (restoreAmount > 0)
            {
                // 恢复数据
                slot.SetSlotData(
                    savedSlotData.itemType,
                    restoreAmount,
                    savedSlotData.sellTimer,
                    savedSlotData.isSelling
                );
                slotsRestored++;

                if (debugMode) Debug.Log($"恢复槽位 {i}: {savedSlotData.itemType} x{restoreAmount}");

                // 如果原数据有剩余物品，需要处理拆分
                int remaining = savedSlotData.currentCount - restoreAmount;
                if (remaining > 0)
                {
                    // 更新保存的数据，以便继续填充后续槽位
                    savedData[dataIndex] = new ShopSlotUI.ShopSlotData
                    {
                        itemType = savedSlotData.itemType,
                        currentCount = remaining,
                        sellTimer = 0, // 重新开始计时
                        isSelling = false
                    };
                    // 不增加dataIndex，继续处理同一物品
                }
                else
                {
                    dataIndex++; // 处理下一个物品
                }
            }
        }

        if (debugMode) Debug.Log($"成功恢复了 {slotsRestored} 个槽位的物品");

        // 如果有物品因为槽位减少而丢失
        if (dataIndex < savedData.Count)
        {
            int lostCount = 0;
            for (int i = dataIndex; i < savedData.Count; i++)
            {
                lostCount += savedData[i].currentCount;
            }

            if (lostCount > 0 && debugMode)
            {
                Debug.LogWarning($"商店升级后槽位不足，丢失了 {lostCount} 个物品");
            }
        }
    }
    // 清空商店
    public void ClearShop()
    {
        foreach (var slot in shopSlots)
        {
            if (slot != null)
            {
                slot.ClearSlot();
            }
        }

        CheckAndUpdateShopState();
        RearrangeItems();

        if (debugMode) Debug.Log("商店已清空");
    }

    #endregion

    #region 清理

    private void OnDestroy()
    {
        if (autoUpdateCoroutine != null)
        {
            StopCoroutine(autoUpdateCoroutine);
        }

        // 取消订阅事件
        foreach (var slot in shopSlots)
        {
            if (slot != null)
            {
                slot.OnItemSold -= HandleItemSold;
            }
        }
        if (WeaponStatsManager.Instance != null)
        {
            WeaponStatsManager.Instance.OnShopStatsChanged -= UpdateShopStatsFromManager;
        }
    }

    #endregion
}