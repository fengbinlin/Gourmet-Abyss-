using UnityEngine;
using System.Collections.Generic;

public class EmissionTransition : MonoBehaviour
{
    [Header("过渡设置")]
    [Tooltip("过渡持续时间（秒）")]
    [SerializeField] private float transitionDuration = 2.0f;

    [Tooltip("起始自发光颜色")]
    [SerializeField] private Color startEmissionColor = Color.black;

    [Tooltip("目标自发光颜色")]
    [SerializeField] private Color targetEmissionColor = Color.white;

    [Tooltip("使用HDR颜色（推荐用于自发光）")]
    [SerializeField] private bool useHDR = true;

    [Tooltip("起始发光强度")]
    [SerializeField] private float startIntensity = 0.0f;

    [Tooltip("目标发光强度")]
    [SerializeField] private float targetIntensity = 3.0f;

    [Header("层级设置")]
    [Tooltip("进入关卡目标层级")]
    [SerializeField] private int enterLevelLayer = 0;
    
    [Tooltip("离开关卡目标层级")]
    [SerializeField] private int exitLevelLayer = 10;

    [Header("渲染器设置")]
    [Tooltip("要控制的渲染器（如MeshRenderer）")]
    [SerializeField] private Renderer targetRenderer;

    [Tooltip("应用到所有材质还是特定材质索引")]
    [SerializeField] private bool affectAllMaterials = true;

    [Tooltip("要控制的材质索引（如果affectAllMaterials为false）")]
    [SerializeField] private int materialIndex = 0;

    [Header("性能优化")]
    [Tooltip("使用MaterialPropertyBlock避免材质实例化")]
    [SerializeField] private bool usePropertyBlock = true;

    [Tooltip("自动启用/禁用全局光照")]
    [SerializeField] private bool manageGlobalIllumination = true;

    // 私有变量
    private List<Material> materials = new List<Material>();
    private MaterialPropertyBlock propertyBlock;
    private Color currentEmissionColor;
    private float currentIntensity;
    private float transitionTimer;
    private bool isTransitioning = false;
    private Color transitionFromColor;
    private Color transitionToColor;
    private float transitionFromIntensity;
    private float transitionToIntensity;
    private int targetLayer; // 当前过渡的目标层级
    private int originalLayer; // 记录原始层级
    private bool isEnterLevel = true; // 标记是否为进入关卡

    // 属性ID缓存（性能优化）
    private static readonly int EmissionColorID = Shader.PropertyToID("_EmissionColor");
    private static readonly int EmissionMapID = Shader.PropertyToID("_EmissionMap");
    private static readonly int EmissionIntensityID = Shader.PropertyToID("_EmissionIntensity");

    void Start()
    {
        // 保存原始层级
        originalLayer = gameObject.layer;
        
        InitializeRenderer();

        if (materials.Count > 0 || propertyBlock != null)
        {
            // 初始设置为发光状态
            //SetToEmit();
            EnterLevelTransition();
        }
    }

    void Update()
    {
        if (!isTransitioning) return;

        // 更新计时器
        transitionTimer += Time.deltaTime;

        // 计算过渡进度（0到1之间）
        float progress = Mathf.Clamp01(transitionTimer / transitionDuration);

        // 更新颜色和强度
        currentEmissionColor = Color.Lerp(transitionFromColor, transitionToColor, progress);
        currentIntensity = Mathf.Lerp(transitionFromIntensity, transitionToIntensity, progress);

        // 应用过渡
        UpdateEmission();

        // 检查过渡是否完成
        if (progress >= 1f)
        {
            isTransitioning = false;
            //Debug.Log($"自发光过渡完成！最终强度: {currentIntensity}");
            
            // 过渡完成后设置目标层级
            SetLayerRecursive(targetLayer);
        }
    }
    
    void SetLayerRecursive(int layerIndex)
    {
        //print("AAA");
        gameObject.layer = layerIndex;

        foreach (Transform child in transform)
        {
            child.gameObject.layer = layerIndex;
        }
    }
    
    /// <summary>
    /// 恢复原始层级
    /// </summary>
    public void RestoreOriginalLayer()
    {
        SetLayerRecursive(originalLayer);
    }
    
