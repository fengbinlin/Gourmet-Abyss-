using UnityEngine;
using System.Collections.Generic;

public class SecondaryWeapon : MonoBehaviour
{
    [Header("武器设置")]
    [SerializeField] private GameObject secondaryLaserPrefab;
    [SerializeField] private Transform secondaryFirePoint;
    [SerializeField] private float secondaryFireRate = 0.5f;
    [SerializeField] private float secondarySpreadAngle = 2f;
    [SerializeField] private float secondaryLaserRange = 30f;

    [Header("子弹效果调节参数")]
    [Tooltip("基础伤害值")]
    [SerializeField] private float damageValue = 20f;
    [Tooltip("伤害检测频率（秒）")]
    [SerializeField] private float damageTickInterval = 0.1f;
    [Tooltip("激光长度")]
    [SerializeField] private float laserLength = 30f;
    [Tooltip("同时发射的激光条数")]
    [SerializeField] private int laserCount = 1;
    [Tooltip("暴击率（0-1）")]
    [SerializeField][Range(0f, 1f)] private float critChance = 0.1f;
    [Tooltip("暴击伤害倍数")]
    [SerializeField] private float critMultiplier = 2f;
    [Tooltip("激光宽度")]
    [SerializeField] private float laserWidth = 1f;
    [Tooltip("最大连锁数量")]
    [SerializeField] private int maxChainCount = 3;
    [Tooltip("连锁搜索距离")]
    [SerializeField] private float chainSearchRadius = 10f;
    [Tooltip("多条激光之间的扇形角度")]
    [SerializeField] private float laserFanAngle = 15f;

    [Header("贯穿设置")]
    [SerializeField] private bool enablePenetration = true;
    [SerializeField] private int maxPenetrations = 3;
    [SerializeField] private float penetrationDamageReduction = 0.7f;

    [Header("连锁效果设置")]
    [Tooltip("是否启用连锁效果")]
    [Header("连锁激光高度调整")]
    [SerializeField] private float chainLaserHeightOffset = 1.0f;
    [SerializeField] private bool enableChainEffect = true;
    [Tooltip("连锁伤害衰减系数（每次连锁伤害乘以此值）")]
    [SerializeField] private float chainDamageMultiplier = 0.7f;
    [Tooltip("每个敌人最大延伸的连锁激光数")]
    [SerializeField] private int maxChainsPerEnemy = 3;
    [Tooltip("连锁激光预制体（可以与主激光不同）")]
    [SerializeField] private GameObject chainLaserPrefab;
    [Tooltip("敌人图层")]
    [SerializeField] private LayerMask enemyLayer;

    [Header("激光更新设置")]
    [Tooltip("激光位置更新频率（秒）")]
    [SerializeField] private float laserUpdateRate = 0.01f;

    [Header("武器模型")]
    [SerializeField] private Transform secondaryWeaponModel;
    [SerializeField] private float secondaryWeaponRecoilScale = 1.4f;
    [SerializeField] private float secondaryWeaponRestoreSpeed = 12f;

    [Header("射击手感优化")]
    [SerializeField] private float minMouseDistance = 2f;

    [Header("反馈效果")]
    [SerializeField] private float secondaryScreenShakeIntensity = 0.3f;
    [SerializeField] private float secondaryScreenShakeDuration = 0.15f;

    [Header("枪口上扬")]
    [SerializeField] private float secondaryMuzzleRiseAngle = 8f;
    [SerializeField] private float secondaryMuzzleRiseRecoverySpeed = 6f;

    [Header("枪口下垂设置")]
    [SerializeField] private float secondaryMuzzleDownAngle = -90f;
    [SerializeField] private float secondaryMuzzleDownSpeed = 8f;
    [SerializeField] private float secondaryMuzzleDownDelay = 0.2f;

    [Header("高级射击效果")]
    [SerializeField] private Vector3 secondaryWeaponRecoilKick = new Vector3(0, 0, -0.08f);
    [SerializeField] private float secondaryWeaponKickRecoverySpeed = 8f;
    [SerializeField] private Vector3 secondaryWeaponRecoilRotation = new Vector3(-15, 0, 0);

    [Header("粒子特效")]
    [SerializeField] private ParticleSystem secondaryMuzzleFlash;
    [SerializeField] private ParticleSystem secondaryBulletEjectEffect;
    [SerializeField] private ParticleSystem secondaryMuzzleSmoke;

