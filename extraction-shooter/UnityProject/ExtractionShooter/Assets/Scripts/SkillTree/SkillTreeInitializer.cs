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

        // 清理图标路径
        string cleanIconPath = iconPath.Trim();  // 移除首尾空格
        cleanIconPath = cleanIconPath.Replace("\r", "").Replace("\n", "");  // 移除换行符

        // 移除可能的文件扩展名
        string pathWithoutExtension = cleanIconPath;
        if (pathWithoutExtension.EndsWith(".png") || pathWithoutExtension.EndsWith(".jpg"))
        {
            pathWithoutExtension = pathWithoutExtension.Substring(0, pathWithoutExtension.LastIndexOf('.'));
        }

        // 检查路径是否为空
        if (string.IsNullOrWhiteSpace(pathWithoutExtension))
        {
            Debug.LogWarning($"图标路径为空，使用默认图标");
            return defaultIcon;
        }

        // 调试信息
        Debug.Log($"清理后的图标路径: '{pathWithoutExtension}'");
        Debug.Log($"路径长度: {pathWithoutExtension.Length}");
        Debug.Log($"第一个字符: {(int)pathWithoutExtension[0]}");
        Debug.Log($"最后一个字符: {(int)pathWithoutExtension[pathWithoutExtension.Length - 1]}");

        // 直接尝试加载，不使用 Path.Combine
        try
        {
            Sprite icon = Resources.Load<Sprite>(pathWithoutExtension);
            if (icon != null)
            {
                Debug.Log($"成功加载图标: {iconPath} -> {icon.name}");
                return icon;
            }
            else
            {
                // 尝试不同的加载方式
                Debug.LogWarning($"Resources.Load<Sprite>(\"{pathWithoutExtension}\") 返回null");

                // 尝试加载Texture2D然后创建Sprite
                Texture2D texture = Resources.Load<Texture2D>(pathWithoutExtension);
                if (texture != null)
                {
                    Debug.Log($"找到Texture2D: {texture.name}");
                    icon = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), Vector2.one * 0.5f);
                    return icon;
                }

                // 列出所有可用的资源
                UnityEngine.Object[] allResources = Resources.LoadAll("");
                Debug.Log($"Resources根目录共有 {allResources.Length} 个资源:");
                foreach (UnityEngine.Object obj in allResources)
                {
                    Debug.Log($"  - {obj.name} ({obj.GetType().Name})");
                }

                Debug.LogWarning($"无法加载图标: {iconPath}，使用默认图标");
                return defaultIcon;
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"加载图标时发生错误: {e.Message}");
            Debug.LogError($"StackTrace: {e.StackTrace}");
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
                binding.densityMultiplier = 1 * (1 + multiplier);
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
        // 获取初始值
        float initialValue = GetInitialStatValue(statID, wsm);

        switch (statID)
        {
            // 主武器
            case 0: wsm.primaryFireRate = initialValue * (1 + value); break; // 开火速率
            case 1: wsm.primaryPelletCount += (int)value; break; // 个数，保持加法
            case 2: wsm.primaryPenetrationCount += (int)value; break; // 个数，保持加法
            case 3: wsm.primaryBulletSpeed = initialValue * (1 + value); break; // 子弹速度
            case 4: wsm.primaryBulletSize = initialValue * (1 + value); break; // 子弹大小
            case 5: wsm.primaryBaseDamage = initialValue * (1 + value); break; // 基础伤害
            case 6: wsm.primaryCriticalChance = initialValue * (1 + value); break; // 暴击几率
            case 7: wsm.primaryCriticalMultiplier = initialValue * (1 + value); break; // 暴击倍率
            case 8: wsm.primaryMaxTravelDistance = initialValue * (1 + value); break; // 最大射程

            // 副武器
            case 9: wsm.secondaryDamageValue = initialValue * (1 + value); break; // 副武器伤害
            case 10: wsm.secondaryFireRate = initialValue * (1 + value); break; // 副武器开火速率
            case 11: wsm.secondaryLaserLength = initialValue * (1 + value); break; // 激光长度
            case 12: wsm.secondaryLaserCount += (int)value; break; // 个数，保持加法
            case 13: wsm.secondaryLaserWidth = initialValue * (1 + value); break; // 激光宽度
            case 14: wsm.secondaryCritChance = initialValue * (1 + value); break; // 副武器暴击几率
            case 15: wsm.secondaryCritMultiplier = initialValue * (1 + value); break; // 副武器暴击倍率
            case 16: wsm.secondaryMaxChainCount += (int)value; break; // 个数，保持加法
            case 17: wsm.secondaryChainSearchRadius = initialValue * (1 + value); break; // 连锁搜索半径

            // 商店相关
            case 18: wsm.sellPriceMultiplier = initialValue * (1 + value); break; // 售价乘数
            case 19: wsm.sellTimeMultiplier = initialValue * (1 + value); break; // 时间乘数
            case 20: wsm.shopSlotCount += (int)value; break; // 个数，保持加法
            case 21: wsm.slotCapacity += (int)value; break; // 个数，保持加法
            case 22: wsm.inventorySlotCount += (int)value; break; // 个数，保持加法
            case 23: wsm.inventorySlotCapacity += (int)value; break; // 个数，保持加法

            // 氧气系统
            case 24: wsm.oxygenMax = initialValue * (1 + value); break; // 氧气最大值
            case 25: wsm.oxygenConsumeRate = initialValue * (1 + value); break; // 氧气消耗速率

            // 弹药系统
            case 26: wsm.primaryAmmoMax = Mathf.Max(1, (int)(initialValue * (1 + value))); break; // 主武器弹药最大值
            case 27: wsm.primaryAmmoConsumePerShot = Mathf.Max(1, (int)(initialValue * (1 + value))); break; // 主武器每发弹药消耗
            case 28: wsm.secondaryAmmoMax = Mathf.Max(1, (int)(initialValue * (1 + value))); break; // 副武器弹药最大值
            case 29: wsm.secondaryAmmoConsumePerShot = Mathf.Max(1, (int)(initialValue * (1 + value))); break; // 副武器每发弹药消耗
        }
    }

    // 获取初始数值
    private float GetInitialStatValue(int statID, WeaponStatsManager wsm)
    {
        var configReader = ExcelConfigReader.Instance;
        if (configReader != null)
        {
            return configReader.GetInitialStatValue(statID);
        }

        // 如果无法从配置读取，则从当前值推断
        return GetCurrentStatValue(statID, wsm);
    }

    // 获取当前值（用于回退）
    private float GetCurrentStatValue(int statID, WeaponStatsManager wsm)
    {
        if (wsm == null) return 0f;

        switch (statID)
        {
            case 0: return wsm.primaryFireRate;
            case 1: return wsm.primaryPelletCount;
            case 2: return wsm.primaryPenetrationCount;
            case 3: return wsm.primaryBulletSpeed;
            case 4: return wsm.primaryBulletSize;
            case 5: return wsm.primaryBaseDamage;
            case 6: return wsm.primaryCriticalChance;
            case 7: return wsm.primaryCriticalMultiplier;
            case 8: return wsm.primaryMaxTravelDistance;
            case 9: return wsm.secondaryDamageValue;
            case 10: return wsm.secondaryFireRate;
            case 11: return wsm.secondaryLaserLength;
            case 12: return wsm.secondaryLaserCount;
            case 13: return wsm.secondaryLaserWidth;
            case 14: return wsm.secondaryCritChance;
            case 15: return wsm.secondaryCritMultiplier;
            case 16: return wsm.secondaryMaxChainCount;
            case 17: return wsm.secondaryChainSearchRadius;
            case 18: return wsm.sellPriceMultiplier;
            case 19: return wsm.sellTimeMultiplier;
            case 20: return wsm.shopSlotCount;
            case 21: return wsm.slotCapacity;
            case 22: return wsm.inventorySlotCount;
            case 23: return wsm.inventorySlotCapacity;
            case 24: return wsm.oxygenMax;
            case 25: return wsm.oxygenConsumeRate;
            case 26: return wsm.primaryAmmoMax;
            case 27: return wsm.primaryAmmoConsumePerShot;
            case 28: return wsm.secondaryAmmoMax;
            case 29: return wsm.secondaryAmmoConsumePerShot;
            default: return 0f;
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