using UnityEngine;
using System.Collections.Generic;
using System.Collections;
using UnityEngine.UI;
public class CustomerManager : MonoBehaviour
{
    public static CustomerManager instance;

    [Header("顾客生成位置")]
    public List<Transform> spawnPoints;

    [Header("顾客离开位置")]
    public List<Transform> exitPoints; // 就餐后离开

    [Header("碟子无菜时离开出口（不排队，从此离开）")]
    [Tooltip("未配置时回退为 exitPoints 的最后一个，或仅有一个出口时用该出口")]
    public List<Transform> noFoodExitPoints;

    [Header("餐厅入口队首位置")]
    public Transform queueFrontPoint;

    [Header("座位与金币")]
    [Tooltip("顾客就餐完成后在座位旁生成的可点击金币预制体（需挂 RestaurantCoinPickup）")]
    public GameObject restaurantCoinPrefab;
    [Tooltip("碟子飞向顾客时的临时精灵预制体（可选）")]
    public GameObject dishFlyToCustomerPrefab;
    public float dishFlyToCustomerDuration = 0.35f;
    [Tooltip("顾客从碟子取菜后端在手上走向座位的菜品预制体（未填则尝试 dishFlyToCustomerPrefab）")]
    public GameObject carriedDishPrefab;

    [Header("餐厅菜碟列表")]
    public List<Plate> plates;

    [Header("顾客预制体列表")]  // 改为列表存储多种NPC预制体
    public List<GameObject> customerPrefabs;
    [Tooltip("勾选时：可用种类数受 WeaponStatsManager.restaurantCustomerPrefabCount 限制（技能树解锁）。不勾选：使用列表中全部预制体。")]
    [SerializeField] private bool limitPrefabPoolByWeaponStats = false;
    // 用于记录当前场景中已存在的NPC类型
    private HashSet<string> activeNPCTypes = new HashSet<string>();

    [Header("餐厅人数限制")]
    public int maxCustomersInside = 3;
    public float queueSpacing = 1.5f;
    public Text restaurantCustomerCountText;

    [Header("自动生成设置")]
    public bool enableAutoSpawn = true; // 是否启用自动生成
    public float minSpawnInterval = 5f; // 最小生成间隔
    public float maxSpawnInterval = 15f; // 最大生成间隔
    public int maxTotalCustomers = 20; // 最大总顾客数（防止卡顿）
   [Header("是否顾客间对话")]
    public bool isCustomerChat=false;
    private List<CustomerNPC> activeCustomers = new List<CustomerNPC>();
    private List<CustomerNPC> customersQueue = new List<CustomerNPC>(); // 正在排队的顾客
    private List<CustomerNPC> walkingToQueue = new List<CustomerNPC>(); // 正在走向队尾的顾客

    private float nextSpawnTime; // 下次生成的时间
    public ProjectileLauncher projectileLauncher;
    public Transform moneyBoxTransform;
    // 🔹 当前正在与玩家交互的 NPC（全局唯一）
    public CustomerNPC currentInteractingNPC = null;
    private bool hasSubscribedCustomerStats = false;
    void Awake()
    {
        instance = this;
    }

    void Start()
    {
        StartCoroutine(WaitAndBindCustomerStats());
        UpdateCustomerCountDisplay();
        ValidateSpawnConfiguration(logOnStart: true);

        // 设置第一次自动生成时间
        if (enableAutoSpawn)
        {
            nextSpawnTime = Time.time + Random.Range(minSpawnInterval, maxSpawnInterval);
        }
    }

    private void ValidateSpawnConfiguration(bool logOnStart = false)
    {
        if (spawnPoints == null || spawnPoints.Count == 0)
        {
            if (logOnStart)
                Debug.LogWarning("[CustomerManager] 未配置 spawnPoints，无法生成顾客。");
            return;
        }

        if (customerPrefabs == null || customerPrefabs.Count == 0)
        {
            if (logOnStart)
                Debug.LogWarning("[CustomerManager] customerPrefabs 为空！请在 Inspector 中拖入顾客预制体（Assets/Prefabas/Customer/）。");
            return;
        }

        if (queueFrontPoint == null && logOnStart)
            Debug.LogWarning("[CustomerManager] 未配置 queueFrontPoint，排队逻辑将无法正常工作。");
    }

    private void OnEnable()
    {
        TrySubscribeCustomerStats();
        StartCoroutine(WaitAndBindCustomerStats());
    }

