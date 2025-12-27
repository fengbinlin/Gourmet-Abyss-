using System;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class UnlockRule
{
    [Tooltip("解锁的等级")]
    public int unlockLevel = 1;
    
    [Tooltip("地图ID")]
    public string mapID = "";
    
    [Tooltip("区域ID列表（为空时解锁整个地图）")]
    public string[] regionIDs = new string[0];
    
    [Tooltip("解锁描述")]
    public string description = "";
}

[CreateAssetMenu(fileName = "UnlockConfig", menuName = "Map/Unlock Configuration")]
public class UnlockConfig : ScriptableObject
{
    [Tooltip("解锁规则列表，按等级顺序排列")]
    public List<UnlockRule> unlockRules = new List<UnlockRule>();
    
    [Tooltip("当没有找到具体规则时的默认解锁策略")]
    public bool autoUnlockByLevelOrder = true;
}

public class LevelUnlockSystem : MonoBehaviour
{
    [Header("配置")]
    [SerializeField] private UnlockConfig unlockConfig;
    
    [Header("调试")]
    [SerializeField] private bool showDebugLog = true;
    
    // 已应用的解锁记录
    private Dictionary<int, bool> appliedUnlocks = new Dictionary<int, bool>();
    
    // 地图-区域解锁状态跟踪
    private Dictionary<string, bool> mapUnlockedCache = new Dictionary<string, bool>();
    private Dictionary<string, Dictionary<string, bool>> regionUnlockedCache = new Dictionary<string, Dictionary<string, bool>>();
    
