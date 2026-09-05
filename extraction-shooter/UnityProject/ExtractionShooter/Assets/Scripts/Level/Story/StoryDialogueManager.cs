using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using GourmetAbyss.CameraSystem;

/// <summary>
/// 剧情对话期间：冻结玩家移动/射击、暂停扣氧气、聚焦相机（orthographicSize）。
/// 使用 lockCount 支持嵌套对话调用。
/// </summary>
public class StoryDialogueManager : MonoBehaviour
{
    public static StoryDialogueManager Instance { get; private set; }

    [Header("剧情完成（仅当前游戏轮，停止运行后重置）")]
    [Tooltip("区分不同剧情线；建议每套剧情用不同 ID。")]
    [SerializeField] private string storyId = "CarrotCubBossSister";

    /// <summary>进程内会话缓存：退出 Play / 关闭游戏后自动清空，不写盘。</summary>
    private static readonly Dictionary<string, bool> SessionStoryCleared = new Dictionary<string, bool>();

    private bool storyClearedThisScene;
    private string completionKey;
    private string currentSceneName;

    [Header("引用（可不填：运行时自动查找）")]
    [SerializeField] private Camera targetCamera;
    [SerializeField] private TopDownController playerController;
    [SerializeField] private CameraFollow cameraFollow;

    [Header("聚焦参数")]
    [SerializeField] private float focusLerpDuration = 0.4f;
    [SerializeField] private float restoreLerpDuration = 0.4f;

    [Header("调试（相机聚焦）")]
    [SerializeField] private bool debugLogCameraFocus = false;

    private bool originalPlayerEnabled;
    private bool originalCanPlayerMove;
    private bool originalCombatState;
    private bool originalOxygenConsuming;

    private int lockCount;
    private readonly Stack<CameraShotLease> storyCameraLeases = new Stack<CameraShotLease>();
    private bool frameworkCameraUsedForCurrentLock;

    private BattleValManager bvm;

