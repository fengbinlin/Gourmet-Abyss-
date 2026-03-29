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
    [SerializeField] private float moneyScrollSmoothTime = 0.14f;
    [SerializeField] private float moneyScrollMaxSpeed = 2200f;
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
    private float _moneyScrollVel;

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
        {
            _moneyTarget = newCount;
            if (!_moneyScrolling)
            {
                _moneyScrolling = true;
                _moneyPulsePhase = 0f;
            }
        }
        else
        {
            _moneyTarget = newCount;
            _moneyDisplayFloat = newCount;
            _moneyScrolling = false;
            _moneyScrollVel = 0f;
            ResetMoneyTextScales();
            ApplyMoneyTexts();
        }
    }

    private void Update()
    {
        TrySubscribeResourceEvents();
        if (GameValManager.Instance == null) return;

        UpdateMoneyDisplay();

        textF1.text = GameValManager.Instance.GetResourceCount(ResourceType.LootPumkin).ToString();
        textF2.text = GameValManager.Instance.GetResourceCount(ResourceType.LootOnion).ToString();
        textF3.text = GameValManager.Instance.GetResourceCount(ResourceType.LootPear).ToString();
        STextPKVal.text = GameValManager.Instance.GetResourceCount(ResourceType.LootPumkin).ToString();
    }

    private void UpdateMoneyDisplay()
    {
        if (_moneyScrolling)
        {
            float dist = _moneyTarget - _moneyDisplayFloat;
            if (Mathf.Abs(dist) <= moneyScrollSnapEpsilon)
            {
                _moneyDisplayFloat = _moneyTarget;
                _moneyScrollVel = 0f;
                _moneyScrolling = false;
                ResetMoneyTextScales();
                ApplyMoneyTexts();
                return;
            }

            float smoothT = Mathf.Max(0.03f, moneyScrollSmoothTime);
            _moneyDisplayFloat = Mathf.SmoothDamp(
                _moneyDisplayFloat,
                _moneyTarget,
                ref _moneyScrollVel,
                smoothT,
                moneyScrollMaxSpeed,
                Time.deltaTime);

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
            int m = GameValManager.Instance.GetResourceCount(ResourceType.Money);
            _moneyTarget = m;
            _moneyDisplayFloat = m;
            _moneyScrollVel = 0f;
            ResetMoneyTextScales();
            ApplyMoneyTexts();
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
