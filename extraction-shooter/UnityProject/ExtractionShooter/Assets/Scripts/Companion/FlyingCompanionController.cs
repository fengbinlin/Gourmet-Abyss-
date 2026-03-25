using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 主角飞行随从：跟随主角并自动攻击范围内敌人。
/// </summary>
public class FlyingCompanionController : MonoBehaviour, IPetSystem
{
    private const float MinAutoFanAngleWhenBurst = 12f;

    [Header("跟随设置")]
    [SerializeField] private TopDownController playerController;
    [SerializeField] private Vector3 localFollowOffset = new Vector3(0f, 1.8f, -2.2f);
    [SerializeField] private float followLerpSpeed = 8f;
    [SerializeField] private float lookAtPlayerSpeed = 12f;

    [Header("攻击设置")]
    [SerializeField] private float attackRange = 12f;
    [SerializeField] private float fireInterval = 0.45f;
    [SerializeField] private float bulletDamage = 12f;
    [SerializeField] private GameObject homingBulletPrefab;
    [SerializeField] private Transform firePoint;
    [SerializeField] private LayerMask enemyLayerMask = ~0;

    [Header("子弹参数")]
    [SerializeField] private float bulletSize = 1f;
    [SerializeField] private int burstBulletCount = 1;
    [SerializeField] private float burstFanAngle = 0f; // degrees, 子弹在该角度范围内均匀分布
    [SerializeField] private float bulletMoveSpeed = 18f;
    [SerializeField] private float bulletMoveSpeedRandomFactor = 0.1f; // 多发时每发速度随机幅度（±10%）
    [SerializeField] private float bulletSizeRandomFactor = 0.08f; // 多发时每发大小随机幅度（±8%）
    [SerializeField] private float bulletRotateSpeed = 540f;
    [SerializeField] private float bulletLifeTime = 4f;
    [SerializeField] private float bulletHitDistance = 0.35f;
    [SerializeField] private float homingPathOffsetRadius = 0.6f; // 每发子弹的目标偏移半径，增加路径差异
    [SerializeField] private float rotateSpeedRandomFactor = 0.15f; // 每发子弹追踪转速随机扰动比例（0.15 => ±15%）

    [Header("命中减速参数（最终=初始*乘数）")]
    [SerializeField] private float slowRatioBase = 0.2f;
    [SerializeField] private float slowDurationBase = 1.5f;
    [SerializeField] private float slowRatioMultiplier = 1f;
    [SerializeField] private float slowDurationMultiplier = 1f;

    private float fireTimer;

    private void Awake()
    {
        if (playerController == null)
        {
            playerController = FindFirstObjectByType<TopDownController>();
        }
    }

    private void Update()
    {
        if (playerController == null) return;

        FollowPlayer();
        TryAttack();
    }

