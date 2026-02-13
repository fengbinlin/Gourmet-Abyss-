using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class RestaurantPanel : MonoBehaviour
{
    public static RestaurantPanel instance;

    [Header("食材背包UI配置")]
    public GameObject foodItemPrefabs;
    public Transform foodItemParent;
    public Text foodInformationTitle;
    public Text foodInformationDescription;

    [Header("菜单UI配置")]
    public GameObject dishItemPrefabs;       // 菜的预制体
    public GameObject dishFoodItemPrefabs;   // 菜里显示食材的预制体
    public Transform dishParent;             // 菜列表的父物体

    [Header("菜单数据")]
    public List<DishRecipe> dishRecipes = new List<DishRecipe>();

    private List<GameObject> currentFoodItems = new List<GameObject>();
    private List<GameObject> currentDishes = new List<GameObject>();
    void Awake()
    {
        instance = this;
    }
    /// <summary>
    /// 根据 GameValManager 中的资源生成UI
    /// </summary>

    void Start()
    {
        GenerateFoodItems();
        GenerateDishList();
    }
    public void GenerateFoodItems()
    {
        // 先清空旧UI
        ClearFoodItems();

        if (GameValManager.Instance == null)
        {
            Debug.LogError("GameValManager.Instance 未初始化！");
            return;
        }

        // 遍历资源列表
        foreach (var item in GameValManager.Instance.resources)
        {
            // 只显示食物类资源
            if (item.resourceKind != ResourceKind.Food) continue;

            // 实例化预制体
            GameObject foodGO = Instantiate(foodItemPrefabs, foodItemParent);
            foodItemPrefabs script = foodGO.GetComponent<foodItemPrefabs>();
            foodGO.GetComponent<foodItemPrefabs>().resourceType = item.type;
            if (script != null)
            {
                // 设置图标和数量
                script.foodIcon.sprite = item.Icon;
                script.foodAmount.text = item.count.ToString();
            }

            currentFoodItems.Add(foodGO);
        }
    }

    /// <summary>
    /// 清空食材UI
    /// </summary>
    public void ClearFoodItems()
    {
        foreach (var go in currentFoodItems)
        {
            if (go != null) Destroy(go);
        }
        currentFoodItems.Clear();

        // 或者直接清空父物体下所有子物体（更保险）
        for (int i = foodItemParent.childCount - 1; i >= 0; i--)
        {
            Destroy(foodItemParent.GetChild(i).gameObject);
        }
    }

    /// <summary>
    /// 设置选中食材信息
    /// </summary>
    public void SetFoodInformation(ResourceType resourceType)
    {
        foreach (var item in GameValManager.Instance.resources)
        {
            if (item.type == resourceType)
            {
                foodInformationTitle.text = item.name;
                foodInformationDescription.text = item.description;
                break;
            }
        }

    }

    /// <summary>
    /// 根据 dishRecipes 生成菜肴UI列表
    /// </summary>
    public void GenerateDishList()
    {
        ClearDishList();

        foreach (DishRecipe recipe in dishRecipes)
        {
            GameObject dishGO = Instantiate(dishItemPrefabs, dishParent);
            dishItemPrefabs script = dishGO.GetComponent<dishItemPrefabs>();
            if (script == null)
            {
                Debug.LogWarning("dishItemPrefabs prefab 缺少 dishItemPrefabs 脚本！");
                continue;
            }

            // 设置菜名和图标
            script.disName.text = recipe.dishName;
            script.dishItem.sprite = recipe.dishIcon;

            // 清空旧的食材UI（如果有）
            for (int i = script.dishFoodParent.childCount - 1; i >= 0; i--)
            {
                Destroy(script.dishFoodParent.GetChild(i).gameObject);
            }

            // 生成所需食材列表
            foreach (DishIngredient ing in recipe.ingredients)
            {
                GameObject foodGO = Instantiate(dishFoodItemPrefabs, script.dishFoodParent);
                foodItemPrefabs foodScript = foodGO.GetComponent<foodItemPrefabs>();
                if (foodScript == null)
                {
                    Debug.LogWarning("dishFoodItemPrefabs 缺少 foodItemPrefabs 脚本！");
                    continue;
                }

                // 获取图标和当前拥有数量
                ResourceItem resItem = GameValManager.Instance.resources.Find(r => r.type == ing.resourceType);

                if (resItem != null)
                {
                    foodScript.foodIcon.sprite = resItem.Icon;
                    foodScript.foodAmount.text = $"{resItem.count}/{ing.requiredCount}";
                    foodScript.resourceType = ing.resourceType;
                }
                else
                {
                    foodScript.foodAmount.text = $"0/{ing.requiredCount}";
                }
            }

            currentDishes.Add(dishGO);
        }
    }
    /// <summary>
    /// 清空菜肴UI列表
    /// </summary>
    public void ClearDishList()
    {
        foreach (var go in currentDishes)
        {
            if (go != null) Destroy(go);
        }
        currentDishes.Clear();

        for (int i = dishParent.childCount - 1; i >= 0; i--)
        {
            Destroy(dishParent.GetChild(i).gameObject);
        }
    }
}