using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
public enum plateState
{
    isUsed,
    unUsed,
}


[System.Serializable]
public class Dish
{
    public DishRecipe recipe;      // 菜肴配方
    public int currentAmount;      // 当前数量
    public float totalValue;       // 总价值

    public Dish(DishRecipe recipe, int amount = 1)
    {
        this.recipe = recipe;
        this.currentAmount = amount;
        this.totalValue = recipe.baseDishPrice * amount;
    }

    // 添加菜肴
    public void AddAmount(int amount)
    {
        currentAmount += amount;
        totalValue += recipe.baseDishPrice * amount;
    }

    // 消耗菜肴
    public int Consume(int amountToConsume)
    {
        int actualConsume = Mathf.Min(amountToConsume, currentAmount);
        currentAmount -= actualConsume;

        float consumeValue = recipe.baseDishPrice * actualConsume;
        totalValue -= consumeValue;

        return actualConsume;
    }

    public bool IsEmpty()
    {
        return currentAmount <= 0;
    }
}
public class Plate : MonoBehaviour
{
    [Header("菜碟配置")]
    public int maxCapacity = 5;             // 最大容量
    public float consumeTime = 2f;          // 消耗时间（秒）（非首碟或未启用自动售卖时的手动消耗间隔）
    public plateState currentState = plateState.unUsed;

    [Tooltip("自动售出金币飞向钱箱时的表现数量上限")]
    [SerializeField] private int maxVisualCoins = 12;

    [Header("售卖金币飞行轨迹参数（面板配置）")]
    [SerializeField] private float moneyProjectileFlightDuration = 2f;
    [SerializeField] private float moneyProjectileMaxHeight = 5f;

    [Header("售卖表现：盘中物品飞出")]
    [Tooltip("售卖时在盘子位置生成的飞出物（会设置 dishIcon 精灵）。如果为空则退化为原逻辑：直接从盘子生成金币。")]
    [SerializeField] private GameObject dishFlyOnSellPrefab;
    [SerializeField] private float dishFlyOnSellUpDistance = 0.35f;
    [SerializeField] private float dishFlyOnSellMoveDuration = 0.18f;
    [SerializeField] private float dishFlyOnSellScalePulseDuration = 0.14f;
    [SerializeField] private float dishFlyOnSellScaleUp = 1.22f;
    [Tooltip("金币投射器 Spawn 的起点（由你在面板里拖引用）。卖出表现里会把它的位置移动到飞出物的终点。")]
    [SerializeField] private Transform sellMoneyStartRoot;
    [Tooltip("飞出物 prefab 的基础缩放倍率（只影响显示，不影响 Plate 数据）。")]
    [SerializeField] private float dishFlyOnSellBaseScaleMultiplier = 1f;

    [Header("UI组件")]
    public Text dishNameText;               // 菜名显示
    public Text amountText;                 // 数量显示
    public SpriteRenderer dishIcon;                  // 菜图标


    [Header("当前菜肴")]
    public Dish currentDish;               // 当前装的菜肴

    [Header("烹饪锅引用")]
    public Pot sourcePot;                  // 来源锅（用于自动装盘）

    private bool isConsuming = false;
    private Coroutine consumeCoroutine;
    private Coroutine autoSellCoroutine;

    private Vector3 _dishIconPulseBaseLocalScale;
    private Coroutine _sellPulseCoroutine;

    /// <summary>餐厅 <c>platesList[0]</c>：唯一执行自动售卖的碟子；其余碟子可堆放任意菜品（可多盘同一种菜）。</summary>
    public bool IsRestaurantPrimarySellPlate()
    {
        RestaurantPanel panel = RestaurantPanel.instance;
        if (panel == null || panel.platesList == null || panel.platesList.Count == 0)
            return false;
        return panel.platesList[0] == this;
    }

    public bool IsPlateEmpty()
    {
        return currentState == plateState.unUsed || currentDish == null || currentDish.IsEmpty();
    }

    /// <summary>出菜飞行落点世界坐标：优先 <see cref="dishIcon"/> 的位置（与菜图对齐）。</summary>
    public Vector3 GetDishFlyTargetWorldPosition()
    {
        if (dishIcon != null)
        {
            // 空碟时图标常 SetActive(false)，bounds 不可靠，用 transform 更稳
            if (dishIcon.gameObject.activeInHierarchy && dishIcon.sprite != null)
                return dishIcon.bounds.center;
            return dishIcon.transform.position;
        }

        SpriteRenderer sr = GetComponentInChildren<SpriteRenderer>(true);
        if (sr != null)
            return sr.bounds.center;
        return transform.position;
    }

