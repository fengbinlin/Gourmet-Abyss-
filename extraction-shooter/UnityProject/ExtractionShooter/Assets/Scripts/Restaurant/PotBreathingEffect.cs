using UnityEngine;

public class PotBreathingEffect : MonoBehaviour
{
    [Header("呼吸效果设置")]
    [Tooltip("呼吸速度")]
    public float breathSpeed = 1.5f;
    [Tooltip("缩放幅度 (0.1表示±10%的缩放)")]
    [Range(0f, 0.5f)]
    public float breathAmplitude = 0.08f;
    
    [Header("缩放轴向")]
    [Tooltip("是否缩放X轴")]
    public bool scaleX = true;
    [Tooltip("是否缩放Y轴")]
    public bool scaleY = true;
    [Tooltip("是否缩放Z轴")]
    public bool scaleZ = true;
    
    [Header("随机性")]
    [Tooltip("是否启用随机性")]
    public bool enableRandomness = true;
    [Tooltip("随机性强度")]
    [Range(0f, 1f)]
    public float randomnessIntensity = 0.2f;
    
    private Vector3 originalScale;
    private float randomOffset;
    
    void Start()
    {
        // 记录原始缩放
        originalScale = transform.localScale;
        
        // 生成随机偏移，使每个锅的呼吸不同步
        randomOffset = Random.Range(0f, 100f);
    }
    
    void Update()
    {
        float time = Time.time + randomOffset;
        
        // 计算基础呼吸曲线
        float breathCurve = Mathf.Sin(time * breathSpeed) * breathAmplitude;
        
        // 添加随机性
        if (enableRandomness)
        {
            float randomVariation = Mathf.PerlinNoise(time * 0.8f, randomOffset) * 2f - 1f;
            breathCurve += randomVariation * breathAmplitude * randomnessIntensity;
        }
        
        // 计算目标缩放
        float targetScale = 1f + breathCurve;
        
        // 创建目标缩放向量
        Vector3 newScale = new Vector3(
            scaleX ? originalScale.x * targetScale : originalScale.x,
            scaleY ? originalScale.y * targetScale : originalScale.y,
            scaleZ ? originalScale.z * targetScale : originalScale.z
        );
        
        // 应用缩放
        transform.localScale = newScale;
    }
    
    // 重置为原始缩放
    public void ResetScale()
    {
        transform.localScale = originalScale;
    }
    
    // 在编辑器中可视化当前缩放状态
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireCube(transform.position, transform.lossyScale);
    }
}