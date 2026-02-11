using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.Tilemaps;
using System.Collections; // 协程需要
[System.Serializable]
public class PrefabProbability
{
    public GameObject prefab;
    [Range(0f, 1f)] public float probability = 1f;
}

[System.Serializable]
public class Dungeon3DData
{
    public List<Vector2Int> roomPositions = new List<Vector2Int>();
    public List<Vector2Int> corridorPositions = new List<Vector2Int>();
    public List<Vector2Int> wallPositions = new List<Vector2Int>();
    public List<Vector2Int> mountainPositions = new List<Vector2Int>();
    public List<List<Vector2Int>> mountainLayers = new List<List<Vector2Int>>();
    public List<Vector2Int> mountainEdgePositions = new List<Vector2Int>();
}
public class ChunkData
{
    public GameObject chunkRoot; // 父节点
    public List<ObjectRecord> objects = new List<ObjectRecord>();
    public Vector3 worldOffset; // Chunk的世界偏移量
    public HashSet<Vector2Int> generatedFloorPositions = new HashSet<Vector2Int>();
    public Dungeon3DData dungeon3DData = new Dungeon3DData();  // 把数据放这里
    public bool isMountainOver=false;
}

public class ObjectRecord
{
    public GameObject prefab;
    public Vector3 position;
    public Quaternion rotation;
    public Transform parent;
}
public class Dungeon3DGenerator : MonoBehaviour
{
    // ================================
    [Header("无限生成设置")]
    public Transform cameraTransform;         // 主相机
    public float dungeonChunkLength = 50f;    // 单个3D地牢沿Z轴的长度
    private float nextChunkTriggerZ = 0f;     // 下一个生成点的触发位置
    private int chunkIndex = 0;               // 已生成的块索引
    public int localMinX = -50;
    public int localMinY = -22;
    public int localMaxX = 50;
    public int localMaxY = 22;
    public int dungeoHeight = 30;
    public static Dungeon3DGenerator instance;
    public GameObject chunkConnector;
    public GameObject ChunkBaseObject;

    // ================================
    [Header("Dungeon 2D 数据源")]
    public DungeonGenerator dungeonGenerator2D;

    // ================================
    [Header("藤蔓生成设置")]
    public Artngame.TreeGEN.ProceduralIvy.IvyGeneratorTREANT ivyGenerator;  // 引用已有的藤蔓生成器脚本
    public int vineRayCount = 30;            // 要生成多少条藤蔓
    public float vineRayHeight = 30f;        // 射线起点的高度（从底部往上）
    public float vineSpawnProbability = 0.8f; // 每次射线命中是否生成藤蔓的概率

    // ================================
    [Header("3D 网格设置")]
    public float gridSize = 2.0f;
    public float floorHeight = 0f;
    public float wallHeight = 1.0f;
    public float baseMountainHeight = 0.5f;
    public float mountainLayerHeight = 1.0f;
    public int mountainThickness = 2;
    public int maxMountainLayers = 5;

    // ================================
    [Header("分层高度设置")]
    public bool useCustomLayerHeights = false;
    public List<float> layerHeights = new List<float>();

    // ================================
    [Header("3D 模型预制体")]
    public GameObject roomWallPrefab;
    public GameObject corridorWallPrefab;
    public GameObject mountainPrefab;
    public GameObject mountainLayer2Prefab;
    public GameObject mountainLayer3PlusPrefab;

    // ================================
    [Header("地面 prefab 及概率设置（房间和走廊共用）")]
    public List<PrefabProbability> groundPrefabsWithProbabilities;
    public float groundNoiseScale = 0.05f;
    public float groundNoiseThreshold = 0.5f;

    // ================================
    [Header("第一层山体扩展噪声")]
    public int noiseSeed = 0;
    public float expansionNoiseScale = 0.05f;
    public float expansionNoiseThreshold = 0.6f;
    public float expansionProbability = 0.4f;
    public int expansionDistance = 2;

    // ================================
    [Header("高层山体噪声设置")]
    public float layerNoiseScale = 0.1f;
    public float layerNoiseThreshold = 0.5f;
    public float layerDensity = 0.7f;
    public float noiseDetailScale = 0.3f;

    // ================================
    [Header("山体层限制")]
    public bool excludeEdgesFromHigherLayers = true;
    public int edgeDistance = 1;

    // ================================
    [Header("山体块旋转变化")]
    public bool randomizeRotation = true;
    public float rotationNoiseScale = 0.2f;

    // ================================
    [Header("性能优化")]
    public bool setObjectsStatic = true;

    // ================================
    [Header("山体装饰设置")]
    public List<PrefabProbability> propsPrefabsWithProbabilities;
    public float propsNoiseScale = 0.1f;
    public bool randomizePropsRotation = true;
    public Vector2 propsScaleRange = new Vector2(0.8f, 1.2f);

    // ================================
    [Header("地面装饰设置（支持交界优先）")]
    public List<PrefabProbability> floorPropsPrefabsWithProbabilities;
    public float floorPropsNoiseScale = 0.08f;
    public float floorPropsClusterRadius = 3f;
    public int floorPropsClusterDensity = 3;
    public float floorPropsEdgeBonus = 0.3f; // 在山体交界处额外的概率加成
    public float floorPropsTH = 0.7f;        // 在山体交界处额外的概率加成

    // ================================
    [Header("地板边缘装饰设置（与山体交界）")]
    [Range(0f, 1f)] public float floorEdgeDecorationProbability = 0.5f;
    [Range(1, 5)] public int floorEdgeDetectionDistance = 1;
    public float floorEdgeNoiseScale = 0.1f;
    [Range(0f, 1f)] public float floorEdgeNoiseThreshold = 0.4f;
    [Range(0f, 1f)] public float floorEdgeNoiseInfluence = 0.7f; // 噪声对最终概率的影响程度
    public float floorEdgeDecorationHeightOffset = 0.05f;
    public float floorEdgeRotationVariation = 0f; // 旋转变化范围（角度）

    // ================================
    [Header("走廊屏蔽prefabs列表")]
    [Tooltip("在走廊地块上会屏蔽这些prefabs的生成")]
    public List<GameObject> corridorBlockedPrefabs = new List<GameObject>();

    // ================================
    [Header("内部数据缓存")]
    private Dungeon3DData dungeon3DData = new Dungeon3DData();
    private List<GameObject> spawnedModels = new List<GameObject>();
    private List<GameObject> spawnedMountains = new List<GameObject>();
    private List<GameObject> spawnedFloors = new List<GameObject>();
    private List<GameObject> spawnedWalls = new List<GameObject>();
    private List<GameObject> spawnedProps = new List<GameObject>();