    [Header("动画参数")]
    public string shootBoolName = "IsShootingSecondary";

    [Header("调试设置")]
    [SerializeField] private bool showDebugLines = true;
    [SerializeField] private Color debugLineColor = Color.cyan;
    [SerializeField] private float debugLineDuration = 0.1f;

    // 内部变量
    private Animator animator;
    private Camera mainCamera;
    private TopDownController controller;
    private CameraFollow cameraScript;

    private float nextSecondaryFireTime = 0f;
    private float lastSecondaryFireTime = 0f;
    private float nextLaserUpdateTime = 0f;
    private bool isShooting = false;

    // 武器状态
    private Vector3 secondaryWeaponOriginalScale;
    private Vector3 secondaryWeaponOriginalPosition;
    private Quaternion secondaryWeaponOriginalRotation;
    private Quaternion secondaryIdleWeaponRotation;
    private float currentSecondaryMuzzleRise = 0f;
    private Vector3 currentSecondaryWeaponKick = Vector3.zero;
    private Vector3 currentSecondaryWeaponRotation = Vector3.zero;
    private float currentSecondaryMuzzleDownAngle = 0f;

    // 激光相关
    private List<GameObject> currentLaserInstances = new List<GameObject>();
    private List<ChainLaserData> chainLasers = new List<ChainLaserData>();

    // 用于跟踪已连锁的敌人
    private List<GameObject> chainedEnemies = new List<GameObject>();

    // 连锁激光数据结构
    private Dictionary<GameObject, GameObject> laserCurrentTarget = new Dictionary<GameObject, GameObject>();
    private class ChainLaserData
    {
        public GameObject laserObject;   // 子激光实例
        public Transform startPoint;
        public Transform endPoint;
        public int chainLevel;
        public GameObject sourceEnemy;   // 来源敌人
        public GameObject parentLaser;   // 产生它的主激光

        public ChainLaserData(GameObject laser, Transform start, Transform end, int level, GameObject source, GameObject parent)
        {
            laserObject = laser;
            startPoint = start;
            endPoint = end;
            chainLevel = level;
            sourceEnemy = source;
            parentLaser = parent;
        }
    }
    private void Start()
    {
        // 从 WeaponStatsManager 获取数值
        damageTickInterval = WeaponStatsManager.Instance.secondaryFireRate;
        damageValue = WeaponStatsManager.Instance.secondaryDamageValue;
        laserLength = WeaponStatsManager.Instance.secondaryLaserLength;
        laserCount = WeaponStatsManager.Instance.secondaryLaserCount;
        laserWidth = WeaponStatsManager.Instance.secondaryLaserWidth;
        critChance = WeaponStatsManager.Instance.secondaryCritChance;
        critMultiplier = WeaponStatsManager.Instance.secondaryCritMultiplier;
        maxChainCount = WeaponStatsManager.Instance.secondaryMaxChainCount;
        chainSearchRadius = WeaponStatsManager.Instance.secondaryChainSearchRadius;
    }
    public void Initialize(Animator anim, Camera cam, TopDownController ctrl)
    {
        animator = anim;
        mainCamera = cam;
        controller = ctrl;

        if (mainCamera != null)
        {
            cameraScript = mainCamera.GetComponent<CameraFollow>();
        }

        if (secondaryWeaponModel != null)
        {
            secondaryWeaponOriginalScale = secondaryWeaponModel.localScale;
            secondaryWeaponOriginalPosition = secondaryWeaponModel.localPosition;
            secondaryWeaponOriginalRotation = secondaryWeaponModel.localRotation;
            secondaryIdleWeaponRotation = Quaternion.Euler(secondaryMuzzleDownAngle, 0, 0) * secondaryWeaponOriginalRotation;
        }

        if (secondaryFirePoint != null)
        {
            //Debug.Log($"副武器枪口位置: {secondaryFirePoint.position}");
        }
        else
        {
            //Debug.LogError("副武器枪口位置(secondaryFirePoint)未设置！");
        }

        if (chainLaserPrefab == null)
        {
            chainLaserPrefab = secondaryLaserPrefab;
        }
    }

