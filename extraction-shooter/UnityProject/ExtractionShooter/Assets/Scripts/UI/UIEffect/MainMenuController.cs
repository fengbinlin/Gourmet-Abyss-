using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

[RequireComponent(typeof(VerticalLayoutGroup))]
public class MainMenuController : MonoBehaviour
{
    [System.Serializable]
    public class ButtonData
    {
        public Button button;
        public RectTransform rectTransform;
        public Text text;
        public Vector2 targetPosition;
        public Color normalColor = Color.white;
        public Color hoverColor = new Color(1.2f, 1.2f, 1.2f, 1f);

        [Header("图片显示设置")]
        public Image hoverImage; // 悬停时显示的图片
        [Range(0f, 1f)] public float imageFadeInDuration = 0.2f; // 图片淡入持续时间
        [Range(0f, 1f)] public float imageFadeOutDuration = 0.2f; // 图片淡出持续时间

        [HideInInspector] public bool isHovering = false;
        [HideInInspector] public Coroutine hoverCoroutine;
        [HideInInspector] public Quaternion targetRotation = Quaternion.identity;
        [HideInInspector] public Coroutine imageFadeCoroutine; // 图片淡入淡出协程

        // 新增：保存初始状态
        [HideInInspector] public Vector2 initialPosition;
        [HideInInspector] public Vector3 initialScale = Vector3.one;
        [HideInInspector] public Quaternion initialRotation = Quaternion.identity;
        [HideInInspector] public Color initialTextColor = Color.white;
        [HideInInspector] public Color initialNormalColor = Color.white;
        [HideInInspector] public Color initialImageColor = Color.white;
        public Vector2 fixedTargetPosition;
    }

    [Header("标题设置")]
    [SerializeField] private RectTransform titleRectTransform;
    [SerializeField] private Image titleImage;
    [SerializeField] private float titleMoveDuration = 0.6f;
    [SerializeField] private float titleMoveDistance = 300f;
    [SerializeField] private AnimationCurve titleMoveCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    [SerializeField] private float titleOvershootAmount = 50f;
    [SerializeField] private float titleOvershootDuration = 0.2f;

    [Header("提示文本")]
    [SerializeField] private Text hintText;
    [SerializeField] private float hintBlinkInterval = 0.8f;
    [SerializeField] private Color hintNormalColor = Color.white;
    [SerializeField] private Color hintBlinkColor = new Color(1f, 1f, 1f, 0.5f);

    [Header("入场动画设置")]
    [SerializeField] private float entryOffset = 500f;
    [SerializeField] private float entryAnimationDelay = 0.1f;
    [SerializeField] private float entrySlideDuration = 0.5f;
    [SerializeField] private float entryRotationDuration = 0.5f;
    [SerializeField] private float entryOvershootAmount = 30f;
    [SerializeField] private float entryOvershootDuration = 0.15f;

    [Header("悬停动画设置")]
    [SerializeField] private float hoverOffset = 25f;
    [SerializeField] private float hoverMoveDuration = 0.2f;
    [SerializeField] private float hoverRotationAngle = -5f;
    [SerializeField] private float hoverRotationDuration = 0.2f;
    [SerializeField] private float hoverOvershootAmount = 8f;
    [SerializeField] private float hoverOvershootDuration = 0.1f;

    [Header("动画曲线")]
    [SerializeField] private AnimationCurve slideCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    [SerializeField] private AnimationCurve rotationCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    [SerializeField] private AnimationCurve hoverMoveCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    [SerializeField] private AnimationCurve hoverRotationCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    [SerializeField]
    private AnimationCurve jellyCurve = new AnimationCurve(
        new Keyframe(0f, 0f),
        new Keyframe(0.3f, 1.2f),
        new Keyframe(0.5f, 0.9f),
        new Keyframe(0.7f, 1.05f),
        new Keyframe(1f, 1f)
    );
    [SerializeField]
    private AnimationCurve overshootCurve = new AnimationCurve(
        new Keyframe(0f, 0f),
        new Keyframe(0.7f, 1.1f),
        new Keyframe(1f, 1f)
    );

