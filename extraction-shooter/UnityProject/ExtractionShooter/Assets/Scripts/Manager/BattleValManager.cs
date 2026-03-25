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
    private int secondaryAmmoCurrent;

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

        if (weaponImage != null)
            weaponImage.fillAmount = primaryAmmoCurrent * 1.0f / primaryAmmoMax;
        if (subWeaponImage != null)
            subWeaponImage.fillAmount = secondaryAmmoCurrent * 1.0f / secondaryAmmoMax;

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
        if (primaryAmmoCurrent < primaryAmmoConsumePerShot)
        {
            OnPrimaryAmmoEmpty?.Invoke();
            return false;
        }

        primaryAmmoCurrent -= primaryAmmoConsumePerShot;
        primaryAmmoCurrent = Mathf.Max(0, primaryAmmoCurrent);

        OnPrimaryAmmoChanged?.Invoke();
        return true;
    }
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
    #endregion

    #region 副武器弹药管理
    /// <summary>
    /// 尝试消耗副武器弹药
    /// </summary>
    public bool TryConsumeSecondaryAmmo()
    {

        if (secondaryAmmoCurrent < secondaryAmmoConsumePerShot)
        {
            OnSecondaryAmmoEmpty?.Invoke();
            return false;
        }
        //print(secondaryAmmoCurrent);
        secondaryAmmoCurrent -= secondaryAmmoConsumePerShot;
        secondaryAmmoCurrent = Mathf.Max(0, secondaryAmmoCurrent);

        OnSecondaryAmmoChanged?.Invoke();
        return true;
    }
    public bool CanConsumeSecondaryAmmo()
    {

        if (secondaryAmmoCurrent < secondaryAmmoConsumePerShot)
        {

            return false;
        }

        return true;
    }
    public bool CheckConsumeSecondaryAmmo()
    {
        print("消耗副武器弹药");
        if (secondaryAmmoCurrent < secondaryAmmoConsumePerShot)
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
        primaryAmmoMax = WeaponStatsManager.Instance.primaryAmmoMax;
        primaryAmmoConsumePerShot = WeaponStatsManager.Instance.primaryAmmoConsumePerShot;
        secondaryAmmoMax = WeaponStatsManager.Instance.secondaryAmmoMax;
        secondaryAmmoConsumePerShot = WeaponStatsManager.Instance.secondaryAmmoConsumePerShot;
        oxygenCurrent = oxygenMax;
        primaryAmmoCurrent = primaryAmmoMax;
        secondaryAmmoCurrent = secondaryAmmoMax;

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
        weaponImage.fillAmount = primaryAmmoCurrent * 1.0f / primaryAmmoMax;
        subWeaponImage.fillAmount = secondaryAmmoCurrent * 1.0f / secondaryAmmoMax;
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
        primaryAmmoMax = Mathf.Max(0, newPrimaryAmmoMax);
        primaryAmmoConsumePerShot = Mathf.Max(1, newPrimaryAmmoConsume);
        secondaryAmmoMax = Mathf.Max(0, newSecondaryAmmoMax);
        secondaryAmmoConsumePerShot = Mathf.Max(1, newSecondaryAmmoConsume);

        // 确保当前值不超过新的最大值
        oxygenCurrent = Mathf.Min(oxygenCurrent, oxygenMax);
        primaryAmmoCurrent = Mathf.Min(primaryAmmoCurrent, primaryAmmoMax);
        secondaryAmmoCurrent = Mathf.Min(secondaryAmmoCurrent, secondaryAmmoMax);

        OnOxygenChanged?.Invoke();
        OnPrimaryAmmoChanged?.Invoke();
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