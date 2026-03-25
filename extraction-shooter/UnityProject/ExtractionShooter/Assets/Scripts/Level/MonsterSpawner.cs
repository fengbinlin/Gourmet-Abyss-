using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class MonsterSpawnConfig
{
    [Header("基础设置")]
    [Tooltip("怪物预制体")]
    public GameObject monsterPrefab;

    [Tooltip("刷出概率 (0-1)")]
    [Range(0f, 1f)]
    public float spawnProbability = 0.5f;

    [Tooltip("该类型怪物最大数量")]
    public int maxCount = 5;

    [Tooltip("当前怪物数量 (运行时自动更新)")]
    [HideInInspector]
    public int currentCount = 0;

    [Tooltip("怪物减少后等待时间 (秒)，等待这段时间没有继续减少后开始连续刷怪")]
    public float waitTimeAfterDecrease = 3.0f;

    [Tooltip("补充刷怪间隔 (秒)，在补充模式下刷怪的时间间隔")]
    public float rapidSpawnInterval = 1.0f;

    [Header("预热设置")]
    [Tooltip("预热时生成的数量（游戏开始时一次性生成）")]
    [Range(0, 20)]
    public int warmUpCount = 5;

    [Tooltip("预热数量比例：最终预热数 = 有效maxCount * warmUpCountRate。设置为 >=0 时优先使用该比例；设置为 -1 则兼容 warmUpCount 作为旧配置。")]
    public float warmUpCountRate = -1f;

    [Tooltip("是否启用预热")]
    public bool enableWarmUp = true;

    [Header("随机设置")]
    [Tooltip("是否随机大小")]
    public bool randomSize = true;

    [Tooltip("最小大小比例")]
    [Range(0.5f, 1.5f)]
    public float minSizeScale = 0.8f;

    [Tooltip("最大大小比例")]
    [Range(0.5f, 3.0f)]
    public float maxSizeScale = 1.2f;

    [Tooltip("是否随机旋转朝向")]
    public bool randomRotation = true;

    [HideInInspector]
    public float timeSinceLastDecrease = 0f;

    [HideInInspector]
    public bool isInRapidSpawnMode = false;

    [HideInInspector]
    public float lastRecordedCount = 0;
}

public class MonsterSpawner : MonoBehaviour
{
    [Header("关卡参数ID")]
    public string statsId = "default";

    [Header("刷怪配置")]
    [Tooltip("怪物生成配置列表")]
    public List<MonsterSpawnConfig> spawnConfigs = new List<MonsterSpawnConfig>();

    [Header("预热设置")]
    [Tooltip("全局预热开关")]
    public bool globalWarmUp = true;
    
    [Tooltip("预热生成的时间间隔（秒，避免在同一帧生成太多）")]
    [Range(0f, 1f)]
    public float warmUpInterval = 0.1f;

    [Header("常规刷怪设置")]
    // [Tooltip("普通刷怪间隔 (秒)")]
    // public float normalSpawnInterval = 5.0f;

    [Header("调试信息")]
    [Tooltip("是否显示调试信息")]
    public bool showDebug = true;

    [Tooltip("刷怪点是否启用")]
    public bool isActive = true;
    
    [Header("生成位置设置")]
    [Tooltip("生成半径（以刷怪点为中心的球形区域）")]
    public float spawnRadius = 5.0f;
    
    // 私有变量
    private float spawnTimer = 0f;
    private Dictionary<GameObject, MonsterSpawnConfig> prefabToConfig = new Dictionary<GameObject, MonsterSpawnConfig>();
    private Dictionary<MonsterSpawnConfig, Coroutine> rapidSpawnCoroutines = new Dictionary<MonsterSpawnConfig, Coroutine>();
    private Coroutine warmUpCoroutine = null;
    private bool isWarmingUp = false;

    void Start()
    {
        InitializeSpawner();
        
        // 如果启用全局预热，则开始预热生成
        if (globalWarmUp && isActive)
        {
            StartWarmUp();
        }
    }

