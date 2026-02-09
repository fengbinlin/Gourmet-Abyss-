using System;
using System.Collections.Generic;
using System.Linq; // 引入 Linq 方便列表操作
using UnityEngine;
using UnityEngine.Tilemaps;

[System.Serializable]
public class Room
{
    public Vector2 center; // 房间中心坐标
    public Vector2 size;   // 房间尺寸
    public int id;         // 房间唯一标识符

    // 计算并返回房间的矩形边界
    public Rect Bounds => new Rect(center - size * 0.5f, size);

    // 获取整数边界（用于Tilemap绘制）
    public int xMin => Mathf.RoundToInt(center.x - size.x * 0.5f);
    public int xMax => Mathf.RoundToInt(center.x + size.x * 0.5f);
    public int yMin => Mathf.RoundToInt(center.y - size.y * 0.5f);
    public int yMax => Mathf.RoundToInt(center.y + size.y * 0.5f);

    public float Area => size.x * size.y; // 计算房间面积

    public Room(Vector2 center, Vector2 size, int id)
    {
        this.center = center;
        this.size = size;
        this.id = id;
    }

    // 检查两个房间是否重叠
    public bool Overlaps(Room other)
    {
        return Bounds.Overlaps(other.Bounds);
    }

    // 计算到另一个房间中心的距离
    public float DistanceTo(Room other)
    {
        return Vector2.Distance(center, other.center);
    }

    public override bool Equals(object obj)
    {
        if (obj is Room other)
            return id == other.id;
        return false;
    }

    public override int GetHashCode()
    {
        return id;
    }
}

[System.Serializable]
public class Edge
{
    public Room a, b; // 边连接的两个房间
    public float length; // 边的长度（两房间中心距离）

    public Edge(Room a, Room b)
    {
        this.a = a;
        this.b = b;
        this.length = a.DistanceTo(b);
    }

    public bool Equals(Edge other)
    {
        return (a.Equals(other.a) && b.Equals(other.b)) || (a.Equals(other.b) && b.Equals(other.a));
    }
}

[System.Serializable]
public class Triangle
{
    public Room a, b, c; // 三角形的三个顶点
    public Vector2 circumcenter; // 外接圆圆心
    public float circumradius;   // 外接圆半径

    public Triangle(Room a, Room b, Room c)
    {
        this.a = a; this.b = b; this.c = c;
        CalculateCircumcircle(); // 构造时计算外接圆
    }

    // 计算三角形的外接圆
    private void CalculateCircumcircle()
    {
        Vector2 A = a.center;
        Vector2 B = b.center;
        Vector2 C = c.center;

        // 计算外接圆圆心公式的分母
        float D = 2 * (A.x * (B.y - C.y) + B.x * (C.y - A.y) + C.x * (A.y - B.y));
        if (Mathf.Abs(D) < 0.0001f) // 三点共线的情况
        {
            circumcenter = (A + B + C) / 3f;
            circumradius = 0;
            return;
        }

        float A2 = A.x * A.x + A.y * A.y;
        float B2 = B.x * B.x + B.y * B.y;
        float C2 = C.x * C.x + C.y * C.y;

        float Ux = (A2 * (B.y - C.y) + B2 * (C.y - A.y) + C2 * (A.y - B.y)) / D;
        float Uy = (A2 * (C.x - B.x) + B2 * (A.x - C.x) + C2 * (B.x - A.x)) / D;

        circumcenter = new Vector2(Ux, Uy);
        circumradius = Vector2.Distance(circumcenter, A);
    }

    // 判断点是否在三角形的外接圆内
    public bool ContainsInCircumcircle(Vector2 point)
    {
        return Vector2.Distance(circumcenter, point) <= circumradius + 0.0001f;
    }

    // 判断三角形是否包含指定房间作为顶点
    public bool ContainsVertex(Room room)
    {
        return a.Equals(room) || b.Equals(room) || c.Equals(room);
    }
}

[System.Serializable]
public class DungeonGenerator : MonoBehaviour
{
    [Header("地牢边界限制")]
    public Vector2 boundaryMin = new Vector2(-50, -50);
    public Vector2 boundaryMax = new Vector2(50, 50);
    [Header("标记物房间设置")]
    public List<Vector2Int> markerPositions = new List<Vector2Int>();
    [Range(4, 20)] public int markerMinRoomSize = 6;
    [Range(4, 20)] public int markerMaxRoomSize = 12;
    public TileBase markerTile; // 给标记房间用的特殊 Tile
    [Header("基础生成设置")]
    [Range(5, 100)] public int roomCount = 50; // 初始生成的房间数量
    [Range(4, 20)] public int minRoomSize = 4;   // 房间最小尺寸
    [Range(20, 40)] public int maxRoomSize = 12; // 房间最大尺寸
    [Range(0.1f, 1f)] public float mainRoomThreshold = 0.3f; // 主房间面积阈值（前30%面积为大房间）
    [Range(0f, 1f)] public float extraEdgeChance = 0.1f; // 额外边添加概率

