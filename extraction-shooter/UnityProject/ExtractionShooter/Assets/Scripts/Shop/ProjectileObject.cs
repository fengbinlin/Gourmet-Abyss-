using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public class ResourceTextureMapping
{
    public ResourceType type;
    public Texture texture;
}

public class ProjectileObject : MonoBehaviour
{
    [Header("飞行参数")]
    public Vector3 targetPoint;
    public float flightDuration = 2f;
    public float maxHeight = 5f;

    [Header("轨迹随机变化")]
    [SerializeField] private float horizontalRandomRange = 2f; // 水平随机偏移范围
    [SerializeField] private float verticalRandomRange = 1f;   // 垂直随机偏移范围
    [SerializeField] private float pathCurveRandomness = 0.5f; // 路径曲线随机性
    [SerializeField] private float rotationRandomness = 30f;   // 旋转随机性

    [Header("飞行进度曲线")]
    public AnimationCurve speedCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("资源类型 - 贴图映射")]
    public List<ResourceTextureMapping> textureMappings;

    [Header("粒子系统渲染器（手动赋值）")]
    public ParticleSystemRenderer psRenderer;

    // 物品数据 & 回调
    public ResourceType itemType;
    public int amount;
    public System.Action onArrive;

    private Vector3 startPoint;
    private float elapsedTime = 0f;
    private bool isFlying = false;
    public Transform startTransform;
    public Transform targetTransform;
    
    // 轨迹随机参数
    private Vector3 horizontalOffset = Vector3.zero;
    private Vector3 verticalOffset = Vector3.zero;
    private float curveFrequency = 1f;
    private float curvePhase = 0f;
    private Vector3 rotationAxis = Vector3.up;
    private float rotationSpeed = 0f;
    
    // 用于计算中间控制点的贝塞尔曲线参数
    private Vector3 controlPoint1 = Vector3.zero;
    private Vector3 controlPoint2 = Vector3.zero;
    
    public void Launch(Transform start, Transform target)
    {
        startTransform = start;
        targetTransform = target;
        transform.position = start.position;
        elapsedTime = 0f;
        isFlying = true;
        
        // 初始化随机轨迹参数
        InitializeRandomTrajectory();
        
        SetAppearanceByItemType(itemType);
    }

    private void InitializeRandomTrajectory()
    {
        Vector3 direction = (targetTransform.position - startTransform.position).normalized;
        Vector3 horizontalDirection = Vector3.Cross(direction, Vector3.up).normalized;
        
        // 生成随机水平偏移
        float randomHorizontalOffset = Random.Range(-horizontalRandomRange, horizontalRandomRange);
        horizontalOffset = horizontalDirection * randomHorizontalOffset;
        
        // 生成随机垂直偏移
        float randomVerticalOffset = Random.Range(-verticalRandomRange, verticalRandomRange);
        verticalOffset = Vector3.up * randomVerticalOffset;
        
        // 生成随机曲线参数
        curveFrequency = Random.Range(0.5f, 2f);
        curvePhase = Random.Range(0f, Mathf.PI * 2f);
        
        // 生成随机旋转参数
        rotationAxis = Random.onUnitSphere;
        rotationSpeed = Random.Range(-rotationRandomness, rotationRandomness);
        
        // 计算贝塞尔曲线的控制点
        Vector3 midpoint = Vector3.Lerp(startTransform.position, targetTransform.position, 0.5f);
        
        // 第一个控制点：向上偏移并添加随机性
        controlPoint1 = midpoint + Vector3.up * maxHeight + 
                       horizontalDirection * Random.Range(-pathCurveRandomness, pathCurveRandomness) + 
                       Vector3.Cross(horizontalDirection, Vector3.up) * Random.Range(-pathCurveRandomness, pathCurveRandomness);
        
        // 第二个控制点：稍微偏向目标，增加曲线变化
        float randomT = Random.Range(0.3f, 0.7f);
        controlPoint2 = Vector3.Lerp(midpoint, targetTransform.position, randomT) + 
                       Vector3.up * (maxHeight * 0.7f) + 
                       -horizontalDirection * Random.Range(-pathCurveRandomness, pathCurveRandomness);
    }

    private void Update()
    {
        if (!isFlying) return;

        elapsedTime += Time.deltaTime;
        float t = elapsedTime / flightDuration;

        if (t >= 1f)
        {
            isFlying = false;
            onArrive?.Invoke();
            FlyObjectPool.Instance.ReturnObject(gameObject);
            return;
        }

        // 应用速度曲线
        float curvedT = speedCurve != null ? speedCurve.Evaluate(t) : t;

        // 方法1：简单的水平偏移
        Vector3 baseHorizontalPos = Vector3.Lerp(
            startTransform != null ? startTransform.position : startPoint,
            targetTransform != null ? targetTransform.position : targetPoint,
            curvedT
        );
        
        // 添加正弦波水平偏移
        float horizontalCurve = Mathf.Sin(curvedT * Mathf.PI * curveFrequency + curvePhase) * horizontalRandomRange;
        Vector3 curvedHorizontalOffset = horizontalOffset * horizontalCurve;
        
        // 计算高度
        float baseHeight = Mathf.Sin(curvedT * Mathf.PI) * maxHeight;
        
        // 添加垂直波动
        float verticalCurve = Mathf.Sin(curvedT * Mathf.PI * 2f + curvePhase) * verticalRandomRange;
        float curvedHeight = baseHeight + verticalCurve;
        
        // 方法2：使用贝塞尔曲线计算最终位置（更平滑的曲线）
        Vector3 bezierPosition = CalculateCubicBezierPoint(
            curvedT,
            startTransform != null ? startTransform.position : startPoint,
            controlPoint1,
            controlPoint2,
            targetTransform != null ? targetTransform.position : targetPoint
        );
        
        // 混合两种轨迹，产生更自然的效果
        Vector3 finalPosition = Vector3.Lerp(baseHorizontalPos, bezierPosition, 0.3f) + 
                               curvedHorizontalOffset + 
                               Vector3.up * curvedHeight;
        
        transform.position = finalPosition;

        // 随机旋转效果
        if (rotationSpeed != 0)
        {
            transform.Rotate(rotationAxis, rotationSpeed * Time.deltaTime, Space.World);
        }
        
        // 面向移动方向，但添加一些随机性
        if (curvedT > 0.05f && curvedT < 0.95f)
        {
            Vector3 moveDirection = (finalPosition - transform.position).normalized;
            if (moveDirection != Vector3.zero)
            {
                // 使用Slerp使旋转更平滑
                Quaternion targetRotation = Quaternion.LookRotation(moveDirection);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * 5f);
            }
        }
    }

    // 计算三次贝塞尔曲线点
    private Vector3 CalculateCubicBezierPoint(float t, Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3)
    {
        float u = 1 - t;
        float tt = t * t;
        float uu = u * u;
        float uuu = uu * u;
        float ttt = tt * t;
        
        Vector3 p = uuu * p0; // (1-t)^3 * p0
        p += 3 * uu * t * p1; // 3(1-t)^2 * t * p1
        p += 3 * u * tt * p2; // 3(1-t) * t^2 * p2
        p += ttt * p3;        // t^3 * p3
        
        return p;
    }

    private void SetAppearanceByItemType(ResourceType type)
    {
        if (psRenderer != null && psRenderer.material != null)
        {
            Texture tex = GetTextureByItemType(type);
            if (tex != null)
            {
                psRenderer.material.mainTexture = tex;
            }
        }
    }

    private Texture GetTextureByItemType(ResourceType type)
    {
        foreach (var mapping in textureMappings)
        {
            if (mapping.type == type)
                return mapping.texture;
        }
        return null;
    }
}