    // 新增：缓存过滤后的prefab列表
    private List<PrefabProbability> filteredGroundPrefabsForCorridor = null;
    private List<PrefabProbability> filteredFloorPropsPrefabsForCorridor = null;
    private bool isFilteredPrefabsCached = false;

    private GameObject floorsParent;
    private GameObject wallsParent;
    private GameObject mountainsParent;
    private GameObject propsParent;

    private HashSet<Vector2Int> generatedFloorPositions = new HashSet<Vector2Int>();
    public List<Vector2Int> markerPositions = new List<Vector2Int>();
    private List<Vector2Int> previousExits = new List<Vector2Int>();
    List<Vector2Int> currentEntrances;
    List<Vector2Int> currentExits;

    // ================================
    [Header("可视化与调试")]
    [SerializeField] private int visibleChunkCount = 3;
    private Dictionary<int, ChunkData> chunkRegistry = new Dictionary<int, ChunkData>();
    [SerializeField] private bool debugVinePositions = true;  // 是否可视化调试
    void Awake()
    {
        instance = this;
    }

    void Start()
    {
        // 预填充常用 Prefab
        DungeonObjectPool.Instance.Prewarm(roomWallPrefab, 500, transform);
        DungeonObjectPool.Instance.Prewarm(corridorWallPrefab, 500, transform);
        DungeonObjectPool.Instance.Prewarm(mountainPrefab, 500, transform);
        if (mountainLayer2Prefab) DungeonObjectPool.Instance.Prewarm(mountainLayer2Prefab, 300, transform);
        if (mountainLayer3PlusPrefab) DungeonObjectPool.Instance.Prewarm(mountainLayer3PlusPrefab, 300, transform);

        foreach (var p in groundPrefabsWithProbabilities)
            if (p.prefab) DungeonObjectPool.Instance.Prewarm(p.prefab, 500, transform);

        foreach (var p in floorPropsPrefabsWithProbabilities)
            if (p.prefab) DungeonObjectPool.Instance.Prewarm(p.prefab, 300, transform);

        foreach (var p in propsPrefabsWithProbabilities)
            if (p.prefab) DungeonObjectPool.Instance.Prewarm(p.prefab, 300, transform);

        // 一开始生成多个 Chunk
        int prewarmChunkCount = 3; // 要提前生成的 Chunk 数
        for (int i = 0; i < prewarmChunkCount; i++)
        {
            
            nextChunkTriggerZ -= dungeonChunkLength;
            Vector3 chunkOffset = new Vector3(0, -dungeoHeight * chunkIndex, -dungeonChunkLength * chunkIndex);
            chunkIndex++;
            StartCoroutine(GenerateChunk(chunkIndex, chunkOffset, 50));
        }

    }

    private List<Vector2Int> GenerateMarkersForChunk(int chunkIndex)
    {
        List<Vector2Int> markers = new List<Vector2Int>();

        if (chunkIndex == 1)
        {
            // 第一个 Chunk，入口固定
            markers.Add(new Vector2Int(0, 15));
            // 出口随机 1~3 个
            int exitCount = Random.Range(1, 4);
            List<Vector2Int> exits = GenerateRandomMarkers(exitCount, -15);
            previousExits = exits;
            markers.AddRange(exits);
        }
        else
        {
            // 入口根据上一个 Chunk 的出口对应生成
            foreach (var exit in previousExits)
            {
                markers.Add(new Vector2Int(exit.x, 15));
            }
            // 出口随机 1~3 个
            int exitCount = Random.Range(1, 4);
            List<Vector2Int> exits = GenerateRandomMarkers(exitCount, -15);
            previousExits = exits;
            markers.AddRange(exits);
        }

        return markers;
    }

