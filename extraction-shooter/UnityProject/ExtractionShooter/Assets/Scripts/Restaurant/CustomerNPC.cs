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

/// <summary>入场后取菜 → 端上桌 → 就餐的子阶段。</summary>
public enum CustomerDiningPhase
{
    None,
    WalkingToPlate,   // 走向碟子
    WalkingToSeat,    // 端着菜走向座位
    Eating            // 就坐用餐
}

public enum CustomerExpression
{
    Mischievous = 0, // 调皮
    Speechless = 1,  // 无语
    Serious = 2,     // 认真
    Crying = 3,      // 流泪
    Touched = 4,     // 感动
    Awkward = 5,     // 尴尬
    HeartEyes = 6,   // 爱心眼
    BadTaste = 7,    // 难吃
    Shy = 8,         // 害羞
    Surprised = 9    // 惊讶
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
    private CustomerNPCInfo customerNPCInfo;

    [Header("表情管理")]
    [Tooltip("表情贴图列表：按 CustomerExpression 枚举顺序存放（0调皮,1无语,2认真,3流泪,4感动,5尴尬,6爱心眼,7难吃,8害羞,9惊讶）。")]
    [SerializeField] private List<Texture> expressionTextures = new List<Texture>(10);
    [Tooltip("挂载表情材质的 SkinnedMeshRenderer（表情材质附加在该 Mesh 上）。")]
    [SerializeField] private SkinnedMeshRenderer expressionSkinRenderer;
    [Tooltip("SkinnedMeshRenderer.materials 中用于表情的材质槽位索引。")]
    [SerializeField] private int expressionMaterialSlot = 0;
    [Tooltip("表情贴图写入到材质的哪个纹理属性（默认 _MainTex）。")]
    [SerializeField] private string expressionTextureProperty = "_MainTex";

    private Material expressionMaterialInstance;
    private CustomerExpression currentExpression = CustomerExpression.Serious;
    public GameObject bubble;         // 气泡对象
    public Text bubbleText;           // 气泡文本组件
    private Vector3 targetPosition;
    private RestaurantSeat currentSeat;
    private Plate servedPlate;
    private DishRecipe _carriedRecipe;
    private int _carriedGoldAmount;
    private GameObject _carriedDishVisual;
    private bool isConsuming = false;
    private CustomerDiningPhase _diningPhase = CustomerDiningPhase.None;
    private bool _hasHandledPlateArrival;
    private bool _hasHandledSeatArrival;

    [Header("端菜展示")]
    [Tooltip("端菜时菜品预制体的挂点（为空则挂在顾客根节点）")]
    [SerializeField] private Transform carriedDishHoldPoint;
    [SerializeField] private Vector3 carriedDishLocalOffset = new Vector3(0.4f, 0.5f, 0f);
    private Transform _exitTargetTransform;
    private CustomerManager manager;

    [Header("鸟类高度（移动抬起、Idle落地）")]
    [SerializeField] private bool enableBirdGroundDrop = true;
    [SerializeField] private float moveYWalkingOffset = 0.35f; // 移动时相对地面的抬起高度（可调）
    [SerializeField] private float idleDropSpeed = 2.5f;         // idle 时落回地面的速度
    [SerializeField] private float moveHeightRiseSpeedMultiplier = 1f; // 移动时逼近目标抬起高度的速度
    [SerializeField] private float reachPlanarThreshold = 0.1f;
    [SerializeField] private float leavingDestroyYThreshold = 0.05f;

    // 2D（XY 平面）：排队时的 Y 轴错开量
    private float spawnLaneOffset = 0f;

    [Header("生成随机偏移（2D：Y 轴错开；Z 仅排序）")]
    [SerializeField] private bool enableSpawnRandomOffset = true;
    [SerializeField] private float spawnRandomYRange = 0.08f;
    [SerializeField] private float spawnRandomLaneRange = 0.08f;

    // 供 Manager 的配对聊天逻辑做“临时暂停/恢复”用
    public Vector3 CurrentTargetPosition => GetLiveTargetPosition();

    [Header("2D朝向（仅左/右）")]
    [SerializeField] private bool useScaleFlipForFacing = true;
    private int facingSign = 1; // 1=右，-1=左（以 X 轴方向为准）

    public Transform coinSpawnPoint;
    [Tooltip("飞向钱箱的金币最多生成数量，超出部分只加数值不生成抛射物")]
    [SerializeField] private int maxVisualCoins = 12;

    [Header("顾客金币飞行轨迹参数（面板配置）")]
    [SerializeField] private float moneyProjectileFlightDuration = 2f;
    [SerializeField] private float moneyProjectileMaxHeight = 5f;
    // 气泡相关变量
    private Coroutine bubbleRoutineCoroutine;  // 循环更新协程
    private Coroutine bubbleHideCoroutine;     // 气泡隐藏协程
    private Coroutine restaurantAmbienceRoutine;
    private float bubbleDuration = 4f;         // 气泡显示持续时间
    private float bubbleInterval = 3f;         // 气泡更新间隔
    private float bubbleRandomness = 2f;       // 气泡随机性

    public List<GameObject> playerModelList;
    public Animator animator;

