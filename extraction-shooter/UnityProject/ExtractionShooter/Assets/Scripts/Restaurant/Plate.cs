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

    // 尝试添加菜肴到菜碟
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
                yield return null;
            }

            if (currentDish == null || currentDish.IsEmpty())
                break;

            float unitPrice = currentDish.recipe.baseDishPrice;
            int consumed = currentDish.Consume(1);
            int goldAmount = Mathf.RoundToInt(unitPrice * consumed);
            Transform startTf = transform;
            Transform moneyBox = CustomerManager.instance != null ? CustomerManager.instance.moneyBoxTransform : null;
            if (goldAmount > 0 && CustomerManager.instance != null)
                yield return StartCoroutine(SpawnMoneySmoothlyForSell(goldAmount, startTf, moneyBox));

            if (currentDish != null && currentDish.IsEmpty())
            {
                currentDish = null;
                currentState = plateState.unUsed;
                sourcePot = null;
            }
            UpdateUI();

            if (IsRestaurantPrimarySellPlate() && IsPlateEmpty())
                RestaurantPanel.instance?.CompactPlatesLeftPreserveOrder();
        }

    EndRoutine:
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
                () => { MoneyChest.Instance.AddMoney(capture); }
            );
            yield return new WaitForSeconds(spawnInterval);
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