    public void SetShooting(bool shooting)
    {
        bool wasShooting = isShooting;

        // 如果要开始射击，先检查弹药
        if (shooting)
        {
            // 检查弹药是否足够
            if (BattleValManager.Instance != null &&
                !BattleValManager.Instance.CheckConsumeSecondaryAmmo())  // 需要添加这个方法
            {
                Debug.Log("副武器弹药不足，无法开始射击！");
                isShooting = false;  // 确保状态为 false
                if (wasShooting)  // 如果之前是射击状态，停止射击
                {
                    StopShooting();
                }
                return;  // 直接返回，不设置射击状态
            }
        }

        isShooting = shooting;

        if (isShooting)
        {
            lastSecondaryFireTime = Time.time;

            if (!wasShooting)
            {
                chainedEnemies.Clear();
                CreateLasers();
            }
        }
        else
        {
            StopShooting();
        }
    }

    public void HandleShooting(Vector3 aimPoint, bool mouseActive)
    {
        if (!isShooting) return;

        if (Time.time >= nextSecondaryFireTime)
        {
            nextSecondaryFireTime = Time.time + secondaryFireRate;
            ShootSecondary(aimPoint, mouseActive);
        }
    }

    public void UpdateWeapon()
    {
        UpdateWeaponRecovery();
        UpdateMuzzleDownState();

        float timeSinceLastFire = Time.time - lastSecondaryFireTime;
        bool isSecondaryMuzzleDown = timeSinceLastFire > secondaryMuzzleDownDelay && !isShooting;

        if (!isSecondaryMuzzleDown && secondaryWeaponModel != null)
        {
            secondaryWeaponModel.localPosition = secondaryWeaponOriginalPosition + currentSecondaryWeaponKick;
            secondaryWeaponModel.localRotation = secondaryWeaponOriginalRotation * Quaternion.Euler(currentSecondaryWeaponRotation);
        }

        UpdateLaserContinuously();
        UpdateChainLasers();
    }

    public Transform GetFirePoint()
    {
        return secondaryFirePoint;
    }

    public void OnControllerDisabled()
    {
        StopShooting();
    }

    public void OnControllerDestroyed()
    {
        StopShooting();
    }

    #region --- 激光核心逻辑 ---

    private void UpdateLaserContinuously()
    {
        if (!isShooting || currentLaserInstances.Count == 0) return;

        if (Time.time >= nextLaserUpdateTime)
        {
            nextLaserUpdateTime = Time.time + laserUpdateRate;

            Vector3 aimPoint = controller.GetAimPoint();
            bool mouseActive = controller.IsMouseActive();

            UpdateLaserPositions(aimPoint, mouseActive);
        }
    }

