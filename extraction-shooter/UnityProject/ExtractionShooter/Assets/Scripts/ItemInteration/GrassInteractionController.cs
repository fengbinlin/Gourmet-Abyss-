using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(Renderer))]
public class GrassInteractionController : MonoBehaviour
{
    [System.Serializable]
    public class InteractionPoint
    {
        public Vector3 localPosition;
        public float currentRadius = 1.5f;
        public float currentStrength = 0f;
        public float targetStrength = 0f;
        public float time = 0f;
        public float speed = 5f;
        
        public void Update()
        {
            // 平滑过渡到目标强度
            currentStrength = Mathf.Lerp(currentStrength, targetStrength, Time.deltaTime * speed);
            
            // 逐渐减小目标强度
            targetStrength = Mathf.Lerp(targetStrength, 0f, Time.deltaTime * 2f);
            
            time += Time.deltaTime;
        }
        
        public bool IsActive => currentStrength > 0.01f || targetStrength > 0.01f;
    }

    [Header("交互设置")]
    [SerializeField] private int maxInteractions = 8;
    [SerializeField] private float baseInteractionRadius = 1.5f;
    [SerializeField] private float baseInteractionStrength = 0.8f;
    
    [Header("弹性设置")]
    [SerializeField] private float bounceBackSpeed = 4f;
    [SerializeField] private float bounceOvershoot = 0.2f;
    
    [Header("风动设置")]
    [SerializeField] private float windStrength = 0.15f;
    [SerializeField] private float windSpeed = 1.5f;
    [SerializeField] private float windFrequency = 0.5f;
    [SerializeField] private Vector3 windDirection = new Vector3(1, 0, 0.5f);
    
    [Header("调试")]
    [SerializeField] private bool showDebugGizmos = false;

    private Renderer grassRenderer;
    private MaterialPropertyBlock propertyBlock;
    private List<InteractionPoint> interactionPoints = new List<InteractionPoint>();
    private Vector4[] shaderInteractionData = new Vector4[8];
    private float windTime = 0f;
    
    // 着色器属性ID
    private static readonly int InteractionDataArrayID = Shader.PropertyToID("_InteractionDataArray");
    private static readonly int InteractionCountID = Shader.PropertyToID("_InteractionCount");
    private static readonly int WindParamsID = Shader.PropertyToID("_WindParams");
    private static readonly int WindDirectionID = Shader.PropertyToID("_WindDirection");

    private void Start()
    {
        grassRenderer = GetComponent<Renderer>();
        propertyBlock = new MaterialPropertyBlock();
        
        // 初始化数组
        for (int i = 0; i < 8; i++)
        {
            shaderInteractionData[i] = Vector4.zero;
        }
        
        UpdateMaterialProperties();
    }

    private void Update()
    {
        // 更新风动时间
        windTime += Time.deltaTime;
        
        // 更新所有交互点
        UpdateInteractionPoints();
        
        // 更新材质属性
        UpdateMaterialProperties();
    }
    
    private void UpdateInteractionPoints()
    {
        for (int i = interactionPoints.Count - 1; i >= 0; i--)
        {
            var point = interactionPoints[i];
            point.Update();
            
            if (point.IsActive)
            {
                // 添加弹性回弹
                if (point.currentStrength > 0.5f)
                {
                    float bounce = Mathf.Sin(point.time * 8f) * bounceOvershoot * point.currentStrength;
                    shaderInteractionData[i] = new Vector4(
                        point.localPosition.x,
                        point.localPosition.y,
                        point.localPosition.z,
                        point.currentStrength + bounce
                    );
                }
                else
                {
                    shaderInteractionData[i] = new Vector4(
                        point.localPosition.x,
                        point.localPosition.y,
                        point.localPosition.z,
                        point.currentStrength
                    );
                }
            }
            else
            {
                // 移除不活跃的点
                interactionPoints.RemoveAt(i);
                shaderInteractionData[i] = Vector4.zero;
            }
        }
    }
    
    private void UpdateMaterialProperties()
    {
        grassRenderer.GetPropertyBlock(propertyBlock);
        
        // 设置交互数据
        propertyBlock.SetVectorArray(InteractionDataArrayID, shaderInteractionData);
        propertyBlock.SetInt(InteractionCountID, interactionPoints.Count);
        
        // 设置风参数
        Vector4 windParams = new Vector4(windStrength, windSpeed, windFrequency, windTime);
        propertyBlock.SetVector(WindParamsID, windParams);
        
        // 设置风向
        propertyBlock.SetVector(WindDirectionID, windDirection.normalized);
        
        grassRenderer.SetPropertyBlock(propertyBlock);
    }
    
    public void AddInteraction(Vector3 worldPosition, float strengthMultiplier = 1f, float radiusMultiplier = 1f)
    {
        // 转换到草的局部空间
        Vector3 localPos = transform.InverseTransformPoint(worldPosition);
        
        // 创建新的交互点
        InteractionPoint newPoint = new InteractionPoint
        {
            localPosition = localPos,
            currentRadius = baseInteractionRadius * radiusMultiplier,
            currentStrength = 0f,
            targetStrength = baseInteractionStrength * strengthMultiplier,
            time = 0f,
            speed = bounceBackSpeed
        };
        
        // 检查是否与现有点太近，如果是则合并
        bool merged = false;
        for (int i = 0; i < interactionPoints.Count; i++)
        {
            if (Vector3.Distance(interactionPoints[i].localPosition, localPos) < 0.5f)
            {
                interactionPoints[i].targetStrength = Mathf.Max(
                    interactionPoints[i].targetStrength, 
                    newPoint.targetStrength
                );
                merged = true;
                break;
            }
        }
        
        if (!merged)
        {
            if (interactionPoints.Count >= maxInteractions)
            {
                interactionPoints.RemoveAt(0);
            }
            interactionPoints.Add(newPoint);
        }
    }
    
    public void SetWindParameters(float strength, float speed, float frequency, Vector3 direction)
    {
        windStrength = strength;
        windSpeed = speed;
        windFrequency = frequency;
        windDirection = direction.normalized;
    }
    
    private void OnDrawGizmos()
    {
        if (!showDebugGizmos || !Application.isPlaying) return;
        
        Gizmos.color = Color.green;
        Gizmos.matrix = transform.localToWorldMatrix;
        
        foreach (var point in interactionPoints)
        {
            float radius = Mathf.Lerp(0.1f, 0.5f, point.currentStrength);
            Gizmos.DrawWireSphere(point.localPosition, radius);
        }
    }
}