    private void Awake()
    {
        _dishIconPulseBaseLocalScale = dishIcon != null ? dishIcon.transform.localScale : Vector3.one;
    }

    /// <summary>首碟倒计时结束并卖出一份后，仅 dishIcon 缩放反馈。</summary>
    public void PlaySellFeedbackPulse()
    {
        if (!isActiveAndEnabled || dishIcon == null) return;
        if (_sellPulseCoroutine != null)
            StopCoroutine(_sellPulseCoroutine);
        _sellPulseCoroutine = StartCoroutine(CoSellPulse());
    }

    private IEnumerator CoSellPulse()
    {
        Transform pulseTf = dishIcon != null ? dishIcon.transform : null;
        if (pulseTf != null)
            yield return UIFeedbackPulse.CoScalePulse(pulseTf, _dishIconPulseBaseLocalScale, 1.26f, 0.32f);
        _sellPulseCoroutine = null;
    }

    void Start()
    {
        UpdateUI();
        EnsureAutoSellRunning();
    }

    public void StopConsumeOnly()
    {
        if (consumeCoroutine != null)
        {
            StopCoroutine(consumeCoroutine);
            consumeCoroutine = null;
        }
        isConsuming = false;
    }

    /// <summary>仅清空数据字段，不停止自动售卖协程（供首碟补位紧凑使用）。</summary>
    public void ClearDishDataFieldsOnly()
    {
        currentDish = null;
        currentState = plateState.unUsed;
        sourcePot = null;
    }

    public void ApplyContentForRebalance(Dish dish, Pot src)
    {
        currentDish = dish;
        if (dish != null && !dish.IsEmpty())
            currentState = plateState.isUsed;
        else
            currentState = plateState.unUsed;
        sourcePot = src;
    }

    public void RefreshDisplay()
    {
        UpdateUI();
    }

    /// <summary>写入碟子数据与 UI；出锅流程中应在飞行预制体落点后再调用（见 <see cref="Pot"/>）。</summary>
    public bool TryAddDish(DishRecipe recipe, Pot pot = null)
    {
        if (!CanAddDish(recipe))
            return false;

        // 添加或更新菜肴
        if (currentDish == null||currentDish.currentAmount==0)
        {
            currentDish = new Dish(recipe, 1);

        }
        else
        {
            currentDish.AddAmount(1);
        }
        currentState = plateState.isUsed;
        // 记录来源锅（用于动画效果）
        if (pot != null)
        {
            sourcePot = pot;
        }

        UpdateUI();
        EnsureAutoSellRunning();

        return true;
    }

    public void EnsureAutoSellRunning()
    {
        if (!IsRestaurantPrimarySellPlate()) return;
        if (currentDish == null || currentDish.IsEmpty()) return;
        if (autoSellCoroutine != null) return;
        autoSellCoroutine = StartCoroutine(AutoSellRoutine());
    }

