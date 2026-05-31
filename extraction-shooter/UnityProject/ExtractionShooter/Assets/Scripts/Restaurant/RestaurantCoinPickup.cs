using UnityEngine;

/// <summary>
/// 顾客就餐后留在座位旁的可点击金币；被玩家点击后增加 Money 资源并销毁。
/// </summary>
[RequireComponent(typeof(Collider))]
public class RestaurantCoinPickup : MonoBehaviour
{
    [SerializeField] private int goldAmount = 1;

    public int GoldAmount => goldAmount;

    public void Initialize(int amount)
    {
        goldAmount = Mathf.Max(0, amount);
    }

    /// <summary>由 <see cref="PlayerInteractionController"/> 射线点击调用。</summary>
    public void OnClicked()
    {
        if (goldAmount <= 0)
        {
            Destroy(gameObject);
            return;
        }

        if (GameValManager.Instance != null)
            GameValManager.Instance.AddResource(ResourceType.Money, goldAmount);

        Destroy(gameObject);
    }

    private void Reset()
    {
        Collider col = GetComponent<Collider>();
        if (col != null)
            col.isTrigger = false;
    }
}