    [Header("喜欢菜展示")]
    [SerializeField] private GameObject favouriteDishIconPrefab;
    [SerializeField] private Transform favouriteDishParent;
    private readonly List<GameObject> spawnedFavouriteDishIcons = new List<GameObject>();

    [Header("交互镜头缩放")]
    [SerializeField] private float interactionOrthoSize = 5.5f;
    [SerializeField] private float homeGuestOrthoSize = 5.0f;
    private string interactionCameraRequestKey;
    private string homeGuestCameraRequestKey;
    private string interactionCameraXFocusRequestKey;
    private string homeGuestCameraXFocusRequestKey;

    [Header("点击交互反馈")]
    [SerializeField] private float clickPulseDuration = 0.2f;
    [SerializeField] private float clickPulseScale = 0.12f;
    private Coroutine clickPulseCoroutine;
    private Vector3 defaultVisualScale;

    public float rotationSpeed = 5.0f; // 控制旋转平滑速度

    public Vector3 guestOriginPosition;
    public bool isGuesting;

    [Header("送礼状态")]
    [SerializeField] private bool hasReceivedGiftFromPlayer = false; // 同一个NPC生成后，玩家是否已送过礼（用于限制重复送礼UI）
    private bool isInGiftFlow = false; // 玩家已点“想要送礼”，暂停对话倒计时

    public void Init()
    {
        manager = CustomerManager.instance;
        interactionCameraRequestKey = $"customer_interaction_{GetInstanceID()}";
        homeGuestCameraRequestKey = $"customer_home_guest_{GetInstanceID()}";
        interactionCameraXFocusRequestKey = $"customer_interaction_x_{GetInstanceID()}";
        homeGuestCameraXFocusRequestKey = $"customer_home_guest_x_{GetInstanceID()}";

        EnsureExpressionMaterialInstance();
        SetExpression(CustomerExpression.Serious);
        defaultVisualScale = new Vector3(Mathf.Abs(transform.localScale.x), transform.localScale.y, transform.localScale.z);
        transform.localScale = defaultVisualScale;
        InitCustomerInfoView();
        RefreshFavouriteDishIcons();

        // 生成时增加轻微随机偏移，避免完全重合（2D：XY 平面）
        if (enableSpawnRandomOffset)
        {
            Vector3 p = transform.position;
            p.y += Random.Range(-spawnRandomYRange, spawnRandomYRange);
            spawnLaneOffset = Random.Range(-spawnRandomLaneRange, spawnRandomLaneRange);
            transform.position = p;
        }

        if (bubble != null)
            bubble.SetActive(false);

        SetInteractionPanelVisible(false);
        SetGiftPanelVisible(false);
        //SetBagUIVisible(false);

        UpdateAffectionUI(); // 初始化好感度UI
        StartBubbleRoutine();
    }

    private void InitCustomerInfoView()
    {
        customerNPCInfo = GetComponentInChildren<CustomerNPCInfo>(true);
        if (customerNPCInfo == null || data == null) return;
        customerNPCInfo.SetInfo(data.customerName, data.mbti);
    }

    private void RefreshFavouriteDishIcons()
    {
        ClearFavouriteDishIcons();

        if (favouriteDishIconPrefab == null || favouriteDishParent == null || data == null || data.favouriteFood == null)
            return;

        RestaurantPanel panel = RestaurantPanel.instance;
        if (panel == null) return;

        for (int i = 0; i < data.favouriteFood.Count; i++)
        {
            int dishId = data.favouriteFood[i];
            Sprite icon = panel.GetDishIconByID(dishId);
            if (icon == null) continue;

            GameObject iconGo = Instantiate(favouriteDishIconPrefab, favouriteDishParent);
            SetDishIconOnPrefab(iconGo, icon);
            spawnedFavouriteDishIcons.Add(iconGo);
        }
    }

    private void SetDishIconOnPrefab(GameObject iconGo, Sprite icon)
    {
        if (iconGo == null || icon == null) return;

        Image image = iconGo.GetComponentInChildren<Image>(true);
        if (image != null)
        {
            image.sprite = icon;
            return;
        }

        SpriteRenderer spriteRenderer = iconGo.GetComponentInChildren<SpriteRenderer>(true);
        if (spriteRenderer != null)
        {
            spriteRenderer.sprite = icon;
        }
    }

    private void ClearFavouriteDishIcons()
    {
        for (int i = 0; i < spawnedFavouriteDishIcons.Count; i++)
        {
            if (spawnedFavouriteDishIcons[i] != null)
            {
                Destroy(spawnedFavouriteDishIcons[i]);
            }
        }
        spawnedFavouriteDishIcons.Clear();
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
        int oldLevel = data.affectionLevel;
        float oldValue = data.affectionValue;

        data.AddAffection(amount);
        UpdateAffectionUI();

        // 全局消息提示：好感度提升/等级提升
        if (amount > 0f)
        {
            string name = string.IsNullOrEmpty(data.customerName) ? "顾客" : data.customerName;
            GlobalMessageUI.Show($"{name} 好感度 +{amount:0.#}", 1.2f);
        }

        if (data.affectionLevel > oldLevel)
        {
            string name = string.IsNullOrEmpty(data.customerName) ? "顾客" : data.customerName;
            GlobalMessageUI.Show($"{name} 好感等级提升：Lv.{data.affectionLevel}", 1.6f);
        }
    }