    private void FollowPlayer()
    {
        Transform player = playerController.transform;
        Vector3 targetPos = player.TransformPoint(localFollowOffset);
        transform.position = Vector3.Lerp(transform.position, targetPos, followLerpSpeed * Time.deltaTime);

        Vector3 toPlayer = player.position - transform.position;
        toPlayer.y = 0f;
        if (toPlayer.sqrMagnitude > 0.001f)
        {
            Quaternion targetRot = Quaternion.LookRotation(toPlayer.normalized);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, lookAtPlayerSpeed * Time.deltaTime);
        }
    }

    private void TryAttack()
    {
        fireTimer -= Time.deltaTime;
        if (fireTimer > 0f) return;

        Transform shootPoint = firePoint != null ? firePoint : transform;
        int count = Mathf.Max(1, burstBulletCount);
        List<EnemyHealth> targets = FindNearestEnemiesInRange(count);
        if (targets.Count == 0) return;

        EnemyHealth primaryTarget = targets[0];

        // 基于目标方向做一个“水平扇形”的初始朝向（后续会由追踪逻辑再微调）
        Vector3 baseDir = (primaryTarget.transform.position - shootPoint.position);
        baseDir.y = 0f;
        if (baseDir.sqrMagnitude < 0.0001f) baseDir = shootPoint.forward;

        Quaternion baseRot = Quaternion.LookRotation(baseDir.normalized, Vector3.up);

        float fanAngle = Mathf.Max(0f, burstFanAngle);
        if (count > 1 && fanAngle <= 0.01f)
        {
            // 防止多发子弹完全重叠，视觉上像“只发了一发”
            fanAngle = MinAutoFanAngleWhenBurst;
        }
        float step = count > 1 ? fanAngle / (count - 1) : 0f;

        for (int i = 0; i < count; i++)
        {
            EnemyHealth shotTarget = i < targets.Count ? targets[i] : primaryTarget;
            float offset = count > 1 ? (-fanAngle * 0.5f + i * step) : 0f;
            Quaternion rot = Quaternion.AngleAxis(offset, Vector3.up) * baseRot;

            // 子弹路径偏移：如果目标不足导致复用同一个目标，就提高偏移量，尽量让轨迹分离
            float offsetScale = i < targets.Count ? 1f : (1.8f + 0.35f * (i - targets.Count + 1));
            float offsetRadius = Mathf.Max(0f, homingPathOffsetRadius * offsetScale);
            Vector3 shotHomingOffset = Quaternion.AngleAxis(offset, Vector3.up) * (Vector3.right * offsetRadius);

            GameObject bulletObj = Instantiate(homingBulletPrefab, shootPoint.position, rot);
            CompanionHomingProjectile bullet = bulletObj.GetComponent<CompanionHomingProjectile>();
            if (bullet != null)
            {
                float rotateRand = Random.Range(-rotateSpeedRandomFactor, rotateSpeedRandomFactor);
                float finalRotateSpeed = bulletRotateSpeed * (1f + rotateRand);

                // 仅在多发时加入随机，保证单发手感稳定
                float speedRand = (count > 1) ? Random.Range(-bulletMoveSpeedRandomFactor, bulletMoveSpeedRandomFactor) : 0f;
                float sizeRand = (count > 1) ? Random.Range(-bulletSizeRandomFactor, bulletSizeRandomFactor) : 0f;
                float finalMoveSpeed = Mathf.Max(0.01f, bulletMoveSpeed * (1f + speedRand));
                float finalBulletSize = Mathf.Max(0.01f, bulletSize * (1f + sizeRand));

                // 仅做减速，不做定身；最终仍会在 EnemyAI 侧再次保护钳制
                float finalSlowRatio = Mathf.Clamp(slowRatioBase * slowRatioMultiplier, 0f, 0.85f);
                float finalSlowDuration = Mathf.Max(0f, slowDurationBase * slowDurationMultiplier);

                bullet.Initialize(
                    shotTarget,
                    bulletDamage,
                    finalMoveSpeed,
                    Mathf.Max(0.01f, finalRotateSpeed),
                    bulletLifeTime,
                    bulletHitDistance,
                    finalBulletSize,
                    offsetRadius,
                    shotHomingOffset,
                    finalSlowRatio,
                    finalSlowDuration
                );
            }
        }

        Debug.Log($"飞行随从发射子弹 x{count}，锁定目标数={targets.Count}，扇形角={fanAngle:F1}，主目标：{primaryTarget.name}");
        fireTimer = fireInterval;
    }

    private EnemyHealth FindNearestEnemyInRange()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, attackRange, enemyLayerMask);
        if (hits == null || hits.Length == 0) return null;

        float nearestSqrDistance = float.MaxValue;
        EnemyHealth nearest = null;

        for (int i = 0; i < hits.Length; i++)
        {
            EnemyHealth enemyHealth = hits[i].GetComponentInParent<EnemyHealth>();
            if (enemyHealth == null) continue;

            EnemyAI enemyAI = enemyHealth.GetComponent<EnemyAI>();
            if (enemyAI != null && enemyAI.GetCurrentState() == EnemyState.Dead) continue;

            float sqrDistance = (enemyHealth.transform.position - transform.position).sqrMagnitude;
            if (sqrDistance < nearestSqrDistance)
            {
                nearestSqrDistance = sqrDistance;
                nearest = enemyHealth;
            }
        }

        return nearest;
    }

    private List<EnemyHealth> FindNearestEnemiesInRange(int maxCount)
    {
        List<EnemyHealth> result = new List<EnemyHealth>();
        if (maxCount <= 0) return result;

        Collider[] hits = Physics.OverlapSphere(transform.position, attackRange, enemyLayerMask);
        if (hits == null || hits.Length == 0) return result;

        HashSet<EnemyHealth> unique = new HashSet<EnemyHealth>();
        List<EnemyHealth> candidates = new List<EnemyHealth>();

        for (int i = 0; i < hits.Length; i++)
        {
            EnemyHealth enemyHealth = hits[i].GetComponentInParent<EnemyHealth>();
            if (enemyHealth == null) continue;
            if (!unique.Add(enemyHealth)) continue;

            EnemyAI enemyAI = enemyHealth.GetComponent<EnemyAI>();
            if (enemyAI != null && enemyAI.GetCurrentState() == EnemyState.Dead) continue;

            candidates.Add(enemyHealth);
        }

        candidates.Sort((a, b) =>
        {
            float da = (a.transform.position - transform.position).sqrMagnitude;
            float db = (b.transform.position - transform.position).sqrMagnitude;
            return da.CompareTo(db);
        });

        int take = Mathf.Min(maxCount, candidates.Count);
        for (int i = 0; i < take; i++)
        {
            result.Add(candidates[i]);
        }

        return result;
    }

    public void ApplyGrowth(PetGrowthValues growth)
    {
        if (growth == null) return;

        attackRange = Mathf.Max(0.1f, growth.attackRange);
        fireInterval = Mathf.Max(0.01f, growth.fireInterval);
        bulletDamage = Mathf.Max(0f, growth.bulletDamage);

        bulletSize = Mathf.Max(0.01f, growth.bulletSize);
        burstBulletCount = Mathf.Max(1, growth.burstBulletCount);
        burstFanAngle = Mathf.Max(0f, growth.burstFanAngle);

        bulletMoveSpeed = Mathf.Max(0.01f, growth.bulletMoveSpeed);
        bulletRotateSpeed = Mathf.Max(0.01f, growth.bulletRotateSpeed);
        bulletLifeTime = Mathf.Max(0.01f, growth.bulletLifeTime);
        bulletHitDistance = Mathf.Max(0.01f, growth.bulletHitDistance);

        slowRatioBase = Mathf.Clamp01(growth.slowRatioBase);
        slowDurationBase = Mathf.Max(0f, growth.slowDurationBase);
        slowRatioMultiplier = Mathf.Max(0f, growth.slowRatioMultiplier);
        slowDurationMultiplier = Mathf.Max(0f, growth.slowDurationMultiplier);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}

