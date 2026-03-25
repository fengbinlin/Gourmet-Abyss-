using UnityEngine;
using System.Collections.Generic;

public class GridManager : MonoBehaviour
{
    [Header("网格编号（用于多网格隔离）")]
    [Min(0)]
    public int gridId = 0;

    [Header("网格尺寸")]
    public int gridWidth = 30;    // N
    public int gridHeight = 30;   // N
    public float cellSize = 1f;

    [SerializeField, HideInInspector]
    private bool[] blockPlacementMaskFlat;
    [SerializeField, HideInInspector] private int _blockMaskSerializedWidth;
    [SerializeField, HideInInspector] private int _blockMaskSerializedHeight;

    [Header("Game 视图 — 运行时网格线")]
    [Tooltip("允许通过 SetGameViewGridVisible 在 Game 视图画网格（Scene 仍用 Gizmos）")]
    public bool allowGameViewGridDraw = true;
    public Color gameViewGridColor = new Color(0.55f, 0.55f, 0.6f, 0.75f);
    [Tooltip("世界空间线宽（正交相机下各向一致）；Mesh 线模式约 1 像素易粗细不一，故用 LineRenderer")]
    [Min(0.0001f)]
    public float gameViewLineWidthWorld = 0.04f;
    [Tooltip("2D 排序，数值越小越靠后")]
    public int gameViewGridSortingOrder = -100;
    [Tooltip("Game 网格显示时的 Alpha 正弦波动")]
    public bool gameViewAlphaPulseEnabled = true;
    public float gameViewPulseSpeed = 2.2f;
    [Range(0f, 1f)] public float gameViewPulseAlphaMin = 0.35f;
    [Range(0f, 1f)] public float gameViewPulseAlphaMax = 0.92f;

    // 占用状态：true = 已占用
    private bool[,] occupied;

    private GameObject gameViewGridRoot;
    private readonly List<LineRenderer> gameViewLineRenderers = new List<LineRenderer>();
    private Material gameViewGridMaterial;
    private bool gameViewAlphaPulseActive;

    public static GridManager Instance { get; private set; }
    private static readonly Dictionary<int, GridManager> InstancesById = new Dictionary<int, GridManager>();

    public static GridManager GetById(int id)
    {
        InstancesById.TryGetValue(id, out var gm);
        return gm;
    }

    private void Awake()
    {
        RegisterSelf();
        EnsureBlockMaskArray();
        EnsureOccupiedArray();
    }

    private void OnValidate()
    {
        EnsureBlockMaskArray();
    }

    /// <summary>供自定义 Inspector 在改宽高后同步数组尺寸（含尽量保留已有格）。</summary>
    public void SyncBlockMaskSize()
    {
        EnsureBlockMaskArray();
    }

    private void OnEnable()
    {
        RegisterSelf();
    }

    private void Update()
    {
        if (!gameViewAlphaPulseActive || !gameViewAlphaPulseEnabled || gameViewGridMaterial == null)
            return;
        if (gameViewGridRoot == null || !gameViewGridRoot.activeInHierarchy)
            return;
        RefreshGameViewGridLineColor();
    }

    private void LateUpdate()
    {
        // 与 OnDrawGizmos 一致：格线端点为世界轴对齐的 position + (i * cellSize)，与 lossyScale 无关
        if (gameViewGridRoot != null && gameViewGridRoot.activeInHierarchy && transform.hasChanged)
        {
            transform.hasChanged = false;
            RebuildGameViewGridLines();
        }
    }

    private void OnDisable()
    {
        UnregisterSelf();
    }

    private void OnDestroy()
    {
        UnregisterSelf();
        if (Instance == this) Instance = null;

        if (gameViewGridMaterial != null)
        {
            if (Application.isPlaying)
                Destroy(gameViewGridMaterial);
            else
                DestroyImmediate(gameViewGridMaterial);
        }

        if (gameViewGridRoot != null)
        {
            if (Application.isPlaying)
                Destroy(gameViewGridRoot);
            else
                DestroyImmediate(gameViewGridRoot);
        }
    }

    private void RegisterSelf()
    {
        if (InstancesById.TryGetValue(gridId, out var existing) && existing != null && existing != this)
        {
            Debug.LogWarning($"⚠️ GridManager gridId={gridId} 已存在（{existing.name}），当前实例 {name} 将覆盖注册");
        }
        InstancesById[gridId] = this;

        // 兼容旧逻辑：如果还没有默认Instance，则用第一个注册的作为默认
        if (Instance == null) Instance = this;
    }

