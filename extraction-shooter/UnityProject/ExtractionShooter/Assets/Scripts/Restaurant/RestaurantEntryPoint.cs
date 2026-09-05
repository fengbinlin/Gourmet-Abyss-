using System.Collections.Generic;
using Game.Core;
using GourmetAbyss.CameraSystem;
using UnityEngine;

/// <summary>
/// 挂在餐厅「进入点」触发器上：进入范围后按 E 进入餐厅视角；通过 LeaveRestaurant()（如 UI 按钮）离开。
/// </summary>
[RequireComponent(typeof(Collider))]
public class RestaurantEntryPoint : MonoSingleton<RestaurantEntryPoint>
{
    // 每个场景各一份，后加载的接管——沿用原来的裸赋值语义。
    protected override DuplicatePolicy Duplicate => DuplicatePolicy.OverwriteReference;

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
    [SerializeField] private RestaurantCameraProfile cameraProfile;
    [Tooltip("可选。指定后直接使用 Collider 边界；为空时按 cameraBoundsRoot、锚点父物体、活动内容依次回退。")]
    [SerializeField] private Collider cameraBounds;
    [Tooltip("可选。未指定 cameraBounds 时，从该对象的 Renderer 计算边界；为空时优先使用餐厅锚点的父物体。")]
    [SerializeField] private GameObject cameraBoundsRoot;

    [Header("交互")]
    [SerializeField] private KeyCode interactKey = KeyCode.E;
    [SerializeField] private string playerTag = "Player";

    private readonly HashSet<int> _playerColliderIdsInTrigger = new HashSet<int>();
    private TopDownController _cachedPlayer;
    private TopDownController _lockedPlayer;
    private bool _wasPlayerMoveEnabled = true;
    private bool _playerInRange;
    private bool _isEntered;
    private bool _hasSavedCameraState;
    private float _savedOrthoSize;
    private Vector3 _savedCameraPosition;
    private string _cameraRequestKey;
    private InteractiveFeedback _feedback;
    private CameraShotLease _restaurantCameraLease;
    private CameraPlanarBounds _cachedCameraBounds;
    private bool _hasCachedCameraBounds;
    private GameObject _runtimeCameraAnchor;

    public bool IsEntered => _isEntered;

    protected override void OnAwake()
    {
        Collider col = GetComponent<Collider>();
        if (col != null && !col.isTrigger)
            col.isTrigger = true;

        _cameraRequestKey = $"restaurant_entry_{GetInstanceID()}";
        _feedback = GetComponent<InteractiveFeedback>();
        SetRestaurantContentActive(false);
    }

    // 原 OnDestroy 只做「清空 Instance」，该职责已由 MonoSingleton 基类接管。

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
            LeaveRestaurant();

