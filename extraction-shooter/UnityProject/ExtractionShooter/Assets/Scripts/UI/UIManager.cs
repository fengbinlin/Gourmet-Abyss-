using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class UIManager : MonoBehaviour
{
    public static UIManager instance;
    public List<GameObject> BattleUI;

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

    /// <summary>
    /// 批量开关战斗UI（进入家/地上隐藏；进入关卡战斗显示）。
    /// </summary>
    public void SetBattleUIActive(bool active)
    {
        if (BattleUI == null) return;
        for (int i = 0; i < BattleUI.Count; i++)
        {
            var go = BattleUI[i];
            if (go == null) continue;
            if (go.activeSelf != active) go.SetActive(active);
        }
    }
}