    void Update()
    {
        if (!isActive || isWarmingUp) return;

        // 更新计时器
        spawnTimer += Time.deltaTime;

        // 更新每种怪物的"未减少"时间
        UpdateMonsterDecreaseTimers();

        // 检查是否需要进入快速刷怪模式
        CheckRapidSpawnConditions();

        // // 普通刷怪逻辑
        // if (spawnTimer >= normalSpawnInterval)
        // {
        //     spawnTimer = 0f;
        //     TrySpawnMonster();
        // }
    }

    /// <summary>
    /// 初始化刷怪器
    /// </summary>
    private void InitializeSpawner()
    {
        prefabToConfig.Clear();

        foreach (var config in spawnConfigs)
        {
            if (config.monsterPrefab != null)
            {
                prefabToConfig[config.monsterPrefab] = config;
                config.currentCount = 0;
                config.timeSinceLastDecrease = 0f;
                config.isInRapidSpawnMode = false;
                config.lastRecordedCount = 0;
            }
        }

        if (showDebug)
        {
            Debug.Log($"刷怪点 {gameObject.name} 初始化完成，配置了 {spawnConfigs.Count} 种怪物");
        }
    }

    /// <summary>
    /// 开始预热生成怪物
    /// </summary>
    private void StartWarmUp()
    {
        if (warmUpCoroutine != null)
        {
            StopCoroutine(warmUpCoroutine);
        }
        
        isWarmingUp = true;
        warmUpCoroutine = StartCoroutine(WarmUpRoutine());
        
        if (showDebug)
        {
            Debug.Log($"{gameObject.name}: 开始预热生成怪物");
        }
    }

    /// <summary>
    /// 预热协程
    /// </summary>
    private IEnumerator WarmUpRoutine()
    {
        // 计算每种怪物实际需要预热的数量
        Dictionary<MonsterSpawnConfig, int> targetWarmUpCounts = new Dictionary<MonsterSpawnConfig, int>();
        int totalWarmUpMonsters = 0;
        
        foreach (var config in spawnConfigs)
        {
            int effectiveMax = GetEffectiveMaxCount(config);
            int targetCount = GetWarmUpTargetCount(config, effectiveMax);

            if (config.monsterPrefab != null && config.enableWarmUp && targetCount > 0)
            {
                targetWarmUpCounts[config] = targetCount;
                totalWarmUpMonsters += targetCount;
                
                if (showDebug)
                {
                    Debug.Log($"{gameObject.name}: {config.monsterPrefab.name} 预热目标: {targetCount}/{effectiveMax} (warmUpCountRate={config.warmUpCountRate:F2})");
                }
            }
        }
        
        if (showDebug)
        {
            Debug.Log($"{gameObject.name}: 总共需要预热生成 {totalWarmUpMonsters} 个怪物");
        }
        
        if (totalWarmUpMonsters == 0)
        {
            isWarmingUp = false;
            if (showDebug)
            {
                Debug.Log($"{gameObject.name}: 没有需要预热的怪物");
            }
            yield break;
        }
        
        // 分批生成怪物，避免在同一帧生成太多
        int generatedCount = 0;
        foreach (var config in spawnConfigs)
        {
            int effectiveMax = GetEffectiveMaxCount(config);
            int targetCount = GetWarmUpTargetCount(config, effectiveMax);

            if (config.monsterPrefab != null && config.enableWarmUp && targetCount > 0)
            {
                for (int i = 0; i < targetCount; i++)
                {
                    if (config.currentCount >= GetEffectiveMaxCount(config))
                    {
                        if (showDebug)
                        {
                            Debug.Log($"{gameObject.name}: {config.monsterPrefab.name} 已达到最大数量 {GetEffectiveMaxCount(config)}，停止预热生成");
                        }
                        break;
                    }
                    
                    // 生成怪物
                    SpawnSpecificMonster(config, true);
                    generatedCount++;
                    
                    // 显示进度
                    if (showDebug && generatedCount % 5 == 0)
                    {
                        Debug.Log($"{gameObject.name}: 预热进度: {generatedCount}/{totalWarmUpMonsters}");
                    }
                    
                    // 等待间隔
                    if (warmUpInterval > 0)
                    {
                        yield return new WaitForSeconds(warmUpInterval);
                    }
                }
            }
        }
        
        warmUpCoroutine = null;
        isWarmingUp = false;
        
        if (showDebug)
        {
            Debug.Log($"{gameObject.name}: 预热完成，共生成 {generatedCount} 个怪物");
        }
    }

