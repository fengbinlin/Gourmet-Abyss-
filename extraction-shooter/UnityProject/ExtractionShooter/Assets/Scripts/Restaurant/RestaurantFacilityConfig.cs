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
public class FacilityUpgradeLevelEntry
{
    [Tooltip("从上一级升到本级所需资源；1 级通常全为 0")]
    public FacilityResourceCost[] upgradeCosts = { new FacilityResourceCost { amount = 0 } };

    [Header("厨房 — 烹饪时间倍率（越小越快，1=原速）")]
    public float cookingTimeMultiplier = 1f;

    [Header("摆菜台 — 每个碟子的总容量")]
    [Min(1)] public int plateCapacity = 5;

    [Header("餐桌 — 就餐速度倍率（越大越快，1=原速）")]
    public float diningSpeedMultiplier = 1f;
}

[Serializable]
public class FacilityUpgradeTrackEntry
{
    public RestaurantFacilityUpgradeType upgradeType = RestaurantFacilityUpgradeType.Kitchen;
    public string displayName = "厨房";
    [Tooltip("levels[0] = 1 级属性")]
    public FacilityUpgradeLevelEntry[] levels;
}

/// <summary>
/// 餐厅设施解锁与升级的统一配置。创建资产后可在 Inspector 直接编辑默认值。
/// 同时供 FacilityUnlockable / FacilityUnlockPanelUI 与 DecorationPanel 升级系统使用。
/// </summary>
[CreateAssetMenu(fileName = "RestaurantFacilityConfig", menuName = "Restaurant/Facility Config")]
public class RestaurantFacilityConfig : ScriptableObject
{
    [Header("设施解锁（点击 3D 设施解锁新槽位）")]
    public FacilityUnlockEntry[] unlockEntries;

    [Header("设施升级（DecorationPanel，同类设施属性统一）")]
    public FacilityUpgradeTrackEntry[] upgradeTracks;

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

    public FacilityUpgradeTrackEntry GetUpgradeTrack(RestaurantFacilityUpgradeType type)
    {
        if (upgradeTracks == null)
            return null;

        for (int i = 0; i < upgradeTracks.Length; i++)
        {
            if (upgradeTracks[i] != null && upgradeTracks[i].upgradeType == type)
                return upgradeTracks[i];
        }

        return null;
    }

    public FacilityUpgradeLevelEntry GetUpgradeLevel(RestaurantFacilityUpgradeType type, int level)
    {
        FacilityUpgradeTrackEntry track = GetUpgradeTrack(type);
        if (track?.levels == null || track.levels.Length == 0)
            return null;

        int index = Mathf.Clamp(level, 1, track.levels.Length) - 1;
        return track.levels[index];
    }

    public int GetUpgradeMaxLevel(RestaurantFacilityUpgradeType type)
    {
        FacilityUpgradeTrackEntry track = GetUpgradeTrack(type);
        return track?.levels != null && track.levels.Length > 0 ? track.levels.Length : 1;
    }

    public string GetUpgradeDisplayName(RestaurantFacilityUpgradeType type)
    {
        FacilityUpgradeTrackEntry track = GetUpgradeTrack(type);
        if (track != null && !string.IsNullOrEmpty(track.displayName))
            return track.displayName;

        return type switch
        {
            RestaurantFacilityUpgradeType.Kitchen => "厨房",
            RestaurantFacilityUpgradeType.ServingCounter => "摆菜台",
            RestaurantFacilityUpgradeType.Table => "餐桌",
            _ => type.ToString()
        };
    }

    public FacilityResourceCost[] GetUpgradeCosts(RestaurantFacilityUpgradeType type, int targetLevel)
    {
        FacilityUpgradeLevelEntry entry = GetUpgradeLevel(type, targetLevel);
        return entry?.upgradeCosts;
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

        FacilityUpgradeLevelEntry current = GetUpgradeLevel(type, currentLevel);
        FacilityUpgradeLevelEntry next = GetUpgradeLevel(type, currentLevel + 1);
        if (current == null || next == null)
            return string.Empty;

        List<string> parts = new List<string>();
        switch (type)
        {
            case RestaurantFacilityUpgradeType.Kitchen:
                AppendCookingSpeedDelta(parts, current.cookingTimeMultiplier, next.cookingTimeMultiplier);
                break;
            case RestaurantFacilityUpgradeType.ServingCounter:
                AppendIntDelta(parts, "碟子容量", current.plateCapacity, next.plateCapacity);
                break;
            case RestaurantFacilityUpgradeType.Table:
                AppendSpeedDelta(parts, "就餐速度", current.diningSpeedMultiplier, next.diningSpeedMultiplier);
                break;
        }

        return parts.Count > 0 ? string.Join("，", parts) : "属性提升";
    }

