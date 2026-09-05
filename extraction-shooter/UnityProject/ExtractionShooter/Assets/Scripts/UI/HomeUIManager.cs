using UnityEngine;
using UnityEngine.UI;

public class HomeUIManager : MonoBehaviour
{
    public Text textMoneyVal;
    public Text textF1;
    public Text textF2;
    public Text textF3;
    public Text STextMoneyVal;
    public Text STextPKVal;

    [Header("金币数字滚动")]
    [Tooltip("用于估算动画时长：时长 ≈ 变化量 / 该速度，再被下方最小/最大时长钳制")]
    [SerializeField] private float moneyScrollReferenceSpeed = 2200f;
    [SerializeField] private float moneyScrollMinDuration = 0.06f;
    [SerializeField] private float moneyScrollMaxDuration = 0.42f;
    [SerializeField] private float moneyScrollSnapEpsilon = 0.35f;

    [Header("金币滚动时仅 Y 轴缩放")]
    [Tooltip("滚动期间相对原始 Y 的主压扁系数（越小压得越扁）")]
    [SerializeField] private float moneySquashBaseY = 0.76f;
    [Tooltip("在大压扁基础上，Y 的相对波动幅度（越大起伏越明显）")]
    [SerializeField] private float moneySquashWobbleY = 0.11f;
    [Tooltip("波动频率（Hz），越大起伏越快")]
    [SerializeField] private float moneyWobbleHz = 8f;
    [Tooltip("二次谐波权重：与主波叠加形成「顶过头再收回」的波形（建议 0.28~0.45）")]
    [SerializeField] private float moneyWobbleHarmonic2 = 0.38f;
    [Tooltip("二次谐波相位偏移（弧度），微调波峰不对称感")]
    [SerializeField] private float moneyWobblePhase2 = 0.55f;
    [Tooltip("合成后位移的钳制，防止极端帧")]
    [SerializeField] private float moneyWobbleClamp = 1.35f;

    private float _moneyDisplayFloat;
    private int _moneyTarget;
    private bool _moneyScrolling;
    private float _moneyPulsePhase;
    private float _moneyScrollStartValue;
    private float _moneyScrollElapsed;
    private float _moneyScrollDuration;

    private Vector3 _moneyTextScaleOriginal;
    private Vector3 _stMoneyTextScaleOriginal;
    private bool _moneyScalesCached;
    private bool _hookedResourceEvents;

    private void Awake()
    {
        CacheMoneyTextScales();
    }

    private void OnEnable()
    {
        TrySubscribeResourceEvents();
    }

    private void OnDisable()
    {
        if (GameValManager.Instance != null && _hookedResourceEvents)
        {
            GameValManager.Instance.OnResourceChanged.RemoveListener(OnResourceChanged);
            _hookedResourceEvents = false;
        }
    }

    private void Start()
    {
        CacheMoneyTextScales();
        TrySubscribeResourceEvents();
        if (GameValManager.Instance != null)
        {
            int m = GameValManager.Instance.GetResourceCount(ResourceType.Money);
            _moneyTarget = m;
            _moneyDisplayFloat = m;
            _moneyScrolling = false;
            ApplyMoneyTexts();
        }
    }

    private void CacheMoneyTextScales()
    {
        if (_moneyScalesCached) return;
        if (textMoneyVal != null)
            _moneyTextScaleOriginal = textMoneyVal.rectTransform.localScale;
        if (STextMoneyVal != null)
            _stMoneyTextScaleOriginal = STextMoneyVal.rectTransform.localScale;
        _moneyScalesCached = textMoneyVal != null || STextMoneyVal != null;
    }

    private void TrySubscribeResourceEvents()
    {
        if (GameValManager.Instance == null || _hookedResourceEvents) return;
        GameValManager.Instance.OnResourceChanged.AddListener(OnResourceChanged);
        _hookedResourceEvents = true;
    }

    private void OnResourceChanged(ResourceType type, int oldCount, int newCount)
    {
        if (type != ResourceType.Money) return;

        if (newCount > oldCount)
            BeginMoneyIncreaseScroll(newCount);
        else
            SnapMoneyDisplay(newCount);
    }

