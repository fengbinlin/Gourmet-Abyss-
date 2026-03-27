using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 家具背包 UI 管理器（仿照 ItemBagManager，但去掉送礼相关逻辑）
/// </summary>
public class FurnitureUIManager : MonoBehaviour
{
    public static FurnitureUIManager instance;

    [Header("家具背包 UI 配置")]
    public UIAnimatedPanelController panelAnimatedController;
    public GameObject itemPrefab;
    public Transform itemParent;
    public Text informationTitle;
    public Text informationDescription;

    [Header("家中魅力显示")]
    [Tooltip("显示当前家中总魅力值的文本")]
    public Text totalCharmText;

    /// <summary>
    /// 当前家中所有已放置家具的总魅力值
    /// </summary>
    public int TotalCharmValue { get; private set; }

    [Header("魅力值刷新设置")]
    [Tooltip("多久重新统计一次场景中的家具魅力（秒），用于保证数值和场景实时同步")]
    [SerializeField] private float charmRecalcInterval = 0.2f;
    private float charmRecalcTimer = 0f;

    private readonly List<GameObject> currentItems = new List<GameObject>();

    // 当前是否按单一 ResourceKind 过滤显示
    private bool useKindFilter = false;
    private ResourceKind filteredKind;

    private void Awake()
    {
        instance = this;
        // 默认只显示家具类型
        useKindFilter = true;
        filteredKind = ResourceKind.Furniture;
    }

    private void Start()
    {
        GenerateItems();
        // 场景加载时，统计当前场景中已经存在的所有家具魅力值
        RecalculateCharmFromScene();
    }

    private void OnEnable()
    {
        GenerateItems();
        // 重新开启时也做一次同步，避免中途有物体被创建/删除
        RecalculateCharmFromScene();
    }

    private void Update()
    {
        // 周期性重新统计一次场景中的魅力值，确保即使有其他逻辑增减家具、
        // 也能在不需要额外点击的情况下自动刷新 UI。
        charmRecalcTimer += Time.deltaTime;
        if (charmRecalcTimer >= charmRecalcInterval)
        {
            charmRecalcTimer = 0f;
            RecalculateCharmFromScene();
        }
    }

    /// <summary>
    /// 生成（刷新）家具物品列表
    /// </summary>
    public void GenerateItems()
    {
        ClearItems();

        if (GameValManager.Instance == null)
        {
            Debug.LogWarning("GameValManager.Instance 未初始化！");
            return;
        }

        var sortedResources = new List<ResourceItem>(GameValManager.Instance.resources);
        sortedResources.Sort((a, b) =>
        {
            int kindCompare = a.resourceKind.CompareTo(b.resourceKind);
            if (kindCompare != 0) return kindCompare;
            return a.type.CompareTo(b.type);
        });

        foreach (var item in sortedResources)
        {
            // 只显示有数量的资源
            if (item.count == 0) continue;

            // 默认只关心家具，也可以通过 ShowAllTypes 放开
            if (useKindFilter)
            {
                if (item.resourceKind != filteredKind) continue;
            }

            GameObject go = Instantiate(itemPrefab, itemParent);
            ItemPrefabs script = go.GetComponent<ItemPrefabs>();

            if (script != null)
            {
                script.resourceType = item.type;
                script.Icon.sprite = item.Icon;
                script.Amount.text = item.count.ToString();
            }

            // 确保家具格子支持“按下立刻生成并拖拽”
            if (go.GetComponent<FurnitureUIItemDragHandler>() == null)
            {
                go.AddComponent<FurnitureUIItemDragHandler>();
            }

            currentItems.Add(go);
        }
    }

    /// <summary>
    /// 有家具成功放置到家中时调用，增加总魅力
    /// </summary>
    /// <param name="unit">成功放置的建筑单元（家具）</param>
    public void OnFurniturePlaced(BuildingUnit unit)
    {
        if (unit == null) return;

        TotalCharmValue += Mathf.Max(0, unit.charmValue);
        RefreshCharmText();
    }

    /// <summary>
    /// 家具被收回背包（或从场景中移除）时调用，减少总魅力
    /// </summary>
    /// <param name="unit">被收回的建筑单元（家具）</param>
    public void OnFurnitureReturnedToBag(BuildingUnit unit)
    {
        if (unit == null) return;

        TotalCharmValue -= Mathf.Max(0, unit.charmValue);
        if (TotalCharmValue < 0) TotalCharmValue = 0;
        RefreshCharmText();
    }

    private void RefreshCharmText()
    {
        if (totalCharmText != null)
        {
            totalCharmText.text = TotalCharmValue.ToString();
        }
    }

    /// <summary>
    /// 从当前场景中重新统计所有已存在家具的总魅力值。
    /// 适用于开局时场景里本来就摆好的家具。
    /// </summary>
    private void RecalculateCharmFromScene()
    {
        TotalCharmValue = 0;

        // 找到场景中所有 BuildingUnit（默认认为都是已经“在场景中”的家具/建筑）
        BuildingUnit[] units = Object.FindObjectsOfType<BuildingUnit>();
        foreach (var unit in units)
        {
            if (unit == null) continue;

            int v = Mathf.Max(0, unit.charmValue);
            if (v <= 0) continue;

            TotalCharmValue += v;

            // 告诉它的 BuildController：这个家具已经被计入过总魅力，后续移动不要重复加
            BuildController ctrl = unit.GetComponent<BuildController>();
            if (ctrl != null)
            {
                ctrl.MarkCharmCountedInTotal();
            }
        }

        RefreshCharmText();
    }

