using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
public class BattleValManager : MonoBehaviour
{
    public Animator mainUIAnimator;
    public static BattleValManager Instance { get; private set; }
    public GameObject subweaponUI;
    [Header("氧气设置")]
    [SerializeField] private float oxygenMax = 100f;          // 氧气总量
    [SerializeField] private float oxygenConsumeRate = 1f;  // 氧气每秒消耗速度

    [Header("主武器弹药")]
    [SerializeField] private int primaryAmmoMax = 100;       // 主武器弹容量
    [SerializeField] private int primaryAmmoConsumePerShot = 1; // 主武器每次射击消耗

    [Header("副武器弹药")]
    [SerializeField] private int secondaryAmmoMax = 50;      // 副武器弹容量
    [SerializeField] private int secondaryAmmoConsumePerShot = 1; // 副武器每次射击消耗

    [Header("状态")]
    [SerializeField] private bool isActive = false;          // 是否启动消耗
    [Header("调试")]
    [SerializeField] private bool debugOxygenDamageLog = true;

    // 当前值
    public float oxygenCurrent;
    private int primaryAmmoCurrent;
    // 剩余弹药（不在弹夹内，用于充能完成后装填）
    private int primaryReserveAmmoCurrent;
    private int primaryReserveAmmoMax;
    private int secondaryAmmoCurrent;
    // 剩余弹药（不在弹夹内，用于充能完成后装填）
    private int secondaryReserveAmmoCurrent;
    private int secondaryReserveAmmoMax;

    // 事件
    public event Action OnOxygenChanged;
    public event Action OnPrimaryAmmoChanged;
    public event Action OnSecondaryAmmoChanged;
    public event Action OnOxygenDepleted;    // 氧气耗尽
    public event Action OnPrimaryAmmoEmpty;  // 主武器弹药耗尽
    public event Action OnSecondaryAmmoEmpty; // 副武器弹药耗尽
    [Header("进度条效果控制器")]
    [SerializeField] private ResourceBarController oxygenBarController;
    [SerializeField] private ResourceBarController primaryAmmoBarController;
    [SerializeField] private ResourceBarController secondaryAmmoBarController;

    [SerializeField] private GameObject healthTips;
    #region 公共属性
    public float OxygenCurrent => oxygenCurrent;
    public float OxygenMax => oxygenMax;
    public float OxygenPercentage => oxygenMax > 0 ? oxygenCurrent / oxygenMax : 0;

    public int PrimaryAmmoCurrent => primaryAmmoCurrent;
    public int PrimaryAmmoMax => primaryAmmoMax;
    public float PrimaryAmmoPercentage => primaryAmmoMax > 0 ? (float)primaryAmmoCurrent / primaryAmmoMax : 0;

    public int SecondaryAmmoCurrent => secondaryAmmoCurrent;
    public int SecondaryAmmoMax => secondaryAmmoMax;
    public float SecondaryAmmoPercentage => secondaryAmmoMax > 0 ? (float)secondaryAmmoCurrent / secondaryAmmoMax : 0;

    public bool IsActive => isActive;
    public UnityEngine.UI.Image oxgImage;
    public UnityEngine.UI.Image weaponImage;
    public UnityEngine.UI.Image subWeaponImage;

    [Header("弹药条 UI 平滑参数")]
    [Tooltip("弹药减少时，UI fill 从当前值快速靠近目标值的速度(单位: fillAmount/秒)")]
    [SerializeField] private float ammoUiDepleteSpeed = 8f;

    private float weaponUiFillCurrent;
    private float subWeaponUiFillCurrent;

    [Header("弹夹条 UI 平滑参数")]
    [Tooltip("装填后，弹夹UI fill 从当前值快速靠近目标值的速度(单位: fillAmount/秒)")]
    [SerializeField] private float magazineUiFillSpeed = 12f;

    private float primaryMagazineUiFillCurrent;
    private float secondaryMagazineUiFillCurrent;
    [Header("弹夹进度条(填充Image)")]
    public UnityEngine.UI.Image primaryMagazineImage;
    public UnityEngine.UI.Image secondaryMagazineImage;

    #endregion



    private void Awake()
    {
        // 单例初始化
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        // 初始化数值
        ResetValues();
    }

