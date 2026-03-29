using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public enum BossState
{
    Idle,
    Chase,
    MeleeAttack,
    RangedAttack,
    ChargeAttack,
    HitReact,
    Dead
}

/// <summary>
/// BOSS AI：警戒圆外 Idle，入内追击，并周期性释放近战 / 抛射 / 冲撞。
/// 动画参数为 Trigger：Roar、AttackMelee、AttackThrow、AttackCharge、Hit、Die；Bool：IsMoving。
/// 也可在动画事件中调用 AnimNotify_* 方法对齐特效与伤害帧。
/// </summary>
[DisallowMultipleComponent]
public class BossAI : MonoBehaviour
{
    [Header("目标")]
    [SerializeField] private Transform playerTarget;
    [SerializeField] private string playerTag = "Player";
    [Tooltip("解析目标时优先选择：激活 + 带已启用的 TopDownController 的对象（避免打到隐藏/备用 Player）")]
    [SerializeField] private bool preferActivePlayerWithTopDown = true;

    [Header("范围与移动")]
    [SerializeField] private float aggroRadius = 18f;
    [SerializeField] private float moveSpeed = 4f;
    [SerializeField] private float turnSpeed = 8f;
    [SerializeField] private float stoppingDistance = 1.8f;
    [SerializeField] private LayerMask groundLayers = ~0;
    [Tooltip("从 Idle/攻击/后摇 恢复追击时：先播 Run（IsMoving），过此时间后再实际位移，减轻衔接生硬")]
    [SerializeField] private float runBlendLeadTime = 0.14f;

    [Header("普攻")]
    [SerializeField] private float meleeRange = 2.8f;
    [SerializeField] private float meleeDamage = 20f;
    [SerializeField] private float meleeSphereRadius = 1.2f;
    [SerializeField] private Transform meleeOrigin;
    [SerializeField] private float meleeAnimationDuration = 0.6f;

    [Header("打击特效（近战伤害帧 / 冲撞结束）")]
    [SerializeField] private GameObject meleeStrikeVfxPrefab;
    [Tooltip("冲撞招式结束时生成；为空则用近战特效预制体")]
    [SerializeField] private GameObject chargeEndStrikeVfxPrefab;
    [Tooltip("近战与冲撞结束打击特效共用挂点；空则用 meleeOrigin")]
    [SerializeField] private Transform meleeStrikeVfxAnchor;

    [Header("抛射")]
    [SerializeField] private GameObject bossProjectilePrefab;
    [SerializeField] private Transform projectileSpawn;
    [SerializeField] private GameObject warningZonePrefab;
    [SerializeField] private int projectileVolleyCount = 3;
    [SerializeField] private float projectileRadiusAroundPlayer = 4f;
    [SerializeField] private float projectileSpeed = 16f;
    [SerializeField] private float projectileDamage = 22f;
    [SerializeField] private bool projectileUseBallistic = true;
    [SerializeField] private float roarDurationRanged = 0.8f;
    [SerializeField] private float warningShowDuration = 1.1f;
    [SerializeField] private float throwClipDuration = 0.7f;
    [Tooltip("抛物线水平分量 = projectileSpeed × 该值；越小飞得越高、越远（仍落向目标点）")]
    [Range(0.15f, 1f)] [SerializeField] private float projectileBallisticHorizontalScale = 0.48f;
    [Tooltip("在物理公式基础上再放大竖直初速度，整体弧线更高")]
    [Min(0.5f)] [SerializeField] private float projectileArcHeightMultiplier = 1.35f;
    [Tooltip("抛射时每一发之间的间隔（秒），避免同时生成挤在一起互相触发")]
    [SerializeField] private float projectileSpawnInterval = 0.2f;

