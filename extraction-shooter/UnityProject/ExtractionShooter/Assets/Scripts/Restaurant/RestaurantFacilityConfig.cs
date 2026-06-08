using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class FacilityResourceCost
{
    public ResourceType resourceType = ResourceType.Money;
    [Min(0)] public int amount = 100;
}

[Serializable]
public class FacilityUnlockEntry
{
    public FacilityType facilityType = FacilityType.Plate;
    public string displayName = "设施";
    public FacilityResourceCost[] unlockCosts = { new FacilityResourceCost() };
}

[Serializable]
public class KitchenUpgradeLevel
{
    [Tooltip("从上一级升到本级所需资源；1 级通常为 0")]
    public FacilityResourceCost[] upgradeCosts = { new FacilityResourceCost { amount = 0 } };
    [Tooltip("烹饪时间倍率（越小越快，1=原速）")]
    public float cookingTimeMultiplier = 1f;
}

[Serializable]
public class KitchenUpgradeTrack
{
    public string displayName = "厨房";
    [Tooltip("levels[0] = 1 级")]
    public KitchenUpgradeLevel[] levels;
}

[Serializable]
public class ServingCounterUpgradeLevel
{
    [Tooltip("从上一级升到本级所需资源；1 级通常为 0")]
    public FacilityResourceCost[] upgradeCosts = { new FacilityResourceCost { amount = 0 } };
    [Tooltip("每个碟子的总容量")]
    [Min(1)] public int plateCapacity = 5;
}

[Serializable]
public class ServingCounterUpgradeTrack
{
    public string displayName = "摆菜台";
    [Tooltip("levels[0] = 1 级")]
    public ServingCounterUpgradeLevel[] levels;
}

[Serializable]
public class TableUpgradeLevel
{
    [Tooltip("从上一级升到本级所需资源；1 级通常为 0")]
    public FacilityResourceCost[] upgradeCosts = { new FacilityResourceCost { amount = 0 } };
    [Tooltip("就餐速度倍率（越大越快，1=原速）")]
    public float diningSpeedMultiplier = 1f;
}

[Serializable]
public class TableUpgradeTrack
{
    public string displayName = "餐桌";
    [Tooltip("levels[0] = 1 级")]
    public TableUpgradeLevel[] levels;
}

[Serializable]
public class TakeawayUpgradeLevel
{
    [Tooltip("从上一级升到本级所需资源；1 级通常为 0")]
    public FacilityResourceCost[] upgradeCosts = { new FacilityResourceCost { amount = 0 } };
    [Tooltip("售价加成比例（0.1 = +10%）")]
    public float sellBonusRate;
    [Tooltip("场上顾客总量上限")]
    [Min(1)] public int maxTotalCustomers = 20;
}

[Serializable]
public class TakeawayUpgradeTrack
{
    public string displayName = "外卖";
    [Tooltip("levels[0] = 1 级")]
    public TakeawayUpgradeLevel[] levels;
}

/// <summary>
/// 餐厅设施解锁与升级的统一配置。
/// 升级固定为厨房 / 摆菜台 / 餐桌 / 外卖四条 Track，各 Track 仅包含自身属性。
/// </summary>
[CreateAssetMenu(fileName = "RestaurantFacilityConfig", menuName = "Restaurant/Facility Config")]
public class RestaurantFacilityConfig : ScriptableObject
{
    [Header("设施解锁（点击 3D 设施解锁新槽位）")]
    public FacilityUnlockEntry[] unlockEntries;

    [Header("设施升级 — 厨房")]
    public KitchenUpgradeTrack kitchenUpgrade = new KitchenUpgradeTrack();

    [Header("设施升级 — 摆菜台")]
    public ServingCounterUpgradeTrack servingCounterUpgrade = new ServingCounterUpgradeTrack();

    [Header("设施升级 — 餐桌")]
    public TableUpgradeTrack tableUpgrade = new TableUpgradeTrack();

    [Header("设施升级 — 外卖")]
    public TakeawayUpgradeTrack takeawayUpgrade = new TakeawayUpgradeTrack();

    public FacilityUnlockEntry GetUnlockEntry(FacilityType type)
    {
        if (unlockEntries == null)
            return null;

        for (int i = 0; i < unlockEntries.Length; i++)
        {
            if (unlockEntries[i] != null && unlockEntries[i].facilityType == type)
                return unlockEntries[i];
        }

        return null;
    }