    private void UnregisterSelf()
    {
        if (InstancesById.TryGetValue(gridId, out var existing) && existing == this)
        {
            InstancesById.Remove(gridId);
        }
    }

    private void EnsureBlockMaskArray()
    {
        int w = Mathf.Max(1, gridWidth);
        int h = Mathf.Max(1, gridHeight);

        if (_blockMaskSerializedWidth <= 0) _blockMaskSerializedWidth = w;
        if (_blockMaskSerializedHeight <= 0) _blockMaskSerializedHeight = h;

        if (blockPlacementMaskFlat != null &&
            blockPlacementMaskFlat.Length == w * h &&
            _blockMaskSerializedWidth == w &&
            _blockMaskSerializedHeight == h)
            return;

        bool[] old = blockPlacementMaskFlat;
        int ow = _blockMaskSerializedWidth;
        int oh = _blockMaskSerializedHeight;
        blockPlacementMaskFlat = new bool[w * h];
        if (old != null && ow > 0 && oh > 0)
        {
            for (int y = 0; y < Mathf.Min(oh, h); y++)
            {
                for (int x = 0; x < Mathf.Min(ow, w); x++)
                    blockPlacementMaskFlat[y * w + x] = old[y * ow + x];
            }
        }

        _blockMaskSerializedWidth = w;
        _blockMaskSerializedHeight = h;
    }

    /// <summary>该格是否被地图预设为禁止摆放（不含动态占用）。</summary>
    public bool IsCellPlacementBlocked(Vector2Int gridPos)
    {
        EnsureBlockMaskArray();
        if (gridPos.x < 0 || gridPos.x >= gridWidth || gridPos.y < 0 || gridPos.y >= gridHeight)
            return true;
        return blockPlacementMaskFlat[gridPos.y * gridWidth + gridPos.x];
    }

    /// <summary>确保occupied数组与当前宽高一致</summary>
    private void EnsureOccupiedArray()
    {
        if (gridWidth <= 0) gridWidth = 1;
        if (gridHeight <= 0) gridHeight = 1;

        if (occupied == null ||
            occupied.GetLength(0) != gridWidth ||
            occupied.GetLength(1) != gridHeight)
        {
            occupied = new bool[gridWidth, gridHeight];
        }
    }

    /// <summary>将此GridManager设为当前激活实例</summary>
    public void ActivateInstance(bool resetOccupied = false)
    {
        Instance = this;

        if (resetOccupied)
        {
            occupied = null;
        }

        EnsureOccupiedArray();

        Debug.Log($"✅ GridManager 已激活: {name} -> 尺寸 {gridWidth} x {gridHeight}");
    }

    /// <summary>
    /// 在 Game 视图中显示/隐藏本网格线（运行时 Mesh，非 Gizmo）。
    /// </summary>
    public void SetGameViewGridVisible(bool visible)
    {
        if (!allowGameViewGridDraw)
        {
            visible = false;
        }

        if (!visible)
        {
            gameViewAlphaPulseActive = false;
            if (gameViewGridRoot != null)
                gameViewGridRoot.SetActive(false);
            return;
        }

        EnsureGameViewGridResources();
        gameViewAlphaPulseActive = true;
        RebuildGameViewGridLines();
        if (gameViewGridRoot != null)
            gameViewGridRoot.SetActive(true);
    }

    private void EnsureGameViewGridResources()
    {
        if (gameViewGridRoot != null)
            return;

        gameViewGridRoot = new GameObject("GameViewGridLines");
        gameViewGridRoot.transform.SetParent(transform, false);
        gameViewGridRoot.transform.localPosition = Vector3.zero;
        gameViewGridRoot.transform.localRotation = Quaternion.identity;
        gameViewGridRoot.transform.localScale = Vector3.one;

        gameViewGridMaterial = CreateGameViewGridMaterial();
    }

    private static Material CreateGameViewGridMaterial()
    {
        // 2D 项目优先 Sprites/Default，线框 Mesh 在 Game 视图兼容性较好
        Shader s = Shader.Find("Sprites/Default");
        if (s == null) s = Shader.Find("Universal Render Pipeline/Unlit");
        if (s == null) s = Shader.Find("Unlit/Color");
        var mat = new Material(s);
        mat.renderQueue = 3200;
        return mat;
    }

