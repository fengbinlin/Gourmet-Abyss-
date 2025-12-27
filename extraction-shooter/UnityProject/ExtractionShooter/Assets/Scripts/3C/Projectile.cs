using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class Projectile : MonoBehaviour
{
    [Header("子弹基础参数")]
    [SerializeField] private float speed = 20f;
    [SerializeField] private float lifeTime = 5f;
    [SerializeField] private float damage = 10f;
    [SerializeField] private float size = 1f; // 子弹大小

    [Header("子弹高级属性")]
    [SerializeField] private int penetrationCount = 0; // 穿透次数
    [SerializeField] private float maxTravelDistance = 100f; // 最大飞行距离
    [Range(0f, 1f)]
    [SerializeField] private float criticalChance = 0.1f; // 暴击率
    [SerializeField] private float criticalMultiplier = 2f; // 暴击伤害倍率
    [SerializeField] private GameObject impactVFX;
    [SerializeField] private LayerMask hitLayers = ~0;

    [Header("碰撞设置")]
    [Tooltip("子弹发射后的无敌时间（避免自伤）")]
    [SerializeField] private float invulnerableTime = 0.1f;

    [Header("高级设置")]
    [SerializeField] private bool destroyOnAnyHit = true;
    [SerializeField] private float impactForce = 10f;

    [Header("伤害衰减设置")]
    [SerializeField] private bool useDamageFalloff = false;
    [SerializeField] private AnimationCurve damageFalloffCurve = AnimationCurve.Linear(0f, 1f, 1f, 0.5f);
    [SerializeField] private float maxFalloffDistance = 50f;

    [Header("调试设置")]
    [SerializeField] private bool debugMode = false;

    // 私有变量
    private Rigidbody rb;
    private Vector3 previousPosition;
    private Vector3 startPosition;
    private float spawnTime;
    private float currentSize = 1f;
    private Collider bulletCollider;
    private Renderer bulletRenderer;

    // 运行时属性
    private int currentPenetrationCount = 0;
    private float travelDistance = 0f;
    private float baseDamage = 0f;
    private float baseSpeed = 0f;
    private float baseSize = 1f;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        bulletCollider = GetComponent<Collider>();
        bulletRenderer = GetComponent<Renderer>();
    }

    private void Start()
    {
        currentSize = transform.localScale.x;
        previousPosition = transform.position;
        startPosition = transform.position;
        baseDamage = damage;
        baseSpeed = speed;
        baseSize = size;
        currentPenetrationCount = penetrationCount;
        spawnTime = Time.time;
        travelDistance = 0f;

        // 初始应用大小
        if (currentSize != 1f)
        {
            ApplySize(currentSize);
        }

        // 自动销毁
        if (lifeTime > 0)
        {
            Destroy(gameObject, lifeTime);
        }
        DetectInitialOverlap();

    }
    private void DetectInitialOverlap()
    {
        if (bulletCollider == null) return;

        // 使用子弹的碰撞器检测初始重叠
        Collider[] overlappingColliders = Physics.OverlapSphere(
            transform.position,
            GetColliderRadius(),
            hitLayers
        );

        foreach (Collider col in overlappingColliders)
        {
            if (col.gameObject != gameObject) // 忽略自己
            {
                if (debugMode) Debug.Log($"检测到初始重叠: {col.gameObject.name}");

                // 计算碰撞点（使用子弹中心或最近的表面点）
                Vector3 closestPoint = bulletCollider.ClosestPoint(col.transform.position);
                Vector3 hitPoint = col.ClosestPoint(closestPoint);

                // 处理碰撞
                HandleCollision(col.gameObject, hitPoint);

                // 如果子弹被销毁，跳出循环
                if (this == null) break;
            }
        }
    }

    private float GetColliderRadius()
    {
        if (bulletCollider is SphereCollider sphereColl)
        {
            return sphereColl.radius * transform.localScale.x;
        }
        else if (bulletCollider is CapsuleCollider capsuleColl)
        {
            return capsuleColl.radius * Mathf.Max(transform.localScale.x, transform.localScale.z);
        }
        else if (bulletCollider is BoxCollider boxColl)
        {
            // 对于盒子碰撞器，返回对角线的一半作为近似半径
            return boxColl.size.magnitude * 0.5f * transform.localScale.x;
        }
        return 0.5f * currentSize; // 默认
    }
    private void Update()
    {
        // 更新飞行距离
        if (rb != null && rb.velocity != Vector3.zero)
        {
            travelDistance += rb.velocity.magnitude * Time.deltaTime;

            // 检查最大飞行距离
            if (maxTravelDistance > 0 && travelDistance >= maxTravelDistance)
            {
                if (debugMode) Debug.Log($"子弹达到最大飞行距离: {travelDistance}");
                Destroy(gameObject);
            }
        }
    }

    public void Initialize(Vector3 direction, float newSpeed = 0f, float newDamage = 0f, float newSize = 0f,
        int penetration = 0, float maxDistance = 0f, float critChance = 0f, float critMultiplier = 1f)
    {
        if (rb == null) rb = GetComponent<Rigidbody>();

        // 重置状态
        spawnTime = Time.time;
        previousPosition = transform.position;
        startPosition = transform.position;
        travelDistance = 0f;

        // 应用传入的参数
        if (newSpeed > 0)
        {
            speed = newSpeed;
        }
        else
        {
            speed = baseSpeed;
        }

        if (newDamage > 0)
        {
            damage = newDamage;
        }
        else
        {
            damage = baseDamage;
        }

        if (newSize > 0 && Mathf.Abs(newSize - currentSize) > 0.01f)
        {
            //print("使用NewSize");
            currentSize = newSize;
            ApplySize(currentSize);
        }
        else
        {
            //print("使用BaseSize");
            print(newSize);
            currentSize = baseSize;
        }

        // 设置高级属性
        if (penetration >= 0)
        {
            penetrationCount = penetration;
            currentPenetrationCount = penetration;
        }

        if (maxDistance > 0)
        {
            maxTravelDistance = maxDistance;
        }

        if (critChance >= 0)
        {
            criticalChance = Mathf.Clamp01(critChance);
        }

        if (critMultiplier > 0)
        {
            criticalMultiplier = Mathf.Max(1f, critMultiplier);
        }

        // 设置速度和方向
        if (rb != null)
        {
            rb.useGravity = false;
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.velocity = direction.normalized * speed;
        }

        transform.forward = direction;

        if (debugMode)
        {
            Debug.Log($"子弹初始化 - 方向: {direction}, 速度: {speed}, 伤害: {damage}, 大小: {currentSize}, 穿透: {currentPenetrationCount}");
        }
    }

    public void SetDamageFalloff(bool useFalloff, AnimationCurve curve, float maxDistance)
    {
        useDamageFalloff = useFalloff;
        damageFalloffCurve = curve;
        maxFalloffDistance = maxDistance;
    }

    private void ApplySize(float newSize)
    {
        // 只调整transform的缩放
        transform.localScale = Vector3.one * newSize;
    }

    private float CalculateDamageWithFalloff()
    {
        if (!useDamageFalloff) return damage;

        float distance = Vector3.Distance(transform.position, startPosition);
        float normalizedDistance = Mathf.Clamp01(distance / maxFalloffDistance);
        float falloffMultiplier = damageFalloffCurve.Evaluate(normalizedDistance);

        return damage * falloffMultiplier;
    }

    private (float finalDamage, bool isCritical) CalculateFinalDamage()
    {
        float baseDamage = CalculateDamageWithFalloff();
        bool isCritical = Random.value <= criticalChance;
        float finalDamage = isCritical ? baseDamage * criticalMultiplier : baseDamage;

        if (debugMode && isCritical)
        {
            Debug.Log($"暴击！基础伤害: {baseDamage}, 最终伤害: {finalDamage}, 倍率: {criticalMultiplier}x");
        }

        return (finalDamage, isCritical);
    }

    private void FixedUpdate()
    {
        // 记录上一帧位置用于计算方向
        previousPosition = transform.position;

        // 调试显示
        if (debugMode)
        {
            Debug.DrawRay(transform.position, transform.forward, Color.red, 0.1f);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        HandleCollision(other.gameObject, other.ClosestPoint(transform.position));
    }

    private void OnCollisionEnter(Collision collision)
    {
        HandleCollision(collision.gameObject, collision.contacts[0].point);
    }

    private void HandleCollision(GameObject hitObject, Vector3 hitPoint)
    {
        // 检查无敌时间
        if (Time.time - spawnTime < invulnerableTime)
        {
            if (debugMode) Debug.Log("子弹无敌时间，忽略碰撞");
            return;
        }

        // 检查层级
        if (((1 << hitObject.layer) & hitLayers) == 0)
            return;

        // 计算子弹方向
        Vector3 bulletDirection = (transform.position - previousPosition).normalized;
        if (bulletDirection == Vector3.zero)
        {
            bulletDirection = transform.forward;
        }

        Vector3 hitNormal = (transform.position - hitObject.transform.position).normalized;

        // 计算最终伤害
        var damageInfo = CalculateFinalDamage();
        float finalDamage = damageInfo.finalDamage;
        bool isCritical = damageInfo.isCritical;

        if (debugMode)
        {
            Debug.Log($"子弹命中: {hitObject.name}, 伤害: {finalDamage}, 暴击: {isCritical}, 剩余穿透: {currentPenetrationCount}");
        }

        // 处理伤害
        EnemyHealth enemyHealth = hitObject.GetComponent<EnemyHealth>();
        if (enemyHealth != null)
        {
            enemyHealth.TakeDamageFromProjectile(finalDamage, hitPoint, hitNormal, previousPosition, bulletDirection);

            if (impactForce > 0)
            {
                Rigidbody enemyRb = hitObject.GetComponent<Rigidbody>();
                if (enemyRb != null)
                {
                    enemyRb.AddForce(bulletDirection * impactForce, ForceMode.Impulse);
                }
            }
        }

        // 播放特效
        if (impactVFX != null)
        {
            Quaternion rotation = Quaternion.LookRotation(hitNormal);
            Instantiate(impactVFX, hitPoint, rotation);
        }

        // 处理穿透
        if (currentPenetrationCount > 0)
        {
            currentPenetrationCount--;
            if (debugMode) Debug.Log($"穿透剩余: {currentPenetrationCount}");
            return; // 穿透时不销毁子弹
        }

        // 销毁子弹
        if (destroyOnAnyHit)
        {
            if (debugMode) Debug.Log("销毁子弹（碰撞）");
            Destroy(gameObject);
        }
    }

    // 获取属性
    public float GetSpeed() => speed;
    public float GetDamage() => damage;
    public float GetSize() => currentSize;
    public int GetPenetrationCount() => penetrationCount;
    public float GetMaxTravelDistance() => maxTravelDistance;
    public float GetCriticalChance() => criticalChance;
    public float GetCriticalMultiplier() => criticalMultiplier;
    public float GetTravelDistance() => travelDistance;

    // 设置属性
    public void SetSpeed(float newSpeed) => speed = newSpeed;
    public void SetDamage(float newDamage) => damage = newDamage;
    public void SetSize(float newSize)
    {
        currentSize = newSize;
        ApplySize(currentSize);
    }
    public void SetPenetrationCount(int count)
    {
        penetrationCount = Mathf.Max(0, count);
        currentPenetrationCount = penetrationCount;
    }
    public void SetMaxTravelDistance(float distance) => maxTravelDistance = distance;
    public void SetCriticalChance(float chance) => criticalChance = Mathf.Clamp01(chance);
    public void SetCriticalMultiplier(float multiplier) => criticalMultiplier = Mathf.Max(1f, multiplier);

    private void OnDrawGizmosSelected()
    {
        if (debugMode)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, 0.1f * transform.localScale.x);

            Gizmos.color = Color.yellow;
            Gizmos.DrawRay(transform.position, transform.forward * 1f);
        }
    }
}