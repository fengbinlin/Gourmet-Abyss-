using System.Collections.Generic;
using UnityEngine;

public class SelectManager : MonoBehaviour
{
    private const int EXPECTED_PLAYER_COUNT = 2;

    public static SelectManager Instance { get; private set; }

    [Header("道具配置")]
    public List<BuildingUnit> buildingUnitPrefabs = new List<BuildingUnit>();
    public int spawnCount = 6;
    public Vector2 spawnAreaMin = new Vector2(2, 2);
    public Vector2 spawnAreaMax = new Vector2(18, 18);

    [Header("容器引用")]
    public Transform selectionUnitsRoot;   // 选择阶段的道具父节点
    public Transform placementUnitsRoot;   // 摆放阶段临时父节点

    [Header("日志设置")]
    public bool verboseLog = true;

    private readonly List<SelectionEntry> activeSelectionEntries = new();
    private readonly Dictionary<string, SelectionEntry> playerSelections = new();
    private readonly List<BuildingUnit> persistentPlacedUnits = new();

    private class SelectionEntry
    {
        public BuildingUnit prefab;
        public BuildingUnit selectionInstance;
        public bool IsSelected => selectionInstance == null || selectionInstance.isSelected;
    }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        if (verboseLog)
        {
            Debug.Log($"SelectManager 初始化，预制体数量：{buildingUnitPrefabs.Count}");
        }
    }

    #region Selection Phase

    public void BeginSelectionPhase()
    {
        if (verboseLog)
        {
            Debug.Log("===== SelectManager.BeginSelectionPhase =====");
        }

        EnsureRoots();

        ClearSelectionEntries();
        playerSelections.Clear();

        if (buildingUnitPrefabs.Count == 0)
        {
            Debug.LogError("❌ buildingUnitPrefabs 为空，请在 Inspector 中配置选择用的道具预制体");
            return;
        }

        if (GridManager.Instance == null)
        {
            Debug.LogError("❌ GridManager.Instance 为空，无法计算网格位置");
            return;
        }

        for (int i = 0; i < spawnCount; i++)
        {
            BuildingUnit prefab = GetRandomPrefab();
            if (prefab == null)
            {
                continue;
            }

            Vector2Int randomGridPos = GetRandomGridPosition();
            Vector3 worldPos = GridManager.Instance.GridToWorld(randomGridPos);

            BuildingUnit selectionInstance = Instantiate(prefab, worldPos, Quaternion.identity);
            selectionInstance.name = $"{prefab.name}_Select_{i}";
            selectionInstance.SetOriginPrefab(prefab);
            selectionInstance.ResetForSelection();

            if (selectionUnitsRoot != null)
            {
                selectionInstance.transform.SetParent(selectionUnitsRoot, true);
            }

            activeSelectionEntries.Add(new SelectionEntry
            {
                prefab = prefab,
                selectionInstance = selectionInstance
            });

            if (verboseLog)
            {
                Debug.Log($"✅ 生成选择用道具：{selectionInstance.name} at {randomGridPos}");
            }
        }

        if (verboseLog)
        {
            Debug.Log($"===== 选择阶段准备完毕，本轮共生成 {activeSelectionEntries.Count} 个道具 =====");
        }
    }

    public void TrySelectUnit(string playerID, BuildingUnit unit)
    {
        if (string.IsNullOrEmpty(playerID) || unit == null)
        {
            return;
        }

        if (playerSelections.ContainsKey(playerID))
        {
            if (verboseLog)
            {
                Debug.LogWarning($"⚠️ {playerID} 已选择过道具，忽略重复选择");
            }
            return;
        }

        SelectionEntry entry = FindEntryByInstance(unit);
        if (entry == null)
        {
            Debug.LogWarning($"⚠️ {playerID} 选择的 {unit.name} 不在当前候选列表中");
            return;
        }

        if (entry.IsSelected)
        {
            if (verboseLog)
            {
                Debug.LogWarning($"⚠️ 道具 {unit.name} 已被其他玩家选择");
            }
            return;
        }

        unit.MarkAsSelected(playerID);
        playerSelections[playerID] = entry;

        if (verboseLog)
        {
            Debug.Log($"{playerID} 成功选择 {unit.name}");
        }

        CheckSelectionCompletion();
    }

    private SelectionEntry FindEntryByInstance(BuildingUnit instance)
    {
        foreach (var entry in activeSelectionEntries)
        {
            if (entry.selectionInstance == instance)
            {
                return entry;
            }
        }

        return null;
    }

    private void CheckSelectionCompletion()
    {
        if (playerSelections.Count >= EXPECTED_PLAYER_COUNT)
        {
            if (verboseLog)
            {
                Debug.Log("🎉 所有玩家都已完成选择");
            }

            if (AllGameManager.Instance != null)
            {
                AllGameManager.Instance.SwitchToPlacementPhase();
            }
        }
    }

    private void ClearSelectionEntries()
    {
        foreach (var entry in activeSelectionEntries)
        {
            if (entry.selectionInstance != null)
            {
                // ⚠️ 只销毁未被摆放的建筑，已摆放的建筑保留
                if (!persistentPlacedUnits.Contains(entry.selectionInstance))
                {
                    Destroy(entry.selectionInstance.gameObject);
                    if (verboseLog)
                    {
                        Debug.Log($"🗑️ 销毁未选择的道具: {entry.selectionInstance.name}");
                    }
                }
                else
                {
                    if (verboseLog)
                    {
                        Debug.Log($"✅ 保留已摆放的道具: {entry.selectionInstance.name}");
                    }
                }
            }
        }
        activeSelectionEntries.Clear();
    }

    private BuildingUnit GetRandomPrefab()
    {
        if (buildingUnitPrefabs.Count == 0)
        {
            return null;
        }

        int randomIndex = Random.Range(0, buildingUnitPrefabs.Count);
        return buildingUnitPrefabs[randomIndex];
    }

    private Vector2Int GetRandomGridPosition()
    {
        int x = Random.Range(Mathf.RoundToInt(spawnAreaMin.x), Mathf.RoundToInt(spawnAreaMax.x));
        int y = Random.Range(Mathf.RoundToInt(spawnAreaMin.y), Mathf.RoundToInt(spawnAreaMax.y));
        return new Vector2Int(x, y);
    }

    private void EnsureRoots()
    {
        if (selectionUnitsRoot == null && AllGameManager.Instance != null && AllGameManager.Instance.buildingUnitsContainer != null)
        {
            selectionUnitsRoot = AllGameManager.Instance.buildingUnitsContainer.transform;
        }
    }

    #endregion

    #region Placement Phase

    /// <summary>获取玩家选中的BuildingUnit（不创建副本，直接复用）</summary>
    public Dictionary<string, BuildingUnit> GetSelectedUnitsForPlacement()
    {
        Dictionary<string, BuildingUnit> result = new Dictionary<string, BuildingUnit>();

        foreach (var kvp in playerSelections)
        {
            string playerID = kvp.Key;
            SelectionEntry entry = kvp.Value;

            // 🔧 新逻辑：直接使用选择阶段的实例，不创建副本
            BuildingUnit selectedUnit = entry.selectionInstance;

            if (selectedUnit == null)
            {
                Debug.LogError($"❌ {playerID} 的选中实例丢失");
                continue;
            }

            result[playerID] = selectedUnit;

            if (verboseLog)
            {
                Debug.Log($"✅ {playerID} 的选中道具 {selectedUnit.name} 将用于摆放阶段");
            }
        }

        // 不清除选择记录，保留引用
        return result;
    }

    public void RegisterPlacedUnit(BuildingUnit unit)
    {
        if (unit == null)
        {
            return;
        }

        if (!persistentPlacedUnits.Contains(unit))
        {
            persistentPlacedUnits.Add(unit);
            if (verboseLog)
            {
                Debug.Log($"📦 已将 {unit.name} 记录为场景内永久道具，累计 {persistentPlacedUnits.Count} 个");
            }
        }
    }

    public void EnsurePlacedUnitsVisible()
    {
        foreach (var unit in persistentPlacedUnits)
        {
            if (unit != null)
            {
                unit.gameObject.SetActive(true);
                if (verboseLog)
                {
                    Debug.Log($"👁️ 显示已摆放的道具: {unit.name}");
                }
            }
        }
    }

    /// <summary>隐藏已摆放的建筑（用于选择阶段）</summary>
    public void HidePlacedUnits()
    {
        foreach (var unit in persistentPlacedUnits)
        {
            if (unit != null)
            {

                // 找到所有Tag为"Bullet"的物体
                GameObject[] bullets = GameObject.FindGameObjectsWithTag("Bullet");

                // 遍历并删除每个物体
                foreach (GameObject bullet in bullets)
                {
                    Destroy(bullet);
                }

                unit.gameObject.SetActive(false);
                if (verboseLog)
                {
                    Debug.Log($"🙈 隐藏已摆放的道具: {unit.name}");
                }
            }
        }
    }

    #endregion

    #region Utility

    public void ResetSelections()
    {
        playerSelections.Clear();
        ClearSelectionEntries();
    }

    public void ClearAllPlacedUnits()
    {
        foreach (var unit in persistentPlacedUnits)
        {
            if (unit != null)
            {
                Destroy(unit.gameObject);
            }
        }
        persistentPlacedUnits.Clear();
    }

    #endregion
}