    private void OnDisable()
    {
        if (WeaponStatsManager.Instance != null && hasSubscribedCustomerStats)
        {
            WeaponStatsManager.Instance.OnCustomerStatsChanged -= ApplyCustomerStatsFromManager;
        }
        hasSubscribedCustomerStats = false;
    }

    void Update()
    {
        // 按键生成顾客（调试用）
        if (Input.GetKeyDown(KeyCode.G))
        {
            SpawnCustomer();
        }

        // 自动生成顾客
        if (enableAutoSpawn && Time.time >= nextSpawnTime)
        {
            if (activeCustomers.Count < maxTotalCustomers)
            {
                SpawnCustomer();
                nextSpawnTime = Time.time + Random.Range(minSpawnInterval, maxSpawnInterval);
            }
            else
            {
                nextSpawnTime = Time.time + Random.Range(minSpawnInterval, maxSpawnInterval);
            }
        }

        UpdateQueueTargets();    // 更新所有顾客的队列目标
        HandleQueueEntry();      // 处理顾客加入队列
        HandleQueueAdvancement(); // 处理队列推进


        // 🔹 检测是否有两位喜欢的顾客可以聊天
        if (Time.frameCount % 50 == 0&&isCustomerChat) // 每隔几秒检查一次
        {
            //print("DA测试相互喜欢的顾客");
            CheckForLikedPairChat();
        }

        UpdateCustomerCountDisplay();
    }

    // 生成顾客
    public void SpawnCustomer()
    {
        if (spawnPoints == null || spawnPoints.Count == 0)
        {
            Debug.LogWarning("[CustomerManager] 没有配置生成点（spawnPoints）！");
            return;
        }

        if (customerPrefabs == null || customerPrefabs.Count == 0)
        {
            Debug.LogWarning("[CustomerManager] 没有配置顾客预制体（customerPrefabs）！");
            return;
        }

        // ✅ 先清理：把已经转化成厨师的顾客从活跃顾客列表移除
        for (int i = activeCustomers.Count - 1; i >= 0; i--)
        {
            CustomerNPC npc = activeCustomers[i];
            if (npc == null || npc.data == null)        // 已销毁或无数据
            {
                activeCustomers.RemoveAt(i);
            }
            else if (npc.data.isCook)                   // 已成为厨师
            {
                activeCustomers.RemoveAt(i);
                Debug.Log($"顾客 {npc.data.customerName} 已成为厨师，从顾客列表中移除。");
            }
        }

        // 1️⃣ 筛选可生成的预制体：可选受 WeaponStats 解锁数量限制，并跳过已是厨师的类型
        int allowedPrefabCount = customerPrefabs.Count;
        if (limitPrefabPoolByWeaponStats && WeaponStatsManager.Instance != null)
        {
            allowedPrefabCount = Mathf.Clamp(
                WeaponStatsManager.Instance.restaurantCustomerPrefabCount,
                0,
                customerPrefabs.Count);
        }

        if (allowedPrefabCount <= 0)
        {
            Debug.LogWarning("[CustomerManager] 可用顾客预制体数量为 0（检查 limitPrefabPoolByWeaponStats / restaurantCustomerPrefabCount）。");
            return;
        }

        List<GameObject> availablePrefabs = new List<GameObject>();
        for (int i = 0; i < allowedPrefabCount; i++)
        {
            GameObject prefab = customerPrefabs[i];
            if (prefab == null) continue;

            CustomerNPC prefabNPC = prefab.GetComponent<CustomerNPC>();
            if (prefabNPC == null || prefabNPC.data == null) continue;

            if (prefabNPC.data.isCook)
                continue;

            string npcType = prefabNPC.data.id.ToString();
            if (!activeNPCTypes.Contains(npcType))
                availablePrefabs.Add(prefab);
        }

        if (availablePrefabs.Count == 0)
        {
            Debug.LogWarning(
                "[CustomerManager] 没有可生成的顾客：同类型已在场（每种 id 同时仅 1 个），或前 "
                + allowedPrefabCount + " 个预制体均不可用。若只刷得出第一个，请检查 WeaponStatsManager.restaurantCustomerPrefabCount 是否被设为 1。");
            return;
        }

        // 2️⃣ 随机选择一个预制体
        GameObject selectedPrefab = availablePrefabs[Random.Range(0, availablePrefabs.Count)];
        CustomerNPC prefabNPCComponent = selectedPrefab.GetComponent<CustomerNPC>();
        string selectedType = prefabNPCComponent.data.id.ToString();

        // 3️⃣ 记录这个NPC类型
        activeNPCTypes.Add(selectedType);

        // 4️⃣ 生成并初始化（挂到 CustomerManager 自身下便于层级管理）
        Transform randomSpawn = spawnPoints[Random.Range(0, spawnPoints.Count)];
        GameObject go = Instantiate(selectedPrefab, randomSpawn.position, Quaternion.identity, transform);
        CustomerNPC npcInstance = go.GetComponent<CustomerNPC>();

        npcInstance.Init();

        // 👇 按你原先逻辑判断要不要吃饭
        bool dislikeAround = HasDislikedPersonAround(npcInstance);
        bool likeAround = HasLikedPersonAround(npcInstance);

        if (dislikeAround)
        {
            npcInstance.SetExpression(CustomerExpression.Speechless); // 无语
            npcInstance.donotWantToEat();
        }
        else if (likeAround)
        {
            npcInstance.ShowCustomBubble(GetCustomerWord(npcInstance, npcInstance.data?.LikePersonEncounterWords, "呀，遇见喜欢的人了～"));
            npcInstance.SetExpression(CustomerExpression.HeartEyes); // 爱心眼
            TryHandleEntrance(npcInstance);
        }
        else
        {
            if (Random.value > npcInstance.data.buyprobability)
            {
                npcInstance.SetExpression(CustomerExpression.BadTaste); // 难吃 / 不想吃
                npcInstance.donotWantToEat();
            }
            else
            {
                npcInstance.SetExpression(CustomerExpression.Serious); // 认真 / 准备排队
                TryHandleEntrance(npcInstance);
            }
        }

        // ✅ 最后加入顾客列表
        activeCustomers.Add(npcInstance);
        UpdateCustomerCountDisplay();
    }
    /// <summary>碟子上是否有菜（无菜时顾客不排队、不入场）。</summary>
    public bool HasPlateWithFood()
    {
        return RestaurantPanel.instance != null && RestaurantPanel.instance.HasPlateWithFood();
    }