    private IEnumerator AutoSellRoutine()
    {
        while (currentDish != null && !currentDish.IsEmpty())
        {
            float baseSell = currentDish.recipe != null ? currentDish.recipe.sellTime : 2f;
            float sellTimeMult = 1f;
            if (WeaponStatsManager.Instance != null)
                sellTimeMult = Mathf.Max(0.01f, WeaponStatsManager.Instance.sellTimeMultiplier);
            float waitSeconds = Mathf.Max(0.01f, baseSell / sellTimeMult);

            float elapsed = 0f;
            while (elapsed < waitSeconds)
            {
                if (currentDish == null || currentDish.IsEmpty())
                    goto EndRoutine;
                elapsed += Time.deltaTime;
                if (IsRestaurantPrimarySellPlate() && RestaurantPanel.instance != null)
                    RestaurantPanel.instance.SetPlateSellProgress(elapsed / waitSeconds);
                yield return null;
            }

            if (IsRestaurantPrimarySellPlate() && RestaurantPanel.instance != null)
                RestaurantPanel.instance.SetPlateSellProgress(1f);

            if (currentDish == null || currentDish.IsEmpty())
                break;

            float unitPrice = currentDish.recipe.baseDishPrice;
            int consumed = currentDish.Consume(1);
            int goldAmount = Mathf.RoundToInt(unitPrice * consumed);

            // 先给 dishIcon 反馈，再生成金币投射器
            PlaySellFeedbackPulse();
            // 记录本次要售卖的“那一份 Dish 引用”
            // 关键：如果在飞出表现阶段期间，有新菜被 TryAddDish 加进来，
            // 则 currentDish 引用会变化；售卖协程结束后不应再把新菜清空。
            Dish dishRef = currentDish;
            bool becameEmpty = dishRef != null && dishRef.IsEmpty();

            // 关键：在飞出物生成前就让“数量减少”在 UI 上可见，
            // 避免用户感觉“要到飞完才扣数量”。
            UpdateUI();

            // 在清空 currentDish 之前先缓存“飞出物”所需信息，避免 UpdateUI 隐藏 dishIcon 导致位置/精灵不一致
            Sprite dishSprite = currentDish?.recipe != null ? currentDish.recipe.dishIcon : null;
            Vector3 sellItemStartPos = GetDishFlyTargetWorldPosition();

            Transform moneyBox = CustomerManager.instance != null ? CustomerManager.instance.moneyBoxTransform : null;
            if (goldAmount > 0 && CustomerManager.instance != null && moneyBox != null)
            {
                // 先飞出物：上移 + scale 波动后销毁，再在销毁点生成金币
                yield return StartCoroutine(CoDishFlyUpThenSpawnMoney(goldAmount, dishSprite, sellItemStartPos, moneyBox));
            }
            else
            {
                // 退化到原逻辑
                Transform startTf = transform;
                yield return StartCoroutine(SpawnMoneySmoothlyForSell(goldAmount, startTf, moneyBox));
            }

            if (becameEmpty)
            {
                // 只有当 currentDish 仍然是售卖前那份引用时，才清空。
                // 否则说明期间已经被新菜替换，保持当前盘子数据不变。
                if (currentDish == dishRef)
                {
                    currentDish = null;
                    currentState = plateState.unUsed;
                    sourcePot = null;
                }
            }
            UpdateUI();

            if (IsRestaurantPrimarySellPlate() && IsPlateEmpty())
                RestaurantPanel.instance?.CompactPlatesLeftPreserveOrder();
        }

    EndRoutine:
        if (IsRestaurantPrimarySellPlate() && RestaurantPanel.instance != null)
            RestaurantPanel.instance.SetPlateSellProgress(0f);
        autoSellCoroutine = null;
    }

    private IEnumerator SpawnMoneySmoothlyForSell(int totalAmount, Transform start, Transform target)
    {
        if (totalAmount <= 0) yield break;

        ProjectileLauncher launcher = CustomerManager.instance != null ? CustomerManager.instance.projectileLauncher : null;
        if (launcher == null || target == null) yield break;

        int numProjectiles = Mathf.Min(totalAmount, Mathf.Max(1, maxVisualCoins));
        int baseAmount = totalAmount / numProjectiles;
        int remainder = totalAmount % numProjectiles;
        float spawnInterval = 0.05f;

        for (int i = 0; i < numProjectiles; i++)
        {
            int amountForThis = baseAmount + (i < remainder ? 1 : 0);
            int capture = amountForThis;
            launcher.SpawnProjectile(
                start,
                target,
                ResourceType.Money,
                capture,
                () => { MoneyChest.Instance.AddMoney(capture); },
                moneyProjectileFlightDuration,
                moneyProjectileMaxHeight
            );
            yield return new WaitForSeconds(spawnInterval);
        }
    }

