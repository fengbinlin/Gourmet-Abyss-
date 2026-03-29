using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 宠物管理器：
/// 1) 配置宠物预制体与成长数值
/// 2) 进入战斗时读取 WeaponStatsManager 里的宠物状态（enum + bool）
/// 3) 生成启用的宠物预制体，并把成长数值写入到“宠物系统”组件（IPetSystem）
/// </summary>
[DefaultExecutionOrder(90)]
public class PetManager : MonoBehaviour
{
    public static PetManager Instance { get; private set; }

    [System.Serializable]
    public class PetConfigEntry
    {
        public PetType petType = PetType.None;
        public GameObject petPrefab;
        public Vector3 spawnOffset = Vector3.zero; // 相对“生成点/玩家”的偏移
    }

    [Header("生成点（为空则使用玩家位置）")]
    [SerializeField] private Transform petSpawnPoint;

    [Header("宠物配置列表（只配置预制体与偏移）")]
    [SerializeField] private List<PetConfigEntry> petConfigs = new List<PetConfigEntry>();

    [Header("宠物成长数值（不在 PetConfig 内；按类型前缀命名）")]
    [SerializeField] private float FlyingCompanion_attackRange = 12f;
    [SerializeField] private float FlyingCompanion_fireInterval = 0.45f;
    [SerializeField] private float FlyingCompanion_bulletDamage = 12f;

    [Header("FlyingCompanion 子弹表现")]
    [SerializeField] private float FlyingCompanion_bulletSize = 1f;
    [SerializeField] private int FlyingCompanion_burstBulletCount = 1;
    [SerializeField] private float FlyingCompanion_burstFanAngle = 0f; // degrees
    [Header("FlyingCompanion 命中减速")]
    [SerializeField] private float FlyingCompanion_slowRatioBase = 0.2f;
    [SerializeField] private float FlyingCompanion_slowDurationBase = 1.5f;
    [SerializeField] private float FlyingCompanion_slowRatioMultiplier = 1f;
    [SerializeField] private float FlyingCompanion_slowDurationMultiplier = 1f;

    [SerializeField] private float FlyingCompanion_bulletMoveSpeed = 18f;
    [SerializeField] private float FlyingCompanion_bulletRotateSpeed = 540f;
    [SerializeField] private float FlyingCompanion_bulletLifeTime = 4f;
    [SerializeField] private float FlyingCompanion_bulletHitDistance = 0.35f;

    [System.Serializable]
    private struct FlyingCompanionInitialSnapshot
    {
        public float attackRange;
        public float fireInterval;
        public float bulletDamage;
        public float bulletSize;
        public int burstBulletCount;
        public float burstFanAngle;
        public float slowRatioBase;
        public float slowDurationBase;
        public float slowRatioMultiplier;
        public float slowDurationMultiplier;
        public float bulletMoveSpeed;
        public float bulletRotateSpeed;
        public float bulletLifeTime;
        public float bulletHitDistance;
    }

    private FlyingCompanionInitialSnapshot _fcInitial;
    private bool _fcInitialCaptured;

