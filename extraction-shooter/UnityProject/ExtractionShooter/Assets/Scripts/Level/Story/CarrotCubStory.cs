using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 幼崽剧情逻辑：
/// 1) 等待阶段循环显示“呜呜呜/有人吗/快救救我姐”等文案
/// 2) 玩家进入触发范围：播放对话（冻结玩家/暂停扣氧气/聚焦相机）
/// 3) 对话结束后：幼崽沿路标点移动到恶霸地点，并用文字引导玩家
/// 4) 恶霸死亡后：等待姐姐登场；姐姐对话结束后由姐姐脚本触发幼崽消失
/// </summary>
public class CarrotCubStory : MonoBehaviour
{
    public enum CubState
    {
        Waiting,
        IntroDialogue,
        GuidingMove,
        WaitSister,
        Disappearing
    }

    [Header("动画")]
    [SerializeField] private Animator animator;
    [SerializeField] private string animBoolIsMoving = "IsMoving";

    [Header("幼崽 UI（Canvas->Text/TMP）")]
    [SerializeField] private GameObject canvasRoot;
    [SerializeField] private TMP_Text tmpText;
    [SerializeField] private UnityEngine.UI.Text uiText;
    [SerializeField] private bool showCanvasEvenWhenEmpty = true;

    [Header("等待阶段（循环显示）")]
    [SerializeField] private List<string> waitingLines = new List<string>
    {
        "呜呜呜……",
        "有人吗？",
        "快救救我姐！"
    };
    [SerializeField] private float waitingLineInterval = 2.0f;

    [Header("玩家进入触发后的“赎金对话”（冻结玩家）")]
    [SerializeField] private List<string> introDialogueLines = new List<string>
    {
        "我姐姐被恶霸掳走了！",
        "他们要我交赎金……要不然就撕票！",
        "求求你，快救救她！"
    };
    [SerializeField] private float introLineDuration = 1.1f;
    [SerializeField] private float introFocusOrthographicSize = 3.4f;

    [Header("移动路线（空物体路标点）")]
    [SerializeField] private Transform[] waypoints;
    [SerializeField] private float moveSpeed = 2.2f;
    [SerializeField] private float waypointArrivalThreshold = 0.15f;
    [SerializeField] private float pauseAtWaypointSeconds = 0.75f;

    [Header("沿路引导文案（不冻结玩家）")]
    [SerializeField] private List<string> guideLinesByWaypoint = new List<string>
    {
        "这边！跟我来！",
        "别慢呀！快点！",
        "就在前面了……恶霸就在那！"
    };
    [SerializeField] private float guideLineDuration = 0.9f;
    [SerializeField] private bool lockDuringGuideText = false;
    [SerializeField] private float guideFocusOrthographicSize = 3.6f;

    [Header("剧情引用")]
    [SerializeField] private BossStoryController bossStory;
    [Header("剧情激活控制（到最后路标后才出现）")]
    [SerializeField] private GameObject bossRoot;
    [SerializeField] private GameObject sisterRoot;
    [SerializeField] private bool deactivateBossAndSisterAtStart = true;
    [SerializeField] private bool disappearWhenBossDefeatedIfSisterMissing = true;

    public CubState State { get; private set; }

    public StoryDialogueManager dialogueManager;
    private Coroutine waitingRoutine;
    private Coroutine moveRoutine;
    private bool interacted;

    [Header("引导前置条件（需要玩家进入幼崽范围）")]
    [SerializeField] private bool requirePlayerEnterRangeToProceed = true;
    [SerializeField] private float playerEnterRangeRecheckInterval = 0.15f;
    [SerializeField] private float playerProceedDistance = 2.2f; // 引路继续所需的玩家距离
    [SerializeField] private List<string> waitingForPlayerLines = new List<string>
    {
        "怎么不跟上呀！快进来！",
        "就在这儿，别离太远！",
        "快点！再晚恶霸就要动手了！"
    };
    [SerializeField] private float waitingForPlayerLineInterval = 1.2f;

    private Collider triggerCollider;
    private Transform playerTransform;
    private bool playerInCubTrigger;

    private StoryDialogueManager GetDialogueManager()
    {
        if (dialogueManager != null) return dialogueManager;
        return StoryDialogueManager.Instance;
    }

    private void Awake()
    {
        StripDontDestroyMarker();
        MoveToActiveSceneIfInDontDestroy();

        //dialogueManager = StoryDialogueManager.Instance;
        // 优先拿 IsTrigger 的 collider，避免脚本挂在带多个 collider 的物体上时拿错引用。
        Collider[] cols = GetComponents<Collider>();
        for (int i = 0; i < cols.Length; i++)
        {
            if (cols[i] != null && cols[i].isTrigger)
            {
                triggerCollider = cols[i];
                break;
            }
        }
        if (triggerCollider == null)
            triggerCollider = GetComponent<Collider>();
    }

    private void StripDontDestroyMarker()
    {
        DonotDestroy marker = GetComponent<DonotDestroy>();
        if (marker != null)
        {
            Debug.LogError("[CarrotCubStory] 移除了 DonotDestroy 组件，CarrotCub 不能常驻 DontDestroyOnLoad。");
            Destroy(marker);
        }
    }

