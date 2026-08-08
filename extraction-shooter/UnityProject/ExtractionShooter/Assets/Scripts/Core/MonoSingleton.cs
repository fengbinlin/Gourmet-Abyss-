using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// 重复实例出现时的处理策略。四个取值穷举了项目中实际存在的四种守卫写法，
    /// 选错会造成静默的行为变更。用法见 Core/README.md。
    /// </summary>
    public enum DuplicatePolicy
    {
        /// <summary>留先来的，销毁后来的。</summary>
        DestroyNewcomer,

        /// <summary>留先来的，后来者保持存活（Start/Update 照跑），只是不成为单例。</summary>
        KeepIncumbent,

        /// <summary>留后来的，谁都不销毁。对应裸写法 <c>instance = this;</c>。</summary>
        OverwriteReference,

        /// <summary>留后来的，并销毁先来的那个 GameObject。</summary>
        DestroyIncumbent
    }

    /// <summary>
    /// 场景内单例基类。跨场景请用 <see cref="PersistentMonoSingleton{T}"/>。
    /// </summary>
    /// <remarks>
    /// 子类不要声明 <c>Awake</c>（用 <see cref="OnAwake"/>）；需要 <c>OnDestroy</c> 时必须
    /// <c>override</c> 并调用 <c>base.OnDestroy()</c>，否则 <see cref="Instance"/> 不会被清空。
    /// </remarks>
    public abstract class MonoSingleton<T> : MonoBehaviour where T : MonoSingleton<T>
    {
        public static T Instance { get; private set; }

        public static bool Exists => Instance != null;

        protected virtual DuplicatePolicy Duplicate => DuplicatePolicy.DestroyNewcomer;

        /// <summary>赢得单例后调用，时机等同于原来的 Awake。</summary>
        protected virtual void OnAwake() { }

        /// <summary>未能成为单例时调用。<see cref="DuplicatePolicy.KeepIncumbent"/> 下本物体仍然存活。</summary>
        protected virtual void OnLostSingletonRace() { }

        /// <summary>当前单例即将销毁时调用。对所有实例都要执行的清理请改用 <c>override OnDestroy</c>。</summary>
        protected virtual void OnSingletonDestroyed() { }

        /// <summary>
        /// 赢得单例后、<see cref="OnAwake"/> 之前调用。供持久化子类挂接 DontDestroyOnLoad，
        /// 这样子类覆写 <see cref="OnAwake"/> 而不调 base 也不会丢失持久化。
        /// </summary>
        private protected virtual void OnSingletonClaimed() { }

        protected virtual void Awake()
        {
            if (!ClaimSingleton())
            {
                OnLostSingletonRace();
                return;
            }

            OnSingletonClaimed();
            OnAwake();
        }

        protected virtual void OnDestroy()
        {
            if (!ReferenceEquals(Instance, this)) return;

            OnSingletonDestroyed();
            Instance = null;
        }

        /// <summary>返回 false 表示本实例落败、不应继续初始化。</summary>
        private bool ClaimSingleton()
        {
            T incumbent = Instance;

            // 用 == 而非 ReferenceEquals：需要 Unity 的「已销毁对象等于 null」语义，
            // 好让上次场景遗留的悬空引用被当作空位。
            if (incumbent == null || ReferenceEquals(incumbent, this))
            {
                Instance = (T)this;
                return true;
            }

            switch (Duplicate)
            {
                case DuplicatePolicy.OverwriteReference:
                    Instance = (T)this;
                    return true;

                case DuplicatePolicy.DestroyIncumbent:
                    Instance = (T)this;
                    Destroy(incumbent.gameObject);
                    return true;

                case DuplicatePolicy.KeepIncumbent:
                    return false;

                default:
                    Destroy(gameObject);
                    return false;
            }
        }
    }

    /// <summary>
    /// 跨场景存活的单例基类（自动 DontDestroyOnLoad）。
    /// </summary>
    /// <remarks>
    /// 刻意不声明 <c>Start</c>：项目里多数管理器已有自己的 <c>private void Start()</c>，
    /// 基类若也声明会被隐藏且 Unity 只派发到子类，导致基类逻辑被静默跳过。
    /// </remarks>
    public abstract class PersistentMonoSingleton<T> : MonoSingleton<T> where T : PersistentMonoSingleton<T>
    {
        /// <summary>false 时由子类自行选时机调用 <see cref="MarkPersistent"/>。</summary>
        protected virtual bool PersistOnAwake => true;

        /// <summary>
        /// DontDestroyOnLoad 只对根物体生效。默认 false 以保留既有行为——
        /// 项目里有管理器挂在子物体上，其 DDOL 一直是空操作，改成 true 会改变运行时结构。
        /// </summary>
        protected virtual bool DetachBeforePersist => false;

        private bool _persisted;

        private protected override void OnSingletonClaimed()
        {
            if (PersistOnAwake) MarkPersistent();
        }

        /// <summary>幂等。</summary>
        protected void MarkPersistent()
        {
            if (_persisted) return;
            _persisted = true;

            if (DetachBeforePersist && transform.parent != null)
                transform.SetParent(null, true);

            DontDestroyOnLoad(gameObject);
        }
    }
}
