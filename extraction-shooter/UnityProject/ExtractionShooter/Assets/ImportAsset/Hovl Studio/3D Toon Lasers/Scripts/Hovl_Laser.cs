using System.Collections.Generic;
using UnityEngine;

public class Hovl_Laser : MonoBehaviour
{
    [Header("激光视觉设置")]
    public GameObject HitEffect;
    public float HitOffset = 0;
    public bool useLaserRotation = false;
    public float MaxLength = 50f;
    private LineRenderer Laser;
    public float MainTextureLength = 1f;
    public float NoiseTextureLength = 1f;
    private Vector4 Length = new Vector4(1, 1, 1, 1);
    private bool LaserSaver = false;
    private bool UpdateSaver = false;
    private ParticleSystem[] Effects;
    private ParticleSystem[] Hit;
    private bool isChainLaser = false;
    [HideInInspector] public bool hasSpawnedChain = false;
    [Header("激光宽度设置")]
    [SerializeField] private float laserWidth = 0.5f;
    [SerializeField] private float startWidthMultiplier = 1f;
    [SerializeField] private float endWidthMultiplier = 1f;

    [Header("贯穿设置")]
    [SerializeField] private bool penetrateObjects = true;   // 是否贯穿
    [SerializeField] private int maxPenetrations = 5;
    [SerializeField] private float penetrationDamageReduction = 0.8f;

    [Header("伤害设置")]
    [SerializeField] private float damagePerSecond = 20f;
    [SerializeField] private float damageTickInterval = 0.1f;
    [SerializeField] private LayerMask damageLayers = ~0;
    [SerializeField] private GameObject damageEffect;

    [Header("暴击设置")]
    [SerializeField][Range(0f, 1f)] private float critChance = 0.1f;
    [SerializeField] private float critMultiplier = 2f;

    [Header("冲击力设置")]
    [SerializeField] private float impactForce = 5f;
    [SerializeField] private float impactDuration = 0.2f;

    [Header("特效设置")]
    [SerializeField] private bool hitEffectOnLastObject = true;       // 击中特效在最后一个物体
    [SerializeField] private bool damageEffectOnAllObjects = false;   // 伤害特效在所有物体

    [Header("调试设置")]
    [SerializeField] private bool showDebugRays = true;

    private float lastDamageTickTime = 0f;
    private GameObject lastHitEnemy = null;
    private float continuousHitTime = 0f;
    private List<GameObject> hitEnemies = new List<GameObject>();
    private List<RaycastHit> hitPoints = new List<RaycastHit>();
    // 用于记录每条主激光当前锁定的敌人
    
    void Start()
    {
        Laser = GetComponent<LineRenderer>();
        Effects = GetComponentsInChildren<ParticleSystem>();

        if (HitEffect != null)
        {
            Hit = HitEffect.GetComponentsInChildren<ParticleSystem>();
        }

        lastDamageTickTime = Time.time;
        UpdateLaserWidth();
    }
    public void SetIsChainLaser(bool value)
    {
        isChainLaser = value;
    }

    public bool IsChainLaser()
    {
        return isChainLaser;
    }
    void Update()
    {
        if (Laser != null && Laser.material != null)
        {
            Laser.material.SetTextureScale("_MainTex", new Vector2(Length[0], Length[1]));
            Laser.material.SetTextureScale("_Noise", new Vector2(Length[2], Length[3]));
        }

        if (Laser != null && !UpdateSaver)
        {
            hitEnemies.Clear();
            hitPoints.Clear();

            CollectAllHitPoints();

            if (hitPoints.Count > 0)
            {
                UpdateLaserPositions();
                UpdateHitEffects();      // ✅ 支持多命中特效
                PlayEffects();

                // 更新纹理长度，使用激光的实际长度
                if (hitPoints.Count > 0)
                {
                    Vector3 lastHitPoint = hitPoints[hitPoints.Count - 1].point;
                    float laserLength = Vector3.Distance(transform.position, lastHitPoint);
                    Length[0] = MainTextureLength * laserLength;
                    Length[2] = NoiseTextureLength * laserLength;
                }
                else
                {
                    Length[0] = MainTextureLength * MaxLength;
                    Length[2] = NoiseTextureLength * MaxLength;
                }

                HandleAllDamage();
            }
            else
            {
                Vector3 EndPos = transform.position + transform.forward * MaxLength;
                Laser.positionCount = 2;
                Laser.SetPosition(0, transform.position);
                Laser.SetPosition(1, EndPos);

                if (HitEffect != null)
                {
                    HitEffect.transform.position = EndPos;

                    if (Hit != null)
                    {
                        foreach (var AllPs in Hit) { if (AllPs.isPlaying) AllPs.Stop(); }
                    }
                }

                // 更新纹理长度，使用最大长度
                Length[0] = MainTextureLength * MaxLength;
                Length[2] = NoiseTextureLength * MaxLength;
                ResetHitState();
            }

            if (!Laser.enabled && !LaserSaver)
            {
                LaserSaver = true;
                Laser.enabled = true;
            }
        }

        // 绘制调试射线
        if (showDebugRays)
        {
            DrawDebugRays();
        }
    }

