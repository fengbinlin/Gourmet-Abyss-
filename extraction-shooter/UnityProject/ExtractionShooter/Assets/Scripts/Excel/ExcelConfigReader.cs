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
    public string prerequisiteIDs; // 原来的，兼容旧数据
    public string position;
    public int maxLevel;
    public int isRare;
    public string description;
    public string buffEffects;
    public int costType;
    public int costAmount;
    public string iconPath;

    // ✅ 新增：前置技能等级需求字符串（例如 "1:2;2:3"）
    public string prerequisiteLevels;
}

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
            if (string.IsNullOrEmpty(lines[i].Trim())) continue;

            string[] values = ParseCSVLine(lines[i]);
            if (values.Length >= 10)
            {
                SkillConfigData data = new SkillConfigData();
                if (int.TryParse(values[0], out int id)) data.skillID = id;
                data.skillName = values[1];
                data.prerequisiteIDs = values[2];
                data.prerequisiteLevels = values[2];
                data.position = values[3];
                if (int.TryParse(values[4], out int maxLevel)) data.maxLevel = maxLevel;
                if (int.TryParse(values[5], out int isRare)) data.isRare = isRare;
                data.description = values[6];
                data.buffEffects = values[7];
                if (int.TryParse(values[8], out int costType)) data.costType = costType;
                if (int.TryParse(values[9], out int cost)) data.costAmount = cost;
                if (values.Length > 10) data.iconPath = values[10];

                skillConfigs.Add(data);
            }
        }
    }

    private void ApplyInitialStats()
    {
        var wsm = WeaponStatsManager.Instance;
        if (wsm == null) return;

        foreach (var stat in initialStats)
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
            }
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
        List<string> result = new List<string>();
        StringReader reader = new StringReader(line);
        bool inQuotes = false;
        string field = "";

        while (reader.Peek() != -1)
        {
            char c = (char)reader.Read();

            if (c == '"')
            {
                inQuotes = !inQuotes;
            }
            else if (c == ',' && !inQuotes)
            {
                result.Add(field);
                field = "";
            }
            else
            {
                field += c;
            }
        }

        result.Add(field);
        return result.ToArray();
    }
}