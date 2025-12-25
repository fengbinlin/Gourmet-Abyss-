using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using DG.Tweening;

[System.Serializable]
public class PrerequisiteData
{
    public SkillNode node;  // 前置节点
    public int requiredLevel = 1;  // 需要的前置节点等级
}

[System.Serializable]
public class SkillNodeData
{
    public string skillID;
    public string skillName;
    [TextArea(3, 5)]
    public string description;
    public Sprite icon;

    public ResourceType costType = ResourceType.Money;
    public int costAmount = 100;

    public int maxLevel = 1;
    public int currentLevel = 0;
    public bool isLearned = false;

    [Header("技能效果（可选）")]
    public UnityEvent onSkillLearned;

    [Header("技能属性加成")]
    public float attackMultiplier = 1f;
    public float defenseMultiplier = 1f;
    public float speedMultiplier = 1f;
    public bool isRare = false;
    public bool CanLearn()
    {
        return !isLearned && currentLevel < maxLevel;
    }

    public void Learn()
    {
        if (currentLevel < maxLevel)
        {
            currentLevel++;
            if (currentLevel == maxLevel)
            {
                isLearned = true;
            }
            onSkillLearned?.Invoke();
        }
    }

    public SkillNodeData Clone()
    {
        return new SkillNodeData
        {
            skillID = this.skillID,
            skillName = this.skillName,
            description = this.description,
            icon = this.icon,
            costType = this.costType,
            costAmount = this.costAmount,
            maxLevel = this.maxLevel,
            currentLevel = this.currentLevel,
            isLearned = this.isLearned,
            attackMultiplier = this.attackMultiplier,
            defenseMultiplier = this.defenseMultiplier,
            speedMultiplier = this.speedMultiplier
        };
    }
}

public enum SkillNodeState
{
    Locked,     // 锁定（前置技能未满足条件）
    Unlocked,   // 解锁但未学习
    Learned     // 已学习
}

// UI信息面板组件


