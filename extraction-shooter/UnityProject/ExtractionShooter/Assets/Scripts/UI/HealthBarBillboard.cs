using UnityEngine;

public class HealthBarBillboard : MonoBehaviour
{
    public enum BillboardType
    {
        CameraForward,  // 朝向摄像机前方
        CameraPosition, // 直接朝向摄像机位置
        YAxisOnly,      // 只绕Y轴旋转
        XAxisOnly       // 只绕X轴旋转
    }
    
    [Header("摄像机设置")]
    [Tooltip("目标摄像机，为空则使用主摄像机")]
    public Camera targetCamera;
    
    [Header("朝向类型")]
    [Tooltip("选择朝向方式")]
    public BillboardType billboardType = BillboardType.CameraPosition;
    
    [Header("轴锁定")]
    [Tooltip("锁定X轴旋转")]
    public bool lockX = false;
    [Tooltip("锁定Y轴旋转")]
    public bool lockY = false;
    [Tooltip("锁定Z轴旋转")]
    public bool lockZ = false;
    
    [Header("其他设置")]
    [Tooltip("是否在LateUpdate中更新")]
    public bool useLateUpdate = true;
    [Tooltip("是否忽略摄像机的俯仰旋转")]
    public bool ignoreCameraPitch = false;
    
    private Transform _cameraTransform;
    private Vector3 _originalEulerAngles;
    
    void Start()
    {
        // 保存初始旋转
        _originalEulerAngles = transform.eulerAngles;
        
        // 设置摄像机
        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }
        
        if (targetCamera != null)
        {
            _cameraTransform = targetCamera.transform;
        }
        else
        {
            Debug.LogWarning("HealthBarBillboard: 没有找到摄像机，将自动搜索主摄像机");
        }
    }
    
    void Update()
    {
        if (!useLateUpdate)
        {
            UpdateBillboard();
        }
    }
    
    void LateUpdate()
    {
        if (useLateUpdate)
        {
            UpdateBillboard();
        }
    }
    
    void UpdateBillboard()
    {
        if (_cameraTransform == null)
        {
            // 每帧尝试查找摄像机
            if (targetCamera == null)
            {
                targetCamera = Camera.main;
            }
            
            if (targetCamera != null)
            {
                _cameraTransform = targetCamera.transform;
            }
            else
            {
                return;
            }
        }
        
        Vector3 targetPosition = _cameraTransform.position;
        
        switch (billboardType)
        {
            case BillboardType.CameraForward:
                // 朝向摄像机前方
                transform.forward = -_cameraTransform.forward;
                break;
                
            case BillboardType.CameraPosition:
                // 直接朝向摄像机位置
                Vector3 direction = transform.position - targetPosition;
                transform.rotation = Quaternion.LookRotation(direction);
                break;
                
            case BillboardType.YAxisOnly:
                // 只绕Y轴旋转
                Vector3 lookPos = targetPosition;
                lookPos.y = transform.position.y; // 保持相同高度
                transform.LookAt(lookPos);
                break;
                
            case BillboardType.XAxisOnly:
                // 只绕X轴旋转
                Vector3 lookPosX = targetPosition;
                lookPosX.x = transform.position.x; // 保持相同X位置
                transform.LookAt(lookPosX);
                break;
        }
        
        // 应用轴锁定
        Vector3 euler = transform.eulerAngles;
        if (lockX) euler.x = _originalEulerAngles.x;
        if (lockY) euler.y = _originalEulerAngles.y;
        if (lockZ) euler.z = _originalEulerAngles.z;
        
        // 忽略摄像机的俯仰旋转
        if (ignoreCameraPitch)
        {
            euler.x = 0;
        }
        
        transform.eulerAngles = euler;
    }
    
    // 强制更新摄像机引用
    public void SetTargetCamera(Camera newCamera)
    {
        targetCamera = newCamera;
        if (targetCamera != null)
        {
            _cameraTransform = targetCamera.transform;
        }
    }
    
    // 强制更新一次朝向
    public void ForceUpdate()
    {
        UpdateBillboard();
    }
}