using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using System;

[DisallowMultipleComponent]
public class UIFloatingButtonItem : MonoBehaviour
{
    [Header("引用（可不填，会自动尝试获取）")]
    [SerializeField] private RectTransform root;
    [SerializeField] private RectTransform visual;
    [SerializeField] private Image dimImage;
    [SerializeField] private Button button;

    [Header("对应 Canvas 显隐（选中显示，未选中隐藏）")]
    [SerializeField] private GameObject canvasObject;

    private bool isInvokingButtonClick;

    private Vector2 baseVisualAnchoredPos;
    private Vector3 baseVisualScale;
    private Color baseDimColor;

    private Tween posTween;
    private Tween scaleTween;
    private Tween colorTween;
    private Sequence mainSeq;
    private Tween punchTween;

    private UIFloatingButtonGroup group;
    private int indexInGroup;
    [Header("相机切换")]
    public Transform CameraTarget;


    public Transform GetCameraTarget()
    {
        return CameraTarget;
    }
    private void Awake()
    {
        if (root == null) root = transform as RectTransform;
        if (button == null) button = GetComponent<Button>();

        if (visual == null)
        {
            // 默认用第一个子物体做视觉层，避免 Layout 控制 root 的 anchoredPosition
            if (transform.childCount > 0) visual = transform.GetChild(0) as RectTransform;
            else visual = root;
        }

        if (dimImage == null && visual != null)
            dimImage = visual.GetComponentInChildren<Image>(true);

        CacheBaseState();
    }

    private void OnDisable()
    {
        KillTweens();
    }

    public void Bind(UIFloatingButtonGroup owner, int index)
    {
        group = owner;
        indexInGroup = index;

        if (button != null)
        {
            button.onClick.RemoveListener(OnClick);
            button.onClick.AddListener(OnClick);
        }
    }

    private void OnClick()
    {
        if (group != null) group.ToggleSelectButton(indexInGroup);
    }

    public void ApplyImmediate(UIFloatingButtonGroup.AnimConfig cfg, bool isSelected, bool anySelected)
    {
        if (visual == null) return;

        KillTweens();
        ApplyCanvasActiveState(isSelected);

        float offsetY = 0f;
        float scale = 1f;
        float dim = 1f;

        if (anySelected)
        {
            if (isSelected)
            {
                offsetY = cfg.selectedOffsetY;
                scale = cfg.selectedScale;
                dim = 1f;
            }
            else
            {
                offsetY = cfg.othersOffsetY;
                scale = 1f;
                dim = cfg.dimMultiplier;
            }
        }

        visual.anchoredPosition = baseVisualAnchoredPos + new Vector2(0f, offsetY);
        visual.localScale = baseVisualScale * scale;
        if (dimImage != null) dimImage.color = MultiplyRGB(baseDimColor, dim);
    }

    public void Play(UIFloatingButtonGroup.AnimConfig cfg, bool isSelected, bool anySelected)
    {
        if (visual == null) return;

        ApplyCanvasActiveState(isSelected);

        float offsetY = 0f;
        float scale = 1f;
        float dim = 1f;

        if (anySelected)
        {
            if (isSelected)
            {
                offsetY = cfg.selectedOffsetY;
                scale = cfg.selectedScale;
                dim = 1f;
            }
            else
            {
                offsetY = cfg.othersOffsetY;
                scale = 1f;
                dim = cfg.dimMultiplier;
            }
        }

        Vector2 targetPos = baseVisualAnchoredPos + new Vector2(0f, offsetY);
        Vector3 targetScale = baseVisualScale * scale;
        Color targetColor = dimImage != null ? MultiplyRGB(baseDimColor, dim) : default;

        KillTweens();

        mainSeq = DOTween.Sequence().SetUpdate(true);

        if (!cfg.useJuicyMotion)
        {
            posTween = visual.DOAnchorPos(targetPos, cfg.duration).SetEase(cfg.ease);
            scaleTween = visual.DOScale(targetScale, cfg.duration).SetEase(cfg.ease);
            mainSeq.Join(posTween);
            mainSeq.Join(scaleTween);

            if (dimImage != null)
            {
                colorTween = dimImage.DOColor(targetColor, cfg.duration).SetEase(cfg.ease);
                mainSeq.Join(colorTween);
            }
            return;
        }

        Vector2 startPos = visual.anchoredPosition;
        float dy = targetPos.y - startPos.y;
        bool movingUp = dy > 0.01f;
        bool movingDown = dy < -0.01f;

        // 速度感：按距离估算一个下限时长（避免小距离太慢/大距离太快）
        float dist = Mathf.Abs(dy);
        float speedDur = (cfg.speedBasedUnitsPerSecond > 1f) ? (dist / cfg.speedBasedUnitsPerSecond) : 0f;
        speedDur = Mathf.Max(cfg.speedBasedMinDuration, speedDur);

        float mainDur = movingUp ? cfg.upDuration : (movingDown ? cfg.downDuration : cfg.duration);
        Ease mainEase = movingUp ? cfg.upEase : (movingDown ? cfg.downEase : cfg.ease);
        mainDur = Mathf.Max(mainDur, speedDur);

        // 位移：主段到 overshoot，再 settle 回目标（更有冲劲/重量）
        float overshootY = cfg.overshootY;
        Vector2 midPos = targetPos;
        if (movingUp) midPos = targetPos + new Vector2(0f, overshootY);
        else if (movingDown) midPos = targetPos - new Vector2(0f, overshootY);

        posTween = DOTween.Sequence()
            .Append(visual.DOAnchorPos(midPos, mainDur).SetEase(mainEase))
            .Append(visual.DOAnchorPos(targetPos, cfg.settleDuration).SetEase(cfg.settleEase))
            .SetUpdate(true);
        mainSeq.Join(posTween);

        // 缩放：略微滞后 + 小幅超调，更“弹”
        float sOver = Mathf.Max(0f, cfg.scaleOvershoot);
        Vector3 midScale = targetScale * (1f + (movingUp ? sOver : (movingDown ? -sOver * 0.35f : 0f)));
        scaleTween = DOTween.Sequence()
            .AppendInterval(Mathf.Max(0f, cfg.scaleDelay))
            .Append(visual.DOScale(midScale, mainDur * 0.85f).SetEase(movingUp ? Ease.OutBack : Ease.OutCubic))
            .Append(visual.DOScale(targetScale, cfg.settleDuration).SetEase(cfg.settleEase))
            .SetUpdate(true);
        mainSeq.Join(scaleTween);

        // 颜色：单独节奏（稍快一点）
        if (dimImage != null)
        {
            float cDur = Mathf.Max(0.01f, mainDur * Mathf.Max(0.1f, cfg.colorDurationMultiplier));
            colorTween = DOTween.Sequence()
                .AppendInterval(Mathf.Max(0f, cfg.colorDelay))
                .Append(dimImage.DOColor(targetColor, cDur).SetEase(Ease.OutCubic))
                .SetUpdate(true);
            mainSeq.Join(colorTween);
        }
    }

