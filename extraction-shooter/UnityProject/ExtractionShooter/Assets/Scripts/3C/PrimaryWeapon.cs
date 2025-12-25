using UnityEngine;
using System.Collections.Generic;

public class PrimaryWeapon : MonoBehaviour
{
    [Header("武器设置")]
    [SerializeField] private GameObject bulletPrefab; // 子弹预制体
    [SerializeField] private Transform firePoint;     // 枪口位置
    [SerializeField] private float fireRate = 0.2f;   // 射击间隔

    [Header("子弹散射设置")]
    [SerializeField] private float spreadAngle = 3f;  // 子弹随机散射角度
    [SerializeField] private int pelletCount = 1;     // 散射个数（霰弹效果）

    [Header("霰弹固定角度序列")]
    [SerializeField] private float pelletAngleStep = 5f; // 每个子弹的角度步长
    [SerializeField] private float pelletStartAngle = 0f; // 起始角度
    [SerializeField] private float pelletEndAngle = 20f; // 结束角度

    [Header("子弹基础属性")]
    [SerializeField] private float baseDamage = 10f;  // 基础伤害
    [SerializeField] private float bulletSpeed = 20f; // 子弹速度
    [SerializeField] private float bulletSize = 1f;   // 子弹大小

    [Header("子弹属性调节")]
    [Range(0.1f, 5f)]
    [SerializeField] private float bulletSizeMultiplier = 1f; // 子弹大小倍率
    [Range(0.1f, 10f)]
    [SerializeField] private float damageMultiplier = 1f; // 伤害倍率
    [Range(0.1f, 10f)]
    [SerializeField] private float speedMultiplier = 1f; // 速度倍率

    [Header("子弹高级属性")]
    [SerializeField] private int penetrationCount = 0; // 穿透次数
    [SerializeField] private float maxTravelDistance = 100f; // 最大飞行距离
    [Range(0f, 1f)]
    [SerializeField] private float criticalChance = 0.1f; // 暴击率
    [SerializeField] private float criticalMultiplier = 2f; // 暴击伤害倍率

    [Header("射击方向")]
    [Tooltip("是否使用角色Z轴正方向发射")]
    [SerializeField] private bool useCharacterForward = true; // 新增：切换发射方向模式
    [Tooltip("鼠标距离阈值（当使用鼠标瞄准时生效）")]
    [SerializeField] private float minMouseDistance = 2f; // 鼠标距离枪口的水平距离小于此值时使用角色方向

    [Header("武器模型")]
    [Tooltip("主武器模型对象")]
    [SerializeField] private Transform weaponModel;
    [Tooltip("射击时枪变大的倍数")]
    [SerializeField] private float weaponRecoilScale = 1.3f;
    [Tooltip("恢复原大小的速度")]
    [SerializeField] private float weaponRestoreSpeed = 15f;

    [Header("反馈效果")]
    [Tooltip("射击时屏幕震动强度")]
    [SerializeField] private float screenShakeIntensity = 0.2f;
    [Tooltip("射击时屏幕震动持续时间")]
    [SerializeField] private float screenShakeDuration = 0.1f;

    [Header("枪口上扬")]
    [SerializeField] private float muzzleRiseAngle = 5f;
    [SerializeField] private float muzzleRiseRecoverySpeed = 8f;

    [Header("枪口下垂设置")]
    [SerializeField] private float muzzleDownAngle = -90f;
    [SerializeField] private float muzzleDownSpeed = 8f;
    [SerializeField] private float muzzleDownDelay = 0.2f;

    [Header("高级射击效果")]
    [SerializeField] private Vector3 weaponRecoilKick = new Vector3(0, 0, -0.05f);
    [SerializeField] private float weaponKickRecoverySpeed = 10f;
    [SerializeField] private Vector3 weaponRecoilRotation = new Vector3(-10, 0, 0);

    [Header("粒子特效")]
    [SerializeField] private ParticleSystem muzzleFlash;  // 枪口火焰
    [SerializeField] private ParticleSystem bulletEjectEffect; // 子弹发射粒子
    [SerializeField] private ParticleSystem muzzleSmoke; // 枪口烟雾

    [Header("动画参数")]
    public string shootBoolName = "IsShooting";

