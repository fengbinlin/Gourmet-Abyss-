using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.Linq;
using UnityEngine.AI;

public class PlantGenerator : MonoBehaviour
{
    [SerializeField] private PlantGenerationSettings settings;
    [SerializeField] private Transform plantsParent;
    [SerializeField] private bool generateOnStart = true;
    [SerializeField] private bool showDebugInfo = true;
    public float ManagertdensityMultiplier = 1;
    // 再生相关字段
    [Header("=== 再生控制 ===")]
    [Tooltip("自动开始再生协程")]
    [SerializeField] private bool autoStartRegeneration = true;
    [Tooltip("显示再生调试信息")]
    [SerializeField] private bool showRegenerationDebug = false;

    private List<GameObject> spawnedPlants = new List<GameObject>();
    private System.Random random;

    // 柏林噪声缓存
    private float noiseOffsetX, noiseOffsetZ;

    // 再生协程控制
    private Coroutine regenerationCoroutine;
    private bool isRegenerating = false;

    // 每种植物的数量统计
    private Dictionary<GameObject, int> plantCountByPrefab = new Dictionary<GameObject, int>();
    private Dictionary<GameObject, int> plantTargetCountByPrefab = new Dictionary<GameObject, int>();

    // 调试统计
    private int totalAttempts = 0;
    private int noisePassed = 0;
    private int groundPassed = 0;
    private int prefabPassed = 0;
    private int regenerationCount = 0;

    private void Start()
    {
        if (generateOnStart)
        {
            StartCoroutine(GeneratePlantsCoroutine());
        }

        if (autoStartRegeneration && settings.enableRegeneration)
        {
            StartRegeneration();
        }
    }

    private void OnDestroy()
    {
        StopRegeneration();
    }

    #region 主生成方法

    /// <summary>
    /// 生成植被
    /// </summary>
    public void GeneratePlants()
    {
        StartCoroutine(GeneratePlantsCoroutine());
    }

    private IEnumerator GeneratePlantsCoroutine()
    {
        if (isRegenerating)
        {
            if (showDebugInfo) Debug.Log("正在再生中，等待完成...");
            yield break;
        }

        ClearExistingPlants();

        // 重置统计
        totalAttempts = 0;
        noisePassed = 0;
        groundPassed = 0;
        prefabPassed = 0;
        regenerationCount = 0;

        // === 在生成之前读取密度乘积并应用 ===
        if (WeaponStatsManager.Instance != null)
        {
            float densityMultiplier = WeaponStatsManager.Instance.GetMapDensityMultiplier(settings);
            ManagertdensityMultiplier = densityMultiplier;
            float originalBaseDensity = settings.baseDensity;

            // settings.baseDensity = Mathf.Clamp01(originalBaseDensity * densityMultiplier);

            if (showDebugInfo)
            {
                Debug.Log($"应用密度乘积：原始={originalBaseDensity} 乘积={densityMultiplier} 结果={settings.baseDensity}");
            }
        }

        // 设置随机种子
        int seed = settings.useRandomSeed ? UnityEngine.Random.Range(1, 99999) : settings.seed;
        random = new System.Random(seed);
        UnityEngine.Random.InitState(seed);

        // 噪声偏移
        noiseOffsetX = (float)random.NextDouble() * 1000f;
        noiseOffsetZ = (float)random.NextDouble() * 1000f;

        if (showDebugInfo)
        {
            Debug.Log($"开始植被生成，种子: {seed}");
            Debug.Log($"生成区域: {settings.generationAreaSize.x}x{settings.generationAreaSize.z} 米");
        }

        // 选择生成模式
        List<Vector2> generationPoints = new List<Vector2>();

        switch (settings.generationMode)
        {
            case PlantGenerationSettings.GenerationMode.Grid:
                generationPoints = GenerateGridPoints();
                break;
            case PlantGenerationSettings.GenerationMode.Poisson:
                generationPoints = GeneratePoissonPoints();
                break;
            case PlantGenerationSettings.GenerationMode.RandomGrid:
                generationPoints = GenerateRandomGridPoints();
                break;
            case PlantGenerationSettings.GenerationMode.Uniform:
                generationPoints = GenerateUniformPoints();
                break;
        }

        if (showDebugInfo)
        {
            Debug.Log($"生成了 {generationPoints.Count} 个候选点");
        }

        // 初始化植物计数
        InitializePlantCounts();

        // 生成植物
        int generatedThisFrame = 0;
        List<Vector3> placedPositions = new List<Vector3>();

        foreach (Vector2 point in generationPoints)
        {
            Vector3 worldPos = new Vector3(point.x, 0, point.y);

            if (TryPlacePlantAtPosition(worldPos, placedPositions, false))
            {
                placedPositions.Add(worldPos);
            }

            generatedThisFrame++;
            if (settings.maxPlantsPerFrame > 0 && generatedThisFrame >= settings.maxPlantsPerFrame)
            {
                generatedThisFrame = 0;
                yield return null;
            }
        }

        if (showDebugInfo)
        {
            // Debug.Log($"生成完成！统计：");
            // Debug.Log($"总尝试: {totalAttempts}");
            // Debug.Log($"通过噪声检测: {noisePassed}");
            // Debug.Log($"通过地面检测: {groundPassed}");
            // Debug.Log($"通过植物选择: {prefabPassed}");
            Debug.Log($"最终生成: {spawnedPlants.Count} 棵植物");
            // Debug.Log($"成功率: {(float)spawnedPlants.Count / totalAttempts * 100:F1}%");

            // 显示每种植物的数量
            Debug.Log("每种植物的数量：");
            foreach (var kvp in plantCountByPrefab)
            {
                if (kvp.Key != null)
                {
                    int targetCount = plantTargetCountByPrefab.ContainsKey(kvp.Key) ? plantTargetCountByPrefab[kvp.Key] : 0;
                    //Debug.Log($"  {kvp.Key.name}: {kvp.Value}/{targetCount}");
                }
            }
        }
    }

