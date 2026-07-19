using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public enum LootStorageMode
{
    UseLegacyConfiguration = 0,
    SlotInventory = 1,
    RunIngredientBag = 2,
    PermanentResource = 3
}

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
    [SerializeField] private float destroyTimeout = 10f;  // Total lifetime from spawn.
    [Tooltip("Seconds of blinking before the loot expires. Included in Destroy Timeout.")]
    [SerializeField] private float expiryWarningDuration = 3f;
    [Tooltip("Visibility toggle interval during the expiry warning.")]
    [SerializeField] private float expiryBlinkInterval = 0.15f;

    [Header("特效设置")]
    [SerializeField] private GameObject collectEffectPrefab; // 收集特效
    [SerializeField] private AudioClip collectSound;         // 收集音效

    [Header("资源设置")]
    [SerializeField] private ResourceType resourceType = ResourceType.Money; // 资源类型
    [SerializeField] private int resourceAmount = 1;                         // 资源数量
    [SerializeField] private bool useCustomResource = false;                 // 是否使用自定义资源设置

    [Header("植物设置")]
    [Tooltip("是否为植物类资源")]
    [HideInInspector]
    [SerializeField] private bool isPlantResource = false; // 是否为植物类资源
    [Tooltip("Where this pickup is stored. Legacy preserves existing prefab behavior.")]
    [SerializeField] private LootStorageMode storageMode = LootStorageMode.UseLegacyConfiguration;
    [HideInInspector]
    [SerializeField] private bool plantDirectToGameVal = true; // Legacy compatibility only.

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
    private Renderer[] cachedRenderers;
    private bool[] rendererInitialStates;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        col = GetComponent<Collider>();
        startPosition = transform.position;
        spawnTime = Time.time;
        timeSinceLastInteraction = Time.time;

        cachedRenderers = GetComponentsInChildren<Renderer>(true);
        rendererInitialStates = new bool[cachedRenderers.Length];
        for (int i = 0; i < cachedRenderers.Length; i++)
            rendererInitialStates[i] = cachedRenderers[i] != null && cachedRenderers[i].enabled;

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

            // 兜底：物体生成时玩家已在范围内，或 Update 直接改位置导致 OnTriggerEnter 未触发
            if (!canBeCollected && !isFlyingToPlayer)
                TryBeginCollectionFromPlayerOverlap();
        }
        else if (player != null && isFlyingToPlayer)
        {
            FlyToPlayer();
        }

        // Lifetime is based on spawn time and is never reset by trigger interaction.
        if (!isFlyingToPlayer)
            CheckDestroyTimeout();
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
        if (destroyTimeout <= 0f) return;

        float age = Time.time - spawnTime;
        if (age >= destroyTimeout)
        {
            Debug.Log($"物品存在时间超过{destroyTimeout}秒，自动销毁: {resourceType}");
            Destroy(gameObject);
            return;
        }

        float warningStart = Mathf.Max(0f, destroyTimeout - Mathf.Max(0f, expiryWarningDuration));
        if (age < warningStart)
        {
            RestoreRendererVisibility();
            return;
        }

        float interval = Mathf.Max(0.03f, expiryBlinkInterval);
        bool visible = Mathf.FloorToInt((age - warningStart) / interval) % 2 == 0;
        SetRendererVisibility(visible);
    }

    // 设置掉落物的资源类型和数量
    public void SetResourceInfo(ResourceType type, int amount)
    {
        resourceType = type;
        resourceAmount = amount;
    }

    public void SetResourceInfo(ResourceType type, int amount, LootStorageMode targetStorageMode)
    {
        resourceType = type;
        resourceAmount = amount;
        storageMode = targetStorageMode;
    }

    private IEnumerator InitiateCollection()
    {
        // 初始延迟
        yield return new WaitForSeconds(initialDelay);
        isReadyToCollect = true;
        timeSinceLastInteraction = Time.time;

        TryBeginCollectionFromPlayerOverlap();
    }

    private static bool IsPlayerCollider(Collider other)
    {
        if (other == null) return false;
        if (other.CompareTag("Player")) return true;
        if (other.attachedRigidbody != null && other.attachedRigidbody.CompareTag("Player")) return true;
        return other.transform.root != null && other.transform.root.CompareTag("Player");
    }

    private static bool IsPlayerDead(Collider other)
    {
        if (other == null) return false;

        TopDownController ctrl = other.GetComponent<TopDownController>();
        if (ctrl == null && other.attachedRigidbody != null)
            ctrl = other.attachedRigidbody.GetComponent<TopDownController>();
        if (ctrl == null && other.transform.root != null)
            ctrl = other.transform.root.GetComponent<TopDownController>();

        return ctrl != null && ctrl.isDead;
    }

    private void TryBeginCollectionFromPlayerOverlap()
    {
        if (!isReadyToCollect || isFlyingToPlayer || canBeCollected || col == null)
            return;

        if (player == null)
        {
            GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
            if (playerObject != null)
                player = playerObject.transform;
        }

        if (player == null)
            return;

        Vector3 closest = col.ClosestPoint(player.position);
        if (Vector3.Distance(closest, player.position) > 0.35f)
            return;

        BeginCollectionForPlayer();
    }

    private void BeginCollectionForPlayer()
    {
        playerInTrigger = true;
        timeSinceLastInteraction = Time.time;

        if (UsesNoSlotStorage())
        {
            canBeCollected = true;
            StartFlyToPlayer();
            return;
        }

        bool hasSpace = CheckInventorySpace();
        isInventoryFull = !hasSpace;

        if (hasSpace)
        {
            canBeCollected = true;
            StartFlyToPlayer();
            return;
        }

        if (flyAndDestroyWhenFull)
        {
            canBeCollected = true;
            isInventoryFull = true;
            ShowFullInventoryMessage();
            StartFlyToPlayer();
            return;
        }

        ShowFullInventoryMessage();
        if (waitForCollectionCoroutine != null)
            StopCoroutine(waitForCollectionCoroutine);
        waitForCollectionCoroutine = StartCoroutine(WaitForCollectionInTrigger());
    }

    // 当玩家进入触发器
    private void OnTriggerEnter(Collider other)
    {
        if (!isReadyToCollect || isFlyingToPlayer || canBeCollected) return;
        if (!IsPlayerCollider(other) || IsPlayerDead(other)) return;

        BeginCollectionForPlayer();
    }

    private void OnTriggerStay(Collider other)
    {
        if (!isReadyToCollect || isFlyingToPlayer || canBeCollected) return;
        if (!IsPlayerCollider(other) || IsPlayerDead(other)) return;

        BeginCollectionForPlayer();
    }

    // 当玩家离开触发器
    private void OnTriggerExit(Collider other)
    {
        if (IsPlayerCollider(other))
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

    private LootStorageMode GetEffectiveStorageMode()
    {
        if (storageMode != LootStorageMode.UseLegacyConfiguration)
            return storageMode;

        // Existing plant prefabs used these two booleans. Preserve their serialized
        // behavior, but route them to the run-only bag instead of permanent storage.
        return plantDirectToGameVal && isPlantResource
            ? LootStorageMode.RunIngredientBag
            : LootStorageMode.SlotInventory;
    }

    private bool UsesNoSlotStorage()
    {
        LootStorageMode mode = GetEffectiveStorageMode();
        return mode == LootStorageMode.RunIngredientBag || mode == LootStorageMode.PermanentResource;
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
        RestoreRendererVisibility();
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

    private void SetRendererVisibility(bool visible)
    {
        if (cachedRenderers == null) return;
        for (int i = 0; i < cachedRenderers.Length; i++)
        {
            if (cachedRenderers[i] != null)
                cachedRenderers[i].enabled = visible && rendererInitialStates[i];
        }
    }

    private void RestoreRendererVisibility()
    {
        if (cachedRenderers == null) return;
        for (int i = 0; i < cachedRenderers.Length; i++)
        {
            if (cachedRenderers[i] != null)
                cachedRenderers[i].enabled = rendererInitialStates[i];
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
        LootStorageMode effectiveStorageMode = GetEffectiveStorageMode();
        if (UsesNoSlotStorage())
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

            bool stored = false;
            if (effectiveStorageMode == LootStorageMode.RunIngredientBag)
            {
                InventoryManager inventoryManager = FindObjectOfType<InventoryManager>();
                stored = inventoryManager != null;
                if (stored)
                    inventoryManager.AddRunIngredient(resourceType, resourceAmount);
            }
            else if (effectiveStorageMode == LootStorageMode.PermanentResource && GameValManager.Instance != null)
            {
                GameValManager.Instance.AddResource(resourceType, resourceAmount);
                stored = true;
            }

            if (stored)
            {
                Debug.Log($"已收集到 {effectiveStorageMode}: {resourceAmount} 个 {resourceType}");
                Destroy(gameObject);
                return;
            }

            Debug.LogWarning($"{effectiveStorageMode} 不可用，资源未写入，将按占格背包逻辑回退：{resourceAmount} 个 {resourceType}");
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
        if (UsesNoSlotStorage())
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
