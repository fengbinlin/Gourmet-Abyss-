using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using Unity.VisualScripting;
using UnityEngine;

public enum PotLidState
{
    Open,
    Closing,
    Closed,
    Opening
}

public class Pot : MonoBehaviour
{

    // 新增字段以追踪当前厨师
    private CustomerNPC assignedCook;
    public potType potType;
    public potState potState;
    public PotLidState lidState = PotLidState.Open; // 锅盖状态

    [Header("锅盖动画配置")]
    public Transform lidTransform; // 锅盖的Transform组件
    public float lidAnimationTime = 0.5f; // 锅盖动画时间
    public Vector3 closedRotation = new Vector3(0, 0, -90f); // 关闭时的旋转角度
    public Vector3 openRotation = Vector3.zero; // 打开时的旋转角度

    [Header("烹饪状态动画")]
    public Animator animator;
    public string animatorIsCookingBool = "isCooking";
    public bool isCooking;

    public event System.Action<bool> CookingStateChanged;

    [Header("烹饪效果组件")]
    public GameObject Fog;
    public GameObject Fire;

    [Header("动画曲线")]
    public AnimationCurve lidAnimationCurve = AnimationCurve.EaseInOut(0, 0, 1, 1); // 默认缓动曲线

    // 当前烹饪的菜谱
    public DishRecipe currentRecipe;
    // 烹饪协程引用
    private Coroutine cookingCoroutine;
    private Coroutine lidAnimationCoroutine; // 锅盖动画协程

    [Header("装盘配置")]
    public bool autoTransferToPlate = true;    // 是否自动装盘
    public Plate targetPlate;                  // 目标菜碟（可手动指定）

    void Start()
    {
        // 初始化锅状态
        potState = potState.unUsed;
        lidState = PotLidState.Open;

        // 初始时锅盖应该是打开的
        if (lidTransform != null)
        {
            lidTransform.localEulerAngles = openRotation;
        }

        // 初始时禁用所有烹饪效果
        SetCookingEffects(false);

        Debug.Log("锅初始化完成，锅盖打开，效果组件休眠");
    }

    /// <summary>
    /// 设置烹饪效果组件的激活状态
    /// </summary>
    private void SetCookingEffects(bool isActive)
    {
        if (isCooking == isActive)
        {
            if (Fire != null) Fire.gameObject.SetActive(isActive);
            if (Fog != null) Fog.gameObject.SetActive(isActive);
            return;
        }

        isCooking = isActive;

        if (animator != null && !string.IsNullOrEmpty(animatorIsCookingBool))
        {
            animator.SetBool(animatorIsCookingBool, isActive);
        }

        if (Fire != null) Fire.gameObject.SetActive(isActive);
        if (Fog != null) Fog.gameObject.SetActive(isActive);

        CookingStateChanged?.Invoke(isActive);
    }

    /// <summary>
    /// 播放锅盖动画
    /// </summary>
    private IEnumerator PlayLidAnimation(bool isClosing)
    {
        if (lidTransform == null) yield break;

        // 设置锅盖状态
        lidState = isClosing ? PotLidState.Closing : PotLidState.Opening;

        Vector3 startRotation = isClosing ? openRotation : closedRotation;
        Vector3 targetRotation = isClosing ? closedRotation : openRotation;

        float elapsedTime = 0f;

        while (elapsedTime < lidAnimationTime)
        {
            float t = elapsedTime / lidAnimationTime;
            t = lidAnimationCurve.Evaluate(t); // 使用动画曲线

            Vector3 currentRotation = Vector3.Lerp(startRotation, targetRotation, t);
            lidTransform.localEulerAngles = currentRotation;

            elapsedTime += Time.deltaTime;
            yield return null;
        }

        // 确保最终位置准确
        lidTransform.localEulerAngles = targetRotation;

        // 更新锅盖状态
        lidState = isClosing ? PotLidState.Closed : PotLidState.Open;

        Debug.Log($"锅盖{(isClosing ? "关闭" : "打开")}动画完成");
    }

