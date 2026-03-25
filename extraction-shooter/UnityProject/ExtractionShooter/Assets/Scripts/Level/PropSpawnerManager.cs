using System.Collections.Generic;
using UnityEngine;

public class PropSpawnerManager : MonoBehaviour
{
    [Header("关卡参数ID")]
    public string statsId = "default";

    [Header("Spawner管理")]
    [Tooltip("参与抽样的 PropSpawner 列表")]
    public List<PropSpawner> propSpawners = new List<PropSpawner>();

    [Tooltip("会执行一次生成的 Spawner 百分比（0~1）")]
    [Range(0f, 1f)]
    public float spawnSpawnerPercent = 0.5f;

    [Header("运行设置")]
    [Tooltip("开始时自动执行一次生成计划")]
    public bool spawnOnStart = true;

    [Tooltip("至少保证有1个Spawner生成（当列表非空且百分比>0时）")]
    public bool ensureAtLeastOneWhenPositivePercent = true;

    [Header("调试")]
    public bool showDebug = true;

    private void Start()
    {
        if (spawnOnStart)
        {
            ExecuteSpawnPlan();
        }
    }

    /// <summary>
    /// 按百分比随机抽取一部分 Spawner，让它们各自生成一次。
    /// </summary>
    public void ExecuteSpawnPlan()
    {
        List<PropSpawner> available = GetAvailableSpawners();
        if (available.Count == 0)
        {
            if (showDebug) Debug.LogWarning($"{gameObject.name}: 没有可用 PropSpawner。");
            return;
        }

        int total = available.Count;
        float effectivePercent = GetEffectiveSpawnSpawnerPercent();
        int targetCount = Mathf.RoundToInt(total * effectivePercent);
        if (ensureAtLeastOneWhenPositivePercent && effectivePercent > 0f)
        {
            targetCount = Mathf.Max(1, targetCount);
        }
        targetCount = Mathf.Clamp(targetCount, 0, total);

        ShuffleList(available);

        int success = 0;
        for (int i = 0; i < targetCount; i++)
        {
            PropSpawner spawner = available[i];
            if (spawner != null && spawner.SpawnOnceRandomProp())
            {
                success++;
            }
        }

        if (showDebug)
        {
            Debug.Log($"{gameObject.name}: 计划生成 {targetCount}/{total} 个Spawner，实际成功 {success}。概率倍率后Percent={effectivePercent:F2}");
        }
    }

    /// <summary>
    /// 重置所有 Spawner 的“已生成”状态。
    /// </summary>
    public void ResetAllSpawnerStates()
    {
        for (int i = 0; i < propSpawners.Count; i++)
        {
            if (propSpawners[i] != null)
            {
                propSpawners[i].ResetSpawnState();
            }
        }
    }

    private List<PropSpawner> GetAvailableSpawners()
    {
        List<PropSpawner> result = new List<PropSpawner>();
        for (int i = 0; i < propSpawners.Count; i++)
        {
            PropSpawner spawner = propSpawners[i];
            if (spawner == null) continue;
            if (!spawner.isActive) continue;
            if (spawner.HasSpawned) continue;
            result.Add(spawner);
        }
        return result;
    }

    private void ShuffleList<T>(List<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            T tmp = list[i];
            list[i] = list[j];
            list[j] = tmp;
        }
    }

    private float GetEffectiveSpawnSpawnerPercent()
    {
        float rate = 1f;
        if (WeaponStatsManager.Instance != null)
        {
            rate = WeaponStatsManager.Instance.GetPropProbabilityRate(statsId);
        }

        return Mathf.Clamp01(spawnSpawnerPercent * rate);
    }
}