    /// <summary>
    /// 初始化植物计数
    /// </summary>
    private void InitializePlantCounts()
    {
        plantCountByPrefab.Clear();
        plantTargetCountByPrefab.Clear();

        foreach (var plantData in settings.plantPrefabs)
        {
            if (plantData.prefab != null)
            {
                plantCountByPrefab[plantData.prefab] = 0;
                plantTargetCountByPrefab[plantData.prefab] = plantData.regenerationTargetCount;
            }
        }
    }

    /// <summary>
    /// 更新植物计数
    /// </summary>
    private void UpdatePlantCount(GameObject prefab, int delta)
    {
        if (prefab != null && plantCountByPrefab.ContainsKey(prefab))
        {
            plantCountByPrefab[prefab] += delta;
            if (plantCountByPrefab[prefab] < 0) plantCountByPrefab[prefab] = 0;
        }
    }

    /// <summary>
    /// 清理被销毁的植物
    /// </summary>
    private void CleanupDestroyedPlants()
    {
        int removedCount = 0;
        for (int i = spawnedPlants.Count - 1; i >= 0; i--)
        {
            if (spawnedPlants[i] == null)
            {
                spawnedPlants.RemoveAt(i);
                removedCount++;
            }
        }

        if (removedCount > 0 && showRegenerationDebug)
        {
            Debug.Log($"清理了 {removedCount} 个被销毁的植物");
        }
    }

    /// <summary>
    /// 尝试在位置放置植物
    /// </summary>
    private bool TryPlacePlantAtPosition(Vector3 position, List<Vector3> placedPositions, bool isRegeneration = false)
    {
        totalAttempts++;

        // 步骤1: 计算柏林噪声
        float noiseValue = CalculatePerlinNoise(position, isRegeneration);

        // 应用密度曲线
        noiseValue = settings.densityCurve.Evaluate(noiseValue);

        // 步骤2: 噪声阈值检查
        if (noiseValue < settings.minNoiseThreshold || noiseValue > settings.maxNoiseThreshold)
            return false;

        noisePassed++;

        // 步骤3: 基础密度检查
        float densityMultiplier = isRegeneration ? settings.regenerationDensityMultiplier : 1f;
        float effectiveDensity = settings.baseDensity * densityMultiplier * ManagertdensityMultiplier;
        float effectiveNoise = Mathf.Lerp(noiseValue, 1f, 1f - settings.noiseInfluence);

        if ((float)random.NextDouble() > effectiveDensity * effectiveNoise)
            return false;

        // 步骤4: 地面检测
        if (!RaycastToGround(position, out Vector3 groundPos, out Vector3 groundNormal, out float groundHeight, out float groundAngle))
            return false;

        groundPassed++;

        // 步骤5: 选择植物
        GameObject plantPrefab = SelectPlantPrefabByNoise(noiseValue, groundHeight, groundAngle, isRegeneration);
        if (plantPrefab == null)
            return false;

        // 获取植物数据
        PlantGenerationSettings.PlantPrefabData plantData = null;
        foreach (var data in settings.plantPrefabs)
        {
            if (data.prefab == plantPrefab)
            {
                plantData = data;
                break;
            }
        }

        if (plantData == null)
            return false;

        // 如果是再生模式，检查该植物是否需要再生
        if (isRegeneration)
        {
            // 检查该植物是否允许再生
            if (!plantData.allowRegeneration)
                return false;

            // 检查是否已达到目标数量
            if (plantData.regenerationTargetCount > 0)
            {
                int currentCount = GetPlantCountByPrefab(plantPrefab);
                if (currentCount >= plantData.regenerationTargetCount)
                {
                    if (showRegenerationDebug)
                    {
                        Debug.Log($"植物 {plantPrefab.name} 已达到目标数量 {currentCount}/{plantData.regenerationTargetCount}，跳过");
                    }
                    return false;
                }
            }
        }

        prefabPassed++;

        // 步骤6: 实例化植物
        Vector3 spawnPos = groundPos;

        // 对齐地面
        if (settings.alignToGroundNormal)
        {
            Quaternion groundRotation = Quaternion.FromToRotation(Vector3.up, groundNormal);
            float randomYRotation = (float)random.NextDouble() * 360f;
            Quaternion finalRotation = groundRotation * Quaternion.Euler(0, randomYRotation, 0);
            SpawnPlant(plantPrefab, plantData, spawnPos, finalRotation, isRegeneration);
        }
        else
        {
            Quaternion rotation = Quaternion.Euler(0, (float)random.NextDouble() * 360f, 0);
            SpawnPlant(plantPrefab, plantData, spawnPos, rotation, isRegeneration);
        }

        // 步骤7: 集群生成
        if (plantData.allowClustering && plantData.clusterMax > 1)
        {
            int clusterCount = random.Next(plantData.clusterMin, plantData.clusterMax + 1);

            for (int i = 1; i < clusterCount; i++)
            {
                float angle = (float)random.NextDouble() * Mathf.PI * 2;
                float distance = 0.3f + (float)random.NextDouble() * 1.5f;

                Vector3 clusterPos = spawnPos + new Vector3(
                    Mathf.Cos(angle) * distance,
                    0,
                    Mathf.Sin(angle) * distance
                );

                if (RaycastToGround(clusterPos, out Vector3 clusterGroundPos, out Vector3 clusterNormal, out float _, out float clusterAngle))
                {
                    if (clusterAngle <= plantData.maxGroundAngle)
                    {
                        if (settings.alignToGroundNormal)
                        {
                            Quaternion clusterRotation = Quaternion.FromToRotation(Vector3.up, clusterNormal) *
                                                       Quaternion.Euler(0, (float)random.NextDouble() * 360f, 0);
                            SpawnPlant(plantPrefab, plantData, clusterGroundPos, clusterRotation, isRegeneration);
                        }
                        else
                        {
                            Quaternion clusterRotation = Quaternion.Euler(0, (float)random.NextDouble() * 360f, 0);
                            SpawnPlant(plantPrefab, plantData, clusterGroundPos, clusterRotation, isRegeneration);
                        }
                    }
                }
            }
        }

        return true;
    }

