using System.Collections;
using System.Collections.Generic;
using Game.Core;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 全局消息提示系统：其他脚本可调用 GlobalMessageUI.Show("文本", 显示时间)
/// - 生成消息面板预制体
/// - 从屏幕底部外滑入到屏幕中部
/// - 停留一段时间后淡出并离开
/// - 高并发调用时自动排队显示（同一时间只播一条）
/// </summary>
public class GlobalMessageUI : PersistentMonoSingleton<GlobalMessageUI>
{
    private enum MessageKind
    {
        NormalText,
        ResourceGain
    }

    [Header("基础配置")]
    [Tooltip("消息面板预制体（Prefab 内需要有 Text 或 TMP 文本；此脚本默认找 Text）")]
    [SerializeField] private GameObject messagePanelPrefab;
    [Tooltip("资源增加消息预制体（包含 Image + Text/TMP_Text，例如：图标 + '+13'）")]
    [SerializeField] private GameObject resourceGainPanelPrefab;

    [Tooltip("消息面板生成到哪个父节点下（通常是 Canvas 下的一个空 RectTransform）")]
    [SerializeField] private RectTransform messageParent;

    [Header("动画位置（anchoredPosition）")]
    [SerializeField] private Vector2 startPos = new Vector2(0f, -400f);
    [SerializeField] private Vector2 endPos = new Vector2(0f, 400f);

    [Header("时间参数")]
    [Tooltip("从 S 移动到 E 的时长")]
    [SerializeField] private float moveDuration = 0.6f;
    [Tooltip("到达 E 后的渐隐时长")]
    [SerializeField] private float fadeOutDuration = 0.25f;
    [SerializeField] private float defaultShowTime = 1.5f;
    [Tooltip("是否使用不受 Time.timeScale 影响的时间（避免暂停/慢动作时消息卡住）")]
    [SerializeField] private bool useUnscaledTime = true;
    [Tooltip("单条消息超时上限（秒）。超过后强制结束，避免队列卡死。<=0 表示关闭。")]
    [SerializeField] private float perMessageTimeout = 8f;

    [Header("淡入淡出")]
    [SerializeField] private bool useFade = true;

    [Header("缩放动效")]
    [Tooltip("初始缩放（进入动画开始时）")]
    [SerializeField] private float startScale = 0.9f;
    [Tooltip("进入动画峰值缩放（形成轻微弹入感）")]
    [SerializeField] private float popScale = 1.05f;
    [Tooltip("常态缩放（通常为 1）")]
    [SerializeField] private float normalScale = 1f;
    [Tooltip("渐隐结束时缩放（略微缩小更自然）")]
    [SerializeField] private float fadeOutScale = 0.92f;

    private readonly Queue<MessageData> queue = new Queue<MessageData>();
    private bool isPlaying;

    private sealed class MessageData
    {
        public readonly MessageKind kind;
        public readonly string text;
        public readonly float showTime;
        public readonly Sprite icon;
        public readonly int amount;

        public MessageData(MessageKind kind, string text, float showTime, Sprite icon, int amount)
        {
            this.kind = kind;
            this.text = text;
            this.showTime = showTime;
            this.icon = icon;
            this.amount = amount;
        }
    }

    private void OnDisable()
    {
        // 切场/过渡时可能会禁用某些 UI 根节点，导致协程被中断而残留消息面板卡在屏幕上。
        // 这里做一次硬清理，避免“消息不消失”。
        ClearAllMessages();
    }

    /// <summary>
    /// 强制清空所有消息（队列 + 当前正在播放的消息面板），用于切场/过渡时避免残留。
    /// </summary>
    public void ClearAllMessages()
    {
        queue.Clear();
        isPlaying = false;
        StopAllCoroutines();

        if (messageParent == null) return;

        // 销毁所有已生成的消息面板
        for (int i = messageParent.childCount - 1; i >= 0; i--)
        {
            Transform child = messageParent.GetChild(i);
            if (child != null)
                Destroy(child.gameObject);
        }
    }

    /// <summary>静态入口：切场时可直接调用。</summary>
    public static void Clear()
    {
        Instance?.ClearAllMessages();
    }

    /// <summary>
    /// 其他脚本调用入口。示例：GlobalMessageUI.Show("获得金币+10", 1.2f);
    /// </summary>
    public static void Show(string text, float showTime = -1f)
    {
        if (Instance == null)
        {
            Debug.LogWarning("[GlobalMessageUI] 场景中没有 GlobalMessageUI 实例。请创建一个物体挂载 GlobalMessageUI.cs。");
            return;
        }

        if (string.IsNullOrEmpty(text))
            return;

        float finalTime = showTime > 0f ? showTime : Instance.defaultShowTime;
        Instance.Enqueue(new MessageData(MessageKind.NormalText, text, finalTime, null, 0));
    }

    /// <summary>
    /// 资源增长提示（图标 + 文本，如 "+13"），与普通消息共用同一队列。
    /// </summary>
    public static void ShowResourceGain(Sprite icon, int amount, float showTime = -1f)
    {
        if (Instance == null)
        {
            Debug.LogWarning("[GlobalMessageUI] 场景中没有 GlobalMessageUI 实例。请创建一个物体挂载 GlobalMessageUI.cs。");
            return;
        }

        if (amount <= 0) return;
        float finalTime = showTime > 0f ? showTime : Instance.defaultShowTime;
        Instance.Enqueue(new MessageData(MessageKind.ResourceGain, string.Empty, finalTime, icon, amount));
    }

