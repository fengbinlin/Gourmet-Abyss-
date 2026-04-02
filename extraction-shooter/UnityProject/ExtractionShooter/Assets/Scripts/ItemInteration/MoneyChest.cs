using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Text;

public class MoneyChest : MonoBehaviour
{
    [Header("存钱箱设置")]
    [SerializeField] private int currentMoney = 0;
    [SerializeField] private float bounceHeight = 0.2f;
    [SerializeField] private float bounceDuration = 0.3f;

    [Header("取钱设置")]
    [SerializeField] private float baseWithdrawTime = 2.0f;
    [SerializeField] private float minWithdrawTime = 0.3f;  // 最短取钱时间
    [SerializeField] private float maxWithdrawTime = 5.0f;
    [SerializeField] private float smoothEndDuration = 0.5f;
    [SerializeField] private float minWithdrawRate = 20f;   // 新增：最小取钱速率（每秒至少取多少钱）

    [Header("UI设置")]
    [SerializeField] private GameObject moneyTextPrefab;
    [SerializeField] private Vector3 textOffset = new Vector3(0, 2f, 0);
    [SerializeField] private bool useCompactNotation = true;
    [SerializeField] private int decimalPlaces = 1;
    [SerializeField] private bool showDecimalForSmallValues = false;

    [Header("交互设置")]
    [SerializeField] private KeyCode interactKey = KeyCode.E;

    [Header("音效")]
    [SerializeField] private AudioClip depositSound;
    [SerializeField] private AudioClip withdrawSound;

    [Header("视觉效果")]
    [SerializeField] private ParticleSystem moneyParticles;
    [SerializeField] private Light moneyLight;

    [Header("入账波动（售卖金币入箱期间）")]
    [Tooltip("相对原始缩放的波动幅度（例如 0.08 = ±8%）")]
    [SerializeField] private float depositPulseAmplitude = 0.07f;
    [Tooltip("完成一次变大→变小 的周期（秒）")]
    [SerializeField] private float depositPulseCycleSeconds = 0.22f;
    [Tooltip("每次入账后，波动至少再持续多久；多次入账会顺延结束时间")]
    [SerializeField] private float depositPulseExtendSeconds = 0.4f;

    [Header("发射器引用")]
    [SerializeField] private ProjectileLauncher projectileLauncher;
    [SerializeField] private Transform playerTransform;

    [Header("取钱金币飞行轨迹参数（面板配置）")]
    [SerializeField] private float moneyProjectileFlightDuration = 2f;
    [SerializeField] private float moneyProjectileMaxHeight = 5f;

    [Header("调试")]
    [SerializeField] private bool debugMode = false;

    private Text moneyText;
    private AudioSource audioSource;
    private Vector3 originalScale;

    public bool isPlayerInRange = false;
    private bool isWithdrawing = false;
    private Coroutine withdrawCoroutine;
    private bool isBouncing = false;
    private Coroutine bounceCoroutine;
    private Coroutine depositPulseCoroutine;
    private float depositPulseActiveUntil;

    private int withdrawStartAmount = 0;
    private int alreadyWithdrawn = 0;

    public delegate void MoneyChangedHandler(int newAmount, int changeAmount);
    public event MoneyChangedHandler OnMoneyChanged;

    public static MoneyChest Instance { get; private set; }

    private static readonly string[] MoneyUnits = { "", "K", "M", "B", "T", "Qa", "Qi", "Sx", "Sp", "Oc", "No" };

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();

