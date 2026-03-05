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
    public float initialTorque = 50f;          // 初始扭矩（旋转力度）
    public float gravityScale = 1f;            // 重力大小
    public float destroyDelayAfterHit = 0f;    // 碰撞后销毁延迟
    
    [Header("锅检测")]
    public Pot targetPot;                       // 目标锅
    public float potDistanceThreshold = 0.5f;  // 落入锅中的距离阈值
    
    [Header("状态")]
    public ResourceType ingredientType;        // 食材类型
    public bool hasLandedInPot = false;        // 是否已落入锅中
    
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
        float randomX = UnityEngine.Random.Range(-randomHorizontalRange, randomHorizontalRange);
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
        
        // 播放落入锅中的效果
        StartCoroutine(LandingEffect());
        
        // 通知锅有食材落入（如果需要）
        // targetPot.OnIngredientLanded(ingredientType);
    }
    
    // 落入效果
    private IEnumerator LandingEffect()
    {

        // 销毁对象
        Destroy(gameObject, destroyDelayAfterHit);
        yield return null;
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