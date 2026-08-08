using System.Collections;
using System.Collections.Generic;
using Game.Core;
using Game.Core.SceneFlow;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DefaultExecutionOrder(-60)] // 在 Audio/UI/GameVal 之后，大部分游戏逻辑之前
public class LevelManager : PersistentMonoSingleton<LevelManager>
{
    public GameObject mainUI;
    public Text TitleText;

    /// <summary>兼容旧调用点的小写别名，等价于 <see cref="Instance"/>。</summary>
    public static LevelManager instance => Instance;

    [Header("场景对象")]
    public GameObject homeSceneObject;
    public GameObject restaurantObject;
    public GameObject postProcessObject;

    [Header("过渡系统")]
    public EmissionTransition emissionTransition;
    public SaturationTransition saturationTransition;
    public Animator transitionUIAnimator;

    [Header("过渡设置")]
    [SerializeField] private float transitionDuration = 1.0f;
    [SerializeField] private float uiAnimationDelay = 0.5f;

    [Header("场景设置")]
    [SerializeField] private string levelSceneName = "Layer1";

    // 私有变量
    private bool isTransitioning = false;
    private List<string> loadedLevels = new List<string>();
    private Vector3 restaurantInitialPosition;
    private bool hasCachedRestaurantInitialPosition = false;
    private bool movedForSkillTree = false;

    private void Start()
    {
        ApplySceneTitle(SceneManager.GetActiveScene().name);
        // 默认启动一般在“家/地上”，先隐藏战斗UI，进入关卡再打开
        UIManager.instance?.SetBattleUIActive(false);
        // TransitionUIAnimator启用时自动播放第一个动画
        if (transitionUIAnimator != null && transitionUIAnimator.enabled)
        {
            transitionUIAnimator.Play("DefaultState", 0, 0f);
        }

        CacheRestaurantInitialPosition();
    }

    #region 对外的四条转场入口

    /// <summary>
    /// 进入关卡
    /// </summary>
    public void EnterLevel(string sceneName = null)
    {
        if (isTransitioning) return;

        string targetScene = sceneName ?? levelSceneName;
        AudioManager.Instance.PlayAudio("3");
        StartCoroutine(RunTransition(TransitionPresets.EnterLevel(targetScene)));
    }

    /// <summary>
    /// 离开关卡返回主场景
    /// </summary>
    public void ExitLevel()
    {
        if (isTransitioning || loadedLevels.Count == 0) return;

        string currentLevel = loadedLevels[loadedLevels.Count - 1];
        AudioManager.Instance.PlayAudio("3");
        StartCoroutine(RunTransition(TransitionPresets.ExitLevel(currentLevel)));
    }

    /// <summary>
    /// 从关卡返回主场景
    /// </summary>
    public void FromLevelToHome(string sceneName = null)
    {
        if (isTransitioning) return;

        string targetScene = sceneName;
        if (string.IsNullOrEmpty(targetScene) && loadedLevels.Count > 0)
        {
            targetScene = loadedLevels[loadedLevels.Count - 1];
        }
        AudioManager.Instance.PlayAudio("3");
        StartCoroutine(RunTransition(TransitionPresets.LevelToHome(targetScene)));
    }

    /// <summary>
    /// 切换关卡
    /// </summary>
    public void SwitchLevel(string fromLevel, string toLevel)
    {
        if (isTransitioning) return;
        AudioManager.Instance.PlayAudio("3");
        StartCoroutine(RunTransition(TransitionPresets.SwitchLevel(fromLevel, toLevel)));
    }

    /// <summary>
    /// 检查是否正在过渡
    /// </summary>
    public bool IsTransitioning()
    {
        return isTransitioning;
    }

    #endregion

    #region 统一转场管线

