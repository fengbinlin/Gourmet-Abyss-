using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.UI;
using JetBrains.Annotations;

public enum PointType
{
    Entrance,
    Restaurant,
    Queue,
    Plate
}

public enum CustomerState
{
    Spawning,         // 刚生成
    WalkingToQueue,   // 正在走向队尾
    Queueing,         // 排队中
    InsideRestaurant, // 在餐厅消费
    Leaving           // 离开餐厅
}

[System.Serializable]
public class ScenePoint
{
    public PointType type;
    public Transform pointTransform;
}

public enum CookState
{
    Idle,       // 闲逛（在 Left / Right 间来回移动）
    Cooking,    // 正在烹饪（跳动）
    Returning   // 烹饪结束，回到巡逻状态
}

public class CustomerNPC : MonoBehaviour
{
    public bool isCookingNow = false;
    private CookState cookState = CookState.Idle;
    private Coroutine cookRoutineCoroutine;
    public bool hasChattedWithOtherCustomer = false; // 是否已和其他顾客交谈过
    public Text affectionLevelText; // 显示好感度等级（非 TMP）
    public Image affectionProgressImage; // 环形进度条（使用 FillAmount）
    public bool isInteractingWithPlayer = false;  // 当前是否正与玩家互动
    [HideInInspector] public bool isPairChatPositioning = false; // 配对聊天：是否允许在“交互”中继续走位
    private float interactionDuration = 10f;      // 暂停持续时间（可自行调整）
    [SerializeField] private float maxPlayerInteractionDistance = 5f; // 超出该距离则解除对话/交互
    private Transform playerTransform;             // 玩家对象（运行时获取）
    public CustomerData data;
    public CustomerState state = CustomerState.Spawning;
    public GameObject bubble;         // 气泡对象
    public Text bubbleText;           // 气泡文本组件
    private Vector3 targetPosition;
    private Plate targetPlate;
    private bool isConsuming = false;
    private CustomerManager manager;

    // 供 Manager 的配对聊天逻辑做“临时暂停/恢复”用
    public Vector3 CurrentTargetPosition => targetPosition;

    [Header("2D朝向（仅左/右）")]
    [SerializeField] private bool useScaleFlipForFacing = true;
    private int facingSign = 1; // 1=右，-1=左（以 X 轴方向为准）

    public Transform coinSpawnPoint;
    [Tooltip("飞向钱箱的金币最多生成数量，超出部分只加数值不生成抛射物")]
    [SerializeField] private int maxVisualCoins = 12;
    // 气泡相关变量
    private Coroutine bubbleRoutineCoroutine;  // 循环更新协程
    private Coroutine bubbleHideCoroutine;     // 气泡隐藏协程
    private float bubbleDuration = 4f;         // 气泡显示持续时间
    private float bubbleInterval = 3f;         // 气泡更新间隔
    private float bubbleRandomness = 2f;       // 气泡随机性

    public List<GameObject> playerModelList;
    public Animator animator;

    public float rotationSpeed = 5.0f; // 控制旋转平滑速度

    public Vector3 guestOriginPosition;
    public bool isGuesting;

    [Header("送礼状态")]
    [SerializeField] private bool hasReceivedGiftFromPlayer = false; // 同一个NPC生成后，玩家是否已送过礼（用于限制重复送礼UI）
    private bool isInGiftFlow = false; // 玩家已点“想要送礼”，暂停对话倒计时

    public void Init()
    {
        manager = CustomerManager.instance;

        if (bubble != null)
            bubble.SetActive(false);

        SetInteractionPanelVisible(false);
        SetGiftPanelVisible(false);
        SetBagUIVisible(false);

        UpdateAffectionUI(); // 初始化好感度UI
        StartBubbleRoutine();
    }

    private void UpdateAffectionUI()
    {
        if (data == null) return;

        int level = data.affectionLevel;
        float currentValue = data.affectionValue;
        float needValue = 1f;

        if (data.affectionLevelNeeds != null && data.affectionLevelNeeds.Count > 0)
        {
            if (level < data.affectionLevelNeeds.Count)
                needValue = data.affectionLevelNeeds[level];
            else
                needValue = data.affectionLevelNeeds[data.affectionLevelNeeds.Count - 1];
        }

        float fill = needValue == 0 ? 1 : Mathf.Clamp01(currentValue / needValue);

        if (affectionLevelText != null)
            affectionLevelText.text = $"Lv.{level}";

        if (affectionProgressImage != null)
            affectionProgressImage.fillAmount = fill;
    }

    public void AddAffection(float amount)
    {
        if (data == null) return;
        data.AddAffection(amount);
        UpdateAffectionUI();
    }

