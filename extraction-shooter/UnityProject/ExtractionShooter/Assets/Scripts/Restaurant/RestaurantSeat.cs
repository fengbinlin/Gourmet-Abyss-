using UnityEngine;

/// <summary>
/// 挂在餐厅座位物体上；激活时向 <see cref="SeatManager"/> 注册自身。
/// </summary>
public class RestaurantSeat : MonoBehaviour
{
    [Header("就坐位置（为空则使用本物体 Transform）")]
    [SerializeField] private Transform sitPoint;

    [Header("金币生成位置（为空则在就坐点旁偏移）")]
    [SerializeField] private Transform coinSpawnPoint;

    [Tooltip("无 coinSpawnPoint 时，相对就坐点的默认偏移")]
    [SerializeField] private Vector3 defaultCoinSpawnOffset = new Vector3(0.6f, 0f, 0f);

    public CustomerNPC Occupant { get; private set; }

    public bool IsAvailable => Occupant == null;

    private void OnEnable()
    {
        TryRegisterToManager();
    }

    private void Start()
    {
        TryRegisterToManager();
    }

    private void TryRegisterToManager()
    {
        if (SeatManager.Instance != null)
            SeatManager.Instance.RegisterSeat(this);
    }

    private void OnDisable()
    {
        if (Occupant != null)
            Release();

        if (SeatManager.Instance != null)
            SeatManager.Instance.UnregisterSeat(this);
    }

    public bool TryAssign(CustomerNPC customer)
    {
        if (!IsAvailable || customer == null)
            return false;

        Occupant = customer;
        return true;
    }

    public void Release()
    {
        Occupant = null;
    }

    public Vector3 GetSitWorldPosition()
    {
        return sitPoint != null ? sitPoint.position : transform.position;
    }

    public Vector3 GetCoinSpawnWorldPosition()
    {
        if (coinSpawnPoint != null)
            return coinSpawnPoint.position;

        return GetSitWorldPosition() + defaultCoinSpawnOffset;
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 sit = GetSitWorldPosition();
        Gizmos.color = IsAvailable ? Color.green : Color.red;
        Gizmos.DrawWireSphere(sit, 0.25f);

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(GetCoinSpawnWorldPosition(), 0.18f);
        Gizmos.DrawLine(sit, GetCoinSpawnWorldPosition());
    }
}
