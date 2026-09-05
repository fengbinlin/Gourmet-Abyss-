using System;
using System.Collections.Generic;
using GourmetAbyss.CameraSystem;
using UnityEngine;

/// <summary>
/// 旧场景和业务脚本的兼容入口。实际镜头计算、仲裁和输出由 CameraDirector 完成；
/// 新功能应直接提交 ICameraShotSource，不应继续扩展本类。
/// </summary>
[DisallowMultipleComponent]
public class CameraFollow : MonoBehaviour
{
    private enum LegacyDefaultSource
    {
        Auto,
        Town,
        Dungeon
    }

    private sealed class LegacyFocusState
    {
        public CameraDirector Director;
        public CameraShotLease Lease;
        public MutableLegacyFocusSource Source;
    }

    private sealed class MutableLegacyFocusSource : ICameraShotSource
    {
        public CameraPose ReferencePose;
        public bool OverrideX;
        public bool OverrideY;
        public bool OverrideSize;
        public float X;
        public float Y;
        public float Size;

        public bool TryEvaluate(in CameraEvaluationContext context, out CameraShotResult result)
        {
            CameraPose pose = ReferencePose;
            if (OverrideX) pose.Position.x = X;
            if (OverrideY) pose.Position.y = Y;
            if (OverrideSize) pose.OrthographicSize = Mathf.Max(0.01f, Size);
            CameraPlane plane = CameraPlane.FromRotation(pose.Rotation, pose.Position);
            result = new CameraShotResult(
                pose,
                new CameraDamping(0.12f, 0.15f, 0.18f),
                plane,
                CameraShotPolicy.AllowShake | CameraShotPolicy.UseUnscaledTime);
            return true;
        }
    }

    private static readonly Dictionary<string, LegacyFocusState> LegacyFocusRequests =
        new Dictionary<string, LegacyFocusState>();

    [Header("目标设置")]
    [SerializeField] private Transform target;
    [SerializeField] private TopDownController playerController;

    [Header("兼容场景默认镜头")]
    [SerializeField] private LegacyDefaultSource defaultSource = LegacyDefaultSource.Auto;
    [SerializeField] private TownCameraProfile townProfile;
    [SerializeField] private DungeonCameraProfile dungeonProfile;

    [Header("跟随参数（无 Profile 时作为默认值）")]
    [SerializeField] private float smoothTime = 0.3f;
    [SerializeField] private Vector3 offset;
    [SerializeField] private bool autoOffset = true;
    [SerializeField] private float townLookAheadDistance = 1.5f;
    [SerializeField] private float townLookAheadSmoothTime = 0.2f;
    [SerializeField] private float dungeonPointerMaxOffset = 3f;
    [SerializeField, Range(0f, 0.95f)] private float dungeonPointerDeadZone = 0.1f;
    [SerializeField, Range(0.25f, 4f)] private float dungeonPointerResponseExponent = 1.4f;
    [SerializeField] private float dungeonPointerSmoothTime = 0.14f;

    // 旧序列化字段保留，避免现有场景丢字段；新框架不再按轴平均多个请求。
    [Header("旧字段（仅兼容序列化）")]
    [SerializeField] private float orthoSizeSmoothTime = 0.18f;
    [SerializeField] private float xFocusSmoothTime = 0.08f;
    [SerializeField] private float yFocusSmoothTime = 0.08f;

    private CameraDirector _director;
    private CameraSceneContext _sceneContext;
    private CameraShotLease _baseLease;
    private CameraShotLease _overrideLease;
    private ICameraTargetSource _baseSource;
    private Transform _defaultTarget;
    private Transform _overrideTarget;
    private Vector3 _baseOffset;

    public event Action OnOverrideClearedByPlayerMove;
    public CameraDirector Director => _director;
    public Transform DefaultTarget => _defaultTarget != null ? _defaultTarget : target;

    private void Awake()
    {
        _director = GetComponent<CameraDirector>();
        if (_director == null)
            _director = gameObject.AddComponent<CameraDirector>();
        _sceneContext = GetComponent<CameraSceneContext>();
        if (_sceneContext == null)
            _sceneContext = gameObject.AddComponent<CameraSceneContext>();
    }

    private void Start()
    {
        _defaultTarget = target;
        _sceneContext?.BindDefaultTarget(_defaultTarget);
        AutoBindPlayerControllerIfNeeded();
        RegisterBaseSource();
    }

    private void OnEnable()
    {
        if (_director != null && _baseLease == null && _defaultTarget != null)
            RegisterBaseSource();
    }

