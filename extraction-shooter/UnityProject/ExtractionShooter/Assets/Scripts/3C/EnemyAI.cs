using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public enum EnemyState
{
    Idle,           // 闲置状态
    Patrol,         // 巡逻状态
    LookAround,     // 四处张望状态
    Hit,            // 受击状态
    ChasePlayer,    // 2D 攻击性：追击玩家
    MeleeAttack,    // 2D 攻击性：近战攻击
    Dead            // 死亡状态
}

public class EnemyAI : MonoBehaviour
{
    [Header("移动设置")]
    [SerializeField] private float moveSpeed = 3.5f;           // 移动速度
    [SerializeField] private float patrolRadius = 10f;           // 巡逻半径
    [SerializeField] private float minPatrolDistance = 3f;      // 最小巡逻距离
    [SerializeField] private float stoppingDistance = 0.5f;     // 停止距离
    [SerializeField] private float raycastDistance = 5f;        // 射线检测距离
    
    [Header("层设置")]
    [SerializeField] private LayerMask walkableLayer;           // 可行走的地板层
    [SerializeField] private LayerMask obstacleLayer;          // 障碍物层
    
    [Header("动画设置")]
    [SerializeField] private Animator animator;
    [SerializeField] private string idleAnimationState = "Idle";
    [SerializeField] private string walkAnimationState = "Walk";
    [SerializeField] private string lookAroundAnimationState = "LookAround";
    [SerializeField] private float animationTransitionTime = 0.1f;  // 动画过渡时间
    
    [Tooltip("勾选后：移动时不旋转模型，使用下方 2D 状态名（idle / 左右移动）驱动 Animator，而非 IsWalking 布尔参数。")]
    [SerializeField] private bool use2DFrameAnimation;
    [Tooltip("为 true 时用 CrossFade 混合；为 false 时用 Play（不依赖状态间连线，更容易播出来）。")]
    [SerializeField] private bool useCrossFadeFor2D;
    [SerializeField] private string spriteIdleStateName = "idle";
    [SerializeField] private string spriteMoveLeftStateName = "moveleft";
    [Tooltip("走路动画（朝右）。勾选「单方向走路」时只用此状态 + 旋转节点翻面，不再用 moveleft。")]
    [SerializeField] private string spriteMoveRightStateName = "moveright";
    
    [Tooltip("仅一套向右走路动画时勾选：巡逻时只播「朝右走路」状态，向左时旋转 spriteVisualTransform 翻面，不再切换 moveleft。")]
    [SerializeField] private bool singleDirectionWalkRotateSpriteForLeft;
    [Tooltip("用于翻面的节点（通常为挂 SpriteRenderer 的子物体 Transform）。朝右姿态以场景里该物体的 localRotation 为准（运行时 Awake/Start 会缓存）。")]
    [SerializeField] private Transform spriteVisualTransform;
    [Tooltip("向左时：在「朝右」预设姿态上，绕 sprite 本地该轴再转 180°。常见 (0,1,0) 绕本地 Y；侧视纸片也可试 (0,0,1) 绕本地 Z。")]
    [SerializeField] private Vector3 spriteFlipLocalAxis = Vector3.up;
    
    [Header("2D 攻击性（需开启 2D 帧动画）")]
    [Tooltip("勾选后：玩家在警戒半径内会追击并近战；否则行为与原来一致。")]
    [SerializeField] private bool aggressive2D;
    [SerializeField] private float alertRange = 8f;
    [Tooltip("0 = 自动为 alertRange + 2。追击时水平距离超过本值才脱战（应大于警戒半径），避免在边界反复进出、体感上要多次进范围才追击。")]
    [SerializeField] private float chaseAggroExitRange;
    [SerializeField] private float attackRange = 1.35f;
    [Tooltip("额外放宽：根节点水平距离 ≤ attackRange + 本值 即可进入近战（解决贴脸仍略大于 attackRange、或轴心距与体感不符）。")]
    [SerializeField] private float attackRangeExtraMargin = 0.35f;
    [SerializeField] private float attackCooldown = 1.1f;
    [Tooltip("读不到 Animator 攻击状态长度时的后备秒数。正常情况以当前攻击状态/Clip 实际长度为准，播满 100% 才结束近战。")]
    [SerializeField] private float attackAnimDuration = 0.45f;
    [Tooltip("在「整段攻击动画」的该比例处结算伤害与 slash；默认 0.8。之后仍会播完剩余部分再切回追击/张望。")]
    [SerializeField, Range(0f, 1f)] private float attackHitNormalizedTime = 0.8f;
    [SerializeField] private string spriteAttackStateName = "attack";
    [SerializeField] private GameObject slashVfxPrefab;
    [Tooltip("slash 生成在自身位置沿朝向玩家方向前移的距离。")]
    [SerializeField] private float slashForwardOffset = 0.75f;
    [Tooltip("命中判定时，与玩家水平距离不超过 attackRange * 该系数才扣饥饿。")]
    [SerializeField] private float attackHitDistanceMultiplier = 1.35f;
    [Tooltip("命中时通过 BattleValManager 扣除的氧气量（即饥饿/生存条）。")]
    [SerializeField] private float hungerDamageOnMeleeHit = 8f;
    