    private void MoveToActiveSceneIfInDontDestroy()
    {
        if (gameObject.scene.buildIndex != -1 && gameObject.scene.name != "DontDestroyOnLoad") return;
        Scene activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(gameObject, activeScene);
        Debug.LogError($"[CarrotCubStory] 对象在 DontDestroyOnLoad，已强制移回当前场景：{activeScene.name}");
    }

    private void Start()
    {
        State = CubState.Waiting;
        if (canvasRoot != null) canvasRoot.SetActive(showCanvasEvenWhenEmpty);
        SetText("");
        SetIsMoving(false);

        if (gameObject.scene.buildIndex == -1 || gameObject.scene.name == "DontDestroyOnLoad")
        {
            Debug.LogError("[CarrotCubStory] 当前对象处于 DontDestroyOnLoad 场景。这样会导致关卡卸载后路标引用丢失。请移除 CarrotCub（或其父物体）上的 DonotDestroy/DontDestroyOnLoad。");
        }

        playerTransform = GameObject.FindGameObjectWithTag("Player")?.transform;

        if (waypoints == null || waypoints.Length == 0)
        {
            Debug.LogError("[CarrotCubStory] waypoints 未配置或长度为0，幼崽无法引导移动。");
        }
        else
        {
            for (int i = 0; i < waypoints.Length; i++)
            {
                if (waypoints[i] == null)
                {
                    Debug.LogError($"[CarrotCubStory] waypoints[{i}] 为空引用。通常是该路标对象被销毁/场景卸载后引用失效。");
                }
            }
        }

        // 若已通关：本关不再激活幼崽/BOSS/姐姐（直接跳过剧情）
        if (StoryDialogueManager.Instance != null && StoryDialogueManager.Instance.IsStoryClearedThisScene)
        {
            Debug.LogError($"[CarrotCubStory] 启动即销毁：检测到本场景剧情已通关。{StoryDialogueManager.Instance.GetCurrentStoryProgressDebugText()}");
            if (bossRoot != null) bossRoot.SetActive(false);
            if (sisterRoot != null) sisterRoot.SetActive(false);
            if (canvasRoot != null) canvasRoot.SetActive(false);
            Destroy(gameObject);
            return;
        }
        else if (StoryDialogueManager.Instance == null)
        {
            Debug.LogError("[CarrotCubStory] StoryDialogueManager.Instance 为空，无法读取剧情进度，按未通关继续。");
        }
        else
        {
            Debug.Log($"[CarrotCubStory] 正常启动（未通关）。{StoryDialogueManager.Instance.GetCurrentStoryProgressDebugText()}");
        }

        if (deactivateBossAndSisterAtStart)
        {
            if (bossRoot != null) bossRoot.SetActive(false);
            if (sisterRoot != null) sisterRoot.SetActive(false);
        }

        if (bossStory != null)
            bossStory.OnBossDefeated += HandleBossDefeated;

        StartWaitingLoop();
    }

    private void OnDestroy()
    {
        if (bossStory != null)
            bossStory.OnBossDefeated -= HandleBossDefeated;
    }

    private void StartWaitingLoop()
    {
        if (waitingRoutine != null) StopCoroutine(waitingRoutine);
        waitingRoutine = StartCoroutine(WaitingTextRoutine());
    }