    // 运行时：记录已生成的宠物实例
    private readonly Dictionary<PetType, GameObject> spawnedPets = new Dictionary<PetType, GameObject>();
    private PlayerState lastState = PlayerState.UpGround;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        CaptureFlyingCompanionInitialIfNeeded();
    }

    /// <summary>
    /// 初始数值表写入单条（52–65）；不会在内部刷新快照，请最后调用 <see cref="RefreshFlyingCompanionInitialSnapshot"/>。
    /// </summary>
    public void ApplyFlyingCompanionTableValue(int statID, float value)
    {
        switch (statID)
        {
            case 52: FlyingCompanion_attackRange = Mathf.Max(0.01f, value); break;
            case 53: FlyingCompanion_fireInterval = Mathf.Max(0.01f, value); break;
            case 54: FlyingCompanion_bulletDamage = Mathf.Max(0f, value); break;
            case 55: FlyingCompanion_bulletSize = Mathf.Max(0.01f, value); break;
            case 56: FlyingCompanion_burstBulletCount = Mathf.Max(1, Mathf.RoundToInt(value)); break;
            case 57: FlyingCompanion_burstFanAngle = value; break;
            case 58: FlyingCompanion_slowRatioBase = Mathf.Clamp01(value); break;
            case 59: FlyingCompanion_slowDurationBase = Mathf.Max(0.01f, value); break;
            case 60: FlyingCompanion_slowRatioMultiplier = Mathf.Max(0.01f, value); break;
            case 61: FlyingCompanion_slowDurationMultiplier = Mathf.Max(0.01f, value); break;
            case 62: FlyingCompanion_bulletMoveSpeed = Mathf.Max(0.01f, value); break;
            case 63: FlyingCompanion_bulletRotateSpeed = Mathf.Max(1f, value); break;
            case 64: FlyingCompanion_bulletLifeTime = Mathf.Max(0.01f, value); break;
            case 65: FlyingCompanion_bulletHitDistance = Mathf.Max(0.01f, value); break;
        }
    }

    /// <summary>
    /// 在初始表覆盖完 52–65 后调用，使技能树 buff 的「基准值」与表一致。
    /// </summary>
    public void RefreshFlyingCompanionInitialSnapshot()
    {
        _fcInitialCaptured = false;
        CaptureFlyingCompanionInitialIfNeeded();
    }

    /// <summary>供 SkillTreeInitializer 在缺少 CSV 时回退读取当前飞行跟班数值。</summary>
    public float GetFlyingCompanionStatValue(int statID)
    {
        switch (statID)
        {
            case 52: return FlyingCompanion_attackRange;
            case 53: return FlyingCompanion_fireInterval;
            case 54: return FlyingCompanion_bulletDamage;
            case 55: return FlyingCompanion_bulletSize;
            case 56: return FlyingCompanion_burstBulletCount;
            case 57: return FlyingCompanion_burstFanAngle;
            case 58: return FlyingCompanion_slowRatioBase;
            case 59: return FlyingCompanion_slowDurationBase;
            case 60: return FlyingCompanion_slowRatioMultiplier;
            case 61: return FlyingCompanion_slowDurationMultiplier;
            case 62: return FlyingCompanion_bulletMoveSpeed;
            case 63: return FlyingCompanion_bulletRotateSpeed;
            case 64: return FlyingCompanion_bulletLifeTime;
            case 65: return FlyingCompanion_bulletHitDistance;
            default: return 0f;
        }
    }

    private void CaptureFlyingCompanionInitialIfNeeded()
    {
        if (_fcInitialCaptured) return;
        _fcInitial = new FlyingCompanionInitialSnapshot
        {
            attackRange = FlyingCompanion_attackRange,
            fireInterval = FlyingCompanion_fireInterval,
            bulletDamage = FlyingCompanion_bulletDamage,
            bulletSize = FlyingCompanion_bulletSize,
            burstBulletCount = FlyingCompanion_burstBulletCount,
            burstFanAngle = FlyingCompanion_burstFanAngle,
            slowRatioBase = FlyingCompanion_slowRatioBase,
            slowDurationBase = FlyingCompanion_slowDurationBase,
            slowRatioMultiplier = FlyingCompanion_slowRatioMultiplier,
            slowDurationMultiplier = FlyingCompanion_slowDurationMultiplier,
            bulletMoveSpeed = FlyingCompanion_bulletMoveSpeed,
            bulletRotateSpeed = FlyingCompanion_bulletRotateSpeed,
            bulletLifeTime = FlyingCompanion_bulletLifeTime,
            bulletHitDistance = FlyingCompanion_bulletHitDistance
        };
        _fcInitialCaptured = true;
    }

    /// <summary>
    /// 技能树 buff：FlyingCompanion 成长（statID 52–65），与 SkillTreeInitializer 约定一致。
    /// </summary>
    public void ApplyFlyingCompanionBuffStat(int statID, float value, int skillLevel)
    {
        CaptureFlyingCompanionInitialIfNeeded();
        int L = Mathf.Max(0, skillLevel);
        float init;
        switch (statID)
        {
            case 52:
                init = _fcInitial.attackRange;
                FlyingCompanion_attackRange = init * (1f + value * L);
                break;
            case 53:
                init = _fcInitial.fireInterval;
                FlyingCompanion_fireInterval = Mathf.Max(0.01f, init * (1f + value * L));
                break;
            case 54:
                init = _fcInitial.bulletDamage;
                FlyingCompanion_bulletDamage = Mathf.Max(0f, init * (1f + value * L));
                break;
            case 55:
                init = _fcInitial.bulletSize;
                FlyingCompanion_bulletSize = Mathf.Max(0.01f, init * (1f + value * L));
                break;
            case 56:
                FlyingCompanion_burstBulletCount = Mathf.Max(1, FlyingCompanion_burstBulletCount + (int)value);
                break;
            case 57:
                init = _fcInitial.burstFanAngle;
                FlyingCompanion_burstFanAngle = init * (1f + value * L);
                break;
            case 58:
                init = _fcInitial.slowRatioBase;
                FlyingCompanion_slowRatioBase = Mathf.Clamp01(init * (1f + value * L));
                break;
            case 59:
                init = _fcInitial.slowDurationBase;
                FlyingCompanion_slowDurationBase = Mathf.Max(0.01f, init * (1f + value * L));
                break;
            case 60:
                init = _fcInitial.slowRatioMultiplier;
                FlyingCompanion_slowRatioMultiplier = Mathf.Max(0.01f, init * (1f + value * L));
                break;
            case 61:
                init = _fcInitial.slowDurationMultiplier;
                FlyingCompanion_slowDurationMultiplier = Mathf.Max(0.01f, init * (1f + value * L));
                break;
            case 62:
                init = _fcInitial.bulletMoveSpeed;
                FlyingCompanion_bulletMoveSpeed = Mathf.Max(0.01f, init * (1f + value * L));
                break;
            case 63:
                init = _fcInitial.bulletRotateSpeed;
                FlyingCompanion_bulletRotateSpeed = Mathf.Max(1f, init * (1f + value * L));
                break;
            case 64:
                init = _fcInitial.bulletLifeTime;
                FlyingCompanion_bulletLifeTime = Mathf.Max(0.01f, init * (1f + value * L));
                break;
            case 65:
                init = _fcInitial.bulletHitDistance;
                FlyingCompanion_bulletHitDistance = Mathf.Max(0.01f, init * (1f + value * L));
                break;
            default:
                return;
        }

        SyncPetsForBattleNow();
    }

    private void Start()
    {
        if (PlayerStateManager.instance != null)
            lastState = PlayerStateManager.instance.currentState;

        SyncByState(force: true);
    }

    private void Update()
    {
        if (PlayerStateManager.instance == null) return;

        var currentState = PlayerStateManager.instance.currentState;
        if (currentState == lastState) return;

        lastState = currentState;
        SyncByState(force: false);
    }

    /// <summary>
    /// 剧情/事件用途：在当前仍处于战斗状态时，立刻根据 WeaponStatsManager 重新生成（或同步）已启用的宠物。
    /// </summary>
    public void SyncPetsForBattleNow()
    {
        if (PlayerStateManager.instance == null) return;
        if (PlayerStateManager.instance.currentState != PlayerState.Battle) return;

        EnterBattle();
    }

    private void SyncByState(bool force)
    {
        if (PlayerStateManager.instance == null) return;

        if (force || PlayerStateManager.instance.currentState == PlayerState.Battle)
        {
            if (PlayerStateManager.instance.currentState == PlayerState.Battle)
                EnterBattle();
        }
        else
        {
            ExitBattle();
        }
    }

    private void EnterBattle()
    {
        if (WeaponStatsManager.Instance == null)
        {
            Debug.LogWarning("[PetManager] WeaponStatsManager.Instance 为空，跳过宠物生成。");
            return;
        }

        // 生成旋转时尽量对齐玩家朝向（如果找不到玩家则用默认旋转）
        Transform playerTransform = null;
        var player = FindFirstObjectByType<TopDownController>();
        if (player != null) playerTransform = player.transform;

        for (int i = 0; i < petConfigs.Count; i++)
        {
            var cfg = petConfigs[i];
            if (cfg == null) continue;
            if (cfg.petType == PetType.None) continue;
            if (cfg.petPrefab == null) continue;

            bool enabled = WeaponStatsManager.Instance.IsPetEnabled(cfg.petType);
            if (!enabled) continue;

            if (spawnedPets.TryGetValue(cfg.petType, out var existing) && existing != null)
            {
                // 如果之前已生成（比如重复进入战斗），也同步一次成长数值
                var petSystem = existing.GetComponentInChildren<IPetSystem>();
                petSystem?.ApplyGrowth(GetGrowthValuesForPetType(cfg.petType));
                continue;
            }

            Vector3 spawnPos = GetSpawnPosition(playerTransform) + cfg.spawnOffset;
            Quaternion spawnRot = playerTransform != null
                ? Quaternion.Euler(0f, playerTransform.eulerAngles.y, 0f)
                : Quaternion.identity;

            GameObject petObj = Instantiate(cfg.petPrefab, spawnPos, spawnRot);
            spawnedPets[cfg.petType] = petObj;

            var system = petObj.GetComponentInChildren<IPetSystem>();
            if (system == null)
            {
                Debug.LogWarning($"[PetManager] 宠物预制体 {cfg.petPrefab.name} 未实现 IPetSystem，无法写入成长数值。");
                continue;
            }
            system.ApplyGrowth(GetGrowthValuesForPetType(cfg.petType));
        }
    }

    private void ExitBattle()
    {
        // 离开战斗时清理，避免重复进入战斗导致多次生成
        foreach (var kv in spawnedPets)
        {
            if (kv.Value != null)
            {
                Destroy(kv.Value);
            }
        }
        spawnedPets.Clear();
    }

    private Vector3 GetSpawnPosition(Transform playerTransform)
    {
        if (petSpawnPoint != null)
            return petSpawnPoint.position;

        if (playerTransform != null)
            return playerTransform.position;

        return Vector3.zero;
    }

    private PetGrowthValues GetGrowthValuesForPetType(PetType petType)
    {
        switch (petType)
        {
            case PetType.FlyingCompanion:
                return new PetGrowthValues
                {
                    attackRange = FlyingCompanion_attackRange,
                    fireInterval = FlyingCompanion_fireInterval,
                    bulletDamage = FlyingCompanion_bulletDamage,

                    bulletSize = FlyingCompanion_bulletSize,
                    burstBulletCount = Mathf.Max(1, FlyingCompanion_burstBulletCount),
                    burstFanAngle = FlyingCompanion_burstFanAngle,

                    bulletMoveSpeed = FlyingCompanion_bulletMoveSpeed,
                    bulletRotateSpeed = FlyingCompanion_bulletRotateSpeed,
                    bulletLifeTime = FlyingCompanion_bulletLifeTime,
                    bulletHitDistance = FlyingCompanion_bulletHitDistance,

                    slowRatioBase = FlyingCompanion_slowRatioBase,
                    slowDurationBase = FlyingCompanion_slowDurationBase,
                    slowRatioMultiplier = FlyingCompanion_slowRatioMultiplier,
                    slowDurationMultiplier = FlyingCompanion_slowDurationMultiplier
                };
            default:
                return null;
        }
    }
}