    private void ApplyGameViewGridMaterialColor(Color c)
    {
        if (gameViewGridMaterial == null) return;
        if (gameViewGridMaterial.HasProperty("_BaseColor"))
            gameViewGridMaterial.SetColor("_BaseColor", c);
        else if (gameViewGridMaterial.HasProperty("_Color"))
            gameViewGridMaterial.SetColor("_Color", c);
        else
            gameViewGridMaterial.color = c;
    }

    /// <summary>拖拽显示网格时：Alpha 波动；否则使用 Inspector 基准色。</summary>
    private void RefreshGameViewGridLineColor()
    {
        if (gameViewGridMaterial == null) return;
        if (gameViewAlphaPulseActive && gameViewAlphaPulseEnabled)
        {
            float w = Mathf.Sin(Time.time * gameViewPulseSpeed) * 0.5f + 0.5f;
            Color c = gameViewGridColor;
            c.a = Mathf.Lerp(gameViewPulseAlphaMin, gameViewPulseAlphaMax, w);
            ApplyGameViewGridMaterialColor(c);
        }
        else
            ApplyGameViewGridMaterialColor(gameViewGridColor);
    }

    /// <summary>
    /// Game 视图：与 Scene Gizmo 相同的世界端点；用 LineRenderer 固定世界线宽，避免 MeshTopology.Lines 约 1px Raster 导致粗细不一。
    /// </summary>
    private void RebuildGameViewGridLines()
    {
        if (gameViewGridRoot == null || gameViewGridMaterial == null) return;

        int w = Mathf.Max(1, gridWidth);
        int h = Mathf.Max(1, gridHeight);
        float cs = cellSize;
        Vector3 o = transform.position;

        int lineCount = (w + 1) + (h + 1);
        EnsureGameViewLineRendererCount(lineCount);

        int idx = 0;
        for (int x = 0; x <= w; x++)
        {
            Vector3 worldA = o + new Vector3(x * cs, 0f, 0f);
            Vector3 worldB = o + new Vector3(x * cs, h * cs, 0f);
            ApplyLineSegment(gameViewLineRenderers[idx++], worldA, worldB);
        }

        for (int y = 0; y <= h; y++)
        {
            Vector3 worldA = o + new Vector3(0f, y * cs, 0f);
            Vector3 worldB = o + new Vector3(w * cs, y * cs, 0f);
            ApplyLineSegment(gameViewLineRenderers[idx++], worldA, worldB);
        }

        RefreshGameViewGridLineColor();
    }

