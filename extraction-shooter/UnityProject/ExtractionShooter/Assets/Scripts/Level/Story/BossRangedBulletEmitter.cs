using UnityEngine;

/// <summary>
/// 恶霸“子弹发射器”：
/// 负责实例化 <see cref="BossProjectile"/> 并计算落点（可用 Raycast 投影到地面）。
/// 真正扣氧气的逻辑在 <see cref="BossProjectile"/> + <see cref="BossAttackReceiver"/> 里完成。
/// </summary>
public class BossRangedBulletEmitter : MonoBehaviour
{
    public enum FirePattern
    {
        AimSingle,
        FanTowardPlayer,
        RadialBurst
    }

    [Header("发射物")]
    [SerializeField] private GameObject bossProjectilePrefab;
    [SerializeField] private Transform spawnPoint;

    [Header("投射物运动")]
    [SerializeField] private float projectileSpeed = 16f;
    [SerializeField] private bool projectileUseBallistic = true;
    [SerializeField] private float projectileBallisticHorizontalScale = 0.48f;
    [SerializeField] private float projectileArcHeightMultiplier = 1.35f;
    [SerializeField] private float projectileDamage = 22f;

    [Header("命中落点（投影到地面）")]
    [SerializeField] private LayerMask groundLayers = ~0;
    [SerializeField] private float groundRayHeight = 6f;
    [SerializeField] private float groundRayDistance = 30f;
    [SerializeField] private float targetUpOffset = 0.05f;

    private Vector3 SpawnPos => spawnPoint != null ? spawnPoint.position : transform.position;

    public void ShootAtTargetPoint(Vector3 worldStart, Vector3 worldTargetPoint)
    {
        if (bossProjectilePrefab == null) return;

        var go = Instantiate(bossProjectilePrefab, SpawnPos, Quaternion.identity);
        var bp = go.GetComponent<BossProjectile>();
        if (bp == null) return;

        bp.LaunchFromTo(
            SpawnPos,
            worldTargetPoint + Vector3.up * targetUpOffset,
            projectileSpeed,
            projectileDamage,
            projectileUseBallistic,
            projectileBallisticHorizontalScale,
            projectileArcHeightMultiplier
        );
    }

    public void ShootAtPlayer(Transform player)
    {
        if (player == null) return;

        Vector3 aimOrigin = player.position + Vector3.up * groundRayHeight;
        Vector3 targetPoint = player.position;

        if (Physics.Raycast(aimOrigin, Vector3.down, out RaycastHit hit, groundRayDistance, groundLayers,
                QueryTriggerInteraction.Ignore))
        {
            targetPoint = hit.point;
        }

        ShootAtTargetPoint(transform.position, targetPoint);
    }

    private bool TryProjectToGround(Vector3 flatWorldPoint, out Vector3 groundPoint)
    {
        // flatWorldPoint：只关心 XZ；Y 会由射线决定
        Vector3 rayOrigin = flatWorldPoint + Vector3.up * groundRayHeight;
        if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, groundRayDistance, groundLayers,
                QueryTriggerInteraction.Ignore))
        {
            groundPoint = hit.point;
            return true;
        }

        groundPoint = flatWorldPoint;
        return false;
    }

    /// <summary>
    /// 扇形：以“朝向玩家的水平方向”为中心，左右展开多个角度。
    /// </summary>
    public void ShootFanTowardPlayer(Transform player, int bulletCount, float totalSpreadDegrees, float distanceToProject)
    {
        if (player == null) return;
        if (bulletCount <= 0) return;

        Vector3 toPlayerFlat = player.position - SpawnPos;
        toPlayerFlat.y = 0f;
        if (toPlayerFlat.sqrMagnitude < 0.001f) return;
        Vector3 baseDir = toPlayerFlat.normalized;

        float half = Mathf.Max(0.01f, totalSpreadDegrees) * 0.5f;
        if (distanceToProject <= 0f)
            distanceToProject = toPlayerFlat.magnitude;

        Vector3 originFlat = SpawnPos;
        originFlat.y = 0f;

        for (int i = 0; i < bulletCount; i++)
        {
            float t = bulletCount == 1 ? 0f : (float)i / (bulletCount - 1);
            float yaw = Mathf.Lerp(-half, half, t);
            Vector3 dir = Quaternion.Euler(0f, yaw, 0f) * baseDir;
            Vector3 targetFlat = originFlat + dir * distanceToProject;

            if (!TryProjectToGround(targetFlat, out var ground))
                ground = targetFlat;

            ShootAtTargetPoint(SpawnPos, ground);
        }
    }

    /// <summary>
    /// 四面八方：在 XZ 平面上均匀分布角度，向圆周方向投射落点。
    /// </summary>
    public void ShootRadialBurst(int bulletCount, float radiusAround, float startYawDegOffset = 0f)
    {
        if (bossProjectilePrefab == null) return;
        if (bulletCount <= 0) return;

        Vector3 originFlat = SpawnPos;
        originFlat.y = 0f;

        float radius = Mathf.Max(0.1f, radiusAround);
        float step = 360f / bulletCount;
        for (int i = 0; i < bulletCount; i++)
        {
            float yaw = startYawDegOffset + i * step;
            Vector3 dir = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
            Vector3 targetFlat = originFlat + dir * radius;

            if (!TryProjectToGround(targetFlat, out var ground))
                ground = targetFlat;

            ShootAtTargetPoint(SpawnPos, ground);
        }
    }
}

