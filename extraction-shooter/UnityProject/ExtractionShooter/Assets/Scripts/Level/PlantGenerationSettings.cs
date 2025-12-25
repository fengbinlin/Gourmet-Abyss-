using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "PlantGenerationSettings", menuName = "植被生成/生成设置")]
public class PlantGenerationSettings : ScriptableObject
{
    [System.Serializable]
    public class PlantPrefabData
    {
        [Header("预制体")]
        public GameObject prefab;
        
        [Header("生成概率")]
        [Range(0f, 1f)] public float spawnProbability = 0.5f;
        
        [Header("缩放范围")]
        [Min(0.1f)] public float minScale = 0.8f;
        [Min(0.1f)] public float maxScale = 1.2f;
        
        [Header("高度范围")]
        public Vector2 heightRange = new Vector2(-1000f, 1000f);
        
        [Header("地面角度")]
        [Range(0f, 90f)] public float maxGroundAngle = 60f;
        
        [Header("噪声偏好")]
        [Tooltip("植物偏好的噪声值范围")]
        [Range(0f, 1f)] public float preferredNoiseMin = 0f;
        [Range(0f, 1f)] public float preferredNoiseMax = 1f;
        
        [Header("集群生成")]
        [Tooltip("是否允许集群生成")]
        public bool allowClustering = false;
        [Min(1)] public int clusterMin = 1;
        [Min(1)] public int clusterMax = 3;
        
        [Header("后续生成")]
        [Tooltip("是否允许在后续生成中出现")]
        public bool allowRegeneration = true;
        [Tooltip("再生时的生成概率（相对于初始概率）")]
        [Range(0f, 2f)] public float regenerationProbabilityMultiplier = 1f;
        
        [Header("再生目标数量")]
        [Tooltip("该植物的目标数量（0表示不限制）")]
        [Min(0)] public int regenerationTargetCount = 0;
    }
    
    [Header("=== 植物列表 ===")]
    public List<PlantPrefabData> plantPrefabs = new List<PlantPrefabData>();
    
    [Header("=== 生成区域 ===")]
    public Vector3 generationAreaCenter = Vector3.zero;
    public Vector3 generationAreaSize = new Vector3(100f, 0f, 100f);
    
    [Header("=== 柏林噪声设置 ===")]
    [Tooltip("噪声缩放（值越小，噪声特征越大）")]
    [Range(0.001f, 0.5f)] public float noiseScale = 0.1f;
    
    [Tooltip("噪声偏移（避免重复）")]
    public Vector2 noiseOffset = Vector2.zero;
    
    [Tooltip("噪声层数（分形布朗运动）")]
    [Range(1, 8)] public int octaves = 3;
    
    [Tooltip("持久性（每层强度衰减）")]
    [Range(0.1f, 1f)] public float persistence = 0.5f;
    
    [Tooltip("间隙（每层频率增加）")]
    [Range(1f, 4f)] public float lacunarity = 2f;
    
    [Header("=== 再生噪声设置 ===")]
    [Tooltip("再生噪声偏移量（与初始生成不同）")]
    public Vector2 regenerationNoiseOffset = new Vector2(1000f, 1000f);
    
    [Header("=== 密度控制 ===")]
    [Tooltip("基础密度（0-1）")]
    [Range(0f, 1f)] public float baseDensity = 0.3f;
    
    [Tooltip("密度曲线（控制噪声到密度的映射）")]
    public AnimationCurve densityCurve = AnimationCurve.Linear(0, 0, 1, 1);
    
    [Tooltip("噪声乘数（值越大，噪声影响越大）")]
    [Range(0f, 2f)] public float noiseInfluence = 1f;
    
    [Tooltip("最小放置噪声阈值")]
    [Range(0f, 1f)] public float minNoiseThreshold = 0f;
    
    [Tooltip("最大放置噪声阈值")]
    [Range(0f, 1f)] public float maxNoiseThreshold = 1f;
    
    [Header("=== 生成模式 ===")]
    public GenerationMode generationMode = GenerationMode.Poisson;
    public enum GenerationMode { Grid, Poisson, RandomGrid, Uniform }
    
