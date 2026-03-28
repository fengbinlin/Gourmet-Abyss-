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
    [Header("烹饪排队槽（UI，顺序即队列下标 0=队首）")]
    [Tooltip("仅下标 0（第一个槽）的菜会下锅；锅空闲后仍从当前队首 0 取菜；队首空则整列左移递补。")]
    public List<DishQueueSlot> allDishQueueSlots = new List<DishQueueSlot>();
    [Tooltip("单槽最大叠堆份数；若已存在 WeaponStatsManager 则优先使用其 slotCapacity")]
    [SerializeField] private int cookQueueMaxPerSlotFallback = 20;

    private class CookQueueStackEntry
    {
        public DishRecipe recipe;
        public int count;
        public bool IsEmpty => recipe == null || count <= 0;
        public void Clear() { recipe = null; count = 0; }
    }

    private readonly List<CookQueueStackEntry> _cookQueueData = new List<CookQueueStackEntry>();
    private int _lastCookQueueActiveCount = -1;

    /// <summary>判断是否同一道菜（用于装盘唯一性、叠堆判定）。</summary>
    public static bool RecipesMatch(DishRecipe a, DishRecipe b)
    {
        if (a == null || b == null) return false;
        if (a.dishID != b.dishID) return false;
        return a.dishName == b.dishName && Mathf.Approximately(a.baseDishPrice, b.baseDishPrice);
    }

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
        EnsureCookQueueDataSize();
    }

    private void OnEnable()
    {
        if (WeaponStatsManager.Instance != null)
        {
            WeaponStatsManager.Instance.OnRestaurantStatsChanged -= SyncRestaurantUnitsFromStats;
            WeaponStatsManager.Instance.OnRestaurantStatsChanged += SyncRestaurantUnitsFromStats;
        }

        if (ShopSlotManager.Instance != null)
        {
            ShopSlotManager.Instance.OnRestaurantPlateSlotsChanged -= SyncRestaurantUnitsFromStats;
            ShopSlotManager.Instance.OnRestaurantPlateSlotsChanged += SyncRestaurantUnitsFromStats;
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

        if (ShopSlotManager.Instance != null)
        {
            ShopSlotManager.Instance.OnRestaurantPlateSlotsChanged -= SyncRestaurantUnitsFromStats;
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

        if (ShopSlotManager.Instance != null)
        {
            ShopSlotManager.Instance.OnRestaurantPlateSlotsChanged -= SyncRestaurantUnitsFromStats;
            ShopSlotManager.Instance.OnRestaurantPlateSlotsChanged += SyncRestaurantUnitsFromStats;
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
        SyncPlatesByCount(GetTargetPlateSlotCount());
        SyncCookQueueSlotsFromStats();
    }

    /// <summary>餐碟数量：优先 ShopSlotManager，否则 WeaponStatsManager.restaurantPlateCount。</summary>
    private int GetTargetPlateSlotCount()
    {
        if (allPlates == null || allPlates.Count == 0) return 0;
        if (ShopSlotManager.Instance != null)
            return Mathf.Clamp(ShopSlotManager.Instance.restaurantPlateSlotCount, 1, allPlates.Count);
        return Mathf.Clamp(WeaponStatsManager.Instance.restaurantPlateCount, 1, allPlates.Count);
    }

    private int GetActiveCookQueueSlotCount()
    {
        if (allDishQueueSlots == null || allDishQueueSlots.Count == 0) return 0;
        if (WeaponStatsManager.Instance == null)
            return Mathf.Min(allDishQueueSlots.Count, 4);
        return Mathf.Clamp(WeaponStatsManager.Instance.restaurantDishQueueSlotCount, 1, allDishQueueSlots.Count);
    }

    private int GetCookQueueMaxPerSlot()
    {
        if (WeaponStatsManager.Instance != null)
            return Mathf.Max(1, WeaponStatsManager.Instance.slotCapacity);
        return Mathf.Max(1, cookQueueMaxPerSlotFallback);
    }

    private void EnsureCookQueueDataSize()
    {
        if (allDishQueueSlots == null) return;
        while (_cookQueueData.Count < allDishQueueSlots.Count)
            _cookQueueData.Add(new CookQueueStackEntry());
    }

    private void SyncCookQueueSlotsFromStats()
    {
        if (allDishQueueSlots == null || allDishQueueSlots.Count == 0)
            return;

        EnsureCookQueueDataSize();
        int newActive = GetActiveCookQueueSlotCount();

        if (_lastCookQueueActiveCount < 0)
            _lastCookQueueActiveCount = newActive;

        if (newActive < _lastCookQueueActiveCount)
            ShrinkCookQueueActiveRegion(_lastCookQueueActiveCount, newActive);

        _lastCookQueueActiveCount = newActive;

        for (int i = 0; i < allDishQueueSlots.Count; i++)
        {
            DishQueueSlot slot = allDishQueueSlots[i];
            if (slot == null) continue;
            bool on = i < newActive;
            if (slot.gameObject.activeSelf != on)
                slot.gameObject.SetActive(on);
        }

        RefreshCookQueueUI();
    }

    private void ShrinkCookQueueActiveRegion(int oldActive, int newActive)
    {
        List<(DishRecipe r, int c)> order = new List<(DishRecipe, int)>();
        for (int i = 0; i < oldActive && i < _cookQueueData.Count; i++)
        {
            CookQueueStackEntry e = _cookQueueData[i];
            if (!e.IsEmpty) order.Add((e.recipe, e.count));
        }

        for (int i = 0; i < _cookQueueData.Count; i++)
            _cookQueueData[i].Clear();

        if (order.Count > newActive)
            Debug.LogWarning($"[餐厅] 烹饪排队槽减至 {newActive}，将丢失末尾 {order.Count - newActive} 条队列。");

        for (int i = 0; i < order.Count && i < newActive; i++)
        {
            _cookQueueData[i].recipe = order[i].r;
            _cookQueueData[i].count = order[i].c;
        }
    }

    private void RefreshCookQueueUI()
    {
        if (allDishQueueSlots == null) return;
        EnsureCookQueueDataSize();
        int active = GetActiveCookQueueSlotCount();
        for (int i = 0; i < allDishQueueSlots.Count; i++)
        {
            DishQueueSlot ui = allDishQueueSlots[i];
            if (ui == null) continue;
            if (i >= active || i >= _cookQueueData.Count)
            {
                ui.SetEmpty();
                continue;
            }
            CookQueueStackEntry e = _cookQueueData[i];
            ui.SetVisual(e.recipe, e.count);
        }
    }

    /// <summary>点击菜谱：入队（扣食材），再由空闲锅自动开始烹饪。</summary>
    public bool TryEnqueueDishForCooking(DishRecipe recipe)
    {
        if (recipe == null) return false;
        EnsureCookQueueDataSize();
        if (!CheckIngredientsAvailableForRecipe(recipe))
        {
            Debug.Log("食材不足，无法加入烹饪队列：" + recipe.dishName);
            return false;
        }

        int idx = FindSuitableCookQueueSlotIndex(recipe);
        if (idx < 0)
        {
            Debug.Log("烹饪排队已满：" + recipe.dishName);
            return false;
        }

        if (!ConsumeIngredientsForRecipe(recipe))
            return false;

        CookQueueStackEntry e = _cookQueueData[idx];
        if (e.IsEmpty)
        {
            e.recipe = recipe;
            e.count = 1;
        }
        else
            e.count++;

        RefreshCookQueueUI();
        TryDispatchQueueToPots();

        StartCoroutine(CoRefreshMenuUiDeferred());

        return true;
    }

    private IEnumerator CoRefreshMenuUiDeferred()
    {
        yield return null;
        if (GameValManager.Instance != null && foodItemParent != null)
            GenerateFoodItems();
        if (dishParent != null)
            GenerateDishList();
    }

    private int FindSuitableCookQueueSlotIndex(DishRecipe recipe)
    {
        int active = GetActiveCookQueueSlotCount();
        int maxPer = GetCookQueueMaxPerSlot();
        int best = -1;
        int bestRemaining = int.MaxValue;

        for (int i = 0; i < active && i < _cookQueueData.Count; i++)
        {
            CookQueueStackEntry e = _cookQueueData[i];
            if (e.IsEmpty) continue;
            if (!RecipesMatch(e.recipe, recipe)) continue;
            int remaining = maxPer - e.count;
            if (remaining > 0 && remaining < bestRemaining)
            {
                bestRemaining = remaining;
                best = i;
            }
        }

        if (best >= 0) return best;

        for (int i = 0; i < active && i < _cookQueueData.Count; i++)
        {
            if (_cookQueueData[i].IsEmpty)
                return i;
        }

        return -1;
    }

    private bool CheckIngredientsAvailableForRecipe(DishRecipe recipe)
    {
        if (recipe == null || recipe.ingredients == null || InventoryManager.instance == null)
            return false;

        foreach (DishIngredient ingredient in recipe.ingredients)
        {
            if (ingredient.requiredCount <= 0) continue;
            if (InventoryManager.instance.GetItemCount(ingredient.resourceType) < ingredient.requiredCount)
                return false;
        }
        return true;
    }

    private bool ConsumeIngredientsForRecipe(DishRecipe recipe)
    {
        if (InventoryManager.instance == null || recipe == null) return false;
        return InventoryManager.instance.TryConsumeIngredientsForRecipe(recipe);
    }

    /// <summary>餐厅仅一口锅：使用 <see cref="potsList"/> 中第一个锅位（下标 0）。</summary>
    public Pot FindAvailablePotForRecipe(List<potType> acceptablePotTypes)
    {
        if (acceptablePotTypes == null || potsList == null || potsList.Count == 0) return null;
        Pot pot = potsList[0];
        if (pot != null && pot.IsAvailable() && acceptablePotTypes.Contains(pot.potType))
            return pot;
        return null;
    }

    /// <summary>
    /// 将队首排队菜派给唯一空闲锅。只从 <see cref="_cookQueueData"/> 下标 0 取一份；每轮最多开始 1 次烹饪。
    /// </summary>
    private void TryDispatchQueueToPots()
    {
        if (!isActiveAndEnabled || potsList == null || potsList.Count == 0) return;
        TryDispatchOneCookFromQueueFront();
    }

    /// <summary>仅从排队槽第 1 格（下标 0）取 1 份菜；唯一锅空闲且接受该菜谱时开始烹饪。</summary>
    private bool TryDispatchOneCookFromQueueFront()
    {
        EnsureCookQueueDataSize();
        int active = GetActiveCookQueueSlotCount();
        if (active <= 0 || _cookQueueData.Count == 0) return false;

        if (_cookQueueData[0].IsEmpty)
            CompactCookQueueLeftPreserveOrder();

        if (_cookQueueData[0].IsEmpty)
            return false;

        DishRecipe r = _cookQueueData[0].recipe;
        if (r == null) return false;

        Pot pot = FindAvailablePotForRecipe(r.acceptablePot);
        if (pot == null)
            return false;

        // 食材实例仅在 Pot.CookingProcess 内生成一次（勿在此处再 StartCoroutine，否则会与锅内协程重复实例化）
        if (!pot.StartCooking(r))
            return false;

        _cookQueueData[0].count--;
        if (_cookQueueData[0].count <= 0)
        {
            _cookQueueData[0].Clear();
            CompactCookQueueLeftPreserveOrder();
        }
        else
            RefreshCookQueueUI();

        return true;
    }

    /// <summary>队列槽 0 卖空概念：与餐碟一致，非空槽整体左移。</summary>
    private void CompactCookQueueLeftPreserveOrder()
    {
        int active = GetActiveCookQueueSlotCount();
        EnsureCookQueueDataSize();
        List<(DishRecipe r, int c)> filled = new List<(DishRecipe, int)>();
        for (int i = 0; i < active && i < _cookQueueData.Count; i++)
        {
            CookQueueStackEntry e = _cookQueueData[i];
            if (!e.IsEmpty) filled.Add((e.recipe, e.count));
        }

        for (int i = 0; i < active && i < _cookQueueData.Count; i++)
            _cookQueueData[i].Clear();

        for (int i = 0; i < filled.Count && i < active; i++)
        {
            _cookQueueData[i].recipe = filled[i].r;
            _cookQueueData[i].count = filled[i].c;
        }

        RefreshCookQueueUI();
    }

    public void NotifyCookingPotFreed()
    {
        TryDispatchQueueToPots();
    }

    private int _dispatchFrameCounter;

    private void Update()
    {
        if ((_dispatchFrameCounter++ % 12) != 0) return;
        TryDispatchQueueToPots();
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
                int invCount = InventoryManager.instance != null
                    ? InventoryManager.instance.GetItemCount(item.type)
                    : 0;
                // 设置图标和数量（数量来自背包）
                script.foodIcon.sprite = item.Icon;
                script.foodAmount.text = invCount.ToString();
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
                int invAmt = InventoryManager.instance != null
                    ? InventoryManager.instance.GetItemCount(ingredient.resourceType)
                    : 0;
                Debug.Log($"      当前背包数量: {invAmt}");
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

                ResourceItem resItem = GameValManager.Instance != null
                    ? GameValManager.Instance.GetResourceInfo(ing.resourceType)
                    : null;
                int owned = InventoryManager.instance != null
                    ? InventoryManager.instance.GetItemCount(ing.resourceType)
                    : 0;

                if (resItem != null)
                {
                    foodScript.foodIcon.sprite = resItem.Icon;
                    foodScript.foodAmount.text = $"{owned}/{ing.requiredCount}";
                    foodScript.resourceType = ing.resourceType;
                }
                else
                {
                    foodScript.foodAmount.text = $"{owned}/{ing.requiredCount}";
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
            ResourceItem resource = GameValManager.Instance != null
                ? GameValManager.Instance.GetResourceInfo(ingredient.resourceType)
                : null;
            if (resource == null || resource.Icon == null)
            {
                Debug.LogWarning($"找不到食材 {ingredient.resourceType} 的图标");
                continue;
            }

            for (int i = 0; i < ingredient.requiredCount; i++)
            {
                Transform spawnParent = RestaurantPanel.instance.ingredientSpawnParent != null ?
                    RestaurantPanel.instance.ingredientSpawnParent : pot.transform;

                GameObject instance = Instantiate(
                    RestaurantPanel.instance.ingredientInstancePrefab
                );

                IngredientInstanceController controller = instance.GetComponent<IngredientInstanceController>();
                if (controller != null)
                {
                    controller.Initialize(ingredient.resourceType, pot, resource.Icon);
                }
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
        return FindSuitablePlateForOutgoingRecipe(recipe);
    }

    /// <summary>
    /// 出锅装盘：优先填满已装着同菜的碟子（剩余容量最少的最优先；并列时取更靠前的碟子），否则取最靠前的空碟（可在多盘上堆同一种菜）。
    /// </summary>
    public Plate FindSuitablePlateForOutgoingRecipe(DishRecipe recipe)
    {
        if (recipe == null || platesList == null || platesList.Count == 0) return null;

        Plate mostFilledSameType = null;
        int mostFilledRemainingCap = int.MaxValue;

        for (int i = 0; i < platesList.Count; i++)
        {
            Plate plate = platesList[i];
            if (plate == null || plate.currentDish == null || plate.currentDish.IsEmpty()) continue;
            if (!RecipesMatch(plate.currentDish.recipe, recipe)) continue;

            int remaining = plate.GetRemainingCapacity();
            if (remaining > 0 && remaining < mostFilledRemainingCap)
            {
                mostFilledRemainingCap = remaining;
                mostFilledSameType = plate;
            }
        }

        if (mostFilledSameType != null)
            return mostFilledSameType;

        for (int i = 0; i < platesList.Count; i++)
        {
            Plate plate = platesList[i];
            if (plate == null) continue;
            if (!plate.IsPlateEmpty()) continue;
            if (plate.CanAddDish(recipe))
                return plate;
        }

        return null;
    }

    /// <summary>
    /// 首碟卖空后：将其余碟子上有食物的盘整体按顺序左移补位（不改变相对顺序）。
    /// </summary>
    public void CompactPlatesLeftPreserveOrder()
    {
        if (platesList == null || platesList.Count == 0) return;

        List<Dish> dishes = new List<Dish>(platesList.Count);
        List<Pot> pots = new List<Pot>(platesList.Count);

        for (int i = 0; i < platesList.Count; i++)
        {
            Plate p = platesList[i];
            if (p == null) continue;
            if (p.currentDish != null && !p.currentDish.IsEmpty())
            {
                dishes.Add(p.currentDish);
                pots.Add(p.sourcePot);
            }
        }

        if (dishes.Count > platesList.Count)
            Debug.LogWarning($"[餐厅] 食物种类数量({dishes.Count})多于碟子数，将只保留前 {platesList.Count} 盘。");

        for (int i = 0; i < platesList.Count; i++)
        {
            Plate p = platesList[i];
            if (p == null) continue;
            if (p.IsRestaurantPrimarySellPlate())
                p.StopConsumeOnly();
            else
                p.CancelConsume();
            p.ClearDishDataFieldsOnly();
        }

        int n = Mathf.Min(dishes.Count, platesList.Count);
        for (int i = 0; i < n; i++)
        {
            Plate p = platesList[i];
            if (p == null) continue;
            p.ApplyContentForRebalance(dishes[i], pots[i]);
        }

        for (int i = 0; i < platesList.Count; i++)
        {
            if (platesList[i] != null)
            {
                platesList[i].RefreshDisplay();
                platesList[i].EnsureAutoSellRunning();
            }
        }
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