    /// 生成随机 marker（保证 X 距离 >= 8）
    private List<Vector2Int> GenerateRandomMarkers(int count, int fixedY)
    {
        List<Vector2Int> markers = new List<Vector2Int>();

        int attempts = 0;
        while (markers.Count < count && attempts < 100)
        {
            attempts++;
            int x = Random.Range(-20, 21); // 包含边界
            bool valid = true;

            // 检查 X 间距
            foreach (var m in markers)
            {
                if (Mathf.Abs(m.x - x) < 8)
                {
                    valid = false;
                    break;
                }
            }

            if (valid)
            {
                markers.Add(new Vector2Int(x, fixedY));
            }
        }

        return markers;
    }
    private IEnumerator GenerateChunk(int chunkIndex, Vector3 worldOffset, int BaseSpeed = 1)
    {
        //创建基石
        GameObject.Instantiate(ChunkBaseObject,worldOffset,Quaternion.identity);
        // 1. 创建 ChunkData
        ChunkData chunkData = new ChunkData();
        chunkData.worldOffset = worldOffset;
        chunkData.chunkRoot = new GameObject($"Chunk_{chunkIndex}");
        chunkData.chunkRoot.transform.SetParent(transform);

        // 2. 创建子父节点（floors/walls/mountains/props）
        floorsParent = new GameObject("Grounds");
        floorsParent.transform.SetParent(chunkData.chunkRoot.transform);

        wallsParent = new GameObject("Walls");
        wallsParent.transform.SetParent(chunkData.chunkRoot.transform);

        mountainsParent = new GameObject("Mountains");
        mountainsParent.transform.SetParent(chunkData.chunkRoot.transform);

        propsParent = new GameObject("Props");
        propsParent.transform.SetParent(chunkData.chunkRoot.transform);
        // 3. 生成地牢数据（2D -> 3D）
        markerPositions = GenerateMarkersForChunk(chunkIndex);
        dungeonGenerator2D.GenerateDungeon(markerPositions);
        ExtractDungeonDataFrom2D(chunkData.dungeon3DData);
        CalculateBaseMountainArea(chunkData.dungeon3DData);
        ExpandMountainAreaWithNoise(chunkData.dungeon3DData);
        ClampDungeonBounds(new Vector2Int(localMinX, localMinY), new Vector2Int(localMaxX, localMaxY), chunkData.dungeon3DData);
        LeaveEntranceExitGaps(chunkData.dungeon3DData, worldOffset);

        // 4. 生成物体（全部走 SpawnObject）
        yield return StartCoroutine(GenerateGroundModels_WithOffset(worldOffset, chunkData, 30 * BaseSpeed));
        yield return StartCoroutine(GenerateWallModels_WithOffset(worldOffset, chunkData, 30 * BaseSpeed));
        yield return StartCoroutine(GenerateAllMountainLayers_WithOffset(worldOffset, chunkData, 300 * BaseSpeed));
        yield return StartCoroutine(GenerateVinesOnCaveBottom(worldOffset, chunkData, 30 * BaseSpeed));
        yield return StartCoroutine(DecorateMountainsWithProps_WithOffset(worldOffset, chunkData, 10 * BaseSpeed));
        yield return StartCoroutine(DecorateFloorsWithProps_WithOffset(worldOffset, chunkData, 10 * BaseSpeed));
        yield return StartCoroutine(DecorateFloorMountainEdges_WithOffset(worldOffset, chunkData, 10 * BaseSpeed));
        // 5. 存入字典
        chunkRegistry[chunkIndex] = chunkData;
    }
    void Update()
    {
        if (cameraTransform == null) return;

        // 判断生成新chunk
        if (cameraTransform.position.z - 3 * dungeonChunkLength < nextChunkTriggerZ - dungeonChunkLength / 2f)
        {
            
            nextChunkTriggerZ -= dungeonChunkLength;
            Vector3 chunkOffset = new Vector3(0, -dungeoHeight * chunkIndex, -dungeonChunkLength * chunkIndex);
            chunkIndex++;
            StartCoroutine(GenerateChunk(chunkIndex, chunkOffset));
        }
    }
    void LeaveEntranceExitGaps(Dungeon3DData targetData, Vector3 offset)
    {
        int gapHalfWidth = 2; // 半宽=1，总宽=3
        HashSet<Vector2Int> mountainSet = new HashSet<Vector2Int>(targetData.mountainPositions);
        HashSet<Vector2Int> roomSet = new HashSet<Vector2Int>(targetData.roomPositions);
        // 找入口（标记点 y > 0）
        var entrances = markerPositions.Where(m => m.y > 0).ToList();
        foreach (var entrance in entrances)
        {
            for (int dy = entrance.y; dy <= localMaxY + 1; dy++)
            {
                for (int dx = -gapHalfWidth; dx <= gapHalfWidth; dx++)
                {
                    Vector2Int gapPos = new Vector2Int(entrance.x + dx, dy);
                    if (mountainSet.Contains(gapPos))
                    {
                        targetData.mountainPositions.Remove(gapPos);
                        targetData.roomPositions.Add(gapPos);
                    }
                    else
                    {
                        targetData.roomPositions.Add(gapPos);
                    }
                }
            }
        }

        // 找出口（标记点 y < 0）
        var exits = markerPositions.Where(m => m.y < 0).ToList();
        foreach (var exit in exits)
        {
            for (int dy = exit.y; dy >= localMinY - 1; dy--)
            {
                for (int dx = -gapHalfWidth; dx <= gapHalfWidth; dx++)
                {
                    Vector2Int gapPos = new Vector2Int(exit.x + dx, dy);
                    if (mountainSet.Contains(gapPos))
                    {
                        targetData.mountainPositions.Remove(gapPos);
                        targetData.roomPositions.Add(gapPos);
                    }
                    else
                    {
                        targetData.roomPositions.Add(gapPos);
                    }

                }
                if (dy == localMinY - 1)
                {
                    //生成连接点
                    Vector3 worldPos = GridToWorldOffset(exit.x, dy, floorHeight, offset);
                    worldPos.z-=1;
                    GameObject.Instantiate(chunkConnector, worldPos, Quaternion.identity);
                }
            }
        }
    }
    private GameObject SpawnObject(GameObject prefab, Vector3 position, Quaternion rotation, Transform parent, ChunkData chunkData)
    {
        GameObject obj = DungeonObjectPool.Instance.GetFromPool(prefab, position, rotation, parent);
        chunkData.objects.Add(new ObjectRecord { prefab = prefab, position = position, rotation = rotation, parent = parent });
        return obj;
    }

