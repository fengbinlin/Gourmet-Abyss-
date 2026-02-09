using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[ExecuteAlways]
public class InstancedIndirectGrassPosDefine : MonoBehaviour
{
    [Range(1, 40000000)]
    public int instanceCount = 1000000;
    public float drawDistance = 125;

    [Header("Noise Settings")]
    [Range(0.1f, 10f)]
    public float noiseScale = 1f;  // 控制噪声大小
    [Range(0f, 1f)]
    public float noiseThreshold = 0.3f;  // 采样阈值

    // 三层噪声图的偏移
    private Vector2[] noiseOffsets = new Vector2[3];
    private int cacheCount = -1;
    private float cachedNoiseScale = -1f;
    private float cachedNoiseThreshold = -1f;

    // Start is called before the first frame update
    void Start()
    {
        InitializeNoiseOffsets();
        cacheCount = -1; // 强制刷新
        //UpdatePosIfNeeded();

        // if (InstancedIndirectGrassRenderer.instance != null)
        //     InstancedIndirectGrassRenderer.instance.UpdateAllInstanceTransformBufferIfNeeded();
        Invoke("UpdatePosIfNeeded",0.1f);
    }

    private void Update()
    {
        //UpdatePosIfNeeded();
    }

    private void OnGUI()
    {
        GUI.Label(new Rect(300, 50, 200, 30), "Instance Count: " + instanceCount / 1000000 + "Million");
        instanceCount = Mathf.Max(0, (int)(GUI.HorizontalSlider(new Rect(300, 100, 200, 30), instanceCount / 1000000f, 1, 10))) * 1000000;

        GUI.Label(new Rect(300, 150, 200, 30), "Draw Distance: " + drawDistance);
        drawDistance = Mathf.Max(1, (int)(GUI.HorizontalSlider(new Rect(300, 200, 200, 30), drawDistance / 25f, 1, 8)) * 25);

        GUI.Label(new Rect(300, 250, 200, 30), "Noise Scale: " + noiseScale.ToString("F2"));
        noiseScale = GUI.HorizontalSlider(new Rect(300, 300, 200, 30), noiseScale, 0.1f, 10f);

        GUI.Label(new Rect(300, 350, 200, 30), "Noise Threshold: " + noiseThreshold.ToString("F2"));
        noiseThreshold = GUI.HorizontalSlider(new Rect(300, 400, 200, 30), noiseThreshold, 0f, 1f);
        if (InstancedIndirectGrassRenderer.instance)
            InstancedIndirectGrassRenderer.instance.drawDistance = drawDistance;
    }

    private void InitializeNoiseOffsets()
    {
        // 使用固定的随机种子初始化噪声偏移
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
        // 检查参数是否有变化
        bool parametersChanged =
            instanceCount != cacheCount ||
            !Mathf.Approximately(noiseScale, cachedNoiseScale) ||
            !Mathf.Approximately(noiseThreshold, cachedNoiseThreshold);

        if (!parametersChanged)
            return;

        Debug.Log("UpdatePos (Slow)");

        // 更新缓存
        cacheCount = instanceCount;
        cachedNoiseScale = noiseScale;
        cachedNoiseThreshold = noiseThreshold;

        // 使用相同种子以保证视觉效果一致
        UnityEngine.Random.InitState(123);

        // 自动保持密度
        float scale = Mathf.Sqrt((instanceCount / 16)) / 2f;
        transform.localScale = new Vector3(scale, transform.localScale.y, scale);

        List<Vector3> positions = new List<Vector3>();
        int attempts = 0;

        // 尝试instanceCount次
        while (positions.Count < instanceCount && attempts < instanceCount * 2)
        {
            // 生成随机位置
            Vector3 pos = Vector3.zero;
            pos.x = UnityEngine.Random.Range(-1f, 1f) * transform.lossyScale.x;
            pos.z = UnityEngine.Random.Range(-1f, 1f) * transform.lossyScale.z;

            // 转换为世界坐标
            pos += transform.position;

            positions.Add(new Vector3(pos.x, pos.y, pos.z));

            attempts++;
        }

        // 如果尝试次数用完但还没达到目标数量，记录警告
        if (positions.Count < instanceCount)
        {
            Debug.LogWarning($"Only generated {positions.Count} positions out of {instanceCount} after {attempts} attempts. Try lowering the noise threshold.");
        }
        if (InstancedIndirectGrassRenderer.instance)
        {
            print("AAA");
            // 将所有位置发送到渲染器
            InstancedIndirectGrassRenderer.instance.allGrassPos = positions;
        }
        else
        {
            print("BBBB");
        }

    }

    // 在给定位置采样三层噪声
    private float SampleNoiseAtPosition(Vector3 worldPos)
    {
        float noiseValue = 0f;

        // 计算三层噪声
        for (int i = 0; i < 3; i++)
        {
            // 使用不同的缩放和偏移计算噪声
            float layerScale = noiseScale * (i + 1) * 0.5f;  // 每层使用不同的缩放
            Vector2 noisePos = new Vector2(
                (worldPos.x + noiseOffsets[i].x) * layerScale * 0.1f,  // 调整缩放因子
                (worldPos.z + noiseOffsets[i].y) * layerScale * 0.1f
            );

            // 采样Perlin噪声
            float layerNoise = Mathf.PerlinNoise(noisePos.x, noisePos.y);

            // 根据层数加权（可以调整权重以获得不同效果）
            float weight = 1f / (i + 1);
            noiseValue += layerNoise * weight;
        }

        // 归一化到0-1范围
        noiseValue /= (1f + 0.5f + 0.333f);  // 1 + 1/2 + 1/3

        return noiseValue;
    }
}