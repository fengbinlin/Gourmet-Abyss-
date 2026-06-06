using UnityEngine;
using System;
using System.Collections.Generic;

public class CameraFollow : MonoBehaviour
{
    private static readonly Dictionary<string, float> orthoSizeRequests = new Dictionary<string, float>();
    private static readonly Dictionary<string, float> xFocusRequests = new Dictionary<string, float>();
    private static readonly Dictionary<string, float> yFocusRequests = new Dictionary<string, float>();
    private static float originalOrthoSize = -1f;

    [Header("目标设置")]
    [SerializeField] private Transform target; 
    [SerializeField] private TopDownController playerController;

    [Header("跟随参数")]
    [SerializeField] private float smoothTime = 0.3f; 
    [SerializeField] private Vector3 offset; 
    [SerializeField] private bool autoOffset = true; 
    [Header("正交缩放参数")]
    [SerializeField] private float orthoSizeSmoothTime = 0.18f;
    [Header("交互横向 / 纵向对焦")]
    [SerializeField] private float xFocusSmoothTime = 0.08f;
    [SerializeField] private float yFocusSmoothTime = 0.08f;

    private Vector3 velocity = Vector3.zero; 
    private float orthoSizeVelocity = 0f;
    private float xFocusVelocity = 0f;
    private float yFocusVelocity = 0f;
    private Transform defaultTarget;
    private Transform overrideTarget;
    public event Action OnOverrideClearedByPlayerMove;

    // --- 新增：震动参数 ---
    private float shakeTimer = 0f;
    private float shakeMagnitude = 0f;

    void Start()
    {
        if (target == null) return;
        defaultTarget = target;
        if (autoOffset) offset = transform.position - target.position;
        AutoBindPlayerControllerIfNeeded();
    }

    private void AutoBindPlayerControllerIfNeeded()
    {
        if (playerController != null) return;
        if (defaultTarget == null) return;
        playerController = defaultTarget.GetComponent<TopDownController>();
    }

    void LateUpdate()
    {
        UpdateOrthoSizeSmooth();

        // 如果玩家开始移动，强制回到默认跟随
        if (overrideTarget != null && playerController != null && playerController.IsMoving())
        {
            ClearOverrideTarget(true);
        }

        Transform currentTarget = overrideTarget != null ? overrideTarget : target;
        if (currentTarget == null) return;

        // 1. 计算基础的跟随位置 (平滑处理)
        Vector3 targetPosition = currentTarget.position + offset;
        Vector3 smoothedPosition = Vector3.SmoothDamp(transform.position, targetPosition, ref velocity, smoothTime);
        if (TryGetFocusedX(out float focusX))
        {
            smoothedPosition.x = Mathf.SmoothDamp(
                transform.position.x,
                focusX,
                ref xFocusVelocity,
                Mathf.Max(0.01f, xFocusSmoothTime)
            );
        }

        if (TryGetFocusedY(out float focusY))
        {
            smoothedPosition.y = Mathf.SmoothDamp(
                transform.position.y,
                focusY,
                ref yFocusVelocity,
                Mathf.Max(0.01f, yFocusSmoothTime)
            );
        }

        // 2. 叠加震动效果 (如果有震动时间剩余)
        if (shakeTimer > 0)
        {
            // 在球体内随机取一个点作为偏移
            Vector3 shakeOffset = UnityEngine.Random.insideUnitSphere * shakeMagnitude;
            smoothedPosition += shakeOffset;

            shakeTimer -= Time.deltaTime;
        }

        // 3. 应用最终位置
        transform.position = smoothedPosition;
    }

    /// <summary>
    /// 临时切换相机跟随目标（例如 UI 选中某个点位）。
    /// 注意：offset 不会改变，保持当前相机相对位移。
    /// </summary>
    public void SetOverrideTarget(Transform newTarget)
    {
        if (newTarget == null) return;
        overrideTarget = newTarget;
    }

    /// <summary>
    /// 清除临时跟随目标，回到默认 target（通常是 Player）。
    /// </summary>
    public void ClearOverrideTarget()
    {
        ClearOverrideTarget(false);
    }

    private void ClearOverrideTarget(bool clearedByPlayerMove)
    {
        if (overrideTarget == null) return;
        overrideTarget = null;
        if (clearedByPlayerMove)
        {
            OnOverrideClearedByPlayerMove?.Invoke();
        }
    }

    /// <summary>
    /// 允许外部在运行时重设默认目标（如换人/重生）。
    /// </summary>
    public void SetDefaultTarget(Transform newDefault)
    {
        if (newDefault == null) return;
        defaultTarget = newDefault;
        target = newDefault;
        AutoBindPlayerControllerIfNeeded();
        if (autoOffset) offset = transform.position - newDefault.position;
    }

    /// <summary>
    /// 公开方法：触发屏幕震动
    /// </summary>
    /// <param name="duration">震动持续时间 (秒)</param>
    /// <param name="magnitude">震动强度 (位移距离)</param>
    public void Shake(float duration, float magnitude)
    {
        shakeTimer = duration;
        shakeMagnitude = magnitude;
    }

