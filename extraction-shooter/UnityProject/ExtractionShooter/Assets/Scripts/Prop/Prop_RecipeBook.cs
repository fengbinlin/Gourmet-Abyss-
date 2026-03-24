using UnityEngine;
using System.Collections;

/// <summary>
/// 挂在“食谱书”道具上的脚本：
/// - 通过 dishID 指定要解锁的菜谱
/// - 玩家拾取后，调用 RestaurantPanel 解锁对应菜谱
/// - 解锁成功后销毁自身
/// </summary>
[RequireComponent(typeof(Collider))]
public class Prop_RecipeBook : MonoBehaviour
{
    [Header("要解锁的菜谱 ID")]
    public int dishID;

    [Header("来源宝箱数据（可选）")]
    [Tooltip("记录是哪个 CookBookTreasureData 生成了这本食谱书，用于拾取后回写 Data 的状态")]
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

        print("玩家进入食谱范围");
        // 只响应玩家
        if (!other.CompareTag(playerTag))
        {
            return;
        }

        if (RestaurantPanel.instance == null)
        {
            Debug.LogWarning("RestaurantPanel.instance 未初始化，无法解锁菜谱！");
            return;
        }

        if (dishID < 0)
        {
            Debug.LogWarning("Prop_RecipeBook 的 dishID 未正确设置！");
            return;
        }

        // 解锁菜谱
        RestaurantPanel.instance.UnlockDishByID(dishID);
        hasPickedUp = true;

        // 若有来源宝箱 Data，并且该宝箱配置为只允许开启一次，则在拾取成功后
        // 将 Data 标记为已“完成”（下次生成同 Data 的宝箱时会直接显示为已开启状态，且不可再次开启）。
        if (sourceData != null && sourceData.onlyOpenOnce)
        {
            sourceData.hasBeenOpened = true;
        }

        // 可选：这里也可以播放音效、提示UI等
        // AudioManager.Instance.PlayAudio("解锁菜谱");

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