    /// <summary>
    /// 实例化单个植物
    /// </summary>
    private void SpawnPlant(GameObject prefab, PlantGenerationSettings.PlantPrefabData data, Vector3 position, Quaternion rotation, bool isRegeneration = false)
    {
        GameObject plant = Instantiate(prefab, plantsParent);
        if (plant.GetComponent<NavMeshAgent>())
        {
            plant.GetComponent<NavMeshAgent>().Warp(position);
        }
        else
        {
            plant.transform.position = position;
        }
        
        plant.transform.rotation = rotation;

        // 随机缩放
        float scale = Mathf.Lerp(data.minScale, data.maxScale, (float)random.NextDouble());
        plant.transform.localScale = Vector3.one * scale;

        // 添加组件用于跟踪预制体
        var plantTracker = plant.AddComponent<PlantTracker>();
        plantTracker.prefab = prefab;
        plantTracker.allowRegeneration = data.allowRegeneration;
        plantTracker.regenerationTargetCount = data.regenerationTargetCount;
        plantTracker.isRegenerated = isRegeneration;

        spawnedPlants.Add(plant);

        // 更新植物计数
        UpdatePlantCount(prefab, 1);

        if (isRegeneration)
        {
            regenerationCount++;

            if (showRegenerationDebug)
            {
                int currentCount = GetPlantCountByPrefab(prefab);
                int targetCount = data.regenerationTargetCount;
                Debug.Log($"再生植物: {prefab.name} 在位置 {position}，当前数量: {currentCount}/{targetCount}");
            }
        }
    }

    /// <summary>
    /// 获取指定预制体的植物数量
    /// </summary>
    private int GetPlantCountByPrefab(GameObject prefab)
    {
        if (prefab == null) return 0;

        // 清理被销毁的植物并重新计数
        int count = 0;
        foreach (var plant in spawnedPlants)
        {
            if (plant != null)
            {
                var tracker = plant.GetComponent<PlantTracker>();
                if (tracker != null && tracker.prefab == prefab)
                {
                    count++;
                }
            }
        }

        // 更新缓存
        if (plantCountByPrefab.ContainsKey(prefab))
        {
            plantCountByPrefab[prefab] = count;
        }

        return count;
    }

    /// <summary>
    /// 计算柏林噪声（支持再生使用不同的噪声图）
    /// </summary>
    private float CalculatePerlinNoise(Vector3 position, bool isRegeneration = false)
    {
        float xCoord, yCoord;

        if (isRegeneration)
        {
            // 再生时使用不同的噪声偏移
            xCoord = (position.x + noiseOffsetX + settings.noiseOffset.x + settings.regenerationNoiseOffset.x) * settings.noiseScale;
            yCoord = (position.z + noiseOffsetZ + settings.noiseOffset.y + settings.regenerationNoiseOffset.y) * settings.noiseScale;
        }
        else
        {
            // 初始生成使用原始噪声偏移
            xCoord = (position.x + noiseOffsetX + settings.noiseOffset.x) * settings.noiseScale;
            yCoord = (position.z + noiseOffsetZ + settings.noiseOffset.y) * settings.noiseScale;
        }

        if (settings.octaves == 1)
        {
            // 单层柏林噪声
            return Mathf.PerlinNoise(xCoord, yCoord);
        }

        // 分形布朗运动（多层柏林噪声）
        float noiseValue = 0f;
        float amplitude = 1f;
        float frequency = settings.noiseScale;
        float maxValue = 0f;

        for (int i = 0; i < settings.octaves; i++)
        {
            float x = xCoord * frequency;
            float y = yCoord * frequency;

            noiseValue += Mathf.PerlinNoise(x, y) * amplitude;
            maxValue += amplitude;

            amplitude *= settings.persistence;
            frequency *= settings.lacunarity;
        }

        return Mathf.Clamp01(noiseValue / maxValue);
    }

    #endregion

    #region 修复的再生功能（添加集群生成支持）

    /// <summary>
    /// 开始再生
    /// </summary>
    public void StartRegeneration()
    {
        if (!settings.enableRegeneration)
        {
            Debug.LogWarning("再生功能未在设置中启用");
            return;
        }

        if (regenerationCoroutine != null)
        {
            StopCoroutine(regenerationCoroutine);
        }

        regenerationCoroutine = StartCoroutine(RegenerationCoroutine());

        if (showDebugInfo)
        {
            Debug.Log("植物再生已开始");
        }
    }

