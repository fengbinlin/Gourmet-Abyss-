using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class PropSpawnConfig
{
    [Header("基础设置")]
    [Tooltip("道具预制体")]
    public GameObject propPrefab;

    [Tooltip("该道具被选中的权重（>0 有效）")]
    [Range(0f, 1f)]
    public float spawnProbability = 0.5f;

    [Header("随机设置")]
    [Tooltip("是否随机大小")]
    public bool randomSize = false;

    [Tooltip("最小大小比例")]
    [Range(0.1f, 3f)]
    public float minSizeScale = 0.9f;

    [Tooltip("最大大小比例")]
    [Range(0.1f, 3f)]
    public float maxSizeScale = 1.1f;

    [Tooltip("是否随机Y轴旋转")]
    public bool randomRotation = true;
}

public class PropSpawner : MonoBehaviour
{
    [Header("道具生成配置")]
    [Tooltip("候选道具列表（将从中随机选择一个）")]
    public List<PropSpawnConfig> spawnConfigs = new List<PropSpawnConfig>();

    [Header("生成位置设置")]
    [Tooltip("生成半径（以当前点为中心，在XZ平面随机）")]
    public float spawnRadius = 2f;

    [Header("调试")]
    public bool isActive = true;
    public bool showDebug = true;

    [Tooltip("运行时：是否已经完成过一次生成")]
    [SerializeField] private bool hasSpawned = false;

    public bool HasSpawned => hasSpawned;

    /// <summary>
    /// 从候选列表按权重随机一个道具并生成（仅一次）。
    /// </summary>
    public bool SpawnOnceRandomProp()
    {
        if (!isActive || hasSpawned) return false;

        PropSpawnConfig selected = SelectConfigByProbability();
        if (selected == null || selected.propPrefab == null)
        {
            if (showDebug) Debug.LogWarning($"{gameObject.name}: 没有可用道具配置，跳过生成。");
            return false;
        }

        SpawnSpecificProp(selected);
        hasSpawned = true;
        return true;
    }

    /// <summary>
    /// 重置生成状态（仅重置标记，不自动清场景中的道具实例）。
    /// </summary>
    public void ResetSpawnState()
    {
        hasSpawned = false;
    }

    private PropSpawnConfig SelectConfigByProbability()
    {
        List<PropSpawnConfig> available = new List<PropSpawnConfig>();
        float totalWeight = 0f;

        for (int i = 0; i < spawnConfigs.Count; i++)
        {
            PropSpawnConfig config = spawnConfigs[i];
            if (config == null || config.propPrefab == null) continue;

            float weight = Mathf.Max(0f, config.spawnProbability);
            available.Add(config);
            totalWeight += weight;
        }

        if (available.Count == 0) return null;

        // 如果所有权重都为0，等概率选一个
        if (totalWeight <= 0f)
        {
            int idx = Random.Range(0, available.Count);
            return available[idx];
        }

        float randomValue = Random.Range(0f, totalWeight);
        float cumulative = 0f;

        for (int i = 0; i < available.Count; i++)
        {
            cumulative += Mathf.Max(0f, available[i].spawnProbability);
            if (randomValue <= cumulative)
            {
                return available[i];
            }
        }

        return available[available.Count - 1];
    }

    private void SpawnSpecificProp(PropSpawnConfig config)
    {
        Vector3 spawnPosition = GetRandomSpawnPosition();
        Transform parent = transform.root;

        GameObject prop = Instantiate(config.propPrefab, spawnPosition, Quaternion.identity, parent);

        if (config.randomSize && config.minSizeScale <= config.maxSizeScale)
        {
            float randomScale = Random.Range(config.minSizeScale, config.maxSizeScale);
            prop.transform.localScale = new Vector3(randomScale, randomScale, randomScale);
        }

        if (config.randomRotation)
        {
            float randomY = Random.Range(0f, 360f);
            prop.transform.rotation = Quaternion.Euler(0f, randomY, 0f);
        }

        if (showDebug)
        {
            Debug.Log($"{gameObject.name}: 生成道具 {config.propPrefab.name}");
        }
    }

    private Vector3 GetRandomSpawnPosition()
    {
        Vector2 randomCircle = Random.insideUnitCircle * spawnRadius;
        return transform.position + new Vector3(randomCircle.x, 0f, randomCircle.y);
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = isActive ? Color.cyan : Color.gray;
        Gizmos.DrawWireSphere(transform.position, 0.5f);

        if (spawnRadius > 0f)
        {
            Gizmos.color = isActive ? new Color(0f, 1f, 1f, 0.25f) : new Color(0.5f, 0.5f, 0.5f, 0.2f);
            Gizmos.DrawWireSphere(transform.position, spawnRadius);
        }
    }
}

