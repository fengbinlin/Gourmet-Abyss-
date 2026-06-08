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
    [SerializeField] private float randomStartRotation = 360f; // 随机起始旋转角度范围

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

    [Header("背包已满设置")]
    [Tooltip("背包已满时是否仍然飞向玩家并销毁")]
    [SerializeField] private bool flyAndDestroyWhenFull = true; // 背包已满时是否仍然飞向玩家并销毁
    [Tooltip("背包满时重试检测间隔（秒）")]
    [SerializeField] private float fullRetryInterval = 0.5f;
    [Tooltip("背包满提示消息冷却（秒），防止刷屏")]
    [SerializeField] private float fullMessageCooldown = 1.5f;
    [SerializeField] private string fullInventoryMessage = "背包已满，无法收集";

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
    private bool isInventoryFull = false; // 标记背包是否已满
    private float lastFullMessageTime = -999f;

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
        
        // 添加随机起始旋转角度
        ApplyRandomStartRotation();
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

    // 添加随机起始旋转
    private void ApplyRandomStartRotation()
    {
        // 在Y轴上生成随机旋转角度
        float randomYRotation = Random.Range(0f, randomStartRotation);
        
        // 应用旋转
        transform.rotation = Quaternion.Euler(0f, randomYRotation, 0f);
        
        // 如果需要更随机的三维旋转，可以取消下面的注释
        // float randomXRotation = Random.Range(0f, randomStartRotation);
        // float randomZRotation = Random.Range(0f, randomStartRotation);
        // transform.rotation = Quaternion.Euler(randomXRotation, randomYRotation, randomZRotation);
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
        isPlantResource = isPlant;
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

                if (IsDirectToGameValPickup())
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
            if (other.GetComponent<TopDownController>().isDead)
            {
                return;
            }
            playerInTrigger = true;
            timeSinceLastInteraction = Time.time; // 重置超时计时
            
            // 如果是植物资源并且设置为直接加入数值管理器，则直接可以收集
            if (IsDirectToGameValPickup())
            {
                canBeCollected = true;
                StartFlyToPlayer();
            }
            else
            {
                // 检查背包空间
                bool hasSpace = CheckInventorySpace();
                isInventoryFull = !hasSpace;
                
                if (hasSpace)
                {
                    canBeCollected = true;
                    StartFlyToPlayer();
                }
                else
                {
                    // 背包已满
                    if (flyAndDestroyWhenFull)
                    {
                        // 即使背包已满，也直接开始飞向玩家
                        canBeCollected = true;
                        isInventoryFull = true;
                        ShowFullInventoryMessage();
                        StartFlyToPlayer();
                    }
                    else
                    {
                        ShowFullInventoryMessage();
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
            yield return new WaitForSeconds(Mathf.Max(0.1f, fullRetryInterval));

            bool hasSpace = CheckInventorySpace();
            isInventoryFull = !hasSpace;
            
            if (hasSpace)
            {
                canBeCollected = true;
                StartFlyToPlayer();
                yield break;
            }
            else if (flyAndDestroyWhenFull)
            {
                // 即使背包已满，也直接开始飞向玩家
                canBeCollected = true;
                isInventoryFull = true;
                ShowFullInventoryMessage();
                StartFlyToPlayer();
                yield break;
            }
            else
            {
                ShowFullInventoryMessage();
            }
        }
    }

    /// <summary>植物/南瓜等直入 GameValManager 的拾取物，不占背包，也不提示背包满。</summary>
    private bool IsDirectToGameValPickup()
    {
        return plantDirectToGameVal && (isPlantResource || resourceType == ResourceType.LootPumkin);
    }

    // 检查背包空间
    private bool CheckInventorySpace()
    {
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
        // 植物类/南瓜类收集物：直接写入数值管理器，不进背包
        // 约定：当 plantDirectToGameVal 开启时，勾选 isPlantResource 的物体会直入；同时南瓜（LootPumkin）强制按植物类处理，避免 prefab/生成逻辑漏传 isPlant 导致进背包。
        bool shouldDirectToGameVal = IsDirectToGameValPickup();
        if (shouldDirectToGameVal)
        {
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

            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlayAudio("2");
            }

            if (GameValManager.Instance != null)
            {
                GameValManager.Instance.AddResource(resourceType, resourceAmount);
                Debug.Log($"已收集并直接加入数值管理器: {resourceAmount} 个 {resourceType}");
                Destroy(gameObject);
                return;
            }

            Debug.LogWarning($"GameValManager.Instance 为空，无法直入资源：{resourceAmount} 个 {resourceType}；将按背包逻辑回退");
            // 若数值管理器未初始化，回退到原背包逻辑（不 return）
        }

        // 统一走战斗背包：收集前都要检查背包空间
        bool hasSpaceNow = CheckInventorySpace();
        isInventoryFull = !hasSpaceNow;

        if (!hasSpaceNow && !flyAndDestroyWhenFull)
        {
            Debug.LogWarning($"收集时背包已满: {resourceAmount} 个 {resourceType}");
            ShowFullInventoryMessage();
            canBeCollected = false;
            isFlyingToPlayer = false;

            // 重置位置和状态
            transform.position = startPosition;

            // 重新等待空间
            StartCoroutine(WaitAndRetryCollection());
            return;
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

        // 检查背包是否已满
        if (!isInventoryFull)
        {
            AudioManager.Instance.PlayAudio("2");

            // 统一逻辑：战斗中掉落物只加入临时背包，不直接写入 GameValManager
            InventoryManager inventoryManager = FindObjectOfType<InventoryManager>();
            if (inventoryManager != null)
            {
                addedSuccessfully = inventoryManager.AddItem(resourceType, resourceAmount);
            }

            if (addedSuccessfully)
            {
                Debug.Log($"已收集到战斗背包: {resourceAmount} 个 {resourceType}");
            }
        }
        else
        {
            // 背包已满，不添加任何数值，但仍然销毁物品
            Debug.Log($"背包已满，物品销毁但未添加数值: {resourceAmount} 个 {resourceType}");
            ShowFullInventoryMessage();
            addedSuccessfully = true; // 标记为成功，以便销毁物品
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

            // 统一等待背包空间后重试
            StartCoroutine(WaitAndRetryCollection());
        }
    }

    // 等待并重试收集
    private IEnumerator WaitAndRetryCollection()
    {
        while (!canBeCollected)
        {
            yield return new WaitForSeconds(Mathf.Max(0.1f, fullRetryInterval));

            bool hasSpace = CheckInventorySpace();
            isInventoryFull = !hasSpace;
            
            if (hasSpace)
            {
                canBeCollected = true;
                StartFlyToPlayer();
                yield break;
            }
            else if (flyAndDestroyWhenFull)
            {
                // 即使背包已满，也直接开始飞向玩家
                canBeCollected = true;
                isInventoryFull = true;
                ShowFullInventoryMessage();
                StartFlyToPlayer();
                yield break;
            }
            else
            {
                ShowFullInventoryMessage();
            }
        }
    }

    private void ShowFullInventoryMessage()
    {
        if (IsDirectToGameValPickup())
            return;

        if (Time.time - lastFullMessageTime < Mathf.Max(0.1f, fullMessageCooldown))
            return;

        lastFullMessageTime = Time.time;
        GlobalMessageUI.Show(fullInventoryMessage);
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