    // 处理顾客到入口：有菜才排队，无菜直接离开
    private void TryHandleEntrance(CustomerNPC npc)
    {
        if (!HasPlateWithFood())
        {
            npc.SetExpression(CustomerExpression.Speechless);
            npc.ShowCustomBubble(GetCustomerWord(npc, npc.data?.noPlateFoodWords, "没有菜了……"));
            npc.LeaveRestaurantNoPlates();
            return;
        }

        HandleEntrance(npc);
    }

    // 所有顾客都先排队进入餐厅
    private void HandleEntrance(CustomerNPC npc)
    {
        if (queueFrontPoint == null)
        {
            Debug.LogError("[CustomerManager] queueFrontPoint 未配置，顾客无法排队。");
            npc.LeaveRestaurant();
            return;
        }

        // 所有人一律先排队
        npc.state = CustomerState.WalkingToQueue;
        walkingToQueue.Add(npc);

        // 设置目标为当前队尾
        npc.SetTarget(GetQueueTailPosition());

        npc.ShowCustomBubble(GetCustomerWord(npc, npc.data?.QueueJoinWords, "来排队啦~"));
    }

    private Vector3 GetRestaurantAmbiencePosition()
    {
        if (SeatManager.Instance != null)
        {
            IReadOnlyList<RestaurantSeat> seats = SeatManager.Instance.GetAllSeats();
            if (seats != null && seats.Count > 0 && seats[0] != null)
                return seats[0].GetSitWorldPosition();
        }
        if (queueFrontPoint != null)
            return queueFrontPoint.position + queueFrontPoint.forward * 2f;
        return transform != null ? transform.position : Vector3.zero;
    }

    // 获取当前队尾位置
    private Vector3 GetQueueTailPosition()
    {
        int index = customersQueue.Count + walkingToQueue.Count;
        return GetQueuePosition(index);
    }

    // 获取指定队列位置（实时读取 queueFrontPoint 世界坐标）
    public Vector3 GetQueuePosition(int index)
    {
        if (queueFrontPoint == null)
            return transform != null ? transform.position : Vector3.zero;
        return queueFrontPoint.position + queueFrontPoint.right * (index * queueSpacing) + queueFrontPoint.right * 0.2f;
    }