    /// <summary>
    /// 停止再生
    /// </summary>
    public void StopRegeneration()
    {
        if (regenerationCoroutine != null)
        {
            StopCoroutine(regenerationCoroutine);
            regenerationCoroutine = null;
        }

        isRegenerating = false;

        if (showDebugInfo)
        {
            Debug.Log("植物再生已停止");
        }
    }

    /// <summary>
    /// 立即执行一次再生尝试
    /// </summary>
    public void RegenerateNow()
    {
        if (!settings.enableRegeneration)
        {
            Debug.LogWarning("再生功能未在设置中启用");
            return;
        }

        if (!isRegenerating)
        {
            StartCoroutine(SingleRegenerationCycle());
        }
    }

    /// <summary>
    /// 再生协程
    /// </summary>
    private IEnumerator RegenerationCoroutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(settings.regenerationCheckInterval);

            // 执行再生循环
            yield return StartCoroutine(SingleRegenerationCycle());
        }
    }

    /// <summary>
    /// 单次再生循环
    /// </summary>
    private IEnumerator SingleRegenerationCycle()
    {
        if (isRegenerating)
        {
            yield break;
        }

        isRegenerating = true;

        try
        {
            // 1. 清理被销毁的植物
            CleanupDestroyedPlants();

            // 2. 检查哪些植物需要再生
            List<PlantGenerationSettings.PlantPrefabData> plantsToRegenerate = new List<PlantGenerationSettings.PlantPrefabData>();

            foreach (var plantData in settings.plantPrefabs)
            {
                if (plantData.prefab == null) continue;
                if (!plantData.allowRegeneration) continue;
                if (plantData.regenerationTargetCount <= 0) continue;

                int currentCount = GetPlantCountByPrefab(plantData.prefab);

                if (currentCount < plantData.regenerationTargetCount)
                {
                    plantsToRegenerate.Add(plantData);

                    if (showRegenerationDebug)
                    {
                        Debug.Log($"植物 {plantData.prefab.name} 需要再生: {currentCount}/{plantData.regenerationTargetCount}");
                    }
                }
            }

            if (plantsToRegenerate.Count == 0)
            {
                yield break;
            }

            if (showRegenerationDebug)
            {
                Debug.Log($"开始再生循环，有 {plantsToRegenerate.Count} 种植物需要再生");
            }

            // 3. 为每种需要再生的植物尝试生成
            int totalGenerated = 0;
            List<Vector3> placedPositions = GetCurrentPlantPositions();

            foreach (var plantData in plantsToRegenerate)
            {
                if (plantData.prefab == null) continue;

                int currentCount = GetPlantCountByPrefab(plantData.prefab);
                int neededCount = plantData.regenerationTargetCount - currentCount;

                if (neededCount <= 0) continue;

                if (showRegenerationDebug)
                {
                    Debug.Log($"尝试为 {plantData.prefab.name} 再生 {neededCount} 棵植物");
                }

                // 为该植物尝试生成
                int generatedForThisPlant = 0;
                int attempts = Mathf.Min(neededCount * 3, settings.regenerationAttemptsPerCycle);

                for (int i = 0; i < attempts; i++)
                {
                    // 如果已经达到目标数量，就停止
                    if (GetPlantCountByPrefab(plantData.prefab) >= plantData.regenerationTargetCount)
                    {
                        break;
                    }

                    // 生成随机位置
                    Vector3 randomPosition = GenerateRandomPositionInArea();

                    // 检查是否太靠近现有植物
                    if (IsTooCloseToExistingPlant(randomPosition, placedPositions, settings.regenerationMinDistance))
                    {
                        continue;
                    }

                    // 专门为该植物尝试生成（包含集群生成）
                    if (TryPlaceSpecificPlant(plantData, randomPosition, placedPositions))
                    {
                        // 在TryPlaceSpecificPlant中会处理集群生成，所以这里不需要单独添加位置
                        // 只需要获取新生成的位置
                        Vector3 newPlantPos = randomPosition;
                        if (RaycastToGround(randomPosition, out Vector3 groundPos, out _, out _, out _))
                        {
                            newPlantPos = groundPos;
                        }

                        placedPositions.Add(newPlantPos);
                        generatedForThisPlant++;
                        totalGenerated++;

                        // 每帧生成限制
                        if (settings.maxPlantsPerFrame > 0 && generatedForThisPlant >= settings.maxPlantsPerFrame)
                        {
                            yield return null;
                            generatedForThisPlant = 0;
                        }
                    }
                }

                if (showRegenerationDebug && generatedForThisPlant > 0)
                {
                    Debug.Log($"为 {plantData.prefab.name} 成功再生了 {generatedForThisPlant} 棵植物");
                }
            }

            if (showRegenerationDebug && totalGenerated > 0)
            {
                Debug.Log($"本次再生循环总共生成了 {totalGenerated} 棵植物");
            }
        }
        finally
        {
            isRegenerating = false;
        }
    }

    /// <summary>
    /// 尝试放置特定植物（支持集群生成）
    /// </summary>
    private bool TryPlaceSpecificPlant(PlantGenerationSettings.PlantPrefabData plantData, Vector3 position, List<Vector3> placedPositions)
    {
        // 计算柏林噪声（再生时使用不同的噪声图）
        float noiseValue = CalculatePerlinNoise(position, true);
        noiseValue = settings.densityCurve.Evaluate(noiseValue);

        // 噪声阈值检查
        float minThreshold = Mathf.Clamp01(settings.minNoiseThreshold * (1.0f / ManagertdensityMultiplier));
        float maxThreshold = Mathf.Clamp01(settings.maxNoiseThreshold * ManagertdensityMultiplier);

        if (noiseValue < minThreshold || noiseValue > maxThreshold)
            return false;

        // 基础密度检查
        float densityMultiplier = settings.regenerationDensityMultiplier;
        float effectiveDensity = settings.baseDensity * densityMultiplier * ManagertdensityMultiplier;
        float effectiveNoise = Mathf.Lerp(noiseValue, 1f, 1f - settings.noiseInfluence);

        if ((float)random.NextDouble() > effectiveDensity * effectiveNoise)
            return false;

        // 地面检测
        if (!RaycastToGround(position, out Vector3 groundPos, out Vector3 groundNormal, out float groundHeight, out float groundAngle))
            return false;

        // 检查植物条件
        if (groundHeight < plantData.heightRange.x || groundHeight > plantData.heightRange.y)
            return false;

        if (groundAngle > plantData.maxGroundAngle)
            return false;

        if (noiseValue < plantData.preferredNoiseMin || noiseValue > plantData.preferredNoiseMax)
            return false;

        // 实例化主植物
        Vector3 spawnPos = groundPos;

        if (settings.alignToGroundNormal)
        {
            Quaternion groundRotation = Quaternion.FromToRotation(Vector3.up, groundNormal);
            float randomYRotation = (float)random.NextDouble() * 360f;
            Quaternion finalRotation = groundRotation * Quaternion.Euler(0, randomYRotation, 0);
            SpawnPlant(plantData.prefab, plantData, spawnPos, finalRotation, true);
        }
        else
        {
            Quaternion rotation = Quaternion.Euler(0, (float)random.NextDouble() * 360f, 0);
            SpawnPlant(plantData.prefab, plantData, spawnPos, rotation, true);
        }

        // 集群生成
        if (plantData.allowClustering && plantData.clusterMax > 1)
        {
            int clusterCount = random.Next(plantData.clusterMin, plantData.clusterMax + 1);

            for (int i = 1; i < clusterCount; i++)
            {
                float angle = (float)random.NextDouble() * Mathf.PI * 2;
                float distance = 0.3f + (float)random.NextDouble() * 1.5f;

                Vector3 clusterPos = spawnPos + new Vector3(
                    Mathf.Cos(angle) * distance,
                    0,
                    Mathf.Sin(angle) * distance
                );

                if (RaycastToGround(clusterPos, out Vector3 clusterGroundPos, out Vector3 clusterNormal, out float clusterHeight, out float clusterAngle))
                {
                    // 检查集群植物的条件
                    if (clusterAngle > plantData.maxGroundAngle)
                        continue;

                    if (clusterHeight < plantData.heightRange.x || clusterHeight > plantData.heightRange.y)
                        continue;

                    // 检查集群位置是否太靠近其他植物
                    if (IsTooCloseToExistingPlant(clusterPos, placedPositions, settings.regenerationMinDistance * 0.5f))
                        continue;

                    if (settings.alignToGroundNormal)
                    {
                        Quaternion clusterRotation = Quaternion.FromToRotation(Vector3.up, clusterNormal) *
                                                   Quaternion.Euler(0, (float)random.NextDouble() * 360f, 0);
                        SpawnPlant(plantData.prefab, plantData, clusterGroundPos, clusterRotation, true);
                    }
                    else
                    {
                        Quaternion clusterRotation = Quaternion.Euler(0, (float)random.NextDouble() * 360f, 0);
                        SpawnPlant(plantData.prefab, plantData, clusterGroundPos, clusterRotation, true);
                    }

                    // 添加集群位置到已放置位置列表
                    placedPositions.Add(clusterGroundPos);
                }
            }
        }

        return true;
    }

    /// <summary>
    /// 在生成区域内生成随机位置
    /// </summary>
    private Vector3 GenerateRandomPositionInArea()
    {
        Vector3 min = settings.generationAreaCenter - settings.generationAreaSize * 0.5f;
        Vector3 max = settings.generationAreaCenter + settings.generationAreaSize * 0.5f;

        float x = (float)(random.NextDouble() * (max.x - min.x)) + min.x;
        float z = (float)(random.NextDouble() * (max.z - min.z)) + min.z;

        return new Vector3(x, 0, z);
    }

    /// <summary>
    /// 检查是否太靠近现有植物
    /// </summary>
    private bool IsTooCloseToExistingPlant(Vector3 position, List<Vector3> existingPositions, float minDistance)
    {
        if (existingPositions == null || existingPositions.Count == 0) return false;

        Vector2 pos2D = new Vector2(position.x, position.z);

        foreach (Vector3 existingPos in existingPositions)
        {
            Vector2 existingPos2D = new Vector2(existingPos.x, existingPos.z);
            if (Vector2.Distance(pos2D, existingPos2D) < minDistance)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 获取当前所有植物的位置
    /// </summary>
    private List<Vector3> GetCurrentPlantPositions()
    {
        List<Vector3> positions = new List<Vector3>();

        for (int i = spawnedPlants.Count - 1; i >= 0; i--)
        {
            if (spawnedPlants[i] == null)
            {
                spawnedPlants.RemoveAt(i);
            }
            else
            {
                positions.Add(spawnedPlants[i].transform.position);
            }
        }

        return positions;
    }

    /// <summary>
    /// 根据噪声选择植物（支持再生筛选）
    /// </summary>
    private GameObject SelectPlantPrefabByNoise(float noiseValue, float height, float groundAngle, bool isRegeneration = false)
    {
        if (settings.plantPrefabs.Count == 0) return null;

        List<GameObject> suitablePrefabs = new List<GameObject>();
        List<float> weights = new List<float>();

        foreach (var plantData in settings.plantPrefabs)
        {
            if (plantData.prefab == null) continue;

            // 如果是再生模式，检查是否允许再生
            if (isRegeneration && !plantData.allowRegeneration)
                continue;

            // 检查高度
            if (height < plantData.heightRange.x || height > plantData.heightRange.y)
                continue;

            // 检查地面角度
            if (groundAngle > plantData.maxGroundAngle)
                continue;

            // 检查噪声偏好
            if (noiseValue < plantData.preferredNoiseMin || noiseValue > plantData.preferredNoiseMax)
                continue;

            suitablePrefabs.Add(plantData.prefab);

            // 计算权重
            float weight = plantData.spawnProbability;

            // 如果是再生，应用再生概率乘数
            if (isRegeneration)
            {
                weight *= plantData.regenerationProbabilityMultiplier;
            }

            // 根据噪声值在偏好范围内的位置调整权重
            float noiseRange = plantData.preferredNoiseMax - plantData.preferredNoiseMin;
            if (noiseRange > 0)
            {
                float normalizedNoise = (noiseValue - plantData.preferredNoiseMin) / noiseRange;
                weight *= (1f - Mathf.Abs(normalizedNoise - 0.5f) * 2f);
            }

            weights.Add(Mathf.Max(0.1f, weight));
        }

        if (suitablePrefabs.Count == 0) return null;

        // 加权随机选择
        float totalWeight = 0f;
        foreach (float weight in weights) totalWeight += weight;

        if (totalWeight <= 0) return suitablePrefabs[0];

        float randomValue = (float)random.NextDouble() * totalWeight;
        float currentWeight = 0f;

        for (int i = 0; i < suitablePrefabs.Count; i++)
        {
            currentWeight += weights[i];
            if (randomValue <= currentWeight)
            {
                return suitablePrefabs[i];
            }
        }

        return suitablePrefabs[0];
    }

    #endregion

    #region 原始生成方法（保持不变）

    private List<Vector2> GenerateGridPoints()
    {
        List<Vector2> points = new List<Vector2>();

        Vector3 min = settings.generationAreaCenter - settings.generationAreaSize * 0.5f;
        Vector3 max = settings.generationAreaCenter + settings.generationAreaSize * 0.5f;

        float spacing = Mathf.Max(0.1f, settings.gridSpacing);

        for (float x = min.x; x <= max.x; x += spacing)
        {
            for (float z = min.z; z <= max.z; z += spacing)
            {
                float jitterX = (float)(random.NextDouble() * 2 - 1) * spacing * settings.gridJitter;
                float jitterZ = (float)(random.NextDouble() * 2 - 1) * spacing * settings.gridJitter;

                Vector2 point = new Vector2(x + jitterX, z + jitterZ);

                if (IsInBounds(point))
                {
                    points.Add(point);
                }
            }
        }

        return points;
    }

    private List<Vector2> GenerateRandomGridPoints()
    {
        List<Vector2> points = new List<Vector2>();

        Vector3 min = settings.generationAreaCenter - settings.generationAreaSize * 0.5f;
        Vector3 max = settings.generationAreaCenter + settings.generationAreaSize * 0.5f;

        float area = (max.x - min.x) * (max.z - min.z);
        int pointCount = Mathf.CeilToInt(area * settings.baseDensity);

        for (int i = 0; i < pointCount; i++)
        {
            float x = (float)(random.NextDouble() * (max.x - min.x)) + min.x;
            float z = (float)(random.NextDouble() * (max.z - min.z)) + min.z;

            points.Add(new Vector2(x, z));
        }

        return points;
    }

    private List<Vector2> GenerateUniformPoints()
    {
        List<Vector2> points = new List<Vector2>();

        Vector3 min = settings.generationAreaCenter - settings.generationAreaSize * 0.5f;
        Vector3 max = settings.generationAreaCenter + settings.generationAreaSize * 0.5f;

        float area = (max.x - min.x) * (max.z - min.z);
        int pointCount = Mathf.CeilToInt(area * settings.pointsPer100SquareMeters / 100f);

        for (int i = 0; i < pointCount; i++)
        {
            float x = (float)(random.NextDouble() * (max.x - min.x)) + min.x;
            float z = (float)(random.NextDouble() * (max.z - min.z)) + min.z;

            points.Add(new Vector2(x, z));
        }

        return points;
    }

    private List<Vector2> GeneratePoissonPoints()
    {
        List<Vector2> points = new List<Vector2>();

        Vector3 min = settings.generationAreaCenter - settings.generationAreaSize * 0.5f;
        Vector3 max = settings.generationAreaCenter + settings.generationAreaSize * 0.5f;

        float width = max.x - min.x;
        float height = max.z - min.z;
        float radius = Mathf.Max(0.1f, settings.poissonRadius);

        float cellSize = radius / Mathf.Sqrt(2);
        int cols = Mathf.FloorToInt(width / cellSize);
        int rows = Mathf.FloorToInt(height / cellSize);

        if (cols <= 0 || rows <= 0) return points;

        int?[,] grid = new int?[cols, rows];
        List<Vector2> activeList = new List<Vector2>();

        Vector2 firstPoint = new Vector2(
            (float)(random.NextDouble() * width) + min.x,
            (float)(random.NextDouble() * height) + min.z
        );

        points.Add(firstPoint);
        activeList.Add(firstPoint);

        int gridX = Mathf.FloorToInt((firstPoint.x - min.x) / cellSize);
        int gridY = Mathf.FloorToInt((firstPoint.y - min.z) / cellSize);
        grid[gridX, gridY] = 0;

        int attempts = settings.poissonSamples;
        int maxPoints = Mathf.CeilToInt((width * height) / (Mathf.PI * radius * radius));

        while (activeList.Count > 0 && points.Count < maxPoints)
        {
            int activeIndex = random.Next(activeList.Count);
            Vector2 activePoint = activeList[activeIndex];
            bool found = false;

            for (int i = 0; i < attempts; i++)
            {
                float angle = (float)(random.NextDouble() * Mathf.PI * 2);
                float dist = radius + (float)(random.NextDouble() * radius);

                Vector2 newPoint = new Vector2(
                    activePoint.x + Mathf.Cos(angle) * dist,
                    activePoint.y + Mathf.Sin(angle) * dist
                );

                if (!IsInBounds(newPoint)) continue;

                int newGridX = Mathf.FloorToInt((newPoint.x - min.x) / cellSize);
                int newGridY = Mathf.FloorToInt((newPoint.y - min.z) / cellSize);

                if (newGridX < 0 || newGridX >= cols || newGridY < 0 || newGridY >= rows)
                    continue;

                if (IsPoissonPointValid(newPoint, points, grid, min, cellSize, radius))
                {
                    points.Add(newPoint);
                    activeList.Add(newPoint);
                    grid[newGridX, newGridY] = points.Count - 1;
                    found = true;
                    break;
                }
            }

            if (!found)
            {
                activeList.RemoveAt(activeIndex);
            }
        }

        return points;
    }

    private bool IsPoissonPointValid(Vector2 point, List<Vector2> points, int?[,] grid,
                                     Vector3 min, float cellSize, float radius)
    {
        int cellX = Mathf.FloorToInt((point.x - min.x) / cellSize);
        int cellY = Mathf.FloorToInt((point.y - min.z) / cellSize);

        int startX = Mathf.Max(0, cellX - 2);
        int endX = Mathf.Min(grid.GetLength(0) - 1, cellX + 2);
        int startY = Mathf.Max(0, cellY - 2);
        int endY = Mathf.Min(grid.GetLength(1) - 1, cellY + 2);

        for (int x = startX; x <= endX; x++)
        {
            for (int y = startY; y <= endY; y++)
            {
                int? index = grid[x, y];
                if (index.HasValue)
                {
                    Vector2 other = points[index.Value];
                    if (Vector2.Distance(point, other) < radius)
                        return false;
                }
            }
        }

        return true;
    }

    private bool RaycastToGround(Vector3 position, out Vector3 groundPos, out Vector3 groundNormal,
                                  out float groundHeight, out float groundAngle)
    {
        groundPos = position;
        groundNormal = Vector3.up;
        groundHeight = 0f;
        groundAngle = 0f;

        Vector3 rayOrigin = new Vector3(position.x, settings.raycastHeight, position.z);
        Ray ray = new Ray(rayOrigin, Vector3.down);

        if (Physics.Raycast(ray, out RaycastHit hit, settings.raycastDistance, settings.groundLayer))
        {
            groundPos = hit.point;
            groundNormal = hit.normal;
            groundHeight = hit.point.y;
            groundAngle = Vector3.Angle(hit.normal, Vector3.up);
            return true;
        }

        return false;
    }

    private bool IsInBounds(Vector2 point)
    {
        Vector3 min = settings.generationAreaCenter - settings.generationAreaSize * 0.5f;
        Vector3 max = settings.generationAreaCenter + settings.generationAreaSize * 0.5f;

        return point.x >= min.x && point.x <= max.x &&
               point.y >= min.z && point.y <= max.z;
    }

    #endregion

    #region 公共方法

    /// <summary>
    /// 清空植物
    /// </summary>
    public void ClearExistingPlants()
    {
        for (int i = spawnedPlants.Count - 1; i >= 0; i--)
        {
            if (spawnedPlants[i] != null)
            {
                if (Application.isPlaying)
                    Destroy(spawnedPlants[i]);
                else
                    DestroyImmediate(spawnedPlants[i]);
            }
        }
        spawnedPlants.Clear();
        plantCountByPrefab.Clear();
        regenerationCount = 0;
    }

    /// <summary>
    /// 在编辑器中可视化
    /// </summary>
    private void OnDrawGizmosSelected()
    {
        if (settings == null) return;

        Gizmos.color = new Color(0, 1, 0, 0.3f);
        Gizmos.DrawWireCube(settings.generationAreaCenter, settings.generationAreaSize);

        if (settings.showNoisePreview && Application.isPlaying)
        {
            Vector3 min = settings.generationAreaCenter - settings.generationAreaSize * 0.5f;
            Vector3 max = settings.generationAreaCenter + settings.generationAreaSize * 0.5f;

            float cellSize = 1f;

            for (float x = min.x; x <= max.x; x += cellSize)
            {
                for (float z = min.z; z <= max.z; z += cellSize)
                {
                    // 显示初始噪声
                    float noise = CalculatePerlinNoise(new Vector3(x, 0, z), false);
                    Color color = Color.Lerp(settings.lowDensityColor, settings.highDensityColor, noise);
                    Gizmos.color = new Color(color.r, color.g, color.b, 0.5f);
                    Gizmos.DrawCube(new Vector3(x, 0, z), Vector3.one * cellSize * 0.9f);

                    // 显示再生噪声（稍微偏移显示）
                    float regenNoise = CalculatePerlinNoise(new Vector3(x, 0, z), true);
                    Color regenColor = Color.Lerp(Color.blue, Color.yellow, regenNoise);
                    Gizmos.color = new Color(regenColor.r, regenColor.g, regenColor.b, 0.3f);
                    Gizmos.DrawCube(new Vector3(x, 0.1f, z), Vector3.one * cellSize * 0.4f);
                }
            }
        }

        if (settings.showGenerationPoints && spawnedPlants.Count > 0)
        {
            Gizmos.color = Color.yellow;
            foreach (var plant in spawnedPlants)
            {
                if (plant != null)
                {
                    var tracker = plant.GetComponent<PlantTracker>();
                    if (tracker != null && tracker.isRegenerated)
                    {
                        Gizmos.color = Color.blue; // 再生植物用蓝色
                    }
                    else
                    {
                        Gizmos.color = Color.yellow; // 初始植物用黄色
                    }
                    Gizmos.DrawWireSphere(plant.transform.position, 0.2f);
                }
            }
        }
    }

    /// <summary>
    /// 获取当前植物数量
    /// </summary>
    public int GetPlantCount()
    {
        CleanupDestroyedPlants();
        return spawnedPlants.Count;
    }

    /// <summary>
    /// 获取指定植物的数量
    /// </summary>
    public int GetPlantCount(GameObject prefab)
    {
        if (prefab == null) return 0;
        return GetPlantCountByPrefab(prefab);
    }

    /// <summary>
    /// 获取再生次数
    /// </summary>
    public int GetRegenerationCount()
    {
        return regenerationCount;
    }

    /// <summary>
    /// 手动设置生成区域
    /// </summary>
    public void SetGenerationArea(Vector3 center, Vector3 size)
    {
        if (settings != null)
        {
            settings.generationAreaCenter = center;
            settings.generationAreaSize = size;
        }
    }

    /// <summary>
    /// 重新生成植被
    /// </summary>
    public void Regenerate()
    {
        StartCoroutine(GeneratePlantsCoroutine());
    }

    /// <summary>
    /// 设置植物的目标数量
    /// </summary>
    public void SetPlantTargetCount(GameObject prefab, int targetCount)
    {
        if (prefab == null) return;

        foreach (var plantData in settings.plantPrefabs)
        {
            if (plantData.prefab == prefab)
            {
                plantData.regenerationTargetCount = targetCount;

                if (plantTargetCountByPrefab.ContainsKey(prefab))
                {
                    plantTargetCountByPrefab[prefab] = targetCount;
                }

                if (showDebugInfo)
                {
                    Debug.Log($"设置植物 {prefab.name} 的目标数量为: {targetCount}");
                }
                break;
            }
        }
    }

    /// <summary>
    /// 启用或禁用再生功能
    /// </summary>
    public void SetRegenerationEnabled(bool enabled)
    {
        if (settings != null)
        {
            settings.enableRegeneration = enabled;

            if (enabled && !isRegenerating)
            {
                StartRegeneration();
            }
            else if (!enabled)
            {
                StopRegeneration();
            }

            if (showDebugInfo)
            {
                Debug.Log($"再生功能已{(enabled ? "启用" : "禁用")}");
            }
        }
    }

    /// <summary>
    /// 检查是否正在再生
    /// </summary>
    public bool IsRegenerating()
    {
        return isRegenerating;
    }

    /// <summary>
    /// 强制立即尝试再生植物
    /// </summary>
    public void ForceRegenerate(int attempts = 10)
    {
        if (!settings.enableRegeneration)
        {
            Debug.LogWarning("再生功能未启用");
            return;
        }

        StartCoroutine(ForceRegenerationCoroutine(attempts));
    }

    private IEnumerator ForceRegenerationCoroutine(int attempts)
    {
        isRegenerating = true;

        try
        {
            int generated = 0;
            List<Vector3> placedPositions = GetCurrentPlantPositions();

            for (int i = 0; i < attempts; i++)
            {
                Vector3 randomPosition = GenerateRandomPositionInArea();

                if (TryPlacePlantAtPosition(randomPosition, placedPositions, true))
                {
                    placedPositions.Add(randomPosition);
                    generated++;
                }

                if (i % settings.maxPlantsPerFrame == 0)
                {
                    yield return null;
                }
            }

            if (showDebugInfo)
            {
                Debug.Log($"强制再生完成，成功生成了 {generated} 棵植物");
            }
        }
        finally
        {
            isRegenerating = false;
        }
    }

    /// <summary>
    /// 设置再生噪声偏移
    /// </summary>
    public void SetRegenerationNoiseOffset(Vector2 offset)
    {
        if (settings != null)
        {
            settings.regenerationNoiseOffset = offset;

            if (showDebugInfo)
            {
                Debug.Log($"设置再生噪声偏移为: {offset}");
            }
        }
    }

    #endregion
}

/// <summary>
/// 植物跟踪组件
/// </summary>
public class PlantTracker : MonoBehaviour
{
    public GameObject prefab;
    public bool allowRegeneration = true;
    public int regenerationTargetCount = 0;
    public bool isRegenerated = false;
}