    private void Update()
    {
        if (_overrideTarget != null && playerController != null && playerController.IsMoving())
            ClearOverrideTarget(true);
    }

    private void OnDisable()
    {
        _overrideLease?.Dispose();
        _overrideLease = null;
        _baseLease?.Dispose();
        _baseLease = null;
    }

    private void AutoBindPlayerControllerIfNeeded()
    {
        if (playerController != null || DefaultTarget == null)
            return;
        playerController = DefaultTarget.GetComponent<TopDownController>();
    }

    private void RegisterBaseSource()
    {
        if (_director == null || DefaultTarget == null)
            return;

        Camera cam = _director.Camera;
        if (cam == null)
            return;

        _baseOffset = autoOffset ? transform.position - DefaultTarget.position : offset;
        Quaternion rotation = transform.rotation;
        float currentSize = cam.orthographicSize;
        LegacyDefaultSource selected = ResolveDefaultSource(rotation);

        if (selected == LegacyDefaultSource.Town)
        {
            float size = townProfile != null && townProfile.orthographicSize > 0f
                ? townProfile.orthographicSize
                : currentSize;
            CameraDamping damping = townProfile != null
                ? townProfile.damping
                : new CameraDamping(smoothTime, 0.15f, orthoSizeSmoothTime);
            float lookAhead = townProfile != null ? townProfile.lookAheadDistance : townLookAheadDistance;
            float lookAheadSmooth = townProfile != null ? townProfile.lookAheadSmoothTime : townLookAheadSmoothTime;

            _baseSource = new TownFollowCameraSource(
                DefaultTarget,
                ResolvePlayerFacing,
                _baseOffset,
                rotation,
                size,
                lookAhead,
                lookAheadSmooth,
                damping);
        }
        else
        {
            float size = dungeonProfile != null && dungeonProfile.orthographicSize > 0f
                ? dungeonProfile.orthographicSize
                : currentSize;
            CameraDamping damping = dungeonProfile != null
                ? dungeonProfile.damping
                : new CameraDamping(smoothTime, 0.15f, orthoSizeSmoothTime);

            _baseSource = new DungeonAimCameraSource(
                DefaultTarget,
                _baseOffset,
                rotation,
                size,
                dungeonProfile != null ? dungeonProfile.centerDeadZone : dungeonPointerDeadZone,
                dungeonProfile != null ? dungeonProfile.maxPointerOffset : dungeonPointerMaxOffset,
                dungeonProfile != null ? dungeonProfile.responseExponent : dungeonPointerResponseExponent,
                dungeonProfile != null ? dungeonProfile.pointerSmoothTime : dungeonPointerSmoothTime,
                damping);
        }

        _baseLease?.Dispose();
        _baseLease = _director.AcquireShot(
            this,
            _baseSource,
            CameraShotOptions.Gameplay(selected.ToString()));
    }

    private LegacyDefaultSource ResolveDefaultSource(Quaternion rotation)
    {
        if (defaultSource != LegacyDefaultSource.Auto)
            return defaultSource;

        Vector3 forward = rotation * Vector3.forward;
        return Mathf.Abs(forward.y) > 0.1f
            ? LegacyDefaultSource.Dungeon
            : LegacyDefaultSource.Town;
    }

    private Vector3 ResolvePlayerFacing()
    {
        if (playerController != null && playerController.CameraFacingDirection.sqrMagnitude > 0.0001f)
            return playerController.CameraFacingDirection;
        return DefaultTarget != null ? DefaultTarget.forward : Vector3.right;
    }

    public void SetOverrideTarget(Transform newTarget)
    {
        if (newTarget == null || _director == null)
            return;

        _overrideLease?.Dispose();
        _overrideTarget = newTarget;
        TransformFocusCameraSource source = new TransformFocusCameraSource(
            newTarget,
            _director.CurrentPose,
            _director.CurrentPose.OrthographicSize,
            new CameraDamping(0.2f, 0.15f, 0.18f));
        _overrideLease = _director.AcquireShot(this, source, CameraShotOptions.Ui("Legacy UI Focus"));
    }

    public void ClearOverrideTarget()
    {
        ClearOverrideTarget(false);
    }

    private void ClearOverrideTarget(bool clearedByPlayerMove)
    {
        if (_overrideTarget == null && _overrideLease == null)
            return;

        _overrideTarget = null;
        _overrideLease?.Dispose();
        _overrideLease = null;
        if (clearedByPlayerMove)
            OnOverrideClearedByPlayerMove?.Invoke();
    }

