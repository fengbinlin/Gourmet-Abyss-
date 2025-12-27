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
    public void Launch(Transform start, Transform target)
    {
        startTransform = start;
        targetTransform = target;
        transform.position = start.position;
        elapsedTime = 0f;
        isFlying = true;
        SetAppearanceByItemType(itemType);
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

        // 水平位置 & 高度
        Vector3 horizontalPos = Vector3.Lerp(
    startTransform != null ? startTransform.position : startPoint,
    targetTransform != null ? targetTransform.position : targetPoint,
    curvedT
);
        float height = Mathf.Sin(curvedT * Mathf.PI) * maxHeight;
        transform.position = horizontalPos + Vector3.up * height;

        // 朝向
        if (curvedT > 0.05f && curvedT < 0.95f)
        {
            Vector3 moveDirection = (targetPoint - startPoint).normalized;
            if (moveDirection != Vector3.zero)
                transform.forward = moveDirection;
        }
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