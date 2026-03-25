using System.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UI;

public class IngredientInstanceController : MonoBehaviour
{
    [Header("外观设置")]
    public SpriteRenderer spriteRenderer;      // 食材图片
    public Image uiImage;                       // 如果使用UI而不是SpriteRenderer
    public bool useUI = false;                  // 是否使用UI系统
    
    [Header("物理设置")]
    public Rigidbody2D rb;
    public float spawnHeight = 3f;             // 生成高度
    public float randomHorizontalRange = 1.5f; // 水平随机范围
    public float spawnHorizontalRangeFallback = 0.6f; // 当 randomHorizontalRange 太小/为0时使用的兜底范围
    public float initialTorque = 50f;          // 初始扭矩（旋转力度）
    public float gravityScale = 1f;            // 重力大小
    public float destroyDelayAfterHit = 0f;    // 碰撞后销毁延迟
    
    [Header("锅检测")]
    public Pot targetPot;                       // 目标锅
    public float potDistanceThreshold = 0.5f;  // 落入锅中的距离阈值
    
    [Header("状态")]
    public ResourceType ingredientType;        // 食材类型
    public bool hasLandedInPot = false;        // 是否已落入锅中

    [Header("翻炒效果")]
    public float stirVerticalAmplitude = 0.08f;   // 上下跳动幅度（世界/本地单位）
    public float stirVerticalFrequency = 4.5f;    // 上下跳动频率
    public float stirHorizontalAmplitude = 0.12f; // 左右摆动幅度
    public float stirHorizontalNoiseSpeed = 1.8f; // 左右噪声变化速度
    public float stirRotateSpeed = 90f;           // 翻炒时旋转速度（度/秒）
    public float stirEaseInTime = 0.2f;           // 翻炒强度渐入时间（秒）
    public float landedInsidePotHorizontalRange = 0.18f; // 落锅后在锅内的水平散布
    public float destroyDelayAfterCookDone = 0f;  // 烹饪结束后销毁延迟

    private Coroutine stirCoroutine;
    private Vector3 landedWorldPos;
    private bool hasStartedStir = false;
    private float stirSeed;
    private bool cookingEverStarted = false;
    
    // 初始化食材实例
    public void Initialize(ResourceType type, Pot pot, Sprite ingredientSprite)
    {
        ingredientType = type;
        targetPot = pot;
        
        // 设置食材图标
        if (useUI && uiImage != null)
        {
            uiImage.sprite = ingredientSprite;
        }
        else if (spriteRenderer != null)
        {
            spriteRenderer.sprite = ingredientSprite;
        }
        
        // 设置初始位置（在锅上方随机位置）
        Vector3 potPosition = pot.transform.position;
        float range = randomHorizontalRange;
        if (range < 0.001f)
        {
            range = Mathf.Max(0.01f, spawnHorizontalRangeFallback);
        }
        float randomX = UnityEngine.Random.Range(-range, range);
        Vector3 spawnPosition = new Vector3(
            potPosition.x + randomX,
            potPosition.y + spawnHeight,
            -25
        );
        //print(spawnPosition);
        transform.position = spawnPosition;
        //print(transform.position);
        // 设置物理属性
        if (rb != null)
        {
            rb.gravityScale = gravityScale;
            
            // 添加随机旋转扭矩
            float randomTorque = UnityEngine.Random.Range(-initialTorque, initialTorque);
            rb.AddTorque(randomTorque, ForceMode2D.Impulse);
            
        }
        
        // 开始检测是否落入锅中
        StartCoroutine(CheckPotDistance());
    }
    
