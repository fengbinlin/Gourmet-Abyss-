using UnityEngine;
using System.Collections.Generic;


[System.Serializable]
public class MapDensityBinding
{
    public PlantGenerationSettings settings;   // 对应的Setting文件
    public float densityMultiplier = 1f; // 乘积因子
}

[System.Serializable]
public class EnemyLootBinding
{
    public ResourceType type;   // 对应的Setting文件
    public float lootDensityMultiplier = 1f; // 乘积因子
}

public class WeaponStatsManager : MonoBehaviour
{
    public static WeaponStatsManager Instance { get; private set; }

    [Header("宠物状态（进入战斗时由 PetManager 读取）")]
    public List<PetStateEntry> petStateList = new List<PetStateEntry>();

    [System.Serializable]
    public class PetStateEntry
    {
        public PetType petType;
        public bool isEnabled;
    }

    [Header("主武器数值")]
    public bool isPrimaryEnable=true;
    public float primaryFireRate = 0.2f;
    public int primaryPelletCount = 1;
    public int primaryPenetrationCount = 0;
    public float primaryBulletSpeed = 20f;
    public float primaryBulletSize = 1f;
    public float primaryBaseDamage = 10f;
    public float primaryCriticalChance = 0.1f;
    public float primaryCriticalMultiplier = 2f;
    public float primaryMaxTravelDistance = 100f;

    [Header("副武器数值")]
    public bool isSecondaryEnable=true;
    public float secondaryDamageValue = 20f;
    public float secondaryFireRate = 0.5f;
    public float secondaryLaserLength = 30f;
    public int secondaryLaserCount = 1;
    public float secondaryLaserWidth = 1f;
    public float secondaryCritChance = 0.1f;
    public float secondaryCritMultiplier = 2f;
    public int secondaryMaxChainCount = 3;
    public float secondaryChainSearchRadius = 10f;

    [Header("商店数值")]
    public float sellPriceMultiplier = 1f; // 售卖价格倍率
    public float sellTimeMultiplier = 1f;   // 售卖时间缩短倍率
    public int shopSlotCount = 4;
    public int slotCapacity = 4;

    [Header("背包数值")]
    public int inventorySlotCount = 4;        // 背包插槽个数
    public int inventorySlotCapacity = 4;    // 背包每个插槽的容量
    
    [Header("氧气与弹药数值")]
    public float oxygenMax = 100f;                // 氧气总量
    public float oxygenConsumeRate = 1f;          // 氧气每秒消耗速度
    public int primaryAmmoMax = 100;                // 主武器弹容量
    public int primaryAmmoConsumePerShot = 1;         // 主武器每次射击消耗
    public int secondaryAmmoMax = 50;               // 副武器弹容量
    public int secondaryAmmoConsumePerShot = 1;       // 副武器每次射击消耗

    [Header("地图生成数值")]
    [Tooltip("默认地图密度乘积因子，当没有特定绑定时使用此值")]
    public float defaultMapDensityMultiplier = 1f;

    [Tooltip("各PlantGenerationSettings专属密度乘积绑定")]
    public List<MapDensityBinding> mapDensityBindings = new List<MapDensityBinding>();
    [Tooltip("各掉落物专属密度乘积绑定")]
    public List<EnemyLootBinding> enemtLootDensityBindings = new List<EnemyLootBinding>();
    // 运行时用的快速查找字典
    private Dictionary<string, float> mapDensityMultipliers = new Dictionary<string, float>();

