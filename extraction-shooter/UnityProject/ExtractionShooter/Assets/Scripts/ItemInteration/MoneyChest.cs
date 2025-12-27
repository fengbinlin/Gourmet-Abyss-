using UnityEngine;
using UnityEngine.UI;
using System.Collections;

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

        if (depositSound != null)
            audioSource.PlayOneShot(depositSound);

        PlayDepositEffects();
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
        float smoothStartTime = mainWithdrawTime;

        // ✅ 按下的第一帧就会执行这个 while，所以第一次取钱和后面相差一帧，没有前摇
        while (isWithdrawing && elapsedTime < baseWithdrawTime && currentMoney > 0)
        {
            // **注意**：第一次 elapsedTime = 0，这里就会取到第一笔钱
            if (elapsedTime <= mainWithdrawTime)
            {
                float t = Mathf.Clamp01(elapsedTime / mainWithdrawTime);
                float eased = t * t * (3f - 2f * t);
                int targetAmount = Mathf.RoundToInt(totalWithdrawAmount * Mathf.Lerp(0f, 0.9f, eased));
                int amountToWithdraw = targetAmount - alreadyWithdrawn;
                if (amountToWithdraw > 0)
                    alreadyWithdrawn += WithdrawMoney(amountToWithdraw);
            }
            else
            {
                float smoothElapsed = elapsedTime - smoothStartTime;
                float smoothT = Mathf.Clamp01(smoothElapsed / smoothEndDuration);
                float eased = 1f - Mathf.Pow(1f - smoothT, 3f);
                int targetAmount = Mathf.RoundToInt(totalWithdrawAmount * Mathf.Lerp(0.9f, 1f, eased));
                int amountToWithdraw = targetAmount - alreadyWithdrawn;
                if (amountToWithdraw > 0)
                    alreadyWithdrawn += WithdrawMoney(amountToWithdraw);
            }

            elapsedTime += Time.deltaTime;
            yield return null;
        }

        // 最后清掉剩下的钱
        while (isWithdrawing && currentMoney > 0)
        {
            alreadyWithdrawn += WithdrawMoney(Mathf.CeilToInt(currentMoney * Time.deltaTime * 5f));
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

    private void UpdateMoneyText()
    {
        if (moneyText != null)
        {
            moneyText.text = $"{currentMoney:N0}";
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
}