    [Header("冲撞")]
    [SerializeField] private float chargeMinRange = 4f;
    [SerializeField] private float chargeMaxRange = 14f;
    [SerializeField] private float chargeSpeed = 12f;
    [SerializeField] private float chargeDamage = 30f;
    [SerializeField] private float chargeDuration = 0.55f;
    [SerializeField] private float chargeHitRadius = 1.5f;
    [SerializeField] private float roarDurationCharge = 0.75f;
    [SerializeField] private float chargeAimDuration = 0.9f;
    [SerializeField] private float chargeWindupAfterTrigger = 0.25f;
    [SerializeField] private LayerMask chargeObstacleLayers = ~0;
    [SerializeField] private LineRenderer chargePathLine;

    [Header("冲撞路径线（可调粗细与颜色）")]
    [SerializeField] private float chargePathHeightOffset = 0.2f;
    [SerializeField] private float chargePathStartWidth = 0.38f;
    [SerializeField] private float chargePathEndWidth = 0.22f;
    [SerializeField] private Color chargePathStartColor = new Color(1f, 0.35f, 0.1f, 0.9f);
    [SerializeField] private Color chargePathEndColor = new Color(1f, 0.55f, 0.25f, 0.55f);
    [Range(0, 24)] [SerializeField] private int chargePathCornerVertices = 8;
    [Range(0, 24)] [SerializeField] private int chargePathCapVertices = 8;
    [Tooltip("为空则自动生成时使用 Sprites/Default")]
    [SerializeField] private Material chargePathLineMaterial;

    [Header("攻击后摇（招式结束后、此时间内不追击移动）")]
    [SerializeField] private float attackRecoveryDuration = 0.65f;

    [Header("行为节奏")]
    [SerializeField] private float globalAttackCooldown = 2.2f;
    [SerializeField] private float hitRecoverTime = 0.85f;
    [Range(0f, 1f)] [SerializeField] private float chargePickWeight = 0.35f;

    [Header("受击（闪白/扣血由 EnemyHealth 处理；此处控制 Hit 动画与 AI 打断）")]
    [Tooltip("开启且未使用下方「累计 N 次」时：受击不播 Hit、不进入受击状态、不打断当前招式")]
    [SerializeField] private bool suppressHitAnimationAndInterrupt;
    [Tooltip("≥2：累计受击这么多次才触发一次完整受击（播 Hit、打断招式、使用 Hit Recover Time）；0 表示仅由上方开关决定")]
    [Min(0)] [SerializeField] private int hitsToTriggerHitReactionOnce;

    [Header("动画")]
    [SerializeField] private Animator animator;
    [SerializeField] private string paramIsMoving = "IsMoving";
    [SerializeField] private string trigRoar = "Roar";
    [SerializeField] private string trigMelee = "AttackMelee";
    [SerializeField] private string trigThrow = "AttackThrow";
    [SerializeField] private string trigCharge = "AttackCharge";
    [SerializeField] private string trigHit = "Hit";
    [SerializeField] private string trigDie = "Die";

    [Header("调试")]
    [SerializeField] private bool drawAggroGizmo = true;

    private BossState state = BossState.Idle;

    private int hashIsMoving;
    private int hashRoar;
    private int hashMelee;
    private int hashThrow;
    private int hashCharge;
    private int hashHit;
    private int hashDie;

    private float nextAttackReadyTime;
    private bool isBusy;
    private Coroutine activeAttackCoroutine;
    private Coroutine hitCoroutine;

    private Vector3 chargeDirection;
    private float chargeEndTime;
    private readonly Collider[] meleeHits = new Collider[16];
    private readonly HashSet<GameObject> chargeDamaged = new HashSet<GameObject>();

    /// <summary>早于该时间戳时不进行追击位移（攻击后摇）。</summary>
    private float chaseMovementResumeTime;

    private bool lastWantChaseRunAnim;
    private float chaseRunMoveUnlockTime;
    private bool chaseRunAnimDesired;

    private int hitReactionAccumCount;