    /// <summary>
    /// 清空当前 UI 列表
    /// </summary>
    public void ClearItems()
    {
        foreach (var go in currentItems)
        {
            if (go != null) Destroy(go);
        }
        currentItems.Clear();

        for (int i = itemParent.childCount - 1; i >= 0; i--)
        {
            Destroy(itemParent.GetChild(i).gameObject);
        }
    }

    /// <summary>
    /// 设置右侧家具信息展示
    /// </summary>
    public void SetFurnitureInformation(ResourceType resourceType)
    {
        if (GameValManager.Instance == null) return;

        foreach (var item in GameValManager.Instance.resources)
        {
            if (item.type == resourceType)
            {
                if (informationTitle != null) informationTitle.text = item.name;
                if (informationDescription != null) informationDescription.text = item.description;
                break;
            }
        }
    }

    /// <summary>
    /// 只显示家具类型
    /// </summary>
    public void ShowOnlyFurniture()
    {
        ShowOnlyType(ResourceKind.Furniture);
    }

    /// <summary>
    /// 只显示指定 ResourceKind
    /// </summary>
    public void ShowOnlyType(ResourceKind kind)
    {
        useKindFilter = true;
        filteredKind = kind;
        GenerateItems();
    }

    /// <summary>
    /// 显示所有类型（不做过滤）
    /// </summary>
    public void ShowAllTypes()
    {
        useKindFilter = false;
        GenerateItems();
    }

    /// <summary>
    /// 从家具 UI 开始摆放指定类型的家具
    /// </summary>
    public void BeginPlaceFurniture(ResourceType type)
    {
        if (GameValManager.Instance == null)
        {
            Debug.LogWarning("BeginPlaceFurniture 失败：GameValManager.Instance 为空");
            return;
        }

        ResourceItem info = GameValManager.Instance.GetResourceInfo(type);
        if (info == null || info.ItemObject == null)
        {
            Debug.LogWarning($"BeginPlaceFurniture 失败：资源 {type} 缺少 ItemObject 预制体");
            return;
        }

        // 尝试消耗 1 个该家具
        if (!GameValManager.Instance.TryConsumeResource(type, 1))
        {
            Debug.LogWarning($"BeginPlaceFurniture 失败：资源不足 {type}");
            return;
        }

        // 实例化对应的 BuildingUnit 预制体
        GameObject go = Object.Instantiate(info.ItemObject);
        BuildingUnit unit = go.GetComponent<BuildingUnit>();
        BuildController controller = go.GetComponent<BuildController>();

        if (unit == null || controller == null)
        {
            Debug.LogWarning($"BeginPlaceFurniture 失败：预制体 {info.ItemObject.name} 缺少 BuildingUnit 或 BuildController 组件");
            Object.Destroy(go);
            // 退还资源
            GameValManager.Instance.AddResource(type, 1);
            return;
        }

        GridManager gm = GridManager.GetById(unit.gridId);
        if (gm == null) gm = GridManager.Instance;

        if (gm != null)
        {
            // 生成点：优先网格中心；如果中心不可放则向外扩散找最近合法点
            Vector2Int spawnGridPos = FindSpawnGridPos(gm, unit);
            if (spawnGridPos.x < 0)
            {
                Debug.LogWarning($"BeginPlaceFurniture 失败：网格没有可用位置放置 {type}，已退还资源");
                Object.Destroy(go);
                GameValManager.Instance.AddResource(type, 1);
                GenerateItems();
                return;
            }

            // 生成时必须落在“格子基点”（左下角锚点）
            go.transform.position = gm.GridToWorld(spawnGridPos);
        }
        else
        {
            Debug.LogWarning("BeginPlaceFurniture 警告：找不到 GridManager，默认使用世界原点生成");
            go.transform.position = Vector3.zero;
        }

        // 让新生成的建筑立即进入拖拽流程
        controller.BeginDragFromUI(type);

        // 消耗成功后，刷新一次家具列表 UI（数量减少）
        GenerateItems();
    }

    private Vector2Int FindSpawnGridPos(GridManager gm, BuildingUnit unit)
    {
        if (gm == null || unit == null) return new Vector2Int(-1, -1);

        // 让建筑尽量居中（考虑建筑占用尺寸）
        int centerX = Mathf.Clamp((gm.gridWidth - unit.size) / 2, 0, Mathf.Max(0, gm.gridWidth - 1));
        int centerY = Mathf.Clamp((gm.gridHeight - unit.size) / 2, 0, Mathf.Max(0, gm.gridHeight - 1));
        Vector2Int center = new Vector2Int(centerX, centerY);

        if (gm.CanPlace(center, unit)) return center;

        // 向外扩散搜索最近可放点
        int maxRadius = Mathf.Max(gm.gridWidth, gm.gridHeight);
        for (int r = 1; r <= maxRadius; r++)
        {
            // 扫描一个“方环”边界
            for (int dx = -r; dx <= r; dx++)
            {
                for (int dy = -r; dy <= r; dy++)
                {
                    if (Mathf.Abs(dx) != r && Mathf.Abs(dy) != r) continue; // 只取边界

                    Vector2Int p = new Vector2Int(center.x + dx, center.y + dy);
                    if (gm.CanPlace(p, unit)) return p;
                }
            }
        }

        return new Vector2Int(-1, -1);
    }
}

