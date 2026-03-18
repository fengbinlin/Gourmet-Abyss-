using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HomeBuildInteration : MonoBehaviour
{
    [Header("交互条件")]
    public bool isPlayerEnter = false;
    public KeyCode interactKey = KeyCode.E;

    [Header("进入交互模式时是否激活网格（兼容旧逻辑）")]
    public bool activateGridOnEnter = true;

    private bool isInteracting = false;
    private List<BuildController> cachedControllers = new List<BuildController>();
    private InteractiveFeedback feedback;

    void Start()
    {
        feedback = GetComponent<InteractiveFeedback>();
        RefreshCache();
        // 默认不在交互模式：禁止拖拽调整
        SetBuildControllersEnabled(false);
    }

    // Update is called once per frame
    void Update()
    {
        if (!isPlayerEnter) return;

        if (Input.GetKeyDown(interactKey))
        {
            if (isInteracting) ExitInteractionMode();
            else EnterInteractionMode();
        }
    }

    private void EnterInteractionMode()
    {
        isInteracting = true;

        if (activateGridOnEnter)
            ActivateAllGrids();

        RefreshCache();
        SetBuildControllersEnabled(true);

        Debug.Log("✅ 进入家园建造交互模式（已激活全部网格）");

        if (feedback != null)
        {
            feedback.PlayFeedback();
        }

        // 显示家具背包 UI
        if (FurnitureUIManager.instance != null && FurnitureUIManager.instance.panelAnimatedController != null)
        {
            FurnitureUIManager.instance.panelAnimatedController.ShowUI();
        }
    }

    private void ExitInteractionMode()
    {
        isInteracting = false;
        // 退出时先刷新缓存，确保把“交互过程中新增生成的家具”也一起禁用
        RefreshCache();
        SetBuildControllersEnabled(false);
        Debug.Log("✅ 退出家园建造交互模式");

        if (feedback != null)
        {
            feedback.StopFeedbackSmoothly();
        }

        // 隐藏家具背包 UI
        if (FurnitureUIManager.instance != null && FurnitureUIManager.instance.panelAnimatedController != null)
        {
            FurnitureUIManager.instance.panelAnimatedController.HideUI();
        }
    }

    private void RefreshCache()
    {
        cachedControllers.Clear();

        // 控制范围：所有挂载 BuildingUnit 的建筑
        BuildingUnit[] units = FindObjectsOfType<BuildingUnit>(true);
        foreach (var u in units)
        {
            if (u == null) continue;
            var c = u.GetComponent<BuildController>();
            if (c == null) continue; // 没有BuildController就不影响拖拽逻辑
            cachedControllers.Add(c);
        }
    }

    private void ActivateAllGrids()
    {
        GridManager[] grids = FindObjectsOfType<GridManager>(true);
        foreach (var g in grids)
        {
            if (g == null) continue;
            g.ActivateInstance(false);
        }
    }

    private void SetBuildControllersEnabled(bool enabled)
    {
        // 允许拖拽的本质：BuildController + Collider2D 都要启用
        for (int i = 0; i < cachedControllers.Count; i++)
        {
            var c = cachedControllers[i];
            if (c == null) continue;

            c.enabled = enabled;

            var col2d = c.GetComponent<Collider2D>();
            if (col2d != null) col2d.enabled = enabled;
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            isPlayerEnter = true;
            if (feedback != null)
            {
                feedback.PlayFeedback();
            }
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            isPlayerEnter = false;
            if (isInteracting) ExitInteractionMode();

            if (feedback != null)
            {
                feedback.StopFeedbackSmoothly();
            }

            // 玩家离开范围时，确保 UI 被隐藏
            if (FurnitureUIManager.instance != null && FurnitureUIManager.instance.panelAnimatedController != null)
            {
                FurnitureUIManager.instance.panelAnimatedController.HideUI();
            }
        }
    }
}
