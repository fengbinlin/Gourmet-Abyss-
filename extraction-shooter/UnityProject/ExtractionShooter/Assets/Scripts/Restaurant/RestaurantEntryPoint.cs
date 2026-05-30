using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 挂在餐厅「进入点」触发器上：进入范围后按 E 进入餐厅视角；通过 LeaveRestaurant()（如 UI 按钮）离开。
/// </summary>
[RequireComponent(typeof(Collider))]
public class RestaurantEntryPoint : MonoBehaviour
{
    [Header("餐厅锚点（相机对焦位置；为空则使用本物体 Transform）")]
    [SerializeField] private Transform restaurantAnchor;

    [Header("进入餐厅后激活的对象（离开时休眠）")]
    [SerializeField] private GameObject restaurantActiveContent;

    [Header("相机对焦")]
    [SerializeField] private float cameraOrthoSize = 5.5f;
    [Tooltip("在锚点 Y 上的额外偏移（正值 = 相机看向更低的位置）")]
    [SerializeField] private float cameraYFocusOffset = 0.35f;
    [Tooltip("<= 0 时不改 orthographicSize，仅平移视角")]
    [SerializeField] private bool adjustOrthoSize = true;

    [Header("交互")]
    [SerializeField] private KeyCode interactKey = KeyCode.E;
    [SerializeField] private string playerTag = "Player";

    private readonly HashSet<int> _playerColliderIdsInTrigger = new HashSet<int>();
    private TopDownController _cachedPlayer;
    private TopDownController _lockedPlayer;
    private bool _wasPlayerMoveEnabled = true;
    private bool _playerInRange;
    private bool _isEntered;
    private string _cameraRequestKey;
    private InteractiveFeedback _feedback;

    public bool IsEntered => _isEntered;

    private void Awake()
    {
        Collider col = GetComponent<Collider>();
        if (col != null && !col.isTrigger)
            col.isTrigger = true;

        _cameraRequestKey = $"restaurant_entry_{GetInstanceID()}";
        _feedback = GetComponent<InteractiveFeedback>();
        SetRestaurantContentActive(false);
    }

    private void Update()
    {
        if (!_playerInRange || _isEntered || !Input.GetKeyDown(interactKey)) return;
        EnterEntryState();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag(playerTag)) return;

        int id = other.GetInstanceID();
        if (!_playerColliderIdsInTrigger.Add(id)) return;
        if (_playerColliderIdsInTrigger.Count > 1) return;

        _playerInRange = true;
        _cachedPlayer = other.GetComponent<TopDownController>();
        if (_cachedPlayer == null)
            _cachedPlayer = other.GetComponentInParent<TopDownController>();

        _feedback?.PlayFeedback();
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag(playerTag)) return;

        int id = other.GetInstanceID();
        if (!_playerColliderIdsInTrigger.Remove(id)) return;
        if (_playerColliderIdsInTrigger.Count > 0) return;

        _playerInRange = false;
        _cachedPlayer = null;
        if (_isEntered)
            ExitEntryState();

        _feedback?.StopFeedbackSmoothly();
    }

    private void OnDisable()
    {
        if (_isEntered)
            ExitEntryState();

        _playerInRange = false;
        _cachedPlayer = null;
        _playerColliderIdsInTrigger.Clear();
        _feedback?.StopFeedbackSmoothly();
    }

    private void EnterEntryState()
    {
        if (_isEntered) return;

        TopDownController player = _cachedPlayer;
        if (player == null) return;

        Vector3 anchorPos = GetAnchorPosition();
        CameraFollow.PushXFocusRequest(_cameraRequestKey, anchorPos.x);
        CameraFollow.PushYFocusRequest(_cameraRequestKey, anchorPos.y - cameraYFocusOffset);
        if (adjustOrthoSize && cameraOrthoSize > 0f)
            CameraFollow.PushOrthoSizeRequest(_cameraRequestKey, cameraOrthoSize);

        _lockedPlayer = player;
        _wasPlayerMoveEnabled = player.canPlayerMove;
        player.SetPlayerMovementEnabled(false);
        _isEntered = true;
        SetRestaurantContentActive(true);

        AudioManager.Instance?.PlayAudio("3");
    }

    private void ExitEntryState()
    {
        if (!_isEntered) return;

        CameraFollow.PopOrthoSizeRequest(_cameraRequestKey);
        CameraFollow.PopXFocusRequest(_cameraRequestKey);
        CameraFollow.PopYFocusRequest(_cameraRequestKey);

        if (_lockedPlayer != null)
        {
            _lockedPlayer.SetPlayerMovementEnabled(_wasPlayerMoveEnabled);
            _lockedPlayer = null;
        }

        _isEntered = false;
        SetRestaurantContentActive(false);
    }

    /// <summary>
    /// 离开餐厅（可绑定到 UI 按钮 OnClick）。恢复相机、移动，并休眠 restaurantActiveContent。
    /// </summary>
    public void LeaveRestaurant()
    {
        ExitEntryState();
    }

    private void SetRestaurantContentActive(bool active)
    {
        if (restaurantActiveContent == null) return;
        if (restaurantActiveContent.activeSelf != active)
            restaurantActiveContent.SetActive(active);
    }

    private Vector3 GetAnchorPosition()
    {
        return restaurantAnchor != null ? restaurantAnchor.position : transform.position;
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 anchorPos = GetAnchorPosition();
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(anchorPos, 0.35f);
        Gizmos.DrawLine(transform.position, anchorPos);
    }
}
