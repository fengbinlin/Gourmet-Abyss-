using System;
using UnityEngine;

namespace GourmetAbyss.CameraSystem
{
    /// <summary>
    /// 相机玩法所在的二维平面。屏幕偏移、拖拽与边界都通过该平面计算，
    /// 因此既兼容小镇当前的 XY 平面，也兼容地牢的 XZ 平面。
    /// </summary>
    [Serializable]
    public struct CameraPlane
    {
        public Vector3 Origin;
        public Vector3 Normal;
        public Vector3 Right;
        public Vector3 Up;

        public CameraPlane(Vector3 origin, Vector3 normal, Vector3 right, Vector3 up)
        {
            Origin = origin;
            Normal = normal.sqrMagnitude > 0.0001f ? normal.normalized : Vector3.up;

            Vector3 projectedRight = Vector3.ProjectOnPlane(right, Normal);
            Right = projectedRight.sqrMagnitude > 0.0001f
                ? projectedRight.normalized
                : Vector3.ProjectOnPlane(Vector3.right, Normal).normalized;

            Vector3 projectedUp = Vector3.ProjectOnPlane(up, Normal);
            Up = projectedUp.sqrMagnitude > 0.0001f
                ? projectedUp.normalized
                : Vector3.Cross(Right, Normal).normalized;

            // 防止传入的 Right / Up 不正交，保证平面坐标和边界计算稳定。
            Up = Vector3.ProjectOnPlane(Up, Right).normalized;
            if (Up.sqrMagnitude < 0.0001f)
                Up = Vector3.Cross(Right, Normal).normalized;
        }

        public static CameraPlane FromRotation(Quaternion rotation, Vector3 origin)
        {
            Vector3 forward = rotation * Vector3.forward;
            Vector3 right = rotation * Vector3.right;
            Vector3 screenUp = rotation * Vector3.up;

            // 有明显俯角时视为 Y-up 的地面；正视相机则使用与镜头平行的 XY 类平面。
            Vector3 normal = Mathf.Abs(forward.y) > 0.1f ? Vector3.up : forward;
            return new CameraPlane(origin, normal, right, screenUp);
        }

        public Vector2 ToPlane(Vector3 worldPoint)
        {
            Vector3 delta = worldPoint - Origin;
            return new Vector2(Vector3.Dot(delta, Right), Vector3.Dot(delta, Up));
        }

        public Vector3 FromPlane(Vector2 planePoint, float normalDistance = 0f)
        {
            return Origin + Right * planePoint.x + Up * planePoint.y + Normal * normalDistance;
        }

        public bool TryRaycast(Vector3 rayOrigin, Vector3 rayDirection, out Vector3 hit)
        {
            float denominator = Vector3.Dot(rayDirection, Normal);
            if (Mathf.Abs(denominator) < 0.00001f)
            {
                hit = default;
                return false;
            }

            float distance = Vector3.Dot(Origin - rayOrigin, Normal) / denominator;
            if (distance < 0f)
            {
                hit = default;
                return false;
            }

            hit = rayOrigin + rayDirection * distance;
            return true;
        }
    }

    [Serializable]
    public struct CameraPlanarBounds
    {
        public bool IsValid;
        public Vector2 Min;
        public Vector2 Max;

        public CameraPlanarBounds(Vector2 min, Vector2 max)
        {
            Min = Vector2.Min(min, max);
            Max = Vector2.Max(min, max);
            IsValid = Max.x - Min.x > 0.001f && Max.y - Min.y > 0.001f;
        }

        public Vector2 Center => (Min + Max) * 0.5f;
        public Vector2 Size => Max - Min;

        public static CameraPlanarBounds FromWorldBounds(Bounds bounds, CameraPlane plane)
        {
            Vector3 min = bounds.min;
            Vector3 max = bounds.max;
            Vector2 planeMin = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
            Vector2 planeMax = new Vector2(float.NegativeInfinity, float.NegativeInfinity);

            for (int x = 0; x <= 1; x++)
            for (int y = 0; y <= 1; y++)
            for (int z = 0; z <= 1; z++)
            {
                Vector3 corner = new Vector3(
                    x == 0 ? min.x : max.x,
                    y == 0 ? min.y : max.y,
                    z == 0 ? min.z : max.z);
                Vector2 projected = plane.ToPlane(corner);
                planeMin = Vector2.Min(planeMin, projected);
                planeMax = Vector2.Max(planeMax, projected);
            }

            return new CameraPlanarBounds(planeMin, planeMax);
        }
    }

    [Serializable]
    public struct CameraPose
    {
        public Vector3 Position;
        public Quaternion Rotation;
        public float OrthographicSize;
        public bool Perspective;
        public float FieldOfView;