    Vector3 GridToWorldOffset(int gridX, int gridY, float height, Vector3 offset)
    {
        return new Vector3(gridX * gridSize, height, gridY * gridSize) + offset;
    }
    IEnumerator GenerateGroundModels_WithOffset(Vector3 offset, ChunkData chunkData, int batchSize = 50)
    {
        // 确保已计算过滤列表
        if (!isFilteredPrefabsCached)
        {
            CalculateFilteredPrefabLists();
        }

        HashSet<Vector2Int> allGroundPositions = new HashSet<Vector2Int>(chunkData.dungeon3DData.roomPositions);
        allGroundPositions.UnionWith(chunkData.dungeon3DData.corridorPositions);
        int counter = 0;
        foreach (var pos in allGroundPositions)
        {
            float noiseValue = Mathf.PerlinNoise(
                pos.x * groundNoiseScale + noiseSeed * 0.01f,
                pos.y * groundNoiseScale + noiseSeed * 0.01f + 200
            );

            // 获取过滤后的prefab列表（使用缓存的列表）
            List<PrefabProbability> availablePrefabs = GetFilteredGroundPrefabsForPosition(pos);
            if (availablePrefabs == null || availablePrefabs.Count == 0)
            {
                // 如果列表为空，使用原始列表
                availablePrefabs = groundPrefabsWithProbabilities;
            }

            GameObject prefab = ChoosePrefabByNoiseValue(availablePrefabs, noiseValue);
            if (prefab == null)
            {
                // 如果选择失败，使用原始列表的第一个prefab
                if (groundPrefabsWithProbabilities != null && groundPrefabsWithProbabilities.Count > 0)
                    prefab = groundPrefabsWithProbabilities[0].prefab;
                else
                    continue; // 没有可用的prefab，跳过
            }

            Vector3 worldPos = GridToWorldOffset(pos.x, pos.y, floorHeight, offset);
            GameObject groundTile = SpawnObject(prefab, worldPos, Quaternion.identity, floorsParent.transform, chunkData);
            if (setObjectsStatic) groundTile.isStatic = true;
            spawnedModels.Add(groundTile);
            spawnedFloors.Add(groundTile);
            chunkData.generatedFloorPositions.Add(pos);
            counter++;
            if (counter % batchSize == 0)
                yield return null; // 等待下一帧
        }
    }
    IEnumerator GenerateWallModels_WithOffset(Vector3 offset, ChunkData chunkData, int batchSize = 50)
    {
        int counter = 0;
        foreach (var pos in chunkData.dungeon3DData.wallPositions)
        {
            Vector3 worldPos = GridToWorldOffset(pos.x, pos.y, floorHeight, offset);
            GameObject wallPrefab = roomWallPrefab;
            foreach (var corridorPos in chunkData.dungeon3DData.corridorPositions)
            {
                if (Vector2Int.Distance(pos, corridorPos) <= 1)
                {
                    wallPrefab = corridorWallPrefab;
                    break;
                }
            }
            GameObject wall = SpawnObject(wallPrefab, worldPos, Quaternion.identity, wallsParent.transform, chunkData);
            if (setObjectsStatic) wall.isStatic = true;
            spawnedModels.Add(wall);
            spawnedWalls.Add(wall);
            counter++;
            if (counter % batchSize == 0)
                yield return null; // 等待下一帧
        }
    }
    IEnumerator GenerateAllMountainLayers_WithOffset(Vector3 offset, ChunkData chunkData, int batchSize = 50)
    {
        chunkData.dungeon3DData.mountainLayers.Clear();
        List<Vector2Int> baseLayerPositions = new List<Vector2Int>(chunkData.dungeon3DData.mountainPositions);
        chunkData.dungeon3DData.mountainLayers.Add(baseLayerPositions);
        yield return StartCoroutine(GenerateMountainLayer_WithOffset(0, baseLayerPositions, offset, chunkData));
        int counter = 0;
        for (int layer = 1; layer <= maxMountainLayers; layer++)
        {
            List<Vector2Int> previousLayer = chunkData.dungeon3DData.mountainLayers[layer - 1];
            if (previousLayer.Count == 0) break;
            List<Vector2Int> currentLayerPositions = CalculateHigherMountainLayer(layer, previousLayer);
            if (currentLayerPositions.Count == 0) break;
            chunkData.dungeon3DData.mountainLayers.Add(currentLayerPositions);
            
            yield return StartCoroutine(GenerateMountainLayer_WithOffset(layer, currentLayerPositions, offset, chunkData,batchSize));
            counter++;
            if (counter % batchSize == 0)
                yield return null; // 等待下一帧
        }
        Debug.Log("山体生成完毕");
        chunkData.isMountainOver=true;
    }
    IEnumerator GenerateMountainLayer_WithOffset(int layer, List<Vector2Int> positions, Vector3 offset, ChunkData chunkData, int batchSize = 50, bool isLast=false)
    {
        float currentHeight = GetLayerHeight(layer);
        GameObject prefabToUse = GetMountainPrefabForLayer(layer);
        int counter = 0;
        foreach (var pos in positions)
        {
            Vector3 worldPos = GridToWorldOffset(pos.x, pos.y, currentHeight, offset);
            GameObject mountain = SpawnObject(prefabToUse, worldPos, Quaternion.identity, mountainsParent.transform, chunkData);
            if (setObjectsStatic) mountain.isStatic = true;
            mountain.layer = 14;
            if (randomizeRotation)
            {
                float rotationNoise = CalculateNoiseForRotation(pos.x, pos.y, layer);
                int rotationIndex = Mathf.FloorToInt(rotationNoise * 4) % 4;
                mountain.transform.rotation = Quaternion.Euler(0, rotationIndex * 90, 0);
            }
            spawnedModels.Add(mountain);
            spawnedMountains.Add(mountain);
            counter++;
            if (counter % batchSize == 0)
                yield return null; // 等待下一帧
        }
    }
    IEnumerator DecorateMountainsWithProps_WithOffset(Vector3 offset, ChunkData chunkData, int batchSize = 50)
    {
        if (propsPrefabsWithProbabilities == null || propsPrefabsWithProbabilities.Count == 0) yield break; ;
        float rayHeight = 50f;
        HashSet<Vector2Int> usedPositions = new HashSet<Vector2Int>();
        int counter = 0;
        foreach (var layerPositions in chunkData.dungeon3DData.mountainLayers.ToList())
        {
            foreach (var pos in layerPositions)
            {
                float noiseValue = Mathf.PerlinNoise(
                    pos.x * propsNoiseScale + noiseSeed * 0.01f,
                    pos.y * propsNoiseScale + noiseSeed * 0.01f + 500
                );
                if (noiseValue > 0.5f)
                {
                    if (usedPositions.Contains(pos)) continue;

                    // ✅ 发射射线的起点加上 Chunk 偏移
                    Vector3 rayStart = new Vector3(pos.x * gridSize, rayHeight, pos.y * gridSize) + offset;

                    Ray ray = new Ray(rayStart, Vector3.down);
                    RaycastHit hit;
                    int layerMask = 1 << 14; // 山体的 Layer
                    if (Physics.Raycast(ray, out hit, rayHeight * 2f, layerMask))
                    {
                        GameObject prefab = ChoosePrefabByProbability(propsPrefabsWithProbabilities);
                        if (prefab == null) continue;

                        // ✅ 生成位置也加偏移（hit.point 是世界坐标，不需要再加 offset）
                        GameObject propInstance = SpawnObject(prefab, hit.point, Quaternion.identity, propsParent.transform, chunkData);

                        if (randomizePropsRotation)
                            propInstance.transform.rotation = Quaternion.Euler(0, Random.Range(0f, 360f), 0);

                        float scaleFactor = Random.Range(propsScaleRange.x, propsScaleRange.y);
                        propInstance.transform.localScale *= scaleFactor;

                        if (setObjectsStatic) propInstance.isStatic = true;

                        spawnedModels.Add(propInstance);
                        spawnedProps.Add(propInstance);
                        usedPositions.Add(pos);
                    }
                }
                counter++;
                if (counter % batchSize == 0)
                    yield return null; // 等待下一帧
            }
        }
    }
    IEnumerator DecorateFloorsWithProps_WithOffset(Vector3 offset, ChunkData chunkData, int batchSize = 50)
    {
        if (floorPropsPrefabsWithProbabilities == null || floorPropsPrefabsWithProbabilities.Count == 0) yield break; ;
        HashSet<Vector2Int> usedPositions = new HashSet<Vector2Int>();

        // 缓存走廊位置以提高性能
        HashSet<Vector2Int> corridorPositionsSet = new HashSet<Vector2Int>(chunkData.dungeon3DData.corridorPositions);
        int counter = 0;
        foreach (var floorPos in chunkData.generatedFloorPositions.ToList())
        {
            bool isEdge = chunkData.dungeon3DData.mountainPositions.Any(m => Vector2Int.Distance(m, floorPos) <= 1);
            float noiseValue = Mathf.PerlinNoise(
                floorPos.x * floorPropsNoiseScale + noiseSeed * 0.01f,
                floorPos.y * floorPropsNoiseScale + noiseSeed * 0.01f + 300
            );
            float finalProbabilityBonus = isEdge ? floorPropsEdgeBonus : 0f;

            if (noiseValue + finalProbabilityBonus > floorPropsTH)
            {
                for (int i = 0; i < floorPropsClusterDensity; i++)
                {
                    Vector2 randomOffset = Random.insideUnitCircle * floorPropsClusterRadius;
                    Vector2Int clusterPos = new Vector2Int(
                        Mathf.RoundToInt(floorPos.x + randomOffset.x),
                        Mathf.RoundToInt(floorPos.y + randomOffset.y)
                    );

                    if (usedPositions.Contains(clusterPos)) continue;
                    usedPositions.Add(clusterPos);

                    // 检查当前集群位置是否是走廊
                    bool isClusterInCorridor = corridorPositionsSet.Contains(clusterPos);

                    // 获取可用的 prefab 列表
                    List<PrefabProbability> availablePrefabs = null;
                    if (isClusterInCorridor)
                    {
                        if (isFilteredPrefabsCached && filteredFloorPropsPrefabsForCorridor != null)
                        {
                            availablePrefabs = filteredFloorPropsPrefabsForCorridor;
                        }
                        else
                        {
                            availablePrefabs = floorPropsPrefabsWithProbabilities;
                        }
                    }
                    else
                    {
                        availablePrefabs = floorPropsPrefabsWithProbabilities;
                    }

                    if (availablePrefabs == null || availablePrefabs.Count == 0) continue;

                    GameObject prefab = ChoosePrefabByProbability(availablePrefabs);
                    if (prefab == null) continue;

                    // ✅ 加 offset
                    Vector3 worldPos = GridToWorld(clusterPos.x, clusterPos.y, floorHeight + 0.05f) + offset;
                    GameObject propInstance = SpawnObject(
                        prefab,
                        worldPos,
                        Quaternion.Euler(0f, Random.Range(0f, 360f), 0f),
                        propsParent.transform,
                        chunkData
                    );

                    if (randomizePropsRotation)
                        propInstance.transform.rotation = Quaternion.Euler(0, Random.Range(0f, 360f), 0);

                    float scaleFactor = Random.Range(propsScaleRange.x, propsScaleRange.y);
                    propInstance.transform.localScale *= scaleFactor;
                    if (setObjectsStatic) propInstance.isStatic = true;

                    spawnedModels.Add(propInstance);
                    spawnedProps.Add(propInstance);
                }
            }
            counter++;
            if (counter % batchSize == 0)
                yield return null; // 等待下一帧
        }
    }
    IEnumerator DecorateFloorMountainEdges_WithOffset(Vector3 offset, ChunkData chunkData, int batchSize = 50)
    {
        if (floorPropsPrefabsWithProbabilities == null || floorPropsPrefabsWithProbabilities.Count == 0) yield break; ;

        HashSet<Vector2Int> mountainPosSet = new HashSet<Vector2Int>(chunkData.dungeon3DData.mountainPositions);
        HashSet<Vector2Int> usedPositions = new HashSet<Vector2Int>();
        int counter = 0;
        foreach (var floorPos in chunkData.generatedFloorPositions)
        {
            if (usedPositions.Contains(floorPos)) continue;

            bool isCorridor = chunkData.dungeon3DData.corridorPositions.Contains(floorPos);
            List<PrefabProbability> availablePrefabs = GetFilteredFloorPropsPrefabsForPosition(floorPos);
            if (availablePrefabs == null || availablePrefabs.Count == 0) continue;

            bool touchesMountain = false;
            foreach (var mountainPos in mountainPosSet)
            {
                if (Vector2Int.Distance(mountainPos, floorPos) <= floorEdgeDetectionDistance)
                {
                    touchesMountain = true;
                    break;
                }
            }
            if (!touchesMountain) continue;

            float noiseValue = CalculateFloorEdgeNoise(floorPos.x, floorPos.y);
            if (noiseValue < floorEdgeNoiseThreshold) continue;

            float noiseInfluence = Mathf.Lerp(1f, noiseValue, floorEdgeNoiseInfluence);
            float finalProbability = floorEdgeDecorationProbability * noiseInfluence;

            if (Random.value < finalProbability)
            {
                GameObject prefab = ChoosePrefabByProbability(availablePrefabs);
                if (prefab == null) continue;

                // ✅ 加 offset
                Vector3 worldPos = GridToWorld(floorPos.x, floorPos.y, floorHeight + floorEdgeDecorationHeightOffset) + offset;
                GameObject instance = SpawnObject(
                    prefab,
                    worldPos,
                    Quaternion.Euler(0, Random.Range(0f, 360f), 0),
                    propsParent.transform,
                    chunkData
                );

                if (floorEdgeRotationVariation > 0f)
                {
                    float randomRotation = Random.Range(-floorEdgeRotationVariation, floorEdgeRotationVariation);
                    instance.transform.rotation = Quaternion.Euler(0, randomRotation, 0);
                }

                float scaleFactor = Random.Range(propsScaleRange.x, propsScaleRange.y);
                instance.transform.localScale *= scaleFactor;

                if (setObjectsStatic) instance.isStatic = true;

                spawnedProps.Add(instance);
                spawnedModels.Add(instance);
                usedPositions.Add(floorPos);
                counter++;
                if (counter % batchSize == 0)
                    yield return null; // 等待下一帧
            }
        }
    }