    private void UpdateLaserPositions(Vector3 aimPoint, bool mouseActive)
    {
        if (currentLaserInstances.Count == 0 || secondaryFirePoint == null) return;

        Vector3 baseDirection = CalculateShootDirection(aimPoint, mouseActive);
        if (baseDirection == Vector3.zero)
        {
            baseDirection = controller.GetCharacterForward();
        }

        // 清空连锁记录，重新开始跟踪当前帧的连锁
        chainedEnemies.Clear();

        // 更新每条激光
        for (int i = 0; i < currentLaserInstances.Count; i++)
        {
            if (currentLaserInstances[i] == null) continue;

            float angleOffset = 0f;
            if (laserCount > 1)
            {
                float step = laserFanAngle / (laserCount - 1);
                angleOffset = -laserFanAngle / 2f + step * i;
            }

            Quaternion lookRotation = Quaternion.LookRotation(baseDirection);
            float spreadX = Random.Range(-secondarySpreadAngle, secondarySpreadAngle);
            float spreadY = Random.Range(-secondarySpreadAngle, secondarySpreadAngle) + angleOffset;
            Quaternion finalRotation = lookRotation * Quaternion.Euler(spreadX, spreadY, 0);

            Vector3 finalDirection = Quaternion.Euler(-currentSecondaryMuzzleRise, 0, 0) * finalRotation * Vector3.forward;

            currentLaserInstances[i].transform.position = secondaryFirePoint.position;
            currentLaserInstances[i].transform.rotation = Quaternion.LookRotation(finalDirection);

            Hovl_Laser laserComponent = currentLaserInstances[i].GetComponent<Hovl_Laser>();
            if (laserComponent != null)
            {
                // 设置激光参数
                laserComponent.SetLaserRange(laserLength);
                laserComponent.SetPenetration(enablePenetration, maxPenetrations, penetrationDamageReduction);

                // 设置新参数
                laserComponent.SetLaserDamage(damageValue);
                laserComponent.SetDamageTickInterval(damageTickInterval);
                laserComponent.SetLaserWidth(laserWidth);
                laserComponent.SetCritParams(critChance, critMultiplier);

                // 从激光组件获取所有命中点
                List<RaycastHit> allHits = laserComponent.GetAllHits();

                // 处理每个命中点
                foreach (var hit in allHits)
                {
                    if (IsEnemy(hit.collider.gameObject))
                    {
                        var laserComp = currentLaserInstances[i].GetComponent<Hovl_Laser>();

                        GameObject currentHitEnemy = hit.collider.gameObject;
                        GameObject lastTarget = null;
                        laserCurrentTarget.TryGetValue(currentLaserInstances[i], out lastTarget);

                        // ✅ 如果命中目标变化
                        if (currentHitEnemy != lastTarget)
                        {
                            // 1. 销毁旧的子激光
                            for (int c = chainLasers.Count - 1; c >= 0; c--)
                            {
                                if (chainLasers[c].parentLaser == currentLaserInstances[i])
                                {
                                    if (chainLasers[c].laserObject != null)
                                        Destroy(chainLasers[c].laserObject);
                                    chainLasers.RemoveAt(c);
                                }
                            }

                            // 2. 更新新的锁定目标
                            laserCurrentTarget[currentLaserInstances[i]] = currentHitEnemy;

                            // 3. 重建子激光
                            laserComp.hasSpawnedChain = true;
                            CreateChainLasers(currentHitEnemy, hit.point, 1, currentLaserInstances[i]);
                        }
                    }
                }
                bool hitEnemyThisFrame = false;

                foreach (var hit in allHits)
                {
                    if (IsEnemy(hit.collider.gameObject))
                    {
                        hitEnemyThisFrame = true;
                        break; // 找到敌人就退出
                    }
                }

                if (!hitEnemyThisFrame)
                {
                    // 没有敌人命中 -> 销毁该主激光的所有子激光
                    for (int c = chainLasers.Count - 1; c >= 0; c--)
                    {
                        if (chainLasers[c].parentLaser == currentLaserInstances[i])
                        {
                            if (chainLasers[c].laserObject != null)
                                Destroy(chainLasers[c].laserObject);
                            chainLasers.RemoveAt(c);
                        }
                    }

                    // 移除当前激光的锁定目标记录
                    if (laserCurrentTarget.ContainsKey(currentLaserInstances[i]))
                    {
                        laserCurrentTarget.Remove(currentLaserInstances[i]);
                    }

                    continue; // 本帧不用创建子激光
                }

                // 调试：显示激光路径
                if (showDebugLines)
                {
                    Debug.DrawRay(secondaryFirePoint.position, finalDirection * laserLength, debugLineColor, debugLineDuration);
                }
            }
        }
    }

    private void CreateLasers()
    {
        if (secondaryLaserPrefab == null || secondaryFirePoint == null) return;

        DestroyAllLasers();
        chainedEnemies.Clear();

        Vector3 aimPoint = controller.GetAimPoint();
        bool mouseActive = controller.IsMouseActive();
        Vector3 baseDirection = CalculateShootDirection(aimPoint, mouseActive);

        if (baseDirection == Vector3.zero)
        {
            baseDirection = controller.GetCharacterForward();
        }

        for (int i = 0; i < laserCount; i++)
        {
            float angleOffset = 0f;
            if (laserCount > 1)
            {
                float step = laserFanAngle / (laserCount - 1);
                angleOffset = -laserFanAngle / 2f + step * i;
            }

            Quaternion rotation = Quaternion.LookRotation(baseDirection) * Quaternion.Euler(0, angleOffset, 0);
            GameObject laserInstance = Instantiate(secondaryLaserPrefab, secondaryFirePoint.position, rotation);

            Hovl_Laser laserComponent = laserInstance.GetComponent<Hovl_Laser>();
            if (laserComponent != null)
            {
                laserComponent.SetIsChainLaser(false); // 主激光标志
                laserComponent.hasSpawnedChain = false; // 还未生成过子激光
                // 设置激光参数
                laserComponent.SetLaserWidth(laserWidth);
                laserComponent.SetLaserRange(laserLength);
                laserComponent.SetPenetration(enablePenetration, maxPenetrations, penetrationDamageReduction);

                // 设置新参数
                laserComponent.SetLaserDamage(damageValue);
                laserComponent.SetDamageTickInterval(damageTickInterval);
                laserComponent.SetCritParams(critChance, critMultiplier);

                // 如果是连锁激光预制体，可能需要不同的设置
                if (laserInstance != secondaryLaserPrefab)
                {
                    // 连锁激光可以有不同的宽度
                    float chainWidth = laserWidth * 0.7f;
                    laserComponent.SetLaserWidth(chainWidth);
                }
            }

            currentLaserInstances.Add(laserInstance);
        }

        UpdateLaserPositions(aimPoint, mouseActive);
    }