public class SkillNode : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("技能数据")]
    public SkillNodeData skillData;

    [Header("前置条件")]
    public List<PrerequisiteData> prerequisites = new List<PrerequisiteData>(); // 前置节点及其等级要求

    [Header("连线点")]
    public Transform[] connectionPoints;

    [Header("UI引用")]
    public Image iconImage;
    public Image frameImage;
    public Text levelText;
    public GameObject lockIcon;
    public GameObject unlockIcon;
    public Button learnButton;
    public SkillNodeInfoPanel infoPanel; // 新增：信息面板引用

    [Header("颜色配置")]
    public Color lockedColor = Color.gray;
    public Color unlockedColor = Color.white;
    public Color learnedColor = Color.green;

    [Header("动态效果")]
    public float hoverScale = 1.2f;
    public float animationDuration = 0.3f;
    public float revealAnimationDuration = 0.5f;
    public float revealScale = 1.2f;
    public Ease revealEase = Ease.OutBack;

    [Header("旋转晃动效果")]
    public float rotationShakeStrength = 10f;  // 旋转晃动强度
    public int rotationShakeVibrato = 5;       // 旋转震动次数
    public float rotationShakeRandomness = 90f; // 旋转随机性

    [Header("信息面板设置")]
    public Vector2 infoPanelOffset = new Vector2(0, 120f); // 面板偏移位置
    public float infoPanelShowDelay = 0.1f; // 鼠标进入后显示面板的延迟
    public float infoPanelHideDelay = 0.1f; // 鼠标离开后隐藏面板的延迟

    [Header("事件")]
    public UnityEvent<SkillNode> onNodeClicked;
    public UnityEvent<SkillNode> onNodeLearned;

    private SkillNodeState currentState = SkillNodeState.Locked;
    private SkillTree skillTree;
    private CanvasGroup canvasGroup;
    private bool isVisible = false;
    private bool isRevealing = false;
    private Coroutine showInfoPanelCoroutine;
    private Coroutine hideInfoPanelCoroutine;
    private bool isMouseOver = false;

    public SkillNodeState State => currentState;
    public bool IsVisible => isVisible;
    public bool IsRevealing => isRevealing;

    private void Awake()
    {
        skillTree = GetComponentInParent<SkillTree>();

        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        if (learnButton != null)
        {
            learnButton.onClick.AddListener(OnLearnButtonClicked);
        }

        var button = GetComponent<Button>();
        if (button != null)
        {
            button.onClick.AddListener(() => onNodeClicked?.Invoke(this));
        }

        // 初始化信息面板
        if (infoPanel != null)
        {
            infoPanel.gameObject.SetActive(false);
        }
    }

    private void Start()
    {
        UpdateVisuals(true);
    }

    public void SetState(SkillNodeState newState, bool forceUpdate = false)
    {
        if (!forceUpdate && currentState == newState)
        {
            UpdateVisuals(false);
            return;
        }

        SkillNodeState previousState = currentState;
        currentState = newState;

        bool shouldPlayAnimation = (previousState == SkillNodeState.Locked && newState == SkillNodeState.Unlocked) ||
                                  (previousState == SkillNodeState.Unlocked && newState == SkillNodeState.Learned);

        UpdateVisuals(true);

        if (shouldPlayAnimation && isVisible)
        {
            transform.DOScale(1f, animationDuration)
                .From(1.2f)
                .SetEase(Ease.OutBack);
        }
    }

    public bool ArePrerequisitesMet()
    {
        if (prerequisites == null || prerequisites.Count == 0)
            return true;

        foreach (var prereq in prerequisites)
        {
            if (prereq.node == null)
                continue;

            if (prereq.node.skillData.currentLevel < prereq.requiredLevel)
                return false;
        }

        return true;
    }

    public void UpdateVisibility()
    {
        if (isRevealing) return;

        if (skillData.isLearned)
        {
            SetVisibility(true);
            return;
        }

        bool prerequisitesMet = ArePrerequisitesMet();
        SetVisibility(prerequisitesMet);
    }

    public void RevealWithAnimation()
    {
        if (isRevealing || isVisible) return;

        isRevealing = true;
        SetVisibility(true, true);

        Sequence revealSequence = DOTween.Sequence();

        revealSequence.Append(transform.DOScale(revealScale, revealAnimationDuration)
            .From(0f)
            .SetEase(revealEase));

        revealSequence.Join(transform.DOShakeRotation(
            revealAnimationDuration * 0.8f,
            new Vector3(0, 0, rotationShakeStrength),
            rotationShakeVibrato,
            rotationShakeRandomness,
            false,
            ShakeRandomnessMode.Harmonic
        ).SetEase(Ease.OutElastic));

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            revealSequence.Join(canvasGroup.DOFade(1f, revealAnimationDuration * 0.6f));
        }

        revealSequence.OnComplete(() =>
        {
            isRevealing = false;
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 1f;
                canvasGroup.interactable = true;
                canvasGroup.blocksRaycasts = true;
            }
        });

        PlayRevealSound();
    }

    public IEnumerator RevealWithAnimationCoroutine()
    {
        if (isRevealing || isVisible) yield break;

        isRevealing = true;
        SetVisibility(true, true);

        Sequence revealSequence = DOTween.Sequence();

        revealSequence.Append(transform.DOScale(revealScale, revealAnimationDuration)
            .From(0f)
            .SetEase(revealEase));

        revealSequence.Join(transform.DOShakeRotation(
            revealAnimationDuration * 0.8f,
            new Vector3(0, 0, rotationShakeStrength),
            rotationShakeVibrato,
            rotationShakeRandomness,
            false,
            ShakeRandomnessMode.Harmonic
        ).SetEase(Ease.OutElastic));

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            revealSequence.Join(canvasGroup.DOFade(1f, revealAnimationDuration * 0.6f));
        }

        yield return revealSequence.WaitForCompletion();

        isRevealing = false;
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 1f;
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;
        }

        transform.DOShakeScale(0.2f, 0.1f, 2, 90f, false);
    }

    private void PlayRevealSound()
    {
        // 在这里添加播放声音的代码
        // AudioManager.Instance.PlayOneShot("skill_unlock");
    }

    public void SetVisibility(bool visible, bool immediate = false)
    {
        isVisible = visible;

        if (canvasGroup != null)
        {
            if (visible)
            {
                if (immediate)
                {
                    canvasGroup.alpha = 1f;
                    canvasGroup.interactable = true;
                    canvasGroup.blocksRaycasts = true;
                }
                else
                {
                    canvasGroup.alpha = 0f;
                    canvasGroup.DOFade(1f, 0.3f);
                    canvasGroup.interactable = true;
                    canvasGroup.blocksRaycasts = true;
                }
            }
            else
            {
                if (immediate)
                {
                    canvasGroup.alpha = 0f;
                }
                else
                {
                    canvasGroup.DOFade(0f, 0.3f);
                }
                canvasGroup.interactable = false;
                canvasGroup.blocksRaycasts = false;
            }
        }

        if (visible && !gameObject.activeSelf)
        {
            gameObject.SetActive(true);
        }
        else if (!visible && gameObject.activeSelf)
        {
            if (immediate)
            {
                gameObject.SetActive(false);
            }
        }

        // 如果隐藏节点，也隐藏信息面板
        if (!visible && infoPanel != null)
        {
            HideInfoPanel();
        }
    }

    private void UpdateVisuals(bool forceUpdate = false)
    {
        if (skillData == null) return;

        if (iconImage != null && skillData.icon != null && (forceUpdate || iconImage.sprite != skillData.icon))
        {
            iconImage.sprite = skillData.icon;
        }

        string currentLevelText = $"Lv.{skillData.currentLevel}/{skillData.maxLevel}";
        if (levelText != null && levelText.text != currentLevelText)
        {
            levelText.text = currentLevelText;
        }

        // 根据状态设置颜色和图标
        switch (currentState)
        {
            case SkillNodeState.Locked:
                SetNodeColor(lockedColor);

                if (lockIcon != null && lockIcon.activeSelf != true)
                {
                    lockIcon.SetActive(true);
                }
                if (unlockIcon != null && unlockIcon.activeSelf != false)
                {
                    unlockIcon.SetActive(false);
                }
                if (learnButton != null && learnButton.interactable != false)
                {
                    learnButton.interactable = false;
                }
                break;

            case SkillNodeState.Unlocked:
                SetNodeColor(unlockedColor);

                bool resourceEnough = true;
                if (GameValManager.Instance != null)
                {
                    resourceEnough = GameValManager.Instance.HasEnoughResource(
                        skillData.costType,
                        skillData.costAmount
                    );
                }

                if (resourceEnough)
                {
                    // 资源足够：显示解锁图标
                    if (lockIcon != null) lockIcon.SetActive(false);
                    if (unlockIcon != null) unlockIcon.SetActive(true);
                }
                else
                {
                    // 资源不足：显示锁定图标
                    if (lockIcon != null) lockIcon.SetActive(true);
                    if (unlockIcon != null) unlockIcon.SetActive(false);
                }

                // 按资源状态决定学习按钮是否可点
                bool canLearn = resourceEnough && CanLearn();
                if (learnButton != null) learnButton.interactable = canLearn;
                break;

            case SkillNodeState.Learned:
                SetNodeColor(learnedColor);

                if (lockIcon != null && lockIcon.activeSelf != false)
                {
                    lockIcon.SetActive(false);
                }
                if (unlockIcon != null && unlockIcon.activeSelf != false)
                {
                    unlockIcon.SetActive(false);
                }
                if (learnButton != null && learnButton.interactable != false)
                {
                    learnButton.interactable = false;
                }
                break;
        }
    }

    private void SetNodeColor(Color color)
    {
        if (frameImage != null)
        {
            Color finalColor = isVisible ? color : color * 0.7f;
            finalColor.a = frameImage.color.a;
            frameImage.color = finalColor;
        }
    }

    public bool CanLearn()
    {
        if (!isVisible) return false;
        if (skillData.isLearned) return false;

        foreach (var prereq in prerequisites)
        {
            if (prereq.node == null || prereq.node.skillData.currentLevel < prereq.requiredLevel)
                return false;
        }

        if (GameValManager.Instance != null)
        {
            return GameValManager.Instance.HasEnoughResource(skillData.costType, skillData.costAmount);
        }

        return false;
    }

    public void TryLearn()
    {
        if (!isVisible)
        {
            Debug.LogWarning($"技能节点 {skillData.skillName} 不可见，无法学习");
            return;
        }

        if (CanLearn() && GameValManager.Instance.TryConsumeResource(skillData.costType, skillData.costAmount))
        {
            skillData.Learn();
            SetState(SkillNodeState.Learned, true);
            onNodeLearned?.Invoke(this);

            Sequence learnSequence = DOTween.Sequence();
            learnSequence.Append(transform.DOScale(1.2f, 0.1f).SetEase(Ease.OutCubic));
            learnSequence.Append(transform.DOShakeRotation(0.2f, new Vector3(0, 0, 15f), 3, 45f, false, ShakeRandomnessMode.Harmonic));
            learnSequence.Append(transform.DOScale(1f, 0.1f).SetEase(Ease.InCubic));

            if (skillTree != null)
            {
                skillTree.UpdateAllNodes();
            }

            // 学习后隐藏信息面板
            HideInfoPanel();
        }
    }

    public void UpdateAvailability(bool forceUpdate = false)
    {
        if (skillData.isLearned)
        {
            SetState(SkillNodeState.Learned, forceUpdate);
        }
        else
        {
            bool prerequisitesMet = ArePrerequisitesMet();
            SetState(prerequisitesMet ? SkillNodeState.Unlocked : SkillNodeState.Locked, forceUpdate);
        }
    }

    private void OnLearnButtonClicked()
    {
        TryLearn();
    }

    // 鼠标进入时显示信息面板
    public void OnPointerEnter(PointerEventData eventData)
    {
        isMouseOver = true;

        if (currentState != SkillNodeState.Locked && isVisible)
        {
            transform.DOScale(hoverScale, 0.2f).SetEase(Ease.OutCubic);
        }

        // 延迟显示信息面板
        if (showInfoPanelCoroutine != null)
        {
            StopCoroutine(showInfoPanelCoroutine);
        }

        if (hideInfoPanelCoroutine != null)
        {
            StopCoroutine(hideInfoPanelCoroutine);
        }

        showInfoPanelCoroutine = StartCoroutine(ShowInfoPanelDelayed());
    }

    // 鼠标离开时隐藏信息面板
    public void OnPointerExit(PointerEventData eventData)
    {
        isMouseOver = false;

        if (isVisible)
        {
            transform.DOScale(1f, 0.2f).SetEase(Ease.OutCubic);
        }

        // 延迟隐藏信息面板
        if (showInfoPanelCoroutine != null)
        {
            StopCoroutine(showInfoPanelCoroutine);
        }

        if (hideInfoPanelCoroutine != null)
        {
            StopCoroutine(hideInfoPanelCoroutine);
        }

        hideInfoPanelCoroutine = StartCoroutine(HideInfoPanelDelayed());
    }

    private IEnumerator ShowInfoPanelDelayed()
    {
        yield return new WaitForSeconds(infoPanelShowDelay);

        if (isMouseOver && infoPanel != null && isVisible)
        {
            ShowInfoPanel();
        }
    }

    private IEnumerator HideInfoPanelDelayed()
    {
        yield return new WaitForSeconds(infoPanelHideDelay);

        if (!isMouseOver && infoPanel != null)
        {
            HideInfoPanel();
        }
    }

    private void ShowInfoPanel()
    {
        if (infoPanel == null || skillData == null) return;

        // 更新面板信息
        infoPanel.UpdateInfo(skillData);

        // 计算面板位置（基于屏幕空间或UI空间）
        Vector2 panelPosition = CalculateInfoPanelPosition();
        infoPanel.SetPosition(panelPosition);

        // 显示面板
        infoPanel.Show();
    }

    private void HideInfoPanel()
    {
        if (infoPanel == null) return;

        infoPanel.Hide();
    }

    private Vector2 CalculateInfoPanelPosition()
    {
        if (infoPanel == null) return Vector2.zero;

        RectTransform nodeRect = GetComponent<RectTransform>();
        RectTransform panelRect = infoPanel.GetComponent<RectTransform>();
        RectTransform canvasRect = GetComponentInParent<Canvas>().GetComponent<RectTransform>();

        if (nodeRect == null || panelRect == null || canvasRect == null)
            return infoPanelOffset;

        // 1. 获取节点在世界空间中的位置
        Vector3 nodeWorldPosition = nodeRect.position;

        // 2. 计算面板的世界位置（添加偏移）
        Vector3 panelWorldPosition = nodeWorldPosition;

        // 将屏幕偏移转换为世界空间偏移
        // 这里假设Canvas的Scale是1:1，如果有缩放需要调整
        panelWorldPosition += new Vector3(infoPanelOffset.x, infoPanelOffset.y, 0) * canvasRect.localScale.x;

        // 3. 边界检查（世界空间）
        Vector3[] canvasCorners = new Vector3[4];
        canvasRect.GetWorldCorners(canvasCorners);

        Vector3[] panelCorners = new Vector3[4];
        panelRect.GetWorldCorners(panelCorners);

        // 计算面板的边界
        float panelMinX = panelCorners[0].x;
        float panelMaxX = panelCorners[2].x;
        float panelMinY = panelCorners[0].y;
        float panelMaxY = panelCorners[1].y;

        // 计算Canvas的边界
        float canvasMinX = canvasCorners[0].x;
        float canvasMaxX = canvasCorners[2].x;
        float canvasMinY = canvasCorners[0].y;
        float canvasMaxY = canvasCorners[2].y;

        // 计算调整量
        Vector3 adjustment = Vector3.zero;

        // 右边界检查
        if (panelMaxX > canvasMaxX)
        {
            adjustment.x = canvasMaxX - panelMaxX;
        }
        // 左边界检查
        else if (panelMinX < canvasMinX)
        {
            adjustment.x = canvasMinX - panelMinX;
        }

        // 上边界检查
        if (panelMaxY > canvasMaxY)
        {
            adjustment.y = canvasMaxY - panelMaxY;
        }
        // 下边界检查
        else if (panelMinY < canvasMinY)
        {
            adjustment.y = canvasMinY - panelMinY;
        }

        // 应用调整
        panelWorldPosition += adjustment;

        return panelWorldPosition;
    }

    public void ResetNode()
    {
        skillData.currentLevel = 0;
        skillData.isLearned = false;
        SetState(SkillNodeState.Locked, true);
        SetVisibility(false, true);

        // 重置时隐藏信息面板
        if (infoPanel != null)
        {
            infoPanel.Hide();
        }
    }

    public void ForceShow()
    {
        SetVisibility(true);
    }

    private void OnDisable()
    {
        // 对象被禁用时隐藏信息面板
        HideInfoPanel();
    }
}