    [Header("布局控制")]
    [Tooltip("控制地牢的整体形状。\n(1, 1) = 圆形\n(2, 0.5) = 扁长横向\n(0.5, 2) = 细长纵向")]
    public Vector2 spreadBias = new Vector2(1f, 1f); // 新增：方向偏向参数

    [Range(1, 100)] public int maxIterations = 50; // 房间分离最大迭代次数
    public bool useSeed = false; // 是否使用固定种子
    public int seed = 12345;     // 随机种子

    [Header("可视化")]
    public Tilemap tilemap;     // Unity Tilemap组件
    public TileBase floorTile;  // 地板Tile
    public TileBase wallTile;   // 墙壁Tile
    public TileBase corridorTile; // 走廊Tile（如果不需要区分，可以和floorTile设置一样）

    [Header("房间分离物理")]
    [Range(0.5f, 2f)] public float separationForce = 1.0f; // 房间分离推力
    [Range(0f, 5f)] public float minRoomSpacing = 1.0f;    // 房间间最小间距
    [Range(0f, 1f)] public float dampingFactor = 0.8f;     // 分离力衰减系数

    [Header("有机化 (Random Walk)")]
    [Tooltip("启用后会在现有的房间基础上进行随机挖掘，使边缘更自然")]
    public bool useRandomWalk = true; // 是否启用随机游走
    [Tooltip("生成的游走者数量")]
    [Range(1, 200)] public int randomWalkIterations = 50; // 随机游走迭代次数
    [Tooltip("每个游走者行走的步数")]
    [Range(1, 20)] public int randomWalkSteps = 10; // 每次游走步数

    [Header("调试信息")]
    [SerializeField] public int totalRooms;     // 总房间数
    [SerializeField] public int mainRoomsCount; // 主房间数
    [SerializeField] public int corridorsCount; // 走廊位置数

    // 数据存储列表
    private List<Room> allRooms = new List<Room>();      // 所有生成的房间
    private List<Room> mainRooms = new List<Room>();     // 主房间（大房间）
    public List<Room> activeRooms = new List<Room>();  // 激活的房间（非孤立房间）
    private List<Edge> mstEdges = new List<Edge>();     // 最小生成树的边
    public HashSet<Vector2Int> corridorPositions = new HashSet<Vector2Int>(); // 走廊位置集合
    private HashSet<Vector2Int> roomFloorPositions = new HashSet<Vector2Int>(); // 所有地板位置集合
    private System.Random random; // 随机数生成器

    void Awake()
    {
        //GenerateDungeon(); // 游戏开始时生成地牢
    }

    // 主生成函数
    public void GenerateDungeon(List<Vector2Int> t_markerPositions = null)
    {
        markerPositions = t_markerPositions ?? markerPositions; // 如果传入了标记位置，使用它们；否则使用Inspector中的默认值
        
        random = new System.Random(DateTime.Now.Millisecond);
        ClearDungeon(); // 清空现有地牢
        random = useSeed ? new System.Random(seed) : new System.Random(DateTime.Now.Millisecond);

        GenerateRandomRooms();  // 步骤1：生成随机房间
        SelectMainRooms();       // 步骤3：选择主房间
        SeparateRooms();         // 步骤2：分离房间使其不重叠


        if (mainRooms.Count >= 2) // 只有至少2个主房间才能生成走廊
        {
            List<Edge> delaunayEdges = DelaunayTriangulation(mainRooms); // Delaunay三角剖分
            List<Edge> mst = KruskalMST(mainRooms, delaunayEdges); // 最小生成树
            AddExtraEdges(delaunayEdges, mst); // 添加额外边增加环路
            mstEdges = mst; // 保存最小生成树边
            GenerateCorridors(); // 步骤4：生成走廊连接房间
        }

        CullIsolatedRooms(); // 步骤5：剔除孤立房间
        DrawDungeon();       // 步骤6：绘制地牢到Tilemap
        UpdateDebugInfo();   // 更新调试信息
        //Dungeon3DGenerator.instance.GenerateDungeon3D();
    }

    // 清空地牢数据
    public void ClearDungeon()
    {
        tilemap.ClearAllTiles(); // 清空Tilemap
        allRooms.Clear();
        mainRooms.Clear();
        activeRooms.Clear();
        mstEdges.Clear();
        corridorPositions.Clear();
        roomFloorPositions.Clear();
    }