    [Header("霰弹模式")]
    [SerializeField] private ShotgunMode shotgunMode = ShotgunMode.FixedAngles;

    [Header("伤害衰减设置")]
    [Tooltip("是否启用弹丸伤害衰减")]
    [SerializeField] private bool useDamageFalloff = false;
    [SerializeField] private AnimationCurve damageFalloffCurve = AnimationCurve.Linear(0f, 1f, 1f, 0.5f);
    [SerializeField] private float maxFalloffDistance = 50f;

    [Header("水平射击设置")]
    [Tooltip("是否强制在Y轴上无分量（始终水平射击）")]
    [SerializeField] private bool forceHorizontalShot = true; // 新增：控制是否强制水平射击
    [Tooltip("水平射击的高度偏移")]
    [SerializeField] private float horizontalShotHeight = 0f; // 新增：水平射击的高度

    [Header("调试设置")]
    [SerializeField] private bool debugMode = false;
    [SerializeField] private float debugRayDuration = 2f;

    // 内部变量
    private Animator animator;
    private Camera mainCamera;
    private TopDownController controller;
    private CameraFollow cameraScript;

    private float nextFireTime = 0f;
    private float lastFireTime = 0f;
    private bool isShooting = false;

    // 武器状态
    private Vector3 weaponOriginalScale;
    private Vector3 weaponOriginalPosition;
    private Quaternion weaponOriginalRotation;
    private Quaternion idleWeaponRotation;
    private float currentMuzzleRise = 0f;
    private Vector3 currentWeaponKick = Vector3.zero;
    private Vector3 currentWeaponRotation = Vector3.zero;
    private float currentMuzzleDownAngle = 0f;

    // 子弹池
    private List<GameObject> bulletPool = new List<GameObject>();
    private int poolSize = 20;
    private Transform bulletPoolParent; // 用于组织子弹对象的父对象

    // 霰弹模式枚举
    public enum ShotgunMode
    {
        FixedAngles,    // 固定角度序列
        RandomSpread,   // 随机散射
        UniformSpread   // 均匀散射
    }
    private void Start()
    {
        // 从 WeaponStatsManager 获取数值
        fireRate = WeaponStatsManager.Instance.primaryFireRate;
        pelletCount = WeaponStatsManager.Instance.primaryPelletCount;
        penetrationCount = WeaponStatsManager.Instance.primaryPenetrationCount;
        bulletSpeed = WeaponStatsManager.Instance.primaryBulletSpeed;
        bulletSize = WeaponStatsManager.Instance.primaryBulletSize;
        print("PrimaryWeapon Start获取BaseDamage");
        print(bulletSize);
        baseDamage = WeaponStatsManager.Instance.primaryBaseDamage;
        criticalChance = WeaponStatsManager.Instance.primaryCriticalChance;
        criticalMultiplier = WeaponStatsManager.Instance.primaryCriticalMultiplier;
        maxTravelDistance = WeaponStatsManager.Instance.primaryMaxTravelDistance;
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

        // 记录武器的原始状态
        if (weaponModel != null)
        {
            weaponOriginalScale = weaponModel.localScale;
            weaponOriginalPosition = weaponModel.localPosition;
            weaponOriginalRotation = weaponModel.localRotation;
            idleWeaponRotation = Quaternion.Euler(muzzleDownAngle, 0, 0) * weaponOriginalRotation;
        }

        // 创建子弹池父对象
        CreateBulletPoolParent();

        // 初始化子弹池
        InitializeBulletPool();
    }

    public void SetShooting(bool shooting)
    {
        isShooting = shooting;

        if (animator != null && !string.IsNullOrEmpty(shootBoolName))
        {
            animator.SetBool(shootBoolName, shooting);
        }

        if (shooting)
        {
            lastFireTime = Time.time;
        }
    }

    public void HandleShooting(Vector3 aimPoint, bool mouseActive)
    {
        if (!isShooting) return;

        // 射击间隔检查
        if (Time.time < nextFireTime) return;

        nextFireTime = Time.time + (fireRate / fireRateMultiplier);
        Shoot(aimPoint, mouseActive);
    }