    // 修改后的连锁激光创建逻辑
    private void CreateChainLasers(GameObject hitEnemy, Vector3 hitPoint, int chainLevel, GameObject parentLaser)
    {
        if (!enableChainEffect || chainLevel > maxChainCount || chainedEnemies.Count >= maxChainCount * maxChainsPerEnemy)
            return;

        // 标记这个敌人已经连锁过
        if (!chainedEnemies.Contains(hitEnemy))
        {
            chainedEnemies.Add(hitEnemy);
        }

        // 查找附近的敌人
        Collider[] nearbyEnemies = Physics.OverlapSphere(hitPoint, chainSearchRadius, enemyLayer);
        List<GameObject> validTargets = new List<GameObject>();
        validTargets.Sort((a, b) =>
            Vector3.Distance(hitPoint, a.transform.position)
            .CompareTo(Vector3.Distance(hitPoint, b.transform.position))
        );
        foreach (Collider col in nearbyEnemies)
        {
            // 排除自身和已经连锁过的敌人
            if (col.gameObject != hitEnemy &&
                IsEnemy(col.gameObject) &&
                !chainedEnemies.Contains(col.gameObject))
            {
                validTargets.Add(col.gameObject);
            }
        }

        // 限制每个敌人最多延伸的激光数量
        int chainsToCreate = Mathf.Min(validTargets.Count, maxChainsPerEnemy);

        // 为每个附近的敌人创建连锁激光
        for (int i = 0; i < chainsToCreate; i++)
        {
            if (i >= maxChainsPerEnemy) break;

            GameObject target = validTargets[i];
            if (chainLaserPrefab == null) continue;

            Vector3 direction = (target.transform.position - hitPoint).normalized;
            Vector3 chainLaserStartPos = hitPoint + Vector3.up * chainLaserHeightOffset;
            GameObject chainLaser = Instantiate(chainLaserPrefab, chainLaserStartPos, Quaternion.LookRotation(direction));

            Hovl_Laser laserComponent = chainLaser.GetComponent<Hovl_Laser>();
            if (laserComponent != null)
            {
                laserComponent.SetIsChainLaser(true);
                // 连锁激光设置不同的宽度和伤害
                float chainWidth = laserWidth * Mathf.Pow(0.8f, chainLevel);
                laserComponent.SetLaserWidth(chainWidth);

                float distance = Vector3.Distance(hitPoint, target.transform.position);
                laserComponent.SetLaserRange(distance);

                // 连锁激光通常不需要穿透
                //laserComponent.SetPenetration(false, 1, 1.0f);

                // 设置连锁激光的暴击参数
                laserComponent.SetCritParams(critChance, critMultiplier);

                // 连锁激光伤害衰减
                float chainDamage = damageValue * Mathf.Pow(chainDamageMultiplier, chainLevel);
                laserComponent.SetLaserDamage(chainDamage);

                // 连锁激光的伤害检测频率与主激光相同
                laserComponent.SetDamageTickInterval(damageTickInterval);
            }

            ChainLaserData chainData = new ChainLaserData(chainLaser, null, target.transform, chainLevel, hitEnemy, parentLaser);
            chainLasers.Add(chainData);

            // 标记目标敌人已经连锁，防止被其他激光再次连锁
            if (!chainedEnemies.Contains(target))
            {
                chainedEnemies.Add(target);
            }

            // 不再递归创建下一级连锁，防止指数增长
        }
    }

    private void UpdateChainLasers()
    {
        for (int i = chainLasers.Count - 1; i >= 0; i--)
        {
            if (chainLasers[i].laserObject == null || chainLasers[i].endPoint == null)
            {
                if (chainLasers[i].laserObject != null)
                {
                    Destroy(chainLasers[i].laserObject);
                }
                chainLasers.RemoveAt(i);
                continue;
            }

            Transform startTrans = chainLasers[i].startPoint != null ? chainLasers[i].startPoint : chainLasers[i].laserObject.transform;
            Transform endTrans = chainLasers[i].endPoint;

            Vector3 direction = (endTrans.position - startTrans.position).normalized;
            chainLasers[i].laserObject.transform.rotation = Quaternion.LookRotation(direction);

            Hovl_Laser laserComp = chainLasers[i].laserObject.GetComponent<Hovl_Laser>();
            if (laserComp != null)
            {
                float distance = Vector3.Distance(startTrans.position, endTrans.position);
                laserComp.MaxLength = distance;
            }
        }
    }

