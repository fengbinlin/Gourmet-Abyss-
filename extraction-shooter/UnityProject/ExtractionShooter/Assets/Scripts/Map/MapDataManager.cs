using UnityEngine;
using System.Collections.Generic;
using System;
using System.Text;

public class MapDataManager : MonoBehaviour
{
    public static MapDataManager Instance { get; private set; }
    
    [Header("地图数据")]
    [SerializeField] private List<MapData> allMaps = new List<MapData>();
    
    [Header("测试数据")]
    [SerializeField] private Sprite[] testCreatureSprites;
    [SerializeField] private Sprite[] testProductSprites;
    
    [Header("解锁管理")]
    [SerializeField] private UnlockStatusManager unlockStatusManager;
    
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
        
        InitializeTestData();
        
        // 初始化解锁状态管理器
        unlockStatusManager = new UnlockStatusManager(this);
    }
    
    // 初始化测试数据
    private void InitializeTestData()
    {
        if (allMaps.Count > 0) return; // 如果已有数据，不再初始化
        
        // 创建3个地图
        for (int i = 0; i < 3; i++)
        {
            MapData map = new MapData($"Map_{i}", $"地图{i+1}", i == 0); // 只有第一个地图默认解锁
            allMaps.Add(map);
            
            // 每个地图有3个区域
            for (int j = 0; j < 3; j++)
            {
                RegionData region = new RegionData(
                    $"Region_{i}_{j}",
                    $"区域{i+1}-{j+1}",
                    j == 0 || (i == 0 && j < 2), // 第一个区域默认解锁
                    $"Scene_{i}_{j}"
                );
                
                // 添加测试生物数据
                for (int k = 0; k < 3; k++)
                {
                    CreatureData creature = new CreatureData
                    {
                        creatureID = $"Creature_{i}_{j}_{k}",
                        creatureName = $"生物{k+1}",
                        avatar = testCreatureSprites != null && testCreatureSprites.Length > k ? 
                                 testCreatureSprites[k] : null,
                        description = $"这是地图{i+1}区域{j+1}的生物{k+1}"
                    };
                    region.creatures.Add(creature);
                }
                
                // 添加测试物产数据
                for (int k = 0; k < 2; k++)
                {
                    ProductData product = new ProductData
                    {
                        productID = $"Product_{i}_{j}_{k}",
                        productName = $"物产{k+1}",
                        avatar = testProductSprites != null && testProductSprites.Length > k ? 
                                testProductSprites[k] : null,
                        description = $"这是地图{i+1}区域{j+1}的物产{k+1}"
                    };
                    region.products.Add(product);
                }
                
                map.regions.Add(region);
            }
        }
    }
    
    // 获取所有地图
    public List<MapData> GetAllMaps()
    {
        return allMaps;
    }
    
    // 根据ID获取地图
    public MapData GetMapByID(string mapID)
    {
        return allMaps.Find(map => map.mapID == mapID);
    }
    
    // 获取区域
    public RegionData GetRegionByID(string mapID, string regionID)
    {
        MapData map = GetMapByID(mapID);
        if (map != null)
        {
            return map.regions.Find(r => r.regionID == regionID);
        }
        return null;
    }
    
    // 解锁地图
    public void UnlockMap(string mapID)
    {
        MapData map = GetMapByID(mapID);
        if (map != null)
        {
            map.isUnlocked = true;
            unlockStatusManager.OnMapUnlocked(mapID);
        }
    }
    
    // 解锁区域
    public void UnlockRegion(string mapID, string regionID)
    {
        MapData map = GetMapByID(mapID);
        if (map != null)
        {
            RegionData region = map.regions.Find(r => r.regionID == regionID);
            if (region != null)
            {
                region.isUnlocked = true;
                unlockStatusManager.OnRegionUnlocked(mapID, regionID);
            }
        }
    }
    
    // 锁定地图
    public void LockMap(string mapID)
    {
        MapData map = GetMapByID(mapID);
        if (map != null)
        {
            map.isUnlocked = false;
            unlockStatusManager.OnMapLocked(mapID);
        }
    }
    
    // 锁定区域
    public void LockRegion(string mapID, string regionID)
    {
        MapData map = GetMapByID(mapID);
        if (map != null)
        {
            RegionData region = map.regions.Find(r => r.regionID == regionID);
            if (region != null)
            {
                region.isUnlocked = false;
                unlockStatusManager.OnRegionLocked(mapID, regionID);
            }
        }
    }
    
    // 获取解锁管理器
    public UnlockStatusManager GetUnlockStatusManager()
    {
        return unlockStatusManager;
    }
    
    // 保存数据（可扩展为保存到PlayerPrefs或文件）
    public void SaveData()
    {
        // 这里可以添加保存逻辑
    }
    
    // 加载数据
    public void LoadData()
    {
        // 这里可以添加加载逻辑
    }
}

