using Game.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 场景标题与氧气消耗倍率。每个场景各挂一份，后加载的接管静态引用。
/// </summary>
public class SceneTitle : MonoSingleton<SceneTitle>
{
    /// <summary>每个场景各有一份，后加载的关卡接管——这是既有语义，保持不变。</summary>
    protected override DuplicatePolicy Duplicate => DuplicatePolicy.OverwriteReference;

    private static SceneTitle _fallback;

    /// <summary>
    /// 当前生效的标题组件。关卡卸载后 <see cref="Instance"/> 为空（地面那份不会重新 Awake），
    /// 此时回落到活动场景里的那份，避免在地面上取到 null。
    /// </summary>
    public static SceneTitle Current
    {
        get
        {
            if (Instance != null) return Instance;

            // 命中回落时才扫场景；有主时短路，不会造成每帧开销。
            if (_fallback == null)
                _fallback = Resolve(SceneManager.GetActiveScene().name);

            return _fallback;
        }
    }

    /// <summary>兼容旧调用点的小写别名。</summary>
    public static SceneTitle instance => Current;

    public float SceneOxygenCostSpeedMultiplier = 1;
    public string SceneName;

    protected override void OnAwake()
    {
        ApplyLevelSatietyConsumptionConfig();
    }

    // Awake 时 ExcelConfigReader 可能还没读完表，Start 再补一次。
    private void Start()
    {
        ApplyLevelSatietyConsumptionConfig();
    }

    /// <summary>按所在场景取关卡饱食度消耗配置，覆盖 Inspector 里填的倍率。</summary>
    private void ApplyLevelSatietyConsumptionConfig()
    {
        if (ExcelConfigReader.Instance == null) return;

        string sceneName = gameObject.scene.name;
        if (!ExcelConfigReader.Instance.TryGetLevelSatietyConsumptionConfig(
                sceneName,
                out LevelSatietyConsumptionConfigData config))
            return;

        SceneOxygenCostSpeedMultiplier = config.consumeEnabled
            ? config.consumeMultiplier
            : 0f;
    }

    /// <summary>
    /// 取指定场景的标题组件。静态引用有效且就在目标场景里就用它，否则到该场景根物体里找。
    /// </summary>
    public static SceneTitle Resolve(string sceneName)
    {
        SceneTitle current = Instance;

        if (string.IsNullOrEmpty(sceneName))
            return current;

        if (current != null && current.gameObject.scene.name == sceneName)
            return current;

        Scene scene = SceneManager.GetSceneByName(sceneName);
        if (scene.IsValid() && scene.isLoaded)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                SceneTitle found = root.GetComponentInChildren<SceneTitle>(true);
                if (found != null) return found;
            }
        }

        return current;
    }

    /// <summary>取指定场景的标题文本；解析不到时返回 null，由调用方决定是否保留原文本。</summary>
    public static string ResolveName(string sceneName)
    {
        SceneTitle title = Resolve(sceneName);
        return title != null ? title.SceneName : null;
    }
}
