using UnityEngine;

/// <summary>
/// BOSS 抛射物。仅使用 Trigger 检测命中，不产生实体刚体碰撞。
/// 命中实现 IBossAttackTarget 的对象时造成伤害。
/// </summary>
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Collider))]
public class BossProjectile : MonoBehaviour
{
    [Header("运动")]
    [SerializeField] private float travelSpeed = 18f;
    [SerializeField] private bool useGravityArc = true;
    [Tooltip("弹道模式下：>0 则竖直初速度额外加上该值（会略微偏离落点，一般保持 0）")]
    [SerializeField] private float arcUpwardSpeedOverride = 0f;
    [Tooltip("抛物线基准水平速度 = travelSpeed × 该值")]
    [Range(0.15f, 1f)] [SerializeField] private float ballisticHorizontalSpeedScale = 0.5f;
    [Tooltip(">1 时降低水平速度、拉长飞行时间，用同一落点方程算出更高弧线（仍精确命中目标点）")]
    [Min(0.5f)] [SerializeField] private float arcHeightMultiplier = 1.2f;

    [Header("伤害")]
    [SerializeField] private float damage = 25f;
    [SerializeField] private LayerMask hitLayers = ~0;
    [SerializeField] private float lifeTime = 8f;
    [Tooltip("生成后短时间内不结算，避免与 BOSS 碰撞体重叠")]
    [SerializeField] private float spawnInvulnTime = 0.12f;

    [Header("命中表现")]
    [SerializeField] private GameObject hitVfxPrefab;
    [SerializeField] private bool destroyOnHit = true;
    [SerializeField] private bool debugHitLog = true;