    private void Awake()
    {
        StripDontDestroyMarker();
        MoveToActiveSceneIfInDontDestroy();

        if (Instance != null && Instance != this)
        {
            // 如果旧实例来自 DontDestroyOnLoad（上一次关卡残留），优先替换为当前场景实例，避免“场景内管理器被秒销毁”
            if (Instance.gameObject.scene.buildIndex == -1 || Instance.gameObject.scene.name == "DontDestroyOnLoad")
            {
                Debug.LogError("[StoryDialogueManager] 检测到残留的 DontDestroyOnLoad 实例，已销毁旧实例并使用当前场景实例。");
                Destroy(Instance.gameObject);
            }
            else
            {
                Destroy(gameObject);
                return;
            }
        }
        Instance = this;

        ResolveTargetCamera();

        bvm = BattleValManager.Instance;

        InitCompletionKeyForCurrentScene();
        SyncClearedStateFromSession();

        if (playerController == null)
        {
            var playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null)
                playerController = playerObj.GetComponent<TopDownController>();
        }
    }

    private void StripDontDestroyMarker()
    {
        DonotDestroy marker = GetComponent<DonotDestroy>();
        if (marker != null)
        {
            Debug.LogError("[StoryDialogueManager] 移除了 DonotDestroy 组件，剧情对象不允许进入 DontDestroyOnLoad。");
            Destroy(marker);
        }
    }

    private void MoveToActiveSceneIfInDontDestroy()
    {
        if (gameObject.scene.buildIndex != -1 && gameObject.scene.name != "DontDestroyOnLoad") return;
        Scene activeScene = SceneManager.GetActiveScene();
        SceneManager.MoveGameObjectToScene(gameObject, activeScene);
        Debug.LogError($"[StoryDialogueManager] 对象在 DontDestroyOnLoad，已强制移回当前场景：{activeScene.name}");
    }

    private void OnDestroy()
    {
        DisposeAllStoryCameraLeases();
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void InitCompletionKeyForCurrentScene()
    {
        string sceneName = SceneManager.GetActiveScene().name;
        currentSceneName = sceneName;
        completionKey = BuildSessionProgressKey(GetEffectiveStoryId(), sceneName);
    }

    public bool IsStoryClearedThisScene
    {
        get
        {
            string key = BuildSessionProgressKey(GetEffectiveStoryId(), SceneManager.GetActiveScene().name);
            return SessionStoryCleared.TryGetValue(key, out bool cleared) && cleared;
        }
    }

    public void MarkStoryClearedThisScene()
    {
        string sceneName = SceneManager.GetActiveScene().name;
        string key = BuildSessionProgressKey(GetEffectiveStoryId(), sceneName);
        if (SessionStoryCleared.TryGetValue(key, out bool cleared) && cleared)
            return;

        storyClearedThisScene = true;
        SessionStoryCleared[key] = true;
        currentSceneName = sceneName;
        completionKey = key;
    }

    /// <summary>
    /// 清除“当前场景 + 当前 storyId”对应的剧情完成标记（仅本会话）。
    /// </summary>
    public void ClearStoryClearedFlagForCurrentScene()
    {
        InitCompletionKeyForCurrentScene();
        SessionStoryCleared[completionKey] = false;
        storyClearedThisScene = false;
    }

    private void SyncClearedStateFromSession()
    {
        storyClearedThisScene = IsStoryClearedThisScene;
    }

    private static string BuildSessionProgressKey(string effectiveStoryId, string sceneName)
    {
        string idPart = string.IsNullOrWhiteSpace(effectiveStoryId) ? "Auto" : effectiveStoryId;
        return $"StoryCleared_{idPart}_{sceneName}";
    }

    /// <summary>
    /// 静态入口：清除当前场景剧情完成标记。
    /// </summary>
    public static void ClearCurrentSceneStoryProgress()
    {
        if (Instance == null)
        {
            Debug.LogWarning("[StoryDialogueManager] 实例不存在，无法清除剧情标记。");
            return;
        }

        Instance.ClearStoryClearedFlagForCurrentScene();
    }

    [ContextMenu("Debug/Clear Current Scene Story Key")]
    private void DebugClearCurrentSceneStoryKey()
    {
        ClearStoryClearedFlagForCurrentScene();
    }

    public string GetCurrentStoryProgressDebugText()
    {
        return $"storyId={GetEffectiveStoryId()}, scene={currentSceneName}, key={completionKey}, cleared={IsStoryClearedThisScene}, persistence=session-only";
    }

    private string GetEffectiveStoryId()
    {
        return string.IsNullOrWhiteSpace(storyId) ? "Auto" : storyId;
    }

    public void BeginDialogueLock(float focusOrthographicSize)
    {
        BeginDialogueLock(focusOrthographicSize, focusLerpDuration, null);
    }

    public void BeginDialogueLock(float focusOrthographicSize, float customFocusDuration)
    {
        BeginDialogueLock(focusOrthographicSize, customFocusDuration, null);
    }

    public void BeginDialogueLock(float focusOrthographicSize, Transform followTarget)
    {
        BeginDialogueLock(focusOrthographicSize, focusLerpDuration, followTarget);
    }

    public void BeginDialogueLock(float focusOrthographicSize, float customFocusDuration, Transform followTarget)
    {
        lockCount++;
        if (lockCount == 1)
            frameworkCameraUsedForCurrentLock = false;
        if (lockCount != 1)
        {
            // 嵌套对话：已经冻结过了，只需要更新相机聚焦即可。
            ResolveTargetCamera();
            if (!TryAcquireStoryCamera(focusOrthographicSize, customFocusDuration, followTarget))
                Debug.LogWarning("[StoryDialogueManager] CameraDirector 未就绪，跳过嵌套剧情镜头请求。", this);
            return;
        }

        ResolveTargetCamera();

        if (playerController != null)
        {
            originalPlayerEnabled = playerController.enabled;
            originalCanPlayerMove = playerController.canPlayerMove;
            originalCombatState = playerController.GetCombatState();

            Rigidbody rb = playerController.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.velocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }

            // 关键：CameraFollow 会在 LateUpdate 里根据 IsMoving() 自动清除 override。
            // 对话期间禁用 controller 后 FixedUpdate 不再更新 isMoving，可能停留在旧值导致 override 被立刻清掉。
            playerController.StopFootstepParticles();

            // 彻底暂停输入/移动/射击（避免 moveInput 残留继续推走）。
            playerController.enabled = false;
        }

        if (bvm != null)
        {
            originalOxygenConsuming = bvm.IsActive;
            bvm.StopConsuming();
        }
        else
        {
            originalOxygenConsuming = false;
        }

        if (targetCamera == null)
            ResolveTargetCamera();

        if (targetCamera != null && targetCamera.orthographic)
        {
            if (debugLogCameraFocus)
            {
                Debug.Log(
                    $"[StoryDialogueManager] Begin lock | cam={targetCamera.name} ortho={targetCamera.orthographic} from={targetCamera.orthographicSize:F3} to={focusOrthographicSize:F3} duration={customFocusDuration:F3}");
            }

            frameworkCameraUsedForCurrentLock = TryAcquireStoryCamera(
                focusOrthographicSize,
                customFocusDuration,
                followTarget);
            if (!frameworkCameraUsedForCurrentLock)
                Debug.LogWarning("[StoryDialogueManager] CameraDirector 未就绪，跳过剧情镜头请求。", this);
        }
        else
        {
            if (debugLogCameraFocus)
            {
                string camName = targetCamera != null ? targetCamera.name : "null";
                Debug.LogWarning(
                    $"[StoryDialogueManager] Begin lock failed: targetCamera={camName} orthographic={targetCamera != null && targetCamera.orthographic}");
            }
        }
    }

    private void ResolveTargetCamera()
    {
        if (targetCamera != null && targetCamera.orthographic)
            return;

        // 优先用 Camera.main（如果正交）
        Camera main = Camera.main;
        if (main != null && main.orthographic)
        {
            targetCamera = main;

            if (debugLogCameraFocus)
                Debug.Log($"[StoryDialogueManager] Resolved targetCamera from Camera.main => {targetCamera.name}");

            return;
        }

        // 否则找场景里第一个 orthographic 相机
        Camera[] all = Camera.allCameras;
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] != null && all[i].orthographic)
            {
                targetCamera = all[i];

                if (debugLogCameraFocus)
                    Debug.Log($"[StoryDialogueManager] Resolved targetCamera from Camera.allCameras => {targetCamera.name}");

                return;
            }
        }
    }

    public void EndDialogueLock()
    {
        lockCount = Mathf.Max(0, lockCount - 1);
        DisposeTopStoryCameraLease();
        if (lockCount != 0)
            return;

        // 恢复玩家
        if (playerController != null)
        {
            if (originalPlayerEnabled)
            {
                playerController.enabled = true;
                playerController.canPlayerMove = originalCanPlayerMove;
                playerController.SetCombatState(originalCombatState);
            }
        }

        // 恢复氧气消耗
        if (bvm != null && originalOxygenConsuming)
            bvm.ResumeConsuming();

        // 恢复相机聚焦
        DisposeAllStoryCameraLeases();
        frameworkCameraUsedForCurrentLock = false;
    }

    /// <summary>
    /// 兜底：强制清空所有剧情锁并恢复玩家/氧气/相机。
    /// 用于协程被中断或事件链异常时的状态修复。
    /// </summary>
    public void ForceEndAllDialogueLocks()
    {
        lockCount = 0;

        ResolveTargetCamera();

        // 强制恢复玩家控制（用于异常卡锁的兜底）
        if (playerController != null)
        {
            playerController.enabled = true;
            playerController.canPlayerMove = true;
            playerController.SetCombatState(true);
        }

        // 强制恢复氧气消耗
        if (bvm != null)
            bvm.ResumeConsuming();

        // 强制恢复相机缩放
        DisposeAllStoryCameraLeases();
        frameworkCameraUsedForCurrentLock = false;
    }

    private bool TryAcquireStoryCamera(
        float focusOrthographicSize,
        float blendDuration,
        Transform followTarget)
    {
        CameraDirector director = CameraService.Active;
        if (director == null)
        {
            if (cameraFollow == null)
                cameraFollow = FindFirstObjectByType<CameraFollow>();
            director = cameraFollow != null ? cameraFollow.Director : null;
        }

        Transform effectiveTarget = followTarget;
        if (effectiveTarget == null && playerController != null)
            effectiveTarget = playerController.transform;
        if (effectiveTarget == null && cameraFollow != null)
            effectiveTarget = cameraFollow.DefaultTarget;
        if (director == null || effectiveTarget == null)
            return false;

        TransformFocusCameraSource source = new TransformFocusCameraSource(
            effectiveTarget,
            director.CurrentPose,
            focusOrthographicSize,
            new CameraDamping(0.18f, 0.15f, 0.18f),
            CameraShotPolicy.UseUnscaledTime);
        CameraShotLease lease = director.AcquireShot(
            this,
            source,
            new CameraShotOptions(
                300,
                Mathf.Max(0.01f, blendDuration),
                Mathf.Max(0.01f, restoreLerpDuration),
                "Story Dialogue"));
        storyCameraLeases.Push(lease);
        frameworkCameraUsedForCurrentLock = true;
        return true;
    }

    private void DisposeTopStoryCameraLease()
    {
        if (storyCameraLeases.Count == 0)
            return;
        storyCameraLeases.Pop()?.Dispose();
    }

    private void DisposeAllStoryCameraLeases()
    {
        while (storyCameraLeases.Count > 0)
            storyCameraLeases.Pop()?.Dispose();
    }

}