// 解锁状态管理器
[System.Serializable]
public class UnlockStatusManager
{
    private MapDataManager mapDataManager;
    private UnlockStatus currentStatus;
    
    public UnlockStatusManager(MapDataManager manager)
    {
        mapDataManager = manager;
        UpdateStatus();
    }
    
    // 更新状态
    public void UpdateStatus()
    {
        List<MapData> allMaps = mapDataManager.GetAllMaps();
        currentStatus = new UnlockStatus(allMaps);
    }
    
    // 获取当前解锁状态
    public UnlockStatus GetCurrentStatus()
    {
        return currentStatus;
    }
    
    // 获取格式化状态字符串
    public string GetFormattedStatus()
    {
        UpdateStatus();
        return currentStatus.ToString();
    }
    
    // 获取简洁状态字符串
    public string GetBriefStatus()
    {
        UpdateStatus();
        
        int unlockedMaps = 0;
        int unlockedRegions = 0;
        int totalMaps = 0;
        int totalRegions = 0;
        
        foreach (var mapEntry in currentStatus.mapUnlockStatus)
        {
            totalMaps++;
            if (mapEntry.Value) unlockedMaps++;
            
            if (currentStatus.regionUnlockStatus.ContainsKey(mapEntry.Key))
            {
                foreach (var regionEntry in currentStatus.regionUnlockStatus[mapEntry.Key])
                {
                    totalRegions++;
                    if (regionEntry.Value) unlockedRegions++;
                }
            }
        }
        
        return $"解锁进度: 地图 {unlockedMaps}/{totalMaps} | 区域 {unlockedRegions}/{totalRegions}";
    }
    
    // 地图解锁事件
    public void OnMapUnlocked(string mapID)
    {
        Debug.Log($"地图解锁: {mapID}");
        UpdateStatus();
        
        // 可以在这里添加解锁奖励、成就等逻辑
    }
    
    // 区域解锁事件
    public void OnRegionUnlocked(string mapID, string regionID)
    {
        Debug.Log($"区域解锁: {mapID} -> {regionID}");
        UpdateStatus();
        
        // 可以在这里添加解锁奖励、成就等逻辑
    }
    
    // 地图锁定事件
    public void OnMapLocked(string mapID)
    {
        Debug.Log($"地图锁定: {mapID}");
        UpdateStatus();
    }
    
    // 区域锁定事件
    public void OnRegionLocked(string mapID, string regionID)
    {
        Debug.Log($"区域锁定: {mapID} -> {regionID}");
        UpdateStatus();
    }
    
    // 检查是否所有内容都已解锁
    public bool IsAllUnlocked()
    {
        UpdateStatus();
        
        foreach (var mapEntry in currentStatus.mapUnlockStatus)
        {
            if (!mapEntry.Value) return false;
            
            if (currentStatus.regionUnlockStatus.ContainsKey(mapEntry.Key))
            {
                foreach (var regionEntry in currentStatus.regionUnlockStatus[mapEntry.Key])
                {
                    if (!regionEntry.Value) return false;
                }
            }
        }
        
        return true;
    }
    
    // 解锁所有内容
    public void UnlockAll()
    {
        List<MapData> allMaps = mapDataManager.GetAllMaps();
        
        foreach (var map in allMaps)
        {
            mapDataManager.UnlockMap(map.mapID);
            
            foreach (var region in map.regions)
            {
                mapDataManager.UnlockRegion(map.mapID, region.regionID);
            }
        }
        
        UpdateStatus();
        Debug.Log("已解锁所有内容");
    }
    
    // 锁定所有内容
    public void LockAll()
    {
        List<MapData> allMaps = mapDataManager.GetAllMaps();
        
        foreach (var map in allMaps)
        {
            mapDataManager.LockMap(map.mapID);
            
            foreach (var region in map.regions)
            {
                mapDataManager.LockRegion(map.mapID, region.regionID);
            }
        }
        
        UpdateStatus();
        Debug.Log("已锁定所有内容");
    }
    
