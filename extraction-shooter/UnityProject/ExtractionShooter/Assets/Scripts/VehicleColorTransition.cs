using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Renderer))]
public class VehicleColorTransition : MonoBehaviour
{
    [System.Serializable]
    public class MaterialColorSettings
    {
        public Color originalColor = Color.white;
        public Color originalEmissionColor = Color.black;
        public float originalEmissionIntensity = 0f;
        public int materialIndex = 0;
    }

    [Header("颜色过渡设置")]
    [SerializeField] private float transitionDuration = 1.0f;
    [SerializeField] private AnimationCurve transitionCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("材质设置")]
    [SerializeField] private MaterialColorSettings[] materialSettings;

    [Header("白色设置")]
    [SerializeField] private Color whiteColor = Color.white;
    [SerializeField] private Color whiteEmissionColor = Color.white;
    [SerializeField] private float whiteEmissionIntensity = 1f; // 白色时的高发光强度
    
    [Header("层级设置")]
    [SerializeField] private int initialLayer = 10; // 初始层级
    [SerializeField] private int afterTransitionLayer = 11; // 过渡完成后的层级

    private Renderer meshRenderer;
    private Material[] materials;
    private Coroutine transitionCoroutine;
    public bool isInitialized = false;
    private int originalLayer; // 记录原始层级

    private void Awake()
    {
        meshRenderer = GetComponent<Renderer>();
        originalLayer = gameObject.layer; // 保存原始层级
        InitializeMaterials();
        isInitialized = false;
    }

    private void OnEnable()
    {
        // 启用时自动从白色过渡到原色
        if (meshRenderer != null)
        {
            print("VehicleColorTransition OnEnable - 初始化为白色并开始过渡");
            isInitialized = true;
            SetToWhiteImmediate();
            TransitionToOriginal();
        }
    }

    private void Start()
    {
        // 在Start中设置初始层级为10
        SetLayer(initialLayer);
    }

    private void InitializeMaterials()
    {
        if (meshRenderer == null) return;

        // 克隆材质以防止影响原材质
        materials = meshRenderer.materials;

        // 如果没有设置材质配置，自动创建一个
        if (materialSettings == null || materialSettings.Length == 0)
        {
            materialSettings = new MaterialColorSettings[materials.Length];
            for (int i = 0; i < materials.Length; i++)
            {
                Color baseColor = materials[i].color;
                Color emissionColor = materials[i].HasProperty("_EmissionColor") ?
                    materials[i].GetColor("_EmissionColor") : Color.black;

                // 计算发光强度
                float emissionIntensity = 0f;
                if (materials[i].HasProperty("_EmissionIntensity"))
                {
                    emissionIntensity = materials[i].GetFloat("_EmissionIntensity");
                }
                else if (materials[i].HasProperty("_EmissionColor"))
                {
                    // 从颜色中估算强度
                    Color hdrEmission = materials[i].GetColor("_EmissionColor");
                    emissionIntensity = Mathf.Max(hdrEmission.r, hdrEmission.g, hdrEmission.b);
                }

                materialSettings[i] = new MaterialColorSettings
                {
                    originalColor = baseColor,
                    originalEmissionColor = emissionColor,
                    originalEmissionIntensity = emissionIntensity,
                    materialIndex = i
                };
            }
        }

        // 确保材质索引有效
        foreach (var setting in materialSettings)
        {
            if (setting.materialIndex >= materials.Length)
            {
                Debug.LogWarning($"材质索引 {setting.materialIndex} 超出范围，物体: {gameObject.name}");
            }
        }

        isInitialized = true;
    }

    /// <summary>
    /// 从白色过渡到原色（包括自发光从高到0）
    /// </summary>
    public void TransitionToOriginal(float duration = -1)
    {
        print("D3");
        if (!isInitialized) InitializeMaterials();
        if (duration < 0) duration = transitionDuration;

        if (transitionCoroutine != null)
            StopCoroutine(transitionCoroutine);

        print("D2");
        transitionCoroutine = StartCoroutine(TransitionRoutine(true, duration));
    }

    /// <summary>
    /// 从原色过渡到白色（包括自发光从0到高）
    /// </summary>
    public void TransitionToWhite(float duration = -1)
    {
        if (!isInitialized) InitializeMaterials();
        if (duration < 0) duration = transitionDuration;

        if (transitionCoroutine != null)
            StopCoroutine(transitionCoroutine);

        transitionCoroutine = StartCoroutine(TransitionRoutine(false, duration));
    }

    /// <summary>
    /// 立即设置为白色（包括高发光）
    /// </summary>
    public void SetToWhiteImmediate()
    {
        if (!isInitialized) InitializeMaterials();
        if (materials == null || materialSettings == null) return;

        foreach (var setting in materialSettings)
        {
            if (setting.materialIndex < materials.Length)
            {
                var mat = materials[setting.materialIndex];

                // 设置基础颜色为白色
                mat.color = whiteColor;

                // 设置自发光颜色和强度
                if (mat.HasProperty("_EmissionColor"))
                {
                    // 使用HDR颜色
                    Color hdrWhite = whiteEmissionColor * Mathf.Pow(2, whiteEmissionIntensity);
                    mat.SetColor("_EmissionColor", hdrWhite);
                }

                if (mat.HasProperty("_EmissionIntensity"))
                {
                    mat.SetFloat("_EmissionIntensity", whiteEmissionIntensity);
                }

                // 启用自发光关键字
                mat.EnableKeyword("_EMISSION");
            }
        }
    }

    /// <summary>
    /// 立即设置为原色（包括发光为0）
    /// </summary>
    public void SetToOriginalImmediate()
    {
        if (!isInitialized) InitializeMaterials();
        if (materials == null || materialSettings == null) return;

        foreach (var setting in materialSettings)
        {
            if (setting.materialIndex < materials.Length)
            {
                var mat = materials[setting.materialIndex];

                // 设置基础颜色
                mat.color = setting.originalColor;

                // 设置自发光
                if (mat.HasProperty("_EmissionColor"))
                {
                    // 使用HDR颜色
                    Color hdrEmission = setting.originalEmissionColor * Mathf.Pow(2, setting.originalEmissionIntensity);
                    mat.SetColor("_EmissionColor", hdrEmission);
                }

                if (mat.HasProperty("_EmissionIntensity"))
                {
                    mat.SetFloat("_EmissionIntensity", setting.originalEmissionIntensity);
                }
            }
        }
    }
    
    /// <summary>
    /// 设置车辆层级
    /// </summary>
    public void SetLayer(int layer)
    {
        gameObject.layer = layer;
        Debug.Log($"车辆 {gameObject.name} 层级已设置为: {layer}");
    }
    
    /// <summary>
    /// 获取车辆当前层级
    /// </summary>
    public int GetCurrentLayer()
    {
        return gameObject.layer;
    }
    
    /// <summary>
    /// 获取原始层级
    /// </summary>
    public int GetOriginalLayer()
    {
        return originalLayer;
    }

    private IEnumerator TransitionRoutine(bool toOriginal, float duration)
    {
        print("D1");
        if (materials == null || materialSettings == null) yield break;

        float elapsedTime = 0f;

        // 设置起始颜色和目标颜色
        Color[] startColors = new Color[materialSettings.Length];
        Color[] startEmissionColors = new Color[materialSettings.Length];
        float[] startEmissionIntensities = new float[materialSettings.Length];
        Color[] targetColors = new Color[materialSettings.Length];
        Color[] targetEmissionColors = new Color[materialSettings.Length];
        float[] targetEmissionIntensities = new float[materialSettings.Length];

        for (int i = 0; i < materialSettings.Length; i++)
        {
            var setting = materialSettings[i];
            if (setting.materialIndex >= materials.Length) continue;

            var mat = materials[setting.materialIndex];

            // 获取当前颜色
            print("起始颜色");
            startColors[i] = mat.color;

            print(startEmissionColors[i]);
            if (mat.HasProperty("_EmissionColor"))
            {
                startEmissionColors[i] = mat.GetColor("_EmissionColor");
            }
            else
            {
                startEmissionColors[i] = Color.black;
            }

            if (mat.HasProperty("_EmissionIntensity"))
            {
                startEmissionIntensities[i] = mat.GetFloat("_EmissionIntensity");
            }
            else
            {
                startEmissionIntensities[i] = 0f;
            }

            // 设置目标颜色
            if (toOriginal)
            {
                // 目标：原色
                targetColors[i] = setting.originalColor;
                targetEmissionColors[i] = setting.originalEmissionColor;
                targetEmissionIntensities[i] = setting.originalEmissionIntensity;
            }
            else
            {
                // 目标：白色
                targetColors[i] = whiteColor;
                targetEmissionColors[i] = whiteEmissionColor;
                targetEmissionIntensities[i] = whiteEmissionIntensity;
            }
        }

        // 执行过渡
        while (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;
            float t = transitionCurve.Evaluate(elapsedTime / duration);

            for (int i = 0; i < materialSettings.Length; i++)
            {
                var setting = materialSettings[i];
                if (setting.materialIndex >= materials.Length) continue;

                var mat = materials[setting.materialIndex];

                // 过渡基础颜色
                mat.color = Color.Lerp(startColors[i], targetColors[i], t);

                // 过渡自发光
                if (mat.HasProperty("_EmissionColor"))
                {
                    // 过渡颜色
                    Color currentEmissionColor = Color.Lerp(startEmissionColors[i], targetEmissionColors[i], t);

                    // 过渡强度
                    float currentIntensity = Mathf.Lerp(startEmissionIntensities[i], targetEmissionIntensities[i], t);

                    // 应用HDR颜色
                    Color hdrEmission = currentEmissionColor * Mathf.Pow(2, currentIntensity);
                    mat.SetColor("_EmissionColor", hdrEmission);
                }

                if (mat.HasProperty("_EmissionIntensity"))
                {
                    float currentIntensity = Mathf.Lerp(startEmissionIntensities[i], targetEmissionIntensities[i], t);
                    mat.SetFloat("_EmissionIntensity", currentIntensity);
                }
            }

            yield return null;
        }

        // 确保最终颜色准确
        for (int i = 0; i < materialSettings.Length; i++)
        {
            var setting = materialSettings[i];
            if (setting.materialIndex >= materials.Length) continue;

            var mat = materials[setting.materialIndex];
            mat.color = targetColors[i];

            if (mat.HasProperty("_EmissionColor"))
            {
                Color hdrEmission = targetEmissionColors[i] * Mathf.Pow(2, targetEmissionIntensities[i]);
                mat.SetColor("_EmissionColor", hdrEmission);
            }

            if (mat.HasProperty("_EmissionIntensity"))
            {
                mat.SetFloat("_EmissionIntensity", targetEmissionIntensities[i]);
            }
        }
        
        // 如果过渡到原色（从不发光变成发光），设置层级为11
        if (toOriginal)
        {
            SetLayer(afterTransitionLayer);
        }

        transitionCoroutine = null;
    }

    /// <summary>
    /// 停止当前过渡
    /// </summary>
    public void StopTransition()
    {
        if (transitionCoroutine != null)
        {
            StopCoroutine(transitionCoroutine);
            transitionCoroutine = null;
        }
    }

    /// <summary>
    /// 获取是否正在过渡
    /// </summary>
    public bool IsTransitioning()
    {
        return transitionCoroutine != null;
    }

    private void OnValidate()
    {
        if (meshRenderer == null)
            meshRenderer = GetComponent<Renderer>();
    }
}