using UnityEngine;
using System.Collections.Generic;
using System.Collections;
public class CustomerManager : MonoBehaviour
{
    public static CustomerManager instance;

    [Header("顾客生成位置")]
    public List<Transform> spawnPoints;

    [Header("顾客离开位置")]
    public List<Transform> exitPoints; // 新增：离开点

    [Header("餐厅入口队首位置")]
    public Transform queueFrontPoint;

    [Header("餐厅菜碟列表")]
    public List<Plate> plates;

    [Header("顾客预制体列表")]  // 改为列表存储多种NPC预制体
    public List<GameObject> customerPrefabs;
    // 用于记录当前场景中已存在的NPC类型
    private HashSet<string> activeNPCTypes = new HashSet<string>();

    [Header("餐厅人数限制")]
    public int maxCustomersInside = 3;
    public float queueSpacing = 1.5f;

    [Header("自动生成设置")]
    public bool enableAutoSpawn = true; // 是否启用自动生成
    public float minSpawnInterval = 5f; // 最小生成间隔
    public float maxSpawnInterval = 15f; // 最大生成间隔
    public int maxTotalCustomers = 20; // 最大总顾客数（防止卡顿）

    private List<CustomerNPC> activeCustomers = new List<CustomerNPC>();
    private List<CustomerNPC> customersQueue = new List<CustomerNPC>(); // 正在排队的顾客
    private List<CustomerNPC> walkingToQueue = new List<CustomerNPC>(); // 正在走向队尾的顾客

    private float nextSpawnTime; // 下次生成的时间
    public ProjectileLauncher projectileLauncher;
    public Transform moneyBoxTransform;
    // 🔹 当前正在与玩家交互的 NPC（全局唯一）
    public CustomerNPC currentInteractingNPC = null;
    void Awake()
    {
        instance = this;
    }

