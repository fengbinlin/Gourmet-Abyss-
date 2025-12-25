using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.SceneManagement;
public class EnemyHealth : MonoBehaviour
{
    [Header("生命值设置")]
    [SerializeField] private float maxHealth = 100f;
    [SerializeField] private float currentHealth;
    
    [Header("进度条UI设置")]
    [SerializeField] private RectTransform whiteBar;      // 白色进度条RectTransform
    [SerializeField] private RectTransform redBar;       // 红色进度条RectTransform
    [SerializeField] private GameObject healthBarParent;  // 血量条父物体
    [SerializeField] private float maxBarWidth = 200f;   // 进度条最大宽度
    [SerializeField] private float fillDelay = 0.5f;     // 白色到红色的延迟时间
    [SerializeField] private float fillDuration = 0.3f;  // 红色填充持续时间
    
    [Header("血量条隐藏设置")]
    [SerializeField] private float hideDelay = 2f;       // 停止受击后隐藏血量条的延迟时间
    
    [Header("受击反馈设置")]
    [SerializeField] private float flashDuration = 0.1f;
    [SerializeField] private Color flashColor = Color.white;
    [SerializeField] private float scaleMultiplier = 1.2f;
    [SerializeField] private float scaleDuration = 0.15f;
    [SerializeField] private GameObject hitParticlePrefab;
    [SerializeField] private Transform hitParticleSpawnPoint;
    
    [Header("死亡效果设置")]
    [SerializeField] private GameObject explosionEffectPrefab; // 爆炸特效预制体
    [SerializeField] private float deathEffectDelay = 0.2f;   // 死亡后多久播放爆炸特效
    [SerializeField] private float destroyDelayAfterDeath = 0.1f; // 死亡后多久销毁自身
    
    [Header("死亡击退设置")]
    [SerializeField] private float deathKnockbackDistance = 1.5f; // 击退距离
    [SerializeField] private float deathKnockbackDuration = 0.3f; // 击退持续时间
    [SerializeField] private AnimationCurve deathKnockbackCurve = AnimationCurve.EaseInOut(0, 0, 1, 1); // 击退曲线
    
    [Header("屏幕震动设置")]
    [SerializeField] private float shakeDuration = 0.1f;
    [SerializeField] private float shakeMagnitude = 0.1f;
    
    [Header("动画设置")]
    [SerializeField] private Animator animator;
    [SerializeField] private string hitAnimationTrigger = "Hit";
    
    [Header("掉落物设置")]
    [SerializeField] private LootItem[] lootItems; // 可掉落的物品列表
    
    [System.Serializable]
    public class LootItem
    {
        public GameObject itemPrefab;    // 掉落物预制体
        [Range(0f, 1f)]
        public float dropChance = 0.3f; // 掉落概率 (0-1)
        public int minAmount = 1;       // 最小掉落数量
        public int maxAmount = 3;       // 最大掉落数量
        public float scatterForce = 5f; // 散射力
        
        // 新增：资源类型和数量
        public ResourceType resourceType = ResourceType.Money;
        public int resourceMinAmount = 1;
        public int resourceMaxAmount = 1;
        
        // 新增：资源数量计算
        public int GetRandomResourceAmount()
        {
            return Random.Range(resourceMinAmount, resourceMaxAmount + 1);
        }
    }
    
    private List<Renderer> allRenderers = new List<Renderer>();
    private Dictionary<Renderer, MaterialPropertyBlock[]> originalPropertyBlocks = new Dictionary<Renderer, MaterialPropertyBlock[]>();
    private Vector3 originalScale;
    private CameraFollow cameraFollow;
    private EnemyAI enemyAI;
    private Vector3 lastHitDirection; // 记录最后一次被击中的方向
    private Vector3 lastHitPoint; // 记录最后一次击中点
    private bool isDead = false;
    
    // 进度条相关变量
    private Coroutine redFillCoroutine;
    private Coroutine whiteFillCoroutine;
    
    // 血量条隐藏相关变量
    private Coroutine hideCoroutine;
    
    private void Awake()
    {
        currentHealth = maxHealth;
        originalScale = transform.localScale;
        
        GetAllRenderers();
        
        if (hitParticleSpawnPoint == null)
            hitParticleSpawnPoint = transform;
            
        if (animator == null)
            animator = GetComponent<Animator>();
            
        cameraFollow = FindFirstObjectByType<CameraFollow>();
        enemyAI = GetComponent<EnemyAI>();
        
        // 初始化进度条
        InitializeHealthBar();
    }
    