    /// <summary>按顾客当前排队下标返回实时排队世界坐标。</summary>
    public bool TryGetQueueWorldPosition(CustomerNPC npc, out Vector3 worldPos)
    {
        worldPos = default;
        if (npc == null || queueFrontPoint == null)
            return false;

        int index = customersQueue.IndexOf(npc);
        if (index >= 0)
        {
            worldPos = GetQueuePosition(index);
            return true;
        }

        index = walkingToQueue.IndexOf(npc);
        if (index >= 0)
        {
            worldPos = GetQueuePosition(customersQueue.Count + index);
            return true;
        }

        return false;
    }

    // 更新所有走向队列的顾客的目标点（兼容旧逻辑；实际移动时 CustomerNPC 会每帧 RefreshMoveTarget）
    private void UpdateQueueTargets()
    {
        // 更新所有正在走向队列的顾客的目标（为每个walking分配不同的队列位置，保持间隔）
        // 用正向索引保证“第0个walking”总是去最近的空位，避免反向遍历导致索引跳动。
        for (int i = 0; i < walkingToQueue.Count; i++)
        {
            CustomerNPC npc = walkingToQueue[i];
            if (npc == null || npc.state != CustomerState.WalkingToQueue)
            {
                walkingToQueue.RemoveAt(i);
                i--;
                continue;
            }

            int queueIndex = customersQueue.Count + i;
            npc.SetTarget(GetQueuePosition(queueIndex));
        }
    }

    // 处理顾客加入队列
    private void HandleQueueEntry()
    {
        for (int i = walkingToQueue.Count - 1; i >= 0; i--)
        {
            CustomerNPC npc = walkingToQueue[i];
            if (npc == null) continue;

            // 检查是否到达“分配给自己的队列位置”（实时坐标）
            Vector3 assigned = npc.GetLiveTargetPosition();
            float distanceToAssigned = Vector2.Distance(
                new Vector2(npc.transform.position.x, npc.transform.position.y),
                new Vector2(assigned.x, assigned.y)
            );

            if (distanceToAssigned < 0.3f)
            {
                // 加入队列
                walkingToQueue.RemoveAt(i);
                customersQueue.Add(npc);
                npc.state = CustomerState.Queueing;

                // 立即更新所有队列成员的目标位置
                UpdateQueueMemberPositions();
            }
        }
    }

    // 更新队列中所有成员的目标位置
    private void UpdateQueueMemberPositions()
    {
        for (int i = 0; i < customersQueue.Count; i++)
        {
            CustomerNPC npc = customersQueue[i];
            if (npc != null && npc.state == CustomerState.Queueing)
            {
                npc.SetTarget(GetQueuePosition(i));
            }
        }
    }

    // 处理队列推进（队首进入餐厅并占座）
    private void HandleQueueAdvancement()
    {
        if (SeatManager.Instance == null)
        {
            if (customersQueue.Count > 0 && Time.frameCount % 120 == 0)
                Debug.LogWarning("[CustomerManager] SeatManager 未找到，队首顾客无法入座。请在场景中挂 SeatManager。");
            return;
        }

        if (SeatManager.Instance.TotalSeatCount == 0)
        {
            if (customersQueue.Count > 0 && Time.frameCount % 120 == 0)
                Debug.LogWarning("[CustomerManager] 没有注册任何座位，队首顾客只能在排队点等待。请给座位物体挂 RestaurantSeat。");
            return;
        }

        if (!SeatManager.Instance.HasAvailableSeat)
            return;

        if (!HasPlateWithFood())
            return;

        while (customersQueue.Count > 0 && SeatManager.Instance.HasAvailableSeat && HasPlateWithFood())
        {
            CustomerNPC firstInQueue = customersQueue[0];
            customersQueue.RemoveAt(0);
            UpdateQueueMemberPositions();

            RestaurantSeat seat = SeatManager.Instance.TryReserveSeat(firstInQueue);
            if (seat == null)
                break;

            firstInQueue.GoToSeat(seat);
            break;
        }
    }

    private int GetInsideCustomerCount()
    {
        if (SeatManager.Instance != null)
            return SeatManager.Instance.OccupiedSeatCount;

        int count = 0;
        foreach (var c in activeCustomers)
        {
            if (c != null && c.state == CustomerState.InsideRestaurant)
                count++;
        }
        return count;
    }