    public void Play(UIFloatingButtonGroup.AnimConfig cfg, bool isSelected, bool anySelected, Action onComplete)
    {
        if (visual == null) return;

        Vector2 before = visual.anchoredPosition;
        Play(cfg, isSelected, anySelected);

        if (mainSeq != null && onComplete != null)
        {
            mainSeq.OnComplete(() =>
            {
                // 防止对象被禁用/销毁后回调引发异常
                if (this == null || !isActiveAndEnabled) return;
                onComplete.Invoke();
            });
        }
    }

    public bool IsMovingDownTo(Vector2 targetPos, float minDeltaY)
    {
        if (visual == null) return false;
        float dy = targetPos.y - visual.anchoredPosition.y;
        return dy < -Mathf.Abs(minDeltaY);
    }

    public Vector2 GetTargetAnchoredPos(UIFloatingButtonGroup.AnimConfig cfg, bool isSelected, bool anySelected)
    {
        float offsetY = 0f;
        if (anySelected)
            offsetY = isSelected ? cfg.selectedOffsetY : cfg.othersOffsetY;
        return baseVisualAnchoredPos + new Vector2(0f, offsetY);
    }

    public void PunchHorizontal(float strengthX, float duration, int vibrato, float elasticity)
    {
        if (visual == null) return;
        if (duration <= 0f || Mathf.Approximately(strengthX, 0f)) return;

        punchTween?.Kill();
        punchTween = visual.DOPunchAnchorPos(new Vector2(strengthX, 0f), duration, vibrato, elasticity)
            .SetUpdate(true);
    }

    public void PunchVertical(float strengthY, float duration, int vibrato, float elasticity)
    {
        if (visual == null) return;
        if (duration <= 0f || Mathf.Approximately(strengthY, 0f)) return;

        punchTween?.Kill();
        punchTween = visual.DOPunchAnchorPos(new Vector2(0f, strengthY), duration, vibrato, elasticity)
            .SetUpdate(true);
    }

    private void CacheBaseState()
    {
        if (visual != null)
        {
            baseVisualAnchoredPos = visual.anchoredPosition;
            baseVisualScale = visual.localScale;
        }
        else
        {
            baseVisualAnchoredPos = Vector2.zero;
            baseVisualScale = Vector3.one;
        }

        baseDimColor = dimImage != null ? dimImage.color : Color.white;
    }

    private void KillTweens()
    {
        mainSeq?.Kill();
        posTween?.Kill();
        scaleTween?.Kill();
        colorTween?.Kill();
        punchTween?.Kill();
        mainSeq = null;
        posTween = null;
        scaleTween = null;
        colorTween = null;
        punchTween = null;
    }

    private void ApplyCanvasActiveState(bool isSelected)
    {
        if (canvasObject == null) return;
        bool isActivate = isSelected;
        if (canvasObject.activeSelf != isActivate)
        {
            canvasObject.SetActive(isActivate);
        }
    }

    private static Color MultiplyRGB(Color c, float m)
    {
        return new Color(c.r * m, c.g * m, c.b * m, c.a);
    }

    public void ItemButtonEvent()
    {
        InvokeButtonClick();
    }

    /// <summary>
    /// 用于“键盘触发/外部触发”时，模拟一次按钮点击。
    /// 会触发 Button 的所有 onClick 监听（包括本脚本 Bind 的选择逻辑）。
    /// 内置防重入，避免把 ItemButtonEvent 挂到 onClick 后造成递归。
    /// </summary>
    public void InvokeButtonClick()
    {
        if (button == null) return;
        if (isInvokingButtonClick) return;

        try
        {
            isInvokingButtonClick = true;
            button.onClick.Invoke();
        }
        finally
        {
            isInvokingButtonClick = false;
        }
    }

    public void ShowRestaurantUI()
    {
        if (ShopInteraction.Instance.isUIShowing)
        {
            ShopInteraction.Instance.HideShopUI();
        }
        else
        {
            ShopInteraction.Instance.ShowShopUI();
        }

    }

}