    void Update()
    {
        // 配对聊天的“预站位阶段”允许继续移动到目标点
        bool allowMoveNow = !isInteractingWithPlayer || isPairChatPositioning;
        if (!data.isCook && !isConsuming && allowMoveNow && !isGuesting) // 🔸 如果正在互动则暂停
        {
            MoveToTarget();

            // 如果正在离开且到达目标，销毁自己
            if (state == CustomerState.Leaving && Vector3.Distance(transform.position, targetPosition) < 0.1f)
            {
                manager?.RemoveCustomer(this);
                Destroy(gameObject);
            }
        }

        animator.SetBool("isRunning", allowMoveNow && Vector3.Distance(transform.position, targetPosition) >= 0.1f);

        UpdateBubblePosition();
    }

    private void FaceByDirection(Vector3 worldDirection)
    {
        // 2D Sprite：只允许朝左/朝右。这里以 X 轴正向为“右”，负向为“左”。
        float x = worldDirection.x;
        if (Mathf.Abs(x) < 0.0001f) return;
        FaceByX(x);
    }

    private void FaceByX(float x)
    {
        int sign = x >= 0 ? 1 : -1;
        if (sign == facingSign) return;
        facingSign = sign;

        // 需求：localScale.x 不能为负数。统一保持缩放为正，通过旋转来实现左右朝向。
        Vector3 s = transform.localScale;
        float absX = Mathf.Abs(s.x);
        transform.localScale = new Vector3(absX, s.y, s.z);

        // 用 Y 轴旋转模拟左右（角度按你美术朝向调整）
        // 保留 useScaleFlipForFacing 字段以兼容 Inspector 配置，但不再使用负缩放。
        transform.rotation = Quaternion.Euler(0f, facingSign == 1 ? 90f : 270f, 0f);
    }

    public void FaceToward(Vector3 worldPos)
    {
        Vector3 dir = worldPos - transform.position;
        dir.y = 0f;
        FaceByDirection(dir);
    }

    public bool CanDoCustomerPairChat()
    {
        // 需求：只能在进入餐厅之前、或离开餐厅准备回家时聊天
        if (data != null && data.isCook) return false;
        if (isGuesting) return false;
        if (isConsuming) return false;
        if (isInteractingWithPlayer) return false; // 正在与玩家交互/其它交互中，不参与配对聊天

        // 离开点附近不再触发聊天，避免“聊天结束瞬间到点就销毁”的突兀观感
        if (state == CustomerState.Leaving)
        {
            if (Vector3.Distance(transform.position, targetPosition) < 0.8f)
                return false;
        }

        return state == CustomerState.Spawning
            || state == CustomerState.WalkingToQueue
            || state == CustomerState.Queueing
            || state == CustomerState.Leaving;
    }

    public void BeginPairChatPositioning(Vector3 pos)
    {
        isInteractingWithPlayer = true;
        isPairChatPositioning = true;
        SetTarget(pos);
    }

    public void EndPairChatPositioning()
    {
        isPairChatPositioning = false;
        // 保持 isInteractingWithPlayer = true，让其在聊天阶段停住
    }

    public void ClickCustomer()
    {
        print("点击");

        // NPC成为厨师后：不可再被玩家交互
        if (data != null && data.isCook)
        {
            SetInteractionPanelVisible(false);
            SetGiftPanelVisible(false);
            SetBagUIVisible(false);
            return;
        }

        if (state == CustomerState.InsideRestaurant)
            return;

        // 🔸 如果当前有正在交互的 NPC 且不是自己，则停止它
        if (CustomerManager.instance.currentInteractingNPC != null &&
            CustomerManager.instance.currentInteractingNPC != this)
        {
            CustomerManager.instance.currentInteractingNPC.ForceStopInteraction();
        }

        // 🔸 开始与自己交互
        Transform player = GameObject.FindGameObjectWithTag("Player").transform;
        if (player != null && Vector3.Distance(player.position, transform.position) < 5f)
        {
            playerTransform = player;
            StartCoroutine(InteractWithPlayerCoroutine());
        }
        ItemBagManager.instance.customerGiftImage = customerGiftImage;
    }
    public void ForceStopInteraction()
    {
        if (!isInteractingWithPlayer) return;

        StopAllCoroutines();  // 停止当前交互协程
        isInteractingWithPlayer = false;
        isInGiftFlow = false;
        if (bubble != null) bubble.SetActive(false);
        animator.SetBool("isRunning", true);

        SetInteractionPanelVisible(false);
        SetGiftPanelVisible(false);
        SetBagUIVisible(false);
        if (wantGiftButton != null) wantGiftButton.gameObject.SetActive(!hasReceivedGiftFromPlayer);

        // 清空全局交互引用
        if (CustomerManager.instance.currentInteractingNPC == this)
            CustomerManager.instance.currentInteractingNPC = null;
        ItemBagManager.instance.giftResourceType = ResourceType.None;
        ItemBagManager.instance.customerGiftImage = null;
    }
    private IEnumerator InteractWithPlayerCoroutine()
    {
        isInteractingWithPlayer = true;
        CustomerManager.instance.currentInteractingNPC = this; // ✅ 记录为当前交互 NPC

        SetInteractionPanelVisible(true);
        // 每次开始与玩家交互时，重置交互相关 UI 的初始状态
        SetGiftPanelVisible(false);
        SetBagUIVisible(false);
        isInGiftFlow = false;
        if (wantGiftButton != null) wantGiftButton.gameObject.SetActive(!hasReceivedGiftFromPlayer);
        if (hireButton != null) hireButton.gameObject.SetActive(CanBeRecruitedAsCook());

        animator.SetBool("isRunning", false);
        ShowCustomBubble("你好呀～", interactionDuration);

        // 对话期间：持续面向玩家（仅左/右），并在玩家超出距离时解除交互
        float elapsed = 0f;
        while (elapsed < interactionDuration)
        {
            if (playerTransform == null)
            {
                ForceStopInteraction();
                yield break;
            }

            // 持续面向玩家方向（2D：只左右）
            FaceToward(playerTransform.position);

            float dist = Vector3.Distance(playerTransform.position, transform.position);
            if (dist > maxPlayerInteractionDistance)
            {
                ForceStopInteraction();
                yield break;
            }

            // 若玩家进入送礼流程，则暂停结束对话的计时；提交礼物后再继续计时
            if (!isInGiftFlow)
            {
                elapsed += Time.deltaTime;
            }
            yield return null;
        }

        // 🔸 结束交互
        isInteractingWithPlayer = false;
        isInGiftFlow = false;
        SetInteractionPanelVisible(false);
        SetGiftPanelVisible(false);
        SetBagUIVisible(false);
        if (CustomerManager.instance.currentInteractingNPC == this)
            CustomerManager.instance.currentInteractingNPC = null;
        ItemBagManager.instance.giftResourceType = ResourceType.None;
        ItemBagManager.instance.customerGiftImage = null;
    }