    /// <summary>
    /// 设置目标层级
    /// </summary>
    public void SetTargetLayer(int layer)
    {
        targetLayer = layer;
    }
    
    /// <summary>
    /// 初始化渲染器和材质
    /// </summary>
    private void InitializeRenderer()
    {
        // 如果没有指定渲染器，尝试从当前游戏对象获取
        if (targetRenderer == null)
        {
            targetRenderer = GetComponent<Renderer>();
        }

        // 检查渲染器组件
        if (targetRenderer == null)
        {
            Debug.LogError("未找到Renderer组件！");
            return;
        }

        if (usePropertyBlock)
        {
            // 使用MaterialPropertyBlock
            propertyBlock = new MaterialPropertyBlock();
            targetRenderer.GetPropertyBlock(propertyBlock);

            // 检查是否支持自发光
            if (!HasEmissionProperty())
            {
                Debug.LogWarning("渲染器材质可能不支持自发光属性！");
            }
        }
        else
        {
            // 使用材质实例
            if (affectAllMaterials)
            {
                materials = new List<Material>(targetRenderer.materials);
            }
            else
            {
                if (materialIndex < targetRenderer.materials.Length)
                {
                    materials.Add(targetRenderer.materials[materialIndex]);
                }
                else
                {
                    Debug.LogError($"材质索引{materialIndex}超出范围！");
                }
            }

            // 启用自发光
            foreach (var mat in materials)
            {
                if (mat != null)
                {
                    mat.EnableKeyword("_EMISSION");
                }
            }
        }

        // 初始设置
        currentEmissionColor = startEmissionColor;
        currentIntensity = startIntensity;
    }

    /// <summary>
    /// 检查是否支持自发光属性
    /// </summary>
    private bool HasEmissionProperty()
    {
        if (propertyBlock == null) return false;

        // 检查常用自发光属性
        return targetRenderer.sharedMaterial.HasProperty(EmissionColorID) ||
               targetRenderer.sharedMaterial.HasProperty(EmissionIntensityID);
    }

    /// <summary>
    /// 更新自发光属性
    /// </summary>
    private void UpdateEmission()
    {
        if (usePropertyBlock && propertyBlock != null)
        {
            UpdateEmissionWithPropertyBlock();
        }
        else
        {
            UpdateEmissionWithMaterials();
        }
    }

    /// <summary>
    /// 使用PropertyBlock更新自发光
    /// </summary>
    private void UpdateEmissionWithPropertyBlock()
    {
        Color finalColor = useHDR ?
            currentEmissionColor * Mathf.Pow(2, currentIntensity) :
            currentEmissionColor * currentIntensity;

        // 设置自发光颜色
        propertyBlock.SetColor(EmissionColorID, finalColor);

        // 如果有单独的强度属性，也设置它
        if (targetRenderer.sharedMaterial.HasProperty(EmissionIntensityID))
        {
            propertyBlock.SetFloat(EmissionIntensityID, currentIntensity);
        }

        // 应用PropertyBlock
        targetRenderer.SetPropertyBlock(propertyBlock);
    }

    /// <summary>
    /// 使用材质实例更新自发光
    /// </summary>
    private void UpdateEmissionWithMaterials()
    {
        Color finalColor = useHDR ?
            currentEmissionColor * Mathf.Pow(2, currentIntensity) :
            currentEmissionColor * currentIntensity;

        foreach (var mat in materials)
        {
            if (mat != null)
            {
                mat.SetColor(EmissionColorID, finalColor);

                if (mat.HasProperty(EmissionIntensityID))
                {
                    mat.SetFloat(EmissionIntensityID, currentIntensity);
                }

                // 强制更新全局光照
                if (manageGlobalIllumination)
                {
                    mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                }
            }
        }
    }

    /// <summary>
    /// 开始自发光过渡
    /// </summary>
    public void StartTransition()
    {
        StartTransition(startEmissionColor, targetEmissionColor, startIntensity, targetIntensity);
    }
    