    private void InitializeHealthBar()
    {
        if (whiteBar != null && redBar != null)
        {
            // 设置锚点和轴心为中间
            SetAnchorAndPivotToCenter(whiteBar);
            SetAnchorAndPivotToCenter(redBar);
            
            // 初始时进度条宽度为0
            SetBarWidth(whiteBar, 0f);
            SetBarWidth(redBar, 0f);
            
            // 初始隐藏进度条
            if (healthBarParent != null)
            {
                healthBarParent.SetActive(false);
            }
            else
            {
                // 如果没有指定父物体，隐藏两个进度条
                whiteBar.gameObject.SetActive(false);
                redBar.gameObject.SetActive(false);
            }
        }
    }
    
    // 设置RectTransform的锚点和轴心为中间
    private void SetAnchorAndPivotToCenter(RectTransform rectTransform)
    {
        rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
    }
    
    // 设置进度条宽度
    private void SetBarWidth(RectTransform bar, float width)
    {
        bar.sizeDelta = new Vector2(width, bar.sizeDelta.y);
    }
    
    // 获取当前宽度
    private float GetBarWidth(RectTransform bar)
    {
        return bar.sizeDelta.x;
    }
    
    // 显示血量条
    private void ShowHealthBar()
    {
        if (healthBarParent != null)
        {
            healthBarParent.SetActive(true);
        }
        else if (whiteBar != null && redBar != null)
        {
            whiteBar.gameObject.SetActive(true);
            redBar.gameObject.SetActive(true);
        }
    }
    
    // 隐藏血量条
    private void HideHealthBar()
    {
        // if (isDead) return; // 死亡时不隐藏血量条
        
        if (healthBarParent != null)
        {
            healthBarParent.SetActive(false);
        }
        else if (whiteBar != null && redBar != null)
        {
            whiteBar.gameObject.SetActive(false);
            redBar.gameObject.SetActive(false);
        }
    }
    
    // 重置血量条隐藏计时器
    private void ResetHideTimer()
    {
        // 先停止之前的隐藏协程
        if (hideCoroutine != null)
        {
            StopCoroutine(hideCoroutine);
        }
        
        // 显示血量条
        ShowHealthBar();
        
        // 开始新的隐藏计时
        hideCoroutine = StartCoroutine(StartHideTimer());
    }
    
    // 开始隐藏计时器
    private IEnumerator StartHideTimer()
    {
        yield return new WaitForSeconds(hideDelay);
        HideHealthBar();
    }
    
    // 更新进度条显示
    private void UpdateHealthBar()
    {
        if (whiteBar == null || redBar == null) return;
        
        // 计算损失的血量百分比
        float healthPercent = currentHealth / maxHealth;
        float lostHealthPercent = 1f - healthPercent;
        float targetWidth = lostHealthPercent * maxBarWidth;
        
        // 重置隐藏计时器
        ResetHideTimer();
        
        // 停止之前的填充协程
        if (whiteFillCoroutine != null)
        {
            StopCoroutine(whiteFillCoroutine);
        }
        
        // 先立即将白色进度条扩展到目标宽度
        whiteFillCoroutine = StartCoroutine(FillWhiteBar(targetWidth));
        
        // 延迟后开始红色填充
        if (redFillCoroutine != null)
        {
            StopCoroutine(redFillCoroutine);
        }
        redFillCoroutine = StartCoroutine(FillRedBarDelayed(targetWidth));
    }
    
    // 填充白色进度条
    private IEnumerator FillWhiteBar(float targetWidth)
    {
        float startWidth = GetBarWidth(whiteBar);
        float elapsedTime = 0f;
        float duration = 0.1f; // 白色填充很快
        
        while (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;
            float t = elapsedTime / duration;
            float currentWidth = Mathf.Lerp(startWidth, targetWidth, t);
            SetBarWidth(whiteBar, currentWidth);
            yield return null;
        }
        
        SetBarWidth(whiteBar, targetWidth);
    }
    
    // 延迟后填充红色进度条
    private IEnumerator FillRedBarDelayed(float targetWidth)
    {
        // 等待延迟
        yield return new WaitForSeconds(fillDelay);
        
        float startWidth = GetBarWidth(redBar);
        float elapsedTime = 0f;
        
        while (elapsedTime < fillDuration)
        {
            elapsedTime += Time.deltaTime;
            float t = elapsedTime / fillDuration;
            float currentWidth = Mathf.Lerp(startWidth, targetWidth, t);
            SetBarWidth(redBar, currentWidth);
            yield return null;
        }
        
        SetBarWidth(redBar, targetWidth);
    }
    