    // 生成随机房间
    void GenerateRandomRooms()
    {
        float radius = Mathf.Sqrt(roomCount) * 3f; // 计算分布半径
        int idCounter = 0;

        // 1. 生成标记物房间（固定位置）
        // 1. 先生成标记房间（固定位置，不分离）
        foreach (var posInt in markerPositions)
        {
            Vector2 pos = new Vector2(posInt.x, posInt.y); // Tilemap 网格坐标
            int w = random.Next(markerMinRoomSize, markerMaxRoomSize + 1);
            int h = random.Next(markerMinRoomSize, markerMaxRoomSize + 1);
            Vector2 size = new Vector2(w, h);

            Room r = new Room(pos, size, idCounter++);
            allRooms.Add(r);
            mainRooms.Add(r); // 强制主房间
        }
        for (int i = 0; i < roomCount; i++)
        {
            // 在圆形区域内生成随机位置
            float angle = (float)random.NextDouble() * Mathf.PI * 2f;
            float r = radius * Mathf.Sqrt((float)random.NextDouble());

            // 应用spreadBias将圆形分布变为椭圆分布
            float x = Mathf.Cos(angle) * r * spreadBias.x;
            float y = Mathf.Sin(angle) * r * spreadBias.y;

            Vector2 pos = new Vector2(Mathf.Round(x), Mathf.Round(y)); // 对齐到网格

            int w = random.Next(minRoomSize, maxRoomSize + 1);
            int h = random.Next(minRoomSize, maxRoomSize + 1);
            Vector2 size = new Vector2(w, h);

            allRooms.Add(new Room(pos, size, i));
        }
    }

    // 分离房间使其不重叠
    void SeparateRooms()
    {
        float pushForce = separationForce; // 初始分离力

        for (int iteration = 0; iteration < maxIterations; iteration++)
        {
            bool moved = false; // 标记本轮是否有房间移动

            for (int i = 0; i < mainRooms.Count; i++)
            {
                for (int j = i + 1; j < mainRooms.Count; j++)
                {
                    Room a = mainRooms[i];
                    Room b = mainRooms[j];
                    if (IsMarkerRoom(a) || IsMarkerRoom(b)) continue;

                    float overlapX = 0f;
                    float minX = Mathf.Max(a.xMin, b.xMin);
                    float maxX = Mathf.Min(a.xMax, b.xMax);
                    if (minX < maxX) overlapX = maxX - minX;

                    float overlapY = 0f;
                    float minY = Mathf.Max(a.yMin, b.yMin);
                    float maxY = Mathf.Min(a.yMax, b.yMax);
                    if (minY < maxY) overlapY = maxY - minY;

                    float requiredSpacingX = minRoomSpacing;
                    float requiredSpacingY = minRoomSpacing;

                    if (overlapX > 0)
                    {
                        float pushDistanceX = overlapX + requiredSpacingX;
                        float dirX = Mathf.Sign(a.center.x - b.center.x);
                        if (Mathf.Abs(dirX) < 0.001f) dirX = (random.Next(0, 2) == 0) ? 1f : -1f;

                        Vector2 separationX = new Vector2(dirX * pushDistanceX * pushForce * 0.5f, 0);
                        a.center += separationX;
                        b.center -= separationX;

                        // 边界限制
                        a.center = ClampRoomToBounds(a);
                        b.center = ClampRoomToBounds(b);

                        moved = true;
                    }
                    if (overlapY > 0)
                    {
                        float pushDistanceY = overlapY + requiredSpacingY;
                        float dirY = Mathf.Sign(a.center.y - b.center.y);
                        if (Mathf.Abs(dirY) < 0.001f) dirY = (random.Next(0, 2) == 0) ? 1f : -1f;

                        Vector2 separationY = new Vector2(0, dirY * pushDistanceY * pushForce * 0.5f);
                        a.center += separationY;
                        b.center -= separationY;

                        // 边界限制
                        a.center = ClampRoomToBounds(a);
                        b.center = ClampRoomToBounds(b);

                        moved = true;
                    }
                }
            }

            if (!moved) break;
            pushForce *= dampingFactor;
        }

        // 对齐到整数网格
        foreach (var room in mainRooms)
        {
            room.center = new Vector2(Mathf.Round(room.center.x), Mathf.Round(room.center.y));
            room.center = ClampRoomToBounds(room); // 最后再做一次边界检查
        }
    }

