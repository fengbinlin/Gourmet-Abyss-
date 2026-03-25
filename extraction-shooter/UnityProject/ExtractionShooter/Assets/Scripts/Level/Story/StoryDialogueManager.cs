using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

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

    private bool storyClearedThisScene;
    private string completionKey;

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
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        ResolveTargetCamera();

        bvm = BattleValManager.Instance;

        InitCompletionKeyForCurrentScene();
        LoadStoryProgress();

        SceneManager.sceneLoaded += OnSceneLoaded;

        if (playerController == null)
        {
            var playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null)
                playerController = playerObj.GetComponent<TopDownController>();
        }
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        InitCompletionKeyForCurrentScene();
        LoadStoryProgress();
    }

    private void InitCompletionKeyForCurrentScene()
    {
        string sceneName = SceneManager.GetActiveScene().name;
        string idPart = string.IsNullOrWhiteSpace(storyId) ? "Auto" : storyId;
        completionKey = $"StoryCleared_{idPart}_{sceneName}";
    }

    private void LoadStoryProgress()
    {
        storyClearedThisScene = PlayerPrefs.GetInt(completionKey, 0) == 1;
    }

    public bool IsStoryClearedThisScene => storyClearedThisScene;

    public void MarkStoryClearedThisScene()
    {
        if (storyClearedThisScene) return;
        storyClearedThisScene = true;
        PlayerPrefs.SetInt(completionKey, 1);
        PlayerPrefs.Save();
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

