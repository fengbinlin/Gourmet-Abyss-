using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
[System.Serializable]
public enum potState
{
    unUsed, //未被使用
    Used, //被占用
}
public enum potType
{
    saucepan,//炖锅
    skillet, //平底煎锅
    hotpot //火锅
}

[System.Serializable]
public class DishRecipe
{
    public int dishID;                      //菜ID
    public string dishName;                 // 菜名
    public Sprite dishIcon;                 // 菜图标
    public List<DishIngredient> ingredients = new List<DishIngredient>();  // 所需食材
    public List<potType> acceptablePot = new List<potType>(); // 可以接受的锅
    public float cookTime = 10f;            // 烹饪时间（秒）
    [Tooltip("装盘后每份菜肴自动售卖的间隔（秒，基础值）。实际时间 = sellTime / 商店 sellTimeMultiplier")]
    public float sellTime = 5f;
    public float baseDishPrice = 1;         // 基本菜价格
    public DishCategory category = DishCategory.MainCourse; // 菜肴分类
    public bool locked = true;              // 是否锁定（未解锁的菜不显示/不可用）
}

// 添加菜肴分类枚举
public enum DishCategory
{
    Appetizer,      // 前菜
    MainCourse,     // 主菜
    Dessert,        // 甜品
    Drink,          // 饮品
    Snack           // 小吃
}

[System.Serializable]
public class DishIngredient
{
    public ResourceType resourceType;       // 食材类型
    public int requiredCount;               // 所需数量
}
public class dishItemPrefabs : MonoBehaviour
{
    public Text disName;
    public Image dishItem;
    public Transform dishFoodParent;

    // 添加字段来存储菜谱数据
    public DishRecipe recipeData;

    // Start is called before the first frame update
    void Start()
    {

    }

    // 设置菜谱数据
    public void SetRecipeData(DishRecipe recipe)
    {
        recipeData = recipe;
    }

    public void OnDishItemClicked()
    {
        if (recipeData == null)
        {
            Debug.LogWarning("菜谱数据为空！");
            return;
        }

        if (RestaurantPanel.instance == null)
        {
            Debug.LogError("RestaurantPanel.instance 未初始化");
            return;
        }

        if (RestaurantPanel.instance.TryEnqueueDishForCooking(recipeData))
            Debug.Log($"已加入烹饪队列：{recipeData.dishName}");
        else
            RefreshUI();
    }

    // 刷新UI
    private void RefreshUI()
    {
        if (RestaurantPanel.instance != null)
        {
            RestaurantPanel.instance.GenerateFoodItems();

            // 如果还需要刷新菜谱列表中的食材显示
            RestaurantPanel.instance.GenerateDishList();
        }
    }

    // Update is called once per frame
    void Update()
    {
        // 可以在这里添加更新逻辑，如显示烹饪进度等
    }
}