    private void Awake()
    {
        if (animator == null) animator = GetComponent<Animator>();
        if (meleeOrigin == null) meleeOrigin = transform;
        if (projectileSpawn == null) projectileSpawn = transform;

        hashIsMoving = Animator.StringToHash(paramIsMoving);
        hashRoar = Animator.StringToHash(trigRoar);
        hashMelee = Animator.StringToHash(trigMelee);
        hashThrow = Animator.StringToHash(trigThrow);
        hashCharge = Animator.StringToHash(trigCharge);
        hashHit = Animator.StringToHash(trigHit);
        hashDie = Animator.StringToHash(trigDie);

        EnsureChargeLine();
    }

    private void Start()
    {
        if (!IsValidActivePlayer(playerTarget))
            playerTarget = null;
        if (playerTarget == null)
            playerTarget = FindBestActivePlayerTransform();

        nextAttackReadyTime = Time.time;
        chaseMovementResumeTime = 0f;
        lastWantChaseRunAnim = false;
        chaseRunMoveUnlockTime = 0f;
        SetState(BossState.Idle);
    }

    private void Update()
    {
        if (state == BossState.Dead) return;

        RefreshPlayerTarget();

        Transform player = playerTarget;
        bool inAggro = player != null && FlatDistance(transform.position, player.position) <= aggroRadius;

        if (!inAggro && state != BossState.Idle && state != BossState.HitReact && state != BossState.Dead)
        {
            StopAttack();
            SetState(BossState.Idle);
        }

        chaseRunAnimDesired = state == BossState.Chase && !isBusy && player != null &&
                                Time.time >= chaseMovementResumeTime;
        if (chaseRunAnimDesired)
        {
            if (!lastWantChaseRunAnim)
                chaseRunMoveUnlockTime = Time.time + Mathf.Max(0f, runBlendLeadTime);
            lastWantChaseRunAnim = true;
        }
        else
        {
            lastWantChaseRunAnim = false;
        }

        bool canChaseMove = chaseRunAnimDesired && Time.time >= chaseRunMoveUnlockTime;

        switch (state)
        {
            case BossState.Idle:
                UpdateIdleAnim(false);
                if (inAggro)
                    SetState(BossState.Chase);
                break;

            case BossState.Chase:
                if (!inAggro) break;
                if (!isBusy && Time.time >= nextAttackReadyTime)
                    TryPickAndStartAttack();
                if (canChaseMove)
                    ChaseMoveToward(player);
                else
                    UpdateIdleAnim(false);
                break;

            case BossState.MeleeAttack:
            case BossState.RangedAttack:
            case BossState.ChargeAttack:
                UpdateIdleAnim(false);
                break;

            case BossState.HitReact:
                UpdateIdleAnim(false);
                break;

            case BossState.Dead:
                break;
        }

        if (state == BossState.ChargeAttack && isBusy && Time.time < chargeEndTime)
            PerformChargeMotion();

        UpdateAnimatorIsMoving();
    }

    private void UpdateIdleAnim(bool moving)
    {
        // 由 UpdateAnimatorIsMoving 统一处理
    }

    private void UpdateAnimatorIsMoving()
    {
        if (animator == null) return;
        animator.SetBool(hashIsMoving, chaseRunAnimDesired);
    }

    private void ChaseMoveToward(Transform player)
    {
        if (player == null) return;

        Vector3 self = Flat(transform.position);
        Vector3 pl = Flat(player.position);
        Vector3 to = pl - self;
        float dist = to.magnitude;
        if (dist <= stoppingDistance) return;

        to /= Mathf.Max(dist, 0.001f);
        transform.position += to * (moveSpeed * Time.deltaTime);

        Quaternion look = Quaternion.LookRotation(to);
        transform.rotation = Quaternion.Slerp(transform.rotation, look, turnSpeed * Time.deltaTime);
    }

    private void TryPickAndStartAttack()
    {
        if (playerTarget == null) return;

        float d = FlatDistance(transform.position, playerTarget.position);

        if (d <= meleeRange)
        {
            activeAttackCoroutine = StartCoroutine(CoMelee());
            return;
        }

        bool canCharge = d >= chargeMinRange && d <= chargeMaxRange;
        bool rollCharge = canCharge && Random.value < chargePickWeight;

        if (rollCharge)
        {
            activeAttackCoroutine = StartCoroutine(CoCharge());
        }
        else
        {
            activeAttackCoroutine = StartCoroutine(CoRanged());
        }
    }