    /// <summary>在座位旁生成可点击金币。</summary>
    public void SpawnCoinPickupAt(RestaurantSeat seat, int goldAmount)
    {
        if (seat == null || goldAmount <= 0)
            return;

        if (restaurantCoinPrefab == null)
        {
            Debug.LogWarning("[CustomerManager] 未配置 restaurantCoinPrefab，无法生成金币。");
            return;
        }

        Vector3 pos = seat.GetCoinSpawnWorldPosition();
        GameObject coinGo = Instantiate(restaurantCoinPrefab, pos, Quaternion.identity, transform);
        RestaurantCoinPickup pickup = coinGo.GetComponent<RestaurantCoinPickup>();
        if (pickup == null)
            pickup = coinGo.AddComponent<RestaurantCoinPickup>();
        pickup.Initialize(goldAmount);
    }

    /// <summary>菜肴从碟子飞向顾客就坐位置。</summary>
    public IEnumerator PlayDishFlyToCustomerCoroutine(Sprite dishIcon, Vector3 startWorldPos, Vector3 endWorldPos)
    {
        if (dishIcon == null)
            yield break;

        GameObject flyObj;
        if (dishFlyToCustomerPrefab != null)
        {
            flyObj = Instantiate(dishFlyToCustomerPrefab, startWorldPos, Quaternion.identity);
        }
        else
        {
            flyObj = new GameObject("DishFlyToCustomer");
            flyObj.transform.position = startWorldPos;
            SpriteRenderer sr = flyObj.AddComponent<SpriteRenderer>();
            sr.sprite = dishIcon;
        }

        SpriteRenderer flySr = flyObj.GetComponentInChildren<SpriteRenderer>(true);
        if (flySr != null)
            flySr.sprite = dishIcon;
        UnityEngine.UI.Image flyImg = flyObj.GetComponentInChildren<UnityEngine.UI.Image>(true);
        if (flyImg != null)
            flyImg.sprite = dishIcon;

        float duration = Mathf.Max(0.01f, dishFlyToCustomerDuration);
        float elapsed = 0f;
        Vector3 start = startWorldPos;
        Vector3 end = endWorldPos + Vector3.up * 0.15f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = t * t * (3f - 2f * t);
            flyObj.transform.position = Vector3.LerpUnclamped(start, end, eased);
            yield return null;
        }

