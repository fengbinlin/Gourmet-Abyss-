using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UI;

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
    [Tooltip("0 = 瞬时到位；总烹饪耗时以菜谱读条为主")]
    public float lidAnimationTime = 0f;
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

    /// <summary>煮完但尚未全部装碟的份数；&gt;0 时锅仍为占用，且不给食材发「烹饪结束」事件。</summary>
    private int pendingPostCookServings;

    [Header("装盘配置")]
    public bool autoTransferToPlate = true;    // 是否自动装盘
    public Plate targetPlate;                  // 目标菜碟（可手动指定）

    [Header("出菜飞行动画")]
    public GameObject cookedDishFlyPrefab;     // 出菜飞行预制体（挂有 SpriteRenderer 或 Image）
    public float cookedDishFlyDuration = 0.35f;
    public AnimationCurve cookedDishFlyCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    public float cookedDishFlyZOffset = 0f;    // 在世界坐标基础上的Z偏移（用于层级微调）
    public float cookedDishFlyArcHeight = 0.6f; // 抛物线最高抬升高度（Y+）
    public float cookedDishLandingEffectDuration = 0.15f;
    public float cookedDishLandingScaleUp = 1.2f;

    private Vector3 _defaultLocalScaleForAttentionPulse;
    private Sequence _attentionPulseSequence;

    void Start()
    {
        _defaultLocalScaleForAttentionPulse = transform.localScale;

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

    /// <summary>短时间放大再缩回，用于玩家进入餐厅区域时对首锅的提示。</summary>
    public void PlayAttentionScalePulse(float peakMultiplier, float upDuration, float downDuration)
    {
        peakMultiplier = Mathf.Max(1.001f, peakMultiplier);
        upDuration = Mathf.Max(0.01f, upDuration);
        downDuration = Mathf.Max(0.01f, downDuration);

        _attentionPulseSequence?.Kill();
        transform.localScale = _defaultLocalScaleForAttentionPulse;

        Vector3 peak = _defaultLocalScaleForAttentionPulse * peakMultiplier;
        _attentionPulseSequence = DOTween.Sequence()
            .Append(transform.DOScale(peak, upDuration).SetEase(Ease.OutQuad))
            .Append(transform.DOScale(_defaultLocalScaleForAttentionPulse, downDuration).SetEase(Ease.InOutQuad));
    }

    public void CancelAttentionScalePulse()
    {
        _attentionPulseSequence?.Kill();
        _attentionPulseSequence = null;
        transform.localScale = _defaultLocalScaleForAttentionPulse;
    }

    private void OnDestroy()
    {
        _attentionPulseSequence?.Kill();
    }

    /// <param name="notifySubscribers">为 false 时不触发 <see cref="CookingStateChanged"/>（锅内食材实例不会销毁）。</param>
    private void SetCookingEffects(bool isActive, bool notifySubscribers = true)
    {
        bool stateChanged = isCooking != isActive;

        if (stateChanged)
        {
            isCooking = isActive;
            if (animator != null && !string.IsNullOrEmpty(animatorIsCookingBool))
                animator.SetBool(animatorIsCookingBool, isActive);
        }

        if (Fire != null) Fire.gameObject.SetActive(isActive);
        if (Fog != null) Fog.gameObject.SetActive(isActive);

        if (notifySubscribers && stateChanged)
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

        if (lidAnimationTime <= 0f)
        {
            lidTransform.localEulerAngles = targetRotation;
            lidState = isClosing ? PotLidState.Closed : PotLidState.Open;
            yield break;
        }

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
            currentRecipe.baseDishPrice *= Mathf.Max(0.01f, WeaponStatsManager.Instance.sellPriceMultiplier);
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
        cloned.sellTime = source.sellTime;
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

        // 3. 关闭锅盖（表现延迟已压到最低；读条见下方 recipe.cookTime）
        if (lidAnimationCoroutine != null)
        {
            StopCoroutine(lidAnimationCoroutine);
        }
        lidAnimationCoroutine = StartCoroutine(PlayLidAnimation(true));
        yield return lidAnimationCoroutine;

        // 4. 开始计时烹饪
        float cookDuration = Mathf.Max(0.01f, recipe.cookTime);
        float elapsed = 0f;
        while (elapsed < cookDuration)
        {
            elapsed += Time.deltaTime;
            if (RestaurantPanel.instance != null)
                RestaurantPanel.instance.SetCookingProgress(elapsed / cookDuration);
            yield return null;
        }
        // 烹饪结束后立即清零并隐藏进度条
        if (RestaurantPanel.instance != null)
            RestaurantPanel.instance.SetCookingProgress(0f);

        // 5. 烹饪完成
        OnCookingComplete();
    }

    // 烹饪完成
    private void OnCookingComplete()
    {
        Debug.Log($"烹饪完成：{currentRecipe.dishName}，锅 {potType}");

        if (assignedCook != null)
        {
            assignedCook.ShowCustomBubble($"{currentRecipe.dishName}完成啦！", 2f);
        }

        CustomerNPC cookRef = assignedCook;

        if (lidAnimationCoroutine != null)
            StopCoroutine(lidAnimationCoroutine);
        lidAnimationCoroutine = StartCoroutine(PlayLidAnimation(false));

        // 关火/烟与烹饪动画，但不通知锅内食材销毁（无碟时食材需留在锅里）
        SetCookingEffects(false, false);

        cookingCoroutine = null;
        assignedCook = null;

        int portions = 1;
        if (cookRef != null && cookRef.data != null)
            portions = math.max(1, (int)cookRef.data.outputIncreaseRate);

        if (!autoTransferToPlate || currentRecipe == null)
        {
            FinishPotAndNotifyIngredientsReleased();
            return;
        }

        int served = 0;
        for (int i = 0; i < portions; i++)
        {
            if (!TransferToPlate(out Plate usedPlate))
                break;
            PlayDishFlyEffect(currentRecipe, usedPlate);
            served++;
        }

        pendingPostCookServings = portions - served;

        if (pendingPostCookServings > 0)
        {
            Debug.LogWarning($"无可用餐碟，{currentRecipe.dishName} 仍有 {pendingPostCookServings} 份留在锅内，待有碟子后自动装盘。");
            potState = potState.Used;
            return;
        }

        FinishPotAndNotifyIngredientsReleased();
    }

    /// <summary>全部装碟完毕或取消烹饪：销毁锅内食材实例并释放锅（通知烹饪队列）。</summary>
    private void FinishPotAndNotifyIngredientsReleased()
    {
        pendingPostCookServings = 0;
        CookingStateChanged?.Invoke(false);
        potState = potState.unUsed;
        currentRecipe = null;
        RestaurantPanel.instance?.NotifyCookingPotFreed();
    }

    /// <summary>尝试消耗一份待装盘；成功则飞出特效。</summary>
    private void TryServeOnePendingPortion()
    {
        if (pendingPostCookServings <= 0 || currentRecipe == null) return;
        if (!TransferToPlate(out Plate usedPlate))
            return;

        PlayDishFlyEffect(currentRecipe, usedPlate);
        pendingPostCookServings--;
        if (pendingPostCookServings <= 0)
            FinishPotAndNotifyIngredientsReleased();
    }

    // 转移到菜碟
    public bool TransferToPlate()
    {
        return TransferToPlate(out _);
    }

    // 转移到菜碟，并返回本次使用的目标碟子
    public bool TransferToPlate(out Plate usedPlate)
    {
        usedPlate = null;

        if (currentRecipe == null)
        {
            Debug.LogWarning("没有菜肴可以装盘");
            return false;
        }

        Plate resolvedTarget = targetPlate;
        if (resolvedTarget != null && resolvedTarget.CanAddDish(currentRecipe))
        {
            bool success = resolvedTarget.TryAddDish(currentRecipe, this);
            if (success) usedPlate = resolvedTarget;
            return success;
        }

        // 否则在餐厅中寻找合适的菜碟
        Plate suitablePlate = FindSuitablePlate();
        if (suitablePlate != null)
        {
            bool success = suitablePlate.TryAddDish(currentRecipe, this);
            if (success) usedPlate = suitablePlate;
            return success;
        }

        Debug.LogWarning($"没有找到合适的菜碟来装 {currentRecipe.dishName}");
        return false;
    }

    private void PlayDishFlyEffect(DishRecipe recipe, Plate plate)
    {
        if (recipe == null || recipe.dishIcon == null || plate == null || cookedDishFlyPrefab == null)
        {
            return;
        }

        Vector3 startWorldPos = GetVisualCenterWorldPosition(transform);
        Vector3 endWorldPos = GetVisualCenterWorldPosition(plate.transform);
        StartCoroutine(PlayDishFlyEffectCoroutine(recipe.dishIcon, startWorldPos, endWorldPos));
    }

    private IEnumerator PlayDishFlyEffectCoroutine(Sprite dishIcon, Vector3 startWorldPos, Vector3 targetPosition)
    {
        GameObject flyObj = Instantiate(cookedDishFlyPrefab);
        if (flyObj == null) yield break;

        // 同时兼容 SpriteRenderer 和 UI Image
        SpriteRenderer sr = flyObj.GetComponentInChildren<SpriteRenderer>(true);
        if (sr != null)
        {
            sr.sprite = dishIcon;
        }

        Image img = flyObj.GetComponentInChildren<Image>(true);
        if (img != null)
        {
            img.sprite = dishIcon;
        }

        // 使用锅/碟自身的世界坐标Z，不再强制覆盖成预制体Z
        Vector3 startPos = startWorldPos;
        Vector3 endPos = targetPosition;
        startPos.z += cookedDishFlyZOffset;
        endPos.z += cookedDishFlyZOffset;

        flyObj.transform.position = startPos;

        float duration = Mathf.Max(0.01f, cookedDishFlyDuration);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float curveT = cookedDishFlyCurve.Evaluate(t);
            Vector3 basePos = Vector3.Lerp(startPos, endPos, curveT);

            // 抛物线：4t(1-t) 在 t=0.5 时达到1，叠加到Y轴正方向
            float arc = 4f * t * (1f - t) * cookedDishFlyArcHeight;
            basePos.y += arc;
            flyObj.transform.position = basePos;
            yield return null;
        }

        flyObj.transform.position = endPos;

        // 落点缩放弹跳表现：放大 -> 还原
        Vector3 originalScale = flyObj.transform.localScale;
        Vector3 peakScale = originalScale * Mathf.Max(1f, cookedDishLandingScaleUp);
        float landingDuration = Mathf.Max(0.01f, cookedDishLandingEffectDuration);
        float halfDuration = landingDuration * 0.5f;

        float landElapsed = 0f;
        while (landElapsed < halfDuration)
        {
            landElapsed += Time.deltaTime;
            float t = Mathf.Clamp01(landElapsed / halfDuration);
            flyObj.transform.localScale = Vector3.LerpUnclamped(originalScale, peakScale, t);
            yield return null;
        }

        landElapsed = 0f;
        while (landElapsed < halfDuration)
        {
            landElapsed += Time.deltaTime;
            float t = Mathf.Clamp01(landElapsed / halfDuration);
            flyObj.transform.localScale = Vector3.LerpUnclamped(peakScale, originalScale, t);
            yield return null;
        }

        flyObj.transform.localScale = originalScale;
        Destroy(flyObj);
    }

    // 优先使用可视组件中心点，避免 transform 锚点与模型显示位置不一致
    private Vector3 GetVisualCenterWorldPosition(Transform root)
    {
        if (root == null) return Vector3.zero;

        SpriteRenderer sr = root.GetComponentInChildren<SpriteRenderer>(true);
        if (sr != null)
        {
            return sr.bounds.center;
        }

        Collider2D col2D = root.GetComponentInChildren<Collider2D>(true);
        if (col2D != null)
        {
            return col2D.bounds.center;
        }

        return root.position;
    }

    // 寻找合适的菜碟
    // 在 Pot 类中修改 FindSuitablePlate 方法
    private Plate FindSuitablePlate()
    {
        if (RestaurantPanel.instance == null || currentRecipe == null)
        {
            Debug.LogError("RestaurantPanel 未初始化或菜谱为空！");
            return null;
        }

        Plate plate = RestaurantPanel.instance.FindSuitablePlateForOutgoingRecipe(currentRecipe);
        if (plate == null)
            Debug.LogWarning($"没有可装盘位置：{currentRecipe.dishName}（可能所有碟已满且无空碟）");
        return plate;
    }

    // 取消烹饪（如果需要）
    public void CancelCooking()
    {
        pendingPostCookServings = 0;

        if (cookingCoroutine != null)
        {
            StopCoroutine(cookingCoroutine);
            cookingCoroutine = null;
        }

        if (lidAnimationCoroutine != null)
        {
            StopCoroutine(lidAnimationCoroutine);
            lidAnimationCoroutine = null;
        }

        lidState = PotLidState.Open;
        if (lidTransform != null)
            lidTransform.localEulerAngles = openRotation;

        SetCookingEffects(false, false);
        CookingStateChanged?.Invoke(false);

        potState = potState.unUsed;
        currentRecipe = null;
        assignedCook = null;
        Debug.Log($"取消烹饪，锅 {potType} 已空闲");
        RestaurantPanel.instance?.NotifyCookingPotFreed();
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

    void Update()
    {
        if (pendingPostCookServings > 0)
            TryServeOnePendingPortion();
    }
}