    private IEnumerator CoDishFlyUpThenSpawnMoney(int goldAmount, Sprite dishSprite, Vector3 platePos, Transform moneyBox)
    {
        if (dishSprite == null || moneyBox == null)
        {
            yield return StartCoroutine(SpawnMoneySmoothlyForSell(goldAmount, transform, moneyBox));
            yield break;
        }

        Vector3 startPos = platePos;
        Vector3 endPos = platePos + Vector3.up * dishFlyOnSellUpDistance;

        // 关键点：让 prefab 在“未激活父物体下”实例化，避免其脚本在实例化瞬间就触发 Awake/OnEnable 造成数据被动改。
        GameObject flyObj = null;
        GameObject inactiveRoot = null;
        try
        {
            if (dishFlyOnSellPrefab != null)
            {
                inactiveRoot = new GameObject("DishFlyOnSellRoot_tmp");
                inactiveRoot.SetActive(false);

                flyObj = Instantiate(dishFlyOnSellPrefab, startPos, Quaternion.identity, inactiveRoot.transform);

                if (flyObj != null)
                {
                    // 在把父物体激活前，把会执行逻辑的脚本全部禁用，只保留渲染相关组件。
                    // SpriteRenderer / UI.Image 不是 MonoBehaviour（或即使是也需要显示），这里仅禁用除它们之外的 MonoBehaviour。
                    var mbs = flyObj.GetComponentsInChildren<MonoBehaviour>(true);
                    for (int i = 0; i < mbs.Length; i++)
                    {
                        MonoBehaviour mb = mbs[i];
                        if (mb == null) continue;

                        if (mb is SpriteRenderer) continue;
                        if (mb is UnityEngine.UI.Image) continue;
                        mb.enabled = false;
                    }

                    // 刚体直接停掉，避免物理再“额外下落段”
                    Rigidbody2D rb2d = flyObj.GetComponent<Rigidbody2D>();
                    if (rb2d != null)
                    {
                        rb2d.velocity = Vector2.zero;
                        rb2d.angularVelocity = 0f;
                        rb2d.gravityScale = 0f;
                        rb2d.isKinematic = true;
                    }

                    // sprite 同步到渲染组件（即使 prefab 里已有旧 sprite，也覆盖）
                    SpriteRenderer sr = flyObj.GetComponentInChildren<SpriteRenderer>(true);
                    if (sr != null)
                    {
                        sr.sprite = dishSprite;
                        if (dishIcon != null)
                        {
                            sr.sortingLayerID = dishIcon.sortingLayerID;
                            sr.sortingOrder = dishIcon.sortingOrder;
                            sr.color = dishIcon.color;
                        }
                    }
                    UnityEngine.UI.Image img = flyObj.GetComponentInChildren<UnityEngine.UI.Image>(true);
                    if (img != null)
                        img.sprite = dishSprite;

                    // 激活后才显示
                    inactiveRoot.SetActive(true);
                }
            }

            if (flyObj == null)
            {
                // prefab 没填时退化为临时 SpriteRenderer（纯视觉）
                flyObj = new GameObject("DishFlyOnSellTemp");
                flyObj.transform.position = startPos;
                flyObj.transform.localScale = (dishIcon != null ? dishIcon.transform.localScale : Vector3.one)
                    * Mathf.Max(0.0001f, dishFlyOnSellBaseScaleMultiplier);

                SpriteRenderer sr = flyObj.AddComponent<SpriteRenderer>();
                sr.sprite = dishSprite;
                if (dishIcon != null)
                {
                    sr.sortingLayerID = dishIcon.sortingLayerID;
                    sr.sortingOrder = dishIcon.sortingOrder;
                    sr.color = dishIcon.color;
                }
            }

            flyObj.transform.position = startPos;

            // 不要强行用 dishIcon 的 localScale 覆盖 prefab 尺寸；
            // 使用 prefab 自身初始缩放作为基准，避免“物体变得很小”。
            Vector3 baseScale = flyObj.transform.localScale * Mathf.Max(0.0001f, dishFlyOnSellBaseScaleMultiplier);
            flyObj.transform.localScale = baseScale;

            float moveDuration = Mathf.Max(0.01f, dishFlyOnSellMoveDuration);
            float elapsed = 0f;
            while (elapsed < moveDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / moveDuration);
                float eased = t * t * (3f - 2f * t); // SmoothStep
                flyObj.transform.position = Vector3.LerpUnclamped(startPos, endPos, eased);
                yield return null;
            }

            flyObj.transform.position = endPos;

            // scale 上波动
            Vector3 peakScale = baseScale * Mathf.Max(1f, dishFlyOnSellScaleUp);
            float pulseDuration = Mathf.Max(0.01f, dishFlyOnSellScalePulseDuration);
            float half = pulseDuration * 0.5f;

            float pulseElapsed = 0f;
            while (pulseElapsed < half)
            {
                pulseElapsed += Time.deltaTime;
                float t = Mathf.Clamp01(pulseElapsed / half);
                flyObj.transform.localScale = Vector3.LerpUnclamped(baseScale, peakScale, t);
                yield return null;
            }

            pulseElapsed = 0f;
            while (pulseElapsed < half)
            {
                pulseElapsed += Time.deltaTime;
                float t = Mathf.Clamp01(pulseElapsed / half);
                flyObj.transform.localScale = Vector3.LerpUnclamped(peakScale, baseScale, t);
                yield return null;
            }

            flyObj.transform.localScale = baseScale;

            // 用你指定的 Transform 作为金币起点：只移动位置，不额外创建临时 root
            Transform moneyStart = flyObj.transform;
            if (sellMoneyStartRoot != null)
            {
                sellMoneyStartRoot.position = flyObj.transform.position;
                moneyStart = sellMoneyStartRoot;
            }

            // 等金币生成完成再销毁飞出物
            yield return StartCoroutine(SpawnMoneySmoothlyForSell(goldAmount, moneyStart, moneyBox));
        }
        finally
        {
            if (flyObj != null)
                Destroy(flyObj);
            if (inactiveRoot != null)
                Destroy(inactiveRoot);
        }
    }

    // 开始消耗菜肴
    public void StartConsume()
    {
        if (IsRestaurantPrimarySellPlate())
            return;

        if (currentState == plateState.unUsed || currentDish == null || currentDish.IsEmpty())
        {
            //Debug.LogWarning("菜碟为空，无法消耗");
            return;
        }

        if (isConsuming)
        {
            //Debug.LogWarning("正在消耗中...");
            return;
        }

        consumeCoroutine = StartCoroutine(ConsumeCoroutine());
    }

    // 消耗菜肴协程
    private IEnumerator ConsumeCoroutine()
    {
        isConsuming = true;
        //Debug.Log($"开始消耗菜肴：{currentDish.recipe.dishName}");

        float elapsedTime = 0f;

        while (elapsedTime < consumeTime)
        {
            elapsedTime += Time.deltaTime;



            yield return null;
        }

        // 消耗完成
        OnConsumeComplete();
    }

    // 消耗完成
    private void OnConsumeComplete()
    {
        if (currentDish != null && !currentDish.IsEmpty())
        {
            // 消耗一份菜肴
            int consumedAmount = currentDish.Consume(1);

            // 获得金币
            float goldEarned = currentDish.recipe.baseDishPrice * consumedAmount;
            EarnGold(goldEarned);

            //Debug.Log($"消耗成功！获得金币：{goldEarned}");

            // 如果菜肴已空，清空菜碟
            if (currentDish.IsEmpty())
            {
                currentDish = null;
                currentState = plateState.unUsed;
                sourcePot = null;
            }



            UpdateUI();
        }

        isConsuming = false;
    }

    // 获得金币
    private void EarnGold(float amount)
    {
        // 这里需要根据你的金币系统来实现
        // 例如：GameValManager.Instance.AddGold(amount);
       // Debug.Log($"获得金币：{amount}");

        // 触发金币获得事件
        // EventManager.Instance.TriggerEvent("GoldEarned", amount);
    }

    // 取消消耗
    public void CancelConsume()
    {
        if (consumeCoroutine != null)
        {
            StopCoroutine(consumeCoroutine);
            consumeCoroutine = null;
        }

        if (autoSellCoroutine != null)
        {
            StopCoroutine(autoSellCoroutine);
            autoSellCoroutine = null;
        }

        isConsuming = false;

        //Debug.Log("取消消耗菜肴");
    }

    private void OnDestroy()
    {
        CancelConsume();
    }

    // 更新UI显示
    private void UpdateUI()
    {
        if (currentDish != null)
        {
            if (dishNameText != null)
                dishNameText.text = currentDish.recipe.dishName;

            if (amountText != null)
                amountText.text = $"{currentDish.currentAmount}/{maxCapacity}";

            if (dishIcon != null && currentDish.recipe.dishIcon != null)
            {
                dishIcon.sprite = currentDish.recipe.dishIcon;
                dishIcon.gameObject.SetActive(true);
            }
        }
        else
        {
            if (dishNameText != null)
                dishNameText.text = "空碟";

            if (amountText != null)
                amountText.text = $"0/{maxCapacity}";

            if (dishIcon != null)
                dishIcon.gameObject.SetActive(false);
        }


    }

    // 检查是否可以添加菜肴
    public bool CanAddDish(DishRecipe recipe)
    {
        if (recipe == null) return false;

        if (currentState == plateState.unUsed || currentDish == null || currentDish.currentAmount == 0)
            return true;

        if (currentState == plateState.isUsed &&
            currentDish != null &&
            RestaurantPanel.RecipesMatch(currentDish.recipe, recipe) &&
            currentDish.currentAmount < maxCapacity)
            return true;

        return false;
    }

    // 获取剩余容量
    public int GetRemainingCapacity()
    {
        if (currentState == plateState.unUsed || currentDish == null)
            return maxCapacity;

        return maxCapacity - currentDish.currentAmount;
    }

    // 获取当前菜肴类型
    public string GetCurrentDishName()
    {
        return currentDish?.recipe?.dishName ?? "空";
    }

    void Update()
    {
        // 可以在这里添加更新逻辑
    }
}