    [Header("行为时间设置")]
    [SerializeField] private float minLookAroundTime = 1.5f;    // 最小张望时间
    [SerializeField] private float maxLookAroundTime = 3f;      // 最大张望时间
    [SerializeField] private float minIdleTime = 0.5f;          // 最小闲置时间
    [SerializeField] private float maxIdleTime = 1.5f;          // 最大闲置时间
    [SerializeField] private float hitRecoveryTime = 0.8f;      // 受击后恢复时间
    
    [Header("调试设置")]
    [SerializeField] private bool showDebugInfo = true;         // 显示调试信息
    [Header("调试：玩家距离与未追击/未攻击原因")]
    [Tooltip("勾选后：打印与玩家水平距离；若当前不是追击/近战状态，用 LogWarning 说明原因（带节流）。")]
    [SerializeField] private bool debugLogPlayerAggro;
    [SerializeField] private float debugPlayerAggroLogInterval = 0.25f;
    [SerializeField] private Color patrolPointColor = Color.blue;
    [SerializeField] private Color raycastColor = Color.green;
    [SerializeField] private Color blockedRayColor = Color.red;
    
    [Header("AI设置")]
    [SerializeField] private float raycastHeight = 0.2f;       // 射线发射的高度偏移

    [Header("减速Debuff（运行时）")]
    [SerializeField, Range(0f, 1f)] private float currentSlowRatio = 0f; // 0.2 表示减速20%
    [SerializeField] private float slowEndTime = 0f;
    [SerializeField, Range(0.05f, 1f)] private float minMoveSpeedRatio = 0.15f; // 减速后最低保留速度比例（避免完全定身）
    
    // 八个方向向量
    private static readonly Vector3[] Directions = new Vector3[]
    {
        Vector3.forward,           // 前
        Vector3.back,              // 后
        Vector3.left,              // 左
        Vector3.right,             // 右
        (Vector3.forward + Vector3.left).normalized,    // 左前
        (Vector3.forward + Vector3.right).normalized,   // 右前
        (Vector3.back + Vector3.left).normalized,      // 左后
        (Vector3.back + Vector3.right).normalized       // 右后
    };

    // 私有变量
    private EnemyState currentState = EnemyState.Idle;
    private Vector3 currentPatrolPoint = Vector3.zero;
    private Vector3 moveDirection = Vector3.zero;
    private float lookAroundTimer = 0f;
    private float idleTimer = 0f;
    private float hitTimer = 0f;
    private float patrolTimer = 0f;
    private float patrolDuration = 0f;
    private bool hasReachedDestination = false;
    private EnemyHealth enemyHealth;
    private Vector3 spawnPosition; // 记录生成位置
    private float baseMoveSpeed;
    
    // 动画参数哈希
    private int isWalkingHash;
    private int isLookingAroundHash;
    
    /// <summary>2D 模式下最近一次水平移动方向符号（-1 左，1 右），用于 direction.x≈0 时保持朝向。</summary>
    private float last2DHorizontalSign = 1f;
    
    /// <summary>2D 模式下脚本上次请求播放的状态名（避免用 IsName 与 Animator 不同步导致不切换或每帧重播）。</summary>
    private string lastRequested2DAnimState;
    
    /// <summary>spriteVisualTransform 在「朝右」时的本地旋转（场景预设），向左时在它基础上绕本地轴再转 180°。</summary>
    private Quaternion spriteVisualBaseLocalRotation = Quaternion.identity;
    
    private Transform playerTransform;
    private float meleeAttackTimer;
    private bool meleeHitResolved;
    private float nextMeleeAttackAllowedTime;
    /// <summary>本轮近战：攻击状态完整时长（秒），进入时从 Animator 读取；用于 80% 判定与 100% 再退场。</summary>
    private float meleeAttackFullDuration;
    
    private float lastDebugPlayerDistLogTime;
    private float lastDebugAggroWarnTime;
    
    private void Awake()
    {
        // 获取组件
        if (animator == null)
            animator = GetComponent<Animator>();
            
        enemyHealth = GetComponent<EnemyHealth>();
        baseMoveSpeed = moveSpeed;
        
        // 记录生成位置
        spawnPosition = transform.position;
        
        // 缓存动画参数哈希
        isWalkingHash = Animator.StringToHash("IsWalking");
        isLookingAroundHash = Animator.StringToHash("IsLookingAround");
        
        CacheSpriteVisualBaseLocalRotation();
        CachePlayerTransform();
    }
    
    private void Start()
    {
        CacheSpriteVisualBaseLocalRotation();
        CachePlayerTransform();
        // 初始状态为闲置
        SetState(EnemyState.Idle);
    }
    