    /// <summary>
    /// 所有转场的唯一执行器。四条路径的差异全部由 <see cref="TransitionRequest"/> 描述，
    /// 取值见 <see cref="TransitionPresets"/>。新增路径加预设即可，不要再复制这段协程。
    /// </summary>
    private IEnumerator RunTransition(TransitionRequest req)
    {
        // ---- 前置状态 ----
        isTransitioning = true;
        ApplyRunBag(req.RunBag);
        if (req.Oxygen == OxygenAction.StopConsuming) BattleValManager.Instance?.StopConsuming();
        // 切场前清理全局消息，避免消息面板残留卡住
        GlobalMessageUI.Clear();

        if (transitionUIAnimator != null && !string.IsNullOrEmpty(req.AnimatorTrigger))
            transitionUIAnimator.SetTrigger(req.AnimatorTrigger);

        yield return new WaitForSeconds(uiAnimationDelay);

        // ---- 旧场景淡出到白色 ----
        VehicleColorTransition fadeOut = FindVehicleInScene(req.VehicleFadeOutScene);
        if (fadeOut != null) fadeOut.TransitionToWhite(transitionDuration);

        ApplySaturation(req.Saturation);
        ApplyEmission(req.Emission);

        yield return new WaitForSeconds(transitionDuration);

        if (fadeOut != null) fadeOut.SetToWhiteImmediate();

        // ---- 场景加载 / 卸载 ----
        AsyncOperation unload = null;
        AsyncOperation load = null;

        if (!string.IsNullOrEmpty(req.SceneToUnload))
        {
            unload = SceneManager.UnloadSceneAsync(req.SceneToUnload);
            if (unload != null) unload.allowSceneActivation = true;
        }

        if (!string.IsNullOrEmpty(req.SceneToLoad))
        {
            load = SceneManager.LoadSceneAsync(req.SceneToLoad, LoadSceneMode.Additive);
            if (load != null) load.allowSceneActivation = true;
        }

        // 这一档要在等待完成之前就复位，是跨帧行为，不能挪。
        if (req.HudTiming == HudRefreshTiming.BeforeSceneOpCompletes)
        {
            ResetTapBounce();
            BlinkMainUI();
        }

        while ((unload != null && !unload.isDone) || (load != null && !load.isDone))
            yield return null;

        if (req.HudTiming == HudRefreshTiming.BeforeSceneOpCompletes)
            ApplySceneTitle(TitleSceneOf(req));
        else if (req.HudTiming == HudRefreshTiming.AfterSceneOpBeforeWorldSetup)
            RefreshHud(req);

        // ---- 世界状态 ----
        ApplyHomeObjects(req.HomeObjects);
        ApplyRestaurantPose(req.Restaurant);
        if (req.RefreshMainCamera) RefreshMainCamera();

        if (req.HudTiming == HudRefreshTiming.AfterWorldSetup)
            RefreshHud(req);

        // ---- 新场景从白色淡回原色 ----
        if (req.ExtraFrameBeforeFadeIn) yield return null;

        VehicleColorTransition fadeIn = FindVehicleInScene(req.VehicleFadeInScene);
        if (fadeIn != null)
        {
            fadeIn.enabled = true;
            fadeIn.SetToWhiteImmediate();
            fadeIn.TransitionToOriginal(transitionDuration);
        }
        else if (req.Kind == TransitionKind.EnterLevel)
        {
            Debug.LogWarning($"在场景 {req.VehicleFadeInScene} 中未找到VehicleColorTransition组件");
        }

        // ---- 收尾 ----
        if (req.ReenablePlayerController) ReenablePlayerController();
        if (req.Oxygen == OxygenAction.StartConsuming) BattleValManager.Instance?.StartConsuming();

        if (!string.IsNullOrEmpty(req.SceneToUnload)) loadedLevels.Remove(req.SceneToUnload);
        if (!string.IsNullOrEmpty(req.SceneToLoad)) loadedLevels.Add(req.SceneToLoad);

        isTransitioning = false;
        PlayerStateManager.instance.currentState = req.TargetPlayerState;
        UIManager.instance?.SetBattleUIActive(req.BattleUiActive);
        if (req.ResetBattleValues) BattleValManager.Instance?.ResetValues();
        if (req.PlaySlotsEntranceAnimation) InventoryManager.instance?.PlaySlotsEntranceAnimation();
    }

    private static void ApplyRunBag(RunBagAction action)
    {
        switch (action)
        {
            case RunBagAction.Clear:
                InventoryManager.instance?.ClearRunIngredients();
                break;
            case RunBagAction.CommitToGameVal:
                InventoryManager.instance?.TransferRunIngredientsToGameValAndClear();
                break;
        }
    }

    private void ApplySaturation(SaturationDirection direction)
    {
        if (saturationTransition == null) return;

        switch (direction)
        {
            case SaturationDirection.ToSaturated:
                saturationTransition.TransitionToSaturated();
                break;
            case SaturationDirection.ToUnsaturated:
                saturationTransition.TransitionToUnsaturated();
                break;
        }
    }

    private void ApplyEmission(EmissionEffect effect)
    {
        if (emissionTransition == null) return;

        switch (effect)
        {
            case EmissionEffect.EnterLevel:
                emissionTransition.EnterLevelTransition();
                break;
            case EmissionEffect.ExitLevel:
                emissionTransition.ExitLevelTransition();
                break;
        }
    }

