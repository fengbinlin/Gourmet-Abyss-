using UnityEngine;

/// <summary>用于在启用 UI 时刷新餐厅界面。具体刷新逻辑由 RestaurantPanel 统一处理（含去重/默认选中/滚动归位）。</summary>
public class RestaurantUIRefreshOnEnable : MonoBehaviour
{
    [SerializeField] private RestaurantPanel restaurantPanel;

    private void OnEnable()
    {
        if (restaurantPanel != null)
            restaurantPanel.RefreshOnOpen();
    }
}
