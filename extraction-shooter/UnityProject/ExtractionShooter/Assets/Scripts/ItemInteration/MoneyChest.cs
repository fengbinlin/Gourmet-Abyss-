using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Text; // 用于StringBuilder

public class MoneyChest : MonoBehaviour
{
    [Header("存钱箱设置")]
    [SerializeField] private int currentMoney = 0;
    [SerializeField] private float bounceHeight = 0.2f;
    [SerializeField] private float bounceDuration = 0.3f;

    [Header("取钱设置")]
    [SerializeField] private float baseWithdrawTime = 2.0f;
    [SerializeField] private float minWithdrawTime = 1.0f;
    [SerializeField] private float maxWithdrawTime = 5.0f;
    [SerializeField] private float smoothEndDuration = 0.5f;

    [Header("UI设置")]
    [SerializeField] private GameObject moneyTextPrefab;
    [SerializeField] private Vector3 textOffset = new Vector3(0, 2f, 0);
    [SerializeField] private bool useCompactNotation = true; // 是否使用简写
    [SerializeField] private int decimalPlaces = 1; // 小数位数
    [SerializeField] private bool showDecimalForSmallValues = false; // 小数值是否显示小数

    [Header("交互设置")]
    [SerializeField] private KeyCode interactKey = KeyCode.E;

    [Header("音效")]
    [SerializeField] private AudioClip depositSound;
    [SerializeField] private AudioClip withdrawSound;

    [Header("视觉效果")]
    [SerializeField] private ParticleSystem moneyParticles;
    [SerializeField] private Light moneyLight;

    [Header("发射器引用")]
    [SerializeField] private ProjectileLauncher projectileLauncher;
    [SerializeField] private Transform playerTransform;

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

    private int totalWithdrawAmount = 0;
    private int alreadyWithdrawn = 0;

    public delegate void MoneyChangedHandler(int newAmount, int changeAmount);
    public event MoneyChangedHandler OnMoneyChanged;

    public static MoneyChest Instance { get; private set; }

    // 货币单位定义
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

    private bool moneyJustAdded = false;

    public void AddMoney(int amount)
    {
        if (amount <= 0) return;
        currentMoney += amount;
        UpdateMoneyText();
        PlayDepositEffects();

        OnMoneyChanged?.Invoke(currentMoney, amount);

        if (isWithdrawing)
            moneyJustAdded = true;
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
                }
            );
        }
        return actualAmount;
    }

    private void StartWithdrawing()
    {
        if (isWithdrawing || currentMoney <= 0) return;

        isWithdrawing = true;
        alreadyWithdrawn = 0;
        totalWithdrawAmount = currentMoney;
        baseWithdrawTime = Mathf.Clamp(baseWithdrawTime, minWithdrawTime, maxWithdrawTime);

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
        float mainWithdrawTime = baseWithdrawTime - smoothEndDuration;

        while (isWithdrawing && currentMoney > 0)
        {
            // 当前总金额始终等于已经取到 + 还剩的钱
            int totalNow = alreadyWithdrawn + currentMoney;

            // 按当前总金额算主阶段还是结束阶段
            if (elapsedTime <= mainWithdrawTime)
            {
                float t = Mathf.Clamp01(elapsedTime / mainWithdrawTime);
                float eased = t * t * (3f - 2f * t); // easeInOut
                int targetAmount = Mathf.RoundToInt(totalNow * Mathf.Lerp(0f, 0.9f, eased));
                int amountToWithdraw = targetAmount - alreadyWithdrawn;

                if (amountToWithdraw > 0)
                    alreadyWithdrawn += WithdrawMoney(amountToWithdraw);
            }
            else
            {
                // smoothEnd
                float smoothElapsed = elapsedTime - mainWithdrawTime;
                float smoothT = Mathf.Clamp01(smoothElapsed / smoothEndDuration);
                float eased = 1f - Mathf.Pow(1f - smoothT, 3f);
                int targetAmount = Mathf.RoundToInt(totalNow * Mathf.Lerp(0.9f, 1f, eased));
                int amountToWithdraw = targetAmount - alreadyWithdrawn;

                if (amountToWithdraw > 0)
                    alreadyWithdrawn += WithdrawMoney(amountToWithdraw);
            }

            // 时间推进
            elapsedTime += Time.deltaTime;
            yield return null;

            // 当中途加钱时，直接重置 elapsedTime，让曲线重新开始
            if (moneyJustAdded)
            {
                elapsedTime = 0f;
                moneyJustAdded = false;
            }
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
            // 按下当帧立即取一次钱，比如一次取 10% 或最少 1 块
            int firstWithdraw = WithdrawMoney(Mathf.Max(1, Mathf.CeilToInt(currentMoney * 0.1f)));

            // 如果仍有钱，进入持续取钱状态
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

    /// <summary>
    /// 格式化金额显示，支持简写（如K、M、B、T等）
    /// </summary>
    /// <param name="amount">金额数值</param>
    /// <returns>格式化后的字符串</returns>
    public string FormatMoney(int amount)
    {
        if (!useCompactNotation)
        {
            // 如果不使用简写，直接返回逗号分隔的数字
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

        // 构建格式化字符串
        StringBuilder format = new StringBuilder("0");
        
        // 如果值小于10或者需要显示小数，则添加小数位
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
            // 使用新的格式化方法
            moneyText.text = FormatMoney(currentMoney);
            
            // 颜色设置保持不变
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
        if (isBouncing) return;
        if (bounceCoroutine != null) StopCoroutine(bounceCoroutine);
        bounceCoroutine = StartCoroutine(BounceAnimation());
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
    
    // 调试用：显示简写示例
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