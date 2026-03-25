using UnityEngine;

/// <summary>
/// 预警圈：实例化在地面，缩放从 startScale 过渡到 endScale，时间到后自动销毁。
/// 预制体可用扁平 Quad / 圆柱 + 半透明材质；本地 Y 轴为法线时，缩放 XZ。
/// </summary>
public class BossWarningZone : MonoBehaviour
{
    [SerializeField] private float duration = 1.2f;
    [SerializeField] private float startScaleXZ = 0.15f;
    [SerializeField] private float endScaleXZ = 1f;
    [SerializeField] private AnimationCurve scaleCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

    private float elapsed;

    public void Configure(float life, float startXz, float endXz)
    {
        duration = life;
        startScaleXZ = startXz;
        endScaleXZ = endXz;
        elapsed = 0f;
    }

    private void Update()
    {
        elapsed += Time.deltaTime;
        float t = duration > 0.01f ? Mathf.Clamp01(elapsed / duration) : 1f;
        float k = scaleCurve.Evaluate(t);
        float s = Mathf.Lerp(startScaleXZ, endScaleXZ, k);
        Vector3 ls = transform.localScale;
        transform.localScale = new Vector3(s, ls.y, s);

        if (t >= 1f)
            Destroy(gameObject);
    }
}