    private void ApplyHomeObjects(HomeSceneVisibility visibility)
    {
        if (visibility == HomeSceneVisibility.Unchanged) return;

        bool active = visibility == HomeSceneVisibility.Show;
        if (homeSceneObject != null) homeSceneObject.SetActive(active);
        if (postProcessObject != null) postProcessObject.SetActive(active);
    }

    private void ApplyRestaurantPose(RestaurantPose pose)
    {
        switch (pose)
        {
            case RestaurantPose.MoveAwayForBattle:
                MoveRestaurantForBattle();
                break;
            case RestaurantPose.RestoreToHome:
                RestoreRestaurantToHomePosition();
                break;
        }
    }

    private void RefreshHud(TransitionRequest req)
    {
        ResetTapBounce();
        ApplySceneTitle(TitleSceneOf(req));
        BlinkMainUI();
    }

    /// <summary>标题取自新加载的场景；没有新场景（退出关卡）时取当前活动场景。</summary>
    private static string TitleSceneOf(TransitionRequest req)
    {
        return !string.IsNullOrEmpty(req.SceneToLoad)
            ? req.SceneToLoad
            : SceneManager.GetActiveScene().name;
    }

    /// <summary>关开一次 mainUI，让其下的面板重跑 OnEnable 完成刷新。</summary>
    private void BlinkMainUI()
    {
        if (mainUI == null) return;

        mainUI.SetActive(false);
        mainUI.SetActive(true);
    }

    private static void ReenablePlayerController()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        TopDownController controller = player != null ? player.GetComponent<TopDownController>() : null;
        if (controller != null) controller.enabled = true;
    }

    #endregion

    /// <summary>按目标场景解析标题。解析不到时保留原文本。</summary>
    private void ApplySceneTitle(string sceneName)
    {
        if (TitleText == null) return;

        string title = SceneTitle.ResolveName(sceneName);
        if (!string.IsNullOrEmpty(title))
            TitleText.text = title;
    }

    private static void RefreshMainCamera()
    {
        KeepMainCamera.instance?.tKeepMainCamera();
    }

    private static void ResetTapBounce()
    {
        UITapBounce.Instance?.ResetPosition();
    }

    /// <summary>
    /// 在指定场景中查找车辆
    /// </summary>
    private VehicleColorTransition FindVehicleInScene(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName)) return null;

        Scene scene = SceneManager.GetSceneByName(sceneName);
        if (!scene.IsValid())
        {
            // 尝试在主活动场景中查找
            scene = SceneManager.GetActiveScene();
            if (scene.name != sceneName)
            {
                return null;
            }
        }

        GameObject[] rootObjects = scene.GetRootGameObjects();
        foreach (GameObject obj in rootObjects)
        {
            VehicleColorTransition vehicle = obj.GetComponentInChildren<VehicleColorTransition>(true);
            if (vehicle != null)
            {
                return vehicle;
            }
        }

        return null;
    }

    private void CacheRestaurantInitialPosition()
    {
        if (restaurantObject == null || hasCachedRestaurantInitialPosition) return;
        restaurantInitialPosition = restaurantObject.transform.position;
        hasCachedRestaurantInitialPosition = true;
    }

    private void MoveRestaurantForBattle()
    {
        if (restaurantObject == null) return;
        CacheRestaurantInitialPosition();

        Vector3 targetPos = restaurantInitialPosition;
        targetPos.x += 100f;
        restaurantObject.transform.position = targetPos;
    }

    private void RestoreRestaurantToHomePosition()
    {
        if (restaurantObject == null) return;
        CacheRestaurantInitialPosition();
        restaurantObject.transform.position = restaurantInitialPosition;
    }

    /// <summary>
    /// 打开技能树时把餐厅移开（仅在地面场景使用）
    /// </summary>
    public void MoveRestaurantForSkillTree()
    {
        if (restaurantObject == null) return;
        CacheRestaurantInitialPosition();
        if (movedForSkillTree) return;

        Vector3 targetPos = restaurantInitialPosition;
        targetPos.x += 1000f;
        restaurantObject.transform.position = targetPos;
        movedForSkillTree = true;
    }

    /// <summary>
    /// 关闭技能树时恢复餐厅位置
    /// </summary>
    public void RestoreRestaurantFromSkillTree()
    {
        if (restaurantObject == null) return;
        CacheRestaurantInitialPosition();
        restaurantObject.transform.position = restaurantInitialPosition;
        movedForSkillTree = false;
    }
}
