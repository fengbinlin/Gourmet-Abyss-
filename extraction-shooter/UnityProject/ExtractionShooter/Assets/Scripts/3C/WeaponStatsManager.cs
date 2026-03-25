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

[System.Serializable]
public class LevelParamRateItem
{
    public string id;
    public float monsterCountRate = 1f;
    public float monsterRapidSpawnIntervalRate = 1f;
    public float monsterWaitTimeRate = 1f;
    public float propProbabilityRate = 1f;
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

    [Header("餐厅数值")]
    public int restaurantPotCount = 3;
    public int restaurantPlateCount = 3;
    [Tooltip("烹饪时间倍率：最终时间 = 原时间 * cookingTimeMultiplier")]
    public float cookingTimeMultiplier = 1f;
    [Tooltip("售卖价格加成比例：最终价格 = 原价格 * (1 + restaurantSellBonusRate)")]
    public float restaurantSellBonusRate = 0f;

    [Header("顾客数值")]
    public int restaurantCustomerPrefabCount = 3;
    public int restaurantMaxCustomersInside = 3;
    public int restaurantMaxTotalCustomers = 20;
    public float customerMoveSpeedMultiplier = 1f;

    [Header("背包数值")]
    public int inventorySlotCount = 4;        // 背包插槽个数
    public int inventorySlotCapacity = 4;    // 背包每个插槽的容量
    
    [Header("氧气与弹药数值")]
    public float oxygenMax = 100f;                // 氧气总量
    public float oxygenConsumeRate = 1f;          // 氧气每秒消耗速度
    [Tooltip("BOSS 攻击命中时：伤害数值 × 该系数 = 扣除的氧气量")]
    public float bossDamageToOxygenMultiplier = 1f;
    public int primaryAmmoMax = 100;                // 主武器弹容量
    public int primaryAmmoConsumePerShot = 1;         // 主武器每次射击消耗
    [Tooltip("主武器弹夹容量（充能完成后装填的最大子弹数）")]
    public int primaryMagazineCapacity = 100;
    public int secondaryAmmoMax = 50;               // 副武器弹容量
    public int secondaryAmmoConsumePerShot = 1;       // 副武器每次射击消耗
    [Tooltip("副武器弹夹容量（充能完成后装填的最大子弹数）")]
    public int secondaryMagazineCapacity = 50;

    [Header("弹夹充能 / 换弹夹时间")]
    [Tooltip("主武器弹夹耗尽后，充能(换弹夹)持续时间，期间禁止开火并播放抬起+Y轴旋转动画")]
    public float primaryReloadDuration = 0.8f;
    [Tooltip("副武器弹夹耗尽后，充能(换弹夹)持续时间，期间禁止开火并播放抬起+Z轴旋转动画")]
    public float secondaryReloadDuration = 0.8f;

    [Header("地图生成数值")]
    [Tooltip("默认地图密度乘积因子，当没有特定绑定时使用此值")]
    public float defaultMapDensityMultiplier = 1f;

    [Tooltip("各PlantGenerationSettings专属密度乘积绑定")]
    public List<MapDensityBinding> mapDensityBindings = new List<MapDensityBinding>();
    [Tooltip("各掉落物专属密度乘积绑定")]
    public List<EnemyLootBinding> enemtLootDensityBindings = new List<EnemyLootBinding>();

    [Header("关卡参数")]
    public List<LevelParamRateItem> levelParamRateItems = new List<LevelParamRateItem>();
    // 运行时用的快速查找字典
    private Dictionary<string, float> mapDensityMultipliers = new Dictionary<string, float>();