    private List<Vector3> vineRayStartPoints = new List<Vector3>();
    private List<Vector3> vineHitPoints = new List<Vector3>();
    IEnumerator GenerateVinesOnCaveBottom(Vector3 offset, ChunkData chunkData, int batchSize = 50)
    {
        if (ivyGenerator == null) yield break;
        if (spawnedMountains.Count == 0) yield break;
        if (chunkData.isMountainOver == false)
        {
            Debug.Log("山体没生成完毕");
            yield return null;
        } 


        Debug.Log($"开始边缘加权随机生成藤蔓：{vineRayCount} 条射线...");

        int layerMask = 1 << 14; // 山体层
        HashSet<Vector2Int> edgePositions = CalculateMountainEdges();

        // 统计整体区域范围（用于随机分布）
        int minX = chunkData.dungeon3DData.mountainPositions.Min(p => p.x);
        int maxX = chunkData.dungeon3DData.mountainPositions.Max(p => p.x);
        int minY = chunkData.dungeon3DData.mountainPositions.Min(p => p.y);
        int maxY = chunkData.dungeon3DData.mountainPositions.Max(p => p.y);
        int counter=0;
        //print(edgePositions.Count);
        for (int i = 0; i < vineRayCount; i++)
        {
            Vector3 rayStart;
            // 80% 概率选择边缘位置，20% 随机位置
            if (Random.value < 0.8f && edgePositions.Count > 0)
            {
                Vector2Int edgePos = edgePositions.ElementAt(Random.Range(0, edgePositions.Count));
                float offsetX = Random.Range(-0.1f, 0.1f);
                float offsetZ = Random.Range(-0.1f, 0.1f);
                rayStart = new Vector3(
                    (edgePos.x + offsetX) * gridSize,
                    vineRayHeight,
                    (edgePos.y + offsetZ) * gridSize
                );
                //print($"射线 {i + 1}: 选择边缘位置 {edgePos}，起点 {rayStart}");
            }
            else
            {
                float rx = Random.Range(minX, maxX);
                float ry = Random.Range(minY, maxY);
                rayStart = new Vector3(rx * gridSize, 50, ry * gridSize+offset.z);
            }

            Ray ray = new Ray(rayStart, Vector3.down);
            // vineRayStartPoints.Add(rayStart);

            if (Physics.Raycast(ray, out RaycastHit hit, 100000,layerMask))
            {
                //print($"射线 {i + 1}: 起点 {rayStart} 命中 {hit.point}（距离：{hit.distance}）");
                //vineHitPoints.Add(hit.point);
                ivyGenerator.generateIvy(hit);
            }

            counter++;
            if (counter % batchSize == 0)
            {
                yield return null; // 等待下一帧
            }
        }

        Debug.Log($"✅ 藤蔓生成完毕（倾向边缘区域）");
    }

