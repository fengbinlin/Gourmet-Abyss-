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

    private void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        rectTransform = GetComponent<RectTransform>();

        // 初始隐藏
        canvasGroup.alpha = 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
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

        if (costText != null)
            costText.text = skillData.costAmount.ToString();

        // 可以根据costType设置不同的图标
        if (costIconImage != null)
        {
            // 这里需要根据你的资源系统来设置图标
            // 例如：costIconImage.sprite = ResourceManager.Instance.GetResourceIcon(skillData.costType);
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
}