    public int GetUpgradeMaxLevel(RestaurantFacilityUpgradeType type)
    {
        return GetLevelCount(type);
    }

    public string GetUpgradeDisplayName(RestaurantFacilityUpgradeType type)
    {
        string displayName = type switch
        {
            RestaurantFacilityUpgradeType.Kitchen => kitchenUpgrade?.displayName,
            RestaurantFacilityUpgradeType.ServingCounter => servingCounterUpgrade?.displayName,
            RestaurantFacilityUpgradeType.Table => tableUpgrade?.displayName,
            RestaurantFacilityUpgradeType.Takeaway => takeawayUpgrade?.displayName,
            _ => null
        };

        if (!string.IsNullOrEmpty(displayName))
            return displayName;

        return type switch
        {
            RestaurantFacilityUpgradeType.Kitchen => "厨房",
            RestaurantFacilityUpgradeType.ServingCounter => "摆菜台",
            RestaurantFacilityUpgradeType.Table => "餐桌",
            RestaurantFacilityUpgradeType.Takeaway => "外卖",
            _ => type.ToString()
        };
    }

    public FacilityResourceCost[] GetUpgradeCosts(RestaurantFacilityUpgradeType type, int targetLevel)
    {
        return GetUpgradeCostsInternal(type, targetLevel);
    }

    public bool CanAffordCosts(FacilityResourceCost[] costs)
    {
        if (costs == null || costs.Length == 0 || GameValManager.Instance == null)
            return true;

        Dictionary<ResourceType, int> required = BuildCostDictionary(costs);
        return GameValManager.Instance.HasEnoughResources(required);
    }

    public bool TryPayCosts(FacilityResourceCost[] costs)
    {
        if (costs == null || costs.Length == 0)
            return true;

        if (GameValManager.Instance == null)
            return false;

        Dictionary<ResourceType, int> required = BuildCostDictionary(costs);
        if (!GameValManager.Instance.HasEnoughResources(required))
            return false;

        return GameValManager.Instance.TryConsumeResources(required);
    }

    public static string FormatCosts(FacilityResourceCost[] costs)
    {
        if (costs == null || costs.Length == 0)
            return "免费";

        List<string> parts = new List<string>();
        for (int i = 0; i < costs.Length; i++)
        {
            if (costs[i] == null || costs[i].amount <= 0)
                continue;
            parts.Add($"{GetResourceDisplayName(costs[i].resourceType)} x{costs[i].amount}");
        }

        return parts.Count > 0 ? string.Join("，", parts) : "免费";
    }

    public string BuildUpgradePreviewText(RestaurantFacilityUpgradeType type, int currentLevel, int maxLevel)
    {
        if (currentLevel >= maxLevel)
            return "已达最高等级";

        List<string> parts = new List<string>();
        switch (type)
        {
            case RestaurantFacilityUpgradeType.Kitchen:
            {
                KitchenUpgradeLevel current = GetKitchenLevel(currentLevel);
                KitchenUpgradeLevel next = GetKitchenLevel(currentLevel + 1);
                if (current == null || next == null) return string.Empty;
                AppendCookingSpeedDelta(parts, current.cookingTimeMultiplier, next.cookingTimeMultiplier);
                break;
            }
            case RestaurantFacilityUpgradeType.ServingCounter:
            {
                ServingCounterUpgradeLevel current = GetServingCounterLevel(currentLevel);
                ServingCounterUpgradeLevel next = GetServingCounterLevel(currentLevel + 1);
                if (current == null || next == null) return string.Empty;
                AppendIntDelta(parts, "碟子容量", current.plateCapacity, next.plateCapacity);
                break;
            }
            case RestaurantFacilityUpgradeType.Table:
            {
                TableUpgradeLevel current = GetTableLevel(currentLevel);
                TableUpgradeLevel next = GetTableLevel(currentLevel + 1);
                if (current == null || next == null) return string.Empty;
                AppendSpeedDelta(parts, "就餐速度", current.diningSpeedMultiplier, next.diningSpeedMultiplier);
                break;
            }
            case RestaurantFacilityUpgradeType.Takeaway:
            {
                TakeawayUpgradeLevel current = GetTakeawayLevel(currentLevel);
                TakeawayUpgradeLevel next = GetTakeawayLevel(currentLevel + 1);
                if (current == null || next == null) return string.Empty;
                AppendPercentDelta(parts, "售价加成", current.sellBonusRate, next.sellBonusRate);
                AppendIntDelta(parts, "顾客总量上限", current.maxTotalCustomers, next.maxTotalCustomers);
                break;
            }
        }

        return parts.Count > 0 ? string.Join("，", parts) : "属性提升";
    }