        public CameraPose(Vector3 position, Quaternion rotation, float orthographicSize, bool perspective = false, float fieldOfView = 40f)
        {
            Position = position;
            Rotation = rotation;
            OrthographicSize = Mathf.Max(0.01f, orthographicSize);
            Perspective = perspective;
            FieldOfView = Mathf.Clamp(fieldOfView, 10f, 100f);
        }

        public static CameraPose Lerp(CameraPose from, CameraPose to, float t)
        {
            t = Mathf.Clamp01(t);
            return new CameraPose(
                Vector3.LerpUnclamped(from.Position, to.Position, t),
                Quaternion.SlerpUnclamped(from.Rotation, to.Rotation, t),
                Mathf.LerpUnclamped(from.OrthographicSize, to.OrthographicSize, t),
                t < 1f ? from.Perspective : to.Perspective,
                Mathf.Lerp(from.FieldOfView, to.FieldOfView, t));
        }
    }

    [Serializable]
    public struct CameraDamping
    {
        [Min(0f)] public float PositionSmoothTime;
        [Min(0f)] public float RotationSmoothTime;
        [Min(0f)] public float LensSmoothTime;
        [Min(0.01f)] public float MaxPositionSpeed;

        public CameraDamping(
            float positionSmoothTime,
            float rotationSmoothTime = 0.15f,
            float lensSmoothTime = 0.18f,
            float maxPositionSpeed = 1000f)
        {
            PositionSmoothTime = Mathf.Max(0f, positionSmoothTime);
            RotationSmoothTime = Mathf.Max(0f, rotationSmoothTime);
            LensSmoothTime = Mathf.Max(0f, lensSmoothTime);
            MaxPositionSpeed = Mathf.Max(0.01f, maxPositionSpeed);
        }

        public static CameraDamping Default => new CameraDamping(0.3f);
    }

    [Flags]
    public enum CameraShotPolicy
    {
        None = 0,
        AllowShake = 1 << 0,
        RespectBounds = 1 << 1,
        UseUnscaledTime = 1 << 2,
        Default = AllowShake | RespectBounds
    }

    public struct CameraShotResult
    {
        public CameraPose Pose;
        public CameraDamping Damping;
        public CameraShotPolicy Policy;
        public CameraPlane Plane;
        public CameraPlanarBounds Bounds;

        public CameraShotResult(
            CameraPose pose,
            CameraDamping damping,
            CameraPlane plane,
            CameraShotPolicy policy = CameraShotPolicy.Default,
            CameraPlanarBounds bounds = default)
        {
            Pose = pose;
            Damping = damping;
            Plane = plane;
            Policy = policy;
            Bounds = bounds;
        }
    }

    public struct CameraInputFrame
    {
        public Vector2 PointerPositionPixels;
        public Vector2 PointerNormalized;
        public Vector2 PointerDeltaPixels;
        public bool PanPressed;
        public bool PanHeld;
        public bool PanReleased;
        public bool PointerBlockedByUi;
    }

    public readonly struct CameraEvaluationContext
    {
        public readonly CameraDirector Director;
        public readonly Camera Camera;
        public readonly CameraInputFrame Input;
        public readonly CameraPose CurrentPose;
        public readonly float DeltaTime;
        public readonly float UnscaledDeltaTime;

        public CameraEvaluationContext(
            CameraDirector director,
            Camera camera,
            CameraInputFrame input,
            CameraPose currentPose,
            float deltaTime,
            float unscaledDeltaTime)
        {
            Director = director;
            Camera = camera;
            Input = input;
            CurrentPose = currentPose;
            DeltaTime = deltaTime;
            UnscaledDeltaTime = unscaledDeltaTime;
        }
    }

    public interface ICameraShotSource
    {
        bool TryEvaluate(in CameraEvaluationContext context, out CameraShotResult result);
    }

    public interface ICameraTargetSource : ICameraShotSource
    {
        Transform Target { get; set; }
    }

    [Serializable]
    public struct CameraShotOptions
    {
        public int Priority;
        [Min(0f)] public float BlendIn;
        [Min(0f)] public float BlendOut;
        public string DebugName;

        public CameraShotOptions(int priority, float blendIn, float blendOut, string debugName = null)
        {
            Priority = priority;
            BlendIn = Mathf.Max(0f, blendIn);
            BlendOut = Mathf.Max(0f, blendOut);
            DebugName = debugName;
        }

        public static CameraShotOptions Gameplay(string name = "Gameplay") =>
            new CameraShotOptions(0, 0f, 0.25f, name);

        public static CameraShotOptions Interaction(string name = "Interaction") =>
            new CameraShotOptions(100, 0.25f, 0.25f, name);

        public static CameraShotOptions Ui(string name = "UI Focus") =>
            new CameraShotOptions(200, 0.25f, 0.2f, name);

        public static CameraShotOptions Story(string name = "Story") =>
            new CameraShotOptions(300, 0.35f, 0.3f, name);
    }
}
