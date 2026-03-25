using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// 姐姐剧情逻辑：
/// Boss 死亡后出现，对玩家说感谢话，并在对话结束后让姐姐与幼崽消失。
/// </summary>
public class SisterStoryController : MonoBehaviour
{
    [Header("剧情引用")]
    [SerializeField] private BossStoryController bossStory;
    [SerializeField] private CarrotCubStory carrotCubStory;

    [Header("姐姐对象")]
    [SerializeField] private GameObject sisterRoot;
    [SerializeField] private Animator animator;

    [Header("姐姐 UI（Canvas->Text/TMP）")]
    [SerializeField] private GameObject canvasRoot;
    [SerializeField] private TMP_Text tmpText;
    [SerializeField] private UnityEngine.UI.Text uiText;
    [SerializeField] private bool hideCanvasOnStart = true;

    [Header("感谢对话")]
    [SerializeField] private List<string> thankLines = new List<string>
    {
        "感谢你救了我。",
        "以后我会在地牢中助你一臂之力。"
    };
    [SerializeField] private float lineDuration = 1.2f;
    [SerializeField] private float focusOrthographicSize = 3.0f;

    [Header("开战对话（BOSS进入战斗模式时）")]
    [SerializeField] private List<string> battleStartLines = new List<string>
    {
        "救救我！",
        "拜托了……别让他再伤害我！"
    };
    [SerializeField] private float battleStartLineDuration = 1.0f;
    [SerializeField] private float battleStartFocusOrthographicSize = 3.0f;
    [SerializeField] private bool battleStartUsesDialogueLock = true;

    [Header("激活后持续求救（不断说）")]
    [SerializeField] private bool continuousCryingAfterActivation = true;
    [SerializeField] private float cryingInterval = 1.6f;
    [SerializeField] private bool cryingUsesDialogueLock = false;
    [SerializeField] private float cryingFocusOrthographicSize = 3.0f;

    [Header("感谢语句时序")]
    [SerializeField] private float thanksStartDelayAfterBossDeath = 0.05f;

    [Header("感谢后恢复宠物")]
    [SerializeField] private bool enablePetAfterThanks = true;
    [SerializeField] private PetType petTypeToEnableAfterThanks = PetType.FlyingCompanion;

    [Header("对话后处理")]
    [SerializeField] private bool disappearSisterAfterDialogue = true;
    [SerializeField] private bool disappearCubAfterDialogue = true;

    public StoryDialogueManager dialogueManager;
    private Coroutine routine;
    private Coroutine battleStartRoutine;
    private bool battleStartPlayed;
    private bool bossDefeated;
    private Coroutine cryRoutine;
    private bool cryingLockActive;

    private void OnEnable()
    {
        // 当姐姐被 setActive(true 后：直接开始持续求救，直到 Boss 死亡。
        if (!continuousCryingAfterActivation) return;
        if (bossDefeated) return;
        if (cryRoutine != null) return;

        if (canvasRoot != null) canvasRoot.SetActive(true);
        cryRoutine = StartCoroutine(CryingLoopRoutine());
    }

    private void Awake()
    {
        //dialogueManager = StoryDialogueManager.Instance;
        if (sisterRoot == null) sisterRoot = gameObject;
    }

    private void Start()
    {
        // 如果你希望“激活后立即持续求救”，则不要把 canvas 在 Start 里隐藏掉。
        if (hideCanvasOnStart && canvasRoot != null && !continuousCryingAfterActivation)
            canvasRoot.SetActive(false);

        // 若已通关：本关不再激活姐姐剧情逻辑（由 CarrotCubStory 统一关掉根物体）
        if (StoryDialogueManager.Instance != null && StoryDialogueManager.Instance.IsStoryClearedThisScene)
        {
            if (sisterRoot != null) sisterRoot.SetActive(false);
            if (canvasRoot != null) canvasRoot.SetActive(false);
            gameObject.SetActive(false);
            return;
        }

        if (bossStory != null)
        {
            bossStory.OnBossDefeated += HandleBossDefeated;
            bossStory.OnBossBattleStart += HandleBossBattleStart;
        }
    }

    private void OnDestroy()
    {
        if (bossStory != null)
        {
            bossStory.OnBossDefeated -= HandleBossDefeated;
            bossStory.OnBossBattleStart -= HandleBossBattleStart;
        }
    }

    private void HandleBossDefeated()
    {
        bossDefeated = true;

        // 先确保持续求救不会抢占文本/锁，保证“遗言说完后才开始感谢”。
        if (cryingLockActive)
        {
            dialogueManager?.EndDialogueLock();
            cryingLockActive = false;
        }

        if (cryRoutine != null)
        {
            StopCoroutine(cryRoutine);
            cryRoutine = null;
        }

        if (routine != null) StopCoroutine(routine);
        routine = StartCoroutine(PlayThanksThenDisappearAfterDelayRoutine());
    }

    private IEnumerator PlayThanksThenDisappearAfterDelayRoutine()
    {
        if (thanksStartDelayAfterBossDeath > 0f)
            yield return new WaitForSeconds(thanksStartDelayAfterBossDeath);

        yield return StartCoroutine(PlayThanksThenDisappearRoutine());
    }

    private void OnDisable()
    {
        // 若持续求救期间用了 DialogueLock，协程被 Disable 时可能来不及 End，这里兜底修复。
        if (cryingLockActive)
        {
            dialogueManager?.EndDialogueLock();
            cryingLockActive = false;
        }
    }