    private IEnumerator CoMelee()
    {
        isBusy = true;
        state = BossState.MeleeAttack;
        FacePlayer();

        if (animator != null)
            animator.SetTrigger(hashMelee);

        yield return new WaitForSeconds(Mathf.Max(0.05f, meleeAnimationDuration * 0.45f));
        SpawnMeleeStrikeVfx();
        AnimNotify_MeleeDealDamage();
        yield return new WaitForSeconds(Mathf.Max(0.05f, meleeAnimationDuration * 0.55f));

        EndAttackCommon();
    }

    private IEnumerator CoRanged()
    {
        isBusy = true;
        state = BossState.RangedAttack;
        FacePlayer();

        if (animator != null)
        {
            animator.SetTrigger(hashRoar);
        }

        yield return new WaitForSeconds(roarDurationRanged);

        if (playerTarget == null)
        {
            EndAttackCommon();
            yield break;
        }

        List<Vector3> targets = new List<Vector3>();
        for (int i = 0; i < projectileVolleyCount; i++)
        {
            Vector2 rnd = Random.insideUnitCircle * projectileRadiusAroundPlayer;
            Vector3 guess = playerTarget.position + new Vector3(rnd.x, 0f, rnd.y);
            Vector3 onGround = ProjectToGround(guess);
            targets.Add(onGround);

            if (warningZonePrefab != null)
            {
                Quaternion rot = Quaternion.FromToRotation(Vector3.up, GetGroundNormal(onGround));
                var w = Instantiate(warningZonePrefab, onGround, rot);
                var bz = w.GetComponent<BossWarningZone>();
                if (bz != null)
                    bz.Configure(warningShowDuration);
            }
        }

        yield return new WaitForSeconds(warningShowDuration);

        FacePlayer();
        if (animator != null)
            animator.SetTrigger(hashThrow);

        yield return new WaitForSeconds(throwClipDuration);

        if (bossProjectilePrefab != null)
        {
            Vector3 spawnPos = projectileSpawn.position;
            for (int i = 0; i < targets.Count; i++)
            {
                Vector3 t = targets[i];
                var go = Instantiate(bossProjectilePrefab, spawnPos, Quaternion.identity);
                var bp = go.GetComponent<BossProjectile>();
                if (bp != null)
                    bp.LaunchFromTo(spawnPos, t + Vector3.up * 0.05f, projectileSpeed, projectileDamage,
                        projectileUseBallistic, projectileBallisticHorizontalScale, projectileArcHeightMultiplier);

                if (i < targets.Count - 1 && projectileSpawnInterval > 0f)
                    yield return new WaitForSeconds(projectileSpawnInterval);
            }
        }

        EndAttackCommon();
    }

    private IEnumerator CoCharge()
    {
        isBusy = true;
        state = BossState.ChargeAttack;
        FacePlayer();

        if (animator != null)
            animator.SetTrigger(hashRoar);

        yield return new WaitForSeconds(roarDurationCharge);

        if (playerTarget == null)
        {
            EndAttackCommon();
            yield break;
        }

        FacePlayer();
        Vector3 dir = Flat(playerTarget.position - transform.position);
        if (dir.sqrMagnitude < 0.01f)
            dir = transform.forward;
        dir.Normalize();
        chargeDirection = dir;

        float desiredDist = Mathf.Clamp(FlatDistance(transform.position, playerTarget.position), chargeMinRange, chargeMaxRange);
        Vector3 start = Flat(transform.position);
        Vector3 endFlat = start + chargeDirection * desiredDist;
        if (Physics.Raycast(transform.position + Vector3.up * 0.5f, chargeDirection, out RaycastHit block,
                desiredDist + 0.5f, chargeObstacleLayers, QueryTriggerInteraction.Ignore))
        {
            endFlat = Flat(block.point) - chargeDirection * 0.75f;
        }

        DrawChargePath(start, endFlat);
        yield return new WaitForSeconds(chargeAimDuration);

        if (animator != null)
            animator.SetTrigger(hashCharge);

        yield return new WaitForSeconds(chargeWindupAfterTrigger);

        chargeDamaged.Clear();
        chargeEndTime = Time.time + chargeDuration;
        HideChargePath();

        yield return new WaitUntil(() => Time.time >= chargeEndTime || state != BossState.ChargeAttack || !isBusy);

        SpawnChargeEndStrikeVfx();
        EndAttackCommon();
    }