    public static void PushOrthoSizeRequest(string requestKey, float targetSize)
    {
        if (string.IsNullOrEmpty(requestKey)) return;
        Camera cam = FindActiveMainCamera();
        if (cam == null || !cam.orthographic) return;

        if (originalOrthoSize < 0f)
        {
            originalOrthoSize = cam.orthographicSize;
        }

        orthoSizeRequests[requestKey] = Mathf.Max(0.01f, targetSize);
    }

    public static void PopOrthoSizeRequest(string requestKey)
    {
        if (string.IsNullOrEmpty(requestKey)) return;
        if (!orthoSizeRequests.Remove(requestKey)) return;

        Camera cam = FindActiveMainCamera();
        if (cam == null || !cam.orthographic) return;
    }

    public static void PushXFocusRequest(string requestKey, float worldX)
    {
        if (string.IsNullOrEmpty(requestKey)) return;
        xFocusRequests[requestKey] = worldX;
    }

    public static void PopXFocusRequest(string requestKey)
    {
        if (string.IsNullOrEmpty(requestKey)) return;
        xFocusRequests.Remove(requestKey);
    }

    public static void PushYFocusRequest(string requestKey, float worldY)
    {
        if (string.IsNullOrEmpty(requestKey)) return;
        yFocusRequests[requestKey] = worldY;
    }

    public static void PopYFocusRequest(string requestKey)
    {
        if (string.IsNullOrEmpty(requestKey)) return;
        yFocusRequests.Remove(requestKey);
    }

    /// <summary>离开餐厅等场景时立即回到默认跟随，避免 SmoothDamp 残留导致视角不归位。</summary>
    public void SnapBackToDefaultFollow(float? orthoSize = null, Vector3? worldPosition = null)
    {
        ClearOverrideTarget();

        velocity = Vector3.zero;
        xFocusVelocity = 0f;
        yFocusVelocity = 0f;
        orthoSizeVelocity = 0f;

        Transform followTarget = target != null ? target : defaultTarget;
        if (worldPosition.HasValue)
            transform.position = worldPosition.Value;
        else if (followTarget != null)
            transform.position = followTarget.position + offset;

        Camera cam = GetComponent<Camera>();
        if (cam == null)
            cam = FindActiveMainCamera();
        if (cam == null || !cam.orthographic)
            return;

        if (orthoSize.HasValue)
        {
            cam.orthographicSize = Mathf.Max(0.01f, orthoSize.Value);
            if (orthoSizeRequests.Count == 0)
                originalOrthoSize = -1f;
        }
        else if (orthoSizeRequests.Count == 0 && originalOrthoSize > 0f)
        {
            cam.orthographicSize = originalOrthoSize;
            originalOrthoSize = -1f;
        }
    }

    private void UpdateOrthoSizeSmooth()
    {
        Camera cam = FindActiveMainCamera();
        if (cam == null || !cam.orthographic) return;

        float desiredSize = cam.orthographicSize;
        if (orthoSizeRequests.Count > 0)
        {
            if (originalOrthoSize < 0f)
            {
                originalOrthoSize = cam.orthographicSize;
            }

            float minSize = float.MaxValue;
            foreach (var kv in orthoSizeRequests)
            {
                if (kv.Value < minSize) minSize = kv.Value;
            }

            if (minSize < float.MaxValue)
            {
                desiredSize = minSize;
            }
        }
        else if (originalOrthoSize > 0f)
        {
            desiredSize = originalOrthoSize;
        }

        cam.orthographicSize = Mathf.SmoothDamp(
            cam.orthographicSize,
            desiredSize,
            ref orthoSizeVelocity,
            Mathf.Max(0.01f, orthoSizeSmoothTime)
        );

        if (orthoSizeRequests.Count == 0 && originalOrthoSize > 0f && Mathf.Abs(cam.orthographicSize - originalOrthoSize) < 0.001f)
        {
            originalOrthoSize = -1f;
        }
    }

    private static Camera FindActiveMainCamera()
    {
        if (Camera.main != null && Camera.main.isActiveAndEnabled && Camera.main.gameObject.activeInHierarchy)
        {
            return Camera.main;
        }

        Camera[] allCameras = GameObject.FindObjectsOfType<Camera>(true);
        for (int i = 0; i < allCameras.Length; i++)
        {
            Camera c = allCameras[i];
            if (c == null) continue;
            if (!c.CompareTag("MainCamera")) continue;
            if (!c.gameObject.activeInHierarchy || !c.isActiveAndEnabled) continue;
            return c;
        }

        return null;
    }

    private bool TryGetFocusedX(out float focusedX)
    {
        focusedX = 0f;
        if (xFocusRequests.Count == 0) return false;

        // 多请求时取平均，避免跳变；当前场景通常只有一个请求
        float sum = 0f;
        int count = 0;
        foreach (var kv in xFocusRequests)
        {
            sum += kv.Value;
            count++;
        }
        if (count <= 0) return false;

        focusedX = sum / count;
        return true;
    }

    private bool TryGetFocusedY(out float focusedY)
    {
        focusedY = 0f;
        if (yFocusRequests.Count == 0) return false;

        float sum = 0f;
        int count = 0;
        foreach (var kv in yFocusRequests)
        {
            sum += kv.Value;
            count++;
        }
        if (count <= 0) return false;

        focusedY = sum / count;
        return true;
    }
}