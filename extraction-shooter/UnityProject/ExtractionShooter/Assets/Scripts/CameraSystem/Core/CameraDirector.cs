using System;
using System.Collections.Generic;
using UnityEngine;

namespace GourmetAbyss.CameraSystem
{
    /// <summary>
    /// 相机唯一写入者。业务通过 Shot Source 提交目标构图，Director 负责仲裁、混合、
    /// 边界、平滑和震动。该类不包含任何具体玩法或场景名称。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    [DefaultExecutionOrder(1000)]
    public sealed class CameraDirector : MonoBehaviour
    {
        private sealed class ShotEntry
        {
            public int Id;
            public long Sequence;
            public UnityEngine.Object Owner;
            public ICameraShotSource Source;
            public CameraShotOptions Options;
        }

        private sealed class Impulse
        {
            public float Remaining;
            public float Duration;
            public float Magnitude;
            public float Frequency;
            public float Seed;
        }

        [SerializeField] private Camera targetCamera;
        [SerializeField] private CameraInputRouter inputRouter;
        [SerializeField] private bool showDebugInfo;

        private readonly List<ShotEntry> _shots = new List<ShotEntry>();
        private readonly List<ShotEntry> _candidateBuffer = new List<ShotEntry>();
        private readonly List<Impulse> _impulses = new List<Impulse>();
        private int _nextRequestId = 1;
        private long _nextSequence = 1;
        private int _activeRequestId = -1;
        private CameraShotOptions _activeOptions;
        private float _forcedNextBlendDuration = -1f;

        private CameraPose _logicalPose;
        private CameraPose _blendFrom;
        private float _blendDuration;
        private float _blendElapsed;
        private bool _isBlending;

        private Vector3 _positionVelocity;
        private float _lensVelocity;

        public Camera Camera => targetCamera;
        public CameraInputRouter InputRouter => inputRouter;
        public CameraPose CurrentPose => _logicalPose;
        public int ActiveRequestId => _activeRequestId;
        public int RequestCount => _shots.Count;

        private void Awake()
        {
            if (targetCamera == null)
                targetCamera = GetComponent<Camera>();

            if (inputRouter == null)
                inputRouter = GetComponent<CameraInputRouter>();
            if (inputRouter == null)
                inputRouter = gameObject.AddComponent<CameraInputRouter>();

            _logicalPose = ReadPoseFromCamera();
        }

        private void OnEnable()
        {
            CameraService.Register(this);
        }

        private void OnDisable()
        {
            CameraService.Unregister(this);
            _shots.Clear();
            _impulses.Clear();
            _activeRequestId = -1;
        }

        public CameraShotLease AcquireShot(
            UnityEngine.Object owner,
            ICameraShotSource source,
            CameraShotOptions options)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            ShotEntry entry = new ShotEntry
            {
                Id = _nextRequestId++,
                Sequence = _nextSequence++,
                Owner = owner,
                Source = source,
                Options = options
            };
            _shots.Add(entry);
            return new CameraShotLease(this, entry.Id);
        }

        internal bool ContainsRequest(int requestId)
        {
            for (int i = 0; i < _shots.Count; i++)
            {
                if (_shots[i].Id == requestId)
                    return true;
            }
            return false;
        }

        internal void ReleaseShot(int requestId)
        {
            for (int i = _shots.Count - 1; i >= 0; i--)
            {
                ShotEntry entry = _shots[i];
                if (entry.Id != requestId)
                    continue;

                if (_activeRequestId == requestId)
                    _forcedNextBlendDuration = entry.Options.BlendOut;

                _shots.RemoveAt(i);
                return;
            }
        }

        public void PlayImpulse(float duration, float magnitude, float frequency = 24f)
        {
            if (duration <= 0f || magnitude <= 0f)
                return;

            _impulses.Add(new Impulse
            {
                Duration = Mathf.Max(0.01f, duration),
                Remaining = Mathf.Max(0.01f, duration),
                Magnitude = magnitude,
                Frequency = Mathf.Max(0.01f, frequency),
                Seed = UnityEngine.Random.Range(0f, 1000f)
            });
        }

