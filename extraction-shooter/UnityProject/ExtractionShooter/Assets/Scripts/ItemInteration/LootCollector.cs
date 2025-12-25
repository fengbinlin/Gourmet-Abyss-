using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class LootCollector : MonoBehaviour
{
    [Header("收集设置")]
    [SerializeField] private float initialDelay = 0.3f;  // 初始延迟时间
    [SerializeField] private float rotationSpeed = 180f; // 旋转速度
    [SerializeField] private float floatAmplitude = 0.5f; // 浮动幅度
    [SerializeField] private float floatFrequency = 2f;  // 浮动频率

    [Header("飞行设置")]
    [SerializeField] private float flySpeed = 15f;       // 飞行速度
    [SerializeField] private float flyDelay = 1.5f;     // 开始飞行的延迟时间
    [SerializeField] private AnimationCurve flyAccelerationCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    [SerializeField] private float maxFlySpeed = 25f;    // 最大飞行速度
    [SerializeField] private float accelerationDuration = 0.8f; // 加速时间
    [SerializeField] private float attractionForce = 5f; // 引力大小
    [SerializeField] private float attractionRadius = 3f; // 引力半径

    [Header("销毁设置")]
    [SerializeField] private float destroyTimeout = 10f;  // 超过此时间未被采集则销毁自身

    [Header("特效设置")]
    [SerializeField] private GameObject collectEffectPrefab; // 收集特效
    [SerializeField] private AudioClip collectSound;         // 收集音效

    [Header("资源设置")]
    [SerializeField] private ResourceType resourceType = ResourceType.Money; // 资源类型
    [SerializeField] private int resourceAmount = 1;                         // 资源数量
    [SerializeField] private bool useCustomResource = false;                 // 是否使用自定义资源设置

    [Header("植物设置")]
    [Tooltip("是否为植物类资源")]
    [SerializeField] private bool isPlantResource = false; // 是否为植物类资源
    [Tooltip("植物资源是否直接加入数值管理器而不经过背包")]
    [SerializeField] private bool plantDirectToGameVal = true; // 植物资源是否直接加入数值管理器

    private Transform player;
    private Rigidbody rb;
    private Collider col;
    private Vector3 startPosition;
    private float spawnTime;
    private float timeSinceLastInteraction = 0f;
    private bool isReadyToCollect = false;
    private bool isFlyingToPlayer = false;
    private float currentFlySpeed = 0f;
    private Vector3 floatingOffset = Vector3.zero;
    private bool canBeCollected = false; // 标记是否可以收集
    private bool playerInTrigger = false; // 标记玩家是否在触发器中
    private Coroutine waitForCollectionCoroutine; // 等待收集的协程

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        col = GetComponent<Collider>();
        startPosition = transform.position;
        spawnTime = Time.time;
        timeSinceLastInteraction = Time.time;

        // 随机浮动偏移
        floatingOffset = new Vector3(
            Random.Range(0f, 360f),
            Random.Range(0f, 360f),
            Random.Range(0f, 360f)
        );
    }

    private void Start()
    {
        // 找到玩家
        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
        if (playerObject != null)
        {
            player = playerObject.transform;
        }
        else
        {
            Debug.LogWarning("LootCollector: 未找到标签为'Player'的对象");
        }

        // 初始延迟后可以收集
        StartCoroutine(InitiateCollection());
    }

    private void Update()
    {
        if (!isReadyToCollect) return;

        // 旋转效果
        if (!isFlyingToPlayer)
        {
            transform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime);

            // 浮动效果
            float floatY = Mathf.Sin((Time.time + floatingOffset.x) * floatFrequency) * floatAmplitude;
            Vector3 newPosition = startPosition + new Vector3(0, floatY, 0);
            transform.position = newPosition;
        }
        else if (player != null && isFlyingToPlayer)
        {
            FlyToPlayer();
        }

        // 检查是否超时（只有玩家不在触发器内且没有飞行时才检查）
        if (!playerInTrigger && !isFlyingToPlayer)
        {
            CheckDestroyTimeout();
        }
    }

    // 检查销毁超时
    private void CheckDestroyTimeout()
    {
        if (Time.time - timeSinceLastInteraction > destroyTimeout)
        {
            Debug.Log($"物品存在时间超过{destroyTimeout}秒，自动销毁: {resourceType}");
            Destroy(gameObject);
        }
    }

    // 设置掉落物的资源类型和数量
    public void SetResourceInfo(ResourceType type, int amount, bool isPlant = false)
    {
        resourceType = type;
        resourceAmount = amount;
    }

    private IEnumerator InitiateCollection()
    {
        // 初始延迟
        yield return new WaitForSeconds(initialDelay);
        isReadyToCollect = true;
        timeSinceLastInteraction = Time.time;

        // 检测玩家是否已经在触发器内部
        if (player != null && col != null)
        {
            if (col.bounds.Contains(player.position))
            {
                // 等效于玩家刚刚进入触发器
                playerInTrigger = true;
                timeSinceLastInteraction = Time.time;

                if (isPlantResource && plantDirectToGameVal)
                {
                    canBeCollected = true;
                    StartFlyToPlayer();
                }
                else
                {
                    if (CheckInventorySpace())
                    {
                        canBeCollected = true;
                        StartFlyToPlayer();
                    }
                    else
                    {
                        if (waitForCollectionCoroutine != null)
                            StopCoroutine(waitForCollectionCoroutine);
                        waitForCollectionCoroutine = StartCoroutine(WaitForCollectionInTrigger());
                    }
                }
            }
        }
    }

    // 当玩家进入触发器
    private void OnTriggerEnter(Collider other)
    {
        if (!isReadyToCollect || isFlyingToPlayer) return;

        if (other.CompareTag("Player"))
        {
            playerInTrigger = true;
            timeSinceLastInteraction = Time.time; // 重置超时计时

            // 如果是植物资源并且设置为直接加入数值管理器，则直接可以收集
            if (isPlantResource && plantDirectToGameVal)
            {
                canBeCollected = true;
                StartFlyToPlayer();
            }
            else
            {
                // 检查背包空间
                if (CheckInventorySpace())
                {
                    canBeCollected = true;
                    StartFlyToPlayer();
                }
                else
                {
                    // 背包已满，开始等待重试
                    if (waitForCollectionCoroutine != null)
                    {
                        StopCoroutine(waitForCollectionCoroutine);
                    }
                    waitForCollectionCoroutine = StartCoroutine(WaitForCollectionInTrigger());
                }
            }
        }
    }

    // 当玩家离开触发器
    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            playerInTrigger = false;

            // 停止等待重试的协程
            if (waitForCollectionCoroutine != null)
            {
                StopCoroutine(waitForCollectionCoroutine);
                waitForCollectionCoroutine = null;
            }

            // 如果还没开始飞行，重置时间戳
            if (!isFlyingToPlayer)
            {
                timeSinceLastInteraction = Time.time;
            }
        }
    }

    // 在触发器内等待收集
    private IEnumerator WaitForCollectionInTrigger()
    {
        while (playerInTrigger && !canBeCollected)
        {
            yield return new WaitForSeconds(0.5f); // 每0.5秒检查一次

            if (CheckInventorySpace())
            {
                canBeCollected = true;
                StartFlyToPlayer();
                yield break;
            }
        }
    }

    // 检查背包空间
    private bool CheckInventorySpace()
    {
        // 植物资源直接加入数值管理器，不需要检查背包
        if (isPlantResource && plantDirectToGameVal)
        {
            return true;
        }

        InventoryManager inventoryManager = FindObjectOfType<InventoryManager>();
        if (inventoryManager != null)
        {
            return inventoryManager.CanAddItem(resourceType, resourceAmount);
        }
        return false;
    }

    private void StartFlyToPlayer()
    {
        isFlyingToPlayer = true;
        currentFlySpeed = flySpeed;

        // 禁用物理效果
        if (rb != null)
        {
            rb.isKinematic = true;
        }

        if (col != null)
        {
            col.isTrigger = true;
        }
    }

    private void FlyToPlayer()
    {
        Vector3 direction = (player.position - transform.position).normalized;
        float distance = Vector3.Distance(transform.position, player.position);

        // 加速效果
        float timeSinceStartFlying = Time.time - (spawnTime + flyDelay);
        float accelerationFactor = flyAccelerationCurve.Evaluate(Mathf.Clamp01(timeSinceStartFlying / accelerationDuration));
        currentFlySpeed = Mathf.Lerp(flySpeed, maxFlySpeed, accelerationFactor);

        // 计算引力效果
        float attractionFactor = 1f;
        if (distance < attractionRadius)
        {
            attractionFactor = 1f + (attractionRadius - distance) / attractionRadius * attractionForce;
        }

        // 移动
        Vector3 movement = direction * currentFlySpeed * attractionFactor * Time.deltaTime;
        transform.position += movement;

        // 旋转面向移动方向
        if (movement.magnitude > 0.1f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(movement);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * 10f);
        }

        // 缩小效果（接近时）
        float distanceScaleFactor = Mathf.Clamp01(distance / 2f);
        transform.localScale = Vector3.one * distanceScaleFactor;

        // 接近玩家时检测收集
        if (distance < 0.5f)
        {
            Collect();
        }
    }

    // 添加碰撞检测，以确保即使没有进入触发器也能检测到与玩家的碰撞
    private void OnCollisionEnter(Collision collision)
    {
        // 添加碰撞检测，确保在非飞行状态下也能收集
        if (!isReadyToCollect || !canBeCollected || isFlyingToPlayer) return;

        if (collision.gameObject.CompareTag("Player"))
        {
            Collect();
        }
    }

    private void Collect()
    {
        // 如果是植物资源并且直接加入数值管理器，则不检查背包空间
        if (!isPlantResource || !plantDirectToGameVal)
        {
            // 双重检查背包空间
            if (!CheckInventorySpace())
            {
                Debug.LogWarning($"收集时背包已满: {resourceAmount} 个 {resourceType}");
                canBeCollected = false;
                isFlyingToPlayer = false;

                // 重置位置和状态
                transform.position = startPosition;

                // 重新等待空间
                StartCoroutine(WaitAndRetryCollection());
                return;
            }
        }

        // 播放收集特效
        if (collectEffectPrefab != null)
        {
            Instantiate(collectEffectPrefab, transform.position, Quaternion.identity);
        }

        // 播放收集音效
        if (collectSound != null)
        {
            AudioSource.PlayClipAtPoint(collectSound, transform.position);
        }

        // 触发玩家反馈
        TriggerPlayerFeedback();

        bool addedSuccessfully = false;

        // 如果是植物资源并且直接加入数值管理器
        if (isPlantResource && plantDirectToGameVal)
        {
            // 直接加入数值管理器
            if (GameValManager.Instance != null)
            {
                addedSuccessfully = GameValManager.Instance.AddResource(resourceType, resourceAmount);
                Debug.Log($"植物资源直接加入数值管理器: {resourceAmount} 个 {resourceType}");
            }
            else
            {
                Debug.LogError("GameValManager实例为空，无法添加植物资源");
                addedSuccessfully = false;
            }
        }
        else
        {
            // 正常流程：先添加到背包，再更新数值管理器
            InventoryManager inventoryManager = FindObjectOfType<InventoryManager>();

            if (inventoryManager != null)
            {
                addedSuccessfully = inventoryManager.AddItem(resourceType, resourceAmount);
            }

            if (addedSuccessfully)
            {
                // 同时更新资源管理器
                if (GameValManager.Instance != null)
                {
                    GameValManager.Instance.AddResource(resourceType, resourceAmount);
                }
                Debug.Log($"已收集: {resourceAmount} 个 {resourceType}");
            }
        }

        if (addedSuccessfully)
        {
            // 销毁自身
            Destroy(gameObject);
        }
        else
        {
            // 添加失败，重新等待
            Debug.LogWarning($"收集失败: {resourceAmount} 个 {resourceType}");
            canBeCollected = false;
            isFlyingToPlayer = false;
            transform.position = startPosition;

            // 如果是植物资源，直接等待然后重试
            if (isPlantResource && plantDirectToGameVal)
            {
                StartCoroutine(WaitAndRetryCollection());
            }
            else
            {
                // 重新等待背包空间
                StartCoroutine(WaitAndRetryCollection());
            }
        }
    }

    // 等待并重试收集
    private IEnumerator WaitAndRetryCollection()
    {
        while (!canBeCollected)
        {
            yield return new WaitForSeconds(0.5f); // 每0.5秒检查一次

            if (CheckInventorySpace())
            {
                canBeCollected = true;
                StartFlyToPlayer();
                yield break;
            }
        }
    }

    private void TriggerPlayerFeedback()
    {
        if (player != null)
        {
            PlayerFeedback playerFeedback = player.GetComponent<PlayerFeedback>();
            if (playerFeedback != null)
            {
                playerFeedback.OnItemCollected();
            }
        }
    }

    // 在编辑器模式下绘制引力半径
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, attractionRadius);
    }
}