    public void enbaleSecondWeapon()
    {
        subweaponUI.SetActive(true);
    }
    private void Update()
    {
        if (isActive)
        {
            ConsumeOxygen();
            if (healthTips != null && oxygenBarController != null && oxygenMax > 0.0001f &&
                oxygenCurrent / oxygenMax < oxygenBarController.pulseThreshold)
                healthTips.SetActive(true);
        }

        RefreshOxygenDisplayOnly();

        if (!isActive) return;

        // 用平滑逻辑处理总弹药 UI，避免换弹瞬间骤降
        if (weaponImage != null && primaryAmmoMax > 0)
        {
            float target = primaryAmmoCurrent * 1.0f / primaryAmmoMax;
            weaponUiFillCurrent = Mathf.MoveTowards(
                weaponUiFillCurrent,
                target,
                ammoUiDepleteSpeed * Time.deltaTime
            );
            weaponImage.fillAmount = weaponUiFillCurrent;
        }
        if (subWeaponImage != null && secondaryAmmoMax > 0)
        {
            float target = secondaryAmmoCurrent * 1.0f / secondaryAmmoMax;
            subWeaponUiFillCurrent = Mathf.MoveTowards(
                subWeaponUiFillCurrent,
                target,
                ammoUiDepleteSpeed * Time.deltaTime
            );
            subWeaponImage.fillAmount = subWeaponUiFillCurrent;
        }

        // 弹夹进度条：装填时快速变满（平滑），开火时也平滑减少
        if (primaryMagazineImage != null && primaryReserveAmmoMax > 0)
        {
            primaryMagazineUiFillCurrent = primaryReserveAmmoCurrent * 1.0f / primaryReserveAmmoMax;
            primaryMagazineImage.fillAmount = primaryMagazineUiFillCurrent;
        }
        if (secondaryMagazineImage != null && secondaryReserveAmmoMax > 0)
        {
            secondaryMagazineUiFillCurrent = secondaryReserveAmmoCurrent * 1.0f / secondaryReserveAmmoMax;
            secondaryMagazineImage.fillAmount = secondaryMagazineUiFillCurrent;
        }

        float primaryPercent = PrimaryAmmoPercentage;
        float secondaryPercent = SecondaryAmmoPercentage;
        if (primaryAmmoBarController != null)
            primaryAmmoBarController.UpdateProgress(primaryPercent);
        if (secondaryAmmoBarController != null)
            secondaryAmmoBarController.UpdateProgress(secondaryPercent);
    }

    /// <summary>
    /// 同步氧气 UI（无论 isActive 与否，BOSS 受击扣氧后也必须刷新）。
    /// </summary>
    private void RefreshOxygenDisplayOnly()
    {
        if (oxygenMax <= 0.0001f) return;
        if (oxgImage != null)
            oxgImage.fillAmount = oxygenCurrent / oxygenMax;
        if (oxygenBarController != null)
            oxygenBarController.UpdateProgress(OxygenPercentage);
    }

    #region 氧气管理
    /// <summary>
    /// 消耗氧气
    /// </summary>
    private void ConsumeOxygen()
    {
        if (oxygenCurrent <= 0) return;
        if (SceneTitle.instance == null) return;
        float consumeAmount = oxygenConsumeRate * SceneTitle.instance.SceneOxygenCostSpeedMultiplier * Time.deltaTime;
        oxygenCurrent = Mathf.Max(0, oxygenCurrent - consumeAmount);

        OnOxygenChanged?.Invoke();

        // 检查氧气是否耗尽
        if (oxygenCurrent <= 0)
        {
            OnOxygenDepleted?.Invoke();
        }
    }

    /// <summary>
    /// 添加氧气
    /// </summary>
    public void AddOxygen(float amount)
    {
        if (amount <= 0) return;

        oxygenCurrent = Mathf.Min(oxygenMax, oxygenCurrent + amount);
        OnOxygenChanged?.Invoke();
        RefreshOxygenDisplayOnly();
    }

    /// <summary>
    /// 受击等：按数值直接扣除氧气（BOSS 伤害等）。氧气归零时触发 OnOxygenDepleted。
    /// </summary>
    public void DamageOxygen(float amount)
    {
        if (amount <= 0f) return;

        float before = oxygenCurrent;
        oxygenCurrent = Mathf.Max(0f, oxygenCurrent - amount);
        if (debugOxygenDamageLog)
            Debug.Log($"[BattleValManager] DamageOxygen: -{amount:F1}  |  {before:F1} → {oxygenCurrent:F1} / {oxygenMax:F1}  | isActive={isActive}");

        OnOxygenChanged?.Invoke();
        RefreshOxygenDisplayOnly();

        if (oxygenCurrent <= 0f)
        {
            oxygenCurrent = 0f;
            OnOxygenDepleted?.Invoke();
        }
    }

