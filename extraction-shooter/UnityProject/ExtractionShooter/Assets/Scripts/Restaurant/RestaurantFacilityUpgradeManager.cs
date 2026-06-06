using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 读取 RestaurantFacilityConfig，管理厨房/摆菜台/餐桌的升级等级与属性应用。
/// </summary>
[DefaultExecutionOrder(-20)]
public class RestaurantFacilityUpgradeManager : MonoBehaviour
{
    public static RestaurantFacilityUpgradeManager Instance { get; private set; }

    private const string PrefsPrefix = "RestaurantFacilityLevel_";

    [Header("配置资产（必填）")]
    [SerializeField] private RestaurantFacilityConfig facilityConfig;

    public event Action<RestaurantFacilityUpgradeType, int> OnFacilityLevelChanged;

    private readonly Dictionary<RestaurantFacilityUpgradeType, int> _levels = new Dictionary<RestaurantFacilityUpgradeType, int>();

    public RestaurantFacilityConfig Config => facilityConfig;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        facilityConfig?.EnsureDefaultEntries();
        LoadLevels();
        ApplyAllCurrentLevels();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public int GetLevel(RestaurantFacilityUpgradeType type)
    {
        return _levels.TryGetValue(type, out int level) ? level : 1;
    }

    public int GetMaxLevel(RestaurantFacilityUpgradeType type)
    {
        if (facilityConfig == null)
            return 1;
        return facilityConfig.GetUpgradeMaxLevel(type);
    }

    public bool IsMaxLevel(RestaurantFacilityUpgradeType type)
    {
        return GetLevel(type) >= GetMaxLevel(type);
    }

    public bool IsUpgradeSupported(RestaurantFacilityUpgradeType type)
    {
        return type != RestaurantFacilityUpgradeType.Takeaway && facilityConfig != null;
    }

    public FacilityResourceCost[] GetUpgradeCosts(RestaurantFacilityUpgradeType type)
    {
        if (!IsUpgradeSupported(type) || IsMaxLevel(type))
            return Array.Empty<FacilityResourceCost>();

        return facilityConfig.GetUpgradeCosts(type, GetLevel(type) + 1);
    }

    public string GetDisplayName(RestaurantFacilityUpgradeType type)
    {
        return facilityConfig != null
            ? facilityConfig.GetUpgradeDisplayName(type)
            : type.ToString();
    }

    public string BuildUpgradePreviewText(RestaurantFacilityUpgradeType type)
    {
        if (!IsUpgradeSupported(type))
            return "暂未开放";

        return facilityConfig.BuildUpgradePreviewText(type, GetLevel(type), GetMaxLevel(type));
    }

    public bool CanAffordUpgrade(RestaurantFacilityUpgradeType type)
    {
        if (!IsUpgradeSupported(type) || IsMaxLevel(type) || facilityConfig == null)
            return false;

        return facilityConfig.CanAffordCosts(GetUpgradeCosts(type));
    }

    public bool TryUpgrade(RestaurantFacilityUpgradeType type)
    {
        if (!IsUpgradeSupported(type))
            return false;

        if (IsMaxLevel(type))
        {
            GlobalMessageUI.Show($"{GetDisplayName(type)} 已满级", 1.2f);
            return false;
        }

        if (facilityConfig == null)
            return false;

        FacilityResourceCost[] costs = GetUpgradeCosts(type);
        if (!facilityConfig.TryPayCosts(costs))
        {
            GlobalMessageUI.Show("资源不足，无法升级", 1.2f);
            return false;
        }

        int newLevel = GetLevel(type) + 1;
        _levels[type] = newLevel;
        SaveLevel(type, newLevel);
        ApplyLevel(type, newLevel);
        OnFacilityLevelChanged?.Invoke(type, newLevel);
        GlobalMessageUI.Show($"{GetDisplayName(type)} 升至 Lv.{newLevel}", 1.2f);
        return true;
    }

    public void ApplyAllCurrentLevels()
    {
        if (facilityConfig == null)
            return;

        ApplyLevel(RestaurantFacilityUpgradeType.Kitchen, GetLevel(RestaurantFacilityUpgradeType.Kitchen));
        ApplyLevel(RestaurantFacilityUpgradeType.ServingCounter, GetLevel(RestaurantFacilityUpgradeType.ServingCounter));
        ApplyLevel(RestaurantFacilityUpgradeType.Table, GetLevel(RestaurantFacilityUpgradeType.Table));
    }

    private void ApplyLevel(RestaurantFacilityUpgradeType type, int level)
    {
        if (!IsUpgradeSupported(type))
            return;

        facilityConfig.ApplyUpgradeLevel(type, level);
    }

    private void LoadLevels()
    {
        _levels.Clear();
        foreach (RestaurantFacilityUpgradeType type in Enum.GetValues(typeof(RestaurantFacilityUpgradeType)))
        {
            if (type == RestaurantFacilityUpgradeType.Takeaway)
                continue;

            int saved = PlayerPrefs.GetInt(PrefsPrefix + type, 1);
            _levels[type] = Mathf.Clamp(saved, 1, GetMaxLevel(type));
        }
    }

    private void SaveLevel(RestaurantFacilityUpgradeType type, int level)
    {
        PlayerPrefs.SetInt(PrefsPrefix + type, level);
        PlayerPrefs.Save();
    }
}
