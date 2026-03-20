using UnityEngine;

/// <summary>
/// 主角飞行随从：跟随主角并自动攻击范围内敌人。
/// </summary>
public class FlyingCompanionController : MonoBehaviour
{
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
    [SerializeField] private float bulletMoveSpeed = 18f;
    [SerializeField] private float bulletRotateSpeed = 540f;
    [SerializeField] private float bulletLifeTime = 4f;
    [SerializeField] private float bulletHitDistance = 0.35f;

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

        EnemyHealth target = FindNearestEnemyInRange();
        if (target == null) return;

        Transform shootPoint = firePoint != null ? firePoint : transform;
        GameObject bulletObj = Instantiate(homingBulletPrefab, shootPoint.position, shootPoint.rotation);
        CompanionHomingProjectile bullet = bulletObj.GetComponent<CompanionHomingProjectile>();
        if (bullet != null)
        {
            bullet.Initialize(
                target,
                bulletDamage,
                bulletMoveSpeed,
                bulletRotateSpeed,
                bulletLifeTime,
                bulletHitDistance
            );
        }
        Debug.Log("飞行随从发射子弹，目标：" + target.name);
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

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}
