using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 管理场景中所有 <see cref="RestaurantSeat"/> 的可用与占用状态。
/// </summary>
[DefaultExecutionOrder(-100)]
public class SeatManager : MonoBehaviour
{
    public static SeatManager Instance { get; private set; }

    [SerializeField] private bool logSeatCountOnStart;

    private readonly List<RestaurantSeat> _seats = new List<RestaurantSeat>();

    public int TotalSeatCount => _seats.Count;

    public int OccupiedSeatCount
    {
        get
        {
            int count = 0;
            for (int i = 0; i < _seats.Count; i++)
            {
                if (_seats[i] != null && !_seats[i].IsAvailable)
                    count++;
            }
            return count;
        }
    }

    public int AvailableSeatCount => Mathf.Max(0, TotalSeatCount - OccupiedSeatCount);

    public bool HasAvailableSeat => AvailableSeatCount > 0;

    private void Awake()
    {
        if (Instance == null)
            Instance = this;
        else if (Instance != this)
            Debug.LogWarning("[SeatManager] 场景中存在多个 SeatManager，保留首个实例。");
    }

    private void Start()
    {
        RefreshRegisteredSeats();
        if (logSeatCountOnStart || _seats.Count == 0)
            Debug.Log($"[SeatManager] 已注册座位数: {_seats.Count}");
        if (_seats.Count == 0)
            Debug.LogWarning("[SeatManager] 场景中没有可用座位！请确认座位物体已挂 RestaurantSeat 且处于激活状态。");
    }

    /// <summary>补注册：解决座位 OnEnable 早于 SeatManager.Awake 导致漏注册的问题。</summary>
    public void RefreshRegisteredSeats()
    {
        RestaurantSeat[] found = FindObjectsOfType<RestaurantSeat>(true);
        for (int i = 0; i < found.Length; i++)
            RegisterSeat(found[i]);
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public void RegisterSeat(RestaurantSeat seat)
    {
        if (seat == null || _seats.Contains(seat))
            return;
        _seats.Add(seat);
    }

    public void UnregisterSeat(RestaurantSeat seat)
    {
        if (seat == null)
            return;
        _seats.Remove(seat);
    }

    /// <summary>为顾客预留第一个空位；失败返回 null。</summary>
    public RestaurantSeat TryReserveSeat(CustomerNPC customer)
    {
        if (customer == null)
            return null;

        for (int i = 0; i < _seats.Count; i++)
        {
            RestaurantSeat seat = _seats[i];
            if (seat == null || !seat.IsAvailable)
                continue;

            if (seat.TryAssign(customer))
                return seat;
        }

        return null;
    }

    public void ReleaseSeat(RestaurantSeat seat)
    {
        if (seat == null)
            return;
        seat.Release();
    }

    public IReadOnlyList<RestaurantSeat> GetAllSeats() => _seats;
}