    // 检测是否落入锅中
    private IEnumerator CheckPotDistance()
    {
        while (!hasLandedInPot)
        {
            if (targetPot == null)
            {
                Destroy(gameObject);
                yield break;
            }
            
            // 计算与锅的距离
            float distance = math.abs(transform.position.y-targetPot.transform.position.y);
            //print(distance);
            if (distance <= potDistanceThreshold)
            {
                // 落入锅中
                hasLandedInPot = true;
                OnLandedInPot();
                break;
            }
            
            // 如果掉出屏幕太远，销毁对象（防止内存泄漏）
            if (transform.position.y < -10f)
            {
                Destroy(gameObject);
                break;
            }
            
            yield return null;
        }
    }
    
    // 落入锅中的处理
    private void OnLandedInPot()
    {
        // 停止物理模拟
        if (rb != null)
        {
            rb.velocity = Vector2.zero;
            rb.angularVelocity = 0f;
            rb.gravityScale = 0f;
            rb.isKinematic = true;
        }

        if (targetPot != null)
        {
            transform.SetParent(targetPot.transform, true);
            // 落锅后在锅内做一点随机散布，避免全都堆在一条线上
            float insideRange = Mathf.Max(0f, landedInsidePotHorizontalRange);
            float insideX = UnityEngine.Random.Range(-insideRange, insideRange);
            landedWorldPos = new Vector3(targetPot.transform.position.x + insideX, transform.position.y, transform.position.z);
            stirSeed = UnityEngine.Random.value * 1000f;

            targetPot.CookingStateChanged -= OnPotCookingStateChanged;
            targetPot.CookingStateChanged += OnPotCookingStateChanged;

            // 需求：每个食材一落锅立刻开始翻滚（不等锅整体进入 isCooking）
            StartStir();
        }
    }

    private void OnPotCookingStateChanged(bool cooking)
    {
        if (cooking)
        {
            cookingEverStarted = true;
            StartStir();
            return;
        }

        // 只在锅确实开始过烹饪后才销毁（防止订阅后锅初始为 false 的情况误处理）
        if (cookingEverStarted)
        {
            StopStir();
            Destroy(gameObject, destroyDelayAfterCookDone);
        }
    }

    private void StartStir()
    {
        if (hasStartedStir) return;
        hasStartedStir = true;

        if (stirCoroutine != null) StopCoroutine(stirCoroutine);
        stirCoroutine = StartCoroutine(StirRoutine());
    }

    private void StopStir()
    {
        if (stirCoroutine != null)
        {
            StopCoroutine(stirCoroutine);
            stirCoroutine = null;
        }
    }

    private IEnumerator StirRoutine()
    {
        // 把落点当作“锅内基准位置”，围绕它做小幅运动
        float startedAt = Time.time;
        while (targetPot != null)
        {
            float t = Time.time;
            float ease = 1f;
            if (stirEaseInTime > 0.0001f)
            {
                ease = Mathf.Clamp01((t - startedAt) / stirEaseInTime);
            }

            float y = Mathf.Abs(Mathf.Sin(t * stirVerticalFrequency + stirSeed)) * (stirVerticalAmplitude * ease);
            float x = (Mathf.PerlinNoise(stirSeed, t * stirHorizontalNoiseSpeed) - 0.5f) * 2f * (stirHorizontalAmplitude * ease);

            // 用世界坐标的 XY 平面偏移，避免父物体旋转导致“看起来在 XZ 平面动”
            transform.position = landedWorldPos + new Vector3(x, y, 0f);
            transform.Rotate(0f, 0f, stirRotateSpeed * Time.deltaTime);

            yield return null;
        }
    }

    private void OnDestroy()
    {
        if (targetPot != null)
        {
            targetPot.CookingStateChanged -= OnPotCookingStateChanged;
        }
    }
    
    // 碰撞检测
    private void OnCollisionEnter2D(Collision2D collision)
    {
        // 如果撞到锅或地面等物体，可以提前停止检测
        if (collision.gameObject.CompareTag("Pot") || collision.gameObject.CompareTag("Ground"))
        {
            if (!hasLandedInPot)
            {
                hasLandedInPot = true;
                OnLandedInPot();
            }
        }
    }
}