    private void UpdateBubblePosition()
    {

    }

    private void StartBubbleRoutine()
    {
        if (bubbleRoutineCoroutine != null)
            StopCoroutine(bubbleRoutineCoroutine);

        bubbleRoutineCoroutine = StartCoroutine(BubbleUpdateCoroutine());
    }

    private IEnumerator BubbleUpdateCoroutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(Random.Range(bubbleInterval - bubbleRandomness, bubbleInterval + bubbleRandomness));

            if (state == CustomerState.Leaving || bubble == null)
                break;

            // 根据状态显示不同气泡
            ShowThoughtBubble();
        }
    }

    private void ShowThoughtBubble()
    {

        if (bubble == null || bubbleText == null || isInteractingWithPlayer)
            return;

        // 普通弹出文字时：交互面板应隐藏（仅玩家交互时显示）
        SetInteractionPanelVisible(false);

        string thoughtText = "";

        switch (state)
        {
            case CustomerState.Spawning:
                //thoughtText = GetRandomThought(new string[] { "肚子好饿...", "好想吃饭", "看看有什么好吃的" });
                break;

            case CustomerState.WalkingToQueue:
                thoughtText = GetRandomThought(data.WalkingToQueueWords.ToArray());
                break;

            case CustomerState.Queueing:
                thoughtText = GetRandomThought(data.QueueingWords.ToArray());
                break;

            case CustomerState.InsideRestaurant:
                if (isConsuming)
                    thoughtText = GetRandomThought(data.InsideRestaurantConsumingWords.ToArray());
                else
                    thoughtText = GetRandomThought(data.InsideRestaurantQueueingWords.ToArray());
                break;

            case CustomerState.Leaving:
                //thoughtText = GetRandomThought(new string[] { "吃饱了", "下次再来", "味道不错", "回家咯" });
                break;
        }
        if (thoughtText != "")
        {
            ShowBubble(thoughtText);
        }

    }

    private string GetRandomThought(string[] thoughts)
    {
        if (thoughts == null || thoughts.Length == 0)
            return "";

        int index = Random.Range(0, thoughts.Length);
        return thoughts[index];
    }

    private void ShowBubble(string text)
    {
        if (bubble == null || bubbleText == null)
            return;

        // 普通气泡文字：交互面板应隐藏（仅玩家交互时显示）
        if (!isInteractingWithPlayer) SetInteractionPanelVisible(false);

        bubbleText.text = text;
        bubble.SetActive(true);

        // 启动定时关闭气泡
        if (bubbleHideCoroutine != null)
            StopCoroutine(bubbleHideCoroutine);

        bubbleHideCoroutine = StartCoroutine(HideBubbleAfterDelay());
    }

    private IEnumerator HideBubbleAfterDelay()
    {
        yield return new WaitForSeconds(bubbleDuration);

        if (bubble != null)
            bubble.SetActive(false);

        // ⚠️ 这里不再重启 StartBubbleRoutine()，避免协程冲突
    }

    public void SetTarget(Vector3 pos)
    {
        targetPosition = pos;
    }

    public void GoToPlate(Plate plate)
    {
        if (plate == null)
        {
            //没找到有食物的碟子
            targetPlate = null;
            state = CustomerState.Leaving;
            Transform exit = CustomerManager.instance.GetRandomExitPoint(transform.position);
            if (exit != null)
            {
                SetTarget(exit.position);
            }
            return;
        }

        targetPlate = plate;
        state = CustomerState.InsideRestaurant;
        SetTarget(new Vector3(plate.transform.position.x, plate.transform.position.y, transform.position.z));

        ShowBubble(GetRandomThought(data.InsideRestaurantQueueingWords.ToArray()));
    }

    public void LeaveRestaurant()
    {
        targetPlate = null;
        state = CustomerState.Leaving;
        Transform exit = CustomerManager.instance.GetRandomExitPoint(transform.position);
        if (exit != null)
        {
            SetTarget(exit.position);
        }
        //Debug.Log("D5555");
        ShowBubble(GetRandomThought(data.LeavingRestaurantWords.ToArray()));
    }
    public void LeaveRestaurantNoPlates()
    {
        targetPlate = null;
        state = CustomerState.Leaving;
        Transform exit = CustomerManager.instance.GetRandomExitPoint(transform.position);
        if (exit != null)
        {
            SetTarget(exit.position);
        }
        //Debug.Log("D5555");
        ShowBubble(GetRandomThought(data.noPlateFoodWords.ToArray()));
    }
    public void donotWantToEat()
    {
        targetPlate = null;
        state = CustomerState.Leaving;
        Transform exit = CustomerManager.instance.GetRandomExitPoint(transform.position);
        if (exit != null)
        {
            SetTarget(exit.position);
        }

        //ShowBubble("吃饱回家咯~");
    }


    private void MoveToTarget()
    {
        targetPosition = new Vector3(targetPosition.x, 4.036f, targetPosition.z);

        Vector3 directionToTarget = targetPosition - transform.position;
        directionToTarget.y = 0;

        transform.position = Vector3.MoveTowards(
            transform.position,
            targetPosition,
            data.moveSpeed * Time.deltaTime
        );

        FaceByDirection(directionToTarget);

        if (state == CustomerState.Queueing && Vector3.Distance(transform.position, targetPosition) < 0.1f)
        {
            if (CustomerManager.instance != null && CustomerManager.instance.queueFrontPoint != null)
                FaceToward(CustomerManager.instance.queueFrontPoint.position);
            return;
        }

        if (Vector3.Distance(transform.position, targetPosition) < 0.1f && state != CustomerState.Queueing)
        {
            OnReachTarget();
        }
    }

    private void OnReachTarget()
    {
        if (state == CustomerState.InsideRestaurant && targetPlate != null)
        {
            StartCoroutine(ConsumeDishCoroutine());
        }
    }

    private IEnumerator ConsumeDishCoroutine()
    {
        if (targetPlate == null || targetPlate.currentDish == null || targetPlate.currentDish.IsEmpty())
        {
            //Debug.Log("D11111");
            LeaveRestaurant();
            yield break;
        }

        isConsuming = true;
        ShowBubble("开动了！");
        float consumeTime = targetPlate.consumeTime;

        float elapsed = 0f;
        while (elapsed < consumeTime)
        {
            if (targetPlate.currentDish == null || targetPlate.currentDish.IsEmpty())
            {
                isConsuming = false;
                //Debug.Log("D22222");
                LeaveRestaurant();
                yield break;
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        float cost = 0;
        if (targetPlate.currentDish != null)
        {
            cost = targetPlate.currentDish.recipe.baseDishPrice;
        }



        StartCoroutine(FinishConsumeInteractionCoroutine(cost));
    }
    private IEnumerator FinishConsumeInteractionCoroutine(float cost)
    {
        // 1️⃣ 面向餐碟（2D：只左/右）
        if (targetPlate != null)
            FaceToward(targetPlate.transform.position);

        // 2️⃣ 顾客跳一下表示满意
        Vector3 originalPos = transform.position;
        float jumpHeight = 1.2f;
        float jumpDuration = 0.6f;
        float jumpElapsed = 0f;
        if (data.favouriteFood.Contains(targetPlate.currentDish.recipe.dishID) && targetPlate.currentDish.currentAmount >= 2)
        {
            ShowCustomBubble("这是我的最爱！", jumpDuration * 2 + 1f); // 气泡显示时间要长一些
            AddAffection(5f);
            // 跳两次
            for (int i = 0; i < 2; i++)
            {
                jumpElapsed = 0f; // 重置时间
                while (jumpElapsed < jumpDuration / 2)
                {
                    float progress = jumpElapsed / (jumpDuration / 2);
                    float yOffset = Mathf.Sin(progress * Mathf.PI) * jumpHeight;
                    transform.position = new Vector3(originalPos.x, originalPos.y + yOffset, originalPos.z);
                    jumpElapsed += Time.deltaTime;
                    yield return null;
                }
                transform.position = originalPos;

                // 每次跳跃后爆金币
                int goldAmount = Mathf.RoundToInt(cost);
                Transform moneyBox = CustomerManager.instance.moneyBoxTransform;
                if (moneyBox != null)
                {
                    StartCoroutine(SpawnMoneySmoothly(goldAmount, coinSpawnPoint, moneyBox));
                }

                // 如果不是最后一次跳跃，等待一下
                if (i < 1)
                {
                    yield return new WaitForSeconds(0.5f);
                }
                targetPlate.StartConsume();
            }
        }
        else
        {
            // 非最爱菜品，只跳一次
            ShowCustomBubble("太好吃了！", jumpDuration);
            while (jumpElapsed < jumpDuration)
            {
                float progress = jumpElapsed / jumpDuration;
                float yOffset = Mathf.Sin(progress * Mathf.PI) * jumpHeight;
                transform.position = new Vector3(originalPos.x, originalPos.y + yOffset, originalPos.z);
                jumpElapsed += Time.deltaTime;
                yield return null;
            }
            transform.position = originalPos;

            // 爆金币
            int goldAmount = Mathf.RoundToInt(cost);
            Transform moneyBox = CustomerManager.instance.moneyBoxTransform;
            if (moneyBox != null)
            {
                StartCoroutine(SpawnMoneySmoothly(goldAmount, coinSpawnPoint, moneyBox));
            }
            targetPlate.StartConsume();
        }
        yield return new WaitForSeconds(0.5f);


        // 3️⃣ 爆金币逻辑 —— 菜价换金币特效




        // 4️⃣ 顾客完成吃饭，准备离开
        isConsuming = false;
        LeaveRestaurant();
    }
    private IEnumerator SpawnMoneySmoothly(int totalAmount, Transform start, Transform target)
    {
        if (totalAmount <= 0) yield break;

        ProjectileLauncher launcher = CustomerManager.instance.projectileLauncher;
        if (launcher == null || target == null) yield break;

        // 表现上最多生成 maxVisualCoins 个金币，金额按份分配，总和仍为 totalAmount
        int numProjectiles = Mathf.Min(totalAmount, Mathf.Max(1, maxVisualCoins));
        int baseAmount = totalAmount / numProjectiles;
        int remainder = totalAmount % numProjectiles;

        float spawnInterval = 0.05f;

        for (int i = 0; i < numProjectiles; i++)
        {
            int amountForThis = baseAmount + (i < remainder ? 1 : 0);
            int capture = amountForThis; // 闭包捕获
            launcher.SpawnProjectile(
                start,
                target,
                ResourceType.Money,
                capture,
                () => { MoneyChest.Instance.AddMoney(capture); }
            );
            yield return new WaitForSeconds(spawnInterval);
        }
    }
    public void SetState(CustomerState newState)
    {
        if (state == newState) return;

        state = newState;
        ShowThoughtBubble();
    }

    public void ShowCustomBubble(string text, float duration = 2f)
    {
        if (string.IsNullOrEmpty(text) || bubble == null) return;

        // 如果该 NPC 当前未激活，则不再尝试开启协程，避免
        // "Coroutine couldn't be started because the game object is inactive" 报错
        if (!isActiveAndEnabled)
        {
            return;
        }

        // 普通弹字（非交互 UI）：隐藏交互面板；玩家交互协程会重新打开
        if (!isInteractingWithPlayer) SetInteractionPanelVisible(false);

        if (bubbleHideCoroutine != null)
            StopCoroutine(bubbleHideCoroutine);

        bubbleDuration = duration;
        bubbleText.text = text;
        bubble.SetActive(true);

        bubbleHideCoroutine = StartCoroutine(HideBubbleAfterDelay());
    }

    void OnDestroy()
    {
        manager?.RemoveCustomer(this);

        if (bubbleRoutineCoroutine != null)
            StopCoroutine(bubbleRoutineCoroutine);

        if (bubbleHideCoroutine != null)
            StopCoroutine(bubbleHideCoroutine);
    }
    public Image customerGiftImage;
    public ResourceType giftResourceType;

    [Header("UI逻辑引用")]
    public GameObject interactionPanel;    // 交互面板
    public GameObject giftPanel;           // 赠礼面板
    public Button wantGiftButton;          // 想要赠礼按钮
    public Button giftSubmitButton;        // 赠送提交按钮
    public Button hireButton;              // 雇佣按钮

    // 兼容旧字段（场景/Prefab 里可能仍引用这个名字）
    public GameObject GiftPanel;

    private void SetInteractionPanelVisible(bool visible)
    {
        if (interactionPanel != null) interactionPanel.SetActive(visible);
    }

    private void SetGiftPanelVisible(bool visible)
    {
        GameObject panel = giftPanel != null ? giftPanel : GiftPanel;
        if (panel != null) panel.SetActive(visible);
    }

    private void SetBagUIVisible(bool visible)
    {
        if (ItemBagManager.instance == null || ItemBagManager.instance.bagAnimatedController == null) return;
        if (visible) ItemBagManager.instance.bagAnimatedController.ShowUI();
        else ItemBagManager.instance.bagAnimatedController.HideUI();
    }

    /// <summary>
    /// 给“想要赠礼按钮”调用：显示赠礼面板、隐藏想要赠礼按钮，并显示背包 UI。
    /// </summary>
    public void OnWantGiftButtonClicked()
    {
        if (hasReceivedGiftFromPlayer) return;
        isInGiftFlow = true;
        ItemBagManager.instance.customerGiftImage = customerGiftImage;
        SetGiftPanelVisible(true);
        if (wantGiftButton != null) wantGiftButton.gameObject.SetActive(false);
        SetBagUIVisible(true);
    }

    /// <summary>
    /// 给“赠送提交按钮”调用：提交赠送，完成后隐藏赠礼面板与背包 UI。
    /// </summary>
    public void OnGiftSubmitButtonClicked()
    {
        onSendGift();
    }

    /// <summary>
    /// 给“雇佣按钮”调用：雇佣（转职为厨师）。
    /// </summary>
    public void OnHireButtonClicked()
    {
        TryConvertToCook();
    }

    public void onGiftButtonDown()
    {
        ItemBagManager.instance.customerGiftImage = customerGiftImage;
    }
    public void onSendGift()
    {
        if (hasReceivedGiftFromPlayer)
        {
            // 已送过礼则不重复消耗资源/重复提交
            SetGiftPanelVisible(false);
            SetBagUIVisible(false);
            if (wantGiftButton != null) wantGiftButton.gameObject.SetActive(false);
            return;
        }

        if (data.favouriteItems.Contains(ItemBagManager.instance.giftResourceType))
        {
            ShowCustomBubble("谢谢你！我很喜欢", 1);
            //增加好感度的逻辑写在这里
            AddAffection(5f); // ✅ 增加好感度
        }
        else
        {
            ShowCustomBubble("噢，是礼物！", 1);
        }

        ItemBagManager.instance.SendGift();
        hasReceivedGiftFromPlayer = true;
        isInGiftFlow = false;
        SetGiftPanelVisible(false);
        SetBagUIVisible(false);
        if (wantGiftButton != null) wantGiftButton.gameObject.SetActive(false);
    }

    public void onGuestButtonDown()
    {
        StartCoroutine(GuestWithPlayer());
    }
    private IEnumerator GuestWithPlayer()
    {
        isGuesting = true;
        guestOriginPosition = transform.position;
        Vector3 originPlayerPostion = GameObject.FindGameObjectWithTag("Player").transform.position;
        TopDownController player = GameObject.FindGameObjectWithTag("Player").GetComponent<TopDownController>();
        player.GetComponent<Rigidbody>().isKinematic = true;
        player.canPlayerMove = false;

        GameObject.FindGameObjectWithTag("Player").transform.position = HomeManager.instance.myChair.transform.position;
        Vector3 playerRotation = -1 * (player.gameObject.transform.position - HomeManager.instance.table.transform.position);
        playerRotation.y = 0;
        Quaternion playertargetRotation = Quaternion.LookRotation(playerRotation);
        player.gameObject.transform.rotation = playertargetRotation;


        transform.position = HomeManager.instance.guestChair.transform.position;
        if (HomeManager.instance != null && HomeManager.instance.table != null)
            FaceToward(HomeManager.instance.table.transform.position);


        ShowCustomBubble("承蒙招待，前来拜访。", 1);
        yield return new WaitForSeconds(1);
        ShowCustomBubble("好茶，好茶", 1);
        yield return new WaitForSeconds(1);
        ShowCustomBubble("吃茶点！", 1);
        yield return new WaitForSeconds(1);
        player.gameObject.transform.position = originPlayerPostion;
        player.canPlayerMove = true;
        transform.position = guestOriginPosition;
        isGuesting = false;
        player.GetComponent<Rigidbody>().isKinematic = false;
        ForceStopInteraction();
        AddAffection(10f);
    }

    //TODO，检查好感度是否满足转换成厨师需要的等级，如果满足则转换成厨师，立即中断当前行为和状态，转成厨师的状态，需要做菜时移动到锅旁上下跳动，做完菜则回到闲逛模式，在CookManager的Left和Right的Point之间移动
    // =================== 厨师系统逻辑 ===================

    // 检查是否满足招募条件
    public bool CanBeRecruitedAsCook()
    {
        if (data == null) return false;
        int requiredLevel = Mathf.Max(0, data.recruitCookRequiredAffectionLevel);
        if (data.affectionLevel < requiredLevel) return false;
        if (data.isCook) return false;

        // 受最大厨师数量限制
        if (CookUIManager.instance != null && !CookUIManager.instance.CanRecruitMore())
            return false;

        return true;
    }

    // 由外部或NPC自身调用：尝试转职为厨师
    public void TryConvertToCook()
    {
        if (!CanBeRecruitedAsCook()) return;
        ConvertToCook();
    }

    // 立即转职为厨师
    public void ConvertToCook()
    {
        // 中断原有顾客行为
        StopAllCoroutines();
        isInteractingWithPlayer = false;
        isConsuming = false;
        state = CustomerState.Spawning;

        // 转职后不再需要顾客交互UI
        SetInteractionPanelVisible(false);
        SetGiftPanelVisible(false);
        SetBagUIVisible(false);
        if (wantGiftButton != null) wantGiftButton.gameObject.SetActive(false);
        if (hireButton != null) hireButton.gameObject.SetActive(false);

        // 状态转为厨师
        data.isCook = true;
        cookState = CookState.Idle;

        // 厨师状态下，确保 NPC 子物体 SpriteRenderer 的显示层级正确
        SetCookChildSpriteSortingOrder0();

        // 刚被雇佣时，立即瞬移到 Left / Right 其中一个点
        Vector3 initialCookTargetPos = transform.position;
        if (CookManager.cookManager != null)
        {
            Transform left = CookManager.cookManager.kitchenLeftPoint;
            Transform right = CookManager.cookManager.kitchenRightPoint;

            Transform startPoint = null;
            if (left != null && right != null)
            {
                startPoint = Random.value > 0.5f ? left : right;
            }
            else if (left != null)
            {
                startPoint = left;
            }
            else if (right != null)
            {
                startPoint = right;
            }

            if (startPoint != null)
            {
                Vector3 dest = startPoint.position;
                transform.position = new Vector3(dest.x, dest.y, dest.z);
                initialCookTargetPos = transform.position;

                // 面向巡逻方向（朝向另一个点，如果存在）
                Transform otherPoint = (startPoint == left) ? right : left;
                if (otherPoint != null)
                {
                    Vector3 dir = (otherPoint.position - transform.position);
                    dir.y = 0;
                    if (dir.sqrMagnitude > 0.001f)
                    {
                        FaceByDirection(dir);
                    }
                }
            }
        }

        if (!CookManager.cookManager.curCookList.Contains(this))
            CookManager.cookManager.curCookList.Add(this);

        CookUIManager.instance?.OnChefRecruited(this);

        ShowCustomBubble("我愿意帮忙做菜！", 2f);

        cookRoutineCoroutine = StartCoroutine(CookRoutine(initialCookTargetPos));
    }

    private void SetCookChildSpriteSortingOrder0()
    {
        // 只针对 SpriteRenderer：避免误影响其它 Renderer（例如 UI Image）
        SpriteRenderer[] renderers = GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null) renderers[i].sortingOrder = 0;
        }
    }

    // 厨师工作主循环：在Left与Right之间巡逻，当被要求做菜时去锅旁上下跳动
    private IEnumerator CookRoutine(Vector3 initialTargetPos)
    {
        Transform left = CookManager.cookManager.kitchenLeftPoint;
        Transform right = CookManager.cookManager.kitchenRightPoint;
        // 当前巡逻目标（在 Left / Right 之间的随机点）
        Vector3 currentTargetPos = initialTargetPos;
        // 厨师移动速度稍慢一点，显得更从容
        float cookMoveSpeed = data.moveSpeed * 0.6f;
        // 张望行为的冷却时间，避免一直说话
        float idleCooldown = 0f;
        bool isFirstTick = true; // 首帧到达目标时不立刻随机，避免“刚瞬移就又慢慢走”

        while (data.isCook)
        {

            // 🔸 如果在烹饪状态中，则暂停闲逛
            if (cookState == CookState.Cooking)
            {
                yield return null;
                continue;
            }

            // 🔸 更新张望冷却
            if (idleCooldown > 0f)
                idleCooldown -= Time.deltaTime;

            // 🔸 移动到目标点
            Vector3 dest = currentTargetPos;
            transform.position = Vector3.MoveTowards(transform.position, dest, cookMoveSpeed * Time.deltaTime);

            // 🔸 面向移动方向
            Vector3 dir = dest - transform.position;
            FaceByDirection(dir);

            // 🔸 在移动过程中有一定几率触发停下来张望 + 说话（带冷却）
            float idleTriggerChancePerFrame = 0.004f; // 已降低频率
            if (idleCooldown <= 0f && Random.value < idleTriggerChancePerFrame)
            {
                yield return StartCoroutine(CookIdleLookAroundRoutine());
                // 触发一次后，进入冷却，避免太频繁
                idleCooldown = Random.Range(4f, 7f);
            }

            // 🔸 如果到达当前随机目标点，则重新随机一个目标点（仍在 Left/Right 之间）
            if (!isFirstTick && Vector3.Distance(transform.position, dest) < 0.15f)
            {
                currentTargetPos = GetRandomPointBetween(left, right);
            }

            isFirstTick = false;
            yield return null;
        }
    }

    // 厨师在巡逻途中停下来四处张望、说话
    private IEnumerator CookIdleLookAroundRoutine()
    {
        // 先停一小会
        float idleDuration = Random.Range(1.5f, 3f);
        float elapsed = 0f;

        // 说几句与工作相关的话
        string[] cookThoughts =
        {
            "今天客人好多呀……",
            "要把每一道菜都做好！",
            "看看还有哪口锅需要帮忙。",
            "嗯……味道好像不错。",
            "要注意火候。"
        };
        ShowCustomBubble(GetRandomThought(cookThoughts), idleDuration);

        // 随机左右张望几次
        int lookTimes = Random.Range(1, 3);
        for (int i = 0; i < lookTimes; i++)
        {
            // 2D：只允许左右张望
            FaceByX(Random.value > 0.5f ? 1f : -1f);
            elapsed += 0.25f;
            yield return new WaitForSeconds(0.25f);

            if (elapsed >= idleDuration)
                break;

            // 稍微停顿一下再看另一边
            float pause = Random.Range(0.2f, 0.5f);
            float pauseElapsed = 0f;
            while (pauseElapsed < pause && elapsed < idleDuration)
            {
                pauseElapsed += Time.deltaTime;
                elapsed += Time.deltaTime;
                yield return null;
            }
        }
    }

    // 在厨房左右点之间随机取一个点（线段内插）
    private Vector3 GetRandomPointBetween(Transform left, Transform right)
    {
        if (left == null && right == null)
            return transform.position;

        if (left == null) return right.position;
        if (right == null) return left.position;

        float t = Random.Range(0f, 1f);
        Vector3 p = Vector3.Lerp(left.position, right.position, t);
        return p;
    }



    // 解雇厨师（从CookManager移除）
    public void FireCook()
    {
        StopAllCoroutines();
        if (CookManager.cookManager != null)
        {
            CookManager.cookManager.curCookList.Remove(this);
        }

        CookUIManager.instance?.OnChefFired(this);

        data.isCook = false;
        // 解雇后直接销毁 NPC，避免还走“闲逛/离开”逻辑造成视觉错觉
        Destroy(gameObject);
    }

    // 被锅调用的协同函数：锅请求厨师帮忙烹饪
    public IEnumerator HelpPotCooking(Pot pot)
    {
        if (!data.isCook || pot == null || isCookingNow)
            yield break;

        // 标记为工作中
        isCookingNow = true;
        cookState = CookState.Cooking;

        ShowCustomBubble($"帮忙煮 {pot.currentRecipe?.dishName ?? "菜"}！", 2f);

        // 移动到锅前：只对齐 X 轴，Y、Z 保持 NPC 当前位置不变，且始终面朝锅的方向（不倒着走）
        float cookMoveSpeed = data.moveSpeed * 0.7f;
        float targetX = pot.transform.position.x;
        Vector3 workPos = new Vector3(targetX, transform.position.y, transform.position.z);
        while (Mathf.Abs(transform.position.x - targetX) > 0.05f)
        {
            float newX = Mathf.MoveTowards(transform.position.x, targetX, cookMoveSpeed * Time.deltaTime);
            transform.position = new Vector3(newX, transform.position.y, transform.position.z);
            FaceToward(pot.transform.position);
            yield return null;
        }
        transform.position = workPos;

        FaceToward(pot.transform.position);

        // 做菜时上下跳动，并期间随机说几句话
        float jumpHeight = 0.6f;
        float cookDuration = pot.currentRecipe != null ? pot.currentRecipe.cookTime * 0.5f : 3f;
        float elapsed = 0f;
        float nextBubbleTime = Random.Range(0.8f, 1.5f);
        string[] cookingWords = { "火候刚好~", "马上就好！", "再翻炒两下…", "香味出来了！", "注意别糊了。" };
        while (elapsed < cookDuration)
        {
            float yOffset = Mathf.Sin((elapsed % 0.4f) / 0.4f * Mathf.PI) * jumpHeight;
            transform.position = new Vector3(workPos.x, workPos.y + yOffset, workPos.z);
            if (elapsed >= nextBubbleTime)
            {
                ShowCustomBubble(GetRandomThought(cookingWords), 1.2f);
                nextBubbleTime = elapsed + Random.Range(1f, 2f);
            }
            elapsed += Time.deltaTime;
            yield return null;
        }
        transform.position = workPos;

        ShowCustomBubble("加速烹饪！", 1.5f);

        // 返回闲逛区：移动时面朝目的地，而不是继续朝锅
        Transform left = CookManager.cookManager.kitchenLeftPoint;
        Transform right = CookManager.cookManager.kitchenRightPoint;
        Vector3 backPos = Random.value > 0.5f ? left.position : right.position;
        while (Vector3.Distance(transform.position, backPos) > 0.1f)
        {
            transform.position = Vector3.MoveTowards(transform.position, backPos, cookMoveSpeed * Time.deltaTime);
            FaceToward(backPos);
            yield return null;
        }

        cookState = CookState.Idle;
        isCookingNow = false;  // ✅ 标记为空闲
    }

    // 实际Buff逻辑：减少时间、增加产出与价格
    public void ApplyCookingBuffToPot(Pot pot)
    {
        if (pot.currentRecipe == null) return;

        pot.currentRecipe.cookTime *= data.timeReductionRate;
        pot.currentRecipe.baseDishPrice *= data.priceIncreaseRate;
        // 若有产出数量概念，可记录在菜对象上
    }

}