    // 钳制房间到边界
    Vector2 ClampRoomToBounds(Room room)
    {
        float halfW = room.size.x / 2f;
        float halfH = room.size.y / 2f;

        float clampedX = Mathf.Clamp(room.center.x, boundaryMin.x + halfW, boundaryMax.x - halfW);
        float clampedY = Mathf.Clamp(room.center.y, boundaryMin.y + halfH, boundaryMax.y - halfH);

        return new Vector2(clampedX, clampedY);
    }
    bool IsMarkerRoom(Room room)
    {
        return markerPositions.Contains(new Vector2Int(Mathf.RoundToInt(room.center.x), Mathf.RoundToInt(room.center.y)));
    }
    // 选择主房间（面积较大的房间）
    void SelectMainRooms()
    {
        if (allRooms.Count == 0) return;

        List<float> areas = new List<float>();
        foreach (var room in allRooms) areas.Add(room.Area);
        areas.Sort(); // 按面积排序

        // 根据阈值计算面积分界点
        float thresholdArea = areas[Mathf.FloorToInt((1f - mainRoomThreshold) * (areas.Count - 1))];
        // 保留标记房间 + 面积足够大的房间
        List<Room> newMainRooms = new List<Room>();
        //mainRooms.Clear();
        foreach (var room in allRooms)
        {
            if (room.Area >= thresholdArea) // 面积大于阈值的房间
            {
                mainRooms.Add(room);
            }
        }
        //mainRooms = newMainRooms;
    }

    // Delaunay三角剖分算法
    List<Edge> DelaunayTriangulation(List<Room> rooms)
    {
        if (rooms.Count < 2) return new List<Edge>();
        if (rooms.Count == 2) return new List<Edge> { new Edge(rooms[0], rooms[1]) };

        List<Triangle> triangles = new List<Triangle>();
        List<Triangle> badTriangles = new List<Triangle>(); // 坏三角形（包含新点的外接圆）
        List<Edge> polygon = new List<Edge>(); // 多边形边

        // 计算超级三角形：先求包围盒，再求包围盒的外接圆，最后求圆的外接三角形
        Vector2 min = rooms[0].center;
        Vector2 max = rooms[0].center;

        // 1. 计算所有点的最小包围盒
        foreach (var room in rooms)
        {
            Vector2 center = room.center;
            min = Vector2.Min(min, center);
            max = Vector2.Max(max, center);
        }

        Vector2 boundingBoxSize = max - min;
        Vector2 boundingBoxCenter = (min + max) * 0.5f;

        // 2. 计算包围盒的外接圆
        // 外接圆的半径等于包围盒对角线长度的一半
        float boundingCircleRadius = boundingBoxSize.magnitude * 0.5f;

        // 为了确保安全，将半径稍微扩大一点（避免点刚好在圆上）
        float safeRadius = boundingCircleRadius * 1.1f;


        // 创建超级三角形的三个顶点
        // 计算圆的外切等边三角形
        // 对于等边三角形，内切圆半径r与边长a的关系：
        // r = a * √3 / 6
        // 所以：a = r * 6 / √3 = r * 2√3

        float triangleSideLength = safeRadius * 2f * Mathf.Sqrt(3f);
        float triangleHeight = triangleSideLength * Mathf.Sqrt(3f) * 0.5f;

        // 等边三角形的重心与内切圆圆心重合
        // 重心到各顶点的距离 = 外接圆半径 = a * √3 / 3
        float circumradius = triangleSideLength * Mathf.Sqrt(3f) / 3f;

        // 顶点1：正上方
        Room superA = new Room(
            new Vector2(boundingBoxCenter.x, boundingBoxCenter.y + circumradius),
            Vector2.zero, -1);

        // 顶点2：左下（210°）
        float angle210 = 210f * Mathf.Deg2Rad;
        Room superB = new Room(
            new Vector2(
                boundingBoxCenter.x + circumradius * Mathf.Cos(angle210),
                boundingBoxCenter.y + circumradius * Mathf.Sin(angle210)
            ),
            Vector2.zero, -2);

        // 顶点3：右下（330°）
        float angle330 = 330f * Mathf.Deg2Rad;
        Room superC = new Room(
            new Vector2(
                boundingBoxCenter.x + circumradius * Mathf.Cos(angle330),
                boundingBoxCenter.y + circumradius * Mathf.Sin(angle330)
            ),
            Vector2.zero, -3);


        // 选择一种方法创建超级三角形
        Triangle superTriangle = new Triangle(superA, superB, superC);
        triangles.Add(superTriangle);

        // 逐点插入算法
        foreach (var room in rooms)
        {
            badTriangles.Clear();
            polygon.Clear();

            // 查找所有外接圆包含当前点的三角形
            for (int i = triangles.Count - 1; i >= 0; i--)
            {
                Triangle tri = triangles[i];
                if (tri.ContainsInCircumcircle(room.center))
                {
                    badTriangles.Add(tri);
                    triangles.RemoveAt(i);
                }
            }

            // 收集坏三角形的边
            foreach (var tri in badTriangles)
            {
                polygon.Add(new Edge(tri.a, tri.b));
                polygon.Add(new Edge(tri.b, tri.c));
                polygon.Add(new Edge(tri.c, tri.a));
            }

            // 使用改进的移除重复边方法
            RemoveDuplicateEdges(polygon);

            // 用新三角形填充空洞
            foreach (var edge in polygon)
            {
                triangles.Add(new Triangle(edge.a, edge.b, room));
            }
        }

        // 移除包含超级三角形顶点的三角形
        for (int i = triangles.Count - 1; i >= 0; i--)
        {
            Triangle tri = triangles[i];
            if (tri.ContainsVertex(superA) || tri.ContainsVertex(superB) || tri.ContainsVertex(superC))
                triangles.RemoveAt(i);
        }

        // 从三角形中提取边
        List<Edge> edges = new List<Edge>();
        HashSet<string> edgeSet = new HashSet<string>(); // 用于去重

        foreach (var tri in triangles)
        {
            AddUniqueEdge(edges, edgeSet, tri.a, tri.b);
            AddUniqueEdge(edges, edgeSet, tri.b, tri.c);
            AddUniqueEdge(edges, edgeSet, tri.c, tri.a);
        }
        return edges;
    }

