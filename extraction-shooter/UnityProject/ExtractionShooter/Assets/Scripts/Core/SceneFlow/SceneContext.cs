using UnityEngine.SceneManagement;

namespace Game.Core.SceneFlow
{
    /// <summary>转场类型。用于让监听者区分「进关卡 / 出关卡 / 平级切关卡」。</summary>
    public enum TransitionKind
    {
        /// <summary>从地面主场景进入关卡（Additive 加载）。</summary>
        EnterLevel,

        /// <summary>从关卡退出回地面主场景（卸载关卡）。</summary>
        ExitLevel,

        /// <summary>从关卡直接回家（走矿车/死亡等路径，与 ExitLevel 的收尾步骤不同）。</summary>
        LevelToHome,

        /// <summary>关卡之间平级切换（同时卸载旧关卡、加载新关卡）。</summary>
        SwitchLevel
    }

    /// <summary>
    /// 一次转场的上下文快照，广播给所有 <see cref="ISceneLifecycleListener"/>。
    /// 字段全部只读——监听者只应读取，不应据此互相修改状态。
    /// </summary>
    public readonly struct SceneContext
    {
        /// <summary>本次转场的类型。</summary>
        public readonly TransitionKind Kind;

        /// <summary>来源场景名。进入关卡时为地面场景名，可能为空。</summary>
        public readonly string FromScene;

        /// <summary>目标场景名。退出关卡时为地面场景名，可能为空。</summary>
        public readonly string ToScene;

        /// <summary>与本次回调直接相关的那个场景（Enter 回调里是新加载的场景，Exit 回调里是即将卸载的场景）。</summary>
        public readonly Scene Scene;

        public SceneContext(TransitionKind kind, string fromScene, string toScene, Scene scene)
        {
            Kind = kind;
            FromScene = fromScene;
            ToScene = toScene;
            Scene = scene;
        }
    }

    /// <summary>
    /// 场景生命周期监听者。实现它即可挂接转场时机，无需改动 LevelManager。用法见 Core/README.md。
    /// </summary>
    public interface ISceneLifecycleListener
    {
        /// <summary>目标场景加载完成、且转场收尾步骤执行之前调用。</summary>
        void OnSceneEnter(in SceneContext context);

        /// <summary>场景即将卸载之前调用。此时场景内的物体仍然有效，可以安全读取。</summary>
        void OnSceneExit(in SceneContext context);
    }
}