    /// <summary>
    /// 更新怪物减少计时器
    /// </summary>
    private void UpdateMonsterDecreaseTimers()
    {
        foreach (var config in spawnConfigs)
        {
            // 检查怪物数量是否减少
            if (config.currentCount < config.lastRecordedCount)
            {
                // 数量减少了，重置计时器
                config.timeSinceLastDecrease = 0f;
                config.lastRecordedCount = config.currentCount;

                // 如果正在快速刷怪，停止它
                if (rapidSpawnCoroutines.ContainsKey(config) && rapidSpawnCoroutines[config] != null)
                {
                    StopCoroutine(rapidSpawnCoroutines[config]);
                    config.isInRapidSpawnMode = false;
                }
            }
            else
            {
                // 数量没有减少，增加计时器
                config.timeSinceLastDecrease += Time.deltaTime;
                config.lastRecordedCount = config.currentCount;
            }
        }
    }

    /// <summary>
    /// 检查是否需要进入快速刷怪模式
    /// </summary>
    private void CheckRapidSpawnConditions()
    {
        foreach (var config in spawnConfigs)
        {
            // 条件1: 当前数量小于最大数量
            // 条件2: 已经过了等待时间
            // 条件3: 还没有进入快速刷怪模式
            if (config.currentCount < GetEffectiveMaxCount(config) &&
                config.timeSinceLastDecrease >= GetEffectiveWaitTimeAfterDecrease(config) &&
                !config.isInRapidSpawnMode)
            {
                StartRapidSpawnMode(config);
            }

            // 如果已经达到最大数量，停止快速刷怪
            if (config.currentCount >= GetEffectiveMaxCount(config) && config.isInRapidSpawnMode)
            {
                StopRapidSpawnMode(config);
            }
        }
    }

    /// <summary>
    /// 开始快速刷怪模式
    /// </summary>
    private void StartRapidSpawnMode(MonsterSpawnConfig config)
    {
        config.isInRapidSpawnMode = true;

        if (showDebug)
        {
            Debug.Log($"{gameObject.name}: {config.monsterPrefab.name} 进入补充刷怪模式！等待时间: {config.timeSinceLastDecrease:F1}s");
        }

        // 启动快速刷怪协程
        Coroutine coroutine = StartCoroutine(RapidSpawnRoutine(config));
        rapidSpawnCoroutines[config] = coroutine;
    }

    /// <summary>
    /// 停止快速刷怪模式
    /// </summary>
    private void StopRapidSpawnMode(MonsterSpawnConfig config)
    {
        config.isInRapidSpawnMode = false;

        if (rapidSpawnCoroutines.ContainsKey(config) && rapidSpawnCoroutines[config] != null)
        {
            StopCoroutine(rapidSpawnCoroutines[config]);
            rapidSpawnCoroutines[config] = null;
        }

        if (showDebug)
        {
            Debug.Log($"{gameObject.name}: {config.monsterPrefab.name} 停止补充刷怪，已达最大数量: {config.currentCount}/{config.maxCount}");
        }
    }

    /// <summary>
    /// 快速刷怪协程
    /// </summary>
    private IEnumerator RapidSpawnRoutine(MonsterSpawnConfig config)
    {
        while (config.isInRapidSpawnMode && config.currentCount < GetEffectiveMaxCount(config))
        {
            // 生成怪物
            SpawnSpecificMonster(config);

            // 等待指定的间隔
            yield return new WaitForSeconds(GetEffectiveRapidSpawnInterval(config));

            // 检查是否还需要继续快速刷怪
            if (config.currentCount >= GetEffectiveMaxCount(config) || !config.isInRapidSpawnMode)
            {
                break;
            }
        }

        config.isInRapidSpawnMode = false;
        if (rapidSpawnCoroutines.ContainsKey(config))
        {
            rapidSpawnCoroutines[config] = null;
        }

        if (showDebug)
        {
            Debug.Log($"{gameObject.name}: {config.monsterPrefab.name} 退出补充刷怪模式");
        }
    }

