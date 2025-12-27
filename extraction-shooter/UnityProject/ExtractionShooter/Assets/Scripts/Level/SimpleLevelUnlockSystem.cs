using UnityEngine;

public class SimpleLevelUnlockSystem : MonoBehaviour
{
    [Header("调试")]
    [SerializeField] private bool debugLog = true;
    
    public static SimpleLevelUnlockSystem Instance { get; private set; }
    
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }
    
    private void Start()
    {
        // 监听等级升级事件
        if (PlayerLevelManager.Instance != null)
        {
            PlayerLevelManager.Instance.OnLevelUp += OnPlayerLevelUp;
        }
        
        // 根据当前等级初始化解锁状态
        InitializeUnlocksByCurrentLevel();
    }
    
    private void OnDestroy()
    {
        if (PlayerLevelManager.Instance != null)
        {
            PlayerLevelManager.Instance.OnLevelUp -= OnPlayerLevelUp;
        }
    }
    
    // 根据当前等级初始化解锁状态
    private void InitializeUnlocksByCurrentLevel()
    {
        int currentLevel = PlayerLevelManager.Instance?.GetCurrentLevel() ?? 1;
        
        if (debugLog) Debug.Log($"根据等级{currentLevel}初始化解锁状态");
        
        // 先锁定所有区域，然后按等级重新解锁
        ResetAllUnlocks();
        
        // 解锁从等级1到当前等级的所有区域
        for (int level = 1; level <= currentLevel; level++)
        {
            UnlockRegionForLevel(level);
        }
    }
    
    // 玩家升级时的处理
    private void OnPlayerLevelUp(int newLevel)
    {
        if (debugLog) Debug.Log($"等级提升到 {newLevel}，解锁新区域");
        UnlockRegionForLevel(newLevel);
    }
    
    // 解锁对应等级的区域
    private void UnlockRegionForLevel(int level)
    {
        if (MapDataManager.Instance == null) return;
        
        // 计算要解锁的区域索引（等级-1，因为等级1对应第一个区域）
        int regionIndexToUnlock = level - 1;
        
        // 获取所有地图
        var allMaps = MapDataManager.Instance.GetAllMaps();
        
        int currentRegionCount = 0;
        
        // 遍历地图，找到对应索引的区域
        foreach (var map in allMaps)
        {
            // 先解锁地图
            if (!map.isUnlocked)
            {
                MapDataManager.Instance.UnlockMap(map.mapID);
            }
            
            // 解锁对应区域
            foreach (var region in map.regions)
            {
                if (currentRegionCount == regionIndexToUnlock)
                {
                    if (!region.isUnlocked)
                    {
                        MapDataManager.Instance.UnlockRegion(map.mapID, region.regionID);
                        
                        if (debugLog) Debug.Log($"等级{level}: 解锁区域 - {map.mapName} -> {region.regionName}");
                        
                        // 刷新UI
                        if (MapUIManager.Instance != null)
                        {
                            MapUIManager.Instance.RefreshUI();
                        }
                    }
                    return; // 完成解锁
                }
                currentRegionCount++;
            }
        }
        
        if (debugLog && regionIndexToUnlock >= currentRegionCount)
        {
            Debug.Log($"等级{level}: 所有区域已解锁");
        }
    }
    
    // 重置所有解锁
    public void ResetAllUnlocks()
    {
        if (MapDataManager.Instance == null) return;
        
        var allMaps = MapDataManager.Instance.GetAllMaps();
        
        // 锁定所有地图和区域
        foreach (var map in allMaps)
        {
            // 锁定地图
            map.isUnlocked = false;
            
            // 锁定所有区域
            foreach (var region in map.regions)
            {
                region.isUnlocked = false;
            }
        }
        
        if (debugLog) Debug.Log("已重置所有解锁状态");
    }
    
    [ContextMenu("测试解锁下一个区域")]
    public void TestUnlockNextRegion()
    {
        int currentLevel = PlayerLevelManager.Instance?.GetCurrentLevel() ?? 1;
        UnlockRegionForLevel(currentLevel + 1);
    }
    
    [ContextMenu("显示解锁状态")]
    public void LogUnlockStatus()
    {
        if (MapDataManager.Instance == null) return;
        
        var statusManager = MapDataManager.Instance.GetUnlockStatusManager();
        if (statusManager != null)
        {
            Debug.Log(statusManager.GetBriefStatus());
        }
    }
}