    /// <summary>
    /// 设置氧气消耗速率
    /// </summary>
    public void SetOxygenConsumeRate(float rate)
    {
        oxygenConsumeRate = Mathf.Max(0, rate);
    }
    #endregion

    #region 主武器弹药管理
    /// <summary>
    /// 尝试消耗主武器弹药
    /// </summary>
    public bool TryConsumePrimaryAmmo()
    {
        // 弹药从“弹夹”里扣
        if (primaryReserveAmmoCurrent < primaryAmmoConsumePerShot)
        {
            OnPrimaryAmmoEmpty?.Invoke();
            return false;
        }

        primaryReserveAmmoCurrent -= primaryAmmoConsumePerShot;
        primaryReserveAmmoCurrent = Mathf.Max(0, primaryReserveAmmoCurrent);

        return true;
    }
    public bool CanConsumePrimaryAmmo()
    {
        return primaryReserveAmmoCurrent >= primaryAmmoConsumePerShot;
    }

    // 是否可以开始换弹（弹夹不足且总弹药还有剩余）
    public bool CanReloadPrimaryMagazine()
    {
        // 弹夹不足到无法开火
        bool magazineEmptyOrInsufficient = primaryReserveAmmoCurrent < primaryAmmoConsumePerShot;
        return magazineEmptyOrInsufficient && primaryAmmoCurrent > 0;
    }

    /// <summary>
    /// 添加主武器“总弹药”（不影响当前弹夹，直到换弹时扣除）
    /// </summary>
    public void AddPrimaryAmmo(int amount)
    {
        if (amount <= 0) return;

        primaryAmmoCurrent = Mathf.Min(primaryAmmoMax, primaryAmmoCurrent + amount);
        OnPrimaryAmmoChanged?.Invoke();
    }

    /// <summary>
    /// 设置主武器每次射击消耗
    /// </summary>
    public void SetPrimaryAmmoConsumePerShot(int amount)
    {
        primaryAmmoConsumePerShot = Mathf.Max(1, amount);
    }

    // 兼容旧的 CanConsumePrimaryAmmo 逻辑（保留函数签名以免其它脚本调用出错）
    public bool CanConsumePrimaryAmmoOld()
    {
        return primaryReserveAmmoCurrent >= primaryAmmoConsumePerShot;
    }

    /*
     * 下面原来的 CanConsumePrimaryAmmo/ AddPrimaryAmmo / SetPrimaryAmmoConsumePerShot 将被替换
     */
    /*
    public bool CanConsumePrimaryAmmo()
    {
        if (primaryAmmoCurrent < primaryAmmoConsumePerShot)
        {
            return false;
        }

        return true;
    }
    /// <summary>
    /// 添加主武器弹药
    /// </summary>
    public void AddPrimaryAmmo(int amount) { }

    public void SetPrimaryAmmoConsumePerShot(int amount) { }
    */
    #endregion

    #region 副武器弹药管理
    /// <summary>
    /// 尝试消耗副武器弹药
    /// </summary>
    public bool TryConsumeSecondaryAmmo()
    {

        if (secondaryReserveAmmoCurrent < secondaryAmmoConsumePerShot)
        {
            OnSecondaryAmmoEmpty?.Invoke();
            return false;
        }

        secondaryReserveAmmoCurrent -= secondaryAmmoConsumePerShot;
        secondaryReserveAmmoCurrent = Mathf.Max(0, secondaryReserveAmmoCurrent);
        return true;
    }
    public bool CanConsumeSecondaryAmmo()
    {

        return secondaryReserveAmmoCurrent >= secondaryAmmoConsumePerShot;
    }

    public bool CanReloadSecondaryMagazine()
    {
        bool magazineEmptyOrInsufficient = secondaryReserveAmmoCurrent < secondaryAmmoConsumePerShot;
        return magazineEmptyOrInsufficient && secondaryAmmoCurrent > 0;
    }
    public bool CheckConsumeSecondaryAmmo()
    {
        print("消耗副武器弹药");
        if (secondaryReserveAmmoCurrent < secondaryAmmoConsumePerShot)
        {

            return false;
        }

        return true;
    }
    /// <summary>
    /// 添加副武器弹药
    /// </summary>
    public void AddSecondaryAmmo(int amount)
    {
        if (amount <= 0) return;

        secondaryAmmoCurrent = Mathf.Min(secondaryAmmoMax, secondaryAmmoCurrent + amount);
        OnSecondaryAmmoChanged?.Invoke();
    }

