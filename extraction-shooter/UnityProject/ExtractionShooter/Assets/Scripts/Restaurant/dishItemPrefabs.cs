using System.Collections;
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
    public float baseDishPrice = 1;         // 基本菜价格
    public DishCategory category = DishCategory.MainCourse; // 菜肴分类
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

    // 点击菜谱项的处理函数
    // 点击菜谱项的处理函数
    public void OnDishItemClicked()
    {
        if (recipeData == null)
        {
            Debug.LogWarning("菜谱数据为空！");
            return;
        }

        // 检查食材是否足够
        if (!CheckIngredientsAvailable(recipeData))
        {
            Debug.Log("食材不足，无法烹饪：" + recipeData.dishName);
            return;
        }

        // 查找空闲的锅
        Pot availablePot = FindAvailablePot(recipeData.acceptablePot);
        if (availablePot == null)
        {
            Debug.Log("没有可用的锅：" + recipeData.dishName);
            return;
        }

        // 扣除食材
        if (!ConsumeIngredients(recipeData))
        {
            Debug.Log("扣除食材失败：" + recipeData.dishName);
            return;
        }

        // 开始烹饪前的视觉效果：生成食材实例落入锅中
        RestaurantPanel.instance.StartCoroutine(SpawnIngredientInstances(recipeData, availablePot));

        // 开始烹饪（通过Pot类管理）
        bool success = availablePot.StartCooking(recipeData);

        if (success)
        {
            Debug.Log($"成功开始烹饪：{recipeData.dishName}");

            // 刷新UI
            RefreshUI();
        }
        else
        {
            Debug.LogWarning($"开始烹饪失败：{recipeData.dishName}");
        }
    }

    // 生成食材实例效果
    private IEnumerator SpawnIngredientInstances(DishRecipe recipe, Pot pot)
    {
        Debug.Log($"  - 所需食材数量: {recipeData.ingredients.Count}");
        for (int j = 0; j < recipeData.ingredients.Count; j++)
        {
            DishIngredient ingredient = recipeData.ingredients[j];
            Debug.Log($"    - 食材 {j}: 类型={ingredient.resourceType}, 需要数量={ingredient.requiredCount}");

            // 检查GameValManager中是否有对应的资源
            ResourceItem resItem = GameValManager.Instance.resources.Find(r => r.type == ingredient.resourceType);
            Debug.Log($"      当前拥有数量: {(resItem != null ? resItem.count.ToString() : "资源未找到")}");
        }
        if (RestaurantPanel.instance == null || RestaurantPanel.instance.ingredientInstancePrefab == null)
        {
            Debug.LogWarning("食材实例预制体未设置！");
            yield break;
        }

        foreach (DishIngredient ingredient in recipeData.ingredients)
        {
            //print("AAA");
            // 获取食材的图标
            ResourceItem resource = GameValManager.Instance.resources.Find(r => r.type == ingredient.resourceType);
            if (resource == null || resource.Icon == null)
            {
                Debug.LogWarning($"找不到食材 {ingredient.resourceType} 的图标");
                continue;
            }

            // 根据需求数量生成多个实例
            for (int i = 0; i < ingredient.requiredCount; i++)
            {
                //print("BBB");
                // 实例化食材预制体
                Transform spawnParent = RestaurantPanel.instance.ingredientSpawnParent != null ?
                    RestaurantPanel.instance.ingredientSpawnParent : pot.transform;

                GameObject instance = Instantiate(
                    RestaurantPanel.instance.ingredientInstancePrefab

                );

                // 获取控制器并初始化
                IngredientInstanceController controller = instance.GetComponent<IngredientInstanceController>();
                if (controller != null)
                {
                    controller.Initialize(ingredient.resourceType, pot, resource.Icon);
                }

                // 添加随机延迟，让食材依次下落
                yield return new WaitForSeconds(Random.Range(0.1f, 0.3f));
            }
        }
    }
    // 检查食材是否足够
    private bool CheckIngredientsAvailable(DishRecipe recipe)
    {
        if (GameValManager.Instance == null)
        {
            Debug.LogError("GameValManager.Instance 未初始化！");
            return false;
        }

        foreach (DishIngredient ingredient in recipe.ingredients)
        {
            ResourceItem resource = GameValManager.Instance.resources.Find(r => r.type == ingredient.resourceType);

            if (resource == null || resource.count < ingredient.requiredCount)
            {
                return false;
            }
        }

        return true;
    }

    // 查找空闲的锅
    private Pot FindAvailablePot(List<potType> acceptablePotTypes)
    {
        if (RestaurantPanel.instance == null)
        {
            Debug.LogError("RestaurantPanel.instance 未初始化！");
            return null;
        }

        foreach (Pot pot in RestaurantPanel.instance.potsList)
        {
            if (pot.IsAvailable() && acceptablePotTypes.Contains(pot.potType))
            {
                return pot;
            }
        }

        return null;
    }

    // 扣除食材
    private bool ConsumeIngredients(DishRecipe recipe)
    {
        if (GameValManager.Instance == null)
        {
            return false;
        }

        foreach (DishIngredient ingredient in recipe.ingredients)
        {
            ResourceItem resource = GameValManager.Instance.resources.Find(r => r.type == ingredient.resourceType);

            if (resource != null)
            {
                resource.count -= ingredient.requiredCount;

                // 防止负数
                if (resource.count < 0)
                {
                    resource.count = 0;
                    return false; // 扣除失败
                }
            }
        }

        return true;
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