    public void ApplyUpgradeLevel(RestaurantFacilityUpgradeType type, int level)
    {
        if (WeaponStatsManager.Instance == null)
            return;

        switch (type)
        {
            case RestaurantFacilityUpgradeType.Kitchen:
            {
                KitchenUpgradeLevel entry = GetKitchenLevel(level);
                if (entry != null)
                    WeaponStatsManager.Instance.SetCookingTimeMultiplier(entry.cookingTimeMultiplier);
                break;
            }
            case RestaurantFacilityUpgradeType.ServingCounter:
            {
                ServingCounterUpgradeLevel entry = GetServingCounterLevel(level);
                if (entry != null)
                    WeaponStatsManager.Instance.SetRestaurantPlateCapacity(entry.plateCapacity);
                break;
            }
            case RestaurantFacilityUpgradeType.Table:
            {
                TableUpgradeLevel entry = GetTableLevel(level);
                if (entry != null)
                    WeaponStatsManager.Instance.SetRestaurantDiningSpeedMultiplier(entry.diningSpeedMultiplier);
                break;
            }
            case RestaurantFacilityUpgradeType.Takeaway:
            {
                TakeawayUpgradeLevel entry = GetTakeawayLevel(level);
                if (entry != null)
                {
                    WeaponStatsManager.Instance.SetRestaurantSellBonusRate(entry.sellBonusRate);
                    WeaponStatsManager.Instance.SetRestaurantMaxTotalCustomers(entry.maxTotalCustomers);
                }
                break;
            }
        }
    }

    public void EnsureDefaultEntries()
    {
        if (unlockEntries == null || unlockEntries.Length == 0)
        {
            unlockEntries = new[]
            {
                new FacilityUnlockEntry { facilityType = FacilityType.Pot, displayName = "烹饪锅", unlockCosts = new[] { new FacilityResourceCost { amount = 100 } } },
                new FacilityUnlockEntry { facilityType = FacilityType.Plate, displayName = "摆菜碟", unlockCosts = new[] { new FacilityResourceCost { amount = 100 } } },
                new FacilityUnlockEntry { facilityType = FacilityType.Table, displayName = "餐桌", unlockCosts = new[] { new FacilityResourceCost { amount = 100 } } }
            };
        }

        if (kitchenUpgrade == null)
            kitchenUpgrade = new KitchenUpgradeTrack();
        if (kitchenUpgrade.levels == null || kitchenUpgrade.levels.Length == 0)
            kitchenUpgrade.levels = CreateDefaultKitchenLevels();

        if (servingCounterUpgrade == null)
            servingCounterUpgrade = new ServingCounterUpgradeTrack();
        if (servingCounterUpgrade.levels == null || servingCounterUpgrade.levels.Length == 0)
            servingCounterUpgrade.levels = CreateDefaultServingCounterLevels();

        if (tableUpgrade == null)
            tableUpgrade = new TableUpgradeTrack();
        if (tableUpgrade.levels == null || tableUpgrade.levels.Length == 0)
            tableUpgrade.levels = CreateDefaultTableLevels();

        if (takeawayUpgrade == null)
            takeawayUpgrade = new TakeawayUpgradeTrack();
        if (takeawayUpgrade.levels == null || takeawayUpgrade.levels.Length == 0)
            takeawayUpgrade.levels = CreateDefaultTakeawayLevels();
    }

    private KitchenUpgradeLevel GetKitchenLevel(int level)
    {
        if (kitchenUpgrade?.levels == null || kitchenUpgrade.levels.Length == 0)
            return null;
        int index = Mathf.Clamp(level, 1, kitchenUpgrade.levels.Length) - 1;
        return kitchenUpgrade.levels[index];
    }