    /// <summary>
    /// 设置副武器每次射击消耗
    /// </summary>
    public void SetSecondaryAmmoConsumePerShot(int amount)
    {
        secondaryAmmoConsumePerShot = Mathf.Max(1, amount);
    }
    #endregion

    #region 控制函数
    /// <summary>
    /// 启动消耗（开始消耗氧气）
    /// </summary>
    public void StartConsuming()
    {
        ResetValues();
        isActive = true;
    }

    /// <summary>
    /// 恢复消耗（继续扣氧气，但不重置当前氧气/弹药等数值）
    /// </summary>
    public void ResumeConsuming()
    {
        isActive = true;
    }

    /// <summary>
    /// 停止消耗（暂停消耗氧气）
    /// </summary>
    public void StopConsuming()
    {
        isActive = false;
    }

    /// <summary>
    /// 重置所有数值到最大值
    /// </summary>
    public void ResetValues()
    {
        mainUIAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        // 从WeaponStatsManager获取最新的数值
        oxygenMax = WeaponStatsManager.Instance.oxygenMax;
        oxygenConsumeRate = WeaponStatsManager.Instance.oxygenConsumeRate;
        
        // 总弹药（用于原本的UI弹药条）
        primaryAmmoMax = WeaponStatsManager.Instance.primaryAmmoMax;
        primaryAmmoConsumePerShot = WeaponStatsManager.Instance.primaryAmmoConsumePerShot;
        secondaryAmmoMax = WeaponStatsManager.Instance.secondaryAmmoMax;
        secondaryAmmoConsumePerShot = WeaponStatsManager.Instance.secondaryAmmoConsumePerShot;

        // 弹夹容量（用于新增的弹夹进度条）
        primaryReserveAmmoMax = WeaponStatsManager.Instance.primaryMagazineCapacity;
        secondaryReserveAmmoMax = WeaponStatsManager.Instance.secondaryMagazineCapacity;
        oxygenCurrent = oxygenMax;

        // 总弹药：初始满（第一次弹夹免费，不在此时扣除）
        primaryAmmoCurrent = primaryAmmoMax;
        secondaryAmmoCurrent = secondaryAmmoMax;

        // 弹夹：默认一管子弹（从总弹药中取，但第一次不扣总弹药UI）
        primaryReserveAmmoCurrent = Mathf.Min(primaryReserveAmmoMax, primaryAmmoCurrent);
        secondaryReserveAmmoCurrent = Mathf.Min(secondaryReserveAmmoMax, secondaryAmmoCurrent);

        healthTips.SetActive(false);
        // 重置进度条到初始状态
        if (oxygenBarController != null)
            oxygenBarController.ResetBar();

        if (primaryAmmoBarController != null)
            primaryAmmoBarController.ResetBar();

        if (secondaryAmmoBarController != null)
            secondaryAmmoBarController.ResetBar();

        OnOxygenChanged?.Invoke();
        OnPrimaryAmmoChanged?.Invoke();
        OnSecondaryAmmoChanged?.Invoke();
        oxgImage.fillAmount = OxygenCurrent * 1.0f / oxygenMax;
        if (weaponImage != null && primaryAmmoMax > 0)
            weaponUiFillCurrent = primaryAmmoCurrent * 1.0f / primaryAmmoMax;
        if (subWeaponImage != null && secondaryAmmoMax > 0)
            subWeaponUiFillCurrent = secondaryAmmoCurrent * 1.0f / secondaryAmmoMax;

        if (weaponImage != null && primaryAmmoMax > 0)
            weaponImage.fillAmount = weaponUiFillCurrent;
        if (subWeaponImage != null && secondaryAmmoMax > 0)
            subWeaponImage.fillAmount = subWeaponUiFillCurrent;

        if (primaryMagazineImage != null && primaryReserveAmmoMax > 0)
        {
            float target = primaryReserveAmmoCurrent * 1.0f / primaryReserveAmmoMax;
            primaryMagazineUiFillCurrent = Mathf.MoveTowards(
                primaryMagazineUiFillCurrent,
                target,
                magazineUiFillSpeed * Time.deltaTime
            );
            primaryMagazineImage.fillAmount = primaryMagazineUiFillCurrent;
        }
        if (secondaryMagazineImage != null && secondaryReserveAmmoMax > 0)
        {
            float target = secondaryReserveAmmoCurrent * 1.0f / secondaryReserveAmmoMax;
            secondaryMagazineUiFillCurrent = Mathf.MoveTowards(
                secondaryMagazineUiFillCurrent,
                target,
                magazineUiFillSpeed * Time.deltaTime
            );
            secondaryMagazineImage.fillAmount = secondaryMagazineUiFillCurrent;
        }
    }

