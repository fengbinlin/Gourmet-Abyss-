using UnityEngine;

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
            Destroy(gameObject);
        }
    }
}