        public void SnapTo(CameraPose pose)
        {
            _logicalPose = pose;
            _blendFrom = pose;
            _isBlending = false;
            _positionVelocity = Vector3.zero;
            _lensVelocity = 0f;
            ApplyPose(pose);
        }

        private void LateUpdate()
        {
            if (targetCamera == null)
                return;

            PruneInvalidOwners();

            CameraInputFrame input = inputRouter != null ? inputRouter.CurrentFrame : default;
            CameraEvaluationContext context = new CameraEvaluationContext(
                this,
                targetCamera,
                input,
                _logicalPose,
                Time.deltaTime,
                Time.unscaledDeltaTime);

            if (!TrySelectShot(context, out ShotEntry selected, out CameraShotResult shot))
            {
                AgeImpulses(Time.unscaledDeltaTime);
                return;
            }

            if ((shot.Policy & CameraShotPolicy.RespectBounds) != 0 && shot.Bounds.IsValid)
            {
                shot.Pose = CameraBoundsUtility.ConstrainOrthographicPose(
                    shot.Pose,
                    targetCamera.aspect,
                    shot.Plane,
                    shot.Bounds);
            }

            if (selected.Id != _activeRequestId)
                BeginShotTransition(selected);

            float dt = (shot.Policy & CameraShotPolicy.UseUnscaledTime) != 0
                ? Time.unscaledDeltaTime
                : Time.deltaTime;
            dt = Mathf.Max(0f, dt);

            UpdateLogicalPose(shot, dt);

            Vector3 impulseOffset = EvaluateImpulses(Time.unscaledDeltaTime);
            CameraPose renderedPose = _logicalPose;
            if ((shot.Policy & CameraShotPolicy.AllowShake) != 0)
                renderedPose.Position += impulseOffset;

            ApplyPose(renderedPose);

            if (showDebugInfo)
            {
                Debug.DrawLine(_logicalPose.Position, _logicalPose.Position + _logicalPose.Rotation * Vector3.forward * 3f, Color.cyan);
            }
        }

        private bool TrySelectShot(
            in CameraEvaluationContext context,
            out ShotEntry selected,
            out CameraShotResult result)
        {
            selected = null;
            result = default;

            _candidateBuffer.Clear();
            _candidateBuffer.AddRange(_shots);

            // 原地选择排序：请求量通常只有个位数，无 GC，且失效 Source 会稳定回退到下一层。
            for (int start = 0; start < _candidateBuffer.Count; start++)
            {
                int bestIndex = start;
                for (int i = start + 1; i < _candidateBuffer.Count; i++)
                {
                    ShotEntry entry = _candidateBuffer[i];
                    ShotEntry best = _candidateBuffer[bestIndex];
                    if (entry.Options.Priority > best.Options.Priority ||
                        (entry.Options.Priority == best.Options.Priority && entry.Sequence > best.Sequence))
                        bestIndex = i;
                }

                if (bestIndex != start)
                {
                    ShotEntry swap = _candidateBuffer[start];
                    _candidateBuffer[start] = _candidateBuffer[bestIndex];
                    _candidateBuffer[bestIndex] = swap;
                }

                ShotEntry candidate = _candidateBuffer[start];
                if (candidate.Source.TryEvaluate(context, out result))
                {
                    selected = candidate;
                    return true;
                }
            }

            return false;
        }

        private void BeginShotTransition(ShotEntry selected)
        {
            float duration = _forcedNextBlendDuration >= 0f
                ? _forcedNextBlendDuration
                : selected.Options.BlendIn;

            _forcedNextBlendDuration = -1f;
            _blendFrom = _logicalPose;
            _blendDuration = Mathf.Max(0f, duration);
            _blendElapsed = 0f;
            _isBlending = _blendDuration > 0.0001f;
            _positionVelocity = Vector3.zero;
            _lensVelocity = 0f;
            _activeRequestId = selected.Id;
            _activeOptions = selected.Options;
        }