    public static LevelUnlockSystem Instance { get; private set; }
    
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }
    
    private void Start()
    {
        // 初始化时确保第一个地图的第一个区域已解锁
        InitializeFirstUnlock();
        
        // 监听等级升级事件
        if (PlayerLevelManager.Instance != null)
        {
            PlayerLevelManager.Instance.OnLevelUp += OnPlayerLevelUp;
        }
    }
    
    private void OnDestroy()
    {
        if (PlayerLevelManager.Instance != null)
        {
            PlayerLevelManager.Instance.OnLevelUp -= OnPlayerLevelUp;
        }
    }
    
    private void InitializeFirstUnlock()
    {
        // 确保初始状态：解锁第一个地图的第一个区域
        List<MapData> allMaps = GetMapData();
        
        if (allMaps != null && allMaps.Count > 0)
        {
            MapData firstMap = allMaps[0];
            
            // 解锁第一个地图
            UnlockMap(firstMap.mapID);
            
            // 解锁第一个地图的第一个区域
            if (firstMap.regions.Count > 0)
            {
                UnlockRegion(firstMap.mapID, firstMap.regions[0].regionID);
            }
        }
        
        // 记录已应用初始解锁
        appliedUnlocks[1] = true;
    }
    
    private void OnPlayerLevelUp(int newLevel)
    {
        if (showDebugLog)
        {
            Debug.Log($"等级提升到 {newLevel}，检查解锁规则...");
        }
        
        // 检查并应用当前等级的解锁规则
        ApplyUnlockRulesForLevel(newLevel);
    }
    
    public void ApplyUnlockRulesForLevel(int level)
    {
        // 如果这个等级已经应用过解锁，跳过
        if (appliedUnlocks.ContainsKey(level) && appliedUnlocks[level])
        {
            if (showDebugLog) Debug.Log($"等级 {level} 的解锁规则已应用过，跳过");
            return;
        }
        
        bool unlockedSomething = false;
        
        if (unlockConfig != null && unlockConfig.unlockRules.Count > 0)
        {
            // 查找当前等级对应的解锁规则
            foreach (var rule in unlockConfig.unlockRules)
            {
                if (rule.unlockLevel == level)
                {
                    unlockedSomething = ApplyUnlockRule(rule) || unlockedSomething;
                }
            }
        }
        
        // 如果没有找到具体规则，使用默认的自动解锁策略
        if (!unlockedSomething && unlockConfig.autoUnlockByLevelOrder)
        {
            unlockedSomething = AutoUnlockByLevelOrder(level);
        }
        
        if (unlockedSomething)
        {
            appliedUnlocks[level] = true;
            
            // 刷新UI
            if (MapUIManager.Instance != null)
            {
                MapUIManager.Instance.RefreshUI();
            }
        }
    }
    
    private bool ApplyUnlockRule(UnlockRule rule)
    {
        bool unlockedSomething = false;
        
        if (string.IsNullOrEmpty(rule.mapID))
        {
            Debug.LogWarning($"等级 {rule.unlockLevel} 的解锁规则中未指定地图ID");
            return false;
        }
        
        // 获取地图数据
        MapData mapData = GetMapData(rule.mapID);
        if (mapData == null)
        {
            Debug.LogError($"找不到地图ID: {rule.mapID}");
            return false;
        }
        
        // 解锁指定区域
        if (rule.regionIDs != null && rule.regionIDs.Length > 0)
        {
            foreach (string regionID in rule.regionIDs)
            {
                if (UnlockRegion(rule.mapID, regionID))
                {
                    unlockedSomething = true;
                    
                    if (showDebugLog)
                    {
                        Debug.Log($"等级 {rule.unlockLevel} 解锁: 地图[{mapData.mapName}] -> 区域[{regionID}]");
                    }
                }
            }
        }
        else
        {
            // 解锁整个地图
            if (UnlockMap(rule.mapID))
            {
                unlockedSomething = true;
                
                if (showDebugLog)
                {
                    Debug.Log($"等级 {rule.unlockLevel} 解锁: 整个地图[{mapData.mapName}]");
                }
            }
        }
        
        return unlockedSomething;
    }
    
    private bool AutoUnlockByLevelOrder(int level)
    {
        if (level <= 1) return false; // 等级1已在初始化时处理
        
        List<MapData> allMaps = GetMapData();
        if (allMaps == null || allMaps.Count == 0) return false;
        
        // 计算总共需要解锁的区域数量
        int totalRegionsToUnlock = level - 1; // 等级1已解锁1个区域
        
        int currentUnlockCount = 1; // 初始已解锁1个区域
        int targetUnlockCount = Mathf.Min(totalRegionsToUnlock, GetTotalRegionCount());
        
        // 遍历所有地图和区域，按顺序解锁
        foreach (var map in allMaps)
        {
            if (!IsMapUnlocked(map.mapID))
            {
                // 先解锁地图
                if (UnlockMap(map.mapID))
                {
                    if (showDebugLog) Debug.Log($"自动解锁: 地图[{map.mapName}]");
                }
            }
            
            foreach (var region in map.regions)
            {
                if (currentUnlockCount >= targetUnlockCount)
                {
                    break; // 已达到当前等级应解锁的数量
                }
                
                if (!IsRegionUnlocked(map.mapID, region.regionID))
                {
                    if (UnlockRegion(map.mapID, region.regionID))
                    {
                        currentUnlockCount++;
                        
                        if (showDebugLog)
                        {
                            Debug.Log($"等级 {level} 自动解锁: 地图[{map.mapName}] -> 区域[{region.regionName}] ({currentUnlockCount}/{targetUnlockCount})");
                        }
                        
                        if (currentUnlockCount >= targetUnlockCount)
                        {
                            return true; // 完成解锁
                        }
                    }
                }
            }
            
            if (currentUnlockCount >= targetUnlockCount)
            {
                break;
            }
        }
        
        return currentUnlockCount > 1; // 是否解锁了至少一个新区域
    }
    
    public bool UnlockMap(string mapID)
    {
        List<MapData> allMaps = GetMapData();
        MapData mapData = allMaps?.Find(m => m.mapID == mapID);
        
        if (mapData != null && !mapData.isUnlocked)
        {
            mapData.isUnlocked = true;
            mapUnlockedCache[mapID] = true;
            return true;
        }
        
        return false;
    }
    
    public bool UnlockRegion(string mapID, string regionID)
    {
        MapData mapData = GetMapData(mapID);
        
        if (mapData != null && mapData.isUnlocked)
        {
            RegionData regionData = mapData.regions.Find(r => r.regionID == regionID);
            
            if (regionData != null && !regionData.isUnlocked)
            {
                regionData.isUnlocked = true;
                
                // 更新缓存
                if (!regionUnlockedCache.ContainsKey(mapID))
                {
                    regionUnlockedCache[mapID] = new Dictionary<string, bool>();
                }
                regionUnlockedCache[mapID][regionID] = true;
                
                return true;
            }
        }
        
        return false;
    }
    
    public bool IsMapUnlocked(string mapID)
    {
        if (mapUnlockedCache.TryGetValue(mapID, out bool cached))
        {
            return cached;
        }
        
        MapData mapData = GetMapData(mapID);
        bool isUnlocked = mapData?.isUnlocked ?? false;
        mapUnlockedCache[mapID] = isUnlocked;
        
        return isUnlocked;
    }
    
    public bool IsRegionUnlocked(string mapID, string regionID)
    {
        if (regionUnlockedCache.TryGetValue(mapID, out var regionCache) && 
            regionCache.TryGetValue(regionID, out bool cached))
        {
            return cached;
        }
        
        MapData mapData = GetMapData(mapID);
        RegionData regionData = mapData?.regions.Find(r => r.regionID == regionID);
        bool isUnlocked = regionData?.isUnlocked ?? false;
        
        if (!regionUnlockedCache.ContainsKey(mapID))
        {
            regionUnlockedCache[mapID] = new Dictionary<string, bool>();
        }
        regionUnlockedCache[mapID][regionID] = isUnlocked;
        
        return isUnlocked;
    }
    
    public int GetTotalRegionCount()
    {
        List<MapData> allMaps = GetMapData();
        int count = 0;
        
        if (allMaps != null)
        {
            foreach (var map in allMaps)
            {
                count += map.regions?.Count ?? 0;
            }
        }
        
        return count;
    }
    
    public int GetUnlockedRegionCount()
    {
        int count = 0;
        List<MapData> allMaps = GetMapData();
        
        if (allMaps != null)
        {
            foreach (var map in allMaps)
            {
                if (map.regions != null)
                {
                    foreach (var region in map.regions)
                    {
                        if (region.isUnlocked)
                        {
                            count++;
                        }
                    }
                }
            }
        }
        
        return count;
    }
    
    public List<MapData> GetMapData()
    {
        if (MapDataManager.Instance != null)
        {
            return MapDataManager.Instance.GetAllMaps();
        }
        return new List<MapData>();
    }
    
    public MapData GetMapData(string mapID)
    {
        List<MapData> allMaps = GetMapData();
        return allMaps?.Find(m => m.mapID == mapID);
    }
    
    public void ResetAllUnlocks()
    {
        appliedUnlocks.Clear();
        mapUnlockedCache.Clear();
        regionUnlockedCache.Clear();
        
        // 重置所有地图和区域的解锁状态
        List<MapData> allMaps = GetMapData();
        if (allMaps != null)
        {
            foreach (var map in allMaps)
            {
                map.isUnlocked = false;
                if (map.regions != null)
                {
                    foreach (var region in map.regions)
                    {
                        region.isUnlocked = false;
                    }
                }
            }
        }
        
        // 重新初始化第一个解锁
        InitializeFirstUnlock();
        
        if (MapUIManager.Instance != null)
        {
            MapUIManager.Instance.RefreshUI();
        }
    }
    
    [ContextMenu("强制解锁下一个区域")]
    public void ForceUnlockNextRegion()
    {
        int currentLevel = PlayerLevelManager.Instance?.GetCurrentLevel() ?? 1;
        ApplyUnlockRulesForLevel(currentLevel + 1);
    }
    
    [ContextMenu("显示当前解锁状态")]
    public void LogUnlockStatus()
    {
        Debug.Log("=== 当前解锁状态 ===");
        Debug.Log($"已解锁区域: {GetUnlockedRegionCount()}/{GetTotalRegionCount()}");
        
        List<MapData> allMaps = GetMapData();
        foreach (var map in allMaps)
        {
            Debug.Log($"地图 [{map.mapName}]: {(map.isUnlocked ? "已解锁" : "未解锁")}");
            foreach (var region in map.regions)
            {
                Debug.Log($"  - 区域 [{region.regionName}]: {(region.isUnlocked ? "已解锁" : "未解锁")}");
            }
        }
    }
}