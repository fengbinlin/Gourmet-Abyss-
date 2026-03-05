using UnityEngine;

public class BoilingLidEffect : MonoBehaviour
{
    [Header("旋转设置")]
    [Tooltip("Z轴旋转速度（度/秒）")]
    public float rotationSpeed = 15f;
    [Tooltip("旋转幅度（度）")]
    public float rotationAmplitude = 3f;

    [Header("上下浮动设置")]
    [Tooltip("上下浮动速度")]
    public float floatSpeed = 2f;
    [Tooltip("上下浮动幅度")]
    public float floatAmplitude = 0.02f;

    [Header("随机性")]
    [Tooltip("是否启用随机波动")]
    public bool enableRandomness = true;
    [Tooltip("随机波动强度")]
    [Range(0f, 1f)]
    public float randomnessIntensity = 0.3f;

    private Vector3 initialPosition;
    private float randomOffset;

    void Start()
    {
        // 记录初始位置
        initialPosition = transform.position;
        
        // 生成随机偏移量，让每个锅盖运动不同步
        randomOffset = Random.Range(0f, 100f);
    }

    void Update()
    {
        float time = Time.time + randomOffset;
        
        // 计算Z轴旋转（小幅晃动）
        float rotationAngle = Mathf.Sin(time * rotationSpeed * Mathf.Deg2Rad) * rotationAmplitude;
        
        // 计算Y轴位移（上下浮动）
        float yOffset = Mathf.Sin(time * floatSpeed) * floatAmplitude;
        
        // 添加随机波动
        if (enableRandomness)
        {
            float randomVariation = Mathf.PerlinNoise(time * 0.5f, randomOffset) * 2f - 1f;
            rotationAngle += randomVariation * rotationAmplitude * randomnessIntensity;
            yOffset += randomVariation * floatAmplitude * randomnessIntensity * 0.5f;
        }
        
        // 应用旋转（只在Z轴上）
        transform.rotation = Quaternion.Euler(0f, 0f, rotationAngle);
        
        // 应用位置（只在Y轴上移动）
        Vector3 newPosition = initialPosition;
        newPosition.y += yOffset;
        transform.position = newPosition;
    }

    // 在编辑器中可视化运动范围（可选）
    void OnDrawGizmosSelected()
    {
        if (!Application.isPlaying)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube(transform.position, 
                new Vector3(0.5f, floatAmplitude * 2, 0.5f));
        }
    }
}