    // 数值变化事件
    public event System.Action OnShopStatsChanged;
    public event System.Action OnInventoryStatsChanged;
    public event System.Action OnBattleStatsChanged;
    public event System.Action OnRestaurantStatsChanged;
    public event System.Action OnCustomerStatsChanged;
    public event System.Action OnLevelStatsChanged;
    public event System.Action OnWeaponStatsChanged;

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

#if UNITY_EDITOR
    private void OnValidate()
    {
        // 让 Inspector 里直接改值也能实时同步到餐厅
        restaurantPotCount = Mathf.Max(1, restaurantPotCount);
        restaurantPlateCount = Mathf.Max(1, restaurantPlateCount);
        cookingTimeMultiplier = Mathf.Max(0.01f, cookingTimeMultiplier);
        restaurantSellBonusRate = Mathf.Max(0f, restaurantSellBonusRate);
        restaurantCustomerPrefabCount = Mathf.Max(1, restaurantCustomerPrefabCount);
        restaurantMaxCustomersInside = Mathf.Max(1, restaurantMaxCustomersInside);
        restaurantMaxTotalCustomers = Mathf.Max(1, restaurantMaxTotalCustomers);
        customerMoveSpeedMultiplier = Mathf.Max(0.01f, customerMoveSpeedMultiplier);

        if (Instance == this)
        {
            OnRestaurantStatsChanged?.Invoke();
            OnCustomerStatsChanged?.Invoke();
            OnLevelStatsChanged?.Invoke();
            OnWeaponStatsChanged?.Invoke();
        }
    }
#endif

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

    public void OnRestaurantStatsChangedInvoke()
    {
        OnRestaurantStatsChanged?.Invoke();
    }

    public void OnCustomerStatsChangedInvoke()
    {
        OnCustomerStatsChanged?.Invoke();
    }

    public void OnLevelStatsChangedInvoke()
    {
        OnLevelStatsChanged?.Invoke();
    }

    public void OnWeaponStatsChangedInvoke()
    {
        OnWeaponStatsChanged?.Invoke();
    }

    public void SetPrimaryReloadDuration(float duration)
    {
        primaryReloadDuration = Mathf.Max(0.01f, duration);
        OnWeaponStatsChanged?.Invoke();
    }

    public void SetSecondaryReloadDuration(float duration)
    {
        secondaryReloadDuration = Mathf.Max(0.01f, duration);
        OnWeaponStatsChanged?.Invoke();
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

    public void SetRestaurantPotCount(int count)
    {
        restaurantPotCount = Mathf.Max(1, count);
        OnRestaurantStatsChanged?.Invoke();
    }

    public void SetRestaurantPlateCount(int count)
    {
        restaurantPlateCount = Mathf.Max(1, count);
        OnRestaurantStatsChanged?.Invoke();
    }

    public void SetCookingTimeMultiplier(float multiplier)
    {
        cookingTimeMultiplier = Mathf.Max(0.01f, multiplier);
        OnRestaurantStatsChanged?.Invoke();
    }

    public void SetRestaurantSellBonusRate(float bonusRate)
    {
        restaurantSellBonusRate = Mathf.Max(0f, bonusRate);
        OnRestaurantStatsChanged?.Invoke();
    }

    public void SetRestaurantCustomerPrefabCount(int count)
    {
        restaurantCustomerPrefabCount = Mathf.Max(1, count);
        OnCustomerStatsChanged?.Invoke();
    }

    public void SetRestaurantMaxCustomersInside(int count)
    {
        restaurantMaxCustomersInside = Mathf.Max(1, count);
        OnCustomerStatsChanged?.Invoke();
    }

    public void SetRestaurantMaxTotalCustomers(int count)
    {
        restaurantMaxTotalCustomers = Mathf.Max(1, count);
        OnCustomerStatsChanged?.Invoke();
    }

    public void SetCustomerMoveSpeedMultiplier(float multiplier)
    {
        customerMoveSpeedMultiplier = Mathf.Max(0.01f, multiplier);
        OnCustomerStatsChanged?.Invoke();
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

    public float GetMonsterCountRate(string id)
    {
        return GetLevelRateItem(id)?.monsterCountRate ?? 1f;
    }

    public float GetMonsterRapidSpawnIntervalRate(string id)
    {
        return GetLevelRateItem(id)?.monsterRapidSpawnIntervalRate ?? 1f;
    }

    public float GetMonsterWaitTimeRate(string id)
    {
        return GetLevelRateItem(id)?.monsterWaitTimeRate ?? 1f;
    }

    public float GetPropProbabilityRate(string id)
    {
        return GetLevelRateItem(id)?.propProbabilityRate ?? 1f;
    }

    private LevelParamRateItem GetLevelRateItem(string id)
    {
        if (string.IsNullOrEmpty(id) || levelParamRateItems == null) return null;

        for (int i = 0; i < levelParamRateItems.Count; i++)
        {
            LevelParamRateItem item = levelParamRateItems[i];
            if (item == null) continue;
            if (item.id == id) return item;
        }

        return null;
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