        originalScale = transform.localScale;
        InitializeMoneyText();
    }

    private void Start()
    {
        UpdateMoneyText();
        Collider collider = GetComponent<Collider>();
        if (collider == null) collider = gameObject.AddComponent<BoxCollider>();
        collider.isTrigger = true;
    }

    private void Update()
    {
        if (isPlayerInRange)
            HandlePlayerInput();

        UpdateBounceAnimation();
    }

    private void InitializeMoneyText()
    {
        if (moneyTextPrefab != null)
            moneyText = moneyTextPrefab.GetComponent<Text>();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            GetComponent<InteractiveFeedback>()?.PlayFeedback();
            isPlayerInRange = true;
            playerTransform = other.transform;
            OnPlayerEnterRange();
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            isPlayerInRange = false;
            OnPlayerExitRange();
        }
    }

    public void AddMoney(int amount)
    {
        if (amount <= 0) return;
        currentMoney += amount;
        UpdateMoneyText();
        PlayDepositEffects();
        NotifyDepositPulseExtend();

        OnMoneyChanged?.Invoke(currentMoney, amount);
    }

    public int WithdrawMoney(int amount)
    {
        if (amount <= 0 || currentMoney <= 0) return 0;

        int actualAmount = Mathf.Min(amount, currentMoney);
        currentMoney -= actualAmount;
        UpdateMoneyText();
        StartBounce();

        if (withdrawSound != null)
            audioSource.PlayOneShot(withdrawSound, 0.7f);

        if (projectileLauncher != null && playerTransform != null)
        {
            AudioManager.Instance.PlayAudio("2");
                projectileLauncher.SpawnProjectile(
                    transform,
                    playerTransform,
                    ResourceType.Money,
                    actualAmount,
                    () =>
                    {
                        if (GameValManager.Instance != null)
                            GameValManager.Instance.AddResource(ResourceType.Money, actualAmount);
                        OnMoneyChanged?.Invoke(currentMoney, -actualAmount);
                    },
                    moneyProjectileFlightDuration,
                    moneyProjectileMaxHeight
                );
        }
        return actualAmount;
    }

    /// <summary>
    /// 根据金额计算实际取钱时间
    /// </summary>
    private float CalculateWithdrawTime(int amount)
    {
        // 按最小速率取完需要的时间
        float timeByMinRate = amount / minWithdrawRate;

        // 取 baseWithdrawTime 和 timeByMinRate 中的较小值
        // 这样小金额会更快取完
        float actualTime = Mathf.Min(baseWithdrawTime, timeByMinRate);

        // 限制在最小和最大时间范围内
        return Mathf.Clamp(actualTime, minWithdrawTime, maxWithdrawTime);
    }

    private void StartWithdrawing()
    {
        if (isWithdrawing || currentMoney <= 0) return;

        isWithdrawing = true;
        alreadyWithdrawn = 0;
        withdrawStartAmount = currentMoney;

        if (withdrawCoroutine != null)
            StopCoroutine(withdrawCoroutine);

        withdrawCoroutine = StartCoroutine(WithdrawMoneySmoothly());
    }

    private void StopWithdrawing()
    {
        if (!isWithdrawing) return;

        isWithdrawing = false;
        if (withdrawCoroutine != null)
        {
            StopCoroutine(withdrawCoroutine);
            withdrawCoroutine = null;
        }
    }

    private IEnumerator WithdrawMoneySmoothly()
    {
        float elapsedTime = 0f;

        // 根据金额动态计算取钱时间
        float totalWithdrawTime = CalculateWithdrawTime(withdrawStartAmount);
        float mainWithdrawTime = Mathf.Max(0.1f, totalWithdrawTime - smoothEndDuration);
        float actualSmoothDuration = Mathf.Min(smoothEndDuration, totalWithdrawTime * 0.3f);

        while (isWithdrawing && currentMoney > 0)
        {
            if (elapsedTime <= mainWithdrawTime)
            {
                float t = Mathf.Clamp01(elapsedTime / mainWithdrawTime);
                float eased = t * t * (3f - 2f * t);
                float targetRatio = Mathf.Lerp(0f, 0.9f, eased);

                int targetWithdrawnThisRound = Mathf.RoundToInt(withdrawStartAmount * targetRatio);
                int amountToWithdraw = targetWithdrawnThisRound - alreadyWithdrawn;

                if (amountToWithdraw > 0)
                {
                    int withdrawn = WithdrawMoney(amountToWithdraw);
                    alreadyWithdrawn += withdrawn;
                }
            }
            else
            {
                float smoothElapsed = elapsedTime - mainWithdrawTime;
                float smoothT = Mathf.Clamp01(smoothElapsed / actualSmoothDuration);
                float eased = 1f - Mathf.Pow(1f - smoothT, 3f);
                float targetRatio = Mathf.Lerp(0.9f, 1f, eased);

                int targetWithdrawnThisRound = Mathf.RoundToInt(withdrawStartAmount * targetRatio);
                int amountToWithdraw = targetWithdrawnThisRound - alreadyWithdrawn;

                if (amountToWithdraw > 0)
                {
                    int withdrawn = WithdrawMoney(amountToWithdraw);
                    alreadyWithdrawn += withdrawn;
                }

                // 如果结束阶段完成但还有钱，重新开始一轮
                if (smoothT >= 1f && currentMoney > 0)
                {
                    elapsedTime = 0f;
                    alreadyWithdrawn = 0;
                    withdrawStartAmount = currentMoney;

                    // 重新计算新一轮的时间
                    totalWithdrawTime = CalculateWithdrawTime(withdrawStartAmount);
                    mainWithdrawTime = Mathf.Max(0.1f, totalWithdrawTime - smoothEndDuration);
                    actualSmoothDuration = Mathf.Min(smoothEndDuration, totalWithdrawTime * 0.3f);
                    continue;
                }
            }

            elapsedTime += Time.deltaTime;
            yield return null;
        }

        isWithdrawing = false;
        withdrawCoroutine = null;
    }

    public int GetCurrentMoney() => currentMoney;

    public void ClearMoney()
    {
        currentMoney = 0;
        UpdateMoneyText();
    }

    private void HandlePlayerInput()
    {
        if (currentMoney <= 0) return;

        if (Input.GetKeyDown(interactKey))
        {
            int firstWithdraw = WithdrawMoney(Mathf.Max(1, Mathf.CeilToInt(currentMoney * 0.1f)));

            if (currentMoney > 0)
                StartWithdrawing();
        }

        if (Input.GetKeyUp(interactKey))
        {
            StopWithdrawing();
        }
    }

    private void OnPlayerEnterRange()
    {
        if (moneyLight != null) moneyLight.enabled = true;
    }

    private void OnPlayerExitRange()
    {
        StopWithdrawing();
        if (moneyLight != null) moneyLight.enabled = false;
    }

    public string FormatMoney(int amount)
    {
        if (!useCompactNotation)
        {
            return amount.ToString("N0");
        }

        if (amount < 1000)
        {
            return amount.ToString("N0");
        }

        int unitIndex = 0;
        double value = amount;

        while (value >= 1000.0 && unitIndex < MoneyUnits.Length - 1)
        {
            value /= 1000.0;
            unitIndex++;
        }

        StringBuilder format = new StringBuilder("0");

        bool shouldShowDecimal = (value < 10 && value != Mathf.Floor((float)value)) ||
                                (showDecimalForSmallValues && value < 1000);

        if (shouldShowDecimal && decimalPlaces > 0)
        {
            format.Append(".");
            for (int i = 0; i < decimalPlaces; i++)
            {
                format.Append("#");
            }
        }

        return value.ToString(format.ToString()) + MoneyUnits[unitIndex];
    }

    private void UpdateMoneyText()
    {
        if (moneyText != null)
        {
            moneyText.text = FormatMoney(currentMoney);

            if (currentMoney == 0)
                moneyText.color = Color.white;
            else if (currentMoney < 100)
                moneyText.color = Color.white;
            else if (currentMoney < 1000)
                moneyText.color = Color.yellow;
            else
                moneyText.color = new Color(1f, 0.5f, 0f);
        }
    }

    private void PlayDepositEffects()
    {
        if (moneyParticles != null)
        {
            var main = moneyParticles.main;
            main.maxParticles = Mathf.Min(currentMoney / 10, 100);
            moneyParticles.Play();
        }

        if (moneyLight != null && !moneyLight.enabled)
        {
            moneyLight.enabled = true;
            StartCoroutine(FadeOutLight(1f));
        }
    }

    private IEnumerator FadeOutLight(float duration)
    {
        float timer = duration;
        float startIntensity = moneyLight.intensity;

        while (timer > 0)
        {
            timer -= Time.deltaTime;
            moneyLight.intensity = Mathf.Lerp(0, startIntensity, timer / duration);
            yield return null;
        }

        if (!isPlayerInRange) moneyLight.enabled = false;
        moneyLight.intensity = startIntensity;
    }

    private void StartBounce()
    {
        StopDepositPulse();
        if (isBouncing) return;
        if (bounceCoroutine != null) StopCoroutine(bounceCoroutine);
        bounceCoroutine = StartCoroutine(BounceAnimation());
    }

    private void NotifyDepositPulseExtend()
    {
        depositPulseActiveUntil = Mathf.Max(depositPulseActiveUntil, Time.time + Mathf.Max(0.05f, depositPulseExtendSeconds));
        if (depositPulseCoroutine == null)
            depositPulseCoroutine = StartCoroutine(DepositPulseRoutine());
    }

    private void StopDepositPulse()
    {
        depositPulseActiveUntil = 0f;
        if (depositPulseCoroutine != null)
        {
            StopCoroutine(depositPulseCoroutine);
            depositPulseCoroutine = null;
        }
        if (!isBouncing)
            transform.localScale = originalScale;
    }

    private IEnumerator DepositPulseRoutine()
    {
        float cycle = Mathf.Max(0.08f, depositPulseCycleSeconds);
        float amp = Mathf.Clamp(depositPulseAmplitude, 0.01f, 0.35f);

        while (Time.time < depositPulseActiveUntil)
        {
            float t = Time.time * (2f * Mathf.PI / cycle);
            float mul = 1f + amp * Mathf.Sin(t);
            transform.localScale = originalScale * mul;
            yield return null;
        }

        if (!isBouncing)
            transform.localScale = originalScale;
        depositPulseCoroutine = null;
    }

    private IEnumerator BounceAnimation()
    {
        isBouncing = true;
        float timer = 0f;
        Vector3 startScale = transform.localScale;
        Vector3 targetScale = originalScale * (1f + bounceHeight);

        while (timer < bounceDuration / 2f)
        {
            timer += Time.deltaTime;
            transform.localScale = Vector3.Lerp(startScale, targetScale, timer / (bounceDuration / 2f));
            yield return null;
        }
        timer = 0f;
        while (timer < bounceDuration / 2f)
        {
            timer += Time.deltaTime;
            transform.localScale = Vector3.Lerp(targetScale, originalScale, timer / (bounceDuration / 2f));
            yield return null;
        }

        transform.localScale = originalScale;
        isBouncing = false;
    }

    private void UpdateBounceAnimation()
    {
        if (isBouncing && bounceCoroutine == null)
            bounceCoroutine = StartCoroutine(BounceAnimation());
    }

    private void OnGUI()
    {
        if (debugMode)
        {
            GUILayout.BeginArea(new Rect(10, 10, 300, 400));
            GUILayout.Label("金额简写测试:");
            GUILayout.Label($"0: {FormatMoney(0)}");
            GUILayout.Label($"999: {FormatMoney(999)}");
            GUILayout.Label($"1000: {FormatMoney(1000)}");
            GUILayout.Label($"1500: {FormatMoney(1500)}");
            GUILayout.Label($"999999: {FormatMoney(999999)}");
            GUILayout.Label($"1000000: {FormatMoney(1000000)}");
            GUILayout.Label($"1500000: {FormatMoney(1500000)}");
            GUILayout.Label($"1000000000: {FormatMoney(1000000000)}");
            GUILayout.Label($"1500000000: {FormatMoney(1500000000)}");
            GUILayout.EndArea();
        }
    }
}