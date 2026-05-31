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
    [Tooltip("顾客就餐时长参考（秒）；实际以菜谱 sellTime 为准")]
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

    void Start()
    {
        UpdateUI();
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

    /// <summary>顾客就餐结束后消耗一份；若碟子变空则触发左移补位。</summary>
    public bool TryConsumeOneServing(out int goldEarned)
    {
        goldEarned = 0;
        if (IsPlateEmpty() || currentDish.recipe == null)
            return false;

        int consumed = currentDish.Consume(1);
        if (consumed <= 0)
            return false;

        goldEarned = Mathf.RoundToInt(currentDish.recipe.baseDishPrice * consumed);

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

    public int GetRemainingCapacity()
    {
        if (currentState == plateState.unUsed || currentDish == null)
            return maxCapacity;

        return maxCapacity - currentDish.currentAmount;
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
}