    private void GetAllRenderers()
    {
        allRenderers.Clear();
        originalPropertyBlocks.Clear();
        
        Renderer[] childRenderers = GetComponentsInChildren<Renderer>(true);
        allRenderers.AddRange(childRenderers);
        
        foreach (var renderer in allRenderers)
        {
            int materialCount = renderer.sharedMaterials.Length;
            MaterialPropertyBlock[] propertyBlocks = new MaterialPropertyBlock[materialCount];
            
            for (int i = 0; i < materialCount; i++)
            {
                propertyBlocks[i] = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(propertyBlocks[i], i);
            }
            
            originalPropertyBlocks[renderer] = propertyBlocks;
        }
    }
    
    // 主要伤害处理方法
    public void TakeDamage(float damage, Vector3 hitPoint, Vector3 hitNormal)
    {
        if (currentHealth <= 0 || isDead)
        {
            print("AAA");
            HideHealthBar();
            return;
        } 
        
        currentHealth -= damage;
        
        // 更新进度条
        UpdateHealthBar();
        
        // 计算击中方向
        if (hitPoint != Vector3.zero)
        {
            lastHitDirection = (hitPoint - transform.position).normalized;
        }
        
        lastHitPoint = hitPoint;
        
        StartCoroutine(HitFeedbackCoroutine(hitPoint, hitNormal));
        
        if (enemyAI != null)
        {
            enemyAI.OnHit();
        }
        
        if (currentHealth <= 0)
        {
            Die();
        }
    }
    
    // 专门处理子弹击中的方法
    public void TakeDamageFromProjectile(float damage, Vector3 hitPoint, Vector3 hitNormal, Vector3 bulletPosition, Vector3 bulletDirection)
    {
        if (currentHealth <= 0 || isDead)
        {
            print("AAA");
            HideHealthBar();
            return;
        } 
        
        
        currentHealth -= damage;
        
        // 更新进度条
        UpdateHealthBar();
        
        // 记录子弹方向
        lastHitDirection = bulletDirection.normalized;
        lastHitPoint = hitPoint;
        
        // 调试：绘制子弹方向
        Debug.DrawRay(bulletPosition, bulletDirection * 2f, Color.red, 2f);
        Debug.DrawRay(hitPoint, lastHitDirection * 2f, Color.blue, 2f);
        
        StartCoroutine(HitFeedbackCoroutine(hitPoint, hitNormal));
        
        if (enemyAI != null)
        {
            enemyAI.OnHit();
        }
        
        if (currentHealth <= 0)
        {
            Die();
        }
    }
    
    private IEnumerator HitFeedbackCoroutine(Vector3 hitPoint, Vector3 hitNormal)
    {
        StartCoroutine(FlashWhiteCoroutine());
        StartCoroutine(ScaleBounceCoroutine());
        SpawnHitParticle(hitPoint, hitNormal);
        PlayHitAnimation();
        TriggerScreenShake();
        
        yield return null;
    }
    
    private IEnumerator FlashWhiteCoroutine()
    {
        SetEmissionColorForAllRenderers(flashColor);
        yield return new WaitForSeconds(flashDuration);
        RestoreOriginalEmissionColor();
    }
    
    private void SetEmissionColorForAllRenderers(Color color)
    {
        foreach (var renderer in allRenderers)
        {
            if (renderer == null) continue;
            
            int materialCount = renderer.sharedMaterials.Length;
            for (int i = 0; i < materialCount; i++)
            {
                MaterialPropertyBlock propertyBlock = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(propertyBlock, i);
                propertyBlock.SetColor("_EmissionColor", color);
                
                if (propertyBlock.isEmpty)
                {
                    propertyBlock.SetColor("_BaseColor", color);
                }
                
                renderer.SetPropertyBlock(propertyBlock, i);
            }
        }
    }
    
    private void RestoreOriginalEmissionColor()
    {
        foreach (var renderer in allRenderers)
        {
            if (renderer == null) continue;
            
            if (originalPropertyBlocks.TryGetValue(renderer, out MaterialPropertyBlock[] originalBlocks))
            {
                for (int i = 0; i < originalBlocks.Length; i++)
                {
                    renderer.SetPropertyBlock(originalBlocks[i], i);
                }
            }
            else
            {
                SetEmissionColorForRenderer(renderer, Color.black);
            }
        }
    }
    
