using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// 恶霸剧情逻辑：
/// 1) 玩家进入检测范围：播放威胁对话（冻结玩家/暂停扣氧气/聚焦相机）
/// 2) 对话结束后：恶霸朝玩家持续发射子弹（不需要移动）
/// 3) 恶霸被击杀（EnemyHealth）后：播放死前遗言，然后通知姐姐脚本
/// </summary>
public class BossStoryController : MonoBehaviour
{
    [Header("组件")]
    [SerializeField] private EnemyHealth enemyHealth;
    [SerializeField] private Animator animator;

    [Header("检测范围（Trigger）")]
    [SerializeField] private string playerTag = "Player";

    [Header("Boss UI（Canvas->Text/TMP）")]
    [SerializeField] private GameObject canvasRoot;
    [SerializeField] private TMP_Text tmpText;
    [SerializeField] private UnityEngine.UI.Text uiText;
    [SerializeField] private bool hideCanvasOnStart = true;

    [Header("威胁对话（玩家进入后冻结玩家）")]
    [SerializeField] private List<string> threatLines = new List<string>
    {
        "哈哈哈……你来得正好。",
        "交赎金，或者我现在就撕票！"
    };
    [SerializeField] private float threatLineDuration = 1.0f;
    [SerializeField] private float threatFocusOrthographicSize = 3.2f;

    [Header("死前遗言（恶霸死亡后冻结玩家）")]
    [SerializeField] private List<string> deathLines = new List<string>
    {
        "……咳。你会后悔的……"
    };
    [SerializeField] private float deathLineDuration = 1.1f;
    [SerializeField] private float deathFocusOrthographicSize = 3.1f;

    [Header("远程攻击动画参数")]
    [SerializeField] private string animBoolIsAttacking = "IsAttacking";

    [Header("子弹发射器（BossRangedBulletEmitter）")]
    [SerializeField] private BossRangedBulletEmitter bulletEmitter;

    [Header("攻击节奏")]
    [SerializeField] private float fireInterval = 0.28f;
    [SerializeField] private bool rotateToFacePlayer = true;

    [Header("玩家检测（距离优先，无需触发器）")]
    [SerializeField] private bool useDistanceDetection = true;
    [SerializeField] private float playerDetectDistance = 10f;
    [SerializeField] private float playerExitDistance = 9f;

    public event System.Action OnBossBattleStart;

    public event System.Action OnBossDefeated;

    [Header("蓄力与开火花样")]
    [SerializeField] private bool useChargeWindup = true;
    [SerializeField] private float chargeWindupSeconds = 0.35f;

    [Tooltip("径向爆发概率（0-1））")]
    [SerializeField] private float radialBurstChance = 0.35f;
    [Tooltip("扇形概率（0-1）；当扇形Chance取值命中后才会发扇形）")]
    [SerializeField] private float fanChance = 0.35f;

    [SerializeField] private int radialBulletCount = 12;
    [SerializeField] private float radialRadius = 7f;

    [SerializeField] private int fanBulletCount = 5;
    [SerializeField] private float fanSpreadDegrees = 95f;

    public StoryDialogueManager dialogueManager;
    public Transform playerTransform;
    private Collider myTriggerCollider;

    private bool threatPlayed;
    private bool playerInRange;
    private bool deathHandled;
    private Coroutine threatCoroutine;
    private Coroutine attackCoroutine;

    /// <summary>
    /// 剧情用途：在 Boss 死亡后延长销毁时间，确保姐姐感谢对话完成后再消失。
    /// </summary>
    public void RequestDeathDestroyDelay(float delaySeconds)
    {
        if (enemyHealth == null) return;
        enemyHealth.SetDestroyDelayAfterDeath(delaySeconds);
    }

    public void RequestDeathDestroyDelayForSisterThanks(float sisterThanksDurationSeconds)
    {
        if (enemyHealth == null) return;

        float bossDeathDialogueSeconds = 0f;
        if (deathLines != null && deathLines.Count > 0)
            bossDeathDialogueSeconds = deathLines.Count * deathLineDuration;
        else
            bossDeathDialogueSeconds = 1.2f;

        float extra = 0.6f; // 冗余：给击杀后协程调度/最后一帧收尾
        float total = Mathf.Max(0.1f, bossDeathDialogueSeconds + sisterThanksDurationSeconds + extra);
        enemyHealth.SetDestroyDelayAfterDeath(total);
    }