    private void PerformChargeMotion()
    {
        float step = chargeSpeed * Time.deltaTime;
        transform.position += chargeDirection * step;

        int n = Physics.OverlapSphereNonAlloc(transform.position + Vector3.up * 0.8f, chargeHitRadius, meleeHits,
            ~0, QueryTriggerInteraction.Collide);
        for (int i = 0; i < n; i++)
        {
            var c = meleeHits[i];
            if (c == null) continue;
            if (c.GetComponentInParent<BossAI>() != null) continue;

            GameObject root = c.transform.root.gameObject;
            if (!chargeDamaged.Add(root)) continue;

            TryDamageFromBossHit(c, chargeDamage, transform.position + Vector3.up * 0.8f);
        }
    }

    private void EndAttackCommon()
    {
        isBusy = false;
        activeAttackCoroutine = null;
        nextAttackReadyTime = Time.time + globalAttackCooldown;
        chaseMovementResumeTime = Time.time + Mathf.Max(0f, attackRecoveryDuration);
        HideChargePath();
        if (playerTarget != null && FlatDepthInRange())
            SetState(BossState.Chase);
        else
            SetState(BossState.Idle);
    }

    private bool FlatDepthInRange()
    {
        if (playerTarget == null) return false;
        return FlatDistance(transform.position, playerTarget.position) <= aggroRadius;
    }

    private void StopAttack()
    {
        if (activeAttackCoroutine != null)
        {
            StopCoroutine(activeAttackCoroutine);
            activeAttackCoroutine = null;
            nextAttackReadyTime = Mathf.Max(nextAttackReadyTime, Time.time + globalAttackCooldown * 0.35f);
        }
        isBusy = false;
        HideChargePath();
    }

    private void FacePlayer()
    {
        if (playerTarget == null) return;
        Vector3 to = Flat(playerTarget.position) - Flat(transform.position);
        if (to.sqrMagnitude < 1e-4f) return;
        transform.rotation = Quaternion.LookRotation(to.normalized);
    }

    private Vector3 ProjectToGround(Vector3 world)
    {
        if (Physics.Raycast(world + Vector3.up * 6f, Vector3.down, out RaycastHit hit, 20f, groundLayers,
                QueryTriggerInteraction.Ignore))
            return hit.point;
        return world;
    }

    private Vector3 GetGroundNormal(Vector3 world)
    {
        if (Physics.Raycast(world + Vector3.up * 6f, Vector3.down, out RaycastHit hit, 20f, groundLayers,
                QueryTriggerInteraction.Ignore))
            return hit.normal;
        return Vector3.up;
    }

    private void DrawChargePath(Vector3 startFlat, Vector3 endFlat)
    {
        EnsureChargeLine();
        if (chargePathLine == null) return;
        ApplyChargePathStyle();
        float y = chargePathHeightOffset;
        Vector3 a = startFlat + Vector3.up * y;
        Vector3 b = endFlat + Vector3.up * y;
        chargePathLine.positionCount = 2;
        chargePathLine.SetPosition(0, a);
        chargePathLine.SetPosition(1, b);
        chargePathLine.enabled = true;
    }

    private void HideChargePath()
    {
        if (chargePathLine != null)
            chargePathLine.enabled = false;
    }