    public void UpdateWeapon()
    {
        UpdateWeaponRecovery();
        UpdateMuzzleDownState();

        // 应用后坐力效果
        float timeSinceLastFire = Time.time - lastFireTime;
        bool isPrimaryMuzzleDown = timeSinceLastFire > muzzleDownDelay && !isShooting;

        if (!isPrimaryMuzzleDown && weaponModel != null)
        {
            weaponModel.localPosition = weaponOriginalPosition + currentWeaponKick;
            weaponModel.localRotation = weaponOriginalRotation * Quaternion.Euler(currentWeaponRotation);
        }
    }

    public Transform GetFirePoint()
    {
        return firePoint;
    }

    #region --- 武器属性控制方法 ---

    // 射速控制
    [Header("属性调节")]
    [Range(0.1f, 10f)]
    [SerializeField] private float fireRateMultiplier = 1f; // 射速倍率

    public void SetDamageMultiplier(float multiplier)
    {
        damageMultiplier = Mathf.Max(0f, multiplier);
    }

    public void SetBulletSizeMultiplier(float multiplier)
    {
        bulletSizeMultiplier = Mathf.Clamp(multiplier, 0.1f, 5f);
    }

    public void SetBulletSize(float size)
    {
        bulletSize = Mathf.Max(0.1f, size);
    }

    public void SetBulletSpeed(float speed)
    {
        bulletSpeed = Mathf.Max(0.1f, speed);
    }

    public void SetSpeedMultiplier(float multiplier)
    {
        speedMultiplier = Mathf.Clamp(multiplier, 0.1f, 10f);
    }

    public void SetFireRateMultiplier(float multiplier)
    {
        fireRateMultiplier = Mathf.Clamp(multiplier, 0.1f, 10f);
    }

    public void SetPelletCount(int count)
    {
        pelletCount = Mathf.Max(1, count);
    }

    public void SetSpreadAngle(float angle)
    {
        spreadAngle = Mathf.Clamp(angle, 0f, 90f);
    }

    public void SetPenetrationCount(int count)
    {
        penetrationCount = Mathf.Max(0, count);
    }

    public void SetMaxTravelDistance(float distance)
    {
        maxTravelDistance = Mathf.Max(0.1f, distance);
    }

    public void SetCriticalChance(float chance)
    {
        criticalChance = Mathf.Clamp01(chance);
    }

    public void SetCriticalMultiplier(float multiplier)
    {
        criticalMultiplier = Mathf.Max(1f, multiplier);
    }

    public void SetBaseDamage(float damage)
    {
        baseDamage = Mathf.Max(0f, damage);
    }

    public void SetShotgunMode(ShotgunMode mode)
    {
        shotgunMode = mode;
    }

    public void SetUseDamageFalloff(bool use)
    {
        useDamageFalloff = use;
    }

    public void SetDamageFalloffCurve(AnimationCurve curve)
    {
        damageFalloffCurve = curve;
    }

    public void SetMaxFalloffDistance(float distance)
    {
        maxFalloffDistance = distance;
    }

    // 获取属性值
    public float GetCurrentDamage()
    {
        return baseDamage * damageMultiplier;
    }

    public float GetCurrentBulletSize()
    {
        return bulletSize * bulletSizeMultiplier;
    }

    public float GetCurrentBulletSpeed()
    {
        return bulletSpeed * speedMultiplier;
    }

    public float GetCurrentFireRate()
    {
        return fireRate * fireRateMultiplier;
    }

    public int GetCurrentPelletCount()
    {
        return pelletCount;
    }

    public int GetCurrentPenetrationCount()
    {
        return penetrationCount;
    }

    public float GetCurrentMaxTravelDistance()
    {
        return maxTravelDistance;
    }

    public float GetCurrentCriticalChance()
    {
        return criticalChance;
    }

    public float GetCurrentCriticalMultiplier()
    {
        return criticalMultiplier;
    }

    public float GetBaseFireRate()
    {
        return fireRate;
    }

    public float GetBaseDamage()
    {
        return baseDamage;
    }

    public float GetBaseBulletSpeed()
    {
        return bulletSpeed;
    }

    #endregion

    #region --- 射击逻辑 ---

