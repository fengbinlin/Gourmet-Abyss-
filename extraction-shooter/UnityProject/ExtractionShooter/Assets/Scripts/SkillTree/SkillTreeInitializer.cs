using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System.Globalization;
using System.Text.RegularExpressions;

public class SkillTreeInitializer : MonoBehaviour
{
    [Header("技能树设置")]
    public SkillTree skillTree;
    public SkillNode skillNodePrefab;
    public RectTransform nodesParent;
    public Sprite defaultIcon; // 默认图标，当配置的图标加载失败时使用

    [Header("布局参数")]
    public float horizontalSpacing = 200f;
    public float verticalSpacing = 120f;

    [Header("地图密度绑定")]
    public List<MapDensityBinding> mapDensityBindings;

    private Dictionary<int, SkillNode> skillNodeMap = new Dictionary<int, SkillNode>();

    private void Start()
    {
        if (skillTree == null)
            skillTree = GetComponent<SkillTree>();

        // 初始化地图密度绑定
        InitializeMapDensityBindings();
        
        // 从配置生成技能树
        GenerateSkillTreeFromConfig();
    }

    private void InitializeMapDensityBindings()
    {
        var wsm = WeaponStatsManager.Instance;
        if (wsm == null) return;

        // 如果有手动配置的地图密度绑定，就使用
        if (mapDensityBindings != null && mapDensityBindings.Count > 0)
        {
            wsm.mapDensityBindings = new List<MapDensityBinding>(mapDensityBindings);
            wsm.RebuildDensityDictionary();
        }
    }

    private void GenerateSkillTreeFromConfig()
    {
        var configReader = ExcelConfigReader.Instance;
        if (configReader == null)
        {
            Debug.LogError("ExcelConfigReader未找到");
            return;
        }

        var skillConfigs = configReader.GetSkillConfigs();
        skillTree.allSkillNodes.Clear();
        skillNodeMap.Clear();

        // 第一遍：创建所有技能节点
        foreach (var config in skillConfigs)
        {
            SkillNode newNode = Instantiate(skillNodePrefab, nodesParent);
            newNode.name = $"SkillNode_{config.skillID}";

            SkillNodeData skillData = new SkillNodeData
            {
                skillID = config.skillID.ToString(),
                skillName = config.skillName,
                description = config.description,
                costType = (ResourceType)config.costType,
                costAmount = config.costAmount,
                maxLevel = config.maxLevel,
                currentLevel = 0,
                isLearned = false,
                isRare = config.isRare == 1
            };

            // 加载图标并设置到SkillNodeData
            Sprite icon = LoadSkillIcon(config.iconPath);
            skillData.icon = icon;
            
            // 如果SkillNode有iconImage组件，也直接设置
            if (newNode.iconImage != null)
            {
                newNode.iconImage.sprite = icon;
            }
            
            // 设置技能效果回调
            skillData.onSkillLearned = new UnityEngine.Events.UnityEvent();
            skillData.onSkillLearned.AddListener(() => ApplySkillEffects(config));

            newNode.skillData = skillData;
            
            // 设置位置
            Vector2 position = ParsePosition(config.position);
            newNode.GetComponent<RectTransform>().anchoredPosition = new Vector2(
                position.x * horizontalSpacing, 
                -position.y * verticalSpacing
            );

            skillTree.allSkillNodes.Add(newNode);
            skillNodeMap[config.skillID] = newNode;
        }

        // 第二遍：建立前置关系
        foreach (var config in skillConfigs)
        {
            if (!string.IsNullOrEmpty(config.prerequisiteIDs))
            {
                SkillNode currentNode = skillNodeMap[config.skillID];
                string[] prereqIDs = config.prerequisiteIDs.Split(';');

                foreach (string prereqID in prereqIDs)
                {
                    if (int.TryParse(prereqID.Trim(), out int id) && skillNodeMap.ContainsKey(id))
                    {
                        currentNode.prerequisites.Add(new PrerequisiteData
                        {
                            node = skillNodeMap[id],
                            requiredLevel = 1
                        });
                    }
                }
            }
        }
    }

    private Sprite LoadSkillIcon(string iconPath)
    {
        if (string.IsNullOrEmpty(iconPath))
        {
            Debug.LogWarning($"未配置图标路径，使用默认图标");
            return defaultIcon;
        }

        // 从Resources文件夹加载图标
        // 移除可能的文件扩展名
        string pathWithoutExtension = iconPath;
        if (pathWithoutExtension.EndsWith(".png") || pathWithoutExtension.EndsWith(".jpg"))
        {
            pathWithoutExtension = pathWithoutExtension.Substring(0, pathWithoutExtension.LastIndexOf('.'));
        }

        // 加载Sprite
        Sprite icon = Resources.Load<Sprite>(pathWithoutExtension);
        if (icon != null)
        {
            Debug.Log($"成功加载图标: {iconPath}");
            return icon;
        }
        else
        {
            Debug.LogWarning($"无法加载图标: {iconPath}，使用默认图标");
            return defaultIcon;
        }
    }

