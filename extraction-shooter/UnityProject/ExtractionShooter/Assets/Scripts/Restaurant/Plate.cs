using System.Collections;
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

    public void AddAmount(int amount)
    {
        currentAmount += amount;
        totalValue += recipe.baseDishPrice * amount;
    }

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
    public int maxCapacity = 5;
    [Tooltip("顾客就坐用餐时长兜底（秒）；优先使用菜谱 sellTime")]
    public float consumeTime = 2f;
    public plateState currentState = plateState.unUsed;

    [Header("UI组件")]
    public Text dishNameText;
    public Text amountText;
    public SpriteRenderer dishIcon;

    [Header("当前菜肴")]
    public Dish currentDish;

    [Header("烹饪锅引用")]
    public Pot sourcePot;

    private FacilityUnlockable _facilityUnlock;

    public bool IsFacilityUnlocked => _facilityUnlock == null || _facilityUnlock.IsUnlocked;

    private bool _subscribedRestaurantStats;

    private void Awake()
    {
        _facilityUnlock = GetComponent<FacilityUnlockable>();
    }

    private void OnEnable()
    {
        TrySubscribeRestaurantStats();
        TrySubscribeUpgradeManager();
        UpdateUI();
    }

    private void OnDisable()
    {
        UnsubscribeRestaurantStats();
        UnsubscribeUpgradeManager();
    }

    private bool _subscribedUpgradeManager;

    private void Start()
    {
        StartCoroutine(CoWaitAndSubscribe());
        UpdateUI();
    }

    private IEnumerator CoWaitAndSubscribe()
    {
        float timeout = 5f;
        while (timeout > 0f
               && (WeaponStatsManager.Instance == null || RestaurantFacilityUpgradeManager.Instance == null))
        {
            timeout -= Time.deltaTime;
            yield return null;
        }

        TrySubscribeRestaurantStats();
        TrySubscribeUpgradeManager();
        UpdateUI();
    }

    private void TrySubscribeRestaurantStats()
    {
        if (_subscribedRestaurantStats || WeaponStatsManager.Instance == null)
            return;

        WeaponStatsManager.Instance.OnRestaurantStatsChanged += HandleRestaurantStatsChanged;
        _subscribedRestaurantStats = true;
    }

    private void UnsubscribeRestaurantStats()
    {
        if (WeaponStatsManager.Instance != null && _subscribedRestaurantStats)
            WeaponStatsManager.Instance.OnRestaurantStatsChanged -= HandleRestaurantStatsChanged;
        _subscribedRestaurantStats = false;
    }

    private void TrySubscribeUpgradeManager()
    {
        if (_subscribedUpgradeManager || RestaurantFacilityUpgradeManager.Instance == null)
            return;

        RestaurantFacilityUpgradeManager.Instance.OnFacilityLevelChanged += HandleFacilityLevelChanged;
        _subscribedUpgradeManager = true;
    }

    private void UnsubscribeUpgradeManager()
    {
        if (RestaurantFacilityUpgradeManager.Instance != null && _subscribedUpgradeManager)
            RestaurantFacilityUpgradeManager.Instance.OnFacilityLevelChanged -= HandleFacilityLevelChanged;
        _subscribedUpgradeManager = false;
    }

    private void HandleRestaurantStatsChanged()
    {
        UpdateUI();
    }

    private void HandleFacilityLevelChanged(RestaurantFacilityUpgradeType type, int _)
    {
        if (type == RestaurantFacilityUpgradeType.ServingCounter)
            UpdateUI();
    }

    public int EffectiveMaxCapacity
    {
        get
        {
            if (WeaponStatsManager.Instance != null)
                return Mathf.Max(1, WeaponStatsManager.Instance.restaurantPlateCapacity);
            return Mathf.Max(1, maxCapacity);
        }
    }

    public bool IsPlateEmpty()
    {
        return currentState == plateState.unUsed || currentDish == null || currentDish.IsEmpty();
    }

    public Vector3 GetDishFlyTargetWorldPosition()
    {
        if (dishIcon != null)
        {
            if (dishIcon.gameObject.activeInHierarchy && dishIcon.sprite != null)
                return dishIcon.bounds.center;
            return dishIcon.transform.position;
        }

        SpriteRenderer sr = GetComponentInChildren<SpriteRenderer>(true);
        if (sr != null)
            return sr.bounds.center;
        return transform.position;
    }

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

    public bool TryAddDish(DishRecipe recipe, Pot pot = null)
    {
        if (!IsFacilityUnlocked)
            return false;
        if (!CanAddDish(recipe))
            return false;

        if (currentDish == null || currentDish.currentAmount == 0)
            currentDish = new Dish(recipe, 1);
        else
            currentDish.AddAmount(1);

        currentState = plateState.isUsed;
        if (pot != null)
            sourcePot = pot;

        UpdateUI();
        return true;
    }

    /// <summary>顾客从碟子取走一份（端菜时扣减）；若碟子变空则触发左移补位。</summary>
    public bool TryConsumeOneServing(out int goldEarned)
    {
        goldEarned = 0;
        if (!IsFacilityUnlocked)
            return false;
        if (IsPlateEmpty() || currentDish.recipe == null)
            return false;

        int consumed = currentDish.Consume(1);
        if (consumed <= 0)
            return false;

        goldEarned = WeaponStatsManager.Instance != null
            ? WeaponStatsManager.Instance.CalcRestaurantSellGold(currentDish.recipe.baseDishPrice, consumed)
            : Mathf.RoundToInt(currentDish.recipe.baseDishPrice * consumed);

        if (currentDish.IsEmpty())
        {
            currentDish = null;
            currentState = plateState.unUsed;
            sourcePot = null;
            RestaurantPanel.instance?.CompactPlatesLeftPreserveOrder();
        }

        UpdateUI();
        return true;
    }

    public bool CanAddDish(DishRecipe recipe)
    {
        if (!IsFacilityUnlocked)
            return false;
        if (recipe == null) return false;

        if (currentState == plateState.unUsed || currentDish == null || currentDish.currentAmount == 0)
            return true;

        if (currentState == plateState.isUsed &&
            currentDish != null &&
            RestaurantPanel.RecipesMatch(currentDish.recipe, recipe) &&
            currentDish.currentAmount < EffectiveMaxCapacity)
            return true;

        return false;
    }

    public int GetRemainingCapacity()
    {
        int capacity = EffectiveMaxCapacity;
        if (currentState == plateState.unUsed || currentDish == null)
            return capacity;

        return capacity - currentDish.currentAmount;
    }

    public string GetCurrentDishName()
    {
        return currentDish?.recipe?.dishName ?? "空";
    }

    private void UpdateUI()
    {
        if (currentDish != null)
        {
            if (dishNameText != null)
                dishNameText.text = currentDish.recipe.dishName;

            if (amountText != null)
                amountText.text = $"{currentDish.currentAmount}/{EffectiveMaxCapacity}";

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
                amountText.text = $"0/{EffectiveMaxCapacity}";

            if (dishIcon != null)
                dishIcon.gameObject.SetActive(false);
        }
    }
}
