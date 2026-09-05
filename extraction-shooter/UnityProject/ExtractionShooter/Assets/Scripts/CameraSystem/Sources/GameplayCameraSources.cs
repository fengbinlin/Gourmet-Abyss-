using System;
using UnityEngine;

namespace GourmetAbyss.CameraSystem
{
    public sealed class TownFollowCameraSource : ICameraTargetSource
    {
        private readonly Func<Vector3> _facingProvider;
        private readonly Vector3 _positionOffset;
        private readonly Quaternion _rotation;
        private readonly float _orthographicSize;
        private readonly float _lookAheadDistance;
        private readonly float _lookAheadSmoothTime;
        private readonly CameraDamping _damping;

        private Vector2 _lookAhead;
        private Vector2 _lookAheadVelocity;
        private Vector3 _lastFacing = Vector3.right;

        public Transform Target { get; set; }

        public TownFollowCameraSource(
            Transform target,
            Func<Vector3> facingProvider,
            Vector3 positionOffset,
            Quaternion rotation,
            float orthographicSize,
            float lookAheadDistance,
            float lookAheadSmoothTime,
            CameraDamping damping)
        {
            Target = target;
            _facingProvider = facingProvider;
            _positionOffset = positionOffset;
            _rotation = rotation;
            _orthographicSize = orthographicSize;
            _lookAheadDistance = Mathf.Max(0f, lookAheadDistance);
            _lookAheadSmoothTime = Mathf.Max(0f, lookAheadSmoothTime);
            _damping = damping;
        }

        public bool TryEvaluate(in CameraEvaluationContext context, out CameraShotResult result)
        {
            if (Target == null)
            {
                result = default;
                return false;
            }

            CameraPlane plane = CameraPlane.FromRotation(_rotation, Target.position);
            Vector3 facing = _facingProvider != null ? _facingProvider() : Target.forward;
            Vector3 planarFacing = Vector3.ProjectOnPlane(facing, plane.Normal);
            if (planarFacing.sqrMagnitude > 0.0001f)
                _lastFacing = planarFacing.normalized;

            Vector2 desiredLookAhead = new Vector2(
                Vector3.Dot(_lastFacing, plane.Right),
                Vector3.Dot(_lastFacing, plane.Up)) * _lookAheadDistance;

            float dt = Mathf.Max(0f, context.DeltaTime);
            _lookAhead = _lookAheadSmoothTime <= 0.0001f
                ? desiredLookAhead
                : Vector2.SmoothDamp(
                    _lookAhead,
                    desiredLookAhead,
                    ref _lookAheadVelocity,
                    _lookAheadSmoothTime,
                    Mathf.Infinity,
                    dt);

            Vector3 position = Target.position + _positionOffset +
                               plane.Right * _lookAhead.x + plane.Up * _lookAhead.y;
            CameraPose pose = new CameraPose(position, _rotation, _orthographicSize);
            result = new CameraShotResult(pose, _damping, plane, CameraShotPolicy.Default);
            return true;
        }
    }

    public sealed class DungeonAimCameraSource : ICameraTargetSource
    {
        private readonly Vector3 _positionOffset;
        private readonly Quaternion _rotation;
        private readonly float _orthographicSize;
        private readonly float _centerDeadZone;
        private readonly float _maxPointerOffset;
        private readonly float _responseExponent;
        private readonly float _pointerSmoothTime;
        private readonly CameraDamping _damping;

        private Vector2 _pointerOffset;
        private Vector2 _pointerVelocity;

        public Transform Target { get; set; }

        public DungeonAimCameraSource(
            Transform target,
            Vector3 positionOffset,
            Quaternion rotation,
            float orthographicSize,
            float centerDeadZone,
            float maxPointerOffset,
            float responseExponent,
            float pointerSmoothTime,
            CameraDamping damping)
        {
            Target = target;
            _positionOffset = positionOffset;
            _rotation = rotation;
            _orthographicSize = orthographicSize;
            _centerDeadZone = Mathf.Clamp(centerDeadZone, 0f, 0.95f);
            _maxPointerOffset = Mathf.Max(0f, maxPointerOffset);
            _responseExponent = Mathf.Max(0.01f, responseExponent);
            _pointerSmoothTime = Mathf.Max(0f, pointerSmoothTime);
            _damping = damping;
        }

        public bool TryEvaluate(in CameraEvaluationContext context, out CameraShotResult result)
        {
            if (Target == null)
            {
                result = default;
                return false;
            }

            Vector2 pointer = context.Input.PointerBlockedByUi
                ? Vector2.zero
                : Vector2.ClampMagnitude(context.Input.PointerNormalized, 1f);
            float magnitude = pointer.magnitude;
            Vector2 desiredOffset = Vector2.zero;
            if (magnitude > _centerDeadZone)
            {
                float normalized = Mathf.InverseLerp(_centerDeadZone, 1f, magnitude);
                float response = Mathf.Pow(normalized, _responseExponent);
                desiredOffset = pointer.normalized * (_maxPointerOffset * response);
            }

            float dt = Mathf.Max(0f, context.DeltaTime);
            _pointerOffset = _pointerSmoothTime <= 0.0001f
                ? desiredOffset
                : Vector2.SmoothDamp(
                    _pointerOffset,
                    desiredOffset,
                    ref _pointerVelocity,
                    _pointerSmoothTime,
                    Mathf.Infinity,
                    dt);

            CameraPlane plane = CameraPlane.FromRotation(_rotation, Target.position);
            Vector3 position = Target.position + _positionOffset +
                               plane.Right * _pointerOffset.x + plane.Up * _pointerOffset.y;
            CameraPose pose = new CameraPose(position, _rotation, _orthographicSize);
            result = new CameraShotResult(pose, _damping, plane, CameraShotPolicy.Default);
            return true;
        }
    }

