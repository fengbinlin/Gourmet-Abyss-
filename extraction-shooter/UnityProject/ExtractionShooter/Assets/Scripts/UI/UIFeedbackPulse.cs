using System.Collections;
using UnityEngine;

/// <summary>通用 UI 缩放反馈（不依赖 DOTween，供背包格、排队槽等使用）。</summary>
public static class UIFeedbackPulse
{
    public static IEnumerator CoScalePulse(Transform t, Vector3 baseLocalScale, float peakMul = 1.24f, float totalDuration = 0.3f)
    {
        if (t == null) yield break;
        totalDuration = Mathf.Max(0.04f, totalDuration);
        float half = totalDuration * 0.5f;
        Vector3 peak = baseLocalScale * peakMul;
        float e = 0f;
        while (e < half)
        {
            e += Time.unscaledDeltaTime;
            float u = Mathf.Clamp01(e / half);
            t.localScale = Vector3.LerpUnclamped(baseLocalScale, peak, u);
            yield return null;
        }
        t.localScale = peak;
        e = 0f;
        while (e < half)
        {
            e += Time.unscaledDeltaTime;
            float u = Mathf.Clamp01(e / half);
            t.localScale = Vector3.LerpUnclamped(peak, baseLocalScale, u);
            yield return null;
        }
        t.localScale = baseLocalScale;
    }
}
