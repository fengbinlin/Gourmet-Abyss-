using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using DG.Tweening;
[RequireComponent(typeof(CanvasGroup))]
public class SkillNodeInfoPanel : MonoBehaviour
{
    [Header("UI引用")]
    public Text skillNameText;
    public Text descriptionText;
    public Text costText;
    public Image costIconImage;
    
    [System.Serializable]
    public class ResourceIconConfig
    {
        public ResourceType resourceType;
        public Sprite icon;
    }
    
    [SerializeField]
    public List<ResourceIconConfig> resourceIcons = new List<ResourceIconConfig>();

    [Header("动画设置")]
    public float fadeInDuration = 0.3f;
    public float fadeOutDuration = 0.2f;
    public float scaleInDuration = 0.3f;
    public float scaleOutDuration = 0.2f;
    public Vector2 startScale = new Vector2(0.8f, 0.8f);
    public Vector2 targetScale = Vector2.one;
    public Ease fadeInEase = Ease.OutBack;
    public Ease fadeOutEase = Ease.InBack;

    private CanvasGroup canvasGroup;
    private RectTransform rectTransform;
    private bool isShowing = false;
    private Sequence currentAnimation;
    
    // 资源图标查找字典
    private Dictionary<ResourceType, Sprite> resourceIconDictionary = new Dictionary<ResourceType, Sprite>();

    private void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        rectTransform = GetComponent<RectTransform>();

        // 初始化资源图标字典
        InitializeResourceIconDictionary();

        // 初始隐藏
        canvasGroup.alpha = 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
    }
    
   public void InitializeResourceIconDictionary()
    {
        resourceIconDictionary.Clear();
        foreach (var config in resourceIcons)
        {
            if (!resourceIconDictionary.ContainsKey(config.resourceType))
            {
                resourceIconDictionary.Add(config.resourceType, config.icon);
            }
        }
    }
    
    // 可以在运行时添加或更新资源图标
    public void AddResourceIcon(ResourceType resourceType, Sprite icon)
    {
        if (resourceIconDictionary.ContainsKey(resourceType))
        {
            resourceIconDictionary[resourceType] = icon;
        }
        else
        {
            resourceIconDictionary.Add(resourceType, icon);
        }
    }
    
    // 获取资源图标
    public Sprite GetResourceIcon(ResourceType resourceType)
    {
        if (resourceIconDictionary.ContainsKey(resourceType))
        {
            return resourceIconDictionary[resourceType];
        }
        
        // 如果没有找到对应的图标，返回null
        Debug.LogWarning($"未找到资源类型 {resourceType} 对应的图标");
        return null;
    }

    public void Show()
    {
        if (isShowing) return;
        isShowing = true;

        // 停止当前动画
        if (currentAnimation != null && currentAnimation.IsActive())
        {
            currentAnimation.Kill();
        }

        // 激活对象
        gameObject.SetActive(true);

        // 设置初始状态
        canvasGroup.alpha = 0f;
        rectTransform.localScale = startScale;

        // 创建显示动画序列
        currentAnimation = DOTween.Sequence();

        // 同时进行淡入和缩放动画
        currentAnimation.Join(canvasGroup.DOFade(1f, fadeInDuration).SetEase(Ease.OutCubic));
        currentAnimation.Join(rectTransform.DOScale(targetScale, scaleInDuration).SetEase(fadeInEase));

        currentAnimation.OnComplete(() =>
        {
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;
            currentAnimation = null;
        });
    }

    public void Hide()
    {
        if (!isShowing) return;
        isShowing = false;

        // 停止当前动画
        if (currentAnimation != null && currentAnimation.IsActive())
        {
            currentAnimation.Kill();
        }

        // 设置不可交互
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        // 创建隐藏动画序列
        currentAnimation = DOTween.Sequence();

        // 同时进行淡出和缩放动画
        currentAnimation.Join(canvasGroup.DOFade(0f, fadeOutDuration).SetEase(Ease.InCubic));
        currentAnimation.Join(rectTransform.DOScale(startScale, scaleOutDuration).SetEase(fadeOutEase));

        currentAnimation.OnComplete(() =>
        {
            gameObject.SetActive(false);
            currentAnimation = null;
        });
    }

    public void UpdateInfo(SkillNodeData skillData)
    {
        if (skillNameText != null)
            skillNameText.text = skillData.skillName;

        if (descriptionText != null)
            descriptionText.text = skillData.description;

        if (costText != null && costIconImage != null)
        {
            var cost = skillData.GetCurrentUpgradeCost();
            if (cost != null)
            {
                costText.text = cost.costAmount.ToString();
                
                // 根据资源类型设置图标
                costIconImage.sprite = GetResourceIcon(cost.costType);
                
                // 如果找不到图标，可以设置一个默认图标或隐藏图标
                if (costIconImage.sprite == null)
                {
                    Debug.LogWarning($"未找到资源类型 {cost.costType} 对应的图标，已隐藏图标");
                    costIconImage.gameObject.SetActive(false);
                }
                else
                {
                    costIconImage.gameObject.SetActive(true);
                }
            }
            else
            {
                costText.text = "MAX";
                costIconImage.gameObject.SetActive(false);
            }
        }
    }

    public void SetPosition(Vector2 position)
    {
        if (rectTransform == null)
        {
            rectTransform = GetComponent<RectTransform>();

            // 如果还是null，尝试添加组件
            if (rectTransform == null)
            {
                rectTransform = gameObject.AddComponent<RectTransform>();
            }
        }

        if (rectTransform != null)
        {
            rectTransform.anchoredPosition = position;
        }
    }
    
    // 编辑器方法：刷新资源图标字典
    [ContextMenu("Refresh Resource Icons")]
    public void RefreshResourceIcons()
    {
        InitializeResourceIconDictionary();
    }
}