    /// <summary>
    /// 尝试生成怪物（普通刷怪模式）
    /// </summary>
    private void TrySpawnMonster()
    {
        if (spawnConfigs.Count == 0) return;

        // 筛选可以刷出的怪物类型（未达到最大数量）
        List<MonsterSpawnConfig> availableConfigs = new List<MonsterSpawnConfig>();
        List<float> probabilities = new List<float>();

        float totalProbability = 0f;

        foreach (var config in spawnConfigs)
        {
            if (config.monsterPrefab != null && config.currentCount < GetEffectiveMaxCount(config))
            {
                availableConfigs.Add(config);
                probabilities.Add(config.spawnProbability);
                totalProbability += config.spawnProbability;
            }
        }

        if (availableConfigs.Count == 0) return;

        // 如果总概率为0，则平均分配
        if (totalProbability <= 0)
        {
            float avgProb = 1f / availableConfigs.Count;
            probabilities.Clear();
            for (int i = 0; i < availableConfigs.Count; i++)
            {
                probabilities.Add(avgProb);
            }
            totalProbability = 1f;
        }

        // 按概率选择怪物类型
        float randomValue = Random.Range(0f, totalProbability);
        float cumulative = 0f;

        MonsterSpawnConfig selectedConfig = null;

        for (int i = 0; i < availableConfigs.Count; i++)
        {
            cumulative += probabilities[i];
            if (randomValue <= cumulative)
            {
                selectedConfig = availableConfigs[i];
                break;
            }
        }

        // 生成怪物
        if (selectedConfig != null)
        {
            SpawnSpecificMonster(selectedConfig);
        }
    }
    
    /// <summary>
    /// 获取随机生成位置（在XZ平面圆形区域内）
    /// </summary>
    private Vector3 GetRandomSpawnPosition()
    {
        // 生成圆盘内的随机点（XZ平面）
        Vector2 randomCircle = Random.insideUnitCircle * spawnRadius;

        // 将随机点转换为世界坐标，保持Y坐标不变
        Vector3 spawnPosition = transform.position + new Vector3(randomCircle.x, 0f, randomCircle.y);

        return spawnPosition;
    }
    
    /// <summary>
    /// 生成特定类型的怪物
    /// </summary>
    private void SpawnSpecificMonster(MonsterSpawnConfig config, bool isWarmUp = false)
    {
        if (config.currentCount >= GetEffectiveMaxCount(config)) return;

        // 实例化怪物（父物体设为当前脚本所在场景，附加式加载时怪物会留在本场景层级下）
        Vector3 spawnPosition = GetRandomSpawnPosition();
        Transform parent = transform.root; // 当前对象所在场景的根节点

        GameObject monster = Instantiate(config.monsterPrefab, spawnPosition, Quaternion.identity, parent);

        // 设置随机大小（如果启用）
        if (config.randomSize && config.minSizeScale < config.maxSizeScale)
        {
            float randomScale = Random.Range(config.minSizeScale, config.maxSizeScale);
            monster.transform.localScale = new Vector3(randomScale, randomScale, randomScale);
        }

        // 设置随机旋转（如果启用）
        if (config.randomRotation)
        {
            float randomYRotation = Random.Range(0f, 360f);
            monster.transform.rotation = Quaternion.Euler(0f, randomYRotation, 0f);
        }

        // 增加计数
        config.currentCount++;

        // 添加怪物销毁监听器
        MonsterDeathNotifier notifier = monster.AddComponent<MonsterDeathNotifier>();
        notifier.Initialize(this, config.monsterPrefab);

        if (showDebug && !isWarmUp)
        {
            Debug.Log($"{gameObject.name}: 生成 {config.monsterPrefab.name}，当前数量: {config.currentCount}/{GetEffectiveMaxCount(config)}");
        }
    }