    HashSet<Vector2Int> CalculateMountainEdges()
    {
        HashSet<Vector2Int> boundarySet = new HashSet<Vector2Int>();

        HashSet<Vector2Int> mountainSet = new HashSet<Vector2Int>(dungeon3DData.mountainPositions);
        HashSet<Vector2Int> floorSet = new HashSet<Vector2Int>(dungeon3DData.roomPositions
            .Concat(dungeon3DData.corridorPositions));

        foreach (var mountainPos in mountainSet)
        {
            foreach (var floorPos in floorSet)
            {
                float distance = Vector2Int.Distance(mountainPos, floorPos);
                if (distance <= floorEdgeDetectionDistance)
                {
                    boundarySet.Add(mountainPos);
                    break;
                }
            }
        }

        return boundarySet;
    }
    private void CalculateFilteredPrefabLists()
    {
        isFilteredPrefabsCached = true;

        // 计算地面prefabs的过滤列表
        if (groundPrefabsWithProbabilities != null && groundPrefabsWithProbabilities.Count > 0)
        {
            if (corridorBlockedPrefabs == null || corridorBlockedPrefabs.Count == 0)
            {
                // 如果没有需要屏蔽的prefabs，则使用原始列表
                filteredGroundPrefabsForCorridor = new List<PrefabProbability>(groundPrefabsWithProbabilities);
            }
            else
            {
                // 过滤掉在走廊屏蔽列表中的prefabs
                filteredGroundPrefabsForCorridor = groundPrefabsWithProbabilities
                    .Where(prefabProb => prefabProb.prefab != null && !corridorBlockedPrefabs.Contains(prefabProb.prefab))
                    .ToList();

                // 如果过滤后列表为空，使用原始列表并给出警告
                if (filteredGroundPrefabsForCorridor.Count == 0)
                {
                    Debug.LogWarning("走廊屏蔽过滤后地面prefabs列表为空，将使用原始列表");
                    filteredGroundPrefabsForCorridor = new List<PrefabProbability>(groundPrefabsWithProbabilities);
                }
            }
        }

        // 计算地面装饰prefabs的过滤列表
        if (floorPropsPrefabsWithProbabilities != null && floorPropsPrefabsWithProbabilities.Count > 0)
        {
            if (corridorBlockedPrefabs == null || corridorBlockedPrefabs.Count == 0)
            {
                // 如果没有需要屏蔽的prefabs，则使用原始列表
                filteredFloorPropsPrefabsForCorridor = new List<PrefabProbability>(floorPropsPrefabsWithProbabilities);
            }
            else
            {
                // 过滤掉在走廊屏蔽列表中的prefabs
                filteredFloorPropsPrefabsForCorridor = floorPropsPrefabsWithProbabilities
                    .Where(prefabProb => prefabProb.prefab != null && !corridorBlockedPrefabs.Contains(prefabProb.prefab))
                    .ToList();

                // 如果过滤后列表为空，给出警告，但不使用原始列表（装饰物可以没有）
                if (filteredFloorPropsPrefabsForCorridor.Count == 0)
                {
                    Debug.LogWarning("走廊屏蔽过滤后地面装饰prefabs列表为空");
                }
            }
        }
    }

    void CreateParentObjects()
    {
        floorsParent = new GameObject("Grounds");
        floorsParent.transform.SetParent(transform);

        wallsParent = new GameObject("Walls");
        wallsParent.transform.SetParent(transform);

        mountainsParent = new GameObject("Mountains");
        mountainsParent.transform.SetParent(transform);

        propsParent = new GameObject("Props");
        propsParent.transform.SetParent(transform);
    }

    void ExtractDungeonDataFrom2D(Dungeon3DData targetData)
    {
        targetData.roomPositions.Clear();
        targetData.corridorPositions.Clear();
        targetData.wallPositions.Clear();

        foreach (var room in dungeonGenerator2D.activeRooms)
        {
            for (int x = room.xMin; x < room.xMax; x++)
                for (int y = room.yMin; y < room.yMax; y++)
                    targetData.roomPositions.Add(new Vector2Int(x, y));
        }

        foreach (var corridorPos in dungeonGenerator2D.corridorPositions)
            targetData.corridorPositions.Add(corridorPos);

        CalculateWallPositions(targetData);
    }

    void CalculateWallPositions(Dungeon3DData targetData)
    {
        HashSet<Vector2Int> allFloorPositions = new HashSet<Vector2Int>();
        foreach (var pos in targetData.roomPositions)
            allFloorPositions.Add(pos);
        foreach (var pos in targetData.corridorPositions)
            allFloorPositions.Add(pos);

        foreach (var floorPos in allFloorPositions)
        {
            Vector2Int[] directions = {
                Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left,
                new Vector2Int(1, 1), new Vector2Int(1, -1), new Vector2Int(-1, 1), new Vector2Int(-1, -1)
            };
            foreach (var dir in directions)
            {
                Vector2Int wallPos = floorPos + dir;
                if (!allFloorPositions.Contains(wallPos))
                    targetData.wallPositions.Add(wallPos);
            }
        }
    }