    public void ApplyUpgradeLevel(RestaurantFacilityUpgradeType type, int level)
    {
        FacilityUpgradeLevelEntry entry = GetUpgradeLevel(type, level);
        if (entry == null || WeaponStatsManager.Instance == null)
            return;

        switch (type)
        {
            case RestaurantFacilityUpgradeType.Kitchen:
                WeaponStatsManager.Instance.SetCookingTimeMultiplier(entry.cookingTimeMultiplier);
                break;
            case RestaurantFacilityUpgradeType.ServingCounter:
                WeaponStatsManager.Instance.SetRestaurantPlateCapacity(entry.plateCapacity);
                break;
            case RestaurantFacilityUpgradeType.Table:
                WeaponStatsManager.Instance.SetRestaurantDiningSpeedMultiplier(entry.diningSpeedMultiplier);
                break;
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

        if (upgradeTracks == null || upgradeTracks.Length == 0)
        {
            upgradeTracks = new[]
            {
                CreateDefaultKitchenTrack(),
                CreateDefaultServingCounterTrack(),
                CreateDefaultTableTrack()
            };
        }
    }

    private void OnValidate()
    {
        EnsureDefaultEntries();
    }

    private void Reset()
    {
        EnsureDefaultEntries();
    }

    private static FacilityUpgradeTrackEntry CreateDefaultKitchenTrack()
    {
        return new FacilityUpgradeTrackEntry
        {
            upgradeType = RestaurantFacilityUpgradeType.Kitchen,
            displayName = "厨房",
            levels = new[]
            {
                new FacilityUpgradeLevelEntry { cookingTimeMultiplier = 1f, upgradeCosts = new[] { new FacilityResourceCost { amount = 0 } } },
                new FacilityUpgradeLevelEntry { cookingTimeMultiplier = 0.9f, upgradeCosts = new[] { new FacilityResourceCost { amount = 150 } } },
                new FacilityUpgradeLevelEntry { cookingTimeMultiplier = 0.8f, upgradeCosts = new[] { new FacilityResourceCost { amount = 350 } } },
                new FacilityUpgradeLevelEntry { cookingTimeMultiplier = 0.72f, upgradeCosts = new[] { new FacilityResourceCost { amount = 700 } } },
                new FacilityUpgradeLevelEntry { cookingTimeMultiplier = 0.65f, upgradeCosts = new[] { new FacilityResourceCost { amount = 1200 } } }
            }
        };
    }

    private static FacilityUpgradeTrackEntry CreateDefaultServingCounterTrack()
    {
        return new FacilityUpgradeTrackEntry
        {
            upgradeType = RestaurantFacilityUpgradeType.ServingCounter,
            displayName = "摆菜台",
            levels = new[]
            {
                new FacilityUpgradeLevelEntry { plateCapacity = 5, upgradeCosts = new[] { new FacilityResourceCost { amount = 0 } } },
                new FacilityUpgradeLevelEntry { plateCapacity = 7, upgradeCosts = new[] { new FacilityResourceCost { amount = 120 } } },
                new FacilityUpgradeLevelEntry { plateCapacity = 9, upgradeCosts = new[] { new FacilityResourceCost { amount = 280 } } },
                new FacilityUpgradeLevelEntry { plateCapacity = 12, upgradeCosts = new[] { new FacilityResourceCost { amount = 550 } } },
                new FacilityUpgradeLevelEntry { plateCapacity = 15, upgradeCosts = new[] { new FacilityResourceCost { amount = 900 } } }
            }
        };
    }

    private static FacilityUpgradeTrackEntry CreateDefaultTableTrack()
    {
        return new FacilityUpgradeTrackEntry
        {
            upgradeType = RestaurantFacilityUpgradeType.Table,
            displayName = "餐桌",
            levels = new[]
            {
                new FacilityUpgradeLevelEntry { diningSpeedMultiplier = 1f, upgradeCosts = new[] { new FacilityResourceCost { amount = 0 } } },
                new FacilityUpgradeLevelEntry { diningSpeedMultiplier = 1.15f, upgradeCosts = new[] { new FacilityResourceCost { amount = 200 } } },
                new FacilityUpgradeLevelEntry { diningSpeedMultiplier = 1.3f, upgradeCosts = new[] { new FacilityResourceCost { amount = 450 } } },
                new FacilityUpgradeLevelEntry { diningSpeedMultiplier = 1.5f, upgradeCosts = new[] { new FacilityResourceCost { amount = 800 } } },
                new FacilityUpgradeLevelEntry { diningSpeedMultiplier = 1.75f, upgradeCosts = new[] { new FacilityResourceCost { amount = 1300 } } }
            }
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

    private static void AppendCookingSpeedDelta(List<string> parts, float currentTimeMult, float nextTimeMult)
    {
        if (Mathf.Approximately(currentTimeMult, nextTimeMult))
            return;

        float currentSpeed = 1f / Mathf.Max(0.01f, currentTimeMult);
        float nextSpeed = 1f / Mathf.Max(0.01f, nextTimeMult);
        parts.Add($"烹饪速度 {currentSpeed * 100f:0}%→{nextSpeed * 100f:0}%");
    }
}
