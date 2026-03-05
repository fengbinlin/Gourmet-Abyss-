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
    public float consumeTime = 2f;          // 消耗时间（秒）
    public plateState currentState = plateState.unUsed;

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

    void Start()
    {

        UpdateUI();
    }

    // 尝试添加菜肴到菜碟
    public bool TryAddDish(DishRecipe recipe, Pot pot = null)
    {
        // 检查菜碟是否可用
        if (currentState == plateState.isUsed &&
            (currentDish == null || currentDish.recipe.dishName != recipe.dishName))
        {
            Debug.LogWarning($"菜碟已装有其他菜：{currentDish?.recipe.dishName}");
            return false;
        }

        // 检查容量
        if (currentDish != null && currentDish.currentAmount >= maxCapacity)
        {
            Debug.LogWarning($"菜碟容量已满：{currentDish.currentAmount}/{maxCapacity}");
            return false;
        }

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
        Debug.Log($"成功添加菜肴到菜碟：{recipe.dishName}，当前数量：{currentDish.currentAmount}");

        return true;
    }

    // 开始消耗菜肴
    public void StartConsume()
    {
        if (currentState == plateState.unUsed || currentDish == null || currentDish.IsEmpty())
        {
            Debug.LogWarning("菜碟为空，无法消耗");
            return;
        }

        if (isConsuming)
        {
            Debug.LogWarning("正在消耗中...");
            return;
        }

        consumeCoroutine = StartCoroutine(ConsumeCoroutine());
    }

    // 消耗菜肴协程
    private IEnumerator ConsumeCoroutine()
    {
        isConsuming = true;
        Debug.Log($"开始消耗菜肴：{currentDish.recipe.dishName}");

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

            Debug.Log($"消耗成功！获得金币：{goldEarned}");

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
        Debug.Log($"获得金币：{amount}");

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

        isConsuming = false;



        Debug.Log("取消消耗菜肴");
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
        if (currentState == plateState.unUsed)
            return true;

        if (currentState == plateState.isUsed &&
            currentDish != null &&
            currentDish.recipe.dishName == recipe.dishName &&
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