    void Start()
    {
        // 设置第一次自动生成时间
        if (enableAutoSpawn)
        {
            nextSpawnTime = Time.time + Random.Range(minSpawnInterval, maxSpawnInterval);
        }
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
                // 设置下一次生成时间
                nextSpawnTime = Time.time + Random.Range(minSpawnInterval, maxSpawnInterval);
            }
        }

        UpdateQueueTargets();    // 更新所有顾客的队列目标
        HandleQueueEntry();      // 处理顾客加入队列
        HandleQueueAdvancement(); // 处理队列推进


        // 🔹 检测是否有两位喜欢的顾客可以聊天
        if (Time.frameCount % 50 == 0) // 每隔几秒检查一次
        {
            //print("DA测试相互喜欢的顾客");
            CheckForLikedPairChat();
        }
    }

    // 生成顾客
    public void SpawnCustomer()
    {
        if (spawnPoints.Count == 0)
        {
            Debug.LogWarning("没有配置生成点！");
            return;
        }

        if (customerPrefabs.Count == 0)
        {
            Debug.LogWarning("没有配置顾客预制体！");
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

        // 1️⃣ 筛选可生成的预制体：跳过已经是厨师的类型
        List<GameObject> availablePrefabs = new List<GameObject>();
        foreach (var prefab in customerPrefabs)
        {
            if (prefab == null) continue;

            CustomerNPC prefabNPC = prefab.GetComponent<CustomerNPC>();
            if (prefabNPC == null || prefabNPC.data == null) continue;

            // 👇 跳过已经被转成厨师的顾客类型
            if (prefabNPC.data.isCook)
            {
                //Debug.Log($"跳过厨师类型顾客: {prefabNPC.data.customerName}");
                continue;
            }

            string npcType = prefabNPC.data.id.ToString();
            if (!activeNPCTypes.Contains(npcType))
            {
                availablePrefabs.Add(prefab);
            }
        }

        if (availablePrefabs.Count == 0)
        {
            //Debug.Log("没有可生成的顾客类型（可能都转成厨师了）。");
            return;
        }

        // 2️⃣ 随机选择一个预制体
        GameObject selectedPrefab = availablePrefabs[Random.Range(0, availablePrefabs.Count)];
        CustomerNPC prefabNPCComponent = selectedPrefab.GetComponent<CustomerNPC>();
        string selectedType = prefabNPCComponent.data.id.ToString();

        // 3️⃣ 记录这个NPC类型
        activeNPCTypes.Add(selectedType);

        // 4️⃣ 生成并初始化（挂到 SceneTitle 单例下便于场景层级管理）
        Transform randomSpawn = spawnPoints[Random.Range(0, spawnPoints.Count)];
        GameObject go = Instantiate(selectedPrefab, randomSpawn.position, Quaternion.identity);
        if (SceneTitle.instance != null)
            go.transform.SetParent(SceneTitle.instance.transform);
        CustomerNPC npcInstance = go.GetComponent<CustomerNPC>();

        npcInstance.Init();

        // 👇 按你原先逻辑判断要不要吃饭
        bool dislikeAround = HasDislikedPersonAround(npcInstance);
        bool likeAround = HasLikedPersonAround(npcInstance);

        if (dislikeAround)
        {
            npcInstance.donotWantToEat();
        }
        else if (likeAround)
        {
            npcInstance.ShowCustomBubble("呀，遇见喜欢的人了～");
            HandleEntrance(npcInstance);
        }
        else
        {
            if (Random.value > npcInstance.data.buyprobability)
                npcInstance.donotWantToEat();
            else
                HandleEntrance(npcInstance);
        }

        // ✅ 最后加入顾客列表
        activeCustomers.Add(npcInstance);
    }
    // 处理顾客到入口
    // 所有顾客都先排队进入餐厅
    private void HandleEntrance(CustomerNPC npc)
    {
        // 所有人一律先排队
        npc.state = CustomerState.WalkingToQueue;
        walkingToQueue.Add(npc);

        // 设置目标为当前队尾
        npc.SetTarget(GetQueueTailPosition());

        npc.ShowCustomBubble("来排队啦~");
    }

    // 获取随机可用菜碟
    private Plate GetRandomAvailablePlate()
    {
        List<Plate> availablePlates = new List<Plate>();
        foreach (var plate in plates)
        {
            if (plate != null && plate.currentDish != null && !plate.currentDish.IsEmpty())
            {
                availablePlates.Add(plate);
            }
        }

        if (availablePlates.Count > 0)
        {
            return availablePlates[Random.Range(0, availablePlates.Count)];
        }

        return null;
    }

    // 获取当前队尾位置
    private Vector3 GetQueueTailPosition()
    {
        return queueFrontPoint.position + queueFrontPoint.right * (customersQueue.Count * queueSpacing) + queueFrontPoint.right * 0.2f;
    }

    // 获取指定队列位置
    private Vector3 GetQueuePosition(int index)
    {
        return queueFrontPoint.position + queueFrontPoint.right * (index * queueSpacing) + queueFrontPoint.right * 0.2f;
    }

    // 更新所有走向队列的顾客的目标点
    private void UpdateQueueTargets()
    {
        // 计算当前队尾位置
        Vector3 currentTail = GetQueueTailPosition();

        // 更新所有正在走向队列的顾客的目标
        for (int i = walkingToQueue.Count - 1; i >= 0; i--)
        {
            CustomerNPC npc = walkingToQueue[i];
            if (npc == null || npc.state != CustomerState.WalkingToQueue)
            {
                walkingToQueue.RemoveAt(i);
                continue;
            }

            // 设置目标为当前队尾
            npc.SetTarget(currentTail);
        }
    }

    // 处理顾客加入队列
    private void HandleQueueEntry()
    {
        Vector3 currentTail = GetQueueTailPosition();

        for (int i = walkingToQueue.Count - 1; i >= 0; i--)
        {
            CustomerNPC npc = walkingToQueue[i];
            if (npc == null) continue;

            // 检查是否到达队尾
            float distanceToTail = Vector3.Distance(npc.transform.position, currentTail);

            if (distanceToTail < 0.3f)
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

    // 处理队列推进（队首进入餐厅）
    private void HandleQueueAdvancement()
    {
        if (GetInsideCustomerCount() >= maxCustomersInside) return;

        while (customersQueue.Count > 0 && GetInsideCustomerCount() < maxCustomersInside)
        {
            // 队首进入餐厅
            CustomerNPC firstInQueue = customersQueue[0];
            customersQueue.RemoveAt(0);

            // 更新队列剩余成员的目标位置
            UpdateQueueMemberPositions();

            // 为队首顾客找菜碟
            Plate availablePlate = GetRandomAvailablePlate();

            if (availablePlate != null)
            {
                firstInQueue.GoToPlate(availablePlate);
                break; // 一次只进一个人
            }
            else
            {
                //没有可用餐碟

                firstInQueue.LeaveRestaurantNoPlates();
            }
        }
    }

    private int GetInsideCustomerCount()
    {
        int count = 0;
        foreach (var c in activeCustomers)
        {
            if (c != null && c.state == CustomerState.InsideRestaurant)
            {
                count++;
            }
        }
        return count;
    }

    // 清理离开的顾客
    public void RemoveCustomer(CustomerNPC customer)
    {
        if (customer == null) return;

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

    // 获取随机离开点（用于顾客离开）
    public Transform GetRandomExitPoint(Vector3 NPCPos)
    {

        if (spawnPoints.Count > 1)
        {
            // 如果没有专门配置离开点，但有多于一个生成点，随机选一个不同于出生点的位置
            // 注意：这个方法不完全准确，但比总是返回相同位置好
            Transform exitPoint = spawnPoints[Random.Range(0, spawnPoints.Count)];

            // 可以再尝试几次避免和某些特定点重复
            while (Vector3.Distance(exitPoint.position, NPCPos) < 0.5f)
            {
                exitPoint = spawnPoints[Random.Range(0, spawnPoints.Count)];
            }
            //print("设置离开点:"+exitPoint.position+" 自身坐标"+NPCPos);

            return exitPoint;
        }
        else
        {
            // 只有一个点，那就用它
            if (spawnPoints.Count == 0)
            {
                //Debug.LogWarning("没有配置生成点或离开点！");
                return transform; // 返回Manager自身位置作为备选
            }
            return spawnPoints[0];
        }
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

        List<(CustomerNPC a, CustomerNPC b)> likedPairs = new List<(CustomerNPC, CustomerNPC)>();

        foreach (var npc in activeCustomers)
        {
            if (npc == null || npc.hasChattedWithOtherCustomer) continue; // 跳过已聊过的

            foreach (var other in activeCustomers)
            {
                if (other == null || npc == other || other.hasChattedWithOtherCustomer) continue;

                // 双方互相喜欢
                bool likeEachOther =
                    npc.data.likePeopleList.Contains(other.data.id) &&
                    other.data.likePeopleList.Contains(npc.data.id);

                if (!likeEachOther) continue;

                // 距离判定
                float dist = Vector3.Distance(npc.transform.position, other.transform.position);
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

        // 1) 配对瞬间就确定站位：A 原地，B 走到 A 的对面（或合适距离）
        Vector3 aPos = npcA.transform.position;
        Vector3 bPos = npcB.transform.position;
        Vector3 dirAtoBFlat = bPos - aPos;
        dirAtoBFlat.y = 0f;
        if (dirAtoBFlat.sqrMagnitude < 0.0001f)
        {
            // 极端情况：重叠，给一个默认方向
            dirAtoBFlat = npcA.transform.forward;
            dirAtoBFlat.y = 0f;
        }
        dirAtoBFlat.Normalize();

        float sep = Mathf.Max(0.5f, pairChatSeparation);
        Vector3 targetPosA = aPos; // A 不挪
        Vector3 targetPosB = aPos + dirAtoBFlat * sep;

        // 与 CustomerNPC.MoveToTarget() 的 Y 锁定逻辑保持一致，避免上下抖动
        targetPosA.y = 4.036f;
        targetPosB.y = 4.036f;

        // A 直接停住；B 允许在“交互中”走位到目标点
        npcA.isInteractingWithPlayer = true;
        npcA.isPairChatPositioning = false;
        npcA.SetTarget(targetPosA);

        npcB.BeginPairChatPositioning(targetPosB);

        float elapsed = 0f;
        while (elapsed < pairChatMaxPositioningTime)
        {
            if (npcA == null || npcB == null)
            {
                chatting = false;
                yield break;
            }

            float distB = Vector3.Distance(npcB.transform.position, targetPosB);
            if (distB <= pairChatPositionTolerance)
                break;

            elapsed += Time.deltaTime;
            yield return null;
        }

        if (npcB != null) npcB.EndPairChatPositioning();

        // 2) 到位后再做“面对面旋转”（这样不会出现先站住再挪）
        Vector3 dirAtoB = (npcB.transform.position - npcA.transform.position);
        dirAtoB.y = 0f;
        if (dirAtoB.sqrMagnitude < 0.0001f) dirAtoB = dirAtoBFlat;
        dirAtoB.Normalize();

        Quaternion rotA = Quaternion.LookRotation(dirAtoB);
        Quaternion rotB = Quaternion.LookRotation(-dirAtoB);

        float rotateTime = 0.35f;
        elapsed = 0f;
        while (elapsed < rotateTime)
        {
            if (npcA != null) npcA.transform.rotation = Quaternion.Slerp(npcA.transform.rotation, rotA, elapsed / rotateTime);
            if (npcB != null) npcB.transform.rotation = Quaternion.Slerp(npcB.transform.rotation, rotB, elapsed / rotateTime);
            elapsed += Time.deltaTime;
            yield return null;
        }

        // ➤ 聊天序列
        npcA.ShowCustomBubble("嗨～好久不见！");
        npcB.ShowCustomBubble("哈哈，真巧呀～");
        yield return new WaitForSeconds(2f);

        npcA.ShowCustomBubble("最近在忙什么呢？");
        yield return new WaitForSeconds(2f);

        npcB.ShowCustomBubble("还在那家餐厅工作～哈哈～");
        yield return new WaitForSeconds(2f);

        npcA.ShowCustomBubble("改天一起吃饭呀！");
        yield return new WaitForSeconds(2f);

        // 聊天结束
        npcA.isInteractingWithPlayer = npcB.isInteractingWithPlayer = false;
        chatting = false;
    }
}