    private void Update()
    {
        // 根据当前状态执行对应逻辑
        switch (currentState)
        {
            case EnemyState.Idle:
                UpdateIdleState();
                break;
                
            case EnemyState.Patrol:
                UpdatePatrolState();
                break;
                
            case EnemyState.LookAround:
                UpdateLookAroundState();
                break;
                
            case EnemyState.Hit:
                UpdateHitState();
                break;
                
            case EnemyState.ChasePlayer:
                UpdateChasePlayerState();
                break;
                
            case EnemyState.MeleeAttack:
                UpdateMeleeAttackState();
                break;
                
            case EnemyState.Dead:
                // 死亡状态不执行任何操作
                break;
        }
        
        TryBeginChaseIfPlayerInAlert();
        
        // 更新动画
        UpdateAnimations();
        Apply2DSpriteFacingRotation();

        DebugLogPlayerDistanceAndAggroReason();

        // 减速到时自动清除
        if (currentSlowRatio > 0f && Time.time >= slowEndTime)
        {
            currentSlowRatio = 0f;
        }
    }
    
    #region 状态更新方法
    
    private void UpdateIdleState()
    {
        idleTimer += Time.deltaTime;
        
        // 检查是否应该结束闲置状态
        float targetIdleTime = Random.Range(minIdleTime, maxIdleTime);
        
        if (idleTimer >= targetIdleTime)
        {
            // 闲置时间结束，开始巡逻
            SetState(EnemyState.Patrol);
        }
    }
    
    private void UpdatePatrolState()
    {
        patrolTimer += Time.deltaTime;
        
        // 检查是否到达巡逻时间
        if (patrolTimer >= patrolDuration)
        {
            // 巡逻时间结束，到达目标点
            hasReachedDestination = true;
            SetState(EnemyState.LookAround);
            return;
        }
        
        // 移动敌人
        MoveTowardsDestination();
        
        // 检查是否提前到达目标点（如果目标点很近）
        float distanceToTarget = Vector3.Distance(transform.position, currentPatrolPoint);
        if (distanceToTarget <= stoppingDistance)
        {
            hasReachedDestination = true;
            SetState(EnemyState.LookAround);
        }
        
        // 检查是否卡住了
        if (patrolTimer > 1f && Vector3.Distance(transform.position, currentPatrolPoint) > patrolRadius)
        {
            // 如果移动了1秒但离目标点还是很远，可能卡住了，重新选择方向
            SetState(EnemyState.Patrol);
        }
    }
    
    private void UpdateLookAroundState()
    {
        lookAroundTimer += Time.deltaTime;
        
        // 检查张望时间是否结束
        float targetLookAroundTime = Random.Range(minLookAroundTime, maxLookAroundTime);
        
        if (lookAroundTimer >= targetLookAroundTime)
        {
            // 张望时间结束，开始巡逻
            SetState(EnemyState.Patrol);
        }
    }
    
    private void UpdateHitState()
    {
        hitTimer += Time.deltaTime;
        
        // 检查受击恢复时间是否结束
        if (hitTimer >= hitRecoveryTime)
        {
            // 恢复后进入张望状态
            SetState(EnemyState.LookAround);
        }
    }
    
    private void UpdateChasePlayerState()
    {
        if (!TryGetPlayerTransform(out Transform pl))
        {
            SetState(EnemyState.Idle);
            return;
        }
        
        TopDownController pCtrl = pl.GetComponent<TopDownController>();
        if (pCtrl == null) pCtrl = pl.GetComponentInParent<TopDownController>();
        if (pCtrl != null && pCtrl.isDead)
        {
            SetState(EnemyState.LookAround);
            return;
        }
        
        float dist = GetHorizontalDistance(pl.position);
        if (dist > GetChaseAggroExitDistance())
        {
            SetState(EnemyState.LookAround);
            return;
        }
        
        currentPatrolPoint = pl.position;
        
        float attackReach = attackRange + Mathf.Max(0f, attackRangeExtraMargin);
        if (dist <= attackReach && Time.time >= nextMeleeAttackAllowedTime)
        {
            SetState(EnemyState.MeleeAttack);
            return;
        }
        
        // 已在出手距离内但冷却中：不再猛挤，避免根节点穿模仍判距 > attackRange
        if (dist <= attackReach && Time.time < nextMeleeAttackAllowedTime)
            return;
        
        MoveTowardsHorizontalTarget(pl.position);
    }
    
    private void UpdateMeleeAttackState()
    {
        if (TryGetPlayerTransform(out Transform pl))
            currentPatrolPoint = pl.position;
        
        if (meleeAttackFullDuration <= 0.01f && animator != null)
            CacheMeleeAttackFullDurationFromAnimator();
        
        meleeAttackTimer += Time.deltaTime;
        
        float fullDur = meleeAttackFullDuration > 0.01f ? meleeAttackFullDuration : attackAnimDuration;
        float hitMoment = fullDur * Mathf.Clamp01(attackHitNormalizedTime);
        if (!meleeHitResolved && meleeAttackTimer >= hitMoment)
        {
            SpawnSlashAndTryHitPlayer();
            meleeHitResolved = true;
        }
        
        if (meleeAttackTimer >= fullDur)
        {
            nextMeleeAttackAllowedTime = Time.time + attackCooldown;
            if (TryGetPlayerTransform(out Transform player))
            {
                float d = GetHorizontalDistance(player.position);
                if (d <= GetChaseAggroExitDistance())
                    SetState(EnemyState.ChasePlayer);
                else
                    SetState(EnemyState.LookAround);
            }
            else
                SetState(EnemyState.Idle);
        }
    }
    
