using System.Collections.Generic;
using Game.Core;
using UnityEngine;

/// <summary>摆放阶段管理器 - 只负责启用键盘控制和检查完成状态</summary>
public class PlaceManager : MonoSingleton<PlaceManager>
{
    [Header("玩家初始网格位置")]
    public Vector2Int player1StartGrid = new Vector2Int(8, 8);
    public Vector2Int player2StartGrid = new Vector2Int(12, 8);
    
    // 记录每个玩家是否已摆放
    private Dictionary<string, bool> playerPlacements = new Dictionary<string, bool>();
    
    void Start()
    {
        playerPlacements["Player1"] = false;
        playerPlacements["Player2"] = false;
        
        Debug.Log("PlaceManager 初始化");
    }
    
    /// <summary>开始摆放阶段</summary>
    public void BeginPlacementPhase()
    {
        Debug.Log("===== PlaceManager.BeginPlacementPhase 开始 =====");
        
        playerPlacements["Player1"] = false;
        playerPlacements["Player2"] = false;
        
        if (SelectManager.Instance == null)
        {
            Debug.LogError("❌ SelectManager.Instance 为 null");
            return;
        }
        
        // 获取选中的Unit（不创建副本）
        Dictionary<string, BuildingUnit> selectedUnits = SelectManager.Instance.GetSelectedUnitsForPlacement();
        
        // 为每个玩家启用键盘控制
        EnableUnitKeyboardControl("Player1", selectedUnits, player1StartGrid);
        EnableUnitKeyboardControl("Player2", selectedUnits, player2StartGrid);
        
        Debug.Log("===== PlaceManager.BeginPlacementPhase 结束 =====");
    }
    
    /// <summary>启用Unit的键盘控制模式</summary>
    private void EnableUnitKeyboardControl(string playerID, Dictionary<string, BuildingUnit> selectedUnits, Vector2Int startGrid)
    {
        if (!selectedUnits.TryGetValue(playerID, out BuildingUnit unit) || unit == null)
        {
            Debug.LogWarning($"⚠️ {playerID} 没有选择道具，自动视为完成");
            playerPlacements[playerID] = true;
            return;
        }
        
        // 获取BuildController组件
        BuildController controller = unit.GetComponent<BuildController>();
        if (controller == null)
        {
            Debug.LogError($"❌ {playerID} 的道具 {unit.name} 缺少 BuildController 组件！");
            playerPlacements[playerID] = true;
            return;
        }
        
        // 激活GameObject
        unit.gameObject.SetActive(true);
        
        // 启用键盘控制模式
        controller.EnableKeyboardMode(playerID, startGrid);
        
        Debug.Log($"✅ {playerID} 的道具 {unit.name} 已启用键盘控制，初始位置: {startGrid}");
    }
    
    /// <summary>玩家完成摆放（由BuildController调用）</summary>
    public void PlayerPlacedUnit(string playerID, BuildingUnit placedUnit)
    {
        playerPlacements[playerID] = true;
        Debug.Log($"✅ {playerID} 已完成摆放");
        
        // 通知SelectManager记录已摆放的道具
        if (SelectManager.Instance != null && placedUnit != null)
        {
            SelectManager.Instance.RegisterPlacedUnit(placedUnit);
        }
        
        // 检查是否所有玩家都完成了
        CheckAllPlayersPlaced();
    }
    
    /// <summary>检查所有玩家是否都摆放完了</summary>
    private void CheckAllPlayersPlaced()
    {
        bool allPlaced = true;
        foreach (var kvp in playerPlacements)
        {
            if (!kvp.Value)
            {
                allPlaced = false;
                break;
            }
        }
        
        if (allPlaced)
        {
            Debug.Log("🎯 所有玩家均完成摆放，进入游戏阶段");
            
            if (AllGameManager.Instance != null)
            {
                AllGameManager.Instance.SwitchToPlayingPhase();
            }
        }
    }
    
    /// <summary>重置摆放状态（用于新关卡）</summary>
    public void ResetPlacement()
    {
        playerPlacements["Player1"] = false;
        playerPlacements["Player2"] = false;
        
        Debug.Log("PlaceManager 已重置");
    }
}
