using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class InventoryManager : MonoBehaviour
{
    public static InventoryManager instance;
    [Header("背包设置")]
    [SerializeField] private Transform gridParent; // Grid Layout Group的父物体
    [SerializeField] private GameObject slotPrefab; // 格子预制体
    [SerializeField] private int fixedSlotCount = 4; // 固定格子数量，不会增加
    [SerializeField] private int slotCapacity = 4; // 每个格子的容量

    // 格子列表
    private List<InventoryItemUI> slots = new List<InventoryItemUI>();

    private void Awake()
    {
        instance = this;
        InitializeInventory();
    }

    // 初始化背包
    private void InitializeInventory()
    {
        if (gridParent == null)
        {
            Debug.LogError("InventoryManager: gridParent 未设置！");
            return;
        }

        if (slotPrefab == null)
        {
            Debug.LogError("InventoryManager: slotPrefab 未设置！");
            return;
        }

        fixedSlotCount = WeaponStatsManager.Instance.inventorySlotCount;
        slotCapacity = WeaponStatsManager.Instance.inventorySlotCapacity;

        // 清除现有格子
        ClearExistingSlots();

        // 生成固定数量的格子
        for (int i = 0; i < fixedSlotCount; i++)
        {
            CreateNewSlot(i);
        }
    }

    private void Start()
    {
        // 订阅背包数值变化事件
        WeaponStatsManager.Instance.OnInventoryStatsChanged += OnInventoryStatsUpdated;

        // 测试代码
        AddItem(ResourceType.LootEggSmall, 4);
        AddItem(ResourceType.LootEggBig, 4);
        AddItem(ResourceType.LootMushroom, 4);
        AddItem(ResourceType.LootPumkin, 4);
    }

    private void OnDestroy()
    {
        // 取消订阅事件
        if (WeaponStatsManager.Instance != null)
        {
            WeaponStatsManager.Instance.OnInventoryStatsChanged -= OnInventoryStatsUpdated;
        }
    }

    // 当背包数值更新时的回调
    private void OnInventoryStatsUpdated()
    {
        int newSlotCount = WeaponStatsManager.Instance.inventorySlotCount;
        int newSlotCapacity = WeaponStatsManager.Instance.inventorySlotCapacity;

        Debug.Log($"背包数值更新: 新格子数={newSlotCount}, 新容量={newSlotCapacity}, 当前格子数={slots.Count}");

        // 更新所有现有格子的容量
        UpdateSlotCapacities(newSlotCapacity);

        // 如果新格子数大于当前格子数，增加新格子
        if (newSlotCount > slots.Count)
        {
            AddNewSlots(newSlotCount);
        }

        // 保存新值
        fixedSlotCount = newSlotCount;
        slotCapacity = newSlotCapacity;
    }

    // 更新所有现有格子的容量
    private void UpdateSlotCapacities(int newCapacity)
    {
        foreach (var slot in slots)
        {
            if (slot != null)
            {
                slot.UpdateSlotCapacity(newCapacity);
            }
        }
    }

    // 增加新格子（不销毁已有的）
    private void AddNewSlots(int targetSlotCount)
    {
        int slotsToAdd = targetSlotCount - slots.Count;

        for (int i = 0; i < slotsToAdd; i++)
        {
            int slotIndex = slots.Count; // 新格子的索引
            CreateNewSlot(slotIndex);
        }

        Debug.Log($"增加了 {slotsToAdd} 个新格子，现在总格子数: {slots.Count}");
    }

    // 清除现有格子
    private void ClearExistingSlots()
    {
        foreach (Transform child in gridParent)
        {
            Destroy(child.gameObject);
        }
        slots.Clear();
    }

    // 获取背包槽位数量
    public int GetSlotCount()
    {
        return slots.Count;
    }

    // 重新整理背包（将物品向前移动填补空位）
    public void ReorganizeInventory()
    {
        List<(ResourceType itemType, int itemCount)> items = new List<(ResourceType, int)>();

        // 首先收集所有非空槽位的物品信息
        for (int i = 0; i < slots.Count; i++)
        {
            if (slots[i] == null) continue;

            if (!slots[i].IsEmpty())
            {
                ResourceType itemType = slots[i].GetItemType();
                int itemCount = slots[i].GetCurrentCount();
                items.Add((itemType, itemCount));
            }
        }

        // 清空所有槽位
        for (int i = 0; i < slots.Count; i++)
        {
            if (slots[i] != null)
            {
                slots[i].ClearSlot();
            }
        }

        // 重新填充物品（保持原有顺序）
        for (int i = 0; i < items.Count && i < slots.Count; i++)
        {
            slots[i].AddItem(items[i].itemType, items[i].itemCount, out int added);
        }
    }

    // 创建新的格子
    private InventoryItemUI CreateNewSlot(int slotIndex)
    {
        GameObject slotObj = Instantiate(slotPrefab, gridParent);
        slotObj.name = $"InventorySlot_{slotIndex}";

        InventoryItemUI slotUI = slotObj.GetComponent<InventoryItemUI>();
        if (slotUI != null)
        {
            slotUI.Initialize(slotIndex, slotCapacity);
        }
        else
        {
            Debug.LogError($"格子预制体缺少 InventoryItemUI 组件: {slotPrefab.name}");
        }

        slots.Add(slotUI);
        return slotUI;
    }

    // 添加物品到背包
    public bool AddItem(ResourceType itemType, int amount)
    {
        if (amount <= 0)
        {
            Debug.LogWarning($"添加物品数量必须为正数: {itemType} {amount}");
            return false;
        }

        int remainingAmount = amount;

        // 第一步：尝试添加到已有的同类型格子里（优先填满）
        remainingAmount = AddToExistingSlots(itemType, remainingAmount);

        // 第二步：如果还有剩余，尝试添加到空格子
        if (remainingAmount > 0)
        {
            remainingAmount = AddToEmptySlots(itemType, remainingAmount);
        }

        // 如果还有剩余物品，表示背包已满
        if (remainingAmount > 0)
        {
            Debug.LogWarning($"背包已满，无法完全添加 {itemType}，剩余: {remainingAmount}");
            return false;
        }

        return true;
    }

    // 添加到已有的同类型格子（优先填满已有格子）
    private int AddToExistingSlots(ResourceType itemType, int amount)
    {
        int remaining = amount;

        // 找到所有同类型且未满的格子
        var matchingSlots = slots.Where(slot =>
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

            // 计算这个格子还能放多少
            int canAdd = slot.GetRemainingCapacity();
            if (canAdd > 0)
            {
                int addAmount = Mathf.Min(remaining, canAdd);
                slot.AddItem(itemType, addAmount, out int added);
                remaining -= added;
            }
        }

        return remaining;
    }

    // 添加到空格子
    private int AddToEmptySlots(ResourceType itemType, int amount)
    {
        int remaining = amount;

        // 找到所有空格子
        var emptySlots = slots.Where(slot => slot.IsEmpty()).ToList();

        // 添加到空格子
        foreach (var slot in emptySlots)
        {
            if (remaining <= 0) break;

            slot.AddItem(itemType, remaining, out int added);
            remaining -= added;
        }

        return remaining;
    }

    // 检查是否能添加指定数量的物品
    public bool CanAddItem(ResourceType itemType, int amount)
    {
        if (amount <= 0) return false;

        int remainingCapacity = 0;

        // 计算同类型格子的剩余容量
        var matchingSlots = slots.Where(slot =>
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
        var emptySlots = slots.Where(slot => slot.IsEmpty()).ToList();
        remainingCapacity += emptySlots.Count * slotCapacity;

        return remainingCapacity >= amount;
    }

    // 获取指定类型物品的总数量
    public int GetItemCount(ResourceType itemType)
    {
        return slots.Where(slot => !slot.IsEmpty() && slot.GetItemType() == itemType)
                   .Sum(slot => slot.GetCurrentCount());
    }

    // 获取指定索引的格子
    public InventoryItemUI GetSlot(int index)
    {
        if (index >= 0 && index < slots.Count)
        {
            return slots[index];
        }
        return null;
    }

    // 清空背包内后百分之多少的物体
    public void ClearBackpackByPercentage(float percentage, bool reorganizeAfter = true)
    {
        if (slots.Count == 0)
        {
            Debug.LogWarning("背包为空，无法清空");
            return;
        }

        // 确保百分比在0-1之间
        percentage = Mathf.Clamp01(percentage);

        if (percentage <= 0f)
        {
            Debug.Log("清空百分比为0，不执行任何操作");
            return;
        }

        // 计算需要清空的后百分之多少格子
        int slotsToClear = Mathf.CeilToInt(slots.Count * percentage);

        // 从后往前清空指定数量的格子
        int clearedSlots = 0;
        for (int i = slots.Count - 1; i >= 0 && clearedSlots < slotsToClear; i--)
        {
            if (slots[i] != null && !slots[i].IsEmpty())
            {
                slots[i].ClearSlot();
                clearedSlots++;
            }
        }

        Debug.Log($"已清空背包后 {percentage * 100}% 的物品，清空了 {clearedSlots} 个格子");

        // 如果需要，在清空后重新整理背包
        if (reorganizeAfter)
        {
            ReorganizeInventory();
        }
    }

    // 清空背包内后指定数量的格子
    public void ClearBackpackBySlotCount(int slotCount, bool reorganizeAfter = false)
    {
        if (slotCount <= 0)
        {
            Debug.LogWarning("清空格子数量必须大于0");
            return;
        }

        if (slots.Count == 0)
        {
            Debug.LogWarning("背包为空，无法清空");
            return;
        }

        // 确保不超过背包格子总数
        slotCount = Mathf.Min(slotCount, slots.Count);

        // 从后往前清空指定数量的格子
        int clearedSlots = 0;
        for (int i = slots.Count - 1; i >= 0 && clearedSlots < slotCount; i--)
        {
            if (slots[i] != null && !slots[i].IsEmpty())
            {
                slots[i].ClearSlot();
                clearedSlots++;
            }
        }

        Debug.Log($"已清空背包后 {slotCount} 个格子，实际清空了 {clearedSlots} 个格子");

        // 如果需要，在清空后重新整理背包
        if (reorganizeAfter)
        {
            ReorganizeInventory();
        }
    }

    // 清空背包内所有物品
    public void ClearAllItems(bool reorganizeAfter = false)
    {
        int clearedSlots = 0;

        for (int i = 0; i < slots.Count; i++)
        {
            if (slots[i] != null && !slots[i].IsEmpty())
            {
                slots[i].ClearSlot();
                clearedSlots++;
            }
        }

        Debug.Log($"已清空所有物品，清空了 {clearedSlots} 个格子");

        // 如果需要，在清空后重新整理背包
        if (reorganizeAfter)
        {
            ReorganizeInventory();
        }
    }
}