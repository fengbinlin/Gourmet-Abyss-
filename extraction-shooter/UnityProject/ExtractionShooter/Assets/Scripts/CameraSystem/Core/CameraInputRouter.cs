using UnityEngine;
using UnityEngine.EventSystems;

namespace GourmetAbyss.CameraSystem
{
    /// <summary>
    /// 将具体输入设备转换为稳定的相机输入快照。镜头源只依赖 CameraInputFrame，
    /// 后续接入新 Input System、手柄或触屏时无需修改相机核心与镜头源。
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public sealed class CameraInputRouter : MonoBehaviour
    {
        [SerializeField, Range(0, 2)] private int panMouseButton = 2;
        [SerializeField] private bool blockAimWhenPointerOverUi = true;

        private CameraInputFrame _frame;
        private bool _debugOverrideEnabled;
        private CameraInputFrame _debugFrame;
        private Vector2 _lastPointerPosition;
        private bool _hasPointerSample;

        public CameraInputFrame CurrentFrame => _debugOverrideEnabled ? _debugFrame : _frame;

        private void Update()
        {
            float width = Mathf.Max(1f, Screen.width);
            float height = Mathf.Max(1f, Screen.height);
            Vector2 pointer = Input.mousePosition;
            Vector2 pointerDelta = _hasPointerSample ? pointer - _lastPointerPosition : Vector2.zero;
            _lastPointerPosition = pointer;
            _hasPointerSample = true;

            bool blockedByUi = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
            Vector2 normalized = new Vector2(
                (pointer.x / width - 0.5f) * 2f,
                (pointer.y / height - 0.5f) * 2f);

            if (blockAimWhenPointerOverUi && blockedByUi)
                normalized = Vector2.zero;

            _frame = new CameraInputFrame
            {
                PointerNormalized = Vector2.ClampMagnitude(normalized, 1f),
                PointerDeltaPixels = pointerDelta,
                PanPressed = Input.GetMouseButtonDown(panMouseButton),
                PanHeld = Input.GetMouseButton(panMouseButton),
                PanReleased = Input.GetMouseButtonUp(panMouseButton),
                PointerBlockedByUi = blockedByUi
            };
        }

        public void SetDebugOverride(CameraInputFrame frame)
        {
            _debugFrame = frame;
            _debugOverrideEnabled = true;
        }

        public void ClearDebugOverride()
        {
            _debugOverrideEnabled = false;
            _debugFrame = default;
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus)
            {
                _hasPointerSample = false;
                _frame = default;
            }
        }
    }
}