    private IEnumerator WaitingTextRoutine()
    {
        if (waitingLines == null || waitingLines.Count == 0)
        {
            SetText("");
            yield break;
        }

        int i = 0;
        while (State == CubState.Waiting)
        {
            SetText(waitingLines[i % waitingLines.Count]);
            i++;
            yield return new WaitForSeconds(waitingLineInterval);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        playerTransform = other.transform;
        playerInCubTrigger = true;

        if (State == CubState.Waiting && !interacted)
        {
            interacted = true;
            if (waitingRoutine != null) StopCoroutine(waitingRoutine);
            StartCoroutine(IntroThenMoveRoutine());
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        playerInCubTrigger = false;
    }

    private IEnumerator IntroThenMoveRoutine()
    {
        State = CubState.IntroDialogue;

        if (canvasRoot != null) canvasRoot.SetActive(true);
        // 冻结：玩家不动/氧气不掉/相机聚焦
        StoryDialogueManager mgr = GetDialogueManager();
        if (mgr != null)
        {
            mgr.BeginDialogueLock(introFocusOrthographicSize, transform);
        }

        if (introDialogueLines != null && introDialogueLines.Count > 0)
        {
            foreach (var line in introDialogueLines)
            {
                SetText(line);
                yield return new WaitForSeconds(introLineDuration);
            }
        }
        else
        {
            SetText("我姐姐被掳走了……求你救她。");
            yield return new WaitForSeconds(1.5f);
        }

        if (mgr != null)
        {
            mgr.EndDialogueLock();
        }

        State = CubState.GuidingMove;
        moveRoutine = StartCoroutine(GuidingMoveRoutine());
    }

    private IEnumerator GuidingMoveRoutine()
    {
        SetIsMoving(true);

        Vector3 originalY = transform.position;
        float keepY = originalY.y;

        int waypointCount = waypoints != null ? waypoints.Length : 0;
        if (waypointCount == 0)
        {
            SetIsMoving(false);
            yield break;
        }

        for (int i = 0; i < waypointCount; i++)
        {
            Transform wp = waypoints[i];
            if (wp == null) continue;

            Vector3 target = wp.position;
            target.y = keepY;

            // 移动到路标点
            while (Vector3.Distance(transform.position, target) > waypointArrivalThreshold)
            {
                transform.position = Vector3.MoveTowards(transform.position, target, moveSpeed * Time.deltaTime);

                // 让幼崽面向前方（简化：只在水平面旋转）
                Vector3 dir = target - transform.position;
                dir.y = 0f;
                if (dir.sqrMagnitude > 0.001f)
                    transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), 10f * Time.deltaTime);

                yield return null;
            }

            // 路标点停一下 + 引导文本（可选是否冻结）
            if (!lockDuringGuideText && i < guideLinesByWaypoint.Count && !string.IsNullOrWhiteSpace(guideLinesByWaypoint[i]))
            {
                SetText(guideLinesByWaypoint[i]);
                yield return new WaitForSeconds(guideLineDuration);
            }
            else if (lockDuringGuideText && i < guideLinesByWaypoint.Count && !string.IsNullOrWhiteSpace(guideLinesByWaypoint[i]))
            {
                StoryDialogueManager guideMgr = GetDialogueManager();
                if (guideMgr != null)
                {
                    guideMgr.BeginDialogueLock(guideFocusOrthographicSize, transform);
                }
                SetText(guideLinesByWaypoint[i]);
                yield return new WaitForSeconds(guideLineDuration);
                if (guideMgr != null)
                {
                    guideMgr.EndDialogueLock();
                }
            }

            // 到达本路标后：等待玩家进入幼崽范围，才继续下一个点
            if (requirePlayerEnterRangeToProceed)
            {
                yield return WaitForPlayerEnterRangeBeforeProceed();
            }

            if (pauseAtWaypointSeconds > 0f)
                yield return new WaitForSeconds(pauseAtWaypointSeconds);
        }

        SetIsMoving(false);

        // 到达最后路标：才让恶霸/姐姐出现
        if (bossRoot != null) bossRoot.SetActive(true);
        if (sisterRoot != null) sisterRoot.SetActive(true);

        // 到达恶霸地点后（不冻结玩家）
        SetText("就是这里了……拜托你了！");

        State = CubState.WaitSister;
    }

    private IEnumerator WaitForPlayerEnterRangeBeforeProceed()
    {
        float timeSinceLastPrompt = 0f;
        int promptIdx = 0;

        while (true)
        {
            if (IsPlayerWithinProceedDistance())
                yield break;

            timeSinceLastPrompt += Time.deltaTime;
            if (waitingForPlayerLines != null && waitingForPlayerLines.Count > 0 && timeSinceLastPrompt >= waitingForPlayerLineInterval)
            {
                timeSinceLastPrompt = 0f;
                string line = waitingForPlayerLines[promptIdx % waitingForPlayerLines.Count];
                promptIdx++;
                SetText(line);
            }

            yield return new WaitForSeconds(playerEnterRangeRecheckInterval);
        }
    }

    private bool IsPlayerWithinProceedDistance()
    {
        if (playerTransform == null) return false;

        Vector3 a = transform.position;
        Vector3 b = playerTransform.position;
        a.y = 0f;
        b.y = 0f;
        float dist = Vector3.Distance(a, b);
        return dist <= Mathf.Max(0.1f, playerProceedDistance);
    }

    private void HandleBossDefeated()
    {
        // 恶霸死后：姐姐会负责最终对话与让幼崽消失
        if (State == CubState.Disappearing) return;
        if (State == CubState.Waiting || State == CubState.IntroDialogue) return;

        if (State != CubState.WaitSister)
            State = CubState.WaitSister;

        if (disappearWhenBossDefeatedIfSisterMissing)
        {
            // 作为兜底：如果你没配姐姐脚本，就让幼崽也消失
            //（一般你会用 SisterStoryController 来接管）
        }
    }

    public void Disappear()
    {
        State = CubState.Disappearing;
        if (waitingRoutine != null) StopCoroutine(waitingRoutine);
        if (moveRoutine != null) StopCoroutine(moveRoutine);
        SetIsMoving(false);

        if (canvasRoot != null) canvasRoot.SetActive(false);
        Destroy(gameObject);
    }

    private void SetIsMoving(bool moving)
    {
        if (animator == null) return;
        animator.SetBool(animBoolIsMoving, moving);
    }

    private void SetText(string s)
    {
        if (canvasRoot != null && !showCanvasEvenWhenEmpty && string.IsNullOrWhiteSpace(s))
            canvasRoot.SetActive(false);
        else
        {
            if (canvasRoot != null) canvasRoot.SetActive(true);
        }

        if (tmpText != null) tmpText.text = s;
        if (uiText != null) uiText.text = s;
    }
}