    /// <summary>
    /// 设置初始值（可在运行时调整）
    /// </summary>
    public void SetValues(float newOxygenMax, float newOxygenConsumeRate,
                         int newPrimaryAmmoMax, int newPrimaryAmmoConsume,
                         int newSecondaryAmmoMax, int newSecondaryAmmoConsume)
    {
        oxygenMax = Mathf.Max(0, newOxygenMax);
        oxygenConsumeRate = Mathf.Max(0, newOxygenConsumeRate);
        // 总弹药（原本UI弹药条）
        primaryAmmoMax = Mathf.Max(0, newPrimaryAmmoMax);
        primaryAmmoConsumePerShot = Mathf.Max(1, newPrimaryAmmoConsume);
        secondaryAmmoMax = Mathf.Max(0, newSecondaryAmmoMax);
        secondaryAmmoConsumePerShot = Mathf.Max(1, newSecondaryAmmoConsume);

        // 弹夹容量：优先使用 WeaponStatsManager
        if (WeaponStatsManager.Instance != null)
        {
            primaryReserveAmmoMax = WeaponStatsManager.Instance.primaryMagazineCapacity;
            secondaryReserveAmmoMax = WeaponStatsManager.Instance.secondaryMagazineCapacity;
        }
        else
        {
            primaryReserveAmmoMax = Mathf.Max(1, primaryReserveAmmoMax);
            secondaryReserveAmmoMax = Mathf.Max(1, secondaryReserveAmmoMax);
        }

        // 确保当前值不超过新的最大值
        oxygenCurrent = Mathf.Min(oxygenCurrent, oxygenMax);
        primaryAmmoCurrent = Mathf.Min(primaryAmmoCurrent, primaryAmmoMax);
        secondaryAmmoCurrent = Mathf.Min(secondaryAmmoCurrent, secondaryAmmoMax);
        primaryReserveAmmoCurrent = Mathf.Min(primaryReserveAmmoCurrent, primaryReserveAmmoMax);
        secondaryReserveAmmoCurrent = Mathf.Min(secondaryReserveAmmoCurrent, secondaryReserveAmmoMax);

        OnOxygenChanged?.Invoke();
        OnPrimaryAmmoChanged?.Invoke();
        OnSecondaryAmmoChanged?.Invoke();
    }
    #endregion

    #region 弹夹装填逻辑
    public void ReloadPrimaryMagazine()
    {
        // 弹夹装填完成时：从“总弹药”里扣除本次装填的子弹
        if (primaryAmmoCurrent <= 0) return;

        int bulletsToLoad = Mathf.Min(primaryReserveAmmoMax, primaryAmmoCurrent);
        if (bulletsToLoad <= 0) return;

        primaryAmmoCurrent -= bulletsToLoad;
        primaryReserveAmmoCurrent = bulletsToLoad;
        OnPrimaryAmmoChanged?.Invoke();
    }

    public void ReloadSecondaryMagazine()
    {
        if (secondaryAmmoCurrent <= 0) return;

        int bulletsToLoad = Mathf.Min(secondaryReserveAmmoMax, secondaryAmmoCurrent);
        if (bulletsToLoad <= 0) return;

        secondaryAmmoCurrent -= bulletsToLoad;
        secondaryReserveAmmoCurrent = bulletsToLoad;
        OnSecondaryAmmoChanged?.Invoke();
    }

    #endregion

    #region 调试功能
    /// <summary>
    /// 打印当前状态（用于调试）
    /// </summary>
    public void PrintStatus()
    {
        Debug.Log($"氧气: {oxygenCurrent:F1}/{oxygenMax:F1} ({OxygenPercentage:P0})");
        Debug.Log($"主武器弹药: {primaryAmmoCurrent}/{primaryAmmoMax} ({PrimaryAmmoPercentage:P0})");
        Debug.Log($"副武器弹药: {secondaryAmmoCurrent}/{secondaryAmmoMax} ({SecondaryAmmoPercentage:P0})");
        Debug.Log($"消耗状态: {isActive}");
    }

    /// <summary>
    /// 添加调试快捷键
    /// </summary>
    private void OnGUI()
    {

    }
    #endregion
}