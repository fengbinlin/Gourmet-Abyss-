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
    private bool initialized;

    public void Initialize(
        EnemyHealth targetHealth,
        float projectileDamage,
        float projectileMoveSpeed,
        float projectileRotateSpeed,
        float projectileLifeTime,
        float projectileHitDistance)
    {
        target = targetHealth;
        damage = projectileDamage;
        moveSpeed = projectileMoveSpeed;
        rotateSpeed = projectileRotateSpeed;
        lifeTime = projectileLifeTime;
        hitDistance = Mathf.Max(0.05f, projectileHitDistance);
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

        Vector3 targetPos = target.transform.position + Vector3.up * 0.5f;
        Vector3 toTarget = targetPos - transform.position;

        if (toTarget.sqrMagnitude <= hitDistance * hitDistance)
        {
            HitTarget(targetPos);
            return;
        }

        Vector3 dir = toTarget.normalized;
        Quaternion targetRotation = Quaternion.LookRotation(dir);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, rotateSpeed * Time.deltaTime);
        transform.position += transform.forward * moveSpeed * Time.deltaTime;
    }

    private void HitTarget(Vector3 hitPoint)
    {
        if (target != null)
        {
            Vector3 hitNormal = (target.transform.position - transform.position).normalized;
            Vector3 bulletDirection = transform.forward;
            target.TakeDamageFromProjectile(damage, hitPoint, hitNormal, transform.position, bulletDirection);
        }

        Destroy(gameObject);
    }
}
