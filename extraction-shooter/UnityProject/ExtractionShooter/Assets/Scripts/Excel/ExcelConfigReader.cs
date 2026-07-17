using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System.Globalization;
using System.Text.RegularExpressions;

[System.Serializable]
public class InitialStatsData
{
    public int statID;
    public string statName;
    public float initialValue;
}

[System.Serializable]
public class SkillConfigData
{
    public int skillID;
    public string skillName;
    public List<int> prerequisiteSkillIDs = new List<int>();
    public string position;
    public int maxLevel;
    public int isRare;
    public string description;
    public string buffEffects;
    public int costType;
    public int costAmount;
    public string iconPath;
    public string levelCostStr;
    public List<SkillLevelCost> levelCosts;
}

[DefaultExecutionOrder(100)]
public class ExcelConfigReader : MonoBehaviour
{
    [Header("配置文件路径")]
    public TextAsset initialStatsCSV;
    public TextAsset skillConfigCSV;

    private List<InitialStatsData> initialStats = new List<InitialStatsData>();
    private List<SkillConfigData> skillConfigs = new List<SkillConfigData>();

    public static ExcelConfigReader Instance { get; private set; }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            LoadAllConfigs();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void LoadAllConfigs()
    {
        LoadInitialStats();
        LoadSkillConfigs();
        ApplyInitialStats();
    }

