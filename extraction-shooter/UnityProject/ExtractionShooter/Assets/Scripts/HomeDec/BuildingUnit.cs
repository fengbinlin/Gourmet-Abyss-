using UnityEngine;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif
/// <summary>建筑单元类型枚举</summary>
public enum BuildingUnitType
{
    Platform,      // 平台
    Spring,        // 弹簧
    Spike,         // 尖刺
    MovingPlatform,// 移动平台
    Trampoline,    // 蹦床
    Teleporter,    // 传送门
    Other          // 其他
}

public class BuildingUnit : MonoBehaviour
{
    public List<GameObject> RotateObject;
    [Header("所属网格编号（用于多网格隔离）")]
    [Min(0)]
    public int gridId = 0;

    [Header("单元类型")]
    public BuildingUnitType unitType = BuildingUnitType.Platform;

    [Header("单元格占用设置")]
    public int size = 2; // S*S
    public bool[] occupyMaskFlat; // 一维数组序列化

    [Header("旋转状态")]
    public bool isRotated = false; // 简化：只有两种状态，原始和旋转90°

    [Header("选择状态")]
    public bool isSelected = false; // 是否已被选择
    public string selectedByPlayer = ""; // 被哪个玩家选择

    [HideInInspector]
    public BuildingUnit originPrefab; // 来源预制体引用，用于重新生成摆放阶段实例

    [Header("占用掩码 — Scene 调试")]
    [Tooltip("未选中物体时也在 Scene 中绘制占用预览")]
    public bool showOccupancyInScene = true;
    [Tooltip("在每个格子中心显示「占 / 空」文字（仅编辑器）")]
    public bool showOccupancyCellLabels = false;
    public Color occupyGizmoBoundsColor = new Color(0.25f, 0.55f, 1f, 0.95f);
    public Color occupyGizmoOccupiedFillColor = new Color(0.15f, 0.8f, 0.3f, 0.35f);
    public Color occupyGizmoOccupiedWireColor = new Color(0.1f, 0.65f, 0.2f, 1f);
    public Color occupyGizmoEmptyFillColor = new Color(0.55f, 0.55f, 0.6f, 0.12f);
    public Color occupyGizmoEmptyWireColor = new Color(0.45f, 0.45f, 0.5f, 0.65f);

    [Header("视觉反馈")]
    public Color normalColor = Color.white;
    public Color selectedColor = Color.yellow;
    private SpriteRenderer[] spriteRenderers;

    [Header("魅力值设置")]
    [Tooltip("该建筑/家具提供的魅力值，用于计算家中总魅力")]
    public int charmValue = 0;

    [Header("旋转模式")]
    public RotationMode rotationMode = RotationMode.Rotate90;
    // 记录该单元在Grid中的基础位置
    private Vector2Int gridBasePosition;

    private GridManager cachedGridManager;

    /// <summary>
    /// 解析当前 <see cref="gridId"/> 对应的网格；cellSize / 世界坐标均应以该对象为准。
    /// </summary>
    private GridManager GetGridManager()
    {
        if (cachedGridManager == null || cachedGridManager.gridId != gridId)
            cachedGridManager = null;

        if (cachedGridManager != null)
            return cachedGridManager;

        GridManager gm = GridManager.GetById(gridId);
        if (gm != null)
        {
            cachedGridManager = gm;
            return cachedGridManager;
        }

#if UNITY_EDITOR
        // 编辑模式下尚未 Awake/注册时，GetById 可能为空，按编号在场景中查找以正确取 cellSize
        if (!Application.isPlaying)
        {
            GridManager[] inScene = Object.FindObjectsByType<GridManager>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < inScene.Length; i++)
            {
                if (inScene[i] != null && inScene[i].gridId == gridId)
                {
                    cachedGridManager = inScene[i];
                    return cachedGridManager;
                }
            }
        }
#endif