    void Update()
    {
        // 配对聊天的“预站位阶段”允许继续移动到目标点
        bool allowMoveNow = !isInteractingWithPlayer || isPairChatPositioning;
        bool isPickingUpAtPlate = state == CustomerState.InsideRestaurant
            && _diningPhase == CustomerDiningPhase.WalkingToPlate
            && _hasHandledPlateArrival;
        bool canWalk = !data.isCook && !isConsuming && !isPickingUpAtPlate && allowMoveNow && !isGuesting;
        if (canWalk) // 🔸 如果正在互动则暂停
        {
            MoveToTarget();

            // 如果正在离开且到达目标，销毁自己
            if (state == CustomerState.Leaving)
            {
                if (GetPlanarDistance(transform.position, GetLiveTargetPosition()) < reachPlanarThreshold)
                {
                    manager?.RemoveCustomer(this);
                    Destroy(gameObject);
                }
            }
        }

        float currentPlanarDist = GetPlanarDistance(transform.position, GetLiveTargetPosition());
        bool isMovingNow = canWalk && currentPlanarDist >= reachPlanarThreshold;
        animator.SetBool("isRunning", isMovingNow);

        // 排队时统一面朝右边
        if (state == CustomerState.Queueing)
        {
            FaceByX(1f);
        }

        UpdateBubblePosition();
    }

    private static float GetPlanarDistance(Vector3 a, Vector3 b)
    {
        return Vector2.Distance(new Vector2(a.x, a.y), new Vector2(b.x, b.y));
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
        // 注意：不能只在“方向变化”时设置旋转。
        // 否则初始 facingSign=1 且初始旋转=0 时，向右移动不会触发更新，导致看起来一直是 0°。
        facingSign = sign;

        // 需求：localScale.x 不能为负数。统一保持缩放为正，通过旋转来实现左右朝向。
        Vector3 s = transform.localScale;
        float absX = Mathf.Abs(s.x);
        transform.localScale = new Vector3(absX, s.y, s.z);

        // 用 Y 轴旋转模拟左右，并向屏幕外偏转 30°（右=120，左=-120）
        // 保留 useScaleFlipForFacing 字段以兼容 Inspector 配置，但不再使用负缩放。
        Quaternion desired = Quaternion.Euler(0f, facingSign == 1 ? 120f : -120f, 0f);
        if (Quaternion.Angle(transform.rotation, desired) > 0.1f)
        {
            transform.rotation = desired;
        }
    }

    public void FaceToward(Vector3 worldPos)
    {
        Vector3 dir = worldPos - transform.position;
        dir.y = 0f;
        FaceByDirection(dir);
    }