    // 开始烹饪
    public bool StartCooking(DishRecipe recipe)
    {
        if (potState == potState.Used)
        {
            Debug.LogWarning("锅正在使用中，无法开始新的烹饪！");
            return false;
        }

        // 从CookManager找厨师
        assignedCook = null;
        if (CookManager.cookManager != null)
            assignedCook = CookManager.cookManager.GetAvailableCook();

        // 设置锅状态和菜谱
        potState = potState.Used;
        currentRecipe = CloneRecipe(recipe);
        if (assignedCook)
        {
            currentRecipe = CookManager.cookManager.ApplyCookBuff(currentRecipe, assignedCook);
        }

        if (WeaponStatsManager.Instance != null)
        {
            currentRecipe.cookTime *= WeaponStatsManager.Instance.cookingTimeMultiplier;
            currentRecipe.baseDishPrice *= (1f + WeaponStatsManager.Instance.restaurantSellBonusRate);
        }


        // 若有厨师 → 交给厨师执行Buff与动画
        if (assignedCook != null)
        {
            Debug.Log($"由厨师 {assignedCook.name} 帮助烹饪。");
            assignedCook.StartCoroutine(assignedCook.HelpPotCooking(this));
        }
        else
        {
            Debug.Log("没有空闲厨师，正常烹饪。");
        }

        // 启动锅自身流程
        cookingCoroutine = StartCoroutine(CookingProcess(currentRecipe));
        Debug.Log($"开始烹饪：{currentRecipe.dishName}，预计时间：{currentRecipe.cookTime}秒");

        return true;
    }

    private DishRecipe CloneRecipe(DishRecipe source)
    {
        DishRecipe cloned = new DishRecipe();
        cloned.dishID = source.dishID;
        cloned.dishName = source.dishName;
        cloned.dishIcon = source.dishIcon;
        cloned.ingredients = source.ingredients != null ? new List<DishIngredient>(source.ingredients) : new List<DishIngredient>();
        cloned.acceptablePot = source.acceptablePot != null ? new List<potType>(source.acceptablePot) : new List<potType>();
        cloned.cookTime = source.cookTime;
        cloned.baseDishPrice = source.baseDishPrice;
        cloned.category = source.category;
        cloned.locked = source.locked;
        return cloned;
    }
    /// <summary>
    /// 完整的烹饪流程协程
    /// </summary>
    private IEnumerator CookingProcess(DishRecipe recipe)
    {
        // 1. 进入烹饪表现状态（与食材生成同步开始）
        SetCookingEffects(true);

        // 2. 生成食材实例效果
        yield return RestaurantPanel.instance.StartCoroutine(
            RestaurantPanel.instance.SpawnIngredientInstances(recipe, this)
        );

        // 等待一小段时间，让食材完全落入锅中
        yield return new WaitForSeconds(0.5f);

        // 3. 关闭锅盖
        if (lidAnimationCoroutine != null)
        {
            StopCoroutine(lidAnimationCoroutine);
        }
        lidAnimationCoroutine = StartCoroutine(PlayLidAnimation(true));
        yield return lidAnimationCoroutine;

        // 4. 开始计时烹饪
        yield return new WaitForSeconds(recipe.cookTime);

        // 5. 烹饪完成
        OnCookingComplete();
    }

    // 烹饪完成
    private void OnCookingComplete()
    {
        Debug.Log($"烹饪完成：{currentRecipe.dishName}，锅 {potType} 已空闲");

        // 启用厨师加成产出提示
        if (assignedCook != null)
        {
            assignedCook.ShowCustomBubble($"{currentRecipe.dishName}完成啦！", 2f);
            // 可以额外增加奖励：厨师产出加成
            // if (currentRecipe != null)
            // {
            //     currentRecipe.baseDishPrice *= assignedCook.data.priceIncreaseRate;
            //     // 产出数量逻辑（如果菜碟有数量概念）：
            //     // plate.currentDish.currentAmount += (int)(assignedCook.data.outputIncreaseRate - 1);
            // }
        }

        // 原有逻辑
        SetCookingEffects(false);
        if (lidAnimationCoroutine != null)
        {
            StopCoroutine(lidAnimationCoroutine);
        }
        lidAnimationCoroutine = StartCoroutine(PlayLidAnimation(false));
        int addAmount = 1;
        if (assignedCook)
        {
            addAmount = math.max(1, (int)assignedCook.data.outputIncreaseRate);
        }
        while (addAmount > 0)
        {
            if (autoTransferToPlate && currentRecipe != null)
            {
                bool transferSuccess = TransferToPlate();

                if (!transferSuccess)
                {
                    Debug.LogWarning($"烹饪完成但装盘失败：{currentRecipe.dishName}");
                }
            }
            addAmount--;
        }


        potState = potState.unUsed;
        currentRecipe = null;
        cookingCoroutine = null;
        assignedCook = null;  // 清除厨师引用
    }

