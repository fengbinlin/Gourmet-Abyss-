using UnityEngine;

public class LookAtCamera : MonoBehaviour
{
    public enum LookMode
    {
        LookAtCamera,      // 看向摄像机
        LookAwayFromCamera, // 背对摄像机
        Billboard,         // 广告牌效果（始终正面朝向摄像机）
        BillboardInverted   // 反向广告牌
    }
    
    [SerializeField] private Camera _lookAtCamera;
    [SerializeField] private LookMode _lookMode = LookMode.LookAtCamera;
    [SerializeField] private bool _lookOnlyOnAwake = false;
    [SerializeField] private bool _invertAxis = false; // 是否反转轴向
    
    private Transform _cameraTransform;
    
    private void Start()
    {
        if (_lookAtCamera == null)
        {
            _lookAtCamera = Camera.main;
        }
        
        if (_lookAtCamera != null)
        {
            _cameraTransform = _lookAtCamera.transform;
        }
        
        if (_lookOnlyOnAwake)
        {
            LookAtTarget();
        }
    }
    
    private void Update()
    {
        if (!_lookOnlyOnAwake && _cameraTransform != null)
        {
            LookAtTarget();
        }
    }
    
    private void LookAtTarget()
    {
        if (_cameraTransform == null) return;
        
        Vector3 direction = _cameraTransform.position - transform.position;
        
        switch (_lookMode)
        {
            case LookMode.LookAtCamera:
                // 看向摄像机
                if (direction != Vector3.zero)
                {
                    Quaternion targetRotation = Quaternion.LookRotation(direction);
                    if (_invertAxis)
                    {
                        // 反转轴向（用于特殊情况）
                        targetRotation *= Quaternion.Euler(0, 180, 0);
                    }
                    transform.rotation = targetRotation;
                }
                break;
                
            case LookMode.LookAwayFromCamera:
                // 背对摄像机
                if (direction != Vector3.zero)
                {
                    transform.rotation = Quaternion.LookRotation(-direction);
                }
                break;
                
            case LookMode.Billboard:
                // 广告牌效果 - 面向摄像机但保持自身向上方向
                transform.rotation = _cameraTransform.rotation;
                break;
                
            case LookMode.BillboardInverted:
                // 反向广告牌
                transform.rotation = _cameraTransform.rotation * Quaternion.Euler(0, 180, 0);
                break;
        }
    }
    
    public void SetTargetCamera(Camera newCamera)
    {
        _lookAtCamera = newCamera;
        if (_lookAtCamera != null)
        {
            _cameraTransform = _lookAtCamera.transform;
        }
    }
    
    public void ForceUpdateLook()
    {
        if (_lookAtCamera == null)
        {
            _lookAtCamera = Camera.main;
        }
        
        if (_lookAtCamera != null)
        {
            _cameraTransform = _lookAtCamera.transform;
            LookAtTarget();
        }
    }
}