    // 移除重复边 - 正确的Bowyer-Watson算法实现
    void RemoveDuplicateEdges(List<Edge> edges)
    {
        if (edges.Count == 0) return;

        // 使用字典统计每条边出现的次数
        Dictionary<string, (Edge edge, int count)> edgeStats = new Dictionary<string, (Edge, int)>();

        // 遍历所有边进行统计
        foreach (var edge in edges)
        {
            // 标准化边的键（确保小id在前，避免方向问题）
            int id1 = edge.a.id;
            int id2 = edge.b.id;
            if (id1 > id2) (id1, id2) = (id2, id1);

            string key = $"{id1}-{id2}";

            if (edgeStats.ContainsKey(key))
            {
                // 已存在，增加计数
                var stat = edgeStats[key];
                edgeStats[key] = (stat.edge, stat.count + 1);
            }
            else
            {
                // 新边
                edgeStats[key] = (edge, 1);
            }
        }

        // 清空原列表
        edges.Clear();

        // 根据你的逻辑重新构建边列表：
        // 1. 如果出现偶数次，完全删除（不添加到结果中）
        // 2. 如果出现奇数次，只添加一次
        foreach (var kvp in edgeStats)
        {
            int count = kvp.Value.count;
            Edge edge = kvp.Value.edge;

            if (count % 2 == 1) // 奇数次
            {
                // 只添加一次
                edges.Add(edge);
            }
            // 偶数次：完全删除（不添加）
        }
    }

    // 添加唯一边到列表
    void AddUniqueEdge(List<Edge> edges, HashSet<string> edgeSet, Room a, Room b)
    {
        if (a.id < 0 || b.id < 0) return; // 跳过超级三角形边
        string key1 = $"{a.id}-{b.id}";
        string key2 = $"{b.id}-{a.id}";
        if (!edgeSet.Contains(key1) && !edgeSet.Contains(key2))
        {
            edges.Add(new Edge(a, b));
            edgeSet.Add(key1);
        }
    }

    // Kruskal最小生成树算法
    List<Edge> KruskalMST(List<Room> rooms, List<Edge> edges)
    {
        List<Edge> mst = new List<Edge>(); // 最小生成树
        edges.Sort((e1, e2) => e1.length.CompareTo(e2.length)); // 按边长排序
        Dictionary<Room, Room> parent = new Dictionary<Room, Room>(); // 并查集父节点

        // 查找根节点（带路径压缩）
        Room Find(Room r)
        {
            if (!parent.ContainsKey(r)) parent[r] = r;
            if (parent[r] != r) parent[r] = Find(parent[r]);
            return parent[r];
        }

        // 合并两个集合
        void Union(Room a, Room b)
        {
            Room rootA = Find(a);
            Room rootB = Find(b);
            if (rootA != rootB) parent[rootA] = rootB;
        }

        foreach (var edge in edges)
        {
            if (Find(edge.a) != Find(edge.b)) // 不在同一集合中
            {
                mst.Add(edge);
                Union(edge.a, edge.b);
            }
        }
        return mst;
    }