    private void HandleBossBattleStart()
    {
        // 你现在的需求是：不要等开战才说，姐姐一生成就一直说。
        if (continuousCryingAfterActivation) return;

        if (battleStartPlayed) return;
        battleStartPlayed = true;

        bossDefeated = false;

        if (battleStartRoutine != null) StopCoroutine(battleStartRoutine);
        battleStartRoutine = StartCoroutine(PlayBattleStartRoutine());
    }

    private IEnumerator PlayBattleStartRoutine()
    {
        if (sisterRoot != null) sisterRoot.SetActive(true);
        if (canvasRoot != null) canvasRoot.SetActive(true);

        if (battleStartUsesDialogueLock)
            dialogueManager?.BeginDialogueLock(battleStartFocusOrthographicSize,
                sisterRoot != null ? sisterRoot.transform : transform);

        if (battleStartLines != null && battleStartLines.Count > 0)
        {
            foreach (var line in battleStartLines)
            {
                SetText(line);
                yield return new WaitForSeconds(battleStartLineDuration);
            }
        }
        else
        {
            SetText("救救我！");
            yield return new WaitForSeconds(1.0f);
        }

        SetText("");

        if (battleStartUsesDialogueLock)
            dialogueManager?.EndDialogueLock();

        // 如果你没用“激活后持续求救”，才在这里额外启动循环。
        if (continuousCryingAfterActivation == false && cryRoutine == null && !bossDefeated)
            cryRoutine = StartCoroutine(CryingLoopRoutine());
    }

    private IEnumerator CryingLoopRoutine()
    {
        if (canvasRoot != null) canvasRoot.SetActive(true);
        while (!bossDefeated)
        {
            if (battleStartLines != null && battleStartLines.Count > 0)
            {
                int idx = Random.Range(0, battleStartLines.Count);
                string line = battleStartLines[idx];

                if (cryingUsesDialogueLock)
                {
                    cryingLockActive = true;
                    dialogueManager?.BeginDialogueLock(cryingFocusOrthographicSize,
                        sisterRoot != null ? sisterRoot.transform : transform);
                }

                SetText(line);

                if (cryingUsesDialogueLock)
                {
                    yield return new WaitForSeconds(Mathf.Max(0.5f, cryingInterval * 0.75f));
                    dialogueManager?.EndDialogueLock();
                    cryingLockActive = false;
                }
                else
                {
                    // 不锁定玩家：只做“持续求救字幕”
                    SetText(line);
                    yield return new WaitForSeconds(Mathf.Max(0.1f, cryingInterval));
                    continue;
                }
            }
            else
            {
                SetText("救救我！");
                yield return new WaitForSeconds(Mathf.Max(0.1f, cryingInterval));
            }

            yield return new WaitForSeconds(Mathf.Max(0.1f, cryingInterval));
        }

        SetText("");
        cryingLockActive = false;
        cryRoutine = null;
    }

    private IEnumerator PlayThanksThenDisappearRoutine()
    {
        if (sisterRoot != null) sisterRoot.SetActive(true);
        if (canvasRoot != null) canvasRoot.SetActive(true);

        // 播放 idle（如果 Animator 里有 idle 状态）
        if (animator != null && animator.isActiveAndEnabled)
        {
            // 不强制触发具体名字：直接保持当前状态即可
        }

        // 延长 Boss 死亡后销毁时间：保证镜头复原与“感谢对话”结束后才消失。
        float thanksDurationSeconds = 0.1f;
        if (thankLines != null && thankLines.Count > 0)
            thanksDurationSeconds = thankLines.Count * lineDuration;
        else
            thanksDurationSeconds = 1.1f;

        bossStory?.RequestDeathDestroyDelayForSisterThanks(thanksDurationSeconds);

        dialogueManager?.BeginDialogueLock(focusOrthographicSize,
            sisterRoot != null ? sisterRoot.transform : transform);

        if (thankLines != null && thankLines.Count > 0)
        {
            foreach (var line in thankLines)
            {
                SetText(line);
                yield return new WaitForSeconds(lineDuration);
            }
        }
        else
        {
            SetText("感谢你……");
            yield return new WaitForSeconds(1.1f);
        }

        dialogueManager?.EndDialogueLock();

        TryEnablePetAndSpawn();

        if (disappearCubAfterDialogue && carrotCubStory != null)
            carrotCubStory.Disappear();

        if (disappearSisterAfterDialogue && sisterRoot != null)
        {
            sisterRoot.SetActive(false);
            Destroy(sisterRoot);
        }
    }

    private void TryEnablePetAndSpawn()
    {
        if (!enablePetAfterThanks) return;
        if (petTypeToEnableAfterThanks == PetType.None) return;

        if (WeaponStatsManager.Instance == null) return;
        WeaponStatsManager.Instance.SetPetEnabled(petTypeToEnableAfterThanks, true);

        // 仍在地牢战斗状态才立刻生成（避免切场景时无意义生成）
        if (PlayerStateManager.instance == null) return;
        if (PlayerStateManager.instance.currentState != PlayerState.Battle) return;

        PetManager.Instance?.SyncPetsForBattleNow();

        // 写入本地通关存档：下次进入该关卡无需再触发剧情
        if (StoryDialogueManager.Instance != null)
            StoryDialogueManager.Instance.MarkStoryClearedThisScene();
    }

    private void SetText(string s)
    {
        if (tmpText != null) tmpText.text = s;
        if (uiText != null) uiText.text = s;
    }
}