    // 数值变化事件
    public event System.Action OnShopStatsChanged;
    public event System.Action OnInventoryStatsChanged;
    public event System.Action OnBattleStatsChanged;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);

            // 初始化字典
            RebuildDensityDictionary();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// 从List重建字典
    /// </summary>
    public void RebuildDensityDictionary()
    {
        mapDensityMultipliers.Clear();
        foreach (var binding in mapDensityBindings)
        {
            if (binding.settings != null)
            {
                mapDensityMultipliers[binding.settings.name] = binding.densityMultiplier;
            }
        }
    }
    
    public void OnShopStatsChangedInvoke()
    {
        OnShopStatsChanged?.Invoke();
    }
    
    public void OnInventoryStatsChangedInvoke()
    {
        OnInventoryStatsChanged?.Invoke();
    }
    
    public void OnBattleStatsChangedInvoke()
    {
        OnBattleStatsChanged?.Invoke();
    }

    public void SetSellPriceMultiplier(float multiplier)
    {
        sellPriceMultiplier = multiplier;
        OnShopStatsChanged?.Invoke();
    }

    public void SetSellTimeMultiplier(float multiplier)
    {
        sellTimeMultiplier = multiplier;
        OnShopStatsChanged?.Invoke();
    }

    public void SetShopSlotCount(int count)
    {
        shopSlotCount = count;
        OnShopStatsChanged?.Invoke();
    }

    public void SetSlotCapacity(int capacity)
    {
        slotCapacity = capacity;
        OnShopStatsChanged?.Invoke();
    }

    /// <summary>
    /// 设置背包插槽个数
    /// </summary>
    public void SetInventorySlotCount(int count)
    {
        if (count <= 0) return;
        
        inventorySlotCount = Mathf.Max(1, count);
        OnInventoryStatsChanged?.Invoke();
    }

    /// <summary>
    /// 设置背包插槽容量
    /// </summary>
    public void SetInventorySlotCapacity(int capacity)
    {
        if (capacity <= 0) return;
        
        inventorySlotCapacity = Mathf.Max(1, capacity);
        OnInventoryStatsChanged?.Invoke();
    }

    /// <summary>
    /// 设置氧气总量
    /// </summary>
    public void SetOxygenMax(float value)
    {
        if (value <= 0) return;
        
        oxygenMax = value;
        OnBattleStatsChanged?.Invoke();
    }

    /// <summary>
    /// 设置氧气消耗速度
    /// </summary>
    public void SetOxygenConsumeRate(float rate)
    {
        oxygenConsumeRate = Mathf.Max(0.1f, rate);
        OnBattleStatsChanged?.Invoke();
    }

    /// <summary>
    /// 设置主武器弹容量
    /// </summary>
    public void SetPrimaryAmmoMax(int value)
    {
        if (value <= 0) return;
        
        primaryAmmoMax = value;
        OnBattleStatsChanged?.Invoke();
    }

    /// <summary>
    /// 设置主武器每次射击消耗
    /// </summary>
    public void SetPrimaryAmmoConsumePerShot(int value)
    {
        if (value <= 0) return;
        
        primaryAmmoConsumePerShot = value;
        OnBattleStatsChanged?.Invoke();
    }

    /// <summary>
    /// 设置副武器弹容量
    /// </summary>
    public void SetSecondaryAmmoMax(int value)
    {
        if (value <= 0) return;
        
        secondaryAmmoMax = value;
        OnBattleStatsChanged?.Invoke();
    }

    /// <summary>
    /// 设置副武器每次射击消耗
    /// </summary>
    public void SetSecondaryAmmoConsumePerShot(int value)
    {
        if (value <= 0) return;
        
        secondaryAmmoConsumePerShot = value;
        OnBattleStatsChanged?.Invoke();
    }

    /// <summary>
    /// 设置某个PlantGenerationSettings的密度乘积
    /// </summary>
    public void SetMapDensityMultiplier(PlantGenerationSettings settings, float multiplier)
    {
        if (settings == null) return;

        // 先查List，有则更新，没有则添加
        bool found = false;
        foreach (var binding in mapDensityBindings)
        {
            if (binding.settings == settings)
            {
                binding.densityMultiplier = multiplier;
                found = true;
                break;
            }
        }
        if (!found)
        {
            mapDensityBindings.Add(new MapDensityBinding
            {
                settings = settings,
                densityMultiplier = multiplier
            });
        }

        // 更新字典
        mapDensityMultipliers[settings.name] = multiplier;
    }

    /// <summary>
    /// 获取某个PlantGenerationSettings的密度乘积
    /// </summary>
    public float GetMapDensityMultiplier(PlantGenerationSettings settings)
    {
        if (settings == null) return defaultMapDensityMultiplier;
        if (mapDensityMultipliers.TryGetValue(settings.name, out float multiplier))
        {
            return multiplier;
        }
        else
        {
            return defaultMapDensityMultiplier;
        }
    }

    /// <summary>
    /// 查询宠物是否启用（供 PetManager 在进入战斗时读取）
    /// </summary>
    public bool IsPetEnabled(PetType petType)
    {
        if (petType == PetType.None) return false;
        if (petStateList == null) return false;

        for (int i = 0; i < petStateList.Count; i++)
        {
            var entry = petStateList[i];
            if (entry == null) continue;
            if (entry.petType != petType) continue;
            return entry.isEnabled;
        }

        return false;
    }

    /// <summary>
    /// 运行时设置宠物启用状态（可选：便于技能/配置系统写入）
    /// </summary>
    public void SetPetEnabled(PetType petType, bool enabled)
    {
        if (petType == PetType.None) return;
        if (petStateList == null) petStateList = new List<PetStateEntry>();

        for (int i = 0; i < petStateList.Count; i++)
        {
            var entry = petStateList[i];
            if (entry == null) continue;
            if (entry.petType != petType) continue;
            entry.isEnabled = enabled;
            return;
        }

        petStateList.Add(new PetStateEntry
        {
            petType = petType,
            isEnabled = enabled
        });
    }
}