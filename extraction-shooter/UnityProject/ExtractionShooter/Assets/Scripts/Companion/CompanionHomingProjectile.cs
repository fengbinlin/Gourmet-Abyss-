using UnityEngine;

/// <summary>
/// 随从追踪子弹：锁定目标并命中后对 EnemyHealth 造成伤害。
/// </summary>
public class CompanionHomingProjectile : MonoBehaviour
{
    private EnemyHealth target;
    private float damage;
    private float moveSpeed;
    private float rotateSpeed;
    private float lifeTime;
    private float hitDistance;
    private float bulletSize = 1f;
    private float homingPathOffsetRadius = 0.6f;
    private float slowRatioOnHit = 0f;
    private float slowDurationOnHit = 0f;
    private Vector3 perProjectileHomingOffset = Vector3.zero;
    private bool initialized;

    [Header("命中特效（由子弹决定）")]
    [SerializeField] private GameObject impactVfxPrefab;
    [SerializeField] private float impactVfxLifeTime = 2f;

    public void Initialize(
        EnemyHealth targetHealth,
        float projectileDamage,
        float projectileMoveSpeed,
        float projectileRotateSpeed,
        float projectileLifeTime,
        float projectileHitDistance,
        float projectileSize,
        float projectileHomingPathOffsetRadius,
        Vector3 projectileHomingOffset,
        float projectileSlowRatioOnHit,
        float projectileSlowDurationOnHit,
        GameObject projectileImpactVfxPrefab = null)
    {
        target = targetHealth;
        damage = projectileDamage;
        moveSpeed = projectileMoveSpeed;
        rotateSpeed = projectileRotateSpeed;
        lifeTime = projectileLifeTime;
        hitDistance = Mathf.Max(0.05f, projectileHitDistance);
        bulletSize = Mathf.Max(0.01f, projectileSize);
        homingPathOffsetRadius = Mathf.Max(0f, projectileHomingPathOffsetRadius);
        slowRatioOnHit = Mathf.Clamp01(projectileSlowRatioOnHit);
        slowDurationOnHit = Mathf.Max(0f, projectileSlowDurationOnHit);
        perProjectileHomingOffset = projectileHomingOffset;
        if (projectileImpactVfxPrefab != null)
        {
            impactVfxPrefab = projectileImpactVfxPrefab;
        }
        if (perProjectileHomingOffset.sqrMagnitude > homingPathOffsetRadius * homingPathOffsetRadius && homingPathOffsetRadius > 0.001f)
        {
            perProjectileHomingOffset = perProjectileHomingOffset.normalized * homingPathOffsetRadius;
        }

        // 缩放整个子弹（prefab 如果有Collider/Render 一般会跟随缩放）
        transform.localScale = Vector3.one * bulletSize;
        initialized = true;
    }

    private void Update()
    {
        if (!initialized)
        {
            Destroy(gameObject);
            return;
        }

        lifeTime -= Time.deltaTime;
        if (lifeTime <= 0f)
        {
            Destroy(gameObject);
            return;
        }

        if (target == null)
        {
            Destroy(gameObject);
            return;
        }

        // 追踪目标偏上方的命中点，并叠加每发子弹独立的追踪偏移（靠近目标时逐渐收敛到目标中心）
        Vector3 aimPoint = target.transform.position + Vector3.up * 0.5f;
        Vector3 toTargetCenterFlat = new Vector3(aimPoint.x, transform.position.y, aimPoint.z) - transform.position;
        float distanceToTargetCenter = toTargetCenterFlat.magnitude;
        float offsetFade = Mathf.Clamp01(distanceToTargetCenter / Mathf.Max(hitDistance * 2.5f, 0.2f));
        aimPoint += perProjectileHomingOffset * offsetFade;
        Vector3 aimPointFlat = new Vector3(aimPoint.x, transform.position.y, aimPoint.z);
        Vector3 toAim = aimPointFlat - transform.position;

        float hitDistSqr = hitDistance * hitDistance;
        if (toAim.sqrMagnitude <= hitDistSqr)
        {
            HitTarget(aimPoint);
            return;
        }

        // 目标方向过近时，LookRotation 会不稳定；给个保护
        Vector3 dir = toAim.sqrMagnitude > 0.000001f ? toAim.normalized : transform.forward;
        Quaternion targetRotation = Quaternion.LookRotation(dir, Vector3.up);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, rotateSpeed * Time.deltaTime);

        // 预测下一帧位置，避免高速步进造成“跨过命中距离但本帧没命中”
        Vector3 currentPos = transform.position;
        Vector3 nextPos = currentPos + transform.forward * moveSpeed * Time.deltaTime;

        if ((aimPointFlat - nextPos).sqrMagnitude <= hitDistSqr)
        {
            transform.position = nextPos;
            HitTarget(aimPoint);
            return;
        }

        transform.position = nextPos;
    }

    private void HitTarget(Vector3 hitPoint)
    {
        if (target != null)
        {
            Vector3 hitNormal = (target.transform.position - transform.position).normalized;
            Vector3 bulletDirection = transform.forward;

            if (impactVfxPrefab != null)
            {
                Quaternion rot = hitNormal.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(hitNormal) : Quaternion.identity;
                GameObject vfx = Instantiate(impactVfxPrefab, hitPoint, rot);
                if (impactVfxLifeTime > 0f) Destroy(vfx, impactVfxLifeTime);
            }
            target.TakeDamageFromProjectile(damage, hitPoint, hitNormal, transform.position, bulletDirection);

            EnemyAI enemyAI = target.GetComponent<EnemyAI>();
            if (enemyAI != null && slowRatioOnHit > 0f && slowDurationOnHit > 0f)
            {
                enemyAI.ApplyMoveSlow(slowRatioOnHit, slowDurationOnHit);
            }
        }

        Destroy(gameObject);
    }
}