    private Vector2 ParsePosition(string positionStr)
    {
        if (string.IsNullOrEmpty(positionStr))
            return Vector2.zero;

        string[] parts = positionStr.Split(',');
        if (parts.Length == 2)
        {
            if (float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float x) && 
                float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float y))
                return new Vector2(x, y);
        }
        return Vector2.zero;
    }

    private void ApplySkillEffects(SkillConfigData config)
    {
        var wsm = WeaponStatsManager.Instance;
        if (wsm == null || string.IsNullOrEmpty(config.buffEffects)) return;

        // 解析buff效果字符串，支持普通效果和地图密度元组效果
        string[] effects = config.buffEffects.Split(';');
        foreach (string effect in effects)
        {
            ApplySingleEffect(effect.Trim(), wsm);
        }

        // 触发相应的事件
        TriggerStatChangeEvents(config.buffEffects, wsm);
    }

    private void ApplySingleEffect(string effect, WeaponStatsManager wsm)
    {
        // 检查是否是地图密度元组效果 (30,(levelID,multiplier))
        var mapDensityMatch = Regex.Match(effect, @"\(30,\((\d+),([\d.]+)\)\)");
        if (mapDensityMatch.Success)
        {
            // 地图密度特殊效果
            int levelID = int.Parse(mapDensityMatch.Groups[1].Value);
            float multiplier = float.Parse(mapDensityMatch.Groups[2].Value, CultureInfo.InvariantCulture);
            ApplyMapDensityEffect(levelID, multiplier, wsm);
            return;
        }

        // 普通效果 (statID,value)
        var normalMatch = Regex.Match(effect, @"\((\d+),([\d.-]+)\)");
        if (normalMatch.Success)
        {
            int statID = int.Parse(normalMatch.Groups[1].Value);
            float value = float.Parse(normalMatch.Groups[2].Value, CultureInfo.InvariantCulture);
            ApplyStatEffect(statID, value, wsm);
        }
    }

    private void ApplyMapDensityEffect(int levelID, float multiplier, WeaponStatsManager wsm)
    {
        // 通过levelID查找对应的地图密度绑定
        if (levelID >= 0 && levelID < wsm.mapDensityBindings.Count)
        {
            var binding = wsm.mapDensityBindings[levelID];
            if (binding.settings != null)
            {
                // 应用密度乘数
                binding.densityMultiplier *= multiplier;
                wsm.RebuildDensityDictionary();
            }
        }
        else
        {
            Debug.LogWarning($"未找到关卡ID {levelID} 对应的地图设置");
        }
    }

    private void ApplyStatEffect(int statID, float value, WeaponStatsManager wsm)
    {
        switch (statID)
        {
            case 0: wsm.primaryFireRate *= value; break; // 开火速率：值越小射速越快
            case 1: wsm.primaryPelletCount += (int)value; break;
            case 2: wsm.primaryPenetrationCount += (int)value; break;
            case 3: wsm.primaryBulletSpeed += value; break;
            case 4: wsm.primaryBulletSize += value; break;
            case 5: wsm.primaryBaseDamage += value; break;
            case 6: wsm.primaryCriticalChance += value; break;
            case 7: wsm.primaryCriticalMultiplier += value; break;
            case 8: wsm.primaryMaxTravelDistance += value; break;
            case 9: wsm.secondaryDamageValue += value; break;
            case 10: wsm.secondaryFireRate *= value; break; // 副武器开火速率
            case 11: wsm.secondaryLaserLength += value; break;
            case 12: wsm.secondaryLaserCount += (int)value; break;
            case 13: wsm.secondaryLaserWidth += value; break;
            case 14: wsm.secondaryCritChance += value; break;
            case 15: wsm.secondaryCritMultiplier += value; break;
            case 16: wsm.secondaryMaxChainCount += (int)value; break;
            case 17: wsm.secondaryChainSearchRadius += value; break;
            case 18: wsm.sellPriceMultiplier += value; break;
            case 19: wsm.sellTimeMultiplier *= value; break; // 时间乘数：值越小时间越短
            case 20: wsm.shopSlotCount += (int)value; break;
            case 21: wsm.slotCapacity += (int)value; break;
            case 22: wsm.inventorySlotCount += (int)value; break;
            case 23: wsm.inventorySlotCapacity += (int)value; break;
            case 24: wsm.oxygenMax *= value; break;
            case 25: wsm.oxygenConsumeRate *= value; break; // 消耗速率：值越小消耗越慢
            case 26: wsm.primaryAmmoMax = (int)(wsm.primaryAmmoMax * value); break;
            case 27: wsm.primaryAmmoConsumePerShot = Mathf.Max(1, (int)(wsm.primaryAmmoConsumePerShot * value)); break;
            case 28: wsm.secondaryAmmoMax = (int)(wsm.secondaryAmmoMax * value); break;
            case 29: wsm.secondaryAmmoConsumePerShot = Mathf.Max(1, (int)(wsm.secondaryAmmoConsumePerShot * value)); break;
        }
    }

    private void TriggerStatChangeEvents(string buffEffects, WeaponStatsManager wsm)
    {
        if (buffEffects.Contains("(18,") || buffEffects.Contains("(19,") || 
            buffEffects.Contains("(20,") || buffEffects.Contains("(21,"))
        {
            wsm.OnShopStatsChangedInvoke();
        }
        if (buffEffects.Contains("(22,") || buffEffects.Contains("(23,"))
        {
            wsm.OnInventoryStatsChangedInvoke();
        }
        if (buffEffects.Contains("(24,") || buffEffects.Contains("(25,") ||
            buffEffects.Contains("(26,") || buffEffects.Contains("(27,") ||
            buffEffects.Contains("(28,") || buffEffects.Contains("(29,"))
        {
            wsm.OnBattleStatsChangedInvoke();
        }
        if (buffEffects.Contains("(30,"))
        {
            wsm.RebuildDensityDictionary();
        }
    }
}