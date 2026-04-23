using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(-80)] // 先于大多数 UI 逻辑初始化
public class UIManager : MonoBehaviour
{
    public static UIManager instance;
    public List<GameObject> BattleUI;
    
    [Header("战斗UI特殊规则")]
    [Tooltip("副武器UI在 BattleUI 列表中的 id（下标）。当副武器未激活时会被强制隐藏。")]
    [SerializeField] private int secondaryWeaponUIId = 3;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        // 以 PlayerState 为准做一次初始同步（避免直接从战斗场景启动时 UI 状态不对）
        bool battle = PlayerStateManager.instance != null &&
                      PlayerStateManager.instance.currentState == PlayerState.Battle;
        SetBattleUIActive(battle);
    }
    
    private void OnEnable()
    {
        if (WeaponStatsManager.Instance != null)
            WeaponStatsManager.Instance.OnWeaponStatsChanged += OnWeaponStatsChanged;
    }

    private void OnDisable()
    {
        if (WeaponStatsManager.Instance != null)
            WeaponStatsManager.Instance.OnWeaponStatsChanged -= OnWeaponStatsChanged;
    }

    private void OnWeaponStatsChanged()
    {
        // 数值变化时刷新一次（副武器激活/关闭会影响副武器UI显隐）
        bool battle = PlayerStateManager.instance != null &&
                      PlayerStateManager.instance.currentState == PlayerState.Battle;
        SetBattleUIActive(battle);
    }

    /// <summary>
    /// 批量开关战斗UI（进入家/地上隐藏；进入关卡战斗显示）。
    /// </summary>
    public void SetBattleUIActive(bool active)
    {
        if (BattleUI == null) return;
        
        bool isInBattleState = PlayerStateManager.instance != null &&
                               PlayerStateManager.instance.currentState == PlayerState.Battle;
        bool secondaryEnabled = WeaponStatsManager.Instance != null && WeaponStatsManager.Instance.isSecondaryEnable;
        for (int i = 0; i < BattleUI.Count; i++)
        {
            var go = BattleUI[i];
            if (go == null) continue;
            
            // 副武器 UI（id=secondaryWeaponUIId）只允许在战斗状态 + 战斗UI开启 + 副武器激活时显示
            if (i == secondaryWeaponUIId)
            {
                bool shouldShowSecondaryUI = active && isInBattleState && secondaryEnabled;
                if (go.activeSelf != shouldShowSecondaryUI) go.SetActive(shouldShowSecondaryUI);
                continue;
            }

            if (go.activeSelf != active) go.SetActive(active);
        }
    }
}