    void CalculateBaseMountainArea(Dungeon3DData targetData)
    {
        targetData.mountainPositions.Clear();
        targetData.mountainLayers.Clear();
        targetData.mountainEdgePositions.Clear();

        HashSet<Vector2Int> occupiedPositions = new HashSet<Vector2Int>();
        occupiedPositions.UnionWith(targetData.roomPositions);
        occupiedPositions.UnionWith(targetData.corridorPositions);
        occupiedPositions.UnionWith(targetData.wallPositions);

        int minX = int.MaxValue, maxX = int.MinValue;
        int minY = int.MaxValue, maxY = int.MinValue;

        foreach (var pos in occupiedPositions)
        {
            if (pos.x < minX) minX = pos.x;
            if (pos.x > maxX) maxX = pos.x;
            if (pos.y < minY) minY = pos.y;
            if (pos.y > maxY) maxY = pos.y;
        }

        for (int x = minX - mountainThickness; x <= maxX + mountainThickness; x++)
        {
            for (int y = minY - mountainThickness; y <= maxY + mountainThickness; y++)
            {
                Vector2Int pos = new Vector2Int(x, y);
                if (!occupiedPositions.Contains(pos))
                    targetData.mountainPositions.Add(pos);
            }
        }
    }

    void ExpandMountainAreaWithNoise(Dungeon3DData targetData)
    {
        HashSet<Vector2Int> currentMountainSet = new HashSet<Vector2Int>(targetData.mountainPositions);
        HashSet<Vector2Int> occupiedPositions = new HashSet<Vector2Int>();
        occupiedPositions.UnionWith(targetData.roomPositions);
        occupiedPositions.UnionWith(targetData.corridorPositions);
        occupiedPositions.UnionWith(targetData.wallPositions);

        List<Vector2Int> expandedPositions = new List<Vector2Int>(targetData.mountainPositions);

        for (int distance = 1; distance <= expansionDistance; distance++)
        {
            List<Vector2Int> newEdgePositions = new List<Vector2Int>();

            foreach (var mountainPos in currentMountainSet)
            {
                Vector2Int[] directions = { Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left };
                foreach (var dir in directions)
                {
                    Vector2Int neighborPos = mountainPos + dir;
                    if (!occupiedPositions.Contains(neighborPos) && !currentMountainSet.Contains(neighborPos))
                        newEdgePositions.Add(neighborPos);
                }
            }

            foreach (var edgePos in newEdgePositions.Distinct())
            {
                float noiseValue = CalculateExpansionNoiseValue(edgePos.x, edgePos.y);
                if (noiseValue > expansionNoiseThreshold && Random.value < expansionProbability)
                {
                    expandedPositions.Add(edgePos);
                    currentMountainSet.Add(edgePos);
                }
            }
        }
        dungeon3DData.mountainPositions = expandedPositions;
    }

    float CalculateExpansionNoiseValue(int x, int y)
    {
        float seedOffset = noiseSeed * 0.01f;
        float mainNoise = Mathf.PerlinNoise(x * expansionNoiseScale + seedOffset, y * expansionNoiseScale + seedOffset);
        float detailNoise = Mathf.PerlinNoise(x * expansionNoiseScale * 0.5f + seedOffset + 100, y * expansionNoiseScale * 0.5f + seedOffset + 100);
        return Mathf.Max(mainNoise, detailNoise * 0.7f);
    }

    // 优化：使用缓存的过滤列表
    List<PrefabProbability> GetFilteredGroundPrefabsForPosition(Vector2Int pos)
    {
        if (groundPrefabsWithProbabilities == null || groundPrefabsWithProbabilities.Count == 0)
            return groundPrefabsWithProbabilities;

        // 检查当前位置是否是走廊
        bool isCorridor = dungeon3DData.corridorPositions.Contains(pos);
        if (isCorridor)
        {
            // 如果已计算过过滤列表，则使用缓存的列表
            if (isFilteredPrefabsCached && filteredGroundPrefabsForCorridor != null)
            {
                return filteredGroundPrefabsForCorridor;
            }
        }

        return groundPrefabsWithProbabilities;
    }

    GameObject ChoosePrefabByNoiseValue(List<PrefabProbability> list, float noiseValue)
    {
        if (list == null || list.Count == 0) return null;

        // 计算总概率
        float totalProbability = 0f;
        foreach (var item in list)
        {
            totalProbability += item.probability;
        }

        // 如果总概率为0，返回列表中的第一个prefab
        if (Mathf.Approximately(totalProbability, 0f))
            return list[0].prefab;

        // 根据噪声值映射到总概率范围
        float targetProbability = noiseValue * totalProbability;
        float cumulative = 0f;

        for (int i = 0; i < list.Count; i++)
        {
            cumulative += list[i].probability;
            if (targetProbability <= cumulative)
                return list[i].prefab;
        }

        return list.Last().prefab;
    }
    GameObject ChoosePrefabByProbability(List<PrefabProbability> list)
    {
        if (list == null || list.Count == 0) return null;

        // 计算总概率
        float total = 0f;
        foreach (var item in list)
        {
            total += item.probability;
        }

        // 如果总概率为0，返回列表中的第一个prefab
        if (Mathf.Approximately(total, 0f))
            return list[0].prefab;

        float randomPoint = Random.value * total;
        float cumulative = 0f;

        for (int i = 0; i < list.Count; i++)
        {
            cumulative += list[i].probability;
            if (randomPoint <= cumulative)
                return list[i].prefab;
        }

        return list.Last().prefab;
    }

    List<Vector2Int> CalculateHigherMountainLayer(int layer, List<Vector2Int> previousLayer)
    {
        List<Vector2Int> currentLayer = new List<Vector2Int>();
        HashSet<Vector2Int> previousLayerSet = new HashSet<Vector2Int>(previousLayer);
        List<Vector2Int> previousLayerEdges = CalculateLayerEdgePositions(previousLayer);
        HashSet<Vector2Int> previousLayerEdgeSet = new HashSet<Vector2Int>(previousLayerEdges);

        foreach (var pos in previousLayer)
        {
            if (excludeEdgesFromHigherLayers && previousLayerEdgeSet.Contains(pos)) continue;
            float noiseValue = CalculateLayerNoiseValue(pos.x, pos.y, layer);
            if (noiseValue > layerNoiseThreshold) currentLayer.Add(pos);
        }
        return currentLayer;
    }
    List<Vector2Int> CalculateLayerEdgePositions(List<Vector2Int> layerPositions)
    {
        List<Vector2Int> edges = new List<Vector2Int>();
        HashSet<Vector2Int> layerSet = new HashSet<Vector2Int>(layerPositions);
        foreach (var pos in layerPositions)
        {
            Vector2Int[] directions = { Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left };
            foreach (var dir in directions)
            {
                if (!layerSet.Contains(pos + dir))
                {
                    edges.Add(pos);
                    break;
                }
            }
        }
        return edges;
    }

