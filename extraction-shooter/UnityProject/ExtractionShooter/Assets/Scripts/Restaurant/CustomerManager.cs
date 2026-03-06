using UnityEngine;
using System.Collections.Generic;

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
    }

    // 生成顾客
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

        // 1. 筛选可用的预制体（场景中还不存在的类型）
        List<GameObject> availablePrefabs = new List<GameObject>();
        foreach (var prefab in customerPrefabs)
        {
            if (prefab == null) continue;

            CustomerNPC prefabNPC = prefab.GetComponent<CustomerNPC>();
            if (prefabNPC == null || prefabNPC.data == null) continue;

            string npcType = prefabNPC.data.id;  // 使用ID作为NPC类型标识
            if (!activeNPCTypes.Contains(npcType))
            {
                availablePrefabs.Add(prefab);
            }
        }

        if (availablePrefabs.Count == 0)
        {
            Debug.Log("所有NPC类型都已经在场景中，跳过生成");
            return;
        }

        // 2. 随机选择一个可用预制体
        GameObject selectedPrefab = availablePrefabs[Random.Range(0, availablePrefabs.Count)];
        CustomerNPC prefabNPCComponent = selectedPrefab.GetComponent<CustomerNPC>();
        string selectedType = prefabNPCComponent.data.id;

        // 3. 记录这个NPC类型
        activeNPCTypes.Add(selectedType);

        // 4. 生成NPC
        Transform randomSpawn = spawnPoints[Random.Range(0, spawnPoints.Count)];
        GameObject go = Instantiate(selectedPrefab, randomSpawn.position, Quaternion.identity);
        CustomerNPC npc = go.GetComponent<CustomerNPC>();

        // 5. 初始化NPC（使用预制体上预设的data）
        // 注意：这里不再创建新的CustomerData，而是使用预制体上预设的
        npc.Init();  // 传入类型用于销毁时清理

        if (Random.Range(0,1)>npc.data.buyprobability)
        {
            npc.donotWantToEat();
        }
        else
        {
            HandleEntrance(npc);
        }

        activeCustomers.Add(npc);
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
        if (!string.IsNullOrEmpty(customer.data.id))
        {
            activeNPCTypes.Remove(customer.data.id);
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
            Debug.LogWarning("没有配置生成点！");
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
                Debug.LogWarning("没有配置生成点或离开点！");
                return transform; // 返回Manager自身位置作为备选
            }
            return spawnPoints[0];
        }
    }
}