    [Header("按钮列表")]
    [SerializeField] private List<ButtonData> buttonDataList = new List<ButtonData>();

    private VerticalLayoutGroup layoutGroup;
    private Coroutine entryAnimationCoroutine;
    private Dictionary<Button, Vector2> originalPositions = new Dictionary<Button, Vector2>();
    private Vector2 titleOriginalPosition;
    private Vector2 titleTargetPosition;
    private Coroutine hintBlinkCoroutine;
    private bool inputEnabled = true;
    private bool animationStarted = false;
    public bool isFirst = true;
    public bool needClick = true;
    // 新增：标题初始状态
    private Vector2 titleInitialPosition;
    private Color titleInitialColor = Color.white;
    private void OnDisable()
    {
        if (entryAnimationCoroutine != null)
        {
            StopCoroutine(entryAnimationCoroutine);
            entryAnimationCoroutine = null;
        }

        foreach (var data in buttonDataList)
        {
            if (data.hoverCoroutine != null)
            {
                StopCoroutine(data.hoverCoroutine);
                data.hoverCoroutine = null;
            }
            if (data.imageFadeCoroutine != null)
            {
                StopCoroutine(data.imageFadeCoroutine);
                data.imageFadeCoroutine = null;
            }
        }
    }
    private void Awake()
    {
        isFirst=true;
        layoutGroup = GetComponent<VerticalLayoutGroup>();

        // 保存标题初始状态
        if (titleRectTransform != null)
        {
            titleInitialPosition = titleRectTransform.anchoredPosition;
        }
        if (titleImage != null)
        {
            titleInitialColor = titleImage.color;
        }
    }

    private void OnEnable()
    {
        //isFirst = true;
        if (isFirst && needClick)
        {
            InitializeTitle();
            SetupHintText();

            // 初始化按钮的初始状态
            InitializeButtonPositions();
            //isFirst = false;
        }
        else if (needClick)
        {
            InitializeButtonPositions();
            StartEntryAnimation();
        }
        if (!needClick)
        {
            InitializeButtonPositions();
            StartEntryAnimation();
            //isFirst = false;
        }
    }

    private void InitializeButtonPositions()
    {
        foreach (ButtonData data in buttonDataList)
        {
            if (data.button != null && data.rectTransform != null)
            {
                // 停止协程
                if (data.hoverCoroutine != null)
                {
                    StopCoroutine(data.hoverCoroutine);
                    data.hoverCoroutine = null;
                }
                if (data.imageFadeCoroutine != null)
                {
                    StopCoroutine(data.imageFadeCoroutine);
                    data.imageFadeCoroutine = null;
                }

                data.isHovering = false;
                data.targetRotation = Quaternion.identity;

                // 第一次时保存初始位置
                if (isFirst)
                {
                    Vector2 initPos = data.rectTransform.anchoredPosition;
                    data.initialPosition = initPos;
                    data.fixedTargetPosition = initPos;
                    data.initialScale = data.rectTransform.localScale;
                    data.initialRotation = data.rectTransform.localRotation;
                    if (data.text != null) data.initialTextColor = data.text.color;
                    data.initialNormalColor = data.normalColor;
                    if (data.hoverImage != null) data.initialImageColor = data.hoverImage.color;
                    
                }

                // 每次初始化都用第一次的目标
                data.targetPosition = data.fixedTargetPosition;

                // 重置外观状态
                ResetButtonToInitialState(data);

                // 从屏幕外左侧开始
                data.rectTransform.anchoredPosition = new Vector2(-entryOffset, data.fixedTargetPosition.y);
                data.rectTransform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                data.button.interactable = false;

                if (data.hoverImage != null)
                {
                    data.hoverImage.gameObject.SetActive(false);
                    data.hoverImage.color = data.initialImageColor;
                }

                AddHoverEvents(data.button, data);
            }
        }
    }