    private void EnsureChargeLine()
    {
        if (chargePathLine != null) return;
        var lrgo = new GameObject("ChargePathPreview");
        lrgo.transform.SetParent(transform, false);
        chargePathLine = lrgo.AddComponent<LineRenderer>();
        Material mat = chargePathLineMaterial;
        if (mat == null)
        {
            Shader s = Shader.Find("Sprites/Default");
            if (s != null)
                mat = new Material(s);
        }
        if (mat != null)
            chargePathLine.material = mat;
        chargePathLine.useWorldSpace = true;
        chargePathLine.enabled = false;
        ApplyChargePathStyle();
    }

    private void ApplyChargePathStyle()
    {
        if (chargePathLine == null) return;
        chargePathLine.startWidth = Mathf.Max(0.01f, chargePathStartWidth);
        chargePathLine.endWidth = Mathf.Max(0.01f, chargePathEndWidth);
        chargePathLine.startColor = chargePathStartColor;
        chargePathLine.endColor = chargePathEndColor;
        chargePathLine.numCornerVertices = chargePathCornerVertices;
        chargePathLine.numCapVertices = chargePathCapVertices;
    }

    private void SetState(BossState s)
    {
        state = s;
    }

    /// <summary>
    /// 当前引用失效（未激活/被隐藏）时，重新在场景中寻找激活的玩家。
    /// </summary>
    private void RefreshPlayerTarget()
    {
        if (IsValidActivePlayer(playerTarget))
            return;
        playerTarget = FindBestActivePlayerTransform();
    }

    private static bool IsValidActivePlayer(Transform t)
    {
        if (t == null) return false;
        return t.gameObject.activeInHierarchy;
    }

    /// <summary>
    /// 在带 Tag 的对象中选取：层级激活；若开启 preferActivePlayerWithTopDown 则还要求 TopDownController 存在且启用。
    /// 多个候选时取与 BOSS 水平距离最近者。
    /// </summary>
    private Transform FindBestActivePlayerTransform()
    {
        GameObject[] tagged;
        try
        {
            tagged = GameObject.FindGameObjectsWithTag(playerTag);
        }
        catch (UnityException)
        {
            return null;
        }

        List<Transform> strict = new List<Transform>();
        List<Transform> loose = new List<Transform>();

        for (int i = 0; i < tagged.Length; i++)
        {
            GameObject go = tagged[i];
            if (go == null || !go.activeInHierarchy) continue;

            loose.Add(go.transform);
            if (!preferActivePlayerWithTopDown) continue;

            var tdc = go.GetComponent<TopDownController>();
            if (tdc != null && tdc.enabled)
                strict.Add(go.transform);
        }

        List<Transform> pool = strict.Count > 0 ? strict : loose;
        if (pool.Count == 0) return null;
        if (pool.Count == 1) return pool[0];

        Transform best = pool[0];
        float bestSqr = FlatDistanceSqr(transform.position, best.position);
        for (int i = 1; i < pool.Count; i++)
        {
            float sqr = FlatDistanceSqr(transform.position, pool[i].position);
            if (sqr < bestSqr)
            {
                bestSqr = sqr;
                best = pool[i];
            }
        }
        return best;
    }

    private static float FlatDistanceSqr(Vector3 a, Vector3 b)
    {
        a.y = 0f;
        b.y = 0f;
        return (a - b).sqrMagnitude;
    }

    /// <summary>
    /// 由 EnemyHealth 在受击时调用。返回是否应播放受击动画（闪白/扣血仍由 EnemyHealth 执行）。
    /// </summary>
    public bool OnHit()
    {
        if (state == BossState.Dead) return false;

        if (hitsToTriggerHitReactionOnce >= 2)
        {
            hitReactionAccumCount++;
            if (hitReactionAccumCount < hitsToTriggerHitReactionOnce)
                return false;

            hitReactionAccumCount = 0;
        }
        else if (suppressHitAnimationAndInterrupt)
        {
            return false;
        }

        StopAttack();
        SetState(BossState.HitReact);

        if (animator != null)
            animator.SetTrigger(hashHit);

        if (hitCoroutine != null)
            StopCoroutine(hitCoroutine);
        hitCoroutine = StartCoroutine(CoHitRecover());
        return true;
    }

