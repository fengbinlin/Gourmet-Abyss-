using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[ExecuteAlways]
public class InstancedIndirectGrassPosDefine : MonoBehaviour
{
    [Header("生成范围")]
    public Vector2 areaSize = new Vector2(100f, 100f); // 区域大小（X,Z方向）
    [Header("草密度设置")]
    [Range(0.1f, 100f)]
    public float grassDensity = 10f; // 每单位面积的草的数量

    public float drawDistance = 125;

    [Header("Noise Settings")]
    [Range(0.1f, 10f)]
    public float noiseScale = 1f;  // 控制噪声大小
    [Range(0f, 1f)]
    public float noiseThreshold = 0.3f;  // 采样阈值

    // 三层噪声图的偏移
    private Vector2[] noiseOffsets = new Vector2[3];
    private float cachedNoiseScale = -1f;
    private float cachedNoiseThreshold = -1f;
    private Vector2 cachedAreaSize = Vector2.zero;
    private float cachedGrassDensity = -1f;

    void Start()
    {
        InitializeNoiseOffsets();
        Invoke("UpdatePosIfNeeded", 0.1f);
    }

    private void OnGUI()
    {
        // GUI.Label(new Rect(300, 50, 200, 30), $"Area Size: {areaSize.x} x {areaSize.y}");
        // areaSize.x = GUI.HorizontalSlider(new Rect(300, 80, 200, 30), areaSize.x, 10, 500);
        // areaSize.y = GUI.HorizontalSlider(new Rect(300, 110, 200, 30), areaSize.y, 10, 500);

        // GUI.Label(new Rect(300, 150, 200, 30), $"Grass Density: {grassDensity}");
        // grassDensity = GUI.HorizontalSlider(new Rect(300, 180, 200, 30), grassDensity, 0.1f, 100f);

        // GUI.Label(new Rect(300, 210, 200, 30), $"Draw Distance: {drawDistance}");
        // drawDistance = Mathf.Max(1, (int)(GUI.HorizontalSlider(new Rect(300, 240, 200, 30), drawDistance / 25f, 1, 8)) * 25);

        // GUI.Label(new Rect(300, 270, 200, 30), $"Noise Scale: {noiseScale:F2}");
        // noiseScale = GUI.HorizontalSlider(new Rect(300, 300, 200, 30), noiseScale, 0.1f, 10f);

        // GUI.Label(new Rect(300, 330, 200, 30), $"Noise Threshold: {noiseThreshold:F2}");
        // noiseThreshold = GUI.HorizontalSlider(new Rect(300, 360, 200, 30), noiseThreshold, 0f, 1f);

        if (InstancedIndirectGrassRenderer.instance)
            InstancedIndirectGrassRenderer.instance.drawDistance = drawDistance;
    }

    private void InitializeNoiseOffsets()
    {
        UnityEngine.Random.InitState(123);
        for (int i = 0; i < 3; i++)
        {
            noiseOffsets[i] = new Vector2(
                UnityEngine.Random.Range(-100f, 100f),
                UnityEngine.Random.Range(-100f, 100f)
            );
        }
    }

    private void UpdatePosIfNeeded()
    {
        bool parametersChanged =
            !Mathf.Approximately(noiseScale, cachedNoiseScale) ||
            !Mathf.Approximately(noiseThreshold, cachedNoiseThreshold) ||
            areaSize != cachedAreaSize ||
            !Mathf.Approximately(grassDensity, cachedGrassDensity);

        if (!parametersChanged)
            return;

        Debug.Log("Update Grass Positions (based on area & density)");

        cachedNoiseScale = noiseScale;
        cachedNoiseThreshold = noiseThreshold;
        cachedAreaSize = areaSize;
        cachedGrassDensity = grassDensity;

        // 根据范围和密度计算目标总数
        float area = areaSize.x * areaSize.y;
        int targetCount = Mathf.CeilToInt(area * grassDensity);

        List<Vector3> positions = new List<Vector3>();
        int attempts = 0;
        int maxAttempts = targetCount * 2;

        UnityEngine.Random.InitState(123);

        while (positions.Count < targetCount && attempts < maxAttempts)
        {
            Vector3 pos = Vector3.zero;
            pos.x = UnityEngine.Random.Range(-areaSize.x / 2f, areaSize.x / 2f);
            pos.z = UnityEngine.Random.Range(-areaSize.y / 2f, areaSize.y / 2f);
            pos += transform.position;

            float noiseVal = SampleNoiseAtPosition(pos);
            if (noiseVal >= noiseThreshold)
                positions.Add(new Vector3(pos.x, pos.y, pos.z));

            attempts++;
        }

        if (positions.Count < targetCount)
        {
            Debug.LogWarning($"Only generated {positions.Count} positions out of {targetCount}. Try lowering the noise threshold or increasing density.");
        }

        if (InstancedIndirectGrassRenderer.instance)
            InstancedIndirectGrassRenderer.instance.allGrassPos = positions;
    }

    private float SampleNoiseAtPosition(Vector3 worldPos)
    {
        float noiseValue = 0f;
        for (int i = 0; i < 3; i++)
        {
            float layerScale = noiseScale * (i + 1) * 0.5f;
            Vector2 noisePos = new Vector2(
                (worldPos.x + noiseOffsets[i].x) * layerScale * 0.1f,
                (worldPos.z + noiseOffsets[i].y) * layerScale * 0.1f
            );

            float layerNoise = Mathf.PerlinNoise(noisePos.x, noisePos.y);
            float weight = 1f / (i + 1);
            noiseValue += layerNoise * weight;
        }
        noiseValue /= (1f + 0.5f + 0.333f);
        return noiseValue;
    }
}