using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 挂在玩家上，接收 BOSS 近战/抛射/冲撞伤害。
/// 伤害表现为扣除 BattleValManager 中的氧气（与战斗 UI 一致）；氧气耗尽时由 BattleValManager.OnOxygenDepleted 驱动死亡等逻辑。
/// </summary>
public class BossAttackReceiver : MonoBehaviour, IBossAttackTarget
{
    [Header("与 TopDownController 减伤联动（可选）")]
    [SerializeField] private TopDownController topDown;

    [Tooltip("无 BattleValManager 时是否在控制台提示（例如非战斗场景）")]
    [SerializeField] private bool logIfNoBattleManager = true;

    [SerializeField] private bool debugBossDamageLog = true;

    /// <summary>本次受到的氧气伤害（折算后）</summary>
    public UnityEvent<float> OnDamaged;

    /// <summary>参数为 (当前氧气, 氧气上限)，与旧版血量事件同名便于沿用 Inspector 绑定</summary>
    public UnityEvent<float, float> OnHealthChanged;

    /// <summary>氧气因本次受击归零时触发（若已归零则不再触发）</summary>
    public UnityEvent OnDied;

    private void Awake()
    {
        if (topDown == null)
            topDown = GetComponent<TopDownController>();
    }

    public void TakeBossDamage(float damage, Vector3 worldPoint, Vector3 worldDirection)
    {
        if (debugBossDamageLog)
            Debug.Log($"[BossAttackReceiver] TakeBossDamage 原始={damage:F1}  go={gameObject.name}", this);

        var bvm = BattleValManager.Instance;
        if (bvm == null)
        {
            if (logIfNoBattleManager)
                Debug.LogWarning("BossAttackReceiver: 未找到 BattleValManager，无法扣除氧气。请确认战斗场景已加载该单例。", this);
            return;
        }

        if (bvm.OxygenCurrent <= 0f)
        {
            if (debugBossDamageLog)
                Debug.Log("[BossAttackReceiver] 氧气已为 0，忽略受击。", this);
            return;
        }

        float d = damage;
        if (topDown != null && topDown.currentDamageReducePct > 0f)
            d *= 1f - Mathf.Clamp01(topDown.currentDamageReducePct);

        if (WeaponStatsManager.Instance != null)
            d *= Mathf.Max(0f, WeaponStatsManager.Instance.bossDamageToOxygenMultiplier);

        if (debugBossDamageLog)
            Debug.Log($"[BossAttackReceiver] 折算后扣氧={d:F1}  (减伤/倍率已应用)", this);

        float before = bvm.OxygenCurrent;
        bvm.DamageOxygen(d);

        OnDamaged?.Invoke(d);
        OnHealthChanged?.Invoke(bvm.OxygenCurrent, bvm.OxygenMax);

        if (before > 0f && bvm.OxygenCurrent <= 0f)
            OnDied?.Invoke();
    }
}