    // 添加额外边增加环路
    void AddExtraEdges(List<Edge> delaunayEdges, List<Edge> mst)
    {
        int extraCount = Mathf.Max(1, Mathf.FloorToInt(mst.Count * extraEdgeChance));
        List<Edge> availableEdges = new List<Edge>();

        // 收集不在MST中的Delaunay边
        foreach (var edge in delaunayEdges)
        {
            bool exists = false;
            foreach (var mstEdge in mst)
            {
                if (edge.Equals(mstEdge)) { exists = true; break; }
            }
            if (!exists) availableEdges.Add(edge);
        }

        // 随机选择额外边
        for (int i = 0; i < extraCount && availableEdges.Count > 0; i++)
        {
            int index = random.Next(0, availableEdges.Count);
            mst.Add(availableEdges[index]);
            availableEdges.RemoveAt(index);
        }
    }

    // 生成走廊连接所有MST边
    void GenerateCorridors()
    {
        corridorPositions.Clear();
        foreach (var edge in mstEdges)
        {
            ConnectRoomsWithCorridor(edge.a, edge.b);
        }
    }

    // 用L形走廊连接两个房间
    // 修改ConnectRoomsWithCorridor方法，使其从房间边缘开始连接
    void ConnectRoomsWithCorridor(Room roomA, Room roomB)
    {
        // 获取房间的边缘点
        Vector2Int startPoint = GetEdgePointTowardsRoom(roomA, roomB);
        Vector2Int endPoint = GetEdgePointTowardsRoom(roomB, roomA);

        // 绘制L形走廊
        if (random.Next(0, 2) == 0)
        {
            // 先水平后垂直
            DrawLine(startPoint, new Vector2Int(endPoint.x, startPoint.y));
            DrawLine(new Vector2Int(endPoint.x, startPoint.y), endPoint);
        }
        else
        {
            // 先垂直后水平
            DrawLine(startPoint, new Vector2Int(startPoint.x, endPoint.y));
            DrawLine(new Vector2Int(startPoint.x, endPoint.y), endPoint);
        }
    }

    // 新增：获取房间A朝向房间B的边缘点
    Vector2Int GetEdgePointTowardsRoom(Room sourceRoom, Room targetRoom)
    {
        // 计算房间边界
        int xMin = sourceRoom.xMin;
        int xMax = sourceRoom.xMax;
        int yMin = sourceRoom.yMin;
        int yMax = sourceRoom.yMax;

        // 计算房间中心位置
        Vector2 sourceCenter = sourceRoom.center;
        Vector2 targetCenter = targetRoom.center;

        // 计算方向向量
        Vector2 direction = targetCenter - sourceCenter;
        Vector2 normalizedDir = direction.normalized;

        // 计算边缘点
        Vector2 edgePoint = sourceCenter;

        // 根据方向确定从哪个边缘出去
        float absX = Mathf.Abs(normalizedDir.x);
        float absY = Mathf.Abs(normalizedDir.y);

        if (absX > absY)
        {
            // 主要沿X轴方向
            if (normalizedDir.x > 0)
            {
                // 向右，从右边缘
                edgePoint.x = xMax;
            }
            else
            {
                // 向左，从左边缘
                edgePoint.x = xMin;
            }
        }
        else
        {
            // 主要沿Y轴方向
            if (normalizedDir.y > 0)
            {
                // 向上，从上边缘
                edgePoint.y = yMax;
            }
            else
            {
                // 向下，从下边缘
                edgePoint.y = yMin;
            }
        }

        // 确保边缘点在房间内部
        edgePoint.x = Mathf.Clamp(Mathf.RoundToInt(edgePoint.x), xMin, xMax - 1);
        edgePoint.y = Mathf.Clamp(Mathf.RoundToInt(edgePoint.y), yMin, yMax - 1);

        return new Vector2Int(Mathf.RoundToInt(edgePoint.x), Mathf.RoundToInt(edgePoint.y));
    }