    /// <summary>
    /// 重置按钮到初始状态
    /// </summary>
    private void ResetButtonToInitialState(ButtonData data)
    {
        if (data.rectTransform != null)
        {
            // 重置位置、缩放、旋转
            data.rectTransform.anchoredPosition = data.initialPosition; // 这里保留初始视觉位置
            data.rectTransform.localScale = data.initialScale;
            data.rectTransform.localRotation = data.initialRotation;

            // 重置颜色
            if (data.text != null)
            {
                data.text.color = data.initialTextColor;
            }

            // 重置按钮颜色
            data.normalColor = data.initialNormalColor;

            // 🚀 改成用固定的目标位置，而不是 initialPosition
            data.targetPosition = data.fixedTargetPosition;

            // 重置交互状态
            if (data.button != null)
            {
                data.button.interactable = false;
            }

            // 重置图片
            if (data.hoverImage != null)
            {
                data.hoverImage.color = data.initialImageColor;
                data.hoverImage.gameObject.SetActive(false);
            }
        }
    }

    private void Update()
    {
        if (!animationStarted && inputEnabled)
        {
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) ||
                Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1))
            {
                OnInputDetected();
            }
        }
    }

    private void OnInputDetected()
    {
        if (!inputEnabled || animationStarted) return;

        inputEnabled = false;
        animationStarted = true;

        if (hintBlinkCoroutine != null)
        {
            StopCoroutine(hintBlinkCoroutine);
        }

        if (hintText != null)
        {
            hintText.gameObject.SetActive(false);
        }
        if (needClick)
        {
            StartEntryAnimation();
        }

    }

    private void SetupHintText()
    {
        if (hintText != null)
        {
            hintBlinkCoroutine = StartCoroutine(BlinkHintText());
        }
    }

    private IEnumerator BlinkHintText()
    {
        if (hintText == null) yield break;

        while (true)
        {
            float elapsedTime = 0f;
            while (elapsedTime < hintBlinkInterval)
            {
                elapsedTime += Time.deltaTime;
                float t = elapsedTime / hintBlinkInterval;
                hintText.color = Color.Lerp(hintNormalColor, hintBlinkColor, t);
                yield return null;
            }

            elapsedTime = 0f;
            while (elapsedTime < hintBlinkInterval)
            {
                elapsedTime += Time.deltaTime;
                float t = elapsedTime / hintBlinkInterval;
                hintText.color = Color.Lerp(hintBlinkColor, hintNormalColor, t);
                yield return null;
            }
        }
    }

    private void InitializeTitle()
    {
        if (titleRectTransform != null)
        {
            titleOriginalPosition = titleRectTransform.anchoredPosition;
            titleTargetPosition = new Vector2(
                titleOriginalPosition.x + titleMoveDistance,
                titleOriginalPosition.y
            );
        }
    }

    private void AddHoverEvents(Button button, ButtonData data)
    {
        EventTrigger trigger = button.gameObject.GetComponent<EventTrigger>();
        if (trigger == null) trigger = button.gameObject.AddComponent<EventTrigger>();

        EventTrigger.Entry enterEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
        enterEntry.callback.AddListener((eventData) => OnButtonHoverEnter(data));
        trigger.triggers.Add(enterEntry);

        EventTrigger.Entry exitEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
        exitEntry.callback.AddListener((eventData) => OnButtonHoverExit(data));
        trigger.triggers.Add(exitEntry);

        EventTrigger.Entry clickEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerClick };
        clickEntry.callback.AddListener((eventData) => OnButtonClick(data));
        trigger.triggers.Add(clickEntry);
    }

    public void StartEntryAnimation()
    {
        if (entryAnimationCoroutine != null)
        {
            StopCoroutine(entryAnimationCoroutine);
        }

        if (titleRectTransform != null && isFirst && !needClick)
        {
            StartCoroutine(AnimateTitleMove());
        }
        entryAnimationCoroutine = StartCoroutine(EntryAnimationRoutine());
        isFirst = false;

    }

    private IEnumerator AnimateTitleMove()
    {
        if (titleRectTransform == null) yield break;

        Vector2 startPos = titleOriginalPosition;
        float elapsedTime = 0f;
        Vector2 overshootPos = new Vector2(titleTargetPosition.x + titleOvershootAmount, startPos.y);

        while (elapsedTime < titleMoveDuration)
        {
            elapsedTime += Time.deltaTime;
            float t = titleMoveCurve.Evaluate(elapsedTime / titleMoveDuration);
            float newX = Mathf.Lerp(startPos.x, overshootPos.x, t);
            titleRectTransform.anchoredPosition = new Vector2(newX, startPos.y);
            yield return null;
        }

        elapsedTime = 0f;
        while (elapsedTime < titleOvershootDuration)
        {
            elapsedTime += Time.deltaTime;
            float t = overshootCurve.Evaluate(elapsedTime / titleOvershootDuration);
            float newX = Mathf.Lerp(overshootPos.x, titleTargetPosition.x, t);
            titleRectTransform.anchoredPosition = new Vector2(newX, startPos.y);
            yield return null;
        }

        titleRectTransform.anchoredPosition = titleTargetPosition;
    }

    private IEnumerator EntryAnimationRoutine()
    {
        yield return new WaitForSeconds(0.1f);

        foreach (ButtonData data in buttonDataList)
        {
            StartCoroutine(AnimateButtonEntry(data));
            yield return new WaitForSeconds(entryAnimationDelay);
        }
    }

    private IEnumerator AnimateButtonEntry(ButtonData data)
    {
        if (data.rectTransform == null) yield break;

        RectTransform rt = data.rectTransform;
        Vector2 startPos = rt.anchoredPosition;
        Vector2 targetPos = data.targetPosition;
        Vector2 overshootPos = new Vector2(targetPos.x + entryOvershootAmount, targetPos.y);

        Quaternion startRotation = Quaternion.Euler(85f, 0f, 0f);
        Quaternion targetRotation = Quaternion.identity;
        data.targetRotation = targetRotation;

        float slideTime = 0f;
        float rotationTime = 0f;

        while (slideTime < entrySlideDuration || rotationTime < entryRotationDuration)
        {
            if (slideTime < entrySlideDuration)
            {
                slideTime += Time.deltaTime;
                float t = slideCurve.Evaluate(slideTime / entrySlideDuration);
                float newX = Mathf.Lerp(startPos.x, overshootPos.x, t);
                rt.anchoredPosition = new Vector2(newX, startPos.y);
            }

            if (rotationTime < entryRotationDuration)
            {
                rotationTime += Time.deltaTime;
                float t = rotationCurve.Evaluate(rotationTime / entryRotationDuration);
                rt.localRotation = Quaternion.Lerp(startRotation, targetRotation, t);
            }

            yield return null;
        }

        slideTime = 0f;
        while (slideTime < entryOvershootDuration)
        {
            slideTime += Time.deltaTime;
            float t = overshootCurve.Evaluate(slideTime / entryOvershootDuration);
            float newX = Mathf.Lerp(overshootPos.x, targetPos.x, t);
            rt.anchoredPosition = new Vector2(newX, startPos.y);
            yield return null;
        }

        rt.anchoredPosition = targetPos;
        rt.localRotation = targetRotation;

        float elapsed = 0f;
        Vector3 baseScale = Vector3.one;
        float jellyDuration = 0.4f;
        while (elapsed < jellyDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / jellyDuration;
            float scaleFactor = jellyCurve.Evaluate(t);
            rt.localScale = baseScale * scaleFactor;
            yield return null;
        }
        rt.localScale = baseScale;

        // if (originalPositions.ContainsKey(data.button))
        // {
        //     originalPositions[data.button] = targetPos;
        // }

        if (data.button != null)
        {
            data.button.interactable = true;
        }
    }

    private void OnButtonHoverEnter(ButtonData data)
    {
        if (data.button != null && !data.button.interactable) return;

        data.isHovering = true;
        if (data.hoverCoroutine != null) StopCoroutine(data.hoverCoroutine);
        data.hoverCoroutine = StartCoroutine(HoverEnterAnimationRoutine(data));

        ShowButtonImage(data, true);
    }

    private void OnButtonHoverExit(ButtonData data)
    {
        if (!data.isHovering) return;

        data.isHovering = false;
        if (data.hoverCoroutine != null) StopCoroutine(data.hoverCoroutine);
        data.hoverCoroutine = StartCoroutine(HoverExitAnimationRoutine(data));

        ShowButtonImage(data, false);
    }

    private void ShowButtonImage(ButtonData data, bool show)
    {
        if (data.hoverImage == null)
        {
            Debug.LogWarning($"按钮 {data.button?.name} 没有设置 hoverImage");
            return;
        }

        Debug.Log($"{(show ? "显示" : "隐藏")}按钮 {data.button?.name} 的图片");

        if (data.imageFadeCoroutine != null)
        {
            StopCoroutine(data.imageFadeCoroutine);
        }

        data.imageFadeCoroutine = StartCoroutine(FadeButtonImage(data, show));
    }

    private IEnumerator FadeButtonImage(ButtonData data, bool fadeIn)
    {
        if (data.hoverImage == null) yield break;

        data.hoverImage.gameObject.SetActive(true);
        Debug.Log($"激活图片: {data.hoverImage.gameObject.name}");

        Color startColor = data.hoverImage.color;
        Color targetColor = fadeIn ?
            new Color(startColor.r, startColor.g, startColor.b, 1f) :
            new Color(startColor.r, startColor.g, startColor.b, 0f);

        float duration = fadeIn ? data.imageFadeInDuration : data.imageFadeOutDuration;

        if (duration <= 0f)
        {
            data.hoverImage.color = targetColor;
            if (!fadeIn) data.hoverImage.gameObject.SetActive(false);
            yield break;
        }

        float elapsedTime = 0f;

        while (elapsedTime < duration)
        {
            elapsedTime += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsedTime / duration);
            data.hoverImage.color = Color.Lerp(startColor, targetColor, t);
            yield return null;
        }

        data.hoverImage.color = targetColor;

        if (!fadeIn)
        {
            data.hoverImage.gameObject.SetActive(false);
        }

        data.imageFadeCoroutine = null;
    }

    private IEnumerator HoverEnterAnimationRoutine(ButtonData data)
    {
        RectTransform rt = data.rectTransform;
        Vector2 startPos = rt.anchoredPosition;
        Vector2 targetPos = data.targetPosition + new Vector2(hoverOffset, 0f);
        Vector2 overshootPos = targetPos + new Vector2(hoverOvershootAmount, 0f);

        Color startColor = data.text != null ? data.text.color : data.normalColor;
        Color targetColor = data.hoverColor;
        Quaternion startRotation = rt.localRotation;
        Quaternion hoverRotation = Quaternion.Euler(hoverRotationAngle, 0f, 0f);

        float elapsedTime = 0f;
        while (elapsedTime < hoverMoveDuration)
        {
            elapsedTime += Time.unscaledDeltaTime;
            float t = hoverMoveCurve.Evaluate(elapsedTime / hoverMoveDuration);

            rt.anchoredPosition = Vector2.Lerp(startPos, overshootPos, t);
            float rotationT = hoverRotationCurve.Evaluate(elapsedTime / hoverRotationDuration);
            rt.localRotation = Quaternion.Lerp(startRotation, hoverRotation, rotationT);

            if (data.text != null) data.text.color = Color.Lerp(startColor, targetColor, t);
            yield return null;
        }

        elapsedTime = 0f;
        Vector2 overshootStartPos = rt.anchoredPosition;
        while (elapsedTime < hoverOvershootDuration)
        {
            elapsedTime += Time.unscaledDeltaTime;
            float t = overshootCurve.Evaluate(elapsedTime / hoverOvershootDuration);

            rt.anchoredPosition = Vector2.Lerp(overshootStartPos, targetPos, t);
            if (data.text != null) data.text.color = Color.Lerp(data.text.color, targetColor, t);
            yield return null;
        }

        rt.anchoredPosition = targetPos;
        rt.localRotation = hoverRotation;
        if (data.text != null) data.text.color = targetColor;
        data.hoverCoroutine = null;
    }

    private IEnumerator HoverExitAnimationRoutine(ButtonData data)
    {
        RectTransform rt = data.rectTransform;
        Vector2 startPos = rt.anchoredPosition;
        Vector2 targetPos = data.targetPosition;
        Vector2 overshootPos = targetPos - new Vector2(hoverOvershootAmount, 0f);

        Color startColor = data.text != null ? data.text.color : data.normalColor;
        Color targetColor = data.normalColor;
        Quaternion startRotation = rt.localRotation;
        Quaternion targetRotation = data.targetRotation;

        float elapsedTime = 0f;
        while (elapsedTime < hoverMoveDuration)
        {
            elapsedTime += Time.unscaledDeltaTime;
            float t = hoverMoveCurve.Evaluate(elapsedTime / hoverMoveDuration);

            rt.anchoredPosition = Vector2.Lerp(startPos, overshootPos, t);
            float rotationT = hoverRotationCurve.Evaluate(elapsedTime / hoverRotationDuration);
            rt.localRotation = Quaternion.Lerp(startRotation, targetRotation, rotationT);

            if (data.text != null) data.text.color = Color.Lerp(startColor, targetColor, t);
            yield return null;
        }

        elapsedTime = 0f;
        Vector2 overshootStartPos = rt.anchoredPosition;
        while (elapsedTime < hoverOvershootDuration)
        {
            elapsedTime += Time.unscaledDeltaTime;
            float t = overshootCurve.Evaluate(elapsedTime / hoverOvershootDuration);

            rt.anchoredPosition = Vector2.Lerp(overshootStartPos, targetPos, t);
            if (data.text != null) data.text.color = Color.Lerp(data.text.color, targetColor, t);
            yield return null;
        }

        rt.anchoredPosition = targetPos;
        rt.localRotation = targetRotation;
        if (data.text != null) data.text.color = targetColor;
        data.hoverCoroutine = null;
    }

    private void OnButtonClick(ButtonData data)
    {
        if (data.hoverCoroutine != null) StopCoroutine(data.hoverCoroutine);
        data.hoverCoroutine = StartCoroutine(ClickAnimationRoutine(data));
    }

    private IEnumerator ClickAnimationRoutine(ButtonData data)
    {
        if (data.rectTransform == null) yield break;

        RectTransform rt = data.rectTransform;

        Vector3 originalScale = Vector3.one;
        Vector3 targetScale = originalScale * 0.85f;
        Vector3 overshootScale = originalScale * 1.1f;
        float duration = 0.1f;

        float elapsedTime = 0f;
        while (elapsedTime < duration)
        {
            elapsedTime += Time.unscaledDeltaTime;
            float t = elapsedTime / duration;
            rt.localScale = Vector3.Lerp(originalScale, targetScale, t);
            yield return null;
        }

        elapsedTime = 0f;
        while (elapsedTime < duration * 0.5f)
        {
            elapsedTime += Time.unscaledDeltaTime;
            float t = elapsedTime / (duration * 0.5f);
            rt.localScale = Vector3.Lerp(targetScale, overshootScale, t);
            yield return null;
        }

        elapsedTime = 0f;
        while (elapsedTime < duration)
        {
            elapsedTime += Time.unscaledDeltaTime;
            float t = elapsedTime / duration;
            rt.localScale = Vector3.Lerp(overshootScale, originalScale, t);
            yield return null;
        }

        rt.localScale = Vector3.one;

        if (data.isHovering)
        {
            if (data.hoverCoroutine != null) StopCoroutine(data.hoverCoroutine);
            data.hoverCoroutine = StartCoroutine(HoverEnterAnimationRoutine(data));
        }
        else
        {
            if (data.hoverCoroutine != null) StopCoroutine(data.hoverCoroutine);
            data.hoverCoroutine = StartCoroutine(HoverExitAnimationRoutine(data));
        }
    }

    [ContextMenu("重新播放入场动画")]
    public void ReplayEntryAnimation()
    {
        animationStarted = false;
        inputEnabled = true;

        if (titleRectTransform != null)
        {
            titleRectTransform.anchoredPosition = titleInitialPosition;
        }

        if (titleImage != null)
        {
            titleImage.color = titleInitialColor;
        }

        InitializeButtonPositions();

        if (hintText != null)
        {
            hintText.gameObject.SetActive(true);
            hintText.color = hintNormalColor;
        }

        SetupHintText();
    }

    [ContextMenu("重置所有按钮")]
    public void ResetAllButtons()
    {
        foreach (ButtonData data in buttonDataList)
        {
            if (data.rectTransform != null)
            {
                // 停止所有协程
                if (data.hoverCoroutine != null)
                {
                    StopCoroutine(data.hoverCoroutine);
                    data.hoverCoroutine = null;
                }

                if (data.imageFadeCoroutine != null)
                {
                    StopCoroutine(data.imageFadeCoroutine);
                    data.imageFadeCoroutine = null;
                }

                // 重置状态
                data.isHovering = false;
                data.targetRotation = Quaternion.identity;

                // 重置按钮到初始状态
                ResetButtonToInitialState(data);

                if (data.button != null)
                {
                    data.button.interactable = true;
                }
            }
        }
    }

    /// <summary>
    /// 完全重置所有按钮到初始状态
    /// </summary>
    [ContextMenu("完全重置")]
    public void FullReset()
    {
        // 重置标志
        isFirst = true;
        animationStarted = false;
        inputEnabled = true;

        // 清除协程
        if (hintBlinkCoroutine != null)
        {
            StopCoroutine(hintBlinkCoroutine);
            hintBlinkCoroutine = null;
        }

        if (entryAnimationCoroutine != null)
        {
            StopCoroutine(entryAnimationCoroutine);
            entryAnimationCoroutine = null;
        }

        // 重置标题
        if (titleRectTransform != null)
        {
            titleRectTransform.anchoredPosition = titleInitialPosition;
        }

        if (titleImage != null)
        {
            titleImage.color = titleInitialColor;
        }

        // 重置提示文本
        if (hintText != null)
        {
            hintText.gameObject.SetActive(true);
            hintText.color = hintNormalColor;
        }

        // 重置所有按钮
        foreach (ButtonData data in buttonDataList)
        {
            if (data.rectTransform != null)
            {
                // 停止所有协程
                if (data.hoverCoroutine != null)
                {
                    StopCoroutine(data.hoverCoroutine);
                    data.hoverCoroutine = null;
                }

                if (data.imageFadeCoroutine != null)
                {
                    StopCoroutine(data.imageFadeCoroutine);
                    data.imageFadeCoroutine = null;
                }

                // 重置状态
                data.isHovering = false;
                data.targetRotation = Quaternion.identity;

                // 重置到初始状态
                data.rectTransform.anchoredPosition = data.initialPosition;
                data.rectTransform.localScale = data.initialScale;
                data.rectTransform.localRotation = data.initialRotation;

                if (data.text != null)
                {
                    data.text.color = data.initialTextColor;
                }

                if (data.hoverImage != null)
                {
                    data.hoverImage.color = data.initialImageColor;
                    data.hoverImage.gameObject.SetActive(false);
                }

                if (data.button != null)
                {
                    data.button.interactable = false;
                }
            }
        }
    }
}