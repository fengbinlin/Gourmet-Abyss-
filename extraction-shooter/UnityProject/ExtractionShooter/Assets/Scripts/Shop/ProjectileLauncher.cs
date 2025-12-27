using UnityEngine;

public class ProjectileLauncher : MonoBehaviour
{
    [Header("发射设置")]
    public Transform pointA;          // 起点
    public Transform pointB;          // 终点
    public float spawnInterval = 0.2f; // 生成间隔
    public int maxProjectiles = 20;   // 最大同时存在的抛射物数量

    [Header("飞行参数")]
    public float flightDuration = 2f;
    public float maxHeight = 5f;

    private float spawnTimer = 0f;
    private int currentProjectiles = 0;
    private bool isSpawning = false;

    private void Update()
    {
        UpdateProjectileCount();
    }

    public void SpawnProjectile(Transform start, Transform target, ResourceType itemType, int amount, System.Action onArrive)
    {
        GameObject projectileObj = FlyObjectPool.Instance.GetObject(start.position);
        ProjectileObject projectile = projectileObj.GetComponent<ProjectileObject>();
        if (projectile == null)
            projectile = projectileObj.AddComponent<ProjectileObject>();

        projectile.flightDuration = flightDuration;
        projectile.maxHeight = maxHeight;
        projectile.itemType = itemType;
        projectile.amount = amount;
        projectile.onArrive = onArrive;

        projectile.Launch(start, target);
        currentProjectiles++;
    }

    private void UpdateProjectileCount()
    {
        // 这里可以根据实际需要调整，或者让Projectile在销毁时通知发射器
        // 简单的实现：每5帧更新一次计数
        if (Time.frameCount % 5 == 0)
        {
            int count = 0;
            foreach (Transform child in FlyObjectPool.Instance.transform)
            {
                if (child.gameObject.activeSelf)
                {
                    count++;
                }
            }
            currentProjectiles = count;
        }
    }

    // 在Inspector中显示调试信息
    private void OnDrawGizmos()
    {
        if (pointA != null && pointB != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawSphere(pointA.position, 0.3f);
            Gizmos.color = Color.red;
            Gizmos.DrawSphere(pointB.position, 0.3f);
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(pointA.position, pointB.position);

            // 绘制抛物线示意
            Gizmos.color = Color.cyan;
            Vector3 prevPos = pointA.position;
            for (int i = 1; i <= 10; i++)
            {
                float t = i / 10f;
                Vector3 horizontalPos = Vector3.Lerp(pointA.position, pointB.position, t);
                float height = Mathf.Sin(t * Mathf.PI) * maxHeight;
                Vector3 currentPos = horizontalPos + Vector3.up * height;
                Gizmos.DrawLine(prevPos, currentPos);
                prevPos = currentPos;
            }
        }
    }
}