using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Core
{
    /// <summary>全局系统的初始化阶段，用来取代手排 <c>[DefaultExecutionOrder]</c> 数字。</summary>
    public enum BootPhase
    {
        /// <summary>日志、对象池等，不得依赖其它系统。</summary>
        Boot = 0,

        /// <summary>配置表解析。</summary>
        Config = 100,

        /// <summary>存档与数值数据。</summary>
        Data = 200,

        /// <summary>玩法系统。</summary>
        Systems = 300,

        /// <summary>UI 层。</summary>
        Ui = 400,

        /// <summary>场景内玩法对象。</summary>
        Gameplay = 500
    }

    /// <summary>按阶段初始化的全局系统，由 <see cref="GameRoot"/> 驱动。</summary>
    public interface IGameSystem
    {
        BootPhase Phase { get; }

        /// <summary>阶段到达时调用，此前所有阶段的系统均已初始化完毕。</summary>
        void InitializeSystem();
    }

    /// <summary>
    /// 全局对象的唯一宿主与启动入口。用法与启用步骤见 Core/README.md。
    /// </summary>
    /// <remarks>
    /// <b>当前未激活</b>：<see cref="AutoBootstrap"/> 找不到 <c>Resources/GameRoot</c> 预制体时静默返回，
    /// 因此对运行时零影响；创建该预制体即自动生效，无需改代码。
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class GameRoot : MonoBehaviour
    {
        private const string PrefabResourcePath = "GameRoot";

        private static GameRoot _instance;

        public static GameRoot Instance => _instance;

        public static bool IsBooted => _instance != null;

        [Header("收编设置")]
        [Tooltip("场景加载后，把散落在 DontDestroyOnLoad 里的孤儿全局物体挂到 GameRoot 之下，便于统一清理。")]
        [SerializeField] private bool adoptStrayPersistentObjects = true;

        [Tooltip("收编时跳过的物体名（例如第三方插件自建的 DDOL 物体）。")]
        [SerializeField] private List<string> adoptionExcludedNames = new List<string>();

        private readonly List<IGameSystem> _systems = new List<IGameSystem>();
        private BootPhase _reachedPhase = (BootPhase)(-1);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void AutoBootstrap()
        {
            if (_instance != null) return;

            GameObject prefab = Resources.Load<GameObject>(PrefabResourcePath);
            if (prefab == null) return; // 预制体尚未创建：保持旧的启动方式。

            GameObject root = Instantiate(prefab);
            root.name = prefab.name; // 去掉 "(Clone)"，便于日志与审计工具识别。
            DontDestroyOnLoad(root);
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            transform.SetParent(null, true);
            DontDestroyOnLoad(gameObject);

            CollectSystemsInChildren();
            RunPhasesUpTo(BootPhase.Gameplay);

            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        private void OnDestroy()
        {
            if (_instance != this) return;

            SceneManager.sceneLoaded -= HandleSceneLoaded;
            _instance = null;
        }

        #region 系统注册与阶段驱动

        /// <summary>所属阶段已经过去时会立即初始化，以支持运行时动态加入的系统。</summary>
        public void RegisterSystem(IGameSystem system)
        {
            if (system == null || _systems.Contains(system)) return;

            _systems.Add(system);

            if (_reachedPhase >= system.Phase)
                InitializeSafely(system);
        }

        public void UnregisterSystem(IGameSystem system)
        {
            if (system != null) _systems.Remove(system);
        }

        private void CollectSystemsInChildren()
        {
            foreach (IGameSystem system in GetComponentsInChildren<IGameSystem>(true))
            {
                if (!_systems.Contains(system)) _systems.Add(system);
            }
        }

        private void RunPhasesUpTo(BootPhase target)
        {
            foreach (BootPhase phase in (BootPhase[])Enum.GetValues(typeof(BootPhase)))
            {
                if (phase > target) break;
                if (phase <= _reachedPhase) continue;

                // 用下标遍历：系统在 InitializeSystem 里注册新系统时不会破坏迭代。
                for (int i = 0; i < _systems.Count; i++)
                {
                    if (_systems[i].Phase == phase) InitializeSafely(_systems[i]);
                }

                _reachedPhase = phase;
            }
        }

        /// <summary>单个系统初始化失败不应拖垮整个启动流程。</summary>
        private static void InitializeSafely(IGameSystem system)
        {
            try
            {
                system.InitializeSystem();
            }
            catch (Exception e)
            {
                Debug.LogError($"[GameRoot] 系统 {system.GetType().Name} 初始化失败：{e}");
            }
        }

        #endregion

        #region 孤儿全局物体收编

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (adoptStrayPersistentObjects) AdoptStrayPersistentObjects();
        }

        /// <summary>
        /// 把 DontDestroyOnLoad 里的其它根物体挂到 GameRoot 之下。只改层级，不改存活语义，
        /// 目的是让「清空全局对象」有一个确定入口。
        /// </summary>
        public void AdoptStrayPersistentObjects()
        {
            Scene ddol = gameObject.scene;
            if (!ddol.IsValid()) return;

            foreach (GameObject root in ddol.GetRootGameObjects())
            {
                if (root == gameObject) continue;
                if (adoptionExcludedNames.Contains(root.name)) continue;

                root.transform.SetParent(transform, true);
            }
        }

        #endregion

        /// <summary>
        /// 销毁全部全局对象并加载目标场景，用于重开 / 退回主菜单。
        /// </summary>
        /// <remarks>
        /// 用 <c>Destroy</c> 而非 <c>DestroyImmediate</c>：销毁与 <c>LoadScene(Single)</c>
        /// 都在本帧末生效，不会互相踩到。
        /// </remarks>
        public static void ResetAllAndLoad(string sceneName)
        {
            if (_instance != null)
            {
                _instance.AdoptStrayPersistentObjects();
                Destroy(_instance.gameObject);
                _instance = null;
            }

            SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
        }
    }
}
