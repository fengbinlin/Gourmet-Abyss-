using UnityEngine;
using System.Collections;

/// <summary>
/// 挂在“资源道具”上的脚本：
/// - 通过 resourceType / addAmount 指定要增加的资源
/// - 玩家拾取后，调用 GameValManager 增加对应资源
/// - 可选销毁自身，并保留来源宝箱 Data 回写逻辑
/// </summary>
[RequireComponent(typeof(Collider))]
public class Prop_AddResource : MonoBehaviour
{
    [Header("资源奖励配置")]
    [Tooltip("拾取后要增加的资源类型")]
    public ResourceType resourceType = ResourceType.Money;

    [Tooltip("拾取后要增加的资源数量")]
    [Min(1)]
    public int addAmount = 1;

    [Header("来源宝箱数据（可选）")]
    [Tooltip("记录是哪个 CookBookTreasureData 生成了该道具，用于拾取后回写 Data 的状态")]
    public CookBookTreasureData sourceData;

    [Header("拾取设置")]
    [Tooltip("玩家的 Tag，用于触发拾取")]
    public string playerTag = "Player";

    [Tooltip("拾取后是否立刻销毁该道具")]
    public bool destroyOnPickup = true;

    [Header("拾取后销毁动效")]
    [Tooltip("销毁前放大到的倍数（相对初始缩放）")]
    [Min(1f)]
    public float pickupPulseScale = 1.25f;

    [Tooltip("一次放大+缩小动画总时长（秒）")]
    [Min(0.01f)]
    public float pickupPulseDuration = 0.2f;

    private bool hasPickedUp = false;

    private void Reset()
    {
        // 确保碰撞体为触发器，这样玩家可以穿过并触发拾取
        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            col.isTrigger = true;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (hasPickedUp)
        {
            return;
        }

        // 只响应玩家
        if (!other.CompareTag(playerTag))
        {
            return;
        }

        if (GameValManager.Instance == null)
        {
            Debug.LogWarning("GameValManager.Instance 未初始化，无法增加资源！");
            return;
        }

        if (resourceType == ResourceType.None)
        {
            Debug.LogWarning("Prop_AddResource 的 resourceType 未正确设置！");
            return;
        }

        if (addAmount <= 0)
        {
            Debug.LogWarning("Prop_AddResource 的 addAmount 必须大于 0！");
            return;
        }

        // 增加资源
        GameValManager.Instance.AddResource(resourceType, addAmount);
        hasPickedUp = true;

        // 若有来源宝箱 Data，并且该宝箱配置为只允许开启一次，则在拾取成功后回写状态
        if (sourceData != null && sourceData.onlyOpenOnce)
        {
            sourceData.hasBeenOpened = true;
        }

        if (destroyOnPickup)
        {
            Collider col = GetComponent<Collider>();
            if (col != null)
            {
                col.enabled = false;
            }

            StartCoroutine(PlayPickupPulseAndDestroy());
        }
    }

    private IEnumerator PlayPickupPulseAndDestroy()
    {
        Vector3 originScale = transform.localScale;
        Vector3 peakScale = originScale * pickupPulseScale;
        float halfDuration = pickupPulseDuration * 0.5f;

        float t = 0f;
        while (t < halfDuration)
        {
            t += Time.deltaTime;
            float lerp = Mathf.Clamp01(t / halfDuration);
            transform.localScale = Vector3.Lerp(originScale, peakScale, lerp);
            yield return null;
        }

        t = 0f;
        while (t < halfDuration)
        {
            t += Time.deltaTime;
            float lerp = Mathf.Clamp01(t / halfDuration);
            transform.localScale = Vector3.Lerp(peakScale, originScale, lerp);
            yield return null;
        }

        transform.localScale = originScale;
        Destroy(gameObject);
    }
}