    private bool IsAggressive2DReady()
    {
        return use2DFrameAnimation && aggressive2D;
    }
    
    /// <summary>追击脱战距离：大于警戒半径，减少在边界来回抖动。</summary>
    private float GetChaseAggroExitDistance()
    {
        float exit = chaseAggroExitRange > 0.01f ? chaseAggroExitRange : alertRange + 2f;
        return Mathf.Max(exit, alertRange + 0.15f);
    }
    
    private void CachePlayerTransform()
    {
        playerTransform = null;
        GameObject[] all = GameObject.FindGameObjectsWithTag("Player");
        if (all == null || all.Length == 0)
            return;
        foreach (GameObject go in all)
        {
            if (go != null && go.activeInHierarchy)
            {
                playerTransform = go.transform;
                return;
            }
        }
    }
    
    private bool TryGetPlayerTransform(out Transform pl)
    {
        if (playerTransform != null && !playerTransform.gameObject.activeInHierarchy)
            playerTransform = null;
        if (playerTransform == null)
            CachePlayerTransform();
        pl = playerTransform;
        return pl != null;
    }
    
    private void DebugLogPlayerDistanceAndAggroReason()
    {
        if (!debugLogPlayerAggro)
            return;
        
        float interval = Mathf.Max(0.05f, debugPlayerAggroLogInterval);
        bool hasPlayer = TryGetPlayerTransform(out Transform pl);
        float dist = hasPlayer ? GetHorizontalDistance(pl.position) : -1f;
        
        if (Time.time - lastDebugPlayerDistLogTime >= interval)
        {
            lastDebugPlayerDistLogTime = Time.time;
            if (hasPlayer)
                Debug.Log($"[EnemyAI] {gameObject.name} 与玩家水平距离: {dist:F2}m (状态: {currentState})");
            else
                Debug.Log($"[EnemyAI] {gameObject.name} 无可用玩家 (场景中无激活的 Tag=Player)");
        }
        
        if (currentState == EnemyState.ChasePlayer || currentState == EnemyState.MeleeAttack)
            return;
        
        if (Time.time - lastDebugAggroWarnTime < interval * 2f)
            return;
        lastDebugAggroWarnTime = Time.time;
        
        string reason = BuildNotChaseOrAttackReason(dist, hasPlayer);
        Debug.LogWarning($"[EnemyAI] {gameObject.name} 当前非追击/非近战 | 原因: {reason}");
    }
    
    private string BuildNotChaseOrAttackReason(float dist, bool hasPlayer)
    {
        if (currentState == EnemyState.Dead)
            return "已死亡 (Dead)，不进行追击/攻击";
        if (!IsAggressive2DReady())
            return "未开启 2D 攻击性（需同时勾选 use2DFrameAnimation 与 aggressive2D）";
        if (!hasPlayer)
            return "无激活的 Player（Tag=Player 且 activeInHierarchy）";
        if (currentState == EnemyState.Hit)
            return "受击恢复中 (Hit)，本帧前 TryBeginChase 会跳过追击";
        if (dist > alertRange)
            return $"玩家超出警戒半径 (距离 {dist:F2}m > alertRange {alertRange:F2}m)，不会进入追击";
        return $"玩家在警戒内 (距离 {dist:F2}m) 但当前 AI 状态为 {currentState}（下一帧 TryBeginChase 应切入 Chase；若持续出现请查脚本执行顺序或其它系统改状态）";
    }
    