    // 收集所有命中点 (支持 Trigger)
    private void CollectAllHitPoints()
    {
        if (penetrateObjects)
        {
            RaycastHit[] hits = Physics.RaycastAll(
                transform.position,
                transform.forward,
                MaxLength,
                damageLayers,
                QueryTriggerInteraction.UseGlobal
            );

            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            int hitCount = Mathf.Min(hits.Length, maxPenetrations);
            //Debug.Log($"检测到 {hits.Length} 个物体，将处理 {hitCount} 个穿透");

            for (int i = 0; i < hits.Length; i++)
            {
                // Debug.Log($"命中 {i}: {hits[i].collider.name}, 距离: {hits[i].distance}, 图层: {LayerMask.LayerToName(hits[i].collider.gameObject.layer)}");
            }

            for (int i = 0; i < hitCount; i++)
            {
                hitPoints.Add(hits[i]);
            }
        }
        else
        {
            RaycastHit hit;
            if (Physics.Raycast(transform.position, transform.forward, out hit, MaxLength, damageLayers, QueryTriggerInteraction.Collide))
            {
                hitPoints.Add(hit);
            }
        }
    }

    // ✅ 激光长度调整：从起点到最后一个命中点
    private void UpdateLaserPositions()
    {
        if (hitPoints.Count > 0)
        {
            // 获取最后一个命中点
            Vector3 lastHitPoint = hitPoints[hitPoints.Count - 1].point;

            // 设置激光位置：从起点到最后一个命中点
            Laser.positionCount = 2;
            Laser.SetPosition(0, transform.position);
            Laser.SetPosition(1, lastHitPoint);
        }
    }

    // ✅ 击中特效始终在最后一个命中点
    private void UpdateHitEffects()
    {
        if (HitEffect != null && hitPoints.Count > 0)
        {
            // 无论damageEffectOnAllObjects如何设置，HitEffect始终在最后一个命中点
            RaycastHit lastHit = hitPoints[hitPoints.Count - 1];

            // 设置HitEffect到最后一个命中点
            HitEffect.transform.position = lastHit.point + lastHit.normal * HitOffset;

            if (useLaserRotation)
                HitEffect.transform.rotation = transform.rotation;
            else
                HitEffect.transform.LookAt(lastHit.point + lastHit.normal);

            // 播放HitEffect的粒子系统
            if (Hit != null)
            {
                foreach (var AllPs in Hit)
                    if (!AllPs.isPlaying) AllPs.Play();
            }
        }
    }

    // 计算暴击伤害
    private (float finalDamage, bool isCrit) CalculateCritDamage(float baseDamage)
    {
        float randomValue = Random.Range(0f, 1f);
        if (randomValue <= critChance)
        {
            return (baseDamage * critMultiplier, true);
        }
        return (baseDamage, false);
    }

    private void HandleAllDamage()
    {
        if (Time.time - lastDamageTickTime >= damageTickInterval)
        {
            float baseDamage = damagePerSecond * damageTickInterval;

            for (int i = 0; i < hitPoints.Count; i++)
            {
                RaycastHit hit = hitPoints[i];

                if (((1 << hit.collider.gameObject.layer) & damageLayers) == 0)
                    continue;

                float damageMultiplier = Mathf.Pow(penetrationDamageReduction, i);
                float damageThisTick = baseDamage * damageMultiplier;

                // 计算暴击
                var (finalDamage, isCrit) = CalculateCritDamage(damageThisTick);

                EnemyHealth enemyHealth = hit.collider.GetComponent<EnemyHealth>();

                if (enemyHealth != null)
                {
                    Vector3 laserDirection = transform.forward;
                    enemyHealth.TakeDamageFromProjectile(finalDamage, hit.point, hit.normal, transform.position, laserDirection);

                    Rigidbody enemyRb = hit.collider.GetComponent<Rigidbody>();
                    if (enemyRb != null && impactForce > 0)
                    {
                        float forceMultiplier = Mathf.Pow(penetrationDamageReduction, i);
                        enemyRb.AddForce(laserDirection * impactForce * forceMultiplier, ForceMode.Impulse);
                    }

                    if (damageEffect != null)
                    {
                        if (damageEffectOnAllObjects || (i == 0 && !damageEffectOnAllObjects))
                        {
                            Quaternion rotation = Quaternion.LookRotation(hit.normal);
                            Instantiate(damageEffect, hit.point, rotation);
                        }
                    }

                    if (!hitEnemies.Contains(hit.collider.gameObject))
                        hitEnemies.Add(hit.collider.gameObject);

                    lastHitEnemy = hit.collider.gameObject;
                }
            }

            continuousHitTime = hitEnemies.Count > 0 ? continuousHitTime + damageTickInterval : 0f;
            if (hitEnemies.Count == 0) lastHitEnemy = null;
            lastDamageTickTime = Time.time;
        }
    }

