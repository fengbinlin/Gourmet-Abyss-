using Game.Core;
using UnityEngine;

/// <summary>
/// 商店/餐厅共享的槽位配置入口：当前用于控制餐厅餐碟（Plate）激活数量。
/// 场景中需放置一个实例；若不存在，RestaurantPanel 会回退到 WeaponStatsManager.restaurantPlateCount。
/// </summary>
[DefaultExecutionOrder(-38)]
public class ShopSlotManager : PersistentMonoSingleton<ShopSlotManager>
{
    [Header("餐厅餐碟")]
    [Tooltip("与 RestaurantPanel.allPlates 列表下标对应，控制激活的碟子数量")]
    [Min(1)] public int restaurantPlateSlotCount = 3;

    public event System.Action OnRestaurantPlateSlotsChanged;

#if UNITY_EDITOR
    private void OnValidate()
    {
        restaurantPlateSlotCount = Mathf.Max(1, restaurantPlateSlotCount);
        if (Instance == this)
            OnRestaurantPlateSlotsChanged?.Invoke();
    }
#endif

    public void SetRestaurantPlateSlotCount(int count)
    {
        restaurantPlateSlotCount = Mathf.Max(1, count);
        OnRestaurantPlateSlotsChanged?.Invoke();
    }
}