    private ServingCounterUpgradeLevel GetServingCounterLevel(int level)
    {
        if (servingCounterUpgrade?.levels == null || servingCounterUpgrade.levels.Length == 0)
            return null;
        int index = Mathf.Clamp(level, 1, servingCounterUpgrade.levels.Length) - 1;
        return servingCounterUpgrade.levels[index];
    }

    private TableUpgradeLevel GetTableLevel(int level)
    {
        if (tableUpgrade?.levels == null || tableUpgrade.levels.Length == 0)
            return null;
        int index = Mathf.Clamp(level, 1, tableUpgrade.levels.Length) - 1;
        return tableUpgrade.levels[index];
    }

    private TakeawayUpgradeLevel GetTakeawayLevel(int level)
    {
        if (takeawayUpgrade?.levels == null || takeawayUpgrade.levels.Length == 0)
            return null;
        int index = Mathf.Clamp(level, 1, takeawayUpgrade.levels.Length) - 1;
        return takeawayUpgrade.levels[index];
    }

    private int GetLevelCount(RestaurantFacilityUpgradeType type)
    {
        return type switch
        {
            RestaurantFacilityUpgradeType.Kitchen => kitchenUpgrade?.levels?.Length ?? 1,
            RestaurantFacilityUpgradeType.ServingCounter => servingCounterUpgrade?.levels?.Length ?? 1,
            RestaurantFacilityUpgradeType.Table => tableUpgrade?.levels?.Length ?? 1,
            RestaurantFacilityUpgradeType.Takeaway => takeawayUpgrade?.levels?.Length ?? 1,
            _ => 1
        };
    }

    private FacilityResourceCost[] GetUpgradeCostsInternal(RestaurantFacilityUpgradeType type, int targetLevel)
    {
        return type switch
        {
            RestaurantFacilityUpgradeType.Kitchen => GetKitchenLevel(targetLevel)?.upgradeCosts,
            RestaurantFacilityUpgradeType.ServingCounter => GetServingCounterLevel(targetLevel)?.upgradeCosts,
            RestaurantFacilityUpgradeType.Table => GetTableLevel(targetLevel)?.upgradeCosts,
            RestaurantFacilityUpgradeType.Takeaway => GetTakeawayLevel(targetLevel)?.upgradeCosts,
            _ => null
        };
    }

    private void OnValidate()
    {
        EnsureDefaultEntries();
    }

    private void Reset()
    {
        EnsureDefaultEntries();
    }

    private static KitchenUpgradeLevel[] CreateDefaultKitchenLevels()
    {
        return new[]
        {
            new KitchenUpgradeLevel { cookingTimeMultiplier = 1f, upgradeCosts = new[] { new FacilityResourceCost { amount = 0 } } },
            new KitchenUpgradeLevel { cookingTimeMultiplier = 0.9f, upgradeCosts = new[] { new FacilityResourceCost { amount = 150 } } },
            new KitchenUpgradeLevel { cookingTimeMultiplier = 0.8f, upgradeCosts = new[] { new FacilityResourceCost { amount = 350 } } },
            new KitchenUpgradeLevel { cookingTimeMultiplier = 0.72f, upgradeCosts = new[] { new FacilityResourceCost { amount = 700 } } },
            new KitchenUpgradeLevel { cookingTimeMultiplier = 0.65f, upgradeCosts = new[] { new FacilityResourceCost { amount = 1200 } } }
        };
    }

    private static ServingCounterUpgradeLevel[] CreateDefaultServingCounterLevels()
    {
        return new[]
        {
            new ServingCounterUpgradeLevel { plateCapacity = 5, upgradeCosts = new[] { new FacilityResourceCost { amount = 0 } } },
            new ServingCounterUpgradeLevel { plateCapacity = 7, upgradeCosts = new[] { new FacilityResourceCost { amount = 120 } } },
            new ServingCounterUpgradeLevel { plateCapacity = 9, upgradeCosts = new[] { new FacilityResourceCost { amount = 280 } } },
            new ServingCounterUpgradeLevel { plateCapacity = 12, upgradeCosts = new[] { new FacilityResourceCost { amount = 550 } } },
            new ServingCounterUpgradeLevel { plateCapacity = 15, upgradeCosts = new[] { new FacilityResourceCost { amount = 900 } } }
        };
    }

