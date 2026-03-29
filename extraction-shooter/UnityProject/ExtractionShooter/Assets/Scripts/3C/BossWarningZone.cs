using UnityEngine;

/// <summary>
/// 预警圈：实例化在地面，保持预制体上的 Transform 缩放；仅控制持续时间到后自动销毁。
/// </summary>
public class BossWarningZone : MonoBehaviour
{
    [SerializeField] private float duration = 1.2f;

    private float elapsed;

    public void Configure(float life)
    {
        duration = life;
        elapsed = 0f;
    }

    /// <summary>
    /// <paramref name="startXz"/>、<paramref name="endXz"/> 保留与旧调用兼容，已忽略（缩放完全由预制体决定）。
    /// </summary>
    public void Configure(float life, float startXz, float endXz)
    {
        Configure(life);
    }

    private void Update()
    {
        elapsed += Time.deltaTime;
        if (elapsed >= duration)
            Destroy(gameObject);
    }
}