    // 转移到菜碟
    public bool TransferToPlate()
    {
        if (currentRecipe == null)
        {
            Debug.LogWarning("没有菜肴可以装盘");
            return false;
        }

        // 如果指定了目标菜碟，尝试使用它
        if (targetPlate != null)
        {
            if (targetPlate.CanAddDish(currentRecipe))
            {
                return targetPlate.TryAddDish(currentRecipe, this);
            }
        }

        // 否则在餐厅中寻找合适的菜碟
        Plate suitablePlate = FindSuitablePlate();
        if (suitablePlate != null)
        {
            return suitablePlate.TryAddDish(currentRecipe, this);
        }

        Debug.LogWarning($"没有找到合适的菜碟来装 {currentRecipe.dishName}");
        return false;
    }

    // 寻找合适的菜碟
    // 在 Pot 类中修改 FindSuitablePlate 方法
    private Plate FindSuitablePlate()
    {
        if (RestaurantPanel.instance == null ||
            RestaurantPanel.instance.platesList == null)
        {
            Debug.LogError("RestaurantPanel或platesList未初始化！");
            return null;
        }

        // 步骤1: 首先寻找已经装有同种菜肴且还有容量的菜碟（优先填满）
        Plate mostFilledSameTypePlate = null;
        int mostFilledSameTypeRemainingCapacity = int.MaxValue; // 剩余容量越小表示越满

        // 步骤2: 寻找空菜碟
        Plate emptyPlate = null;

        foreach (Plate plate in RestaurantPanel.instance.platesList)
        {
            if (plate == null) continue;

            // 检查是否是同类型菜肴的菜碟
            if (plate.GetCurrentDishName() == currentRecipe.dishName&&plate.currentDish.recipe.baseDishPrice==currentRecipe.baseDishPrice)
            {
                int remainingCapacity = plate.GetRemainingCapacity();

                // 如果还有容量
                if (remainingCapacity > 0)
                {
                    // 寻找最满的菜碟（剩余容量最小的）
                    if (remainingCapacity < mostFilledSameTypeRemainingCapacity)
                    {
                        mostFilledSameTypeRemainingCapacity = remainingCapacity;
                        mostFilledSameTypePlate = plate;
                    }
                }
            }
            // 检查是否是空菜碟
            else if (plate.currentState == plateState.unUsed)
            {
                // 记录第一个找到的空菜碟
                if (emptyPlate == null)
                {
                    emptyPlate = plate;
                }
            }
        }

        // 优先返回同类型菜肴的菜碟
        if (mostFilledSameTypePlate != null)
        {
            Debug.Log($"找到同类型菜碟：{currentRecipe.dishName}，剩余容量：{mostFilledSameTypeRemainingCapacity}");
            return mostFilledSameTypePlate;
        }

        // 如果没有同类型菜肴的菜碟，返回空菜碟
        if (emptyPlate != null)
        {
            Debug.Log($"找到空菜碟：{currentRecipe.dishName}");
            return emptyPlate;
        }

        Debug.LogWarning($"没有找到合适的菜碟来装 {currentRecipe.dishName}");
        return null;
    }

    // 取消烹饪（如果需要）
    public void CancelCooking()
    {
        if (cookingCoroutine != null)
        {
            StopCoroutine(cookingCoroutine);
            cookingCoroutine = null;
        }

        // 如果锅盖动画正在进行，停止它
        if (lidAnimationCoroutine != null)
        {
            StopCoroutine(lidAnimationCoroutine);
            lidAnimationCoroutine = null;
        }

        // 恢复锅盖状态
        lidState = PotLidState.Open;
        if (lidTransform != null)
        {
            lidTransform.localEulerAngles = openRotation;
        }

        // 禁用烹饪效果
        SetCookingEffects(false);

        potState = potState.unUsed;
        currentRecipe = null;
        Debug.Log($"取消烹饪，锅 {potType} 已空闲");
    }

    // 检查是否空闲
    public bool IsAvailable()
    {
        return potState == potState.unUsed;
    }

    // 获取剩余烹饪时间（如果需要显示进度）
    public float GetRemainingTime()
    {
        // 这里需要额外的逻辑来跟踪剩余时间
        // 可以使用Time.time来记录开始时间并计算剩余时间
        return 0f;
    }

    // Update is called once per frame
    void Update()
    {
        // 可以在这里更新烹饪进度显示等
    }
}