    private void Shoot(Vector3 aimPoint, bool mouseActive)
    {
        if (bulletPrefab == null || firePoint == null)
        {
            Debug.LogError("子弹预制体或开火点为空！");
            return;
        }

        // 非战斗状态下不允许射击
        if (!controller.GetCombatState())
        {
            if (debugMode) Debug.Log("非战斗状态，不能射击");
            return;
        }

        // 检查弹药
        if (BattleValManager.Instance != null)
        {
            if (!BattleValManager.Instance.TryConsumePrimaryAmmo())
            {
                if (debugMode) Debug.Log("主武器弹药不足！");
                return;
            }
        }

        // 重置枪口下垂计时器
        currentMuzzleDownAngle = 0f;

        // 立即将武器旋转恢复到射击状态
        if (weaponModel != null)
        {
            weaponModel.localRotation = weaponOriginalRotation;
        }

        // 播放特效
        PlayShootingEffects();

        // 触发屏幕震动
        if (cameraScript != null)
        {
            cameraScript.Shake(screenShakeDuration, screenShakeIntensity);
        }

        // 武器瞬间膨胀
        if (weaponModel != null)
        {
            weaponModel.localScale = weaponOriginalScale * weaponRecoilScale;
        }

        // 应用枪口上扬效果
        currentMuzzleRise += muzzleRiseAngle;

        // 应用后坐力
        if (weaponModel != null)
        {
            currentWeaponKick = weaponRecoilKick;
            currentWeaponRotation = weaponRecoilRotation;
        }

        // 计算射击方向
        Vector3 baseDirection = Vector3.zero;

        if (useCharacterForward)
        {
            // 模式1：使用角色Z轴正方向发射
            baseDirection = controller.GetCharacterForward();
        }
        else
        {
            // 模式2：使用原有的鼠标瞄准逻辑
            baseDirection = CalculateShootDirection(aimPoint, mouseActive);

            if (baseDirection == Vector3.zero)
            {
                baseDirection = controller.GetCharacterForward();
            }
        }

        // 如果强制水平射击，将Y分量设为0
        if (forceHorizontalShot)
        {
            // 将方向向量设为水平（Y轴分量为0）
            baseDirection.y = 0f;

            // 如果方向向量变成0向量，则使用角色前方
            if (baseDirection.sqrMagnitude < 0.001f)
            {
                baseDirection = controller.GetCharacterForward();
                baseDirection.y = 0f;
            }
            else
            {
                baseDirection.Normalize();
            }
        }

        if (debugMode)
        {
            Debug.DrawRay(firePoint.position, baseDirection * 10f, Color.green, debugRayDuration);
            Debug.Log($"射击方向: {baseDirection}");
        }

        // 计算散射
        Quaternion lookRotation = Quaternion.LookRotation(baseDirection);

        // 根据弹丸数量生成子弹
        if (pelletCount == 1)
        {
            // 单发子弹模式
            FireSingleBullet(lookRotation, baseDirection);
        }
        else
        {
            // 霰弹模式
            FireShotgunSpread(lookRotation, baseDirection);
        }
    }

    private void FireSingleBullet(Quaternion lookRotation, Vector3 baseDirection)
    {
        // 计算单个子弹的散射
        float spreadX = Random.Range(-spreadAngle, spreadAngle);
        float spreadY = Random.Range(-spreadAngle, spreadAngle);

        // 如果强制水平射击，只在水平方向散射
        if (forceHorizontalShot)
        {
            spreadY = 0f; // 垂直方向散射为0
        }

        Quaternion finalRotation = lookRotation * Quaternion.Euler(spreadX, spreadY, 0);
        Vector3 finalDirection = finalRotation * Vector3.forward;

        // 如果强制水平射击，确保最终方向Y分量为0
        if (forceHorizontalShot)
        {
            finalDirection.y = 0f;

            if (finalDirection.sqrMagnitude < 0.001f)
            {
                finalDirection = baseDirection;
            }
            else
            {
                finalDirection.Normalize();
            }

            // 重新计算旋转
            finalRotation = Quaternion.LookRotation(finalDirection);
        }
        else
        {
            // 原始逻辑：应用枪口上扬效果
            finalDirection = Quaternion.Euler(-currentMuzzleRise, 0, 0) * finalDirection;
        }

        if (debugMode)
        {
            Debug.DrawRay(firePoint.position, baseDirection * 5f, Color.yellow, debugRayDuration);
            Debug.DrawRay(firePoint.position, finalDirection * 5f, Color.red, debugRayDuration);
        }

        // 生成子弹
        SpawnBullet(firePoint.position, finalRotation, finalDirection);
    }