    // 绘制线段（使用Bresenham算法）
    void DrawLine(Vector2Int start, Vector2Int end)
    {
        int thickness = 4; // 走廊厚度

        Vector2Int current = start;
        Vector2Int delta = new Vector2Int(Mathf.Abs(end.x - start.x), Mathf.Abs(end.y - start.y));
        Vector2Int step = new Vector2Int(start.x < end.x ? 1 : -1, start.y < end.y ? 1 : -1);
        int error = delta.x - delta.y;

        while (true)
        {
            // 检查当前点是否在任意房间内
            bool inAnyRoom = false;
            foreach (var room in mainRooms)
            {
                if (current.x >= room.xMin && current.x < room.xMax &&
                    current.y >= room.yMin && current.y < room.yMax)
                {
                    inAnyRoom = true;
                    break;
                }
            }

            // 如果不在房间内，则绘制走廊
            if (!inAnyRoom)
            {
                // 绘制厚度的方块
                for (int x = 0; x < thickness; x++)
                {
                    for (int y = 0; y < thickness; y++)
                    {
                        Vector2Int pos = new Vector2Int(current.x + x, current.y + y);
                        // 确保走廊不会覆盖房间
                        bool posInRoom = false;
                        foreach (var room in mainRooms)
                        {
                            if (pos.x >= room.xMin && pos.x < room.xMax &&
                                pos.y >= room.yMin && pos.y < room.yMax)
                            {
                                posInRoom = true;
                                break;
                            }
                        }

                        if (!posInRoom)
                        {
                            corridorPositions.Add(pos);
                        }
                    }
                }
            }

            if (current == end) break;
            int error2 = error * 2;
            if (error2 > -delta.y) { error -= delta.y; current.x += step.x; }
            if (error2 < delta.x) { error += delta.x; current.y += step.y; }
        }
    }

    // 剔除孤立房间（不与任何主房间连通）
    void CullIsolatedRooms()
    {
        activeRooms.Clear();
        activeRooms.AddRange(mainRooms);
        return; // 保留所有房间，跳过孤立房间剔除逻辑
        // 1. 构建连接图
        Dictionary<Room, List<Room>> adjacency = new Dictionary<Room, List<Room>>();

        // 初始化邻接表
        foreach (var room in allRooms)
        {
            adjacency[room] = new List<Room>();
        }

        // 2. 添加MST连接的主房间
        foreach (var edge in mstEdges)
        {
            adjacency[edge.a].Add(edge.b);
            adjacency[edge.b].Add(edge.a);
        }

        // 3. 添加通过重叠连接的关系
        for (int i = 0; i < allRooms.Count; i++)
        {
            for (int j = i + 1; j < allRooms.Count; j++)
            {
                Room a = allRooms[i];
                Room b = allRooms[j];

                if (a.Overlaps(b)) // 房间重叠也算连接
                {
                    adjacency[a].Add(b);
                    adjacency[b].Add(a);
                }
            }
        }

        // 4. 添加通过走廊直接连接的关系
        foreach (var room in allRooms)
        {
            for (int x = room.xMin; x < room.xMax; x++)
            {
                for (int y = room.yMin; y < room.yMax; y++)
                {
                    if (corridorPositions.Contains(new Vector2Int(x, y)))
                    {
                        // 检查这个走廊位置是否也在其他房间内
                        foreach (var other in allRooms)
                        {
                            if (room.Equals(other)) continue;

                            if (other.xMin <= x && x < other.xMax &&
                                other.yMin <= y && y < other.yMax)
                            {
                                if (!adjacency[room].Contains(other))
                                    adjacency[room].Add(other);
                                if (!adjacency[other].Contains(room))
                                    adjacency[other].Add(room);
                            }
                        }
                    }
                }
            }
        }

        // 5. 从主房间开始广度优先搜索
        HashSet<Room> reachableRooms = new HashSet<Room>();
        Queue<Room> queue = new Queue<Room>();

        // 将所有主房间作为起点
        foreach (var mainRoom in mainRooms)
        {
            reachableRooms.Add(mainRoom);
            queue.Enqueue(mainRoom);
        }

        // 广度优先搜索
        while (queue.Count > 0)
        {
            Room current = queue.Dequeue();

            foreach (var neighbor in adjacency[current])
            {
                if (!reachableRooms.Contains(neighbor))
                {
                    reachableRooms.Add(neighbor);
                    queue.Enqueue(neighbor);
                }
            }
        }

        // 6. 标记孤立房间
        List<Room> roomsToRemove = new List<Room>();
        foreach (var room in allRooms)
        {
            if (!reachableRooms.Contains(room))
            {
                roomsToRemove.Add(room);
            }
        }

        // 7. 移除孤立房间
        foreach (var room in roomsToRemove)
        {
            allRooms.Remove(room);
        }

        // 8. 重新选择主房间
        SelectMainRooms();

        // 9. 设置激活房间
        activeRooms.Clear();
        activeRooms.AddRange(mainRooms);
    }

    // 随机游走算法（使地牢边缘更自然）
    void ApplyRandomWalkLogic()
    {
        if (!useRandomWalk || roomFloorPositions.Count == 0) return;

        List<Vector2Int> floorList = roomFloorPositions.ToList();
        Vector2Int[] directions = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };

        for (int i = 0; i < randomWalkIterations; i++)
        {
            // 随机选择一个现有地板位置作为起点
            Vector2Int currentPos = floorList[random.Next(floorList.Count)];

            for (int step = 0; step < randomWalkSteps; step++)
            {
                // 随机方向移动
                Vector2Int direction = directions[random.Next(directions.Length)];

                // 使用笔触大小4x4
                AddBrushStroke(currentPos, direction, brushSize: 4);

                // 移动到新位置
                currentPos += direction;
            }
        }
    }

    // 新增：添加笔触（绘制4x4的区域）
    void AddBrushStroke(Vector2Int centerPos, Vector2Int direction, int brushSize = 4)
    {
        int halfSize = brushSize / 2;
        Vector2Int brushStart = centerPos - new Vector2Int(halfSize, halfSize);

        for (int dx = 0; dx < brushSize; dx++)
        {
            for (int dy = 0; dy < brushSize; dy++)
            {
                Vector2Int pos = brushStart + new Vector2Int(dx, dy);

                // 检查是否超出边界（可选）
                // 这里可以添加边界检查，但通常不需要，因为会自然限制

                // 将新位置加入地板集合
                if (!roomFloorPositions.Contains(pos))
                {
                    roomFloorPositions.Add(pos);
                }
            }
        }
    }

    // 绘制地牢到Tilemap
    void DrawDungeon()
    {
        tilemap.ClearAllTiles();
        roomFloorPositions.Clear();
        // 2. 添加走廊位置
        foreach (var pos in corridorPositions)
        {
            if (!tilemap.HasTile(new Vector3Int(pos.x, pos.y, 0)))
            {
                tilemap.SetTile(new Vector3Int(pos.x, pos.y, 0), corridorTile != null ? corridorTile : floorTile);
            }
        }
        // 1. 收集房间地板
        foreach (var room in activeRooms)
        {
            int xMin = room.xMin;
            int xMax = room.xMax;
            int yMin = room.yMin;
            int yMax = room.yMax;

            for (int x = xMin; x < xMax; x++)
            {
                for (int y = yMin; y < yMax; y++)
                {
                    Vector2Int pos = new Vector2Int(x, y);
                    roomFloorPositions.Add(pos);

                    // 是否标记房间？
                    if (IsMarkerRoom(room))
                    {
                        tilemap.SetTile(new Vector3Int(x, y, 0), markerTile != null ? markerTile : floorTile);
                    }
                    else
                    {
                        tilemap.SetTile(new Vector3Int(x, y, 0), floorTile);
                    }
                }
            }
            
        }



        // 3. 应用随机游走
        ApplyRandomWalkLogic();

        // 4. 绘制墙壁
        DrawWalls();
    }

    // 绘制墙壁
    void DrawWalls()
    {
        HashSet<Vector2Int> wallPositions = new HashSet<Vector2Int>();

        // 检查每个地板位置周围的8个邻居
        foreach (var pos in roomFloorPositions)
        {
            for (int x = -1; x <= 1; x++)
            {
                for (int y = -1; y <= 1; y++)
                {
                    if (x == 0 && y == 0) continue; // 跳过自身

                    Vector2Int neighborPos = new Vector2Int(pos.x + x, pos.y + y);

                    // 如果邻居位置不是地板，则标记为墙壁
                    if (!roomFloorPositions.Contains(neighborPos))
                    {
                        wallPositions.Add(neighborPos);
                    }
                }
            }
        }

        // 绘制墙壁
        foreach (var pos in wallPositions)
        {
            tilemap.SetTile(new Vector3Int(pos.x, pos.y, 0), wallTile);
        }
    }

    // 更新调试信息
    void UpdateDebugInfo()
    {
        totalRooms = activeRooms.Count;
        mainRoomsCount = mainRooms.Count;
        corridorsCount = corridorPositions.Count;
    }

    // Unity编辑器中的可视化调试
    void OnDrawGizmosSelected()
    {
        if (!Application.isPlaying) return;

        // 绘制所有房间
        Gizmos.color = new Color(0, 1, 0, 0.3f);
        foreach (var room in activeRooms)
        {
            Rect bounds = room.Bounds;
            Gizmos.DrawWireCube(bounds.center, bounds.size);
        }

        // 绘制主房间
        Gizmos.color = new Color(1, 0, 0, 0.5f);
        foreach (var room in mainRooms)
        {
            Rect bounds = room.Bounds;
            Gizmos.DrawWireCube(bounds.center, bounds.size);
        }

        // 绘制走廊连接
        Gizmos.color = Color.yellow;
        foreach (var edge in mstEdges)
        {
            Gizmos.DrawLine(edge.a.center, edge.b.center);
        }
    }
}