    float GetLayerHeight(int layer)
    {
        if (useCustomLayerHeights && layer < layerHeights.Count)
        {
            float height = baseMountainHeight;
            for (int i = 0; i <= layer; i++) height += layerHeights[i];
            return height;
        }
        else
        {
            return baseMountainHeight + (layer * mountainLayerHeight);
        }
    }
    GameObject GetMountainPrefabForLayer(int layer)
    {
        if (layer == 0) return mountainPrefab;
        else if (layer == 1 && mountainLayer2Prefab != null) return mountainLayer2Prefab;
        else if (layer >= 2 && mountainLayer3PlusPrefab != null) return mountainLayer3PlusPrefab;
        else return mountainPrefab;
    }
    float CalculateLayerNoiseValue(int x, int y, int layer)
    {
        float seedOffset = (noiseSeed + layer * 100) * 0.01f;
        float baseNoise = Mathf.PerlinNoise(x * layerNoiseScale + seedOffset, y * layerNoiseScale + seedOffset + 100);
        float detailNoise = Mathf.PerlinNoise(x * noiseDetailScale + seedOffset + 200, y * noiseDetailScale + seedOffset + 300);
        float combinedNoise = (baseNoise + detailNoise * 0.5f) / 1.5f;
        combinedNoise = Mathf.Clamp01(combinedNoise * layerDensity);
        return combinedNoise;
    }
    float CalculateNoiseForRotation(int x, int y, int layer)
    {
        float seedOffset = (noiseSeed + layer * 1000) * 0.01f;
        return Mathf.PerlinNoise(x * rotationNoiseScale + seedOffset, y * rotationNoiseScale + seedOffset + 500);
    }

    float CalculateFloorEdgeNoise(int x, int y)
    {
        float seedOffset = noiseSeed * 0.01f;
        float offset = 700f; // 与其他噪声使用不同的偏移
        return Mathf.PerlinNoise(
            x * floorEdgeNoiseScale + seedOffset + offset,
            y * floorEdgeNoiseScale + seedOffset + offset
        );
    }

    List<PrefabProbability> GetFilteredFloorPropsPrefabsForPosition(Vector2Int pos)
    {
        if (floorPropsPrefabsWithProbabilities == null || floorPropsPrefabsWithProbabilities.Count == 0)
            return floorPropsPrefabsWithProbabilities;

        // 检查当前位置是否是走廊
        bool isCorridor = dungeon3DData.corridorPositions.Contains(pos);
        if (isCorridor)
        {
            // 如果已计算过过滤列表，则使用缓存的列表
            if (isFilteredPrefabsCached && filteredFloorPropsPrefabsForCorridor != null)
            {
                return filteredFloorPropsPrefabsForCorridor;
            }
        }

        return floorPropsPrefabsWithProbabilities;
    }

    void ClampDungeonBounds(Vector2Int minBound, Vector2Int maxBound, Dungeon3DData targetData)
    {
        // 1. 把所有位置都裁切到范围内（删除范围外的数据）
        targetData.roomPositions = targetData.roomPositions
            .Where(p => p.x >= minBound.x && p.x <= maxBound.x && p.y >= minBound.y && p.y <= maxBound.y)
            .ToList();

        targetData.corridorPositions = targetData.corridorPositions
            .Where(p => p.x >= minBound.x && p.x <= maxBound.x && p.y >= minBound.y && p.y <= maxBound.y)
            .ToList();

        targetData.wallPositions = targetData.wallPositions
            .Where(p => p.x >= minBound.x && p.x <= maxBound.x && p.y >= minBound.y && p.y <= maxBound.y)
            .ToList();

        targetData.mountainPositions = targetData.mountainPositions
            .Where(p => p.x >= minBound.x && p.x <= maxBound.x && p.y >= minBound.y && p.y <= maxBound.y)
            .ToList();

        // 2. 把范围内的所有空位置都用山体补齐
        HashSet<Vector2Int> allOccupied = new HashSet<Vector2Int>();
        allOccupied.UnionWith(targetData.roomPositions);
        allOccupied.UnionWith(targetData.corridorPositions);
        allOccupied.UnionWith(targetData.wallPositions);
        allOccupied.UnionWith(targetData.mountainPositions);

        for (int x = minBound.x; x <= maxBound.x; x++)
        {
            for (int y = minBound.y; y <= maxBound.y; y++)
            {
                Vector2Int pos = new Vector2Int(x, y);
                if (!allOccupied.Contains(pos))
                {
                    targetData.mountainPositions.Add(pos);
                }
            }
        }
    }

    public void ClearDungeon3D()
    {
        // 销毁已生成的模型
        foreach (var obj in spawnedModels)
        {
            if (obj != null)
            {
#if UNITY_EDITOR
                if (!Application.isPlaying)
                    DestroyImmediate(obj);
                else
#endif
                    Destroy(obj);
            }
        }

        // 清空列表
        spawnedModels.Clear();
        spawnedMountains.Clear();
        spawnedFloors.Clear();
        spawnedWalls.Clear();
        spawnedProps.Clear();
        generatedFloorPositions.Clear();

        // 清空父对象（以防空的父节点残留）
        if (floorsParent != null) DestroyImmediate(floorsParent);
        if (wallsParent != null) DestroyImmediate(wallsParent);
        if (mountainsParent != null) DestroyImmediate(mountainsParent);
        if (propsParent != null) DestroyImmediate(propsParent);

        floorsParent = null;
        wallsParent = null;
        mountainsParent = null;
        propsParent = null;

        // 重置3D数据
        dungeon3DData = new Dungeon3DData();

        // 也可以重置Prefab过滤状态
        filteredGroundPrefabsForCorridor = null;
        filteredFloorPropsPrefabsForCorridor = null;
        isFilteredPrefabsCached = false;

        Debug.Log("✅ 已清空 3D 地牢，恢复原状");
    }
    Vector3 GridToWorld(int gridX, int gridY, float height)
    {
        return new Vector3(gridX * gridSize, height, gridY * gridSize);
    }
#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (!debugVinePositions) return;

        // 起点用黄色
        Gizmos.color = Color.yellow;
        foreach (var start in vineRayStartPoints)
        {
            Gizmos.DrawSphere(start, 0.3f);
        }

        // 命中点用绿色
        Gizmos.color = Color.green;
        foreach (var hit in vineHitPoints)
        {
            Gizmos.DrawSphere(hit, 0.25f);
        }

        // 连线（灰色）
        Gizmos.color = new Color(0.4f, 0.4f, 0.4f, 0.7f);
        for (int i = 0; i < vineRayStartPoints.Count && i < vineHitPoints.Count; i++)
        {
            Gizmos.DrawLine(vineRayStartPoints[i], vineHitPoints[i]);
        }
    }
#endif
}
