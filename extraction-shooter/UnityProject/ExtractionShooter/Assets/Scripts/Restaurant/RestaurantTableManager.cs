using System.Collections.Generic;
using Game.Core;
using UnityEngine;

/// <summary>
/// 根据 WeaponStatsManager.restaurantTableCount 同步场景中餐桌的显示数量。
/// </summary>
[DefaultExecutionOrder(-15)]
public class RestaurantTableManager : MonoSingleton<RestaurantTableManager>
{
    [Header("餐桌列表（按场景顺序；为空则自动查找场景中所有 Table）")]
    [SerializeField] private List<Table> allTables = new List<Table>();

    private bool _subscribed;

    protected override void OnAwake()
    {
        EnsureAllTablesPopulated();
    }

    private void OnEnable()
    {
        TrySubscribeStats();
        FacilityUnlockable.OnFacilityUnlocked += HandleFacilityUnlocked;
        SyncTablesFromStats();
    }

    private void Start()
    {
        TrySubscribeStats();
        SyncTablesFromStats();
    }

    private void OnDisable()
    {
        FacilityUnlockable.OnFacilityUnlocked -= HandleFacilityUnlocked;
        if (WeaponStatsManager.Instance != null && _subscribed)
            WeaponStatsManager.Instance.OnRestaurantStatsChanged -= SyncTablesFromStats;
        _subscribed = false;
    }

    private void HandleFacilityUnlocked(FacilityUnlockable unlockable)
    {
        if (unlockable == null || unlockable.Type != FacilityType.Table)
            return;

        SyncTablesFromStats();
    }

    // 原 OnDestroy 只做「清空 Instance」，该职责已由 MonoSingleton 基类接管。

    private void TrySubscribeStats()
    {
        if (_subscribed || WeaponStatsManager.Instance == null)
            return;

        WeaponStatsManager.Instance.OnRestaurantStatsChanged -= SyncTablesFromStats;
        WeaponStatsManager.Instance.OnRestaurantStatsChanged += SyncTablesFromStats;
        _subscribed = true;
    }

    public void RefreshTables()
    {
        SyncTablesFromStats();
    }

    private void EnsureAllTablesPopulated()
    {
        if (allTables != null && allTables.Count > 0)
            return;

        Table[] found = FindObjectsOfType<Table>(true);
        allTables = new List<Table>(found.Length);
        for (int i = 0; i < found.Length; i++)
        {
            if (found[i] != null)
                allTables.Add(found[i]);
        }
    }

    private void SyncTablesFromStats()
    {
        EnsureAllTablesPopulated();
        if (allTables == null || allTables.Count == 0 || WeaponStatsManager.Instance == null)
            return;

        int targetCount = Mathf.Clamp(WeaponStatsManager.Instance.restaurantTableCount, 0, allTables.Count);
        for (int i = 0; i < allTables.Count; i++)
        {
            Table table = allTables[i];
            if (table == null)
                continue;

            bool shouldShow = ShouldShowTable(table, i, targetCount);
            if (table.gameObject.activeSelf != shouldShow)
                table.gameObject.SetActive(shouldShow);

            FacilityUnlockable unlock = table.GetComponent<FacilityUnlockable>();
            unlock?.RefreshVisualState();
        }

        if (SeatManager.Instance != null)
            SeatManager.Instance.RefreshRegisteredSeats();
    }

    private static bool ShouldShowTable(Table table, int index, int activeCount)
    {
        if (table == null)
            return false;

        FacilityUnlockable unlock = table.GetComponent<FacilityUnlockable>();
        if (index < activeCount)
            return true;

        if (unlock != null && unlock.IsUnlocked)
            return true;

        return unlock != null && !unlock.IsUnlocked;
    }
}