    private void FireShotgunSpread(Quaternion lookRotation, Vector3 baseDirection)
    {
        if (debugMode) Debug.Log($"发射霰弹，弹丸数量: {pelletCount}, 模式: {shotgunMode}");

        // 生成一个随机散射角度，用于所有子弹
        float sharedSpreadX = Random.Range(-spreadAngle, spreadAngle);
        float sharedSpreadY = forceHorizontalShot ? 0f : Random.Range(-spreadAngle, spreadAngle);
        Quaternion sharedSpreadRotation = Quaternion.Euler(sharedSpreadX, sharedSpreadY, 0);

        // 根据不同的霰弹模式生成子弹
        switch (shotgunMode)
        {
            case ShotgunMode.FixedAngles:
                // 固定角度序列模式
                FireFixedAngleSpread(lookRotation, baseDirection, sharedSpreadRotation);
                break;

            case ShotgunMode.RandomSpread:
                // 随机散射模式（原逻辑，每个子弹独立随机）
                FireRandomSpread(lookRotation, baseDirection);
                break;

            case ShotgunMode.UniformSpread:
                // 均匀散射模式
                FireUniformSpread(lookRotation, baseDirection, sharedSpreadRotation);
                break;
        }
    }

    private void FireFixedAngleSpread(Quaternion lookRotation, Vector3 baseDirection, Quaternion sharedSpreadRotation)
    {
        // 计算角度序列
        float angleStep = 0f;
        if (pelletCount > 1)
        {
            angleStep = (pelletEndAngle - pelletStartAngle) / Mathf.Max(1, pelletCount - 1);
        }

        // 发射所有子弹
        for (int i = 0; i < pelletCount; i++)
        {
            // 计算当前子弹的固定角度偏移
            float currentAngle = pelletStartAngle + (i * angleStep);

            // 创建固定角度旋转（在xz平面上偏转）
            Quaternion fixedAngleRotation = Quaternion.Euler(0, currentAngle, 0);

            // 组合旋转：先应用固定角度，再应用共享的随机散射
            Quaternion finalRotation = lookRotation * fixedAngleRotation * sharedSpreadRotation;
            Vector3 finalDirection = finalRotation * Vector3.forward;

            // 如果强制水平射击，确保最终方向Y分量为0
            if (forceHorizontalShot)
            {
                finalDirection.y = 0f;

                if (finalDirection.sqrMagnitude < 0.001f)
                {
                    finalDirection = baseDirection;
                }
                else
                {
                    finalDirection.Normalize();
                }

                // 重新计算旋转
                finalRotation = Quaternion.LookRotation(finalDirection);
            }
            else
            {
                // 原始逻辑：应用枪口上扬效果
                finalDirection = Quaternion.Euler(-currentMuzzleRise, 0, 0) * finalDirection;
            }

            // 生成子弹
            SpawnBullet(firePoint.position, finalRotation, finalDirection);
        }
    }

    private void FireRandomSpread(Quaternion lookRotation, Vector3 baseDirection)
    {
        // 原逻辑：每个子弹独立随机散射
        for (int i = 0; i < pelletCount; i++)
        {
            // 计算每个子弹的独立随机散射
            float spreadX = Random.Range(-spreadAngle, spreadAngle);
            float spreadY = forceHorizontalShot ? 0f : Random.Range(-spreadAngle, spreadAngle);
            Quaternion pelletRotation = lookRotation * Quaternion.Euler(spreadX, spreadY, 0);
            Vector3 pelletDirection = pelletRotation * Vector3.forward;

            // 如果强制水平射击，确保最终方向Y分量为0
            if (forceHorizontalShot)
            {
                pelletDirection.y = 0f;

                if (pelletDirection.sqrMagnitude < 0.001f)
                {
                    pelletDirection = baseDirection;
                }
                else
                {
                    pelletDirection.Normalize();
                }

                // 重新计算旋转
                pelletRotation = Quaternion.LookRotation(pelletDirection);
            }
            else
            {
                // 原始逻辑：应用枪口上扬效果
                pelletDirection = Quaternion.Euler(-currentMuzzleRise, 0, 0) * pelletDirection;
            }

            // 生成子弹
            SpawnBullet(firePoint.position, Quaternion.LookRotation(pelletDirection), pelletDirection);
        }
    }