    public void SetDefaultTarget(Transform newDefault)
    {
        if (newDefault == null)
            return;

        _defaultTarget = newDefault;
        target = newDefault;
        playerController = newDefault.GetComponent<TopDownController>();
        _sceneContext?.BindDefaultTarget(newDefault);
        if (_baseSource != null)
            _baseSource.Target = newDefault;
    }

    public void Shake(float duration, float magnitude)
    {
        if (_director != null)
            _director.PlayImpulse(duration, magnitude);
        else
            CameraService.PlayImpulse(duration, magnitude);
    }

    public CameraShotLease AcquireFocusShot(
        UnityEngine.Object owner,
        Transform focusTarget,
        float orthographicSize,
        CameraShotOptions options,
        CameraDamping? damping = null)
    {
        if (_director == null || focusTarget == null)
            return null;

        TransformFocusCameraSource source = new TransformFocusCameraSource(
            focusTarget,
            _director.CurrentPose,
            orthographicSize,
            damping ?? new CameraDamping(0.2f, 0.15f, 0.18f));
        return _director.AcquireShot(owner, source, options);
    }

    public void SnapBackToDefaultFollow(float? orthographicSize = null, Vector3? worldPosition = null)
    {
        ClearOverrideTarget();
        if (_director == null)
            return;

        Vector3 position = worldPosition ??
                           (DefaultTarget != null ? DefaultTarget.position + _baseOffset : _director.CurrentPose.Position);
        float size = orthographicSize ?? _director.CurrentPose.OrthographicSize;
        _director.SnapTo(new CameraPose(position, transform.rotation, size));
    }

    #region Legacy static request API

    [Obsolete("Use CameraService.AcquireShot and CameraShotLease instead.")]
    public static void PushOrthoSizeRequest(string requestKey, float targetSize)
    {
        LegacyFocusState state = GetLegacyState(requestKey);
        if (state == null) return;
        state.Source.OverrideSize = true;
        state.Source.Size = targetSize;
    }

    [Obsolete("Use CameraShotLease.Dispose instead.")]
    public static void PopOrthoSizeRequest(string requestKey)
    {
        ReleaseLegacyState(requestKey);
    }

    [Obsolete("Use CameraService.AcquireShot and CameraShotLease instead.")]
    public static void PushXFocusRequest(string requestKey, float worldX)
    {
        LegacyFocusState state = GetLegacyState(requestKey);
        if (state == null) return;
        state.Source.OverrideX = true;
        state.Source.X = worldX;
    }

    [Obsolete("Use CameraShotLease.Dispose instead.")]
    public static void PopXFocusRequest(string requestKey)
    {
        ReleaseLegacyState(requestKey);
    }

    [Obsolete("Use CameraService.AcquireShot and CameraShotLease instead.")]
    public static void PushYFocusRequest(string requestKey, float worldY)
    {
        LegacyFocusState state = GetLegacyState(requestKey);
        if (state == null) return;
        state.Source.OverrideY = true;
        state.Source.Y = worldY;
    }

    [Obsolete("Use CameraShotLease.Dispose instead.")]
    public static void PopYFocusRequest(string requestKey)
    {
        ReleaseLegacyState(requestKey);
    }

    private static LegacyFocusState GetLegacyState(string requestKey)
    {
        if (string.IsNullOrEmpty(requestKey) || CameraService.Active == null)
            return null;

        if (LegacyFocusRequests.TryGetValue(requestKey, out LegacyFocusState existing))
        {
            if (existing.Director == CameraService.Active && existing.Lease != null && existing.Lease.IsValid)
                return existing;
            existing.Lease?.Dispose();
            LegacyFocusRequests.Remove(requestKey);
        }

        CameraDirector director = CameraService.Active;
        MutableLegacyFocusSource source = new MutableLegacyFocusSource
        {
            ReferencePose = director.CurrentPose
        };
        LegacyFocusState state = new LegacyFocusState
        {
            Director = director,
            Source = source,
            Lease = director.AcquireShot(
                director,
                source,
                new CameraShotOptions(100, 0.2f, 0.2f, $"Legacy:{requestKey}"))
        };
        LegacyFocusRequests[requestKey] = state;
        return state;
    }

    private static void ReleaseLegacyState(string requestKey)
    {
        if (string.IsNullOrEmpty(requestKey))
            return;
        if (!LegacyFocusRequests.TryGetValue(requestKey, out LegacyFocusState state))
            return;
        state.Lease?.Dispose();
        LegacyFocusRequests.Remove(requestKey);
    }

    #endregion
}