    private void BeginMoneyIncreaseScroll(int newTarget)
    {
        _moneyTarget = newTarget;
        float delta = Mathf.Abs(_moneyTarget - _moneyDisplayFloat);
        if (delta <= moneyScrollSnapEpsilon)
        {
            SnapMoneyDisplay(newTarget);
            return;
        }

        _moneyScrollStartValue = _moneyDisplayFloat;
        _moneyScrollElapsed = 0f;
        float refSpeed = Mathf.Max(1f, moneyScrollReferenceSpeed);
        _moneyScrollDuration = Mathf.Clamp(delta / refSpeed, moneyScrollMinDuration, moneyScrollMaxDuration);
        _moneyScrolling = true;
        _moneyPulsePhase = 0f;
    }

    private void SnapMoneyDisplay(int value)
    {
        _moneyTarget = value;
        _moneyDisplayFloat = value;
        _moneyScrolling = false;
        _moneyScrollElapsed = 0f;
        ResetMoneyTextScales();
        ApplyMoneyTexts();
    }

    private void Update()
    {
        TrySubscribeResourceEvents();
        GameValManager manager = GameValManager.Instance;
        if (manager == null) return;

        UpdateMoneyDisplay();

        string pumpkin = manager.GetResourceCount(ResourceType.LootPumkin).ToString();
        if (textF1 != null) textF1.text = pumpkin;
        if (textF2 != null) textF2.text = manager.GetResourceCount(ResourceType.LootOnion).ToString();
        if (textF3 != null) textF3.text = manager.GetResourceCount(ResourceType.LootPear).ToString();
        if (STextPKVal != null) STextPKVal.text = pumpkin;
    }

    private void UpdateMoneyDisplay()
    {
        if (_moneyScrolling)
        {
            _moneyScrollElapsed += Time.deltaTime;
            float duration = Mathf.Max(0.01f, _moneyScrollDuration);
            float t = Mathf.Clamp01(_moneyScrollElapsed / duration);
            float eased = 1f - Mathf.Pow(1f - t, 3f);
            _moneyDisplayFloat = Mathf.Lerp(_moneyScrollStartValue, _moneyTarget, eased);

            if (t >= 1f || Mathf.Abs(_moneyTarget - _moneyDisplayFloat) <= moneyScrollSnapEpsilon)
            {
                SnapMoneyDisplay(_moneyTarget);
                return;
            }

            _moneyPulsePhase += Time.deltaTime * moneyWobbleHz * Mathf.PI * 2f;
            float p = _moneyPulsePhase;
            // 主波 + 二次谐波：解析波形，无弹簧低通，波峰可「超过」中性位置再回落，肉眼可见
            float wobble = Mathf.Sin(p) + moneyWobbleHarmonic2 * Mathf.Sin(2f * p + moneyWobblePhase2);
            wobble = Mathf.Clamp(wobble, -moneyWobbleClamp, moneyWobbleClamp);

            ApplyMoneyScrollYScale(wobble);
            ApplyMoneyTexts();
        }
        else
        {
            SnapMoneyDisplay(GameValManager.Instance.GetResourceCount(ResourceType.Money));
        }
    }

    private void ApplyMoneyTexts()
    {
        string s = Mathf.RoundToInt(_moneyDisplayFloat).ToString();
        if (textMoneyVal != null) textMoneyVal.text = s;
        if (STextMoneyVal != null) STextMoneyVal.text = s;
    }

    /// <summary>
    /// 滚动中：仅改 Y。主压扁 moneySquashBaseY，再叠加大压扁基础上的合成波（主波+二次谐波，易有过冲感）。
    /// </summary>
    private void ApplyMoneyScrollYScale(float wobbleValue)
    {
        float yMul = Mathf.Clamp(moneySquashBaseY * (1f + moneySquashWobbleY * wobbleValue), 0.05f, 4f);

        if (textMoneyVal != null)
        {
            Vector3 s = _moneyTextScaleOriginal;
            s.y *= yMul;
            textMoneyVal.rectTransform.localScale = s;
        }
        if (STextMoneyVal != null)
        {
            Vector3 s = _stMoneyTextScaleOriginal;
            s.y *= yMul;
            STextMoneyVal.rectTransform.localScale = s;
        }
    }

    private void ResetMoneyTextScales()
    {
        if (textMoneyVal != null)
            textMoneyVal.rectTransform.localScale = _moneyTextScaleOriginal;
        if (STextMoneyVal != null)
            STextMoneyVal.rectTransform.localScale = _stMoneyTextScaleOriginal;
    }
}