    public bool CanDoCustomerPairChat()
    {
        // 需求：只允许在离开阶段聊天（排队/去排队阶段不允许触发相互对话）
        if (data != null && data.isCook) return false;
        if (isGuesting) return false;
        if (isConsuming) return false;
        if (isInteractingWithPlayer) return false; // 正在与玩家交互/其它交互中，不参与配对聊天

        // 离开点附近不再触发聊天，避免“聊天结束瞬间到点就销毁”的突兀观感
        if (state == CustomerState.Leaving)
        {
            if (GetPlanarDistance(transform.position, targetPosition) < 0.8f) return false;
        }

        return state == CustomerState.Leaving;
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

        // 交互中或做客中，不再播放点击特效，避免反复点击导致视觉异常
        bool shouldPlayClickPulse = !isInteractingWithPlayer && !isGuesting;

        // 仅禁止“正在配对聊天中的该NPC”被点击，其他NPC仍可交互
        if (isPairChatPositioning)
        {
            return;
        }
        if (CustomerManager.instance != null && CustomerManager.instance.IsPairChatting && isInteractingWithPlayer)
        {
            return;
        }

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

        SetExpression(CustomerExpression.Surprised);

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
            if (shouldPlayClickPulse)
            {
                StartClickPulse();
            }
            StartCoroutine(InteractWithPlayerCoroutine());
        }
        ItemBagManager.instance.customerGiftImage = customerGiftImage;
    }
    public void ForceStopInteraction()
    {
        if (!isInteractingWithPlayer) return;

        StopAllCoroutines();  // 停止当前交互协程
        restaurantAmbienceRoutine = null;
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
        CameraFollow.PopOrthoSizeRequest(interactionCameraRequestKey);
        CameraFollow.PopXFocusRequest(interactionCameraXFocusRequestKey);
    }
    private IEnumerator InteractWithPlayerCoroutine()
    {
        isInteractingWithPlayer = true;
        CustomerManager.instance.currentInteractingNPC = this; // ✅ 记录为当前交互 NPC
        CameraFollow.PushOrthoSizeRequest(interactionCameraRequestKey, interactionOrthoSize);
        CameraFollow.PushXFocusRequest(interactionCameraXFocusRequestKey, transform.position.x);

        SetInteractionPanelVisible(true);
        // 每次开始与玩家交互时，重置交互相关 UI 的初始状态
        SetGiftPanelVisible(false);
        SetBagUIVisible(false);
        isInGiftFlow = false;
        if (wantGiftButton != null) wantGiftButton.gameObject.SetActive(!hasReceivedGiftFromPlayer);
        if (hireButton != null) hireButton.gameObject.SetActive(CanBeRecruitedAsCook());

        animator.SetBool("isRunning", false);
        // 玩家交互中的聊天气泡常驻显示，不使用定时隐藏
        if (bubbleHideCoroutine != null)
            StopCoroutine(bubbleHideCoroutine);
        if (bubble != null && bubbleText != null)
        {
            bubbleText.text = GetChatText(data?.PlayerInteractionWords, "你好呀～");
            bubble.SetActive(true);
        }
        SetExpression(CustomerExpression.Shy);

        // 交互期间：持续面向玩家；只在超出交互距离时解除交互
        while (true)
        {
            if (playerTransform == null)
            {
                ForceStopInteraction();
                yield break;
            }

            FaceToward(playerTransform.position);

            float dist = Vector3.Distance(playerTransform.position, transform.position);
            if (dist > maxPlayerInteractionDistance)
            {
                ForceStopInteraction();
                yield break;
            }

            yield return null;
        }
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

        // 玩家交互中不自动隐藏聊天气泡，由交互退出逻辑统一关闭
        if (isInteractingWithPlayer)
        {
            bubbleHideCoroutine = null;
            yield break;
        }

        if (bubble != null)
            bubble.SetActive(false);

        // ⚠️ 这里不再重启 StartBubbleRoutine()，避免协程冲突
        bubbleHideCoroutine = null;
    }

    public void SetTarget(Vector3 pos, bool applySpawnLaneOffset = true)
    {
        targetPosition = ToPlanarTarget(pos, applySpawnLaneOffset);
    }

    /// <summary>根据当前状态实时解析移动目标（座位 / 排队位 / 离开点会每帧跟随 Transform）。</summary>
    public Vector3 GetLiveTargetPosition()
    {
        if (state == CustomerState.InsideRestaurant)
        {
            if (_diningPhase == CustomerDiningPhase.WalkingToPlate
                && servedPlate != null
                && !_hasHandledPlateArrival)
            {
                return ToPlanarTarget(servedPlate.GetDishFlyTargetWorldPosition(), applySpawnLaneOffset: false);
            }

            if (_diningPhase == CustomerDiningPhase.WalkingToSeat
                && currentSeat != null
                && !_hasHandledSeatArrival)
            {
                return ToPlanarTarget(currentSeat.GetSitWorldPosition(), applySpawnLaneOffset: false);
            }
        }

        if ((state == CustomerState.WalkingToQueue || state == CustomerState.Queueing)
            && manager != null
            && manager.TryGetQueueWorldPosition(this, out Vector3 queuePos))
            return ToPlanarTarget(queuePos, applySpawnLaneOffset: true);

        if (state == CustomerState.Leaving && _exitTargetTransform != null)
            return ToPlanarTarget(_exitTargetTransform.position, applySpawnLaneOffset: false);

        return targetPosition;
    }

    private Vector3 ToPlanarTarget(Vector3 pos, bool applySpawnLaneOffset)
    {
        if (applySpawnLaneOffset)
            pos.y += spawnLaneOffset;
        return new Vector3(pos.x, pos.y, transform.position.z);
    }

    private void RefreshMoveTarget()
    {
        targetPosition = GetLiveTargetPosition();
    }

    /// <summary>队首顾客进入餐厅：走向已预留的空座位。</summary>
    public void GoToSeat(RestaurantSeat seat)
    {
        if (seat == null)
        {
            LeaveRestaurantNoPlates();
            return;
        }

        currentSeat = seat;

        RestaurantPanel panel = RestaurantPanel.instance;
        servedPlate = panel != null ? panel.FindFirstPlateWithFood() : null;
        if (servedPlate == null || servedPlate.IsPlateEmpty())
        {
            ReleaseCurrentSeat();
            LeaveRestaurantNoPlates();
            return;
        }
        _diningPhase = CustomerDiningPhase.WalkingToPlate;
        _hasHandledPlateArrival = false;
        _hasHandledSeatArrival = false;
        state = CustomerState.InsideRestaurant;
        SetExpression(CustomerExpression.Serious);
        ShowBubble(GetRandomThought(data.InsideRestaurantQueueingWords.ToArray()));
    }

    [System.Obsolete("已改为座位就餐流程，请使用 GoToSeat")]
    public void EnterRestaurantAmbience(Vector3 insidePosition)
    {
        if (SeatManager.Instance != null && SeatManager.Instance.HasAvailableSeat)
        {
            RestaurantSeat seat = SeatManager.Instance.TryReserveSeat(this);
            if (seat != null)
            {
                GoToSeat(seat);
                return;
            }
        }
        LeaveRestaurantNoPlates();
    }

    [System.Obsolete("已改为座位就餐流程，请使用 GoToSeat")]
    public void GoToPlate(Plate plate)
    {
        GoToSeat(null);
    }

    public void LeaveRestaurant()
    {
        ReleaseCurrentSeat();
        ClearCarriedDishState();
        _diningPhase = CustomerDiningPhase.None;
        state = CustomerState.Leaving;
        SetExpression(CustomerExpression.Touched);
        Transform exit = CustomerManager.instance.GetRandomExitPoint(transform.position);
        if (exit != null)
        {
            _exitTargetTransform = exit;
            SetTarget(exit.position, applySpawnLaneOffset: false);
        }
        //Debug.Log("D5555");
        ShowBubble(GetRandomThought(data.LeavingRestaurantWords.ToArray()));
    }
    public void LeaveRestaurantNoPlates()
    {
        ReleaseCurrentSeat();
        ClearCarriedDishState();
        _diningPhase = CustomerDiningPhase.None;
        state = CustomerState.Leaving;
        SetExpression(CustomerExpression.Speechless);
        Transform exit = CustomerManager.instance != null
            ? CustomerManager.instance.GetAlternateExitPoint(transform.position)
            : null;
        if (exit != null)
        {
            _exitTargetTransform = exit;
            SetTarget(exit.position, applySpawnLaneOffset: false);
        }
        ShowBubble(GetRandomThought(data.noPlateFoodWords.ToArray()));
    }
    public void donotWantToEat()
    {
        ReleaseCurrentSeat();
        ClearCarriedDishState();
        _diningPhase = CustomerDiningPhase.None;
        state = CustomerState.Leaving;
        SetExpression(CustomerExpression.BadTaste);
        Transform exit = CustomerManager.instance.GetRandomExitPoint(transform.position);
        if (exit != null)
        {
            _exitTargetTransform = exit;
            SetTarget(exit.position, applySpawnLaneOffset: false);
        }

        //ShowBubble("吃饱回家咯~");
    }


    private void MoveToTarget()
    {
        RefreshMoveTarget();

        float planarDist = GetPlanarDistance(transform.position, targetPosition);
        bool reached = planarDist < reachPlanarThreshold;
        float moveSpeed = GetFinalMoveSpeed();
        float keepZ = transform.position.z;

        if (!reached)
        {
            Vector3 planarTarget = new Vector3(targetPosition.x, targetPosition.y, keepZ);
            transform.position = Vector3.MoveTowards(transform.position, planarTarget, moveSpeed * Time.deltaTime);

            Vector3 directionToTarget = planarTarget - transform.position;
            directionToTarget.z = 0f;
            FaceByDirection(directionToTarget);
        }
        else
        {
            if (state == CustomerState.Queueing)
            {
                FaceByX(1f);
                return;
            }

            if (state == CustomerState.Leaving)
                return;
        }

        if (reached && state != CustomerState.Queueing)
            OnReachTarget();
    }

    private float GetFinalMoveSpeed()
    {
        float multiplier = 1f;
        if (WeaponStatsManager.Instance != null)
        {
            multiplier = WeaponStatsManager.Instance.customerMoveSpeedMultiplier;
        }

        return data.moveSpeed * multiplier;
    }

    private void ReleaseCurrentSeat()
    {
        if (currentSeat == null)
            return;

        if (SeatManager.Instance != null)
            SeatManager.Instance.ReleaseSeat(currentSeat);
        else
            currentSeat.Release();

        currentSeat = null;
    }

    private void OnReachTarget()
    {
        if (state != CustomerState.InsideRestaurant)
            return;

        if (_diningPhase == CustomerDiningPhase.WalkingToPlate
            && servedPlate != null
            && !_hasHandledPlateArrival)
        {
            _hasHandledPlateArrival = true;
            StartCoroutine(PlatePickupThenGoToSeatRoutine());
            return;
        }

        if (_diningPhase == CustomerDiningPhase.WalkingToSeat
            && currentSeat != null
            && !_hasHandledSeatArrival)
        {
            _hasHandledSeatArrival = true;
            Vector3 sitPos = currentSeat.GetSitWorldPosition();
            transform.position = new Vector3(sitPos.x, sitPos.y, transform.position.z);
            StartCoroutine(SeatEatingRoutine());
        }
    }

    /// <summary>到达碟子后取菜，再走向座位。</summary>
    private IEnumerator PlatePickupThenGoToSeatRoutine()
    {
        if (servedPlate == null || servedPlate.IsPlateEmpty() || currentSeat == null)
        {
            SetExpression(CustomerExpression.Speechless);
            ShowBubble(GetChatText(data?.noPlateFoodWords, "没有菜了……"));
            ReleaseCurrentSeat();
            LeaveRestaurantNoPlates();
            yield break;
        }

        ShowBubble(GetChatText(data?.InsideRestaurantQueueingWords, "端菜上桌~"));
        yield return new WaitForSeconds(0.35f);

        if (servedPlate == null || servedPlate.IsPlateEmpty())
        {
            SetExpression(CustomerExpression.Speechless);
            ReleaseCurrentSeat();
            LeaveRestaurantNoPlates();
            yield break;
        }

        _carriedRecipe = servedPlate.currentDish != null ? servedPlate.currentDish.recipe : null;
        _carriedGoldAmount = 0;
        if (!servedPlate.TryConsumeOneServing(out _carriedGoldAmount) || _carriedRecipe == null)
        {
            SetExpression(CustomerExpression.Speechless);
            ReleaseCurrentSeat();
            LeaveRestaurantNoPlates();
            yield break;
        }

        SpawnCarriedDishVisual(_carriedRecipe);
        _diningPhase = CustomerDiningPhase.WalkingToSeat;
    }

    private IEnumerator SeatEatingRoutine()
    {
        if (currentSeat == null)
        {
            LeaveRestaurant();
            yield break;
        }

        if (_carriedRecipe == null)
        {
            SetExpression(CustomerExpression.Speechless);
            ShowBubble(GetChatText(data?.noPlateFoodWords, "没有菜了……"));
            ReleaseCurrentSeat();
            LeaveRestaurantNoPlates();
            yield break;
        }

        DishRecipe recipe = _carriedRecipe;
        _diningPhase = CustomerDiningPhase.Eating;
        isConsuming = true;
        ShowBubble(GetChatText(data?.ConsumeStartWords, "开动了！"));
        SetExpression(CustomerExpression.Serious);

        float baseEat = recipe != null ? recipe.sellTime : (servedPlate != null ? servedPlate.consumeTime : 2f);
        float eatMult = 1f;
        if (WeaponStatsManager.Instance != null)
            eatMult = Mathf.Max(0.01f, WeaponStatsManager.Instance.sellTimeMultiplier);
        float waitSeconds = Mathf.Max(0.01f, baseEat / eatMult);

        yield return new WaitForSeconds(waitSeconds);

        ClearCarriedDishVisual();

        int goldAmount = _carriedGoldAmount;
        bool isFavourite = recipe != null && data != null && data.favouriteFood != null
            && data.favouriteFood.Contains(recipe.dishID);

        if (isFavourite)
        {
            ShowCustomBubble(GetChatText(data?.FavouriteDishWords, "这是我的最爱！"), 1.5f);
            SetExpression(CustomerExpression.HeartEyes);
            AddAffection(5f);
            yield return StartCoroutine(PlaySatisfactionJump(2));
        }
        else
        {
            ShowCustomBubble(GetChatText(data?.NormalDishWords, "太好吃了！"), 1.2f);
            SetExpression(CustomerExpression.Touched);
            yield return StartCoroutine(PlaySatisfactionJump(1));
        }

        if (goldAmount > 0 && CustomerManager.instance != null)
            CustomerManager.instance.SpawnCoinPickupAt(currentSeat, goldAmount);

        isConsuming = false;
        ClearCarriedDishState();
        _diningPhase = CustomerDiningPhase.None;
        ReleaseCurrentSeat();
        LeaveRestaurant();
    }

    private void SpawnCarriedDishVisual(DishRecipe recipe)
    {
        ClearCarriedDishVisual();
        if (recipe == null || CustomerManager.instance == null)
            return;

        GameObject prefab = CustomerManager.instance.carriedDishPrefab != null
            ? CustomerManager.instance.carriedDishPrefab
            : CustomerManager.instance.dishFlyToCustomerPrefab;
        if (prefab == null)
            return;

        Transform parent = carriedDishHoldPoint != null ? carriedDishHoldPoint : transform;
        _carriedDishVisual = Instantiate(prefab, parent);
        _carriedDishVisual.transform.localPosition = carriedDishLocalOffset;
        _carriedDishVisual.transform.localRotation = Quaternion.identity;

        SpriteRenderer sr = _carriedDishVisual.GetComponentInChildren<SpriteRenderer>(true);
        if (sr != null && recipe.dishIcon != null)
            sr.sprite = recipe.dishIcon;

        Image img = _carriedDishVisual.GetComponentInChildren<Image>(true);
        if (img != null && recipe.dishIcon != null)
            img.sprite = recipe.dishIcon;
    }

    private void ClearCarriedDishVisual()
    {
        if (_carriedDishVisual != null)
            Destroy(_carriedDishVisual);
        _carriedDishVisual = null;
    }

    private void ClearCarriedDishState()
    {
        ClearCarriedDishVisual();
        servedPlate = null;
        _carriedRecipe = null;
        _carriedGoldAmount = 0;
    }

    private IEnumerator PlaySatisfactionJump(int times)
    {
        Vector3 originalPos = transform.position;
        float jumpHeight = 1.2f;
        float jumpDuration = 0.6f;

        for (int i = 0; i < times; i++)
        {
            float jumpElapsed = 0f;
            while (jumpElapsed < jumpDuration)
            {
                float progress = jumpElapsed / jumpDuration;
                float yOffset = Mathf.Sin(progress * Mathf.PI) * jumpHeight;
                transform.position = new Vector3(originalPos.x, originalPos.y + yOffset, originalPos.z);
                jumpElapsed += Time.deltaTime;
                yield return null;
            }
            transform.position = originalPos;

            if (i < times - 1)
                yield return new WaitForSeconds(0.35f);
        }
    }
    public void SetState(CustomerState newState)
    {
        if (state == newState) return;

        state = newState;
        ShowThoughtBubble();

        // 状态改变时回到该状态的默认表情（避免卡在上一事件表情）
        SetExpression(GetDefaultExpressionForCurrentContext());
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
        ClearCarriedDishVisual();
        ReleaseCurrentSeat();
        manager?.RemoveCustomer(this);
        ClearFavouriteDishIcons();
        CameraFollow.PopOrthoSizeRequest(interactionCameraRequestKey);
        CameraFollow.PopOrthoSizeRequest(homeGuestCameraRequestKey);
        CameraFollow.PopXFocusRequest(interactionCameraXFocusRequestKey);
        CameraFollow.PopXFocusRequest(homeGuestCameraXFocusRequestKey);

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
        // 普通交互期间不允许外部逻辑随便关掉面板，
        // 但做客状态下（isGuesting=true）需要强制隐藏，只保留对话气泡。
        if (!visible && isInteractingWithPlayer && !isGuesting)
        {
            return;
        }
        if (interactionPanel != null) interactionPanel.SetActive(visible);
    }

    private void SetGiftPanelVisible(bool visible)
    {
        GameObject panel = giftPanel != null ? giftPanel : GiftPanel;
        if (panel != null) panel.SetActive(visible);
    }

    private bool IsGiftPanelVisible()
    {
        GameObject panel = giftPanel != null ? giftPanel : GiftPanel;
        return panel != null && panel.activeSelf;
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
            ShowCustomBubble(GetChatText(data?.GiftLikedWords, "谢谢你！我很喜欢"), 1);
            SetExpression(CustomerExpression.Touched);
            //增加好感度的逻辑写在这里
            AddAffection(5f); // ✅ 增加好感度
        }
        else
        {
            ShowCustomBubble(GetChatText(data?.GiftNormalWords, "噢，是礼物！"), 1);
            SetExpression(CustomerExpression.Awkward);
            // 非最爱礼物也给少量好感度，避免“送礼不加好感”的落差
            AddAffection(1f);
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
        // 做客开始时，先清掉交互阶段镜头请求，避免两套请求叠加造成抖动/偏移
        CameraFollow.PopOrthoSizeRequest(interactionCameraRequestKey);
        CameraFollow.PopXFocusRequest(interactionCameraXFocusRequestKey);
        // 做客期间不显示任何交互UI
        SetInteractionPanelVisible(false);
        SetGiftPanelVisible(false);
        SetBagUIVisible(false);
        CameraFollow.PushOrthoSizeRequest(homeGuestCameraRequestKey, homeGuestOrthoSize);
        CameraFollow.PushXFocusRequest(homeGuestCameraXFocusRequestKey, transform.position.x);
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
        CameraFollow.PushXFocusRequest(homeGuestCameraXFocusRequestKey, transform.position.x);
        if (HomeManager.instance != null && HomeManager.instance.table != null)
            FaceToward(HomeManager.instance.table.transform.position);


        List<string> guestWords = data != null ? data.HomeGuestWords : null;
        if (guestWords != null && guestWords.Count > 0)
        {
            for (int i = 0; i < guestWords.Count; i++)
            {
                string word = guestWords[i];
                if (string.IsNullOrEmpty(word)) continue;
                ShowCustomBubble(word, 1f);
                yield return StartCoroutine(WaitWithGuestCameraFocus(1f));
            }
        }
        else
        {
            ShowCustomBubble("承蒙招待，前来拜访。", 1);
            yield return StartCoroutine(WaitWithGuestCameraFocus(1f));
            ShowCustomBubble("好茶，好茶", 1);
            yield return StartCoroutine(WaitWithGuestCameraFocus(1f));
            ShowCustomBubble("吃茶点！", 1);
            yield return StartCoroutine(WaitWithGuestCameraFocus(1f));
        }
        player.gameObject.transform.position = originPlayerPostion;
        player.canPlayerMove = true;
        transform.position = guestOriginPosition;
        isGuesting = false;
        player.GetComponent<Rigidbody>().isKinematic = false;
        CameraFollow.PopOrthoSizeRequest(homeGuestCameraRequestKey);
        CameraFollow.PopXFocusRequest(homeGuestCameraXFocusRequestKey);
        ForceStopInteraction();

        // 根据家中总魅力值，对做客结束时的好感提升进行加成
        float baseAffection = 10f;
        float multiplier = 1f;
        if (FurnitureUIManager.instance != null)
        {
            int charm = FurnitureUIManager.instance.TotalCharmValue;
            // 每 10 点魅力 +10% 加成，上限 3 倍
            multiplier += Mathf.Clamp(charm / 10f * 0.1f, 0f, 2f);
        }
        AddAffection(baseAffection * multiplier);
    }

    private IEnumerator WaitWithGuestCameraFocus(float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            // 做客期间持续同步 X，对应“顾客和玩家发生大幅位移”的场景
            CameraFollow.PushXFocusRequest(homeGuestCameraXFocusRequestKey, transform.position.x);
            elapsed += Time.deltaTime;
            yield return null;
        }
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
        restaurantAmbienceRoutine = null;
        isInteractingWithPlayer = false;
        isConsuming = false;
        _diningPhase = CustomerDiningPhase.None;
        ClearCarriedDishState();
        ReleaseCurrentSeat();
        state = CustomerState.Spawning;

        // 转职后不再需要顾客交互UI
        SetInteractionPanelVisible(false);
        SetGiftPanelVisible(false);
        SetBagUIVisible(false);
        if (wantGiftButton != null) wantGiftButton.gameObject.SetActive(false);
        if (hireButton != null) hireButton.gameObject.SetActive(false);
        CameraFollow.PopOrthoSizeRequest(interactionCameraRequestKey);
        CameraFollow.PopXFocusRequest(interactionCameraXFocusRequestKey);
        CameraFollow.PopOrthoSizeRequest(homeGuestCameraRequestKey);
        CameraFollow.PopXFocusRequest(homeGuestCameraXFocusRequestKey);
        if (CustomerManager.instance != null && CustomerManager.instance.currentInteractingNPC == this)
        {
            CustomerManager.instance.currentInteractingNPC = null;
        }

        // 状态转为厨师
        data.isCook = true;
        cookState = CookState.Idle;
        SetExpression(CustomerExpression.Serious);

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

        ShowCustomBubble(GetChatText(data?.RecruitCookWords, "我愿意帮忙做菜！"), 2f);

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
        float cookMoveSpeed = GetFinalMoveSpeed() * 0.6f;
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
        SetExpression(CustomerExpression.Serious);

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
        SetExpression(CustomerExpression.Serious);

        // 移动到锅前：只对齐 X 轴，Y、Z 保持 NPC 当前位置不变，且始终面朝锅的方向（不倒着走）
        float cookMoveSpeed = GetFinalMoveSpeed() * 0.7f;
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

        ShowCustomBubble(GetChatText(data?.CookBoostWords, "加速烹饪！"), 1.5f);
        SetExpression(CustomerExpression.Mischievous);

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
        SetExpression(CustomerExpression.Serious);
    }

    // 实际Buff逻辑：减少时间、增加产出与价格
    public void ApplyCookingBuffToPot(Pot pot)
    {
        if (pot.currentRecipe == null) return;

        pot.currentRecipe.cookTime *= data.timeReductionRate;
        pot.currentRecipe.baseDishPrice *= data.priceIncreaseRate;
        // 若有产出数量概念，可记录在菜对象上
    }

    public void SetExpression(CustomerExpression expression)
    {
        currentExpression = expression;
        EnsureExpressionMaterialInstance();

        int idx = (int)expression;
        if (expressionTextures == null || idx < 0 || idx >= expressionTextures.Count) return;
        Texture tex = expressionTextures[idx];
        if (tex == null) return;
        if (expressionMaterialInstance == null) return;

        if (!string.IsNullOrEmpty(expressionTextureProperty) && expressionMaterialInstance.HasProperty(expressionTextureProperty))
        {
            expressionMaterialInstance.SetTexture(expressionTextureProperty, tex);
        }
        else
        {
            // 兜底：尽量写到 _MainTex
            if (expressionMaterialInstance.HasProperty("_MainTex"))
                expressionMaterialInstance.SetTexture("_MainTex", tex);
        }
    }

    private void EnsureExpressionMaterialInstance()
    {
        if (expressionMaterialInstance != null) return;
        if (expressionSkinRenderer == null) return;

        Material[] mats = expressionSkinRenderer.materials; // 这里会返回实例数组（可安全修改并回写）
        if (mats == null || mats.Length == 0) return;

        int slot = Mathf.Clamp(expressionMaterialSlot, 0, mats.Length - 1);
        Material source = mats[slot];
        if (source == null) return;

        // 必须用克隆实例材质（避免改到共享材质影响所有 NPC）
        expressionMaterialInstance = Instantiate(source);
        mats[slot] = expressionMaterialInstance;
        expressionSkinRenderer.materials = mats;
    }

    private CustomerExpression GetDefaultExpressionForCurrentContext()
    {
        // 厨师优先：统一认真
        if (data != null && data.isCook) return CustomerExpression.Serious;
        if (isConsuming) return CustomerExpression.Serious;
        if (isInteractingWithPlayer) return CustomerExpression.Shy;

        switch (state)
        {
            case CustomerState.Queueing:
                return CustomerExpression.Awkward;
            case CustomerState.WalkingToQueue:
                return CustomerExpression.Serious;
            case CustomerState.Leaving:
                return CustomerExpression.Touched;
            case CustomerState.InsideRestaurant:
                return CustomerExpression.Serious;
            case CustomerState.Spawning:
            default:
                return CustomerExpression.Serious;
        }
    }

    private string GetChatText(List<string> words, string fallback)
    {
        if (words != null && words.Count > 0)
        {
            string pick = GetRandomThought(words.ToArray());
            if (!string.IsNullOrEmpty(pick)) return pick;
        }
        return fallback;
    }

    private void StartClickPulse()
    {
        if (!isActiveAndEnabled) return;
        if (clickPulseCoroutine != null)
        {
            StopCoroutine(clickPulseCoroutine);
        }
        clickPulseCoroutine = StartCoroutine(ClickPulseRoutine());
    }

    private IEnumerator ClickPulseRoutine()
    {
        Vector3 baseScale = defaultVisualScale;
        transform.localScale = baseScale;
        float duration = Mathf.Max(0.05f, clickPulseDuration);
        float amplitude = Mathf.Max(0f, clickPulseScale);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            float t = elapsed / duration;
            float wave = Mathf.Sin(t * Mathf.PI); // 0->1->0
            float factor = 1f + wave * amplitude;
            float absX = Mathf.Abs(baseScale.x) * factor;
            transform.localScale = new Vector3(absX, baseScale.y * factor, baseScale.z * factor);
            elapsed += Time.deltaTime;
            yield return null;
        }

        transform.localScale = baseScale;
        clickPulseCoroutine = null;
    }

}