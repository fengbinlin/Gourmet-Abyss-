using UnityEngine;

namespace GourmetAbyss.CameraSystem
{
    /// <summary>
    /// 场景级绑定点。保存场景拥有的目标和可选边界，但不包含任何玩法判断。
    /// 玩家重生、换人或 Additive 场景切换时只需重新绑定这里。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CameraSceneContext : MonoBehaviour
    {
        [SerializeField] private CameraDirector director;
        [SerializeField] private Transform defaultTarget;
        [SerializeField] private Collider defaultBounds;

        public CameraDirector Director => director;
        public Transform DefaultTarget => defaultTarget;

        private void Awake()
        {
            if (director == null)
                director = GetComponent<CameraDirector>();
        }

        public void BindDefaultTarget(Transform target)
        {
            defaultTarget = target;
        }

        public CameraPlane GetPlane(Vector3 origin)
        {
            Quaternion rotation = director != null
                ? director.CurrentPose.Rotation
                : transform.rotation;
            return CameraPlane.FromRotation(rotation, origin);
        }

        public bool TryGetDefaultBounds(CameraPlane plane, out CameraPlanarBounds bounds)
        {
            if (defaultBounds == null)
            {
                bounds = default;
                return false;
            }

            bounds = CameraPlanarBounds.FromWorldBounds(defaultBounds.bounds, plane);
            return bounds.IsValid;
        }
    }
}