        _feedback?.StopFeedbackSmoothly();
    }

    private void OnDisable()
    {
        if (_isEntered)
            LeaveRestaurant();

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

        SaveCameraStateBeforeEnter();
        AcquireRestaurantCamera();

        _lockedPlayer = player;
        _wasPlayerMoveEnabled = player.canPlayerMove;
        player.SetPlayerMovementEnabled(false);
        _isEntered = true;
        SetRestaurantContentActive(true);
        RefreshRestaurantMenuOnEnter();

        AudioManager.Instance?.PlayAudio("3");
    }

    private static void RefreshRestaurantMenuOnEnter()
    {
        if (RestaurantPanel.instance == null)
            return;

        RestaurantPanel.instance.RefreshRestaurantUnits();
        RestaurantPanel.instance.RefreshOnOpen();
    }

    /// <summary>
    /// 离开餐厅（可绑定到 UI 按钮 OnClick）。始终恢复相机与玩家移动。
    /// </summary>
    public void LeaveRestaurant()
    {
        _isEntered = false;
        HideDecorationPanel();
        RestoreRestaurantCamera();
        RestorePlayerMovement();
        SetRestaurantContentActive(false);
    }

    private static void HideDecorationPanel()
    {
        if (RestaurantDecorationPanelUI.Instance != null)
        {
            RestaurantDecorationPanelUI.Instance.HidePanel();
            return;
        }

        RestaurantDecorationPanelUI panel = FindObjectOfType<RestaurantDecorationPanelUI>(true);
        panel?.HidePanel();
    }

    private void SaveCameraStateBeforeEnter()
    {
        Camera cam = Camera.main;
        if (cam == null)
            cam = FindObjectOfType<Camera>();

        if (cam == null)
            return;

        _savedOrthoSize = cam.orthographicSize;
        _savedCameraPosition = cam.transform.position;
        _hasSavedCameraState = true;
    }

    private void RestoreRestaurantCamera()
    {
        _restaurantCameraLease?.Dispose();
        _restaurantCameraLease = null;
        if (_runtimeCameraAnchor != null)
        {
            Destroy(_runtimeCameraAnchor);
            _runtimeCameraAnchor = null;
        }

        // 新框架会自动混合回请求栈的下一层；只有框架未初始化时才走旧恢复兜底。
        if (CameraService.Active == null)
        {
            CameraFollow cameraFollow = FindObjectOfType<CameraFollow>();
            if (cameraFollow != null)
            {
                if (_hasSavedCameraState)
                    cameraFollow.SnapBackToDefaultFollow(_savedOrthoSize, _savedCameraPosition);
                else
                    cameraFollow.SnapBackToDefaultFollow();
            }
        }

        _hasSavedCameraState = false;

        if (UIFloatingButtonGroup.Instance != null)
            UIFloatingButtonGroup.Instance.DeselectIfSelected(UIFloatingButtonGroup.Instance.RestaurantButtonIndex);
    }

    private void RestorePlayerMovement()
    {
        if (_lockedPlayer == null)
            return;

        _lockedPlayer.SetPlayerMovementEnabled(_wasPlayerMoveEnabled);
        _lockedPlayer = null;
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

    private void AcquireRestaurantCamera()
    {
        CameraDirector director = CameraService.Active;
        Transform anchor = restaurantAnchor != null ? restaurantAnchor : transform;
        if (director == null || anchor == null)
            return;

        _restaurantCameraLease?.Dispose();
        _hasCachedCameraBounds = false;

        float requestedSize = adjustOrthoSize && cameraOrthoSize > 0f
            ? cameraOrthoSize
            : director.CurrentPose.OrthographicSize;
        if (cameraProfile != null && cameraProfile.orthographicSize > 0f)
            requestedSize = cameraProfile.orthographicSize;

        CameraDamping damping = cameraProfile != null
            ? cameraProfile.damping
            : new CameraDamping(0.15f, 0.15f, 0.18f);
        float dragSensitivity = cameraProfile != null ? cameraProfile.dragSensitivity : 1f;
        bool blockDragOverUi = cameraProfile == null || cameraProfile.blockDragWhenPointerOverUi;

        // 兼容旧配置中的 Y 对焦偏移：沿当前镜头平面的屏幕 Up 方向移动锚点。
        CameraPlane plane = CameraPlane.FromRotation(director.CurrentPose.Rotation, anchor.position);
        if (_runtimeCameraAnchor != null)
            Destroy(_runtimeCameraAnchor);
        _runtimeCameraAnchor = new GameObject("~RestaurantCameraAnchor");
        _runtimeCameraAnchor.hideFlags = HideFlags.HideAndDontSave;
        _runtimeCameraAnchor.transform.SetParent(anchor, false);
        _runtimeCameraAnchor.transform.position = anchor.position - plane.Up * cameraYFocusOffset;
        Transform effectiveAnchor = _runtimeCameraAnchor.transform;

        RestaurantPanCameraSource source = new RestaurantPanCameraSource(
            effectiveAnchor,
            director.CurrentPose,
            requestedSize,
            dragSensitivity,
            blockDragOverUi,
            damping,
            ResolveRestaurantBounds);

        float blendIn = cameraProfile != null ? cameraProfile.blendIn : 0.3f;
        float blendOut = cameraProfile != null ? cameraProfile.blendOut : 0.25f;
        _restaurantCameraLease = director.AcquireShot(
            this,
            source,
            new CameraShotOptions(50, blendIn, blendOut, "Restaurant"));
    }

    private CameraPlanarBounds ResolveRestaurantBounds(CameraPlane plane)
    {
        if (_hasCachedCameraBounds)
            return _cachedCameraBounds;

        Bounds worldBounds;
        bool found = false;
        if (cameraBounds != null)
        {
            worldBounds = cameraBounds.bounds;
            found = true;
        }
        else
        {
            GameObject boundsRoot = ResolveCameraBoundsRoot();
            found = CameraBoundsUtility.TryCollectRendererBounds(boundsRoot, out worldBounds);
            if (!found && boundsRoot != restaurantActiveContent)
                found = CameraBoundsUtility.TryCollectRendererBounds(restaurantActiveContent, out worldBounds);
        }

        _cachedCameraBounds = found
            ? CameraPlanarBounds.FromWorldBounds(worldBounds, plane)
            : default;
        _hasCachedCameraBounds = true;
        return _cachedCameraBounds;
    }

    private GameObject ResolveCameraBoundsRoot()
    {
        if (cameraBoundsRoot != null)
            return cameraBoundsRoot;

        if (restaurantAnchor != null && restaurantAnchor.parent != null)
            return restaurantAnchor.parent.gameObject;

        return restaurantActiveContent;
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 anchorPos = GetAnchorPosition();
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(anchorPos, 0.35f);
        Gizmos.DrawLine(transform.position, anchorPos);
    }
}
