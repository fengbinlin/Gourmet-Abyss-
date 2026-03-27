using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// 剧情对话期间：冻结玩家移动/射击、暂停扣氧气、聚焦相机（orthographicSize）。
/// 使用 lockCount 支持嵌套对话调用。
/// </summary>
public class StoryDialogueManager : MonoBehaviour
{
    public static StoryDialogueManager Instance { get; private set; }

    [Header("剧情完成本地存档（按关卡）")]
    [Tooltip("用于生成 PlayerPrefs Key；建议每套剧情用不同 ID。留空会自动用场景名作为区分。")]
    [SerializeField] private string storyId = "CarrotCubBossSister";
    [Tooltip("剧情进度数据资产（右键 Create -> Story -> Story Progress Data 创建）")]
    [SerializeField] private StoryProgressData storyProgressData;

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

    private float originalOrthographicSize;
    private bool originalOrthographicSizeValid;

    private bool originalPlayerEnabled;
    private bool originalCanPlayerMove;
    private bool originalCombatState;
    private bool originalOxygenConsuming;

    private int lockCount;
    private Coroutine orthographicRoutine;

    private BattleValManager bvm;
    private bool cameraFollowOverrideApplied;

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
        LoadStoryProgress();

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
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void InitCompletionKeyForCurrentScene()
    {
        string sceneName = SceneManager.GetActiveScene().name;
        currentSceneName = sceneName;
        string idPart = string.IsNullOrWhiteSpace(storyId) ? "Auto" : storyId;
        completionKey = $"StoryCleared_{idPart}_{sceneName}";
    }

    private void LoadStoryProgress()
    {
        if (storyProgressData == null)
        {
            storyClearedThisScene = false;
            Debug.LogError($"[StoryDialogueManager] 未绑定 StoryProgressData，默认按未通关处理。storyId={storyId}, scene={currentSceneName}");
            return;
        }

        bool exists = storyProgressData.TryGetCleared(GetEffectiveStoryId(), currentSceneName, out bool cleared);
        storyClearedThisScene = exists && cleared;
    }

    public bool IsStoryClearedThisScene => storyClearedThisScene;

    public void MarkStoryClearedThisScene()
    {
        if (storyClearedThisScene) return;
        storyClearedThisScene = true;
        if (storyProgressData == null)
        {
            Debug.LogError($"[StoryDialogueManager] 标记通关失败：StoryProgressData 未绑定。storyId={storyId}, scene={currentSceneName}");
            return;
        }

        storyProgressData.SetCleared(GetEffectiveStoryId(), currentSceneName, true);
        SaveStoryProgressAsset();
    }

    /// <summary>
    /// 清除“当前场景 + 当前 storyId”对应的剧情完成标记。
    /// </summary>
    public void ClearStoryClearedFlagForCurrentScene()
    {
        InitCompletionKeyForCurrentScene();
        if (storyProgressData == null)
        {
            Debug.LogError($"[StoryDialogueManager] 清除通关标记失败：StoryProgressData 未绑定。storyId={storyId}, scene={currentSceneName}");
            storyClearedThisScene = false;
            return;
        }

        storyProgressData.SetCleared(GetEffectiveStoryId(), currentSceneName, false);
        SaveStoryProgressAsset();
        storyClearedThisScene = false;
        Debug.Log($"[StoryDialogueManager] 已清除剧情完成标记: {completionKey}");
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
        string dataName = storyProgressData != null ? storyProgressData.name : "null";
        return $"storyId={GetEffectiveStoryId()}, scene={currentSceneName}, key={completionKey}, cleared={storyClearedThisScene}, dataAsset={dataName}";
    }

    private string GetEffectiveStoryId()
    {
        return string.IsNullOrWhiteSpace(storyId) ? "Auto" : storyId;
    }

    private void SaveStoryProgressAsset()
    {
#if UNITY_EDITOR
        if (storyProgressData != null)
        {
            EditorUtility.SetDirty(storyProgressData);
            AssetDatabase.SaveAssets();
        }
#endif
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
        if (lockCount != 1)
        {
            // 嵌套对话：已经冻结过了，只需要更新相机聚焦即可。
            ResolveTargetCamera();
            ApplyOrthographicSize(focusOrthographicSize, customFocusDuration);
            
            // 嵌套对话也允许刷新 CameraFollow 目标，避免“size 变了但目标没跟着切”的边缘情况。
            if (followTarget != null)
            {
                if (cameraFollow == null)
                    cameraFollow = FindFirstObjectByType<CameraFollow>();
                if (cameraFollow != null)
                {
                    cameraFollow.SetOverrideTarget(followTarget);
                    cameraFollowOverrideApplied = true;
                }
            }
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

        // 聚焦时也让 CameraFollow 锁定到某个目标（比如 Boss/幼崽/姐姐）
        if (followTarget != null)
        {
            if (cameraFollow == null)
                cameraFollow = FindFirstObjectByType<CameraFollow>();

            if (cameraFollow != null)
            {
                cameraFollow.SetOverrideTarget(followTarget);
                cameraFollowOverrideApplied = true;
            }
        }

        if (targetCamera == null)
            ResolveTargetCamera();

        if (targetCamera != null && targetCamera.orthographic)
        {
            originalOrthographicSize = targetCamera.orthographicSize;
            originalOrthographicSizeValid = true;

            if (debugLogCameraFocus)
            {
                Debug.Log(
                    $"[StoryDialogueManager] Begin lock | cam={targetCamera.name} ortho={targetCamera.orthographic} from={originalOrthographicSize:F3} to={focusOrthographicSize:F3} duration={customFocusDuration:F3}");
            }

            ApplyOrthographicSize(focusOrthographicSize, customFocusDuration);
        }
        else
        {
            originalOrthographicSizeValid = false;

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
        if (targetCamera != null && targetCamera.orthographic && originalOrthographicSizeValid)
            ApplyOrthographicSize(originalOrthographicSize, restoreLerpDuration);

        // 恢复 CameraFollow target
        if (cameraFollowOverrideApplied && cameraFollow != null)
        {
            cameraFollow.ClearOverrideTarget();
            cameraFollowOverrideApplied = false;
        }
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
        if (targetCamera != null && targetCamera.orthographic && originalOrthographicSizeValid)
            ApplyOrthographicSize(originalOrthographicSize, restoreLerpDuration);

        // 强制清理 CameraFollow 的临时跟随
        if (cameraFollow == null)
            cameraFollow = FindFirstObjectByType<CameraFollow>();
        if (cameraFollow != null)
        {
            cameraFollow.ClearOverrideTarget();
            cameraFollowOverrideApplied = false;
        }
    }

    private void ApplyOrthographicSize(float toSize, float duration)
    {
        if (targetCamera == null || !targetCamera.orthographic)
            return;

        if (orthographicRoutine != null)
            StopCoroutine(orthographicRoutine);

        float fromSize = targetCamera.orthographicSize;
        orthographicRoutine = StartCoroutine(OrthographicSizeRoutine(fromSize, toSize, Mathf.Max(0.01f, duration)));
    }

    private IEnumerator OrthographicSizeRoutine(float from, float to, float duration)
    {
        if (targetCamera == null)
            yield break;

        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / duration;
            targetCamera.orthographicSize = Mathf.Lerp(from, to, t);
            yield return null;
        }
        targetCamera.orthographicSize = to;
        orthographicRoutine = null;
    }
}