    private Rigidbody rb;
    private float spawnTime;
    private Vector3 previousPos;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        EnsureTriggersOnly();
    }

    /// <summary>
    /// 全部碰撞体设为 Trigger，避免与其它子弹/物体发生实体挤压。
    /// </summary>
    private void EnsureTriggersOnly()
    {
        Collider[] cols = GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < cols.Length; i++)
        {
            if (cols[i] != null)
                cols[i].isTrigger = true;
        }
    }

    private void OnEnable()
    {
        spawnTime = Time.time;
        previousPos = transform.position;
        if (lifeTime > 0f)
            Destroy(gameObject, lifeTime);
    }

    /// <summary>
    /// 从起点以弹道抛向目标点（世界坐标）。目标点建议已通过射线贴在地面。
    /// </summary>
    /// <param name="ballisticHScale">覆盖预制体上的水平比例；&lt;0 则用预制体 ballisticHorizontalSpeedScale</param>
    /// <param name="arcVertMul">覆盖预制体上的弧线高度倍率；&lt;0 则用预制体 arcHeightMultiplier</param>
    public void LaunchFromTo(Vector3 worldStart, Vector3 worldTarget, float speed, float dmg, bool ballistic,
        float ballisticHScale = -1f, float arcVertMul = -1f)
    {
        transform.position = worldStart;
        travelSpeed = speed;
        damage = dmg;
        useGravityArc = ballistic;
        spawnTime = Time.time;
        previousPos = worldStart;

        rb.velocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        Vector3 delta = worldTarget - worldStart;
        Vector3 flat = new Vector3(delta.x, 0f, delta.z);
        float dist = flat.magnitude;
        if (dist < 0.05f)
        {
            rb.useGravity = useGravityArc;
            rb.velocity = Vector3.down * 2f;
            return;
        }

        Vector3 dir = flat / dist;

        if (!useGravityArc)
        {
            rb.useGravity = false;
            Vector3 v = new Vector3(dir.x, delta.y / Mathf.Max(0.1f, dist), dir.z).normalized * travelSpeed;
            rb.velocity = v;
            if (v.sqrMagnitude > 0.01f)
                transform.forward = v.normalized;
            return;
        }

        rb.useGravity = true;
        float hScale = ballisticHScale >= 0f
            ? Mathf.Clamp(ballisticHScale, 0.15f, 1f)
            : ballisticHorizontalSpeedScale;
        float arcMul = arcVertMul >= 0f ? Mathf.Max(0.5f, arcVertMul) : arcHeightMultiplier;

        float g = Physics.gravity.magnitude;
        if (g < 0.01f) g = 9.81f;

        // 水平距离 d、高度差 dy，在恒定水平速率 vH 下飞行时间 t = d/vH，需满足：dy = vy*t - 0.5*g*t²
        // => vy = (dy + 0.5*g*t²) / t。弧线更高时降低 vH（除以 √arcMul）再重算 vy，落点仍落在 worldTarget。
        float baseVh = travelSpeed * hScale;
        float vHorizontal = baseVh / Mathf.Sqrt(arcMul);
        float tFlight = dist / Mathf.Max(0.01f, vHorizontal);
        float dy = delta.y;
        float vy = (dy + 0.5f * g * tFlight * tFlight) / Mathf.Max(0.01f, tFlight);
        if (arcUpwardSpeedOverride > 0.01f)
            vy += arcUpwardSpeedOverride;

        Vector3 vel = new Vector3(dir.x * vHorizontal, vy, dir.z * vHorizontal);
        rb.velocity = vel;
        if (vel.sqrMagnitude > 1e-6f)
            transform.forward = vel.normalized;
        EnsureTriggersOnly();
    }

    private void FixedUpdate()
    {
        previousPos = transform.position;
    }

    private void OnTriggerEnter(Collider other)
    {
        TryHit(other);
    }

    private void TryHit(Collider other)
    {
        if (Time.time - spawnTime < spawnInvulnTime)
            return;

        if (other.GetComponentInParent<BossProjectile>() != null)
            return;

        if (((1 << other.gameObject.layer) & hitLayers) == 0)
        {
            if (debugHitLog)
                Debug.Log($"[BossProjectile] Layer 被忽略: {LayerMask.LayerToName(other.gameObject.layer)} ({other.gameObject.name})，请检查 Hit Layers。", this);
            return;
        }

        if (other.GetComponentInParent<BossAI>() != null)
            return;

        Vector3 hitPoint = other.ClosestPoint(transform.position);
        Vector3 bulletDir = (transform.position - previousPos).normalized;
        if (bulletDir.sqrMagnitude < 1e-6f)
            bulletDir = transform.forward;

        var recv = other.GetComponentInParent<BossAttackReceiver>();
        if (recv != null)
        {
            if (debugHitLog)
                Debug.Log($"[BossProjectile] 命中 BossAttackReceiver: {recv.gameObject.name}", this);
            recv.TakeBossDamage(damage, hitPoint, bulletDir);
        }
        else
        {
            bool found = false;
            var targets = other.GetComponentsInParent<MonoBehaviour>();
            foreach (var mb in targets)
            {
                if (mb is IBossAttackTarget t)
                {
                    if (debugHitLog)
                        Debug.Log($"[BossProjectile] 命中 IBossAttackTarget: {mb.GetType().Name} on {mb.gameObject.name}", this);
                    t.TakeBossDamage(damage, hitPoint, bulletDir);
                    found = true;
                    break;
                }
            }
            if (!found && debugHitLog)
                Debug.LogWarning($"[BossProjectile] 碰到 {other.gameObject.name} 但未找到 BossAttackReceiver / IBossAttackTarget，无法扣氧。", this);
        }

        if (hitVfxPrefab != null)
            Instantiate(hitVfxPrefab, hitPoint, Quaternion.LookRotation(-bulletDir));

        if (destroyOnHit)
            Destroy(gameObject);
    }
}

/// <summary>
/// 玩家或受 BOSS 技能伤害的物体实现此接口。
/// </summary>
public interface IBossAttackTarget
{
    void TakeBossDamage(float damage, Vector3 worldPoint, Vector3 worldDirection);
}