    public sealed class RestaurantPanCameraSource : ICameraShotSource
    {
        private readonly Transform _anchor;
        private readonly Vector3 _initialPosition;
        private readonly Quaternion _rotation;
        private readonly float _orthographicSize;
        private readonly float _dragSensitivity;
        private readonly bool _blockDragWhenPointerOverUi;
        private readonly CameraDamping _damping;
        private readonly Func<CameraPlane, CameraPlanarBounds> _boundsProvider;

        private Vector2 _panOffset;

        public RestaurantPanCameraSource(
            Transform anchor,
            CameraPose referencePose,
            float orthographicSize,
            float dragSensitivity,
            bool blockDragWhenPointerOverUi,
            CameraDamping damping,
            Func<CameraPlane, CameraPlanarBounds> boundsProvider)
        {
            _anchor = anchor;
            _rotation = referencePose.Rotation;
            _orthographicSize = orthographicSize > 0f ? orthographicSize : referencePose.OrthographicSize;
            _dragSensitivity = Mathf.Max(0.01f, dragSensitivity);
            _blockDragWhenPointerOverUi = blockDragWhenPointerOverUi;
            _damping = damping;
            _boundsProvider = boundsProvider;
            _initialPosition = CalculateCenteredCameraPosition(referencePose, anchor != null ? anchor.position : Vector3.zero);
        }

        public bool TryEvaluate(in CameraEvaluationContext context, out CameraShotResult result)
        {
            if (_anchor == null)
            {
                result = default;
                return false;
            }

            CameraPlane plane = CameraPlane.FromRotation(_rotation, _anchor.position);
            CameraPlanarBounds bounds = _boundsProvider != null ? _boundsProvider(plane) : default;
            bool canDrag = context.Input.PanHeld &&
                           bounds.IsValid &&
                           (!_blockDragWhenPointerOverUi || !context.Input.PointerBlockedByUi);
            if (canDrag)
            {
                float worldPerPixel = 2f * _orthographicSize / Mathf.Max(1f, Screen.height);
                _panOffset -= context.Input.PointerDeltaPixels * (worldPerPixel * _dragSensitivity);
            }

            CameraPose pose = new CameraPose(
                _initialPosition + plane.Right * _panOffset.x + plane.Up * _panOffset.y,
                _rotation,
                _orthographicSize);
            result = new CameraShotResult(
                pose,
                _damping,
                plane,
                CameraShotPolicy.Default | CameraShotPolicy.UseUnscaledTime,
                bounds);
            return true;
        }

        private static Vector3 CalculateCenteredCameraPosition(CameraPose referencePose, Vector3 target)
        {
            CameraPlane plane = CameraPlane.FromRotation(referencePose.Rotation, target);
            Vector3 forward = referencePose.Rotation * Vector3.forward;
            float denominator = Vector3.Dot(forward, plane.Normal);
            if (Mathf.Abs(denominator) < 0.0001f)
                return target + (referencePose.Position - plane.Origin);

            float distance = Vector3.Dot(target - referencePose.Position, plane.Normal) / denominator;
            if (distance <= 0.01f)
                distance = Mathf.Max(1f, Vector3.Distance(referencePose.Position, target));
            return target - forward * distance;
        }
    }

    /// <summary>通用单目标构图，可供剧情、NPC、UI 和未来玩法直接复用。</summary>
    public sealed class TransformFocusCameraSource : ICameraTargetSource
    {
        private readonly Quaternion _rotation;
        private readonly float _rayDistance;
        private readonly float _orthographicSize;
        private readonly CameraDamping _damping;
        private readonly CameraShotPolicy _policy;

        public Transform Target { get; set; }

        public TransformFocusCameraSource(
            Transform target,
            CameraPose referencePose,
            float orthographicSize,
            CameraDamping damping,
            CameraShotPolicy policy = CameraShotPolicy.AllowShake | CameraShotPolicy.UseUnscaledTime)
        {
            Target = target;
            _rotation = referencePose.Rotation;
            _orthographicSize = orthographicSize > 0f ? orthographicSize : referencePose.OrthographicSize;
            _damping = damping;
            _policy = policy;

            Vector3 targetPosition = target != null ? target.position : Vector3.zero;
            CameraPlane plane = CameraPlane.FromRotation(referencePose.Rotation, targetPosition);
            Vector3 forward = referencePose.Rotation * Vector3.forward;
            float denominator = Vector3.Dot(forward, plane.Normal);
            float rayDistance = Mathf.Abs(denominator) > 0.0001f
                ? Vector3.Dot(targetPosition - referencePose.Position, plane.Normal) / denominator
                : Vector3.Distance(referencePose.Position, targetPosition);
            _rayDistance = rayDistance > 0.01f
                ? rayDistance
                : Mathf.Max(1f, Vector3.Distance(referencePose.Position, targetPosition));
        }

        public bool TryEvaluate(in CameraEvaluationContext context, out CameraShotResult result)
        {
            if (Target == null)
            {
                result = default;
                return false;
            }

            CameraPlane plane = CameraPlane.FromRotation(_rotation, Target.position);
            Vector3 forward = _rotation * Vector3.forward;
            CameraPose pose = new CameraPose(
                Target.position - forward * _rayDistance,
                _rotation,
                _orthographicSize);
            result = new CameraShotResult(pose, _damping, plane, _policy);
            return true;
        }
    }
}