    /// <summary>
    /// 怪物死亡时的回调
    /// </summary>
    public void OnMonsterDeath(GameObject monsterPrefab)
    {
        if (prefabToConfig.TryGetValue(monsterPrefab, out MonsterSpawnConfig config))
        {
            config.currentCount = Mathf.Max(0, config.currentCount - 1);

            if (showDebug)
            {
                Debug.Log($"{gameObject.name}: {monsterPrefab.name} 死亡，剩余数量: {config.currentCount}/{config.maxCount}");
            }
        }
    }

    /// <summary>
    /// 手动生成一个怪物
    /// </summary>
    public void SpawnMonsterManually(int configIndex = 0)
    {
        if (configIndex >= 0 && configIndex < spawnConfigs.Count)
        {
            SpawnSpecificMonster(spawnConfigs[configIndex]);
        }
    }

    /// <summary>
    /// 重置刷怪点
    /// </summary>
    public void ResetSpawner()
    {
        foreach (var config in spawnConfigs)
        {
            config.currentCount = 0;
            config.timeSinceLastDecrease = 0f;
            config.isInRapidSpawnMode = false;
            config.lastRecordedCount = 0;

            // 停止所有快速刷怪协程
            if (rapidSpawnCoroutines.ContainsKey(config) && rapidSpawnCoroutines[config] != null)
            {
                StopCoroutine(rapidSpawnCoroutines[config]);
            }
        }

        rapidSpawnCoroutines.Clear();
        spawnTimer = 0f;

        // 重新开始预热
        if (globalWarmUp && isActive)
        {
            StartWarmUp();
        }

        if (showDebug)
        {
            Debug.Log($"{gameObject.name} 已重置");
        }
    }

    /// <summary>
    /// 手动开始预热
    /// </summary>
    public void StartWarmUpManually()
    {
        if (warmUpCoroutine != null)
        {
            StopCoroutine(warmUpCoroutine);
        }
        StartWarmUp();
    }

    /// <summary>
    /// 手动停止预热
    /// </summary>
    public void StopWarmUpManually()
    {
        if (warmUpCoroutine != null)
        {
            StopCoroutine(warmUpCoroutine);
            warmUpCoroutine = null;
        }
        isWarmingUp = false;
        
        if (showDebug)
        {
            Debug.Log($"{gameObject.name}: 手动停止预热");
        }
    }

    /// <summary>
    /// 获取怪物总数
    /// </summary>
    public int GetTotalMonsterCount()
    {
        int total = 0;
        foreach (var config in spawnConfigs)
        {
            total += config.currentCount;
        }
        return total;
    }

    /// <summary>
    /// 强制开始补充刷怪模式
    /// </summary>
    public void ForceStartRapidSpawn(int configIndex)
    {
        if (configIndex >= 0 && configIndex < spawnConfigs.Count)
        {
            var config = spawnConfigs[configIndex];
            if (!config.isInRapidSpawnMode && config.currentCount < GetEffectiveMaxCount(config))
            {
                StartRapidSpawnMode(config);
            }
        }
    }

    /// <summary>
    /// 获取刷怪点状态信息
    /// </summary>
    public string GetSpawnerStatus()
    {
        string status = $"刷怪点: {gameObject.name}\n";
        status += $"状态: {(isActive ? "启用" : "禁用")}\n";
        status += $"预热状态: {(isWarmingUp ? "预热中" : "就绪")}\n";
        //status += $"普通刷怪间隔: {normalSpawnInterval:F1}秒\n";
        status += "怪物状态:\n";

        foreach (var config in spawnConfigs)
        {
            if (config.monsterPrefab != null)
            {
                status += $"  {config.monsterPrefab.name}: {config.currentCount}/{config.maxCount} ";
                status += $"(预热: {config.warmUpCount}, 等待: {config.timeSinceLastDecrease:F1}/{GetEffectiveWaitTimeAfterDecrease(config):F1}s) ";
                status += $"{(config.isInRapidSpawnMode ? "[补充中]" : "")}\n";
            }
        }

        return status;
    }