        // 仅当默认实例与当前 gridId 一致时使用，避免误用其他网格的 cellSize
        if (GridManager.Instance != null && GridManager.Instance.gridId == gridId)
        {
            cachedGridManager = GridManager.Instance;
            return cachedGridManager;
        }

        return null;
    }

    public bool canRotate = true;
    private void Start()
    {
        // 获取所有子对象的SpriteRenderer
        spriteRenderers = GetComponentsInChildren<SpriteRenderer>();

        // 计算并记录在Grid中的基础位置
        GridManager gm = GetGridManager();
        if (gm != null)
        {
            gridBasePosition = gm.WorldToGrid(transform.position);
        }
    }


    public void OnValidate()
    {
        // 确保数组大小正确
        if (occupyMaskFlat == null || occupyMaskFlat.Length != size * size)
        {
            occupyMaskFlat = new bool[size * size];
            for (int i = 0; i < occupyMaskFlat.Length; i++)
                occupyMaskFlat[i] = true; // 默认全部占用
        }
    }

    /// <summary>标记为已选择</summary>
    public void MarkAsSelected(string playerID)
    {
        isSelected = true;
        selectedByPlayer = playerID;

        // 视觉反馈：隐藏GameObject
        gameObject.SetActive(false);

        Debug.Log($"{gameObject.name} 被 {playerID} 选择，已隐藏");
    }

    /// <summary>取消选择（如果需要重置）</summary>
    public void Unselect()
    {
        isSelected = false;
        selectedByPlayer = "";

        // 重新显示GameObject
        gameObject.SetActive(true);
        UpdateVisual();
    }

    /// <summary>准备为选择阶段生成的实例</summary>
    public void ResetForSelection()
    {
        isSelected = false;
        selectedByPlayer = "";
        isRotated = false;
        transform.rotation = Quaternion.identity;
        gameObject.SetActive(true);
        UpdateVisual();
    }

    /// <summary>准备为摆放阶段生成的实例</summary>
    public void ResetForPlacement()
    {
        isSelected = false;
        selectedByPlayer = "";
        isRotated = false;
        transform.rotation = Quaternion.identity;
        UpdateVisual();
    }

    public void SetOriginPrefab(BuildingUnit prefab)
    {
        originPrefab = prefab;
    }

    public BuildingUnit GetOriginPrefab()
    {
        return originPrefab != null ? originPrefab : this;
    }

    /// <summary>更新视觉效果</summary>
    private void UpdateVisual()
    {
        if (spriteRenderers == null || spriteRenderers.Length == 0)
            return;

        Color targetColor = isSelected ? selectedColor : normalColor;
        foreach (var renderer in spriteRenderers)
        {
            if (renderer != null)
            {
                renderer.color = targetColor;
            }
        }
    }

    /// <summary>检查指定Grid位置是否在这个BuildingUnit的占用范围内</summary>
    public bool IsGridPositionInUnit(Vector2Int gridPos)
    {
        // 更新基础位置（以防单元被移动）
        GridManager gm = GetGridManager();
        if (gm != null)
        {
            gridBasePosition = gm.WorldToGrid(transform.position);
        }

        // 计算相对位置
        int relX = gridPos.x - gridBasePosition.x;
        int relY = gridPos.y - gridBasePosition.y;

        // 检查是否在范围内
        if (relX < 0 || relX >= size || relY < 0 || relY >= size)
            return false;

        // 检查是否被该单元占用
        return GetOccupy(relX, relY);
    }

    /// <summary>获取单元类型</summary>
    public BuildingUnitType GetUnitType()
    {
        return unitType;
    }

    /// <summary>获取Grid基础位置</summary>
    public Vector2Int GetGridBasePosition()
    {
        GridManager gm = GetGridManager();
        if (gm != null)
        {
            gridBasePosition = gm.WorldToGrid(transform.position);
        }
        return gridBasePosition;
    }

    /// <summary>根据当前旋转状态获取某格是否占用</summary>
    public bool GetOccupy(int x, int y)
    {

        if (!isRotated)
        {
            // 原始方向
            return occupyMaskFlat[y * size + x];
        }
        else
        {
            if (rotationMode == RotationMode.Rotate90)
            {
                // 旋转90度
                int rotatedX = y;
                int rotatedY = size - 1 - x;
                return occupyMaskFlat[rotatedY * size + rotatedX];
            }
            else if (rotationMode == RotationMode.MirrorHorizontal)
            {
                // 水平镜像翻转
                int mirroredX = size - 1 - x;
                int mirroredY = y;
                return occupyMaskFlat[mirroredY * size + mirroredX];
            }
        }
        return false;
    }

    /// <summary>设置某格占用（基于原始方向）</summary>
    public void SetOccupy(int x, int y, bool value)
    {
        occupyMaskFlat[y * size + x] = value;
    }
    public void ToggleRotationMaskOnly()
    {
        if (canRotate)
        {
            isRotated = !isRotated;
            if (isRotated)
            {
                RotateObject[1].SetActive(true);
                RotateObject[0].SetActive(false);
            }
            else
            {
                RotateObject[0].SetActive(true);
                RotateObject[1].SetActive(false);
            }
            Debug.Log($"旋转切换（仅掩码），当前状态: {(isRotated ? "旋转90°" : "原始方向")}");
        }

    }
    /// <summary>切换旋转状态</summary>
    /// <summary>切换旋转状态</summary>
    /// <summary>切换旋转状态（只改变视觉和内部状态，不更新GridManager）</summary>
    public void ToggleRotation()
    {
        // 记录旋转前的世界位置
        Vector3 prevWorldPos = transform.position;

        // 切换旋转状态
        isRotated = !isRotated;

        // 更新视觉旋转
        transform.rotation = Quaternion.Euler(0, 0, isRotated ? -90f : 0f);

        // 补偿旋转带来的位置变化，保持左下角锚点稳定
        if (isRotated)
        {
            // 旋转到90度：位置需要向上移动 (size-1)个单元格
            GridManager gm = GetGridManager();
            float cs = gm != null ? gm.cellSize : 1f;
            transform.position = prevWorldPos + new Vector3(0, (size - 1) * cs, 0);
        }
        else
        {
            // 旋转回0度：位置需要向下移动 (size-1)个单元格
            GridManager gm = GetGridManager();
            float cs = gm != null ? gm.cellSize : 1f;
            transform.position = prevWorldPos - new Vector3(0, (size - 1) * cs, 0);
        }

        Debug.Log($"旋转切换，当前状态: {(isRotated ? "旋转90°" : "原始方向")}");
    }
    /// <summary>强制更新网格占用状态（用于外部同步）</summary>
    public void RefreshGridOccupation()
    {
        GridManager gm = GetGridManager();
        if (gm == null) return;

        Vector2Int gridPos = GetGridBasePosition();

        // 清除可能存在的旧状态
        for (int x = 0; x < size; x++)
        {
            for (int y = 0; y < size; y++)
            {
                Vector2Int cellPos = new Vector2Int(gridPos.x + x, gridPos.y + y);
                gm.SetCellOccupied(cellPos, false);
            }
        }

        // 设置新状态
        for (int x = 0; x < size; x++)
        {
            for (int y = 0; y < size; y++)
            {
                if (!GetOccupy(x, y)) continue;
                Vector2Int cellPos = new Vector2Int(gridPos.x + x, gridPos.y + y);
                gm.SetCellOccupied(cellPos, true);
            }
        }
    }
    /// <summary>获取当前旋转后的占用网格尺寸（考虑锚点）</summary>
    public Vector2Int GetRotatedSize()
    {
        return isRotated ? new Vector2Int(size, size) : new Vector2Int(size, size);
        // 如果是矩形建筑，这里会返回交换后的尺寸，但你的建筑是方形的，所以尺寸不变
    }

    private void OnDrawGizmos()
    {
        if (!showOccupancyInScene)
            return;
        DrawOccupancyGizmos(false);
    }

    private void OnDrawGizmosSelected()
    {
        // 与 OnDrawGizmos 二选一，避免选中时重复叠加绘制
        if (showOccupancyInScene)
            return;
        DrawOccupancyGizmos(true);
    }

    private void DrawOccupancyGizmos(bool selected)
    {
        if (occupyMaskFlat == null || occupyMaskFlat.Length != size * size || size <= 0)
            return;

        GridManager gm = GetGridManager();
        float cs = gm != null ? gm.cellSize : 1f;
        Vector3 half = new Vector3(cs * 0.5f, cs * 0.5f, 0f);
        float fillScale = selected ? 0.88f : 0.82f;

        // 整体 S×S 范围外框（与当前旋转/镜像下的格子布局一致）
        Gizmos.color = occupyGizmoBoundsColor;
        Vector3 c00 = transform.position + GetCellWorldOffset(0, 0);
        Vector3 c10 = transform.position + GetCellWorldOffset(size - 1, 0);
        Vector3 c11 = transform.position + GetCellWorldOffset(size - 1, size - 1);
        Vector3 c01 = transform.position + GetCellWorldOffset(0, size - 1);
        Gizmos.DrawLine(c00 + half, c10 + half);
        Gizmos.DrawLine(c10 + half, c11 + half);
        Gizmos.DrawLine(c11 + half, c01 + half);
        Gizmos.DrawLine(c01 + half, c00 + half);

        for (int x = 0; x < size; x++)
        {
            for (int y = 0; y < size; y++)
            {
                bool occupies = GetOccupy(x, y);
                Vector3 cellCorner = transform.position + GetCellWorldOffset(x, y);
                Vector3 center = cellCorner + half;

                if (occupies)
                {
                    Gizmos.color = occupyGizmoOccupiedFillColor;
                    Gizmos.DrawCube(center, Vector3.one * (cs * fillScale));
                    Gizmos.color = occupyGizmoOccupiedWireColor;
                }
                else
                {
                    Gizmos.color = occupyGizmoEmptyFillColor;
                    Gizmos.DrawCube(center, Vector3.one * (cs * fillScale * 0.85f));
                    Gizmos.color = occupyGizmoEmptyWireColor;
                }

                Gizmos.DrawWireCube(center, Vector3.one * (cs * fillScale));

#if UNITY_EDITOR
                if (showOccupancyCellLabels)
                {
                    var style = new GUIStyle();
                    style.normal.textColor = occupies ? occupyGizmoOccupiedWireColor : occupyGizmoEmptyWireColor;
                    style.fontSize = 11;
                    style.fontStyle = FontStyle.Bold;
                    style.alignment = TextAnchor.MiddleCenter;
                    Handles.Label(center, occupies ? "占" : "空", style);
                }
#endif
            }
        }
    }

    /// <summary>获取单元格在世界空间中的偏移（考虑旋转和锚点）</summary>
    /// <summary>获取单元格在世界空间中的偏移（考虑旋转和锚点）</summary>
    public Vector3 GetCellWorldOffset(int x, int y)
    {
        GridManager gm = GetGridManager();
        float cs = gm != null ? gm.cellSize : 1f;
        if (!isRotated)
            return new Vector3(x * cs, y * cs, 0);

        if (rotationMode == RotationMode.Rotate90)
        {
            float offsetX = y * cs;
            float offsetY = (size - 1 - x) * cs;
            return new Vector3(offsetX, offsetY, 0);
        }

        // MirrorHorizontal：世界空间仍按 (x,y) 铺格，仅掩码在 X 方向镜像
        return new Vector3(x * cs, y * cs, 0);
    }
}