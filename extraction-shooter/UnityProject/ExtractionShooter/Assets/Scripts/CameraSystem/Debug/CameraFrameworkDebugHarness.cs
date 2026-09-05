using UnityEngine;

namespace GourmetAbyss.CameraSystem
{
    /// <summary>运行时手工验收工具。只在调试时由编辑器菜单动态挂载，不写入场景。</summary>
    public sealed class CameraFrameworkDebugHarness : MonoBehaviour
    {
        private CameraDirector _director;
        private CameraShotLease _debugLease;
        private Transform _target;
        private GameObject _debugFocusObject;
        private string _mode = "Normal";
        private bool _showOverlay = true;

        private void Start()
        {
            _director = CameraService.Active != null ? CameraService.Active : GetComponent<CameraDirector>();
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            _target = player != null ? player.transform : null;
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.F1)) SetNormal();
            if (Input.GetKeyDown(KeyCode.F2)) SetTown();
            if (Input.GetKeyDown(KeyCode.F3)) SetDungeon();
            if (Input.GetKeyDown(KeyCode.F4)) SetRestaurant();
            if (Input.GetKeyDown(KeyCode.F5)) SetFocus();
            if (Input.GetKeyDown(KeyCode.F6)) CameraService.PlayImpulse(0.35f, 0.25f, 22f);
            if (Input.GetKeyDown(KeyCode.F8)) _showOverlay = !_showOverlay;
        }

        private void OnDisable()
        {
            _debugLease?.Dispose();
            _debugLease = null;
            if (_debugFocusObject != null)
                Destroy(_debugFocusObject);
        }

        private void SetNormal()
        {
            ReplaceLease(null, "Normal");
        }

        private void SetTown()
        {
            if (!EnsureReady()) return;
            CameraPose pose = _director.CurrentPose;
            Vector3 offset = pose.Position - _target.position;
            TownFollowCameraSource source = new TownFollowCameraSource(
                _target,
                () => _target.right,
                offset,
                pose.Rotation,
                pose.OrthographicSize,
                1.5f,
                0.15f,
                new CameraDamping(0.2f));
            ReplaceLease(
                _director.AcquireShot(this, source, new CameraShotOptions(900, 0.2f, 0.2f, "Debug Town")),
                "Town：移动/转动目标观察朝向前瞻");
        }

        private void SetDungeon()
        {
            if (!EnsureReady()) return;
            CameraPose pose = _director.CurrentPose;
            DungeonAimCameraSource source = new DungeonAimCameraSource(
                _target,
                pose.Position - _target.position,
                pose.Rotation,
                pose.OrthographicSize,
                0.1f,
                3f,
                1.4f,
                0.12f,
                new CameraDamping(0.18f));
            ReplaceLease(
                _director.AcquireShot(this, source, new CameraShotOptions(900, 0.2f, 0.2f, "Debug Dungeon")),
                "Dungeon：移动鼠标观察有死区的限幅偏移");
        }

        private void SetRestaurant()
        {
            if (!EnsureReady()) return;
            CameraPose pose = _director.CurrentPose;
            CameraPlane plane = CameraPlane.FromRotation(pose.Rotation, _target.position);
            CameraPlanarBounds bounds = new CameraPlanarBounds(new Vector2(-15f, -10f), new Vector2(15f, 10f));
            RestaurantPanCameraSource source = new RestaurantPanCameraSource(
                _target,
                pose,
                pose.OrthographicSize,
                1f,
                true,
                new CameraDamping(0.12f),
                _ => bounds);
            ReplaceLease(
                _director.AcquireShot(this, source, new CameraShotOptions(900, 0.2f, 0.2f, "Debug Restaurant")),
                "Restaurant：按住鼠标中键拖动，验证边界");
        }

        private void SetFocus()
        {
            if (!EnsureReady()) return;
            CameraPose pose = _director.CurrentPose;
            CameraPlane plane = CameraPlane.FromRotation(pose.Rotation, _target.position);
            if (_debugFocusObject == null)
            {
                _debugFocusObject = new GameObject("~CameraDebugFocus");
                _debugFocusObject.hideFlags = HideFlags.HideAndDontSave;
            }
            _debugFocusObject.transform.position = _target.position + plane.Right * 5f + plane.Up * 2f;

            TransformFocusCameraSource source = new TransformFocusCameraSource(
                _debugFocusObject.transform,
                pose,
                Mathf.Max(2f, pose.OrthographicSize * 0.7f),
                new CameraDamping(0.15f),
                CameraShotPolicy.UseUnscaledTime);
            ReplaceLease(
                _director.AcquireShot(this, source, new CameraShotOptions(950, 0.3f, 0.3f, "Debug Focus")),
                "Focus：验证高优先级聚焦及 F1 返回");
        }

        private bool EnsureReady()
        {
            if (_director == null)
                _director = CameraService.Active;
            if (_target == null)
            {
                GameObject player = GameObject.FindGameObjectWithTag("Player");
                _target = player != null ? player.transform : null;
            }
            if (_director != null && _target != null)
                return true;

            Debug.LogWarning("[CameraDebug] 缺少 CameraDirector 或 Player。", this);
            return false;
        }

        private void ReplaceLease(CameraShotLease next, string mode)
        {
            _debugLease?.Dispose();
            _debugLease = next;
            _mode = mode;
        }

        private void OnGUI()
        {
            if (!_showOverlay) return;
            GUI.Box(new Rect(16, 16, 560, 142), "料理地牢 Camera Framework Debug");
            GUI.Label(new Rect(30, 44, 530, 24), "F1 正常  F2 小镇  F3 地牢  F4 餐厅  F5 聚焦  F6 震屏  F8 隐藏");
            GUI.Label(new Rect(30, 70, 530, 24), $"当前：{_mode}");
            string summary = _director != null ? _director.GetDebugSummary() : "CameraDirector not found";
            GUI.Label(new Rect(30, 96, 530, 48), summary);
        }
    }
}