    private void Awake()
    {
        //dialogueManager = StoryDialogueManager.Instance;
        if (enemyHealth == null) enemyHealth = GetComponent<EnemyHealth>();
        if (animator == null) animator = GetComponent<Animator>();
        myTriggerCollider = GetComponent<Collider>();
    }

    private void Start()
    {
        if (hideCanvasOnStart && canvasRoot != null) canvasRoot.SetActive(false);

        // 若已通关：本关不再激活 BOSS 剧情逻辑
        if (StoryDialogueManager.Instance != null && StoryDialogueManager.Instance.IsStoryClearedThisScene)
        {
            if (canvasRoot != null) canvasRoot.SetActive(false);
            gameObject.SetActive(false);
            return;
        }

        // 允许你在 Inspector 直接拖 Player 进来；不填才用 Tag 自动找。
        if (playerTransform == null)
            playerTransform = GameObject.FindGameObjectWithTag(playerTag)?.transform;
    }

    private void OnEnable()
    {
        if (useDistanceDetection)
            return;

        // 如果你是“激活恶霸时玩家已经在范围内”，Unity 不会触发 OnTriggerEnter。
        // 这里做一次兜底检测，保证流程能继续进入威胁/攻击。
        if (deathHandled) return;
        if (playerTransform == null)
            playerTransform = GameObject.FindGameObjectWithTag(playerTag)?.transform;

        if (playerTransform == null) return;
        if (myTriggerCollider == null) return;

        if (!myTriggerCollider.bounds.Contains(playerTransform.position)) return;
        if (playerInRange) return;

        playerInRange = true;

        if (!threatPlayed)
        {
            if (threatCoroutine != null) StopCoroutine(threatCoroutine);
            threatCoroutine = StartCoroutine(ThreatThenStartAttack());
        }
        else
        {
            TryStartAttackLoop();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (useDistanceDetection)
            return;

        if (deathHandled) return;
        if (!other.CompareTag(playerTag)) return;

        playerInRange = true;
        playerTransform = other.transform;

        if (!threatPlayed)
        {
            if (threatCoroutine != null) StopCoroutine(threatCoroutine);
            threatCoroutine = StartCoroutine(ThreatThenStartAttack());
        }
        else
        {
            TryStartAttackLoop();
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (useDistanceDetection)
            return;

        if (!other.CompareTag(playerTag)) return;

        playerInRange = false;
        StopAttackLoop();
    }

    private IEnumerator ThreatThenStartAttack()
    {
        threatPlayed = true;

        if (canvasRoot != null) canvasRoot.SetActive(true);

        OnBossBattleStart?.Invoke();

        // 冻结：玩家不动/氧气不掉/相机聚焦
        dialogueManager?.BeginDialogueLock(threatFocusOrthographicSize, transform);

        if (threatLines != null && threatLines.Count > 0)
        {
            foreach (var line in threatLines)
            {
                SetText(line);
                yield return new WaitForSeconds(threatLineDuration);
            }
        }
        else
        {
            SetText("交出赎金，否则就撕票！");
            yield return new WaitForSeconds(1.2f);
        }

        dialogueManager?.EndDialogueLock();

        // 开始攻击循环（只要玩家仍在范围内）
        TryStartAttackLoop();
    }

    private void TryStartAttackLoop()
    {
        if (deathHandled) return;
        if (!playerInRange) return;

        if (attackCoroutine != null) return;
        attackCoroutine = StartCoroutine(AttackLoop());
    }

    private void StopAttackLoop()
    {
        if (attackCoroutine != null)
        {
            StopCoroutine(attackCoroutine);
            attackCoroutine = null;
        }
        SetIsAttacking(false);
    }

    private IEnumerator AttackLoop()
    {
        SetIsAttacking(true);
        if (canvasRoot != null) canvasRoot.SetActive(true);

        while (playerInRange && !deathHandled)
        {
            if (enemyHealth != null && !enemyHealth.enabled)
                yield break;

            if (playerTransform != null && bulletEmitter != null)
            {
                if (rotateToFacePlayer)
                    FacePlayerOnYAxis();

                if (useChargeWindup && chargeWindupSeconds > 0f)
                    yield return new WaitForSeconds(chargeWindupSeconds);

                // 从单发/扇形/四面八方里随机挑一个
                float roll = Random.value;
                if (roll < Mathf.Clamp01(radialBurstChance))
                {
                    bulletEmitter.ShootRadialBurst(radialBulletCount, radialRadius, Random.Range(0f, 360f));
                }
                else if (roll < Mathf.Clamp01(radialBurstChance) + Mathf.Clamp01(fanChance))
                {
                    float distToPlayer = Mathf.Max(3f, Vector3.Distance(Flatten(transform.position), Flatten(playerTransform.position)));
                    bulletEmitter.ShootFanTowardPlayer(playerTransform, fanBulletCount, fanSpreadDegrees, distToPlayer);
                }
                else
                {
                    bulletEmitter.ShootAtPlayer(playerTransform);
                }
            }

            yield return new WaitForSeconds(Mathf.Max(0.05f, fireInterval));
        }
    }

    private void SetIsAttacking(bool isAttacking)
    {
        if (animator == null) return;
        animator.SetBool(animBoolIsAttacking, isAttacking);
    }

    private void FacePlayerOnYAxis()
    {
        if (playerTransform == null) return;
        Vector3 to = playerTransform.position - transform.position;
        to.y = 0f;
        if (to.sqrMagnitude < 0.0001f) return;

        // 显式只设置 Y 轴朝向，避免 LookRotation 带来不期望的 pitch/roll
        float yaw = Quaternion.LookRotation(to).eulerAngles.y;
        transform.rotation = Quaternion.Euler(0f, yaw, 0f);
    }

    private static Vector3 Flatten(Vector3 v) => new Vector3(v.x, 0f, v.z);

    private void SetText(string s)
    {
        if (tmpText != null) tmpText.text = s;
        if (uiText != null) uiText.text = s;
    }

    private void Update()
    {
        if (deathHandled) return;
        if (enemyHealth == null) return;

        if (useDistanceDetection)
        {
            if (playerTransform == null)
                playerTransform = GameObject.FindGameObjectWithTag(playerTag)?.transform;

            if (playerTransform != null)
            {
                float d = FlatDistance(transform.position, playerTransform.position);

                if (!playerInRange && d <= playerDetectDistance)
                {
                    playerInRange = true;

                    if (!threatPlayed)
                    {
                        if (threatCoroutine != null) StopCoroutine(threatCoroutine);
                        threatCoroutine = StartCoroutine(ThreatThenStartAttack());
                    }
                    else
                    {
                        TryStartAttackLoop();
                    }
                }
                else if (playerInRange && d > playerExitDistance)
                {
                    playerInRange = false;
                    StopAttackLoop();
                }
            }
        }

        // EnemyHealth 在 Die() 时会 enabled=false，因此可用来判断“死亡已开始”
        if (!enemyHealth.enabled)
        {
            deathHandled = true;
            StopAttackLoop();

            if (threatCoroutine != null) StopCoroutine(threatCoroutine);
            StartCoroutine(DeathThenNotifyRoutine());
        }
    }

    private static float FlatDistance(Vector3 a, Vector3 b)
    {
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b);
    }

    private IEnumerator DeathThenNotifyRoutine()
    {
        if (canvasRoot != null) canvasRoot.SetActive(true);

        // 冻结：避免玩家趁死亡阶段乱走、且对话期间不扣氧气
        dialogueManager?.BeginDialogueLock(deathFocusOrthographicSize, transform);

        if (deathLines != null && deathLines.Count > 0)
        {
            foreach (var line in deathLines)
            {
                SetText(line);
                yield return new WaitForSeconds(deathLineDuration);
            }
        }
        else
        {
            SetText("……我不会输。");
            yield return new WaitForSeconds(1.2f);
        }

        dialogueManager?.EndDialogueLock();

        OnBossDefeated?.Invoke();
    }
}

