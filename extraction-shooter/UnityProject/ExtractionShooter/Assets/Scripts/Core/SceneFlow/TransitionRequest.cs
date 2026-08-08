namespace Game.Core.SceneFlow
{
    /// <summary>饱和度过渡方向。</summary>
    public enum SaturationDirection { None, ToSaturated, ToUnsaturated }

    /// <summary>发光（Emission）过渡效果。</summary>
    public enum EmissionEffect { None, EnterLevel, ExitLevel }

    /// <summary>本次转场对「本局食材背包」的处理。</summary>
    public enum RunBagAction
    {
        /// <summary>不动。</summary>
        None,
        /// <summary>清空且不结算（进入新副本时）。</summary>
        Clear,
        /// <summary>结算进 GameValManager 后清空（正常带出时）。</summary>
        CommitToGameVal
    }

    /// <summary>本次转场对氧气消耗的处理。</summary>
    public enum OxygenAction { None, StartConsuming, StopConsuming }

    /// <summary>本次转场对餐厅物体位置的处理。</summary>
    public enum RestaurantPose
    {
        /// <summary>不动。</summary>
        Unchanged,
        /// <summary>移开（进入战斗时 x + 100）。</summary>
        MoveAwayForBattle,
        /// <summary>还原到地面场景的初始位置。</summary>
        RestoreToHome
    }

    /// <summary>地面主场景物体（homeSceneObject / postProcessObject）的显隐处理。</summary>
    public enum HomeSceneVisibility { Unchanged, Hide, Show }

    /// <summary>HUD 收尾（mainUI 关开一次 + UITapBounce.ResetPosition）相对于场景加载/卸载的时机。</summary>
    public enum HudRefreshTiming
    {
        /// <summary>在 LoadSceneAsync / UnloadSceneAsync 完成之后执行。</summary>
        AfterSceneOp,
        /// <summary>在 UnloadSceneAsync 发起之后、等待其完成之前执行（ExitLevel 的既有时机）。</summary>
        BeforeSceneOpCompletes
    }

    /// <summary>
    /// 一次转场的完整描述，用来把 LevelManager 里四个复制粘贴的协程收敛成一个。
    /// </summary>
    public sealed class TransitionRequest
    {
        /// <summary>转场类型，仅用于广播给监听者，不影响执行步骤。</summary>
        public TransitionKind Kind;

        /// <summary>要卸载的场景名。为空表示不卸载。</summary>
        public string SceneToUnload;

        /// <summary>要 Additive 加载的场景名。为空表示不加载。</summary>
        public string SceneToLoad;

        /// <summary>过渡 UI Animator 的 Trigger 名。为空表示不触发。</summary>
        public string AnimatorTrigger;

        /// <summary>淡出到白色的车辆所在场景名。为空表示跳过。</summary>
        public string VehicleFadeOutScene;

        /// <summary>从白色淡回原色的车辆所在场景名。为空表示跳过。</summary>
        public string VehicleFadeInScene;

        public SaturationDirection Saturation;
        public EmissionEffect Emission;
        public RunBagAction RunBag;
        public OxygenAction Oxygen;
        public RestaurantPose Restaurant;
        public HomeSceneVisibility HomeObjects;
        public HudRefreshTiming HudTiming = HudRefreshTiming.AfterSceneOp;

        /// <summary>转场结束时把玩家状态置为该值。</summary>
        public PlayerState TargetPlayerState;

        /// <summary>转场结束时战斗 UI 的开关。</summary>
        public bool BattleUiActive;

        /// <summary>是否调用 KeepMainCamera.tKeepMainCamera() 重新绑定主相机。</summary>
        public bool RefreshMainCamera;

        /// <summary>是否调用 BattleValManager.ResetValues()。</summary>
        public bool ResetBattleValues;

        /// <summary>是否播放背包格子入场动画。</summary>
        public bool PlaySlotsEntranceAnimation;

        /// <summary>是否强制重新启用玩家的 TopDownController。</summary>
        public bool ReenablePlayerController;
    }

    /// <summary>
    /// 四条现有转场路径的预设。字段值照抄 LevelManager 当前实现，含尚未修复的缺陷（见各处 [现状] 注释）。
    /// 接线时不要顺手改这些值，缺陷修复是独立一轮改动。
    /// </summary>
    public static class TransitionPresets
    {
        /// <summary>地面主场景名。LevelManager 里硬编码为 "UpGround"。</summary>
        public const string HomeSceneName = "UpGround";

        /// <summary>
        /// 进入关卡。对应 <c>LevelManager.EnterLevelProcess</c>。
        /// </summary>
        /// <remarks>[现状] 不播放背包格子入场动画（另外两条回家的路径都播）。</remarks>
        public static TransitionRequest EnterLevel(string levelName) => new TransitionRequest
        {
            Kind = TransitionKind.EnterLevel,
            SceneToLoad = levelName,
            AnimatorTrigger = "EnterLevel",
            VehicleFadeOutScene = HomeSceneName,
            VehicleFadeInScene = levelName,
            Saturation = SaturationDirection.ToUnsaturated,
            Emission = EmissionEffect.EnterLevel,
            RunBag = RunBagAction.Clear,
            Oxygen = OxygenAction.StartConsuming,
            Restaurant = RestaurantPose.MoveAwayForBattle,
            HomeObjects = HomeSceneVisibility.Hide,
            HudTiming = HudRefreshTiming.AfterSceneOp,
            TargetPlayerState = PlayerState.Battle,
            BattleUiActive = true,
            RefreshMainCamera = true,
            ResetBattleValues = false,
            PlaySlotsEntranceAnimation = false,
            ReenablePlayerController = false
        };

        /// <summary>
        /// 退出关卡回地面。对应 <c>LevelManager.ExitLevelProcess</c>。
        /// </summary>
        /// <remarks>
        /// [现状] <see cref="TransitionRequest.RefreshMainCamera"/> 为 false ——
        /// 四条路径里唯独这条不调用 KeepMainCamera。<br/>
        /// [现状] <see cref="HudRefreshTiming.BeforeSceneOpCompletes"/> ——
        /// mainUI 关开发生在等待卸载完成之前，与其它三条不同。
        /// </remarks>
        public static TransitionRequest ExitLevel(string levelName) => new TransitionRequest
        {
            Kind = TransitionKind.ExitLevel,
            SceneToUnload = levelName,
            AnimatorTrigger = "ExitLevel",
            VehicleFadeOutScene = levelName,
            VehicleFadeInScene = HomeSceneName,
            Saturation = SaturationDirection.ToSaturated,
            Emission = EmissionEffect.ExitLevel,
            RunBag = RunBagAction.CommitToGameVal,
            Oxygen = OxygenAction.StopConsuming,
            Restaurant = RestaurantPose.RestoreToHome,
            HomeObjects = HomeSceneVisibility.Show,
            HudTiming = HudRefreshTiming.BeforeSceneOpCompletes,
            TargetPlayerState = PlayerState.UpGround,
            BattleUiActive = false,
            RefreshMainCamera = false,
            ResetBattleValues = true,
            PlaySlotsEntranceAnimation = true,
            ReenablePlayerController = false
        };

        /// <summary>
        /// 从关卡回家（矿车/死亡路径）。对应 <c>LevelManager.FromLevelToHomeProcess</c>。
        /// </summary>
        /// <remarks>
        /// [现状] AnimatorTrigger 是 "EnterLevel" 而非 "ExitLevel"，疑似复制粘贴未改。<br/>
        /// [现状] VehicleFadeInScene 是 "HomeScene"，但项目中不存在该场景（地面场景叫 "UpGround"），
        /// 因此这一步实际永远找不到车辆、静默跳过。<br/>
        /// [现状] 无 Emission 过渡（另外三条都有）。
        /// </remarks>
        public static TransitionRequest LevelToHome(string levelName) => new TransitionRequest
        {
            Kind = TransitionKind.LevelToHome,
            SceneToUnload = levelName,
            AnimatorTrigger = "EnterLevel",
            VehicleFadeOutScene = levelName,
            VehicleFadeInScene = "HomeScene",
            Saturation = SaturationDirection.ToSaturated,
            Emission = EmissionEffect.None,
            RunBag = RunBagAction.CommitToGameVal,
            Oxygen = OxygenAction.StopConsuming,
            Restaurant = RestaurantPose.RestoreToHome,
            HomeObjects = HomeSceneVisibility.Show,
            HudTiming = HudRefreshTiming.AfterSceneOp,
            TargetPlayerState = PlayerState.UpGround,
            BattleUiActive = false,
            RefreshMainCamera = true,
            ResetBattleValues = true,
            PlaySlotsEntranceAnimation = true,
            ReenablePlayerController = true
        };

        /// <summary>
        /// 关卡之间平级切换。对应 <c>LevelManager.SwitchLevelProcess</c>。
        /// </summary>
        /// <remarks>
        /// [现状] 不处理本局食材背包（既不清空也不结算）。<br/>
        /// [现状] 不启停氧气消耗，靠上一次转场留下的状态延续。<br/>
        /// [现状] 不改动地面主场景物体与餐厅位置（此时它们本就处于战斗态）。
        /// </remarks>
        public static TransitionRequest SwitchLevel(string fromLevel, string toLevel) => new TransitionRequest
        {
            Kind = TransitionKind.SwitchLevel,
            SceneToUnload = fromLevel,
            SceneToLoad = toLevel,
            AnimatorTrigger = "SwitchLevel",
            VehicleFadeOutScene = fromLevel,
            VehicleFadeInScene = toLevel,
            Saturation = SaturationDirection.ToUnsaturated,
            Emission = EmissionEffect.ExitLevel,
            RunBag = RunBagAction.None,
            Oxygen = OxygenAction.None,
            Restaurant = RestaurantPose.Unchanged,
            HomeObjects = HomeSceneVisibility.Unchanged,
            HudTiming = HudRefreshTiming.AfterSceneOp,
            TargetPlayerState = PlayerState.Battle,
            BattleUiActive = true,
            RefreshMainCamera = true,
            ResetBattleValues = false,
            PlaySlotsEntranceAnimation = false,
            ReenablePlayerController = false
        };
    }
}
