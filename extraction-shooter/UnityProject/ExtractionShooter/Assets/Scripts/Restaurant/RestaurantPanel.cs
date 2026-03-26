using System.Collections;
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
    [Header("锅数据")]
    public List<Pot> potsList = new List<Pot>(); //餐厅有的锅
    [Header("锅数据（全部候选）")]
    public List<Pot> allPots = new List<Pot>();
    [Header("食材实例效果")]
    public GameObject ingredientInstancePrefab;  // 食材实例预制体
    public Transform ingredientSpawnParent;      // 生成父物体
    [Header("菜碟配置")]
    public List<Plate> platesList = new List<Plate>(); //餐厅有的菜碟
    [Header("菜碟配置（全部候选）")]
    public List<Plate> allPlates = new List<Plate>();
    void Awake()
    {
        instance = this;

        if (allPots.Count == 0 && potsList.Count > 0)
        {
            allPots.AddRange(potsList);
        }

        if (allPlates.Count == 0 && platesList.Count > 0)
        {
            allPlates.AddRange(platesList);
        }

        // 不在这里强制同步：避免 WeaponStatsManager 加载顺序导致 Instance 为空
    }

    private void OnEnable()
    {
        if (WeaponStatsManager.Instance != null)
        {
            WeaponStatsManager.Instance.OnRestaurantStatsChanged -= SyncRestaurantUnitsFromStats;
            WeaponStatsManager.Instance.OnRestaurantStatsChanged += SyncRestaurantUnitsFromStats;
        }

        // 无论菜单UI能否立即生成，都先确保锅/盘数量同步（隐藏多余对象）
        StartCoroutine(WaitAndSyncRestaurantUnits());

        if (GameValManager.Instance == null || foodItemParent == null) return;
        GenerateFoodItems();
        GenerateDishList();
    }

    private void OnDisable()
    {
        if (WeaponStatsManager.Instance != null)
        {
            WeaponStatsManager.Instance.OnRestaurantStatsChanged -= SyncRestaurantUnitsFromStats;
        }
    }

    private IEnumerator WaitAndSyncRestaurantUnits()
    {
        // 等待 WeaponStatsManager 单例创建完成
        float timeout = 3f;
        while (WeaponStatsManager.Instance == null && timeout > 0f)
        {
            timeout -= Time.deltaTime;
            yield return null;
        }

        if (WeaponStatsManager.Instance != null)
        {
            // 确保无论加载顺序如何，都能订阅到实时变更事件
            WeaponStatsManager.Instance.OnRestaurantStatsChanged -= SyncRestaurantUnitsFromStats;
            WeaponStatsManager.Instance.OnRestaurantStatsChanged += SyncRestaurantUnitsFromStats;
        }

        SyncRestaurantUnitsFromStats();
    }
    /// <summary>
    /// 每次面板显示时刷新：根据 GameValManager 重新生成食材种类与数量、菜肴列表。
    /// </summary>
    
    private void SyncRestaurantUnitsFromStats()
    {
        if (WeaponStatsManager.Instance == null)
        {
            return;
        }

        EnsureAllUnitsPopulated();
        SyncPotsByCount(WeaponStatsManager.Instance.restaurantPotCount);
        SyncPlatesByCount(WeaponStatsManager.Instance.restaurantPlateCount);
    }

    private void EnsureAllUnitsPopulated()
    {
        // 兜底：如果 Inspector 没填 allPots/allPlates，则自动从场景里收集（包含未激活对象）
        if (allPots == null) allPots = new List<Pot>();
        if (allPlates == null) allPlates = new List<Plate>();

        if (allPots.Count == 0)
        {
            Pot[] potsInScene = FindObjectsOfType<Pot>(true);
            allPots.AddRange(potsInScene);
        }

        if (allPlates.Count == 0)
        {
            Plate[] platesInScene = FindObjectsOfType<Plate>(true);
            allPlates.AddRange(platesInScene);
        }
    }

    private void SyncPotsByCount(int targetCount)
    {
        potsList.Clear();

        int activeCount = Mathf.Clamp(targetCount, 0, allPots.Count);
        for (int i = 0; i < allPots.Count; i++)
        {
            Pot pot = allPots[i];
            if (pot == null) continue;

            bool shouldActive = i < activeCount;
            if (pot.gameObject.activeSelf != shouldActive)
            {
                pot.gameObject.SetActive(shouldActive);
            }

            if (shouldActive)
            {
                potsList.Add(pot);
            }
        }
    }

    private void SyncPlatesByCount(int targetCount)
    {
        platesList.Clear();

        int activeCount = Mathf.Clamp(targetCount, 0, allPlates.Count);
        for (int i = 0; i < allPlates.Count; i++)
        {
            Plate plate = allPlates[i];
            if (plate == null) continue;

            bool shouldActive = i < activeCount;
            if (plate.gameObject.activeSelf != shouldActive)
            {
                plate.gameObject.SetActive(shouldActive);
            }

            if (shouldActive)
            {
                platesList.Add(plate);
            }
        }
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
            if (item.resourceKind != ResourceKind.Food || item.type==ResourceType.None) continue;

            // 实例化预制体
            GameObject foodGO = Instantiate(foodItemPrefabs, foodItemParent);
            foodItemPrefabs script = foodGO.GetComponent<foodItemPrefabs>();
            //print("EEEAAA");
            //print(item.type);
            foodGO.GetComponent<foodItemPrefabs>().resourceType = item.type;
            if (script != null)
            {
                // 设置图标和数量
                script.foodIcon.sprite = item.Icon;
                
                script.foodAmount.text = item.count.ToString();
                //print("EEEAAAAA"+item.name);
                //print("EEEAAAAA"+item.count);
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

        for (int i = 0; i < dishRecipes.Count; i++)
        {
            DishRecipe recipe = dishRecipes[i];
            // 只显示已经解锁的菜谱
            if (recipe.locked)
            {
                continue;
            }
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

            // 设置菜谱数据
            script.SetRecipeData(recipe);
            // 打印食材信息
            Debug.Log($"  - 所需食材数量: {script.recipeData.ingredients.Count}");
            for (int j = 0; j < script.recipeData.ingredients.Count; j++)
            {
                DishIngredient ingredient = script.recipeData.ingredients[j];
                Debug.Log($"    - 食材 {j}: 类型={ingredient.resourceType}, 需要数量={ingredient.requiredCount}");

                // 检查GameValManager中是否有对应的资源
                ResourceItem resItem = GameValManager.Instance.resources.Find(r => r.type == ingredient.resourceType);
                Debug.Log($"      当前拥有数量: {(resItem != null ? resItem.count.ToString() : "资源未找到")}");
            }
            // 清空旧的食材UI（如果有）
            for (int j = script.dishFoodParent.childCount - 1; j >= 0; j--)
            {
                Destroy(script.dishFoodParent.GetChild(j).gameObject);
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

    // 生成食材实例效果
    public IEnumerator SpawnIngredientInstances(DishRecipe recipe, Pot pot)
    {

        if (RestaurantPanel.instance == null || RestaurantPanel.instance.ingredientInstancePrefab == null)
        {
            Debug.LogWarning("食材实例预制体未设置！");
            yield break;
        }

        foreach (DishIngredient ingredient in recipe.ingredients)
        {
            print("AAA");
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
                print("BBB");
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

    // 在 RestaurantPanel 类中添加以下方法

    /// <summary>
    /// 初始化菜碟UI
    /// </summary>
    public void InitializePlates()
    {
        foreach (Plate plate in platesList)
        {
            if (plate != null)
            {
                // 可以在这里添加菜碟的初始化逻辑
                // 例如：设置菜碟的UI引用、按钮事件等
            }
        }
    }

    /// <summary>
    /// 查找可以容纳指定菜肴的菜碟
    /// </summary>
    public Plate FindAvailablePlateForDish(DishRecipe recipe)
    {
        foreach (Plate plate in platesList)
        {
            if (plate != null && plate.CanAddDish(recipe))
            {
                return plate;
            }
        }
        return null;
    }

    /// <summary>
    /// 手动装盘（玩家交互）
    /// </summary>
    public bool ManualTransferToPlate(Pot pot, Plate plate)
    {
        if (pot == null || plate == null)
        {
            Debug.LogWarning("锅或菜碟为空");
            return false;
        }

        // 检查锅是否有烹饪完成的菜肴
        // 这里需要一个方法来检查锅的完成状态

        return false;
    }

    /// <summary>
    /// 根据菜ID解锁菜谱（供道具等调用）
    /// </summary>
    public void UnlockDishByID(int dishID)
    {
        DishRecipe recipe = dishRecipes.Find(d => d.dishID == dishID);
        if (recipe == null)
        {
            Debug.LogWarning($"未找到 dishID 为 {dishID} 的菜谱！");
            return;
        }

        if (!recipe.locked)
        {
            // 已经解锁，无需重复处理
            return;
        }

        recipe.locked = false;
        Debug.Log($"已解锁菜谱：{recipe.dishName} (ID={recipe.dishID})");

        // 如果餐厅面板当前是打开状态，刷新一次菜单显示
        GenerateDishList();
    }

    public Sprite GetDishIconByID(int dishID)
    {
        DishRecipe recipe = dishRecipes.Find(d => d.dishID == dishID);
        if (recipe == null) return null;
        return recipe.dishIcon;
    }
}