    // 重置为默认解锁状态
    public void ResetToDefault()
    {
        List<MapData> allMaps = mapDataManager.GetAllMaps();
        
        for (int i = 0; i < allMaps.Count; i++)
        {
            var map = allMaps[i];
            mapDataManager.LockMap(map.mapID);
            
            for (int j = 0; j < map.regions.Count; j++)
            {
                var region = map.regions[j];
                
                // 默认解锁规则：第一个地图解锁，每个地图的第一个区域解锁，第一个地图的第二个区域也解锁
                bool shouldUnlock = (i == 0 && (j == 0 || j == 1));
                
                if (shouldUnlock)
                {
                    mapDataManager.UnlockRegion(map.mapID, region.regionID);
                }
                else
                {
                    mapDataManager.LockRegion(map.mapID, region.regionID);
                }
            }
        }
        
        // 解锁第一个地图
        if (allMaps.Count > 0)
        {
            mapDataManager.UnlockMap(allMaps[0].mapID);
        }
        
        UpdateStatus();
        Debug.Log("已重置为默认解锁状态");
    }
}

// 示例：在游戏中使用的脚本
public class GameUnlockTester : MonoBehaviour
{
    [Header("测试控制")]
    [SerializeField] private bool testOnStart = false;
    [SerializeField] private bool unlockAll = false;
    [SerializeField] private bool lockAll = false;
    [SerializeField] private bool resetToDefault = false;
    [SerializeField] private bool printStatus = false;
    
    [Header("特定解锁")]
    [SerializeField] private string mapToUnlock = "Map_1";
    [SerializeField] private string regionToUnlockMap = "Map_0";
    [SerializeField] private string regionToUnlock = "Region_0_2";
    
    private UnlockStatusManager unlockManager;
    
    private void Start()
    {
        if (MapDataManager.Instance != null)
        {
            unlockManager = MapDataManager.Instance.GetUnlockStatusManager();
            
            if (testOnStart)
            {
                TestUnlockSystem();
            }
        }
    }
    
    private void Update()
    {
        if (unlockAll)
        {
            unlockAll = false;
            unlockManager.UnlockAll();
        }
        
        if (lockAll)
        {
            lockAll = false;
            unlockManager.LockAll();
        }
        
        if (resetToDefault)
        {
            resetToDefault = false;
            unlockManager.ResetToDefault();
        }
        
        if (printStatus)
        {
            printStatus = false;
            Debug.Log(unlockManager.GetFormattedStatus());
            Debug.Log(unlockManager.GetBriefStatus());
        }
    }
    
    private void TestUnlockSystem()
    {
        Debug.Log("=== 解锁系统测试 ===");
        
        // 打印初始状态
        Debug.Log("初始状态:");
        Debug.Log(unlockManager.GetFormattedStatus());
        
        // 解锁特定地图
        MapDataManager.Instance.UnlockMap(mapToUnlock);
        Debug.Log($"\n解锁地图 {mapToUnlock} 后:");
        Debug.Log(unlockManager.GetBriefStatus());
        
        // 解锁特定区域
        MapDataManager.Instance.UnlockRegion(regionToUnlockMap, regionToUnlock);
        Debug.Log($"\n解锁区域 {regionToUnlock} 后:");
        Debug.Log(unlockManager.GetBriefStatus());
        
        // 检查是否全部解锁
        Debug.Log($"\n是否全部解锁: {unlockManager.IsAllUnlocked()}");
        
        // 重置
        unlockManager.ResetToDefault();
        Debug.Log($"\n重置后状态:");
        Debug.Log(unlockManager.GetBriefStatus());
    }
    
    // 代码调用接口示例
    public void ExampleCodeUsage()
    {
        // 1. 获取解锁状态管理器
        UnlockStatusManager manager = MapDataManager.Instance.GetUnlockStatusManager();
        
        // 2. 获取当前解锁状态
        UnlockStatus status = manager.GetCurrentStatus();
        
        // 3. 获取格式化状态字符串
        string statusText = manager.GetFormattedStatus();
        
        // 4. 获取简洁状态
        string briefStatus = manager.GetBriefStatus();
        
        // 5. 检查是否全部解锁
        bool allUnlocked = manager.IsAllUnlocked();
        
        // 6. 解锁所有
        manager.UnlockAll();
        
        // 7. 重置为默认
        manager.ResetToDefault();
        
        // 8. 通过MapDataManager直接解锁
        MapDataManager.Instance.UnlockMap("Map_1");
        MapDataManager.Instance.UnlockRegion("Map_1", "Region_1_1");
        
        // 9. 通过MapDataManager锁定
        MapDataManager.Instance.LockMap("Map_1");
        MapDataManager.Instance.LockRegion("Map_1", "Region_1_1");
    }
}