    private static TableUpgradeLevel[] CreateDefaultTableLevels()
    {
        return new[]
        {
            new TableUpgradeLevel { diningSpeedMultiplier = 1f, upgradeCosts = new[] { new FacilityResourceCost { amount = 0 } } },
            new TableUpgradeLevel { diningSpeedMultiplier = 1.15f, upgradeCosts = new[] { new FacilityResourceCost { amount = 200 } } },
            new TableUpgradeLevel { diningSpeedMultiplier = 1.3f, upgradeCosts = new[] { new FacilityResourceCost { amount = 450 } } },
            new TableUpgradeLevel { diningSpeedMultiplier = 1.5f, upgradeCosts = new[] { new FacilityResourceCost { amount = 800 } } },
            new TableUpgradeLevel { diningSpeedMultiplier = 1.75f, upgradeCosts = new[] { new FacilityResourceCost { amount = 1300 } } }
        };
    }

    private static TakeawayUpgradeLevel[] CreateDefaultTakeawayLevels()
    {
        return new[]
        {
            new TakeawayUpgradeLevel { sellBonusRate = 0f, maxTotalCustomers = 20, upgradeCosts = new[] { new FacilityResourceCost { amount = 0 } } },
            new TakeawayUpgradeLevel { sellBonusRate = 0.05f, maxTotalCustomers = 25, upgradeCosts = new[] { new FacilityResourceCost { amount = 180 } } },
            new TakeawayUpgradeLevel { sellBonusRate = 0.1f, maxTotalCustomers = 30, upgradeCosts = new[] { new FacilityResourceCost { amount = 400 } } },
            new TakeawayUpgradeLevel { sellBonusRate = 0.15f, maxTotalCustomers = 35, upgradeCosts = new[] { new FacilityResourceCost { amount = 750 } } },
            new TakeawayUpgradeLevel { sellBonusRate = 0.2f, maxTotalCustomers = 40, upgradeCosts = new[] { new FacilityResourceCost { amount = 1200 } } }
        };
    }

    private static Dictionary<ResourceType, int> BuildCostDictionary(FacilityResourceCost[] costs)
    {
        Dictionary<ResourceType, int> dict = new Dictionary<ResourceType, int>();
        if (costs == null)
            return dict;

        for (int i = 0; i < costs.Length; i++)
        {
            if (costs[i] == null || costs[i].amount <= 0)
                continue;

            if (dict.ContainsKey(costs[i].resourceType))
                dict[costs[i].resourceType] += costs[i].amount;
            else
                dict[costs[i].resourceType] = costs[i].amount;
        }

        return dict;
    }

    private static string GetResourceDisplayName(ResourceType type)
    {
        if (GameValManager.Instance != null)
        {
            ResourceItem item = GameValManager.Instance.resources.Find(r => r.type == type);
            if (item != null && !string.IsNullOrEmpty(item.name))
                return item.name;
        }

        return type switch
        {
            ResourceType.Money => "金币",
            _ => type.ToString()
        };
    }

    private static void AppendIntDelta(List<string> parts, string label, int current, int next)
    {
        if (current == next)
            return;
        parts.Add($"{label} {current}→{next}");
    }

    private static void AppendSpeedDelta(List<string> parts, string label, float current, float next)
    {
        if (Mathf.Approximately(current, next))
            return;
        parts.Add($"{label} {current * 100f:0}%→{next * 100f:0}%");
    }

    private static void AppendPercentDelta(List<string> parts, string label, float current, float next)
    {
        if (Mathf.Approximately(current, next))
            return;
        parts.Add($"{label} {current * 100f:0}%→{next * 100f:0}%");
    }

    private static void AppendCookingSpeedDelta(List<string> parts, float currentTimeMult, float nextTimeMult)
    {
        if (Mathf.Approximately(currentTimeMult, nextTimeMult))
            return;

        float currentSpeed = 1f / Mathf.Max(0.01f, currentTimeMult);
        float nextSpeed = 1f / Mathf.Max(0.01f, nextTimeMult);
        parts.Add($"烹饪速度 {currentSpeed * 100f:0}%→{nextSpeed * 100f:0}%");
    }
}