    private void PlayEffects()
    {
        if (Effects != null)
        {
            foreach (var AllPs in Effects)
            {
                if (!AllPs.isPlaying) AllPs.Play();
            }
        }
    }

    private void ResetHitState()
    {
        continuousHitTime = 0f;
        lastHitEnemy = null;
        hitEnemies.Clear();
    }

    private void UpdateLaserWidth()
    {
        if (Laser != null)
        {
            Laser.startWidth = laserWidth * startWidthMultiplier;
            Laser.endWidth = laserWidth * endWidthMultiplier;
        }
    }

    // 调试绘制
    private void DrawDebugRays()
    {
        // 绘制主射线
        Debug.DrawRay(transform.position, transform.forward * MaxLength, Color.red, 0.1f);

        // 绘制命中点
        for (int i = 0; i < hitPoints.Count; i++)
        {
            Color pointColor = (i == 0) ? Color.green : Color.yellow;
            Vector3 hitPoint = hitPoints[i].point;
            Debug.DrawRay(hitPoint - Vector3.right * 0.1f, Vector3.right * 0.2f, pointColor, 0.1f);
            Debug.DrawRay(hitPoint - Vector3.up * 0.1f, Vector3.up * 0.2f, pointColor, 0.1f);
            Debug.DrawRay(hitPoint, hitPoints[i].normal * 0.3f, Color.blue, 0.1f);
        }
    }

    // ---------- 公开方法 ----------
    public void SetLaserWidth(float newWidth)
    {
        laserWidth = newWidth;
        UpdateLaserWidth();
    }

    public void SetLaserDamage(float newDamage)
    {
        damagePerSecond = newDamage;
    }

    public void SetLaserRange(float newRange)
    {
        MaxLength = newRange;
    }

    public void SetPenetration(bool enable, int maxPenetrateCount = 5, float damageReduction = 0.8f)
    {
        penetrateObjects = enable;
        maxPenetrations = maxPenetrateCount;
        penetrationDamageReduction = Mathf.Clamp01(damageReduction);
    }

    public void SetDamageTickInterval(float interval)
    {
        damageTickInterval = Mathf.Max(0.01f, interval);
    }

    public void SetCritParams(float chance, float multiplier)
    {
        critChance = Mathf.Clamp01(chance);
        critMultiplier = Mathf.Max(1f, multiplier);
    }

    public List<GameObject> GetHitEnemies()
    {
        return new List<GameObject>(hitEnemies);
    }

    public List<RaycastHit> GetHitPoints()
    {
        return new List<RaycastHit>(hitPoints);
    }

    // 新添加的方法，供SecondaryWeapon调用
    public int GetPenetrationLimit()
    {
        return maxPenetrations;
    }

    public bool IsPenetrating()
    {
        return penetrateObjects;
    }

    public float GetCurrentMaxLength()
    {
        return MaxLength;
    }

    // 获取所有命中的敌人（包括非敌人对象）
    public List<RaycastHit> GetAllHits()
    {
        return new List<RaycastHit>(hitPoints);
    }

    // 获取所有命中的敌人（只返回敌人）
    public List<GameObject> GetHitEnemiesList()
    {
        List<GameObject> enemies = new List<GameObject>();
        foreach (var hit in hitPoints)
        {
            if (hit.collider.GetComponent<EnemyHealth>() != null)
            {
                enemies.Add(hit.collider.gameObject);
            }
        }
        return enemies;
    }

    public void DisablePrepare()
    {
        if (Laser != null) Laser.enabled = false;
        UpdateSaver = true;
        ResetHitState();
        hitPoints.Clear();

        if (Effects != null)
        {
            foreach (var AllPs in Effects) { if (AllPs.isPlaying) AllPs.Stop(); }
        }

        if (Hit != null)
        {
            foreach (var AllPs in Hit) { if (AllPs.isPlaying) AllPs.Stop(); }
        }
    }

    public float GetDamagePerSecond()
    {
        return damagePerSecond;
    }

    public float GetDamageTickInterval()
    {
        return damageTickInterval;
    }

    public GameObject GetLastHitEnemy()
    {
        return lastHitEnemy;
    }

    public float GetContinuousHitTime()
    {
        return continuousHitTime;
    }

    public float GetImpactForce()
    {
        return impactForce;
    }

    public float GetLaserWidth()
    {
        return laserWidth;
    }

    public float GetCritChance()
    {
        return critChance;
    }

    public float GetCritMultiplier()
    {
        return critMultiplier;
    }
}