    void OnDrawGizmos()
    {
        if (showDebug)
        {
            Gizmos.color = isActive ? Color.green : Color.gray;
            Gizmos.DrawWireSphere(transform.position, 1f);
            Gizmos.DrawIcon(transform.position + Vector3.up * 2, "SpawnPoint.png", true);

            // 绘制XZ平面上的生成区域
            DrawSpawnAreaGizmos();
        }
    }
    
    /// <summary>
    /// 绘制生成区域的Gizmos
    /// </summary>
    private void DrawSpawnAreaGizmos()
    {
        if (spawnRadius <= 0) return;

        // 设置Gizmos颜色
        Gizmos.color = isActive ? new Color(0, 1, 0, 0.3f) : new Color(0.5f, 0.5f, 0.5f, 0.3f);

        // 绘制圆形的边缘
        int segments = 36;
        Vector3 previousPoint = Vector3.zero;
        Vector3 firstPoint = Vector3.zero;

        for (int i = 0; i <= segments; i++)
        {
            float angle = (float)i / segments * Mathf.PI * 2f;
            float x = Mathf.Cos(angle) * spawnRadius;
            float z = Mathf.Sin(angle) * spawnRadius;
            Vector3 point = transform.position + new Vector3(x, 0f, z);

            if (i > 0)
            {
                Gizmos.DrawLine(previousPoint, point);
            }
            else
            {
                firstPoint = point;
            }

            previousPoint = point;
        }

        // 连接最后一点和第一点
        Gizmos.DrawLine(previousPoint, firstPoint);

        // 绘制中心点到边缘的参考线
        Gizmos.color = isActive ? new Color(0, 1, 0, 0.5f) : new Color(0.5f, 0.5f, 0.5f, 0.5f);
        for (int i = 0; i < 4; i++)
        {
            float angle = (float)i / 4 * Mathf.PI * 2f;
            float x = Mathf.Cos(angle) * spawnRadius;
            float z = Mathf.Sin(angle) * spawnRadius;
            Vector3 point = transform.position + new Vector3(x, 0f, z);
            Gizmos.DrawLine(transform.position, point);
        }

        // 绘制填充区域（使用网格）
        DrawFilledCircleGizmos();

        // 在Scene视图中显示半径文本
        DrawRadiusText();
    }
    
    /// <summary>
    /// 绘制填充的圆形区域
    /// </summary>
    private void DrawFilledCircleGizmos()
    {
        // 只在Unity编辑器中绘制填充
#if UNITY_EDITOR
        if (!UnityEditor.SceneView.currentDrawingSceneView) return;

        int fillSegments = 12;
        Vector3 center = transform.position;

        // 绘制填充的三角形扇
        UnityEditor.Handles.color = isActive ? new Color(0, 1, 0, 0.1f) : new Color(0.5f, 0.5f, 0.5f, 0.1f);

        Vector3[] vertices = new Vector3[fillSegments + 2];
        vertices[0] = center;

        for (int i = 0; i <= fillSegments; i++)
        {
            float angle = (float)i / fillSegments * Mathf.PI * 2f;
            float x = Mathf.Cos(angle) * spawnRadius;
            float z = Mathf.Sin(angle) * spawnRadius;
            vertices[i + 1] = center + new Vector3(x, 0f, z);
        }

        // 创建三角形数组
        int[] triangles = new int[fillSegments * 3];
        for (int i = 0; i < fillSegments; i++)
        {
            triangles[i * 3] = 0;
            triangles[i * 3 + 1] = i + 1;
            triangles[i * 3 + 2] = i + 2;
        }

        // 绘制网格
        Mesh mesh = new Mesh();
        mesh.vertices = vertices;
        mesh.triangles = triangles;

        UnityEditor.Handles.DrawAAConvexPolygon(vertices);
#endif
    }
    