    private IEnumerator CoHitRecover()
    {
        yield return new WaitForSeconds(hitRecoverTime);
        hitCoroutine = null;
        if (state != BossState.Dead)
        {
            if (playerTarget != null && FlatDepthInRange())
                SetState(BossState.Chase);
            else
                SetState(BossState.Idle);
        }
    }

    public void OnDeath()
    {
        StopAttack();
        if (hitCoroutine != null)
        {
            StopCoroutine(hitCoroutine);
            hitCoroutine = null;
        }
        hitReactionAccumCount = 0;
        SetState(BossState.Dead);
        isBusy = false;

        if (animator != null)
            animator.SetTrigger(hashDie);
    }

    /// <summary>动画事件：近战伤害帧</summary>
    public void AnimNotify_MeleeDealDamage()
    {
        MeleeOverlapDamage();
    }

    private void SpawnMeleeStrikeVfx()
    {
        if (meleeStrikeVfxPrefab == null) return;
        Transform anchor = meleeStrikeVfxAnchor != null ? meleeStrikeVfxAnchor : meleeOrigin;
        if (anchor == null) anchor = transform;
        Vector3 pos = anchor.position + anchor.forward * (meleeSphereRadius * 0.5f);
        Quaternion rot = Quaternion.LookRotation(anchor.forward, Vector3.up);
        Instantiate(meleeStrikeVfxPrefab, pos, rot);
    }

    private void SpawnChargeEndStrikeVfx()
    {
        GameObject prefab = chargeEndStrikeVfxPrefab != null ? chargeEndStrikeVfxPrefab : meleeStrikeVfxPrefab;
        if (prefab == null) return;
        Transform anchor = meleeStrikeVfxAnchor != null ? meleeStrikeVfxAnchor : meleeOrigin;
        if (anchor == null) anchor = transform;
        Vector3 pos = anchor.position + anchor.forward * (meleeSphereRadius * 0.5f);
        Quaternion rot = Quaternion.LookRotation(anchor.forward, Vector3.up);
        Instantiate(prefab, pos, rot);
    }

    /// <summary>优先 BossAttackReceiver，否则 TopDownController（扣饥饿/氧气 + 受击动画）。</summary>
    private bool TryDamageFromBossHit(Collider col, float damage, Vector3 damageFromPosition)
    {
        if (col == null || col.GetComponentInParent<BossAI>() != null) return false;

        var recv = col.GetComponentInParent<BossAttackReceiver>();
        if (recv != null)
        {
            Vector3 hp = col.ClosestPoint(damageFromPosition);
            Vector3 dir = (hp - damageFromPosition).normalized;
            if (dir.sqrMagnitude < 1e-6f) dir = transform.forward;
            recv.TakeBossDamage(damage, hp, dir);
            return true;
        }

        var td = col.GetComponentInParent<TopDownController>();
        if (td != null)
        {
            td.ApplyBossOxygenDamage(damage);
            return true;
        }

        return false;
    }

    private bool MeleeOverlapDamage()
    {
        Vector3 c = meleeOrigin.position + meleeOrigin.forward * (meleeSphereRadius * 0.5f);
        int n = Physics.OverlapSphereNonAlloc(c, meleeSphereRadius, meleeHits, ~0, QueryTriggerInteraction.Collide);
        bool any = false;
        for (int i = 0; i < n; i++)
        {
            var col = meleeHits[i];
            if (col == null) continue;
            if (TryDamageFromBossHit(col, meleeDamage, c))
                any = true;
        }
        return any;
    }

    public BossState GetState() => state;

    private static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

    private static float FlatDistance(Vector3 a, Vector3 b)
    {
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b);
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawAggroGizmo) return;
        Gizmos.color = new Color(1f, 0.8f, 0.1f, 0.35f);
        Gizmos.DrawWireSphere(transform.position, aggroRadius);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, meleeRange);
    }
}