    [Header("=== 网格设置（Grid模式）===")]
    [Tooltip("网格大小")]
    [Min(0.1f)] public float gridSpacing = 2f;
    
    [Tooltip("随机偏移比例")]
    [Range(0f, 1f)] public float gridJitter = 0.5f;
    
    [Header("=== Poisson圆盘采样 ===")]
    [Tooltip("最小间距")]
    [Min(0.1f)] public float poissonRadius = 1.5f;
    
    [Tooltip("采样尝试次数")]
    [Range(1, 100)] public int poissonSamples = 30;
    
    [Header("=== 均匀随机 ===")]
    [Tooltip("生成点数量（每100平方米）")]
    [Min(1)] public int pointsPer100SquareMeters = 100;
    
    [Header("=== 地形适配 ===")]
    public LayerMask groundLayer = 1 << 0;
    [Min(1f)] public float raycastHeight = 100f;
    [Min(1f)] public float raycastDistance = 200f;
    public bool alignToGroundNormal = true;
    
    [Header("=== 后续生成设置 ===")]
    [Tooltip("是否启用后续生成功能")]
    public bool enableRegeneration = true;
    
    [Tooltip("再生检查间隔（秒）")]
    [Min(0.1f)] public float regenerationCheckInterval = 1f;
    
    [Tooltip("每次再生尝试的次数")]
    [Min(1)] public int regenerationAttemptsPerCycle = 20;
    
    [Tooltip("再生时使用初始密度比例")]
    [Range(0f, 1f)] public float regenerationDensityMultiplier = 0.5f;
    
    [Tooltip("再生最小距离（避免与现有植物太近）")]
    [Min(0.1f)] public float regenerationMinDistance = 1f;
    
    [Header("=== 随机种子 ===")]
    public int seed = 12345;
    public bool useRandomSeed = true;
    
    [Header("=== 性能优化 ===")]
    [Range(1, 1000)] public int maxPlantsPerFrame = 100;
    [Tooltip("开启集群优化（减少射线检测）")]
    public bool useClusterOptimization = false;
    
    [Header("=== 调试与可视化 ===")]
    public bool showNoisePreview = false;
    public bool showGenerationPoints = false;
    public Color lowDensityColor = Color.red;
    public Color highDensityColor = Color.green;
    
    [Header("=== 快速预设 ===")]
    [Tooltip("快速应用预设")]
    public DensityPreset densityPreset = DensityPreset.Medium;
    public enum DensityPreset { Sparse, Medium, Dense, VeryDense, Custom }
    
    // 当属性变化时自动应用预设
    private void OnValidate()
    {
        ApplyDensityPreset();
    }
    
    private void ApplyDensityPreset()
    {
        if (densityPreset == DensityPreset.Custom) return;
        
        switch (densityPreset)
        {
            case DensityPreset.Sparse:
                baseDensity = 0.1f;
                gridSpacing = 5f;
                poissonRadius = 3f;
                pointsPer100SquareMeters = 20;
                minNoiseThreshold = 0.6f;
                maxNoiseThreshold = 1f;
                break;
                
            case DensityPreset.Medium:
                baseDensity = 0.3f;
                gridSpacing = 3f;
                poissonRadius = 2f;
                pointsPer100SquareMeters = 50;
                minNoiseThreshold = 0.4f;
                maxNoiseThreshold = 0.9f;
                break;
                
            case DensityPreset.Dense:
                baseDensity = 0.6f;
                gridSpacing = 1.5f;
                poissonRadius = 1.2f;
                pointsPer100SquareMeters = 100;
                minNoiseThreshold = 0.2f;
                maxNoiseThreshold = 0.8f;
                break;
                
            case DensityPreset.VeryDense:
                baseDensity = 0.9f;
                gridSpacing = 1f;
                poissonRadius = 0.8f;
                pointsPer100SquareMeters = 200;
                minNoiseThreshold = 0f;
                maxNoiseThreshold = 0.6f;
                break;
        }
    }
}