    private void LoadInitialStats()
    {
        if (initialStatsCSV == null)
        {
            Debug.LogError("InitialStatsCSV文件未分配");
            return;
        }

        string[] lines = initialStatsCSV.text.Split('\n');
        for (int i = 1; i < lines.Length; i++)
        {
            if (string.IsNullOrEmpty(lines[i].Trim())) continue;

            string[] values = ParseCSVLine(lines[i]);
            if (values.Length >= 3)
            {
                InitialStatsData data = new InitialStatsData();
                if (int.TryParse(values[0], out int id)) data.statID = id;
                data.statName = values[1];
                if (float.TryParse(values[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float value))
                    data.initialValue = value;

                initialStats.Add(data);
            }
        }
    }
    private List<SkillLevelCost> ParseLevelCosts(string str)
    {
        var costs = new List<SkillLevelCost>();

        // 打印原始字符串
        //Debug.Log($"[ParseLevelCosts] 原始字符串: '{str}'");

        if (string.IsNullOrEmpty(str))
        {
            //Debug.LogWarning("[ParseLevelCosts] 传入的字符串为空，直接返回空列表");
            return costs;
        }

        // 用 '|' 拆分每个等级的消耗
        var levels = str.Split('|');
        //Debug.Log($"[ParseLevelCosts] 按等级拆分，共 {levels.Length} 项");

        foreach (var level in levels)
        {
            //Debug.Log($"[ParseLevelCosts] 当前等级消耗字符串: '{level}'");

            var parts = level.Split(':');
            if (parts.Length != 2)
            {
                //Debug.LogWarning($"[ParseLevelCosts] '{level}' 拆分后 parts.Length={parts.Length}，格式不正确，应为 '类型:数量'");
                continue;
            }

            //Debug.Log($"[ParseLevelCosts] 类型字符串: '{parts[0]}', 数量字符串: '{parts[1]}'");

            if (int.TryParse(parts[0], out int typeInt))
            {
                //Debug.Log($"[ParseLevelCosts] 解析类型成功: {typeInt} ({(ResourceType)typeInt})");
            }
            else
            {
                //Debug.LogWarning($"[ParseLevelCosts] 类型解析失败: '{parts[0]}'");
                continue;
            }

            if (int.TryParse(parts[1], out int costInt))
            {
                //Debug.Log($"[ParseLevelCosts] 解析数量成功: {costInt}");
            }
            else
            {
                //Debug.LogWarning($"[ParseLevelCosts] 数量解析失败: '{parts[1]}'");
                continue;
            }

            // 添加到结果列表
            costs.Add(new SkillLevelCost
            {
                costType = (ResourceType)typeInt,
                costAmount = costInt
            });

            //Debug.Log($"[ParseLevelCosts] 成功添加: 类型={typeInt}({(ResourceType)typeInt}), 数量={costInt}");
        }

        //Debug.Log($"[ParseLevelCosts] 返回列表，共 {costs.Count} 项");

        return costs;
    }
    private void LoadSkillConfigs()
    {
        if (skillConfigCSV == null)
        {
            Debug.LogError("SkillConfigCSV文件未分配");
            return;
        }

        string[] lines = skillConfigCSV.text.Split('\n');
        for (int i = 1; i < lines.Length; i++)
        {
            //print("解析行");
            if (string.IsNullOrEmpty(lines[i].Trim())) continue;

            string[] values = ParseCSVLine(lines[i]);
            if (values.Length >= 10)
            {
                SkillConfigData data = new SkillConfigData();
                if (int.TryParse(values[0], out int id)) data.skillID = id;
                data.skillName = values[1];
                if (!TryParsePrerequisiteSkillIDs(values[2], out data.prerequisiteSkillIDs))
                {
                    Debug.LogError($"技能 {data.skillID} 的前置技能ID格式无效: '{values[2]}'。仅支持用英文分号分隔的正整数，例如 13;15。");
                    continue;
                }
                data.position = values[3];
                if (int.TryParse(values[4], out int maxLevel)) data.maxLevel = maxLevel;
                if (int.TryParse(values[5], out int isRare)) data.isRare = isRare;
                data.description = values[6];
                data.buffEffects = values[7];
                //print("解析资源");
                data.levelCosts = ParseLevelCosts(values[8]);
                // if (int.TryParse(values[8], out int costType)) data.costType = costType;
                // if (int.TryParse(values[9], out int cost)) data.costAmount = cost;
                //print("路径"+values[9]);
                data.iconPath = values[9];

                skillConfigs.Add(data);
            }
        }
    }

    private static bool TryParsePrerequisiteSkillIDs(string rawValue, out List<int> skillIDs)
    {
        skillIDs = new List<int>();
        if (string.IsNullOrWhiteSpace(rawValue))
            return true;

        string[] entries = rawValue.Split(';');
        foreach (string entry in entries)
        {
            string value = entry.Trim();
            if (!Regex.IsMatch(value, @"^[1-9]\d*$") || !int.TryParse(value, out int skillID))
                return false;

            skillIDs.Add(skillID);
        }

        return true;
    }

    private void ApplyInitialStats()
    {
        var wsm = WeaponStatsManager.Instance;
        if (wsm != null)
        {
            foreach (var stat in initialStats)
                ApplyOneInitialStat(wsm, stat);

            wsm.RebuildDensityDictionary();
            if (wsm.levelParamRateItems != null)
            {
                for (int i = 0; i < wsm.levelParamRateItems.Count; i++)
                    wsm.levelParamRateItems[i]?.EnsureLevelBuffBases();
            }

            wsm.OnInventoryStatsChangedInvoke();
            wsm.OnShopStatsChangedInvoke();
            wsm.OnBattleStatsChangedInvoke();
            wsm.OnRestaurantStatsChangedInvoke();
            wsm.OnCustomerStatsChangedInvoke();
            wsm.OnLevelStatsChangedInvoke();
            wsm.OnWeaponStatsChangedInvoke();
        }

        ApplyFlyingCompanionInitialFromTable();
    }

    /// <summary>飞行跟班 statID 52–65，与技能树、PetManager 一致。</summary>
    private void ApplyFlyingCompanionInitialFromTable()
    {
        var pm = PetManager.Instance;
        if (pm == null) return;

        bool any = false;
        foreach (var stat in initialStats)
        {
            if (stat.statID < 52 || stat.statID > 65) continue;
            pm.ApplyFlyingCompanionTableValue(stat.statID, stat.initialValue);
            any = true;
        }

        if (any)
            pm.RefreshFlyingCompanionInitialSnapshot();
    }

    /// <summary>
    /// 与 SkillTreeInitializer statID 对齐；列表/引用型字段（如 mapDensityBindings）仍只在 Inspector 配置。
    /// </summary>
    private static void ApplyOneInitialStat(WeaponStatsManager wsm, InitialStatsData stat)
    {
        switch (stat.statID)
        {
            case 0: wsm.primaryFireRate = stat.initialValue; break;
            case 1: wsm.primaryPelletCount = (int)stat.initialValue; break;
            case 2: wsm.primaryPenetrationCount = (int)stat.initialValue; break;
            case 3: wsm.primaryBulletSpeed = stat.initialValue; break;
            case 4: wsm.primaryBulletSize = stat.initialValue; break;
            case 5: wsm.primaryBaseDamage = stat.initialValue; break;
            case 6: wsm.primaryCriticalChance = stat.initialValue; break;
            case 7: wsm.primaryCriticalMultiplier = stat.initialValue; break;
            case 8: wsm.primaryMaxTravelDistance = stat.initialValue; break;
            case 9: wsm.secondaryDamageValue = stat.initialValue; break;
            case 10: wsm.secondaryFireRate = stat.initialValue; break;
            case 11: wsm.secondaryLaserLength = stat.initialValue; break;
            case 12: wsm.secondaryLaserCount = (int)stat.initialValue; break;
            case 13: wsm.secondaryLaserWidth = stat.initialValue; break;
            case 14: wsm.secondaryCritChance = stat.initialValue; break;
            case 15: wsm.secondaryCritMultiplier = stat.initialValue; break;
            case 16: wsm.secondaryMaxChainCount = (int)stat.initialValue; break;
            case 17: wsm.secondaryChainSearchRadius = stat.initialValue; break;
            case 18: wsm.sellPriceMultiplier = stat.initialValue; break;
            case 19: wsm.sellTimeMultiplier = stat.initialValue; break;
            case 20: wsm.shopSlotCount = (int)stat.initialValue; break;
            case 21: wsm.slotCapacity = (int)stat.initialValue; break;
            case 22: wsm.inventorySlotCount = (int)stat.initialValue; break;
            case 23: wsm.inventorySlotCapacity = (int)stat.initialValue; break;
            case 24: wsm.oxygenMax = stat.initialValue; break;
            case 25: wsm.oxygenConsumeRate = stat.initialValue; break;
            case 26: wsm.primaryAmmoMax = (int)stat.initialValue; break;
            case 27: wsm.primaryAmmoConsumePerShot = (int)stat.initialValue; break;
            case 28: wsm.secondaryAmmoMax = (int)stat.initialValue; break;
            case 29: wsm.secondaryAmmoConsumePerShot = (int)stat.initialValue; break;
            case 30: wsm.defaultMapDensityMultiplier = stat.initialValue; break;
            case 31: wsm.bossDamageToOxygenMultiplier = Mathf.Max(0f, stat.initialValue); break;
            case 32: wsm.isSecondaryEnable = stat.initialValue != 0f; break;
            case 33: wsm.primaryEnableKillSplit = stat.initialValue != 0f; break;
            case 34: wsm.primaryKillSplitCount = Mathf.Max(1, (int)stat.initialValue); break;
            case 35: wsm.primaryKillSplitChildDamageRatio = Mathf.Clamp01(stat.initialValue); break;
            case 36: wsm.primaryEnableAOE = stat.initialValue != 0f; break;
            case 37: wsm.primaryAOERadius = Mathf.Max(0f, stat.initialValue); break;
            case 38: wsm.restaurantPotCount = Mathf.Max(1, (int)stat.initialValue); break;
            case 39: wsm.restaurantPlateCount = Mathf.Max(1, (int)stat.initialValue); break;
            case 40: wsm.cookingTimeMultiplier = Mathf.Max(0.01f, stat.initialValue); break;
            case 41: wsm.restaurantSellBonusRate = Mathf.Max(0f, stat.initialValue); break;
            case 42: wsm.restaurantMaxTotalCustomers = Mathf.Max(1, (int)stat.initialValue); break;
            case 43: wsm.restaurantMaxCustomersInside = Mathf.Max(1, (int)stat.initialValue); break;
            case 44: wsm.customerMoveSpeedMultiplier = Mathf.Max(0.01f, stat.initialValue); break;
            case 45: wsm.primaryMagazineCapacity = Mathf.Max(1, (int)stat.initialValue); break;
            case 46: wsm.secondaryMagazineCapacity = Mathf.Max(1, (int)stat.initialValue); break;
            case 47: wsm.primaryReloadDuration = Mathf.Max(0.01f, stat.initialValue); break;
            case 48: wsm.secondaryReloadDuration = Mathf.Max(0.01f, stat.initialValue); break;
            case 66: wsm.isPrimaryEnable = stat.initialValue != 0f; break;
            case 67: wsm.primaryKillSplitMaxIterations = Mathf.Max(0, (int)stat.initialValue); break;
            case 68: wsm.primaryAOEEdgeMinDamageRatio = Mathf.Clamp01(stat.initialValue); break;
            case 69: wsm.restaurantDishQueueSlotCount = Mathf.Max(1, (int)stat.initialValue); break;
            case 70: wsm.restaurantCustomerPrefabCount = Mathf.Max(0, (int)stat.initialValue); break;
        }
    }

    public List<SkillConfigData> GetSkillConfigs()
    {
        return skillConfigs;
    }

    public float GetInitialStatValue(int statID)
    {
        foreach (var stat in initialStats)
        {
            if (stat.statID == statID)
                return stat.initialValue;
        }
        return 0f;
    }

    private string[] ParseCSVLine(string line)
    {
        var result = new List<string>();
        bool inQuotes = false;
        var field = new System.Text.StringBuilder();

        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];

            if (c == '"')
            {
                inQuotes = !inQuotes;
            }
            else if (c == ',' && !inQuotes)
            {
                result.Add(field.ToString());
                field.Length = 0;
            }
            else
            {
                field.Append(c);
            }
        }
        result.Add(field.ToString());
        return result.ToArray();
    }
}