    /// <summary>
    /// 在Scene视图中显示半径文本
    /// </summary>
    private void DrawRadiusText()
    {
#if UNITY_EDITOR
        GUIStyle style = new GUIStyle();
        style.normal.textColor = isActive ? Color.green : Color.gray;
        style.alignment = TextAnchor.MiddleCenter;
        style.fontSize = 12;

        Vector3 textPosition = transform.position + Vector3.up * 0.5f;
        UnityEditor.Handles.Label(textPosition, $"半径: {spawnRadius:F1}", style);

        // 在四个方向显示距离标记
        for (int i = 0; i < 4; i++)
        {
            float angle = (float)i / 4 * Mathf.PI * 2f;
            float x = Mathf.Cos(angle) * spawnRadius;
            float z = Mathf.Sin(angle) * spawnRadius;
            Vector3 point = transform.position + new Vector3(x, 0f, z);

            string direction = i == 0 ? "X+" : i == 1 ? "Z+" : i == 2 ? "X-" : "Z-";
            UnityEditor.Handles.Label(point + Vector3.up * 0.5f, direction, style);
        }
#endif
    }
    
    void OnDestroy()
    {
        // 清理所有协程
        foreach (var coroutine in rapidSpawnCoroutines.Values)
        {
            if (coroutine != null)
            {
                StopCoroutine(coroutine);
            }
        }
        
        // 清理预热协程
        if (warmUpCoroutine != null)
        {
            StopCoroutine(warmUpCoroutine);
        }
    }

    private int GetEffectiveMaxCount(MonsterSpawnConfig config)
    {
        float rate = 1f;
        if (WeaponStatsManager.Instance != null)
        {
            rate = WeaponStatsManager.Instance.GetMonsterCountRate(statsId);
        }

        return Mathf.Max(1, Mathf.RoundToInt(config.maxCount * rate));
    }

    private float GetEffectiveRapidSpawnInterval(MonsterSpawnConfig config)
    {
        float rate = 1f;
        if (WeaponStatsManager.Instance != null)
        {
            rate = WeaponStatsManager.Instance.GetMonsterRapidSpawnIntervalRate(statsId);
        }

        return Mathf.Max(0.01f, config.rapidSpawnInterval * rate);
    }

    private float GetEffectiveWaitTimeAfterDecrease(MonsterSpawnConfig config)
    {
        float rate = 1f;
        if (WeaponStatsManager.Instance != null)
        {
            rate = WeaponStatsManager.Instance.GetMonsterWaitTimeRate(statsId);
        }

        return Mathf.Max(0.01f, config.waitTimeAfterDecrease * rate);
    }

    private int GetWarmUpTargetCount(MonsterSpawnConfig config, int effectiveMaxCount)
    {
        effectiveMaxCount = Mathf.Max(1, effectiveMaxCount);

        // warmUpCountRate >= 0 时优先使用比例
        if (config.warmUpCountRate >= 0f)
        {
            float ratio = Mathf.Clamp01(config.warmUpCountRate);
            int target = Mathf.RoundToInt(effectiveMaxCount * ratio);
            return Mathf.Clamp(target, 0, effectiveMaxCount);
        }

        // 兼容旧配置：把 warmUpCount 转成比例后再乘有效maxCount
        if (config.maxCount > 0)
        {
            float ratio = Mathf.Clamp01(config.warmUpCount / (float)config.maxCount);
            int target = Mathf.RoundToInt(effectiveMaxCount * ratio);
            return Mathf.Clamp(target, 0, effectiveMaxCount);
        }

        return 0;
    }
}

/// <summary>
/// 怪物死亡通知组件
/// </summary>
public class MonsterDeathNotifier : MonoBehaviour
{
    private MonsterSpawner spawner;
    private GameObject monsterPrefab;

    /// <summary>
    /// 初始化通知器
    /// </summary>
    public void Initialize(MonsterSpawner spawner, GameObject prefab)
    {
        this.spawner = spawner;
        this.monsterPrefab = prefab;
    }

    /// <summary>
    /// 当怪物被销毁时调用
    /// </summary>
    private void OnDestroy()
    {
        if (spawner != null && monsterPrefab != null)
        {
            spawner.OnMonsterDeath(monsterPrefab);
        }
    }
}