    private void Enqueue(MessageData message)
    {
        queue.Enqueue(message);
        if (!isPlaying)
        {
            StartCoroutine(ProcessQueue());
        }
    }

    private IEnumerator ProcessQueue()
    {
        isPlaying = true;

        while (queue.Count > 0)
        {
            var data = queue.Dequeue();
            yield return PlayOne(data);
        }

        isPlaying = false;
    }

    private IEnumerator PlayOne(MessageData data)
    {
        GameObject prefabToUse = data.kind == MessageKind.ResourceGain ? resourceGainPanelPrefab : messagePanelPrefab;
        if (prefabToUse == null || messageParent == null)
        {
            Debug.LogWarning("[GlobalMessageUI] 缺少消息预制体或 messageParent 引用。");
            yield break;
        }

        GameObject go = Instantiate(prefabToUse, messageParent);
        RectTransform rt = go.GetComponent<RectTransform>();
        if (rt == null)
        {
            Debug.LogWarning("[GlobalMessageUI] messagePanelPrefab 缺少 RectTransform（请确保是 UI 预制体）。");
            Destroy(go);
            yield break;
        }

        BindMessageContent(go, data);

        CanvasGroup cg = null;
        if (useFade)
        {
            cg = go.GetComponent<CanvasGroup>();
            if (cg == null) cg = go.AddComponent<CanvasGroup>();
            cg.alpha = 0f;
        }

        rt.anchoredPosition = startPos;
        rt.localScale = Vector3.one * Mathf.Max(0.01f, startScale);
        if (cg != null) cg.alpha = 1f;

        float timeoutTimer = Mathf.Max(0f, perMessageTimeout);
        bool useTimeout = timeoutTimer > 0f;

        // 从 S -> E：EaseOutCubic（更丝滑）
        float t = 0f;
        float move = Mathf.Max(0.01f, moveDuration);
        while (t < move)
        {
            float dt = GetDeltaTime();
            t += dt;
            if (useTimeout)
            {
                timeoutTimer -= dt;
                if (timeoutTimer <= 0f)
                {
                    Destroy(go);
                    yield break;
                }
            }
            float p = Mathf.Clamp01(t / move);
            float ease = 1f - Mathf.Pow(1f - p, 3f);
            rt.anchoredPosition = Vector2.LerpUnclamped(startPos, endPos, ease);

            // 缩放：前半段放大到 popScale，后半段回落到 normalScale
            float scaleValue;
            if (p < 0.5f)
            {
                float p1 = p / 0.5f;
                scaleValue = Mathf.Lerp(startScale, popScale, p1);
            }
            else
            {
                float p2 = (p - 0.5f) / 0.5f;
                scaleValue = Mathf.Lerp(popScale, normalScale, p2);
            }
            rt.localScale = Vector3.one * Mathf.Max(0.01f, scaleValue);
            yield return null;
        }
        rt.anchoredPosition = endPos;
        rt.localScale = Vector3.one * Mathf.Max(0.01f, normalScale);

        // 停留
        float hold = Mathf.Max(0f, data.showTime);
        if (useTimeout)
        {
            hold = Mathf.Min(hold, timeoutTimer);
            timeoutTimer -= hold;
            if (timeoutTimer <= 0f && hold <= 0f)
            {
                Destroy(go);
                yield break;
            }
        }
        yield return WaitForSecondsSafe(hold);
        if (useTimeout && timeoutTimer <= 0f)
        {
            Destroy(go);
            yield break;
        }

        // 到达 E 后渐隐消失
        if (cg != null)
        {
            t = 0f;
            float fade = Mathf.Max(0.01f, fadeOutDuration);
            while (t < fade)
            {
                float dt = GetDeltaTime();
                t += dt;
                if (useTimeout)
                {
                    timeoutTimer -= dt;
                    if (timeoutTimer <= 0f)
                    {
                        Destroy(go);
                        yield break;
                    }
                }
                float p = Mathf.Clamp01(t / fade);
                float ease = p * p;
                cg.alpha = 1f - ease;
                float s = Mathf.Lerp(normalScale, fadeOutScale, ease);
                rt.localScale = Vector3.one * Mathf.Max(0.01f, s);
                yield return null;
            }
        }

        Destroy(go);
    }

    private void BindMessageContent(GameObject go, MessageData data)
    {
        if (go == null) return;

        if (data.kind == MessageKind.ResourceGain)
        {
            Image iconImage = go.GetComponentInChildren<Image>(true);
            if (iconImage != null && data.icon != null)
            {
                iconImage.sprite = data.icon;
            }

            string amountText = $"+{data.amount}";
            Text txt = go.GetComponentInChildren<Text>(true);
            if (txt != null) txt.text = amountText;
            TMP_Text tmp = go.GetComponentInChildren<TMP_Text>(true);
            if (tmp != null) tmp.text = amountText;
            return;
        }

        Text normalTxt = go.GetComponentInChildren<Text>(true);
        if (normalTxt != null) normalTxt.text = data.text;
        TMP_Text normalTmp = go.GetComponentInChildren<TMP_Text>(true);
        if (normalTmp != null) normalTmp.text = data.text;
    }

    private float GetDeltaTime()
    {
        return useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
    }

    private IEnumerator WaitForSecondsSafe(float duration)
    {
        if (duration <= 0f) yield break;
        if (!useUnscaledTime)
        {
            yield return new WaitForSeconds(duration);
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
    }
}

