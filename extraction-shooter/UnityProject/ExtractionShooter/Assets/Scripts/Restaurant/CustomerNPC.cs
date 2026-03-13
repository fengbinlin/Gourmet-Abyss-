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
    private float interactionDuration = 10f;      // 暂停持续时间（可自行调整）
    private Transform playerTransform;             // 玩家对象（运行时获取）
    public CustomerData data;
    public CustomerState state = CustomerState.Spawning;
    public GameObject bubble;         // 气泡对象
    public Text bubbleText;           // 气泡文本组件
    private Vector3 targetPosition;
    private Plate targetPlate;
    private bool isConsuming = false;
    private CustomerManager manager;

    public Transform coinSpawnPoint;
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

    public void Init()
    {
        manager = CustomerManager.instance;

        if (bubble != null)
            bubble.SetActive(false);

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
        if (!data.isCook && !isConsuming && !isInteractingWithPlayer && !isGuesting) // 🔸 如果正在互动则暂停
        {
            MoveToTarget();

            // 如果正在离开且到达目标，销毁自己
            if (state == CustomerState.Leaving && Vector3.Distance(transform.position, targetPosition) < 0.1f)
            {
                manager?.RemoveCustomer(this);
                Destroy(gameObject);
            }
        }

        animator.SetBool("isRunning", !isInteractingWithPlayer && Vector3.Distance(transform.position, targetPosition) >= 0.1f);

        UpdateBubblePosition();
    }

    public void ClickCustomer()
    {
        print("点击");

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
        if (bubble != null) bubble.SetActive(false);
        animator.SetBool("isRunning", true);

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

        animator.SetBool("isRunning", false);
        ShowCustomBubble("你好呀～", interactionDuration);

        // 面向玩家方向
        if (playerTransform != null)
        {
            Vector3 lookDir = playerTransform.position - transform.position;
            lookDir.y = 0;
            Quaternion targetRotation = Quaternion.LookRotation(lookDir);
            float rotateTime = 0.3f;
            float elapsed = 0f;

            while (elapsed < rotateTime)
            {
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, elapsed / rotateTime);
                elapsed += Time.deltaTime;
                yield return null;
            }
        }

        yield return new WaitForSeconds(interactionDuration);

        // 🔸 结束交互
        isInteractingWithPlayer = false;
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

        if (directionToTarget != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(directionToTarget);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
        }

        if (state == CustomerState.Queueing && Vector3.Distance(transform.position, targetPosition) < 0.1f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(CustomerManager.instance.queueFrontPoint.transform.position - transform.position);
            transform.rotation = targetRotation;
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
        // 1️⃣ 面向餐碟旋转
        Vector3 lookDir = targetPlate.transform.position - transform.position;
        lookDir.y = 0;
        Quaternion targetRotation = Quaternion.LookRotation(lookDir);
        float rotateTime = 0.5f;
        float elapsed = 0f;

        while (elapsed < rotateTime)
        {
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, elapsed / rotateTime);
            elapsed += Time.deltaTime;
            yield return null;
        }

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

        int remaining = totalAmount;

        float spawnInterval = 0f; // 每0.05秒一批
        int batchMin = 5;
        int batchMax = 5; // 每次发几个，可根据需求调节

        ProjectileLauncher launcher = CustomerManager.instance.projectileLauncher;
        if (launcher == null || target == null) yield break;

        while (remaining > 0)
        {
            int thisBatch = Mathf.Min(Random.Range(batchMin, batchMax + 1), remaining);

            launcher.SpawnProjectile(
                start,
                target,
                ResourceType.Money,
                thisBatch,
                () =>
                {
                    MoneyChest.Instance.AddMoney(thisBatch);
                }
            );

            remaining -= thisBatch;
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
    public GameObject GiftPanel;
    public void onGiftButtonDown()
    {
        ItemBagManager.instance.customerGiftImage = customerGiftImage;
    }
    public void onSendGift()
    {
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
        GiftPanel.SetActive(false);
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
        Vector3 NPCRotation = -1 * (transform.position - HomeManager.instance.table.transform.position);
        NPCRotation.y = 0;
        Quaternion NPCtargetRotation = Quaternion.LookRotation(NPCRotation);
        transform.rotation = NPCtargetRotation;


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
        // 满足好感度等级阈值，例如至少 Lv3
        int requiredLevel = 3;
        return data != null && data.affectionLevel >= requiredLevel && !data.isCook;
    }

    // 由外部或NPC自身调用：尝试转职为厨师
    public void TryConvertToCook()
    {
        //if (!CanBeRecruitedAsCook()) return;
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

        // 状态转为厨师
        data.isCook = true;
        cookState = CookState.Idle;

        if (!CookManager.cookManager.curCookList.Contains(this))
            CookManager.cookManager.curCookList.Add(this);

        ShowCustomBubble("我愿意帮忙做菜！", 2f);

        cookRoutineCoroutine = StartCoroutine(CookRoutine());
    }

    // 厨师工作主循环：在Left与Right之间巡逻，当被要求做菜时去锅旁上下跳动
    private IEnumerator CookRoutine()
    {
        Transform left = CookManager.cookManager.kitchenLeftPoint;
        Transform right = CookManager.cookManager.kitchenRightPoint;

        // 当前巡逻目标
        Transform currentTarget = right;
        float waitBetween = Random.Range(1f, 2f);
        Debug.Log("AAA");
        while (data.isCook)
        {

            // 🔸 如果在烹饪状态中，则暂停闲逛
            if (cookState == CookState.Cooking)
            {
                yield return null;
                continue;
            }

            // 🔸 实时判断往哪个方向走
            if (Vector3.Distance(transform.position, right.position) < 0.2f)
            {
                Debug.Log("切换坐标为左");
                // 已经到达右端，下一个目标改为左端
                currentTarget = left;
            }
            else if (Vector3.Distance(transform.position, left.position) < 0.2f)
            {
                Debug.Log("切换坐标为右");
                // 到达左端，下一个目标改为右端
                currentTarget = right;
            }
            Debug.Log("当前目标" + currentTarget);
            // 🔸 移动到目标点
            Vector3 dest = new Vector3(currentTarget.position.x, currentTarget.position.y, currentTarget.position.z);
            transform.position = Vector3.MoveTowards(transform.position, dest, data.moveSpeed * Time.deltaTime);
            Debug.Log("目的地" + dest);
            Debug.Log("当前坐标" + transform.position);
            // 🔸 面向移动方向
            Vector3 dir = dest - transform.position;
            if (dir.sqrMagnitude > 0.001f)
            {
                Quaternion rot = Quaternion.LookRotation(dir);
                transform.rotation = Quaternion.Slerp(transform.rotation, rot, Time.deltaTime * rotationSpeed);
            }

            // 🔸 如果到达目标点则停顿一下
            // if (Vector3.Distance(transform.position, dest) < 0.2f)
            // {
            //     yield return new WaitForSeconds(waitBetween);
            //     waitBetween = Random.Range(1f, 2f);
            // }

            yield return null;
        }
    }



    // 解雇厨师（从CookManager移除）
    public void FireCook()
    {
        StopAllCoroutines();
        if (CookManager.cookManager != null)
        {
            CookManager.cookManager.curCookList.Remove(this);
        }
        data.isCook = false;
        ShowCustomBubble("离开厨房……", 2f);
        LeaveRestaurant();  // 恢复离开状态
        Destroy(gameObject, 2f);
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

        // 移动到锅旁逻辑……
        Vector3 workPos = pot.transform.position + new Vector3(0, 0, -2f);
        while (Vector3.Distance(transform.position, workPos) > 0.1f)
        {
            transform.position = Vector3.MoveTowards(transform.position, workPos, data.moveSpeed * Time.deltaTime);
            yield return null;
        }

        // 上下跳动逻辑……
        float jumpHeight = 0.6f;
        float cookDuration = pot.currentRecipe != null ? pot.currentRecipe.cookTime * 0.5f : 3f;
        float elapsed = 0f;
        while (elapsed < cookDuration)
        {
            float yOffset = Mathf.Sin((elapsed % 0.4f) / 0.4f * Mathf.PI) * jumpHeight;
            transform.position = new Vector3(workPos.x, workPos.y + yOffset, workPos.z);
            elapsed += Time.deltaTime;
            yield return null;
        }
        transform.position = workPos;

        //ApplyCookingBuffToPot(pot);
        ShowCustomBubble("加速烹饪！", 1.5f);

        // 返回闲逛区
        Transform left = CookManager.cookManager.kitchenLeftPoint;
        Transform right = CookManager.cookManager.kitchenRightPoint;
        Vector3 backPos = Random.value > 0.5f ? left.position : right.position;
        while (Vector3.Distance(transform.position, backPos) > 0.1f)
        {
            transform.position = Vector3.MoveTowards(transform.position, backPos, data.moveSpeed * Time.deltaTime);
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