    private float GetHorizontalDistance(Vector3 worldPos)
    {
        Vector3 a = transform.position;
        Vector3 b = worldPos;
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b);
    }
    
    private void TryBeginChaseIfPlayerInAlert()
    {
        if (!IsAggressive2DReady()) return;
        if (currentState == EnemyState.Dead || currentState == EnemyState.MeleeAttack ||
            currentState == EnemyState.ChasePlayer || currentState == EnemyState.Hit)
            return;
        
        if (!TryGetPlayerTransform(out Transform pl)) return;
        if (GetHorizontalDistance(pl.position) > alertRange) return;
        
        SetState(EnemyState.ChasePlayer);
    }
    
    private void SpawnSlashAndTryHitPlayer()
    {
        if (!TryGetPlayerTransform(out Transform pl)) return;
        
        Vector3 flatEnemy = transform.position;
        Vector3 flatPl = pl.position;
        flatEnemy.y = 0f;
        flatPl.y = 0f;
        Vector3 dir = flatPl - flatEnemy;
        if (dir.sqrMagnitude < 1e-6f)
            dir = transform.forward;
        dir.y = 0f;
        dir.Normalize();
        
        Vector3 spawnPos = transform.position + dir * slashForwardOffset;
        if (slashVfxPrefab != null)
        {
            Quaternion rot = Quaternion.LookRotation(dir, Vector3.up);
            Instantiate(slashVfxPrefab, spawnPos, rot);
        }
        
        float maxHitDist = attackRange * Mathf.Max(1f, attackHitDistanceMultiplier);
        if (GetHorizontalDistance(pl.position) > maxHitDist)
            return;
        
        TopDownController td = pl.GetComponent<TopDownController>();
        if (td == null) td = pl.GetComponentInParent<TopDownController>();
        if (td != null)
            td.ApplyEnemyMeleeHungerDamage(hungerDamageOnMeleeHit);
        else if (BattleValManager.Instance != null)
            BattleValManager.Instance.DamageOxygen(hungerDamageOnMeleeHit);
    }
    
    #endregion
    
    #region 状态切换方法
    
    public void SetState(EnemyState newState)
    {
        // 如果新状态与当前状态相同，不做处理
        if (currentState == newState)
            return;
        
        // 退出当前状态
        ExitState(currentState);
        
        // 更新当前状态
        currentState = newState;
        
        // 进入新状态
        EnterState(newState);
        
        if (showDebugInfo)
        {
            //Debug.Log($"{gameObject.name} 状态切换: {currentState}");
        }
    }
    
    private void EnterState(EnemyState state)
    {
        switch (state)
        {
            case EnemyState.Idle:
                EnterIdleState();
                break;
                
            case EnemyState.Patrol:
                EnterPatrolState();
                break;
                
            case EnemyState.LookAround:
                EnterLookAroundState();
                break;
                
            case EnemyState.Hit:
                EnterHitState();
                break;
                
            case EnemyState.ChasePlayer:
                EnterChasePlayerState();
                break;
                
            case EnemyState.MeleeAttack:
                EnterMeleeAttackState();
                break;
                
            case EnemyState.Dead:
                EnterDeadState();
                break;
        }
    }
    
    private void ExitState(EnemyState state)
    {
        switch (state)
        {
            case EnemyState.Idle:
                ExitIdleState();
                break;
                
            case EnemyState.Patrol:
                ExitPatrolState();
                break;
                
            case EnemyState.LookAround:
                ExitLookAroundState();
                break;
                
            case EnemyState.Hit:
                ExitHitState();
                break;
                
            case EnemyState.ChasePlayer:
                ExitChasePlayerState();
                break;
                
            case EnemyState.MeleeAttack:
                ExitMeleeAttackState();
                break;
        }
    }
    
    private void EnterIdleState()
    {
        idleTimer = 0f;
        hasReachedDestination = false;
    }
    
    private void ExitIdleState()
    {
        // 清理闲置状态
    }
    
    private void EnterPatrolState()
    {
        hasReachedDestination = false;
        patrolTimer = 0f;
        
        // 获取可行走的方向
        Vector3[] walkableDirections = GetWalkableDirections();
        
        if (walkableDirections.Length == 0)
        {
            // 如果没有可行走的方向，进入闲置状态
            Debug.LogWarning($"{gameObject.name} 没有找到可行走的方向，进入闲置状态");
            SetState(EnemyState.Idle);
            return;
        }
        
        bool foundPatrolPoint = false;
        float selectedDistance = minPatrolDistance;

        // 随机尝试多次，确保目标点一定落在 walkable 上
        for (int attempt = 0; attempt < 12; attempt++)
        {
            int randomIndex = Random.Range(0, walkableDirections.Length);
            Vector3 candidateDir = walkableDirections[randomIndex];
            float candidateDistance = Random.Range(minPatrolDistance, patrolRadius);

            Vector3 candidatePoint;
            if (TryGetValidPatrolPoint(candidateDir, candidateDistance, out candidatePoint))
            {
                moveDirection = candidateDir;
                currentPatrolPoint = candidatePoint;
                selectedDistance = Vector3.Distance(transform.position, currentPatrolPoint);
                foundPatrolPoint = true;
                break;
            }
        }

        if (!foundPatrolPoint)
        {
            if (showDebugInfo)
            {
                Debug.LogWarning($"{gameObject.name} 未找到命中 walkable 的巡逻点，切回 Idle");
            }
            SetState(EnemyState.Idle);
            return;
        }

        // 计算预计的巡逻时间
        patrolDuration = selectedDistance / Mathf.Max(0.01f, GetCurrentMoveSpeed());
        
        if (showDebugInfo)
        {
            //Debug.Log($"{gameObject.name} 巡逻目标点: {currentPatrolPoint}, 方向: {moveDirection}, 距离: {moveDistance}");
        }
    }
    
    private void ExitPatrolState()
    {
        // 清理巡逻状态
    }
    
    private void EnterLookAroundState()
    {
        lookAroundTimer = 0f;
    }
    
    private void ExitLookAroundState()
    {
        // 清理张望状态
    }
    
    private void EnterHitState()
    {
        hitTimer = 0f;
        
        if (showDebugInfo)
        {
            Debug.Log($"{gameObject.name} 受到攻击，进入受击状态");
        }
    }
    
    private void ExitHitState()
    {
        // 清理受击状态
    }
    
    private void EnterChasePlayerState()
    {
    }
    
    private void ExitChasePlayerState()
    {
    }
    
    private void EnterMeleeAttackState()
    {
        meleeAttackTimer = 0f;
        meleeHitResolved = false;
        meleeAttackFullDuration = 0f;
        lastRequested2DAnimState = null;
        if (use2DFrameAnimation && animator != null && !string.IsNullOrEmpty(spriteAttackStateName))
            Play2DAnimStateFromStart(spriteAttackStateName);
        CacheMeleeAttackFullDurationFromAnimator();
    }
    
    private void ExitMeleeAttackState()
    {
    }
    
    private void EnterDeadState()
    {
        if (showDebugInfo)
        {
            Debug.Log($"{gameObject.name} 进入死亡状态");
        }
    }
    
    #endregion
    
    #region 移动和检测方法
    
    // 获取所有可行走的方向
    private Vector3[] GetWalkableDirections()
    {
        List<Vector3> walkableDirs = new List<Vector3>();
        Vector3 rayOrigin = transform.position + Vector3.up * raycastHeight;
        
        foreach (Vector3 dir in Directions)
        {
            if (IsDirectionWalkable(rayOrigin, dir, raycastDistance))
            {
                walkableDirs.Add(dir);
            }
        }
        return walkableDirs.ToArray();
    }
    
    // 向目标点移动
    private void MoveTowardsDestination()
    {
        if (currentPatrolPoint == Vector3.zero)
            return;
        MoveTowardsHorizontalTarget(currentPatrolPoint);
    }
    
    private void MoveTowardsHorizontalTarget(Vector3 worldTarget)
    {
        Vector3 direction = (worldTarget - transform.position).normalized;
        direction.y = 0f;
        
        if (direction.magnitude < 0.1f)
            return;
        
        transform.position += direction * GetCurrentMoveSpeed() * Time.deltaTime;
        
        if (use2DFrameAnimation)
        {
            if (Mathf.Abs(direction.x) > 0.01f)
                last2DHorizontalSign = Mathf.Sign(direction.x);
        }
        else
        {
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, 10f * Time.deltaTime);
        }
    }

    private bool IsDirectionWalkable(Vector3 rayOrigin, Vector3 dir, float distance)
    {
        // 1) 前方不能被障碍物挡住
        if (Physics.Raycast(rayOrigin, dir, distance, obstacleLayer))
        {
            return false;
        }

        // 2) 目标点必须向下命中 walkable 层
        Vector3 targetPos = transform.position + dir * distance;
        RaycastHit groundHit;
        if (!Physics.Raycast(targetPos + Vector3.up * 2f, Vector3.down, out groundHit, 5f, walkableLayer))
        {
            return false;
        }

        // 3) 地面坡度限制，避免极陡斜坡
        float angle = Vector3.Angle(groundHit.normal, Vector3.up);
        return angle < 45f;
    }

    private bool TryGetValidPatrolPoint(Vector3 dir, float moveDistance, out Vector3 patrolPoint)
    {
        patrolPoint = Vector3.zero;
        Vector3 rayOrigin = transform.position + Vector3.up * raycastHeight;

        // 前方路径上必须无障碍，且目标点必须是 walkable
        if (!IsDirectionWalkable(rayOrigin, dir, moveDistance))
        {
            return false;
        }

        Vector3 targetPos = transform.position + dir * moveDistance;
        RaycastHit groundHit;
        if (!Physics.Raycast(targetPos + Vector3.up * 2f, Vector3.down, out groundHit, 5f, walkableLayer))
        {
            return false;
        }

        patrolPoint = groundHit.point;
        return true;
    }
    
    #endregion
    
    #region 工具方法
    
    private void UpdateAnimations()
    {
        if (animator == null)
            return;
        
        if (use2DFrameAnimation)
        {
            UpdateAnimations2DFrame();
            return;
        }
        
        // 根据当前状态设置动画参数
        switch (currentState)
        {
            case EnemyState.Idle:
                animator.SetBool(isWalkingHash, false);
                //animator.SetBool(isLookingAroundHash, false);
                break;
                
            case EnemyState.Patrol:
                animator.SetBool(isWalkingHash, true);
                //animator.SetBool(isLookingAroundHash, false);
                break;
                
            case EnemyState.LookAround:
                animator.SetBool(isWalkingHash, false);
                //animator.SetBool(isLookingAroundHash, true);
                break;
                
            case EnemyState.Hit:
                animator.SetBool(isWalkingHash, false);
                //animator.SetBool(isLookingAroundHash, false);
                break;
                
            case EnemyState.ChasePlayer:
                animator.SetBool(isWalkingHash, true);
                break;
                
            case EnemyState.MeleeAttack:
                animator.SetBool(isWalkingHash, false);
                break;
                
            case EnemyState.Dead:
                animator.SetBool(isWalkingHash, false);
                //animator.SetBool(isLookingAroundHash, false);
                break;
        }
    }
    
    /// <summary>2D 帧动画：按状态名 CrossFade idle / 左移 / 右移，不依赖 IsWalking。</summary>
    private void UpdateAnimations2DFrame()
    {
        //animator.SetBool(isWalkingHash, false);
        
        switch (currentState)
        {
            case EnemyState.Patrol:
            case EnemyState.ChasePlayer:
                {
                    if (singleDirectionWalkRotateSpriteForLeft)
                        Request2DAnimState(spriteMoveRightStateName);
                    else
                    {
                        Vector3 toTarget = currentPatrolPoint - transform.position;
                        toTarget.y = 0f;
                        float hx = toTarget.sqrMagnitude > 0.0001f ? Mathf.Sign(toTarget.x) : last2DHorizontalSign;
                        string moveState = hx < 0f ? spriteMoveLeftStateName : spriteMoveRightStateName;
                        Request2DAnimState(moveState);
                    }
                }
                break;
                
            case EnemyState.MeleeAttack:
                // 攻击由 EnterMeleeAttackState 内 Play2DAnimStateFromStart 强制从 0 重播，避免 Request2DAnimState 同状态名跳过 Play 导致只第一次有特效
                break;
                
            case EnemyState.Idle:
            case EnemyState.LookAround:
            case EnemyState.Hit:
            case EnemyState.Dead:
                Request2DAnimState(spriteIdleStateName);
                break;
        }
    }
    
    /// <summary>仅在「脚本想播的状态」变化时切换；用 Play 不依赖 Entry/连线，状态名须与 Animator 里一致（含大小写）。子状态机内用路径，如 Locomotion.moveleft。</summary>
    private void Request2DAnimState(string stateName)
    {
        if (string.IsNullOrEmpty(stateName))
            return;
        if (lastRequested2DAnimState == stateName)
            return;
        lastRequested2DAnimState = stateName;
        
        if (useCrossFadeFor2D)
            animator.CrossFade(stateName, animationTransitionTime, 0, 0f);
        else
            animator.Play(stateName, 0, 0f);
    }
    
    /// <summary>每次进入近战攻击时调用：无视「与上次请求同名则跳过」，强制从 normalizedTime=0 重播，保证多轮攻击都会出刀与结算。</summary>
    private void Play2DAnimStateFromStart(string stateName)
    {
        if (animator == null || string.IsNullOrEmpty(stateName))
            return;
        lastRequested2DAnimState = stateName;
        if (useCrossFadeFor2D)
            animator.CrossFade(stateName, animationTransitionTime, 0, 0f);
        else
            animator.Play(stateName, 0, 0f);
        animator.Update(0f);
    }
    
    /// <summary>从 Animator 当前层 0 读取攻击状态总时长，保证 80% 出刀后仍能播完剩余 20% 再切状态。</summary>
    private void CacheMeleeAttackFullDurationFromAnimator()
    {
        meleeAttackFullDuration = 0f;
        if (animator == null)
        {
            meleeAttackFullDuration = attackAnimDuration;
            return;
        }
        
        AnimatorStateInfo info = animator.GetCurrentAnimatorStateInfo(0);
        if (info.length > 0.01f)
        {
            meleeAttackFullDuration = info.length;
            return;
        }
        
        AnimatorClipInfo[] clips = animator.GetCurrentAnimatorClipInfo(0);
        if (clips != null && clips.Length > 0 && clips[0].clip != null && clips[0].clip.length > 0.01f)
        {
            meleeAttackFullDuration = clips[0].clip.length;
            return;
        }
        
        meleeAttackFullDuration = attackAnimDuration;
    }
    
    /// <summary>记录 sprite 节点在场景中的「朝右」预设 localRotation，供翻面时作为基准。</summary>
    private void CacheSpriteVisualBaseLocalRotation()
    {
        if (spriteVisualTransform != null)
            spriteVisualBaseLocalRotation = spriteVisualTransform.localRotation;
    }
    
    /// <summary>单方向走路模式：先恢复为缓存的朝右 localRotation，向左时再绕 sprite 本地轴旋转 180°。</summary>
    private void Apply2DSpriteFacingRotation()
    {
        if (!use2DFrameAnimation || !singleDirectionWalkRotateSpriteForLeft || spriteVisualTransform == null)
            return;
        
        spriteVisualTransform.localRotation = spriteVisualBaseLocalRotation;
        
        if (currentState != EnemyState.Patrol && currentState != EnemyState.ChasePlayer &&
            currentState != EnemyState.MeleeAttack)
            return;
        
        Vector3 toTarget = currentPatrolPoint - transform.position;
        toTarget.y = 0f;
        float hx = toTarget.sqrMagnitude > 0.0001f ? Mathf.Sign(toTarget.x) : last2DHorizontalSign;
        if (hx >= 0f)
            return;
        
        Vector3 axis = spriteFlipLocalAxis.sqrMagnitude > 1e-8f ? spriteFlipLocalAxis.normalized : Vector3.up;
        spriteVisualTransform.Rotate(axis, 180f, Space.Self);
    }
    
    #endregion
    
    #region 公共方法
    
    // 被攻击时调用
    public void OnHit()
    {
        // 只有非死亡状态才能进入受击状态
        if (currentState != EnemyState.Dead)
        {
            SetState(EnemyState.Hit);
        }
    }
    
    // 死亡时调用
    public void OnDeath()
    {
        SetState(EnemyState.Dead);
    }
    
    // 获取当前状态
    public EnemyState GetCurrentState()
    {
        return currentState;
    }
    
    /// <summary>供 EnemyHealth：2D 帧动画由脚本 Play 状态驱动时，不应再 SetTrigger(Hit)，否则会打断 idle 并可能卡在受击外观。</summary>
    public bool IsUsing2DFrameAnimation => use2DFrameAnimation;

    /// <summary>
    /// 施加移动减速：slowRatio=0.25 表示减速25%，duration为持续秒数。
    /// 同时存在多个减速时：取更强减速；持续时间取更长。
    /// </summary>
    public void ApplyMoveSlow(float slowRatio, float duration)
    {
        if (currentState == EnemyState.Dead) return;
        if (duration <= 0f || slowRatio <= 0f) return;

        // 仅允许“减速”，不允许变成“定身”：slowRatio 最大限制为 (1 - minMoveSpeedRatio)
        float maxSlowRatio = Mathf.Clamp01(1f - Mathf.Clamp(minMoveSpeedRatio, 0.05f, 1f));
        float clampedRatio = Mathf.Clamp(slowRatio, 0f, maxSlowRatio);
        float newEndTime = Time.time + duration;

        if (clampedRatio > currentSlowRatio)
        {
            currentSlowRatio = clampedRatio;
        }

        if (newEndTime > slowEndTime)
        {
            slowEndTime = newEndTime;
        }
    }

    public float GetCurrentMoveSpeed()
    {
        float minRatio = Mathf.Clamp(minMoveSpeedRatio, 0.05f, 1f);
        float finalRatio = Mathf.Max(minRatio, 1f - currentSlowRatio);
        return Mathf.Max(0.01f, baseMoveSpeed * finalRatio);
    }
    
    // 强制设置巡逻点（调试用）
    public void SetPatrolPoint(Vector3 point)
    {
        if (currentState == EnemyState.Patrol)
        {
            currentPatrolPoint = point;
            moveDirection = (point - transform.position).normalized;
        }
    }
    
    #endregion
    
    #region 调试方法
    
    private void OnDrawGizmosSelected()
    {
        if (!showDebugInfo)
            return;
        
        // 绘制巡逻范围
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, patrolRadius);
        
        if (IsAggressive2DReady())
        {
            Gizmos.color = new Color(1f, 0.5f, 0f, 0.9f);
            Gizmos.DrawWireSphere(transform.position, alertRange);
            Gizmos.color = new Color(1f, 0.35f, 0f, 0.5f);
            float exitR = chaseAggroExitRange > 0.01f ? chaseAggroExitRange : alertRange + 2f;
            exitR = Mathf.Max(exitR, alertRange + 0.15f);
            Gizmos.DrawWireSphere(transform.position, exitR);
        }
        
        // 绘制当前巡逻点
        if (currentPatrolPoint != Vector3.zero)
        {
            Gizmos.color = patrolPointColor;
            Gizmos.DrawSphere(currentPatrolPoint, 0.3f);
            Gizmos.DrawLine(transform.position, currentPatrolPoint);
        }
        
        // 绘制射线检测
        Vector3 rayOrigin = transform.position + Vector3.up * raycastHeight;
        
        foreach (Vector3 dir in Directions)
        {
            // 检查是否为可行走方向
            bool isWalkable = false;
            
            if (!Physics.Raycast(rayOrigin, dir, raycastDistance, obstacleLayer))
            {
                Vector3 targetPos = transform.position + dir * raycastDistance;
                RaycastHit groundHit;
                
                if (Physics.Raycast(targetPos + Vector3.up * 2f, Vector3.down, out groundHit, 3f, walkableLayer))
                {
                    float angle = Vector3.Angle(groundHit.normal, Vector3.up);
                    if (angle < 45f)
                    {
                        isWalkable = true;
                    }
                }
            }
            
            // 根据是否可行走设置颜色
            Gizmos.color = isWalkable ? raycastColor : blockedRayColor;
            Gizmos.DrawRay(rayOrigin, dir * raycastDistance);
        }
        
        // 绘制状态标签
        GUIStyle style = new GUIStyle();
        style.normal.textColor = Color.white;
        style.fontSize = 12;
        Vector3 labelPosition = transform.position + Vector3.up * 2f;
        
        #if UNITY_EDITOR
        UnityEditor.Handles.Label(labelPosition, $"状态: {currentState}", style);
        #endif
    }
    
    #endregion
}