    private void FireUniformSpread(Quaternion lookRotation, Vector3 baseDirection, Quaternion sharedSpreadRotation)
    {
        // 均匀散射模式
        for (int i = 0; i < pelletCount; i++)
        {
            Vector3 pelletDirection = baseDirection;

            // 计算均匀分布的角度
            float angleStep = 360f / pelletCount;
            float currentAngle = i * angleStep;

            // 在垂直于射击方向的平面上计算偏移
            Vector3 right = Vector3.Cross(pelletDirection, Vector3.up).normalized;
            Vector3 up = Vector3.Cross(right, pelletDirection).normalized;

            // 如果强制水平射击，只在水平面上计算偏移
            if (forceHorizontalShot)
            {
                up = Vector3.up; // 强制使用垂直向上的向量
            }

            // 计算圆形分布的偏移
            float offsetX = Mathf.Sin(currentAngle * Mathf.Deg2Rad) * spreadAngle * 0.1f;
            float offsetY = Mathf.Cos(currentAngle * Mathf.Deg2Rad) * spreadAngle * 0.1f;

            pelletDirection += right * offsetX + up * offsetY;
            pelletDirection = pelletDirection.normalized;

            // 应用共享的随机散射
            Quaternion finalRotation = Quaternion.LookRotation(pelletDirection) * sharedSpreadRotation;
            pelletDirection = finalRotation * Vector3.forward;

            // 如果强制水平射击，确保最终方向Y分量为0
            if (forceHorizontalShot)
            {
                pelletDirection.y = 0f;

                if (pelletDirection.sqrMagnitude < 0.001f)
                {
                    pelletDirection = baseDirection;
                }
                else
                {
                    pelletDirection.Normalize();
                }

                // 重新计算旋转
                finalRotation = Quaternion.LookRotation(pelletDirection);
            }
            else
            {
                // 原始逻辑：应用枪口上扬效果
                pelletDirection = Quaternion.Euler(-currentMuzzleRise, 0, 0) * pelletDirection;
            }

            // 生成子弹
            SpawnBullet(firePoint.position, Quaternion.LookRotation(pelletDirection), pelletDirection);
        }
    }

    private void SpawnBullet(Vector3 position, Quaternion rotation, Vector3 direction)
    {
        GameObject bulletObj = GetBulletFromPool();

        if (bulletObj == null)
        {
            Debug.LogError("无法从对象池获取子弹！");
            return;
        }

        // 设置位置和旋转
        bulletObj.transform.position = position;
        bulletObj.transform.rotation = rotation;

        // 如果设置了水平射击高度，调整子弹高度
        if (forceHorizontalShot && Mathf.Abs(horizontalShotHeight) > 0.001f)
        {
            Vector3 bulletPos = bulletObj.transform.position;
            bulletPos.y = horizontalShotHeight;
            bulletObj.transform.position = bulletPos;
        }

        // 确保子弹是激活状态
        bulletObj.SetActive(true);

        // 获取Projectile组件
        Projectile projectile = bulletObj.GetComponent<Projectile>();
        if (projectile != null)
        {
            // 计算所有子弹属性
            float finalDamage = baseDamage * damageMultiplier;
            float finalSize = bulletSize * bulletSizeMultiplier;
            float finalSpeed = bulletSpeed * speedMultiplier;

            // 如果强制水平射击，确保子弹方向是水平的
            if (forceHorizontalShot)
            {
                direction.y = 0f;
                direction.Normalize();
            }

            // 初始化子弹
            projectile.Initialize(
                direction,
                finalSpeed,
                finalDamage,
                finalSize,
                penetrationCount,
                maxTravelDistance,
                criticalChance,
                criticalMultiplier
            );

            // 设置伤害衰减
            projectile.SetDamageFalloff(useDamageFalloff, damageFalloffCurve, maxFalloffDistance);

            if (debugMode)
            {
                Debug.Log($"生成子弹 - 伤害: {finalDamage}, 大小: {finalSize}, 速度: {finalSpeed}, 穿透: {penetrationCount}, 暴击率: {criticalChance:P0}, 暴击伤害: {criticalMultiplier}x");
            }
        }
        else
        {
            Debug.LogError("子弹对象没有Projectile组件！");
        }
    }

