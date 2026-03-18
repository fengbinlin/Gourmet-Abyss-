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

    // 占用状态：true = 已占用
    private bool[,] occupied;

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
        EnsureOccupiedArray();
    }

    private void OnEnable()
    {
        RegisterSelf();
    }

    private void OnDisable()
    {
        UnregisterSelf();
    }

    private void OnDestroy()
    {
        UnregisterSelf();
        if (Instance == this) Instance = null;
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

    private void OnDrawGizmos()
    {
        // 绘制背景网格
        Gizmos.color = Color.gray;
        for (int x = 0; x <= gridWidth; x++)
        {
            Vector3 from = transform.position + new Vector3(x * cellSize, 0, 0);
            Vector3 to = transform.position + new Vector3(x * cellSize, gridHeight * cellSize, 0);
            Gizmos.DrawLine(from, to);
        }

        for (int y = 0; y <= gridHeight; y++)
        {
            Vector3 from = transform.position + new Vector3(0, y * cellSize, 0);
            Vector3 to = transform.position + new Vector3(gridWidth * cellSize, y * cellSize, 0);
            Gizmos.DrawLine(from, to);
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