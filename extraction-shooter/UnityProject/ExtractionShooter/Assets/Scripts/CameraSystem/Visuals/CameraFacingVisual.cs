using UnityEngine;

namespace GourmetAbyss.CameraSystem
{
    /// <summary>
    /// 挂在纯视觉子节点上，使 2D 素材平面始终垂直于镜头方向。
    /// 物理根节点、Collider、Rigidbody 和 NavMeshAgent 不应挂本组件。
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(1300)]
    public sealed class CameraFacingVisual : MonoBehaviour
    {
        public enum UpdateMode
        {
            OnceOnEnable,
            EveryLateUpdate
        }

        [SerializeField] private Camera targetCamera;
        [SerializeField] private UpdateMode updateMode = UpdateMode.OnceOnEnable;
        [Tooltip("部分 Sprite/Quad 的正面法线相反时启用")]
        [SerializeField] private bool reverseForward;
        [SerializeField] private Vector3 additionalEulerAngles;

        private void OnEnable()
        {
            AlignToCamera();
        }

        private void LateUpdate()
        {
            if (updateMode == UpdateMode.EveryLateUpdate)
                AlignToCamera();
        }

        public void AlignToCamera()
        {
            if (targetCamera == null)
                targetCamera = CameraService.Active != null ? CameraService.Active.Camera : Camera.main;
            if (targetCamera == null)
                return;

            Quaternion cameraRotation = targetCamera.transform.rotation;
            if (reverseForward)
                cameraRotation *= Quaternion.Euler(0f, 180f, 0f);
            transform.rotation = cameraRotation * Quaternion.Euler(additionalEulerAngles);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (GetComponent<Rigidbody>() != null || GetComponent<Collider>() != null)
            {
                Debug.LogWarning(
                    $"[CameraFacingVisual] {name} 同时含物理组件。请把 CameraFacingVisual 移到独立 VisualRoot 子节点。",
                    this);
            }
        }
#endif
    }
}
