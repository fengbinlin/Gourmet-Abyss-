using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using DG.Tweening;  // 需要安装 DOTween 插件

public class SkillTreeUIController : MonoBehaviour
{
    [Header("UI引用")]
    public SkillTree skillTree;
    public RectTransform skillTreePanel;
    public Text treeNameText;
    public Text pointsText;
    public GameObject skillInfoPanel;
    public Text skillNameText;
    public Text skillDescriptionText;
    public Text skillCostText;
    public Text skillEffectText;
    public Button learnButton;
    
    [Header("动画设置")]
    public float panelSlideDuration = 0.5f;
    public float infoPanelFadeDuration = 0.3f;
    
    private CanvasGroup skillTreeCanvasGroup;
    private Vector2 skillTreePanelPosition;
    private bool isPanelVisible = false;
    
    private SkillNode selectedSkillNode;
    
    private void Start()
    {
        skillTreeCanvasGroup = skillTreePanel.GetComponent<CanvasGroup>();
        if (skillTreeCanvasGroup == null)
        {
            skillTreeCanvasGroup = skillTreePanel.gameObject.AddComponent<CanvasGroup>();
        }
        
        skillTreePanelPosition = skillTreePanel.anchoredPosition;
        
        // 初始隐藏面板
        skillTreeCanvasGroup.alpha = 0;
        skillTreeCanvasGroup.interactable = false;
        skillTreeCanvasGroup.blocksRaycasts = false;
        
        if (treeNameText != null && skillTree != null)
        {
            treeNameText.text = skillTree.treeName;
        }
        
        if (skillInfoPanel != null)
        {
            skillInfoPanel.SetActive(false);
        }
        
        if (learnButton != null)
        {
            learnButton.onClick.AddListener(OnLearnButtonClick);
        }
        
        // 订阅技能树事件
        if (skillTree != null)
        {
            skillTree.onSkillTreeInitialized.AddListener(OnSkillTreeInitialized);
            skillTree.onSkillLearned.AddListener(OnSkillLearned);
        }
    }
    
    private void Update()
    {
        UpdatePointsDisplay();
        
        // 按快捷键打开/关闭技能树
        if (Input.GetKeyDown(KeyCode.K))
        {
            ToggleSkillTree();
        }
    }
    
    public void ToggleSkillTree()
    {
        isPanelVisible = !isPanelVisible;
        
        if (isPanelVisible)
        {
            ShowSkillTree();
        }
        else
        {
            HideSkillTree();
        }
    }
    
    public void ShowSkillTree()
    {
        skillTreeCanvasGroup.interactable = true;
        skillTreeCanvasGroup.blocksRaycasts = true;
        
        skillTreeCanvasGroup.DOFade(1, panelSlideDuration);
        skillTreePanel.DOAnchorPos(Vector2.zero, panelSlideDuration)
            .SetEase(Ease.OutBack);
        
        isPanelVisible = true;
    }
    
    public void HideSkillTree()
    {
        skillTreeCanvasGroup.interactable = false;
        skillTreeCanvasGroup.blocksRaycasts = false;
        
        skillTreeCanvasGroup.DOFade(0, panelSlideDuration);
        skillTreePanel.DOAnchorPos(skillTreePanelPosition, panelSlideDuration)
            .SetEase(Ease.InBack)
            .OnComplete(() => {
                HideSkillInfo();
            });
        
        isPanelVisible = false;
    }
    
    public void OnSkillNodeSelected(SkillNode node)
    {
        selectedSkillNode = node;
        ShowSkillInfo(node);
    }
    
    private void ShowSkillInfo(SkillNode node)
    {
        if (skillInfoPanel == null || node == null) return;
        
        skillInfoPanel.SetActive(true);
        skillInfoPanel.GetComponent<CanvasGroup>().alpha = 0;
        skillInfoPanel.GetComponent<CanvasGroup>().DOFade(1, infoPanelFadeDuration);
        
        var data = node.skillData;
        
        if (skillNameText != null)
            skillNameText.text = data.skillName;
        
        if (skillDescriptionText != null)
            skillDescriptionText.text = data.description;
        
        if (skillCostText != null)
        {
            string costTypeName = GetResourceName(data.costType);
            skillCostText.text = $"消耗: {costTypeName} x{data.costAmount}";
            
            // 如果资源不足，显示红色
            if (GameValManager.Instance != null && 
                !GameValManager.Instance.HasEnoughResource(data.costType, data.costAmount))
            {
                skillCostText.color = Color.red;
            }
            else
            {
                skillCostText.color = Color.white;
            }
        }
        
        if (skillEffectText != null)
        {
            string effects = "";
            if (data.attackMultiplier != 1f)
                effects += $"攻击力: {data.attackMultiplier}x\n";
            if (data.defenseMultiplier != 1f)
                effects += $"防御力: {data.defenseMultiplier}x\n";
            if (data.speedMultiplier != 1f)
                effects += $"速度: {data.speedMultiplier}x\n";
            
            skillEffectText.text = string.IsNullOrEmpty(effects) ? "无特殊效果" : effects;
        }
        
        if (learnButton != null)
        {
            learnButton.interactable = node.CanLearn();
            learnButton.GetComponentInChildren<Text>().text = 
                data.isLearned ? "已学习" : "学习技能";
        }
    }
    
    private void HideSkillInfo()
    {
        if (skillInfoPanel != null)
        {
            skillInfoPanel.SetActive(false);
        }
    }
    
    private void OnLearnButtonClick()
    {
        if (selectedSkillNode != null && selectedSkillNode.CanLearn())
        {
            selectedSkillNode.TryLearn();
            ShowSkillInfo(selectedSkillNode); // 刷新显示
        }
    }
    
    private void UpdatePointsDisplay()
    {
        if (pointsText != null && GameValManager.Instance != null)
        {
            string pointsInfo = "";
            foreach (ResourceType type in Enum.GetValues(typeof(ResourceType)))
            {
                int count = GameValManager.Instance.GetResourceCount(type);
                if (count > 0)
                {
                    pointsInfo += $"{GetResourceName(type)}: {count}\n";
                }
            }
            pointsText.text = pointsInfo;
        }
    }
    
    private string GetResourceName(ResourceType type)
    {
        return type switch
        {
            ResourceType.Money => "金币",
            ResourceType.watermelonJ => "西瓜汁",
            ResourceType.orangeJ => "橙汁",
            ResourceType.tomatoJ => "番茄汁",
            ResourceType.mushroom => "蘑菇",
            _ => type.ToString()
        };
    }
    
    private void OnSkillTreeInitialized()
    {
        Debug.Log("技能树初始化完成");
    }
    
    private void OnSkillLearned(SkillNode node)
    {
        Debug.Log($"技能已学习: {node.skillData.skillName}");
        
        // 显示学习成功的提示
        StartCoroutine(ShowLearnSuccessMessage(node.skillData.skillName));
    }
    
    private IEnumerator ShowLearnSuccessMessage(string skillName)
    {
        // 这里可以添加一个浮动提示
        yield break;
    }
}