    // 发射方向计算方法
    private Vector3 CalculateShootDirection(Vector3 aimPoint, bool mouseActive)
    {
        // 如果不使用鼠标瞄准，或者鼠标不活跃，则使用角色方向
        if (!mouseActive)
        {
            Vector3 direction1 = controller.GetCharacterForward();

            // 如果强制水平射击，确保方向是水平的
            if (forceHorizontalShot)
            {
                direction1.y = 0f;
                direction1.Normalize();
            }

            return direction1;
        }

        if (firePoint == null || !useCharacterForward)
        {
            Vector3 direction2 = controller.GetCharacterForward();

            // 如果强制水平射击，确保方向是水平的
            if (forceHorizontalShot)
            {
                direction2.y = 0f;
                direction2.Normalize();
            }

            return direction2;
        }

        // 计算鼠标到枪口的方向
        Vector3 firePointToAim = aimPoint - firePoint.position;

        if (firePointToAim.sqrMagnitude < 0.001f)
        {
            Vector3 direction3 = controller.GetCharacterForward();

            // 如果强制水平射击，确保方向是水平的
            if (forceHorizontalShot)
            {
                direction3.y = 0f;
                direction3.Normalize();
            }

            return direction3;
        }

        // 计算水平距离（忽略Y轴）
        Vector3 firePointPos = firePoint.position;
        Vector3 aimPos = aimPoint;
        firePointPos.y = aimPos.y;
        float horizontalDistance = Vector3.Distance(firePointPos, aimPos);

        // 判断是否距离过近
        Vector3 direction;
        if (horizontalDistance < minMouseDistance)
        {
            direction = controller.GetCharacterForward();
        }
        else
        {
            direction = firePointToAim.normalized;
        }

        // 如果强制水平射击，确保方向是水平的
        if (forceHorizontalShot)
        {
            direction.y = 0f;

            if (direction.sqrMagnitude < 0.001f)
            {
                direction = controller.GetCharacterForward();
                direction.y = 0f;
            }

            direction.Normalize();
        }

        return direction;
    }

    // 设置发射方向模式
    public void SetUseCharacterForward(bool useForward)
    {
        useCharacterForward = useForward;
    }

    // 获取当前发射方向模式
    public bool GetUseCharacterForward()
    {
        return useCharacterForward;
    }

    // 设置强制水平射击
    public void SetForceHorizontalShot(bool forceHorizontal)
    {
        forceHorizontalShot = forceHorizontal;
    }

    // 获取强制水平射击状态
    public bool GetForceHorizontalShot()
    {
        return forceHorizontalShot;
    }

    // 设置水平射击高度
    public void SetHorizontalShotHeight(float height)
    {
        horizontalShotHeight = height;
    }

    // 获取水平射击高度
    public float GetHorizontalShotHeight()
    {
        return horizontalShotHeight;
    }

    private void PlayShootingEffects()
    {
        if (muzzleFlash != null) muzzleFlash.Play();
        if (muzzleSmoke != null) muzzleSmoke.Play();
        if (bulletEjectEffect != null) bulletEjectEffect.Play();
    }

    private void UpdateMuzzleDownState()
    {
        if (weaponModel == null) return;

        float timeSinceLastFire = Time.time - lastFireTime;
        bool shouldMuzzleDown = timeSinceLastFire > muzzleDownDelay && !isShooting;

        if (shouldMuzzleDown)
        {
            currentMuzzleDownAngle = Mathf.Lerp(currentMuzzleDownAngle, 1f, Time.deltaTime * muzzleDownSpeed);
            Quaternion targetRotation = Quaternion.Slerp(
                weaponOriginalRotation,
                idleWeaponRotation,
                currentMuzzleDownAngle
            );
            weaponModel.localRotation = targetRotation;
        }
        else
        {
            currentMuzzleDownAngle = Mathf.Lerp(currentMuzzleDownAngle, 0f, Time.deltaTime * muzzleDownSpeed * 2f);
        }
    }