    /// <summary>
    /// 开始自定义自发光过渡
    /// </summary>
    public void StartTransition(Color fromColor, Color toColor, float fromIntensity, float toIntensity)
    {
        if (targetRenderer == null || (materials.Count == 0 && propertyBlock == null))
        {
            Debug.LogWarning("无法开始过渡：渲染器未正确初始化");
            return;
        }

        transitionTimer = 0f;
        isTransitioning = true;
        
        // 设置过渡参数
        transitionFromColor = fromColor;
        transitionToColor = toColor;
        transitionFromIntensity = fromIntensity;
        transitionToIntensity = toIntensity;

        // 设置起始值
        currentEmissionColor = fromColor;
        currentIntensity = fromIntensity;
        UpdateEmission();

        //Debug.Log($"开始自发光过渡: 从强度 {fromIntensity} 到 {toIntensity}");
    }
    
    /// <summary>
    /// 进入关卡过渡：从发光变为不发光，层级设为0
    /// </summary>
    public void EnterLevelTransition()
    {
        isEnterLevel = true;
        targetLayer = enterLevelLayer;
        // 从发光状态开始
        currentEmissionColor = startEmissionColor;
        currentIntensity = targetIntensity;
        // 过渡到不发光
        StartTransition(startEmissionColor, targetEmissionColor, targetIntensity, startIntensity);
    }
    
    /// <summary>
    /// 离开关卡过渡：从发光变为不发光，层级设为10
    /// </summary>
    public void ExitLevelTransition()
    {

        isEnterLevel = false;
        targetLayer = exitLevelLayer;
        SetLayerRecursive(10);
        // 从发光状态开始
        currentEmissionColor = targetEmissionColor;
        currentIntensity = targetIntensity;
        // 过渡到不发光
        StartTransition(targetEmissionColor, startEmissionColor, targetIntensity, startIntensity);
    }
    
    /// <summary>
    /// 反向过渡：从当前状态反向过渡
    /// </summary>
    public void ReverseTransition()
    {
        // 交换起始和目标值
        Color tempColor = transitionFromColor;
        float tempIntensity = transitionFromIntensity;
        
        transitionFromColor = transitionToColor;
        transitionFromIntensity = transitionToIntensity;
        transitionToColor = tempColor;
        transitionToIntensity = tempIntensity;
        
        // 切换目标层级
        targetLayer = isEnterLevel ? exitLevelLayer : enterLevelLayer;
        isEnterLevel = !isEnterLevel;
        
        transitionTimer = 0f;
        isTransitioning = true;
        
        Debug.Log($"反向过渡: 从强度 {transitionFromIntensity} 到 {transitionToIntensity}, 层级设为{targetLayer}");
    }
    
    /// <summary>
    /// 切换到发光状态
    /// </summary>
    public void SetToEmit()
    {
        SetEmissionImmediate(targetEmissionColor, targetIntensity);
    }
    
    /// <summary>
    /// 切换到不发光状态
    /// </summary>
    public void SetToUnemit()
    {
        SetEmissionImmediate(startEmissionColor, startIntensity);
    }
    
    /// <summary>
    /// 切换发光状态
    /// </summary>
    public void ToggleEmission()
    {
        if (Mathf.Abs(currentIntensity - targetIntensity) < 0.1f)
        {
            SetToUnemit();
        }
        else
        {
            SetToEmit();
        }
    }

    /// <summary>
    /// 重置过渡到起始状态
    /// </summary>
    public void ResetTransition()
    {
        transitionTimer = 0f;
        isTransitioning = false;

        currentEmissionColor = startEmissionColor;
        currentIntensity = startIntensity;
        UpdateEmission();
        
        // 恢复原始层级
        RestoreOriginalLayer();
    }

    /// <summary>
    /// 立即设置自发光
    /// </summary>
    public void SetEmissionImmediate(Color color, float intensity)
    {
        isTransitioning = false;
        currentEmissionColor = color;
        currentIntensity = intensity;
        UpdateEmission();
    }

    void OnDestroy()
    {
        // 清理材质实例
        if (!usePropertyBlock && materials != null)
        {
            foreach (var mat in materials)
            {
                if (mat != null && !Application.isPlaying)
                {
                    DestroyImmediate(mat);
                }
            }
        }
    }

#if UNITY_EDITOR
    [UnityEditor.MenuItem("GameObject/创建自发光过渡控制器", false, 10)]
    static void CreateEmissionTransitionController()
    {
        GameObject go = new GameObject("EmissionTransitionController");
        go.AddComponent<EmissionTransition>();
        UnityEditor.Selection.activeGameObject = go;
    }
#endif
}