    private void EnsureGameViewLineRendererCount(int count)
    {
        while (gameViewLineRenderers.Count < count)
        {
            var go = new GameObject($"GridLine_{gameViewLineRenderers.Count}");
            go.transform.SetParent(gameViewGridRoot.transform, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;

            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.loop = false;
            lr.numCapVertices = 0;
            lr.numCornerVertices = 0;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            lr.alignment = LineAlignment.TransformZ;
            lr.textureMode = LineTextureMode.Stretch;
            lr.positionCount = 2;
            lr.material = gameViewGridMaterial;
            gameViewLineRenderers.Add(lr);
        }

        while (gameViewLineRenderers.Count > count)
        {
            int last = gameViewLineRenderers.Count - 1;
            LineRenderer lr = gameViewLineRenderers[last];
            gameViewLineRenderers.RemoveAt(last);
            if (lr != null)
            {
                if (Application.isPlaying)
                    Destroy(lr.gameObject);
                else
                    DestroyImmediate(lr.gameObject);
            }
        }
    }

    private void ApplyLineSegment(LineRenderer lr, Vector3 worldA, Vector3 worldB)
    {
        if (lr == null) return;
        lr.startWidth = gameViewLineWidthWorld;
        lr.endWidth = gameViewLineWidthWorld;
        lr.sortingOrder = gameViewGridSortingOrder;
        lr.SetPosition(0, worldA);
        lr.SetPosition(1, worldB);
        lr.enabled = true;
    }

    private void OnDrawGizmos()
    {
        // 绘制背景网格（与 RebuildGameViewGridLines / BuildingUnit 掩码：同一世界步长 cellSize）
        Gizmos.color = Color.gray;
        Vector3 o = transform.position;
        float cs = cellSize;
        int gw = gridWidth;
        int gh = gridHeight;
        for (int x = 0; x <= gw; x++)
        {
            Vector3 from = o + new Vector3(x * cs, 0f, 0f);
            Vector3 to = o + new Vector3(x * cs, gh * cs, 0f);
            Gizmos.DrawLine(from, to);
        }

        for (int y = 0; y <= gh; y++)
        {
            Vector3 from = o + new Vector3(0f, y * cs, 0f);
            Vector3 to = o + new Vector3(gw * cs, y * cs, 0f);
            Gizmos.DrawLine(from, to);
        }

        // 预设禁摆格（编辑器）
        if (blockPlacementMaskFlat != null && blockPlacementMaskFlat.Length == gw * gh)
        {
            Gizmos.color = new Color(1f, 0.45f, 0.1f, 0.42f);
            for (int x = 0; x < gw; x++)
            {
                for (int y = 0; y < gh; y++)
                {
                    if (!blockPlacementMaskFlat[y * gw + x]) continue;
                    Vector3 pos = GridToWorld(new Vector2Int(x, y)) + new Vector3(cellSize / 2, cellSize / 2, 0);
                    Gizmos.DrawCube(pos, Vector3.one * cellSize * 0.92f);
                }
            }
        }

        // 绘制当前被占用格子（用于调试）
        if (occupied != null)
        {
            Gizmos.color = new Color(1f, 0f, 0f, 0.3f); // 半透明红色
            for (int x = 0; x < gridWidth; x++)
            {
                for (int y = 0; y < gridHeight; y++)
                {
                    if (occupied[x, y])
                    {
                        Vector3 pos = GridToWorld(new Vector2Int(x, y)) + new Vector3(cellSize / 2, cellSize / 2, 0);
                        Gizmos.DrawCube(pos, Vector3.one * cellSize * 0.9f);
                    }
                }
            }
        }
    }

    public Vector2Int WorldToGrid(Vector3 worldPos)
    {
        Vector3 local = worldPos - transform.position;
        int x = Mathf.FloorToInt(local.x / cellSize);
        int y = Mathf.FloorToInt(local.y / cellSize);
        return new Vector2Int(x, y);
    }

    public Vector3 GridToWorld(Vector2Int gridPos)
    {
        return transform.position + new Vector3(gridPos.x * cellSize, gridPos.y * cellSize, 0);
    }

    public bool IsCellOccupied(Vector2Int gridPos)
    {
        EnsureOccupiedArray();

        // 越界视为已占用（不可摆放）
        if (gridPos.x < 0 || gridPos.x >= gridWidth || gridPos.y < 0 || gridPos.y >= gridHeight)
            return true;
        return occupied[gridPos.x, gridPos.y];
    }

    public void SetCellOccupied(Vector2Int gridPos, bool state)
    {
        EnsureOccupiedArray();

        if (gridPos.x < 0 || gridPos.x >= gridWidth || gridPos.y < 0 || gridPos.y >= gridHeight)
            return;
        occupied[gridPos.x, gridPos.y] = state;
    }

    /// <summary>
    /// 检查某建筑能否放在 basePos（格子坐标）的位置
    /// </summary>
    public bool CanPlace(Vector2Int basePos, BuildingUnit unit)
    {
        EnsureOccupiedArray();

        for (int x = 0; x < unit.size; x++)
        {
            for (int y = 0; y < unit.size; y++)
            {
                if (!unit.GetOccupy(x, y)) continue; // 不占用的格子跳过

                Vector2Int cellPos = new Vector2Int(basePos.x + x, basePos.y + y);
                if (IsCellPlacementBlocked(cellPos))
                    return false;
                if (IsCellOccupied(cellPos))
                {
                    return false; // 有一个格子被占用就不能放置
                }
            }
        }
        return true;
    }

    /// <summary>
    /// 将建筑的占用格子记录到地图占用状态
    /// </summary>
    public void PlaceUnit(Vector2Int basePos, BuildingUnit unit)
    {
        EnsureOccupiedArray();

        for (int x = 0; x < unit.size; x++)
        {
            for (int y = 0; y < unit.size; y++)
            {
                if (!unit.GetOccupy(x, y)) continue;
                Vector2Int cellPos = new Vector2Int(basePos.x + x, basePos.y + y);
                SetCellOccupied(cellPos, true);
            }
        }
    }

    /// <summary>
    /// 将建筑在旧位置的占用格子释放
    /// </summary>
    public void RemoveUnit(Vector2Int basePos, BuildingUnit unit)
    {
        EnsureOccupiedArray();

        for (int x = 0; x < unit.size; x++)
        {
            for (int y = 0; y < unit.size; y++)
            {
                if (!unit.GetOccupy(x, y)) continue;
                Vector2Int cellPos = new Vector2Int(basePos.x + x, basePos.y + y);
                SetCellOccupied(cellPos, false);
            }
        }
    }
}