    private void UpdateWeaponRecovery()
    {
        if (weaponModel == null) return;

        float timeSinceLastFire = Time.time - lastFireTime;
        bool shouldRecover = timeSinceLastFire < muzzleDownDelay || isShooting;

        if (shouldRecover)
        {
            weaponModel.localScale = Vector3.Lerp(weaponModel.localScale, weaponOriginalScale, Time.deltaTime * weaponRestoreSpeed);
            weaponModel.localPosition = Vector3.Lerp(weaponModel.localPosition, weaponOriginalPosition, Time.deltaTime * weaponKickRecoverySpeed);
            weaponModel.localRotation = Quaternion.Slerp(
                weaponModel.localRotation,
                weaponOriginalRotation,
                Time.deltaTime * weaponKickRecoverySpeed
            );
        }

        currentMuzzleRise = Mathf.Lerp(currentMuzzleRise, 0f, Time.deltaTime * muzzleRiseRecoverySpeed);
        currentWeaponKick = Vector3.Lerp(currentWeaponKick, Vector3.zero, Time.deltaTime * weaponKickRecoverySpeed);
        currentWeaponRotation = Vector3.Lerp(currentWeaponRotation, Vector3.zero, Time.deltaTime * weaponKickRecoverySpeed);
    }

    public void ResetMuzzle()
    {
        currentMuzzleDownAngle = 0f;
        if (weaponModel != null)
        {
            weaponModel.localRotation = weaponOriginalRotation;
        }
    }

    #endregion

    #region --- 子弹池系统 ---

    private void CreateBulletPoolParent()
    {
        if (bulletPoolParent == null)
        {
            bulletPoolParent = new GameObject("BulletPool").transform;
            bulletPoolParent.position = Vector3.zero;
            bulletPoolParent.rotation = Quaternion.identity;
            DontDestroyOnLoad(bulletPoolParent.gameObject);
        }
    }

    private void InitializeBulletPool()
    {
        if (bulletPrefab == null)
        {
            Debug.LogError("子弹预制体为空，无法初始化子弹池！");
            return;
        }

        bulletPool = new List<GameObject>();

        for (int i = 0; i < poolSize; i++)
        {
            AddBulletToPool();
        }

        if (debugMode) Debug.Log($"子弹池初始化完成，大小: {bulletPool.Count}");
    }

    private void AddBulletToPool()
    {
        GameObject bullet = Instantiate(bulletPrefab, Vector3.zero, Quaternion.identity);
        bullet.SetActive(false);

        // 设置父对象
        if (bulletPoolParent != null)
        {
            bullet.transform.SetParent(bulletPoolParent);
        }

        bulletPool.Add(bullet);
    }

    private GameObject GetBulletFromPool()
    {
        // 清理已销毁的对象
        bulletPool.RemoveAll(item => item == null);

        // 寻找可用的子弹
        foreach (GameObject bullet in bulletPool)
        {
            if (bullet != null && !bullet.activeInHierarchy)
            {
                return bullet;
            }
        }

        // 如果没有可用的子弹，创建新的
        if (debugMode) Debug.Log("子弹池已满，创建新子弹");
        AddBulletToPool();

        // 返回新创建的子弹
        GameObject newBullet = bulletPool[bulletPool.Count - 1];
        newBullet.SetActive(false);
        return newBullet;
    }

    public void ClearBulletPool()
    {
        foreach (GameObject bullet in bulletPool)
        {
            if (bullet != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(bullet);
                }
                else
                {
                    DestroyImmediate(bullet);
                }
            }
        }
        bulletPool.Clear();

        if (bulletPoolParent != null && Application.isPlaying)
        {
            Destroy(bulletPoolParent.gameObject);
        }
    }

    #endregion

    #region --- 调试方法 ---

    public void TestFire()
    {
        if (firePoint != null)
        {
            Vector3 testDirection = firePoint.forward;

            // 如果强制水平射击，确保方向是水平的
            if (forceHorizontalShot)
            {
                testDirection.y = 0f;
                testDirection.Normalize();
            }

            SpawnBullet(firePoint.position, Quaternion.LookRotation(testDirection), testDirection);
        }
    }

    #endregion
}