    private void SetEmissionColorForRenderer(Renderer renderer, Color color)
    {
        int materialCount = renderer.sharedMaterials.Length;
        for (int i = 0; i < materialCount; i++)
        {
            MaterialPropertyBlock propertyBlock = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(propertyBlock, i);
            propertyBlock.SetColor("_EmissionColor", color);
            renderer.SetPropertyBlock(propertyBlock, i);
        }
    }
    
    private IEnumerator ScaleBounceCoroutine()
    {
        Vector3 targetScale = originalScale * scaleMultiplier;
        
        float elapsedTime = 0f;
        while (elapsedTime < scaleDuration / 2)
        {
            transform.localScale = Vector3.Lerp(originalScale, targetScale, elapsedTime / (scaleDuration / 2));
            elapsedTime += Time.deltaTime;
            yield return null;
        }
        
        elapsedTime = 0f;
        while (elapsedTime < scaleDuration / 2)
        {
            transform.localScale = Vector3.Lerp(targetScale, originalScale, elapsedTime / (scaleDuration / 2));
            elapsedTime += Time.deltaTime;
            yield return null;
        }
        
        transform.localScale = originalScale;
    }
    
    private void SpawnHitParticle(Vector3 hitPoint, Vector3 hitNormal)
    {
        if (hitParticlePrefab != null)
        {
            Vector3 spawnPoint = hitParticleSpawnPoint.position;
            
            if (hitPoint != Vector3.zero)
            {
                spawnPoint = hitPoint;
            }
            
            Quaternion rotation = Quaternion.identity;
            if (hitNormal != Vector3.zero)
            {
                rotation = Quaternion.LookRotation(hitNormal);
            }
            
            GameObject particle = Instantiate(hitParticlePrefab, spawnPoint, rotation);
            Destroy(particle, 2f);
        }
    }
    
    private void PlayHitAnimation()
    {
        if (animator != null && !string.IsNullOrEmpty(hitAnimationTrigger))
        {
            animator.SetTrigger(hitAnimationTrigger);
        }
    }
    
    private void TriggerScreenShake()
    {
        if (cameraFollow != null)
        {
            cameraFollow.Shake(shakeDuration, shakeMagnitude);
        }
    }
    
    private IEnumerator DeathKnockbackCoroutine()
    {
        Vector3 knockbackDirection = Vector3.zero;
        
        // 计算击退方向
        if (lastHitDirection != Vector3.zero)
        {
            // 子弹飞行的方向，击退应该沿着这个方向
            knockbackDirection = lastHitDirection;
            
            // 确保不是零向量
            if (knockbackDirection.magnitude < 0.1f)
            {
                // 备用方向：朝向摄像机前方
                Vector3 cameraForward = Camera.main.transform.forward;
                cameraForward.y = 0;
                knockbackDirection = cameraForward.normalized;
            }
            
            // 调整Y轴分量，可以添加一点向上或向下的力
            knockbackDirection.y = Mathf.Clamp(knockbackDirection.y, 0.1f, 0.3f);
            knockbackDirection = knockbackDirection.normalized;
            
            //Debug.Log($"击退方向: {knockbackDirection}, 原始方向: {lastHitDirection}");
        }
        else
        {
            // 如果没有击中方向，使用默认方向
            Vector3 cameraForward = Camera.main.transform.forward;
            cameraForward.y = 0;
            knockbackDirection = cameraForward.normalized;
        }
        
        Vector3 startPosition = transform.position;
        Vector3 targetPosition = startPosition + knockbackDirection * deathKnockbackDistance;
        
        float elapsedTime = 0f;
        
        while (elapsedTime < deathKnockbackDuration)
        {
            float t = elapsedTime / deathKnockbackDuration;
            float curveValue = deathKnockbackCurve.Evaluate(t);
            
            transform.position = Vector3.Lerp(startPosition, targetPosition, curveValue);
            
            elapsedTime += Time.deltaTime;
            yield return null;
        }
        
        transform.position = targetPosition;
    }
    
    // 生成爆炸特效
    private void SpawnExplosionEffect()
    {
        if (explosionEffectPrefab != null)
        {
            // 实例化爆炸特效
            GameObject explosion = Instantiate(explosionEffectPrefab, transform.position+new Vector3(0,0.5f,0), Quaternion.identity);
            
            // 获取爆炸特效的持续时间
            ParticleSystem explosionParticles = explosion.GetComponent<ParticleSystem>();
            float explosionDuration = 2f; // 默认2秒
            
            if (explosionParticles != null)
            {
                explosionDuration = explosionParticles.main.duration;
            }
            else
            {
                // 如果没有ParticleSystem，尝试获取子物体中的ParticleSystem
                ParticleSystem[] allParticles = explosion.GetComponentsInChildren<ParticleSystem>();
                if (allParticles.Length > 0)
                {
                    // 找到持续时间最长的粒子系统
                    foreach (var ps in allParticles)
                    {
                        float duration = ps.main.duration;
                        if (duration > explosionDuration)
                        {
                            explosionDuration = duration;
                        }
                    }
                }
            }
            
            // 在特效播放完后自动销毁
            Destroy(explosion, explosionDuration);
        }
    }
    