    private bool IsEnemy(GameObject obj)
    {
        return obj.GetComponent<EnemyHealth>() != null;
    }

    private void DestroyAllLasers()
    {
        foreach (GameObject laser in currentLaserInstances)
        {
            if (laser != null)
            {
                // 找到属于这个主激光的子激光，一并销毁
                for (int i = chainLasers.Count - 1; i >= 0; i--)
                {
                    if (chainLasers[i].parentLaser == laser)
                    {
                        if (chainLasers[i].laserObject != null)
                            Destroy(chainLasers[i].laserObject);
                        chainLasers.RemoveAt(i);
                    }
                }
                Destroy(laser);
            }
        }
        currentLaserInstances.Clear();

        // 销毁剩余无父激光的子激光
        for (int i = chainLasers.Count - 1; i >= 0; i--)
        {
            if (chainLasers[i].laserObject != null)
                Destroy(chainLasers[i].laserObject);
        }
        chainLasers.Clear();
        chainedEnemies.Clear();
    }

    #endregion

    #region --- 射击逻辑 ---

    private void ShootSecondary(Vector3 aimPoint, bool mouseActive)
    {
        print("副武器开火！");
        if (BattleValManager.Instance != null)
        {
            if (!BattleValManager.Instance.TryConsumeSecondaryAmmo())
            {
                Debug.Log("副武器弹药不足！");
                StopShooting();
                return;
            }
        }
        if (secondaryFirePoint == null) return;
        if (!controller.GetCombatState()) return;



        currentSecondaryMuzzleDownAngle = 0f;

        if (secondaryWeaponModel != null)
        {
            secondaryWeaponModel.localRotation = secondaryWeaponOriginalRotation;
        }

        PlayShootingEffects();

        if (cameraScript != null)
        {
            cameraScript.Shake(secondaryScreenShakeDuration, secondaryScreenShakeIntensity);
        }

        if (secondaryWeaponModel != null)
        {
            secondaryWeaponModel.localScale = secondaryWeaponOriginalScale * secondaryWeaponRecoilScale;
        }

        currentSecondaryMuzzleRise += secondaryMuzzleRiseAngle;

        if (secondaryWeaponModel != null)
        {
            currentSecondaryWeaponKick = secondaryWeaponRecoilKick;
            currentSecondaryWeaponRotation = secondaryWeaponRecoilRotation;
        }

        UpdateLaserPositions(aimPoint, mouseActive);
    }

    private Vector3 CalculateShootDirection(Vector3 aimPoint, bool mouseActive)
    {
        if (secondaryFirePoint == null)
        {
            return controller.GetCharacterForward();
        }

        if (!mouseActive)
        {
            return controller.GetCharacterForward();
        }

        Vector3 firePointToAim = aimPoint - secondaryFirePoint.position;

        if (firePointToAim.sqrMagnitude < 0.001f)
        {
            return controller.GetCharacterForward();
        }

        Vector3 firePointPos = secondaryFirePoint.position;
        Vector3 aimPos = aimPoint;
        firePointPos.y = aimPos.y;
        float horizontalDistance = Vector3.Distance(firePointPos, aimPos);

        if (horizontalDistance < minMouseDistance)
        {
            return controller.GetCharacterForward();
        }
        else
        {
            return controller.GetCharacterForward();
        }
    }

    private void PlayShootingEffects()
    {
        if (secondaryMuzzleFlash != null) secondaryMuzzleFlash.Play();
        if (secondaryMuzzleSmoke != null) secondaryMuzzleSmoke.Play();
        if (secondaryBulletEjectEffect != null) secondaryBulletEjectEffect.Play();
    }

    public void StopShooting()
    {
        isShooting = false;
        DestroyAllLasers();
    }

    #endregion

    #region --- 武器状态恢复 ---