        flyObj.transform.position = end;
        Destroy(flyObj);
    }

    // 清理离开的顾客
    public void RemoveCustomer(CustomerNPC customer)
    {
        if (customer == null) return;

        if (SeatManager.Instance != null)
        {
            IReadOnlyList<RestaurantSeat> seats = SeatManager.Instance.GetAllSeats();
            for (int i = 0; i < seats.Count; i++)
            {
                RestaurantSeat seat = seats[i];
                if (seat != null && seat.Occupant == customer)
                    SeatManager.Instance.ReleaseSeat(seat);
            }
        }

        // 从类型记录中移除
        if (!string.IsNullOrEmpty(customer.data.id.ToString()))
        {
            activeNPCTypes.Remove(customer.data.id.ToString());
        }
        activeCustomers.Remove(customer);
        walkingToQueue.Remove(customer);
        customersQueue.Remove(customer);

        // 如果有顾客离开队列，重新更新队列位置
        if (customer.state == CustomerState.Queueing)
        {
            UpdateQueueMemberPositions();
        }

        UpdateCustomerCountDisplay();
    }

    private void ApplyCustomerStatsFromManager()
    {
        if (WeaponStatsManager.Instance == null) return;

        maxCustomersInside = Mathf.Max(1, WeaponStatsManager.Instance.restaurantMaxCustomersInside);
        maxTotalCustomers = Mathf.Max(1, WeaponStatsManager.Instance.restaurantMaxTotalCustomers);
        UpdateCustomerCountDisplay();
    }

    private void TrySubscribeCustomerStats()
    {
        if (WeaponStatsManager.Instance == null || hasSubscribedCustomerStats) return;

        WeaponStatsManager.Instance.OnCustomerStatsChanged -= ApplyCustomerStatsFromManager;
        WeaponStatsManager.Instance.OnCustomerStatsChanged += ApplyCustomerStatsFromManager;
        hasSubscribedCustomerStats = true;
    }

    private IEnumerator WaitAndBindCustomerStats()
    {
        float timeout = 5f;
        while (WeaponStatsManager.Instance == null && timeout > 0f)
        {
            timeout -= Time.deltaTime;
            yield return null;
        }

        TrySubscribeCustomerStats();
        ApplyCustomerStatsFromManager();
    }

    private void UpdateCustomerCountDisplay()
    {
        if (restaurantCustomerCountText == null) return;

        int insideCount = GetInsideCustomerCount();
        int seatTotal = SeatManager.Instance != null ? SeatManager.Instance.TotalSeatCount : maxCustomersInside;
        if (seatTotal <= 0) seatTotal = maxCustomersInside;
        restaurantCustomerCountText.text = $"餐厅人数: {insideCount}/{seatTotal}";
    }

    // 获取随机生成点（用于生成顾客）
    public Transform GetRandomSpawnPoint()
    {
        if (spawnPoints.Count == 0)
        {
            //Debug.LogWarning("没有配置生成点！");
            return null;
        }
        return spawnPoints[Random.Range(0, spawnPoints.Count)];
    }

    // 获取随机离开点（用于顾客正常就餐后离开）
    public Transform GetRandomExitPoint(Vector3 NPCPos)
    {
        return PickExitFromList(exitPoints, NPCPos, transform);
    }

    /// <summary>碟子无菜等未入场场景：从备用出口离开。</summary>
    public Transform GetAlternateExitPoint(Vector3 npcPos)
    {
        if (noFoodExitPoints != null && noFoodExitPoints.Count > 0)
            return PickExitFromList(noFoodExitPoints, npcPos, transform);

        if (exitPoints != null && exitPoints.Count > 1)
            return exitPoints[exitPoints.Count - 1];

        return GetRandomExitPoint(npcPos);
    }

    private static Transform PickExitFromList(List<Transform> points, Vector3 npcPos, Transform fallback)
    {
        if (points == null || points.Count == 0)
            return fallback;

        Transform exitPoint = points[Random.Range(0, points.Count)];
        if (points.Count == 1)
            return exitPoint != null ? exitPoint : fallback;

        int safety = 8;
        while (safety-- > 0
               && exitPoint != null
               && Vector2.Distance(
                   new Vector2(exitPoint.position.x, exitPoint.position.y),
                   new Vector2(npcPos.x, npcPos.y)
               ) < 0.5f)
        {
            exitPoint = points[Random.Range(0, points.Count)];
        }

        return exitPoint != null ? exitPoint : fallback;
    }

    private bool HasDislikedPersonAround(CustomerNPC npc)
    {
        foreach (var other in activeCustomers)
        {
            if (other == null || other == npc) continue;
            if (npc.data.dislikePeopleList.Contains(other.data.id.GetHashCode())) // 用id区分
            {
                if (other.state == CustomerState.Queueing || other.state == CustomerState.InsideRestaurant)
                {
                    return true;
                }
            }
        }
        return false;
    }

    private bool HasLikedPersonAround(CustomerNPC npc)
    {
        foreach (var other in activeCustomers)
        {
            if (other == null || other == npc) continue;
            if (npc.data.likePeopleList.Contains(other.data.id.GetHashCode()))
            {
                if (other.state == CustomerState.Queueing || other.state == CustomerState.InsideRestaurant)
                {
                    return true;
                }
            }
        }
        return false;
    }

    private bool chatting = false;
    public bool IsPairChatting => chatting;
    [SerializeField] private float likeChatDistance = 3f; // 触发聊天的距离
                                                          // 聊天发生的概率（0~1）
    [SerializeField, Range(0f, 1f)]
    private float chatProbability = 0.5f;

    [Header("配对聊天站位")]
    [SerializeField] private float pairChatSeparation = 3.0f; // 两人之间的最终距离
    [SerializeField] private float pairChatPositionTolerance = 0.15f;
    [SerializeField] private float pairChatMaxPositioningTime = 2.0f; // 防止卡住
    private void CheckForLikedPairChat()
    {
        if (chatting) return;
        // 玩家与任意NPC交互时，禁止触发顾客配对聊天
        if (currentInteractingNPC != null) return;

        List<(CustomerNPC a, CustomerNPC b)> likedPairs = new List<(CustomerNPC, CustomerNPC)>();

        foreach (var npc in activeCustomers)
        {
            if (npc == null || npc.hasChattedWithOtherCustomer) continue; // 跳过已聊过的
            if (!npc.CanDoCustomerPairChat()) continue;

            foreach (var other in activeCustomers)
            {
                if (other == null || npc == other || other.hasChattedWithOtherCustomer) continue;
                if (!other.CanDoCustomerPairChat()) continue;

                // 双方互相喜欢
                bool likeEachOther =
                    npc.data.likePeopleList.Contains(other.data.id) &&
                    other.data.likePeopleList.Contains(npc.data.id);

                if (!likeEachOther) continue;

                // 餐厅内不允许聊天（需求：只能在进餐厅前/离开回家时聊）
                if (npc.state == CustomerState.InsideRestaurant || other.state == CustomerState.InsideRestaurant)
                    continue;

                // 距离判定
                float dist = Vector2.Distance(
                    new Vector2(npc.transform.position.x, npc.transform.position.y),
                    new Vector2(other.transform.position.x, other.transform.position.y)
                );
                if (dist <= likeChatDistance)
                {
                    likedPairs.Add((npc, other));
                }
            }
        }

        if (likedPairs.Count > 0)
        {
            // 随机选一对
            var pair = likedPairs[Random.Range(0, likedPairs.Count)];
            if (Random.value <= chatProbability)
            {
                chatting = true;

                pair.a.hasChattedWithOtherCustomer = true;
                pair.b.hasChattedWithOtherCustomer = true;

                StartCoroutine(LikedPairChatCoroutine(pair.a, pair.b));
            }
        }
    }

    private IEnumerator LikedPairChatCoroutine(CustomerNPC npcA, CustomerNPC npcB)
    {
        print("DA-开始交谈");
        if (npcA == null || npcB == null)
        {
            chatting = false;
            yield break;
        }

        // 聊天期间“暂停移动但不改写原目标”，避免：
        // - Leaving 状态被改写 target 后立刻触发 Destroy（表现为聊天完突然消失）
        // - Queueing / WalkingToQueue 目标被覆盖导致卡住或队列错位
        Vector3 originalTargetA = npcA.CurrentTargetPosition;
        Vector3 originalTargetB = npcB.CurrentTargetPosition;
        CustomerState originalStateA = npcA.state;
        CustomerState originalStateB = npcB.state;

        npcA.isInteractingWithPlayer = true;
        npcA.isPairChatPositioning = false;
        npcB.isInteractingWithPlayer = true;
        npcB.isPairChatPositioning = false;

        // 2D：只允许左右朝向
        if (npcA != null && npcB != null)
        {
            npcA.FaceToward(npcB.transform.position);
            npcB.FaceToward(npcA.transform.position);
        }

        // ➤ 聊天序列
        npcA.ShowCustomBubble(GetCustomerWord(npcA, npcA.data?.PairChatGreetingWords, "嗨～好久不见！"));
        npcB.ShowCustomBubble(GetCustomerWord(npcB, npcB.data?.PairChatReplyWords, "哈哈，真巧呀～"));
        yield return new WaitForSeconds(2f);

        npcA.ShowCustomBubble(GetCustomerWord(npcA, npcA.data?.PairChatQuestionWords, "最近在忙什么呢？"));
        yield return new WaitForSeconds(2f);

        npcB.ShowCustomBubble(GetCustomerWord(npcB, npcB.data?.PairChatStatusWords, "还在那家餐厅工作～哈哈～"));
        yield return new WaitForSeconds(2f);

        npcA.ShowCustomBubble(GetCustomerWord(npcA, npcA.data?.PairChatInviteWords, "改天一起吃饭呀！"));
        yield return new WaitForSeconds(2f);

        // 聊天结束
        if (npcA != null)
        {
            npcA.isInteractingWithPlayer = false;
            npcA.isPairChatPositioning = false;
            // 恢复原目标（重要：Leaving 的 exit 目标不能丢）
            npcA.SetTarget(originalTargetA);
            npcA.state = originalStateA;
        }
        if (npcB != null)
        {
            npcB.isInteractingWithPlayer = false;
            npcB.isPairChatPositioning = false;
            npcB.SetTarget(originalTargetB);
            npcB.state = originalStateB;
        }

        // 若其中有人在队列中，强制刷新一次队列站位，避免聊天暂停后队列错位/卡住
        UpdateQueueMemberPositions();
        chatting = false;
    }

    private string GetCustomerWord(CustomerNPC npc, List<string> words, string fallback)
    {
        if (npc != null && words != null && words.Count > 0)
        {
            int idx = Random.Range(0, words.Count);
            string pick = words[idx];
            if (!string.IsNullOrEmpty(pick)) return pick;
        }
        return fallback;
    }
}