    // 生成掉落物
    private void SpawnLootItems()
    {
        if (lootItems == null || lootItems.Length == 0) return;
        
        foreach (LootItem lootItem in lootItems)
        {
            // 根据掉落概率决定是否生成
            if (Random.value <= lootItem.dropChance)
            {
                // 生成随机数量的掉落物实例
                int itemAmount = Random.Range(lootItem.minAmount, lootItem.maxAmount + 1);
                
                for (int i = 0; i < itemAmount; i++)
                {
                    // 生成位置在敌人周围随机偏移
                    Vector3 spawnPosition = transform.position + 
                                          new Vector3(Random.Range(-0.5f, 0.5f), 
                                                      Random.Range(0.5f, 1.5f), 
                                                      Random.Range(-0.5f, 0.5f));
                    
                    GameObject item = Instantiate(lootItem.itemPrefab, spawnPosition, Quaternion.identity);
                    SceneManager.MoveGameObjectToScene(item, gameObject.scene);
                    // 设置掉落物的资源信息
                    LootCollector lootCollector = item.GetComponent<LootCollector>();
                    if (lootCollector != null)
                    {
                        // 从LootItem中获取资源信息
                        int resourceAmount = lootItem.GetRandomResourceAmount();
                        lootCollector.SetResourceInfo(lootItem.resourceType, resourceAmount);
                    }
                    
                    // 为掉落物添加随机的散射力
                    Rigidbody rb = item.GetComponent<Rigidbody>();
                    if (rb != null)
                    {
                        Vector3 randomDirection = new Vector3(
                            Random.Range(-1f, 1f),
                            Random.Range(0.3f, 1f),
                            Random.Range(-1f, 1f)
                        ).normalized;
                        
                        rb.AddForce(randomDirection * lootItem.scatterForce, ForceMode.Impulse);
                        rb.AddTorque(new Vector3(
                            Random.Range(-100f, 100f),
                            Random.Range(-100f, 100f),
                            Random.Range(-100f, 100f)
                        ));
                    }
                }
            }
        }
    }
    
    // 死亡协程
    private IEnumerator DeathCoroutine()
    {
        // 播放死亡动画
        if (animator != null)
        {
            animator.SetTrigger("Die");
        }
        
        // 等待击退效果完成
        yield return StartCoroutine(DeathKnockbackCoroutine());
        
        // 播放爆炸特效（特效会自行销毁）
        SpawnExplosionEffect();
        
        // 生成掉落物
        SpawnLootItems();
        
        // 隐藏敌人模型
        HideEnemyModel();
        
        // 立即销毁敌人对象，不需要等待特效完成
        Destroy(gameObject);
    }
    
    // 隐藏敌人模型
    private void HideEnemyModel()
    {
        foreach (var renderer in allRenderers)
        {
            if (renderer != null)
            {
                renderer.enabled = false;
            }
        }
    }
    
    private void Die()
    {
        if (isDead) return;
        
        isDead = true;
        //Debug.Log($"{gameObject.name} 被击败了！");
        
        // 死亡时停止所有隐藏协程
        if (hideCoroutine != null)
        {
            StopCoroutine(hideCoroutine);
            hideCoroutine = null;
        }
        
        // 填满进度条
        if (whiteBar != null) 
        {
            SetBarWidth(whiteBar, maxBarWidth);
        }
        if (redBar != null) 
        {
            SetBarWidth(redBar, maxBarWidth);
        }
        
        // 确保血量条显示
        ShowHealthBar();
        
        if (animator != null)
        {
            animator.SetTrigger("Die");
        }
        
        if (enemyAI != null)
        {
            enemyAI.OnDeath();
        }
        
        Collider collider = GetComponent<Collider>();
        if (collider != null)
            collider.enabled = false;
        
        enabled = false;
        
        // 启动死亡协程
        StartCoroutine(DeathCoroutine());
    }
    
    private void OnDrawGizmosSelected()
    {
        if (hitParticleSpawnPoint != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(hitParticleSpawnPoint.position, 0.1f);
        }
    }
}