        private void UpdateLogicalPose(CameraShotResult shot, float dt)
        {
            if (_isBlending)
            {
                _blendElapsed += dt;
                float t = _blendDuration <= 0f ? 1f : Mathf.Clamp01(_blendElapsed / _blendDuration);
                float eased = t * t * (3f - 2f * t);
                _logicalPose = CameraPose.Lerp(_blendFrom, shot.Pose, eased);
                if (t >= 1f)
                    _isBlending = false;
                return;
            }

            CameraDamping damping = shot.Damping;
            _logicalPose.Position = damping.PositionSmoothTime <= 0.0001f
                ? shot.Pose.Position
                : Vector3.SmoothDamp(
                    _logicalPose.Position,
                    shot.Pose.Position,
                    ref _positionVelocity,
                    damping.PositionSmoothTime,
                    Mathf.Max(0.01f, damping.MaxPositionSpeed),
                    dt);

            if (damping.RotationSmoothTime <= 0.0001f)
            {
                _logicalPose.Rotation = shot.Pose.Rotation;
            }
            else
            {
                float rotationT = 1f - Mathf.Exp(-dt / damping.RotationSmoothTime);
                _logicalPose.Rotation = Quaternion.Slerp(_logicalPose.Rotation, shot.Pose.Rotation, rotationT);
            }

            _logicalPose.OrthographicSize = damping.LensSmoothTime <= 0.0001f
                ? shot.Pose.OrthographicSize
                : Mathf.SmoothDamp(
                    _logicalPose.OrthographicSize,
                    shot.Pose.OrthographicSize,
                    ref _lensVelocity,
                    damping.LensSmoothTime,
                    Mathf.Infinity,
                    dt);
        }

        private void PruneInvalidOwners()
        {
            for (int i = _shots.Count - 1; i >= 0; i--)
            {
                ShotEntry entry = _shots[i];
                bool invalid = entry.Owner == null;
                if (!invalid && entry.Owner is Behaviour behaviour)
                    invalid = !behaviour.isActiveAndEnabled;

                if (!invalid)
                    continue;

                if (_activeRequestId == entry.Id)
                    _forcedNextBlendDuration = entry.Options.BlendOut;
                _shots.RemoveAt(i);
            }
        }

        private Vector3 EvaluateImpulses(float dt)
        {
            Vector2 total = Vector2.zero;
            for (int i = _impulses.Count - 1; i >= 0; i--)
            {
                Impulse impulse = _impulses[i];
                impulse.Remaining -= dt;
                if (impulse.Remaining <= 0f)
                {
                    _impulses.RemoveAt(i);
                    continue;
                }

                float life = Mathf.Clamp01(impulse.Remaining / impulse.Duration);
                float envelope = life * life;
                float time = (impulse.Duration - impulse.Remaining) * impulse.Frequency;
                float x = Mathf.PerlinNoise(impulse.Seed, time) * 2f - 1f;
                float y = Mathf.PerlinNoise(impulse.Seed + 137.3f, time) * 2f - 1f;
                total += new Vector2(x, y) * (impulse.Magnitude * envelope);
            }

            total = Vector2.ClampMagnitude(total, 2f);
            return _logicalPose.Rotation * (Vector3.right * total.x + Vector3.up * total.y);
        }

        private void AgeImpulses(float dt)
        {
            for (int i = _impulses.Count - 1; i >= 0; i--)
            {
                _impulses[i].Remaining -= dt;
                if (_impulses[i].Remaining <= 0f)
                    _impulses.RemoveAt(i);
            }
        }

        private CameraPose ReadPoseFromCamera()
        {
            return new CameraPose(
                transform.position,
                transform.rotation,
                targetCamera != null ? targetCamera.orthographicSize : 5f);
        }

        private void ApplyPose(CameraPose pose)
        {
            transform.SetPositionAndRotation(pose.Position, pose.Rotation);
            if (targetCamera != null && targetCamera.orthographic)
                targetCamera.orthographicSize = Mathf.Max(0.01f, pose.OrthographicSize);
        }

        public string GetDebugSummary()
        {
            string activeName = "None";
            for (int i = 0; i < _shots.Count; i++)
            {
                if (_shots[i].Id == _activeRequestId)
                {
                    activeName = string.IsNullOrEmpty(_shots[i].Options.DebugName)
                        ? _shots[i].Source.GetType().Name
                        : _shots[i].Options.DebugName;
                    break;
                }
            }

            return $"Active={activeName}, Requests={_shots.Count}, Pose={_logicalPose.Position}, Ortho={_logicalPose.OrthographicSize:F2}";
        }
    }
}