    private void UpdateMuzzleDownState()
    {
        if (secondaryWeaponModel == null) return;

        float timeSinceLastFire = Time.time - lastSecondaryFireTime;
        bool shouldMuzzleDown = timeSinceLastFire > secondaryMuzzleDownDelay && !isShooting;

        if (shouldMuzzleDown)
        {
            currentSecondaryMuzzleDownAngle = Mathf.Lerp(currentSecondaryMuzzleDownAngle, 1f, Time.deltaTime * secondaryMuzzleDownSpeed);
            Quaternion targetRotation = Quaternion.Slerp(
                secondaryWeaponOriginalRotation,
                secondaryIdleWeaponRotation,
                currentSecondaryMuzzleDownAngle
            );
            secondaryWeaponModel.localRotation = targetRotation;
        }
        else
        {
            currentSecondaryMuzzleDownAngle = Mathf.Lerp(currentSecondaryMuzzleDownAngle, 0f, Time.deltaTime * secondaryMuzzleDownSpeed * 2f);
        }
    }

    private void UpdateWeaponRecovery()
    {
        if (secondaryWeaponModel == null) return;

        float timeSinceLastFire = Time.time - lastSecondaryFireTime;
        bool shouldRecover = timeSinceLastFire < secondaryMuzzleDownDelay || isShooting;

        if (shouldRecover)
        {
            secondaryWeaponModel.localScale = Vector3.Lerp(
                secondaryWeaponModel.localScale,
                secondaryWeaponOriginalScale,
                Time.deltaTime * secondaryWeaponRestoreSpeed
            );
            secondaryWeaponModel.localPosition = Vector3.Lerp(
                secondaryWeaponModel.localPosition,
                secondaryWeaponOriginalPosition,
                Time.deltaTime * secondaryWeaponKickRecoverySpeed
            );
            secondaryWeaponModel.localRotation = Quaternion.Slerp(
                secondaryWeaponModel.localRotation,
                secondaryWeaponOriginalRotation,
                Time.deltaTime * secondaryWeaponKickRecoverySpeed
            );
        }

        currentSecondaryMuzzleRise = Mathf.Lerp(currentSecondaryMuzzleRise, 0f, Time.deltaTime * secondaryMuzzleRiseRecoverySpeed);
        currentSecondaryWeaponKick = Vector3.Lerp(currentSecondaryWeaponKick, Vector3.zero, Time.deltaTime * secondaryWeaponKickRecoverySpeed);
        currentSecondaryWeaponRotation = Vector3.Lerp(currentSecondaryWeaponRotation, Vector3.zero, Time.deltaTime * secondaryWeaponKickRecoverySpeed);
    }

    public void ResetMuzzle()
    {
        currentSecondaryMuzzleDownAngle = 0f;
        if (secondaryWeaponModel != null)
        {
            secondaryWeaponModel.localRotation = secondaryWeaponOriginalRotation;
        }
    }

    #endregion

    #region --- 公开方法 ---

    public bool IsShooting()
    {
        return isShooting;
    }

    public List<GameObject> GetLaserInstances()
    {
        return currentLaserInstances;
    }

    public void SetDamageValue(float damage)
    {
        damageValue = damage;
    }

    public void SetDamageTickInterval(float interval)
    {
        damageTickInterval = Mathf.Max(0.01f, interval);
    }

    public void SetLaserLength(float length)
    {
        laserLength = length;
    }

    public void SetLaserCount(int count)
    {
        laserCount = Mathf.Max(1, count);
    }

    public void SetCritChance(float chance)
    {
        critChance = Mathf.Clamp01(chance);
    }

    public void SetCritMultiplier(float multiplier)
    {
        critMultiplier = Mathf.Max(1f, multiplier);
    }

    public void SetLaserWidth(float width)
    {
        laserWidth = width;
    }

    public void SetMaxChainCount(int count)
    {
        maxChainCount = Mathf.Max(0, count);
    }

    public void SetChainSearchRadius(float radius)
    {
        chainSearchRadius = Mathf.Max(0.1f, radius);
    }

    public void SetLaserRange(float range)
    {
        laserLength = range; // 为了向后兼容
    }

    public void SetPenetration(bool enable, int maxPenetrateCount = 3, float damageReduction = 0.7f)
    {
        enablePenetration = enable;
        maxPenetrations = maxPenetrateCount;
        penetrationDamageReduction = damageReduction;
    }

    public void SetLaserFanAngle(float angle)
    {
        laserFanAngle = angle;
    }

    #endregion
}