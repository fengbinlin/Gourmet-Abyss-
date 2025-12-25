using UnityEngine;

public class SkillTreeCameraController : MonoBehaviour
{
    [Header("相机控制")]
    [SerializeField] private Camera skillTreeCamera;
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float zoomSpeed = 10f;
    [SerializeField] private float minOrthographicSize = 2f;
    [SerializeField] private float maxOrthographicSize = 20f;
    
    [Header("平滑设置")]
    [SerializeField] private float moveSmoothing = 0.1f;
    [SerializeField] private float zoomSmoothing = 0.1f;
    
    [Header("拖拽控制")]
    [SerializeField] private float dragSpeed = 1f;
    
    [Header("边界限制")]
    [SerializeField] private bool enableBounds = true;
    [SerializeField] private Vector2 boundsMin = new Vector2(-10, -10);
    [SerializeField] private Vector2 boundsMax = new Vector2(10, 10);
    [SerializeField] private float edgePadding = 1f;
    
    [Header("输入设置")]
    [SerializeField] private KeyCode dragKey = KeyCode.Mouse2; // 中键拖拽
    [SerializeField] private bool useMiddleMouseDrag = true;
    
    [Header("缩放中心点")]
    [SerializeField] private bool zoomTowardsMouse = true;
    [SerializeField] private Vector2 defaultZoomCenter = Vector2.zero;
    
    [Header("拖拽优化")]
    [SerializeField] private float dragDeadZone = 0.1f; // 新增：拖拽死区，防止微小移动导致跳动
    
    private Vector3 dragOrigin;
    private Vector3 moveVelocity;
    private float zoomVelocity;
    private Vector3 targetPosition;
    private float targetOrthographicSize;
    private bool isDragging = false;
    private Vector3 dragStartCameraPosition; // 新增：记录拖拽开始时相机的位置
    
    private void Start()
    {
        if (skillTreeCamera == null)
        {
            skillTreeCamera = GetComponent<Camera>();
            if (skillTreeCamera == null)
            {
                skillTreeCamera = Camera.main;
            }
        }
        
        targetPosition = skillTreeCamera.transform.position;
        targetOrthographicSize = skillTreeCamera.orthographicSize;
    }
    
    private void Update()
    {
        HandleZoom();
        HandleDrag();
        HandleWASD();
        
        ApplySmoothing();
        ApplyBounds();
    }
    
    private void HandleZoom()
    {
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        
        if (Mathf.Abs(scroll) > 0.01f)
        {
            // 记录缩放前的鼠标位置
            Vector3 mouseWorldPosBeforeZoom = Vector3.zero;
            
            if (zoomTowardsMouse)
            {
                Ray rayBefore = skillTreeCamera.ScreenPointToRay(Input.mousePosition);
                Plane plane = new Plane(Vector3.forward, Vector3.zero);
                
                if (plane.Raycast(rayBefore, out float enter))
                {
                    mouseWorldPosBeforeZoom = rayBefore.GetPoint(enter);
                }
            }
            else
            {
                // 使用默认缩放中心
                mouseWorldPosBeforeZoom = new Vector3(defaultZoomCenter.x, defaultZoomCenter.y, 0);
            }
            
            // 计算目标缩放
            float zoomDelta = -scroll * zoomSpeed;
            float newSize = Mathf.Clamp(
                targetOrthographicSize + zoomDelta,
                minOrthographicSize,
                maxOrthographicSize
            );
            
            // 计算缩放比例
            float zoomRatio = targetOrthographicSize / newSize;
            
            // 计算需要移动的偏移量，以保持鼠标指向点位置不变
            if (zoomTowardsMouse && Mathf.Abs(scroll) > 0.01f)
            {
                Vector3 mouseWorldPos = mouseWorldPosBeforeZoom;
                Vector3 cameraPos = targetPosition;
                
                // 计算缩放后的新位置
                Vector3 newCameraPos = mouseWorldPos - (mouseWorldPos - cameraPos) / zoomRatio;
                
                // 确保缩放中心是有效的点
                if (!float.IsNaN(newCameraPos.x) && !float.IsNaN(newCameraPos.y))
                {
                    targetPosition = newCameraPos;
                }
            }
            
            targetOrthographicSize = newSize;
        }
    }
    
    private void HandleDrag()
    {
        if (useMiddleMouseDrag)
        {
            if (Input.GetKeyDown(dragKey))
            {
                // 修复1：记录拖拽起始点的屏幕坐标而不是世界坐标
                // 这样可以避免在拖拽开始时就计算不准确的世界坐标
                Vector3 mouseScreenPos = Input.mousePosition;
                mouseScreenPos.z = Mathf.Abs(skillTreeCamera.transform.position.z);
                
                // 将屏幕坐标转换为世界坐标
                if (TryGetMouseWorldPosition(mouseScreenPos, out Vector3 worldPos))
                {
                    dragOrigin = worldPos;
                    dragStartCameraPosition = targetPosition; // 记录拖拽开始时相机的目标位置
                    isDragging = true;
                }
            }
            
            if (Input.GetKeyUp(dragKey))
            {
                isDragging = false;
            }
            
            if (isDragging && Input.GetKey(dragKey))
            {
                // 获取当前鼠标的世界坐标
                if (TryGetMouseWorldPosition(Input.mousePosition, out Vector3 currentMousePos))
                {
                    // 计算鼠标移动的偏移量
                    Vector3 move = dragOrigin - currentMousePos;
                    
                    // 修复2：添加死区检查，防止微小移动导致跳动
                    if (move.magnitude > dragDeadZone)
                    {
                        // 修复3：使用拖拽开始时的相机位置作为基准，而不是累加
                        // 这样可以避免因累积误差导致的跳动
                        Vector3 newTargetPosition = dragStartCameraPosition + move * dragSpeed;
                        
                        // 修复4：根据当前缩放级别调整拖拽速度
                        float dragMultiplier = skillTreeCamera.orthographicSize * 0.1f;
                        move *= dragMultiplier;
                        
                        targetPosition = newTargetPosition;
                    }
                }
            }
        }
    }
    
    private void HandleWASD()
    {
        float horizontal = Input.GetAxis("Horizontal");
        float vertical = Input.GetAxis("Vertical");
        
        if (Mathf.Abs(horizontal) > 0.01f || Mathf.Abs(vertical) > 0.01f)
        {
            Vector3 move = new Vector3(horizontal, vertical, 0) * moveSpeed;
            
            // 根据当前缩放级别调整移动速度
            float moveMultiplier = Time.deltaTime * skillTreeCamera.orthographicSize * 0.5f;
            move *= moveMultiplier;
            
            targetPosition += move;
        }
    }
    
    private void ApplySmoothing()
    {
        // 使用平滑阻尼进行位置插值
        skillTreeCamera.transform.position = Vector3.SmoothDamp(
            skillTreeCamera.transform.position,
            targetPosition,
            ref moveVelocity,
            moveSmoothing
        );
        
        // 使用平滑阻尼进行缩放插值
        skillTreeCamera.orthographicSize = Mathf.SmoothDamp(
            skillTreeCamera.orthographicSize,
            targetOrthographicSize,
            ref zoomVelocity,
            zoomSmoothing
        );
    }
    
    private void ApplyBounds()
    {
        if (!enableBounds) return;
        
        // 计算相机可见区域
        float cameraHeight = skillTreeCamera.orthographicSize;
        float cameraWidth = cameraHeight * skillTreeCamera.aspect;
        
        // 计算边界，考虑边距
        float minX = boundsMin.x + cameraWidth - edgePadding;
        float maxX = boundsMax.x - cameraWidth + edgePadding;
        float minY = boundsMin.y + cameraHeight - edgePadding;
        float maxY = boundsMax.y - cameraHeight + edgePadding;
        
        // 如果边界无效（技能树小于视口），将相机居中
        if (minX > maxX)
        {
            minX = maxX = (boundsMin.x + boundsMax.x) * 0.5f;
        }
        
        if (minY > maxY)
        {
            minY = maxY = (boundsMin.y + boundsMax.y) * 0.5f;
        }
        
        // 限制目标位置
        targetPosition.x = Mathf.Clamp(targetPosition.x, minX, maxX);
        targetPosition.y = Mathf.Clamp(targetPosition.y, minY, maxY);
        targetPosition.z = skillTreeCamera.transform.position.z;
        
        // 同时也限制当前相机位置，防止平滑过程中超出边界
        Vector3 currentPos = skillTreeCamera.transform.position;
        currentPos.x = Mathf.Clamp(currentPos.x, minX, maxX);
        currentPos.y = Mathf.Clamp(currentPos.y, minY, maxY);
        skillTreeCamera.transform.position = currentPos;
    }
    
    // 修复5：将GetMouseWorldPosition改为TryGetMouseWorldPosition，添加返回值检查
    private bool TryGetMouseWorldPosition(Vector3 screenPosition, out Vector3 worldPosition)
    {
        worldPosition = Vector3.zero;
        
        Ray ray = skillTreeCamera.ScreenPointToRay(screenPosition);
        Plane plane = new Plane(Vector3.forward, Vector3.zero);
        
        if (plane.Raycast(ray, out float enter))
        {
            worldPosition = ray.GetPoint(enter);
            return true;
        }
        
        return false;
    }
    
    // 原来的方法保留以供其他代码使用，但内部调用新的Try方法
    private Vector3 GetMouseWorldPosition()
    {
        if (TryGetMouseWorldPosition(Input.mousePosition, out Vector3 worldPos))
        {
            return worldPos;
        }
        return Vector3.zero;
    }
    
    public void ResetCamera()
    {
        targetPosition = Vector3.zero;
        targetOrthographicSize = (minOrthographicSize + maxOrthographicSize) * 0.5f;
        
        // 重置平滑速度
        moveVelocity = Vector3.zero;
        zoomVelocity = 0f;
    }
    
    public void FocusOnPosition(Vector3 worldPosition, float zoomSize = -1f)
    {
        targetPosition = new Vector3(worldPosition.x, worldPosition.y, skillTreeCamera.transform.position.z);
        
        if (zoomSize > 0)
        {
            targetOrthographicSize = Mathf.Clamp(zoomSize, minOrthographicSize, maxOrthographicSize);
        }
        
        // 重置平滑速度以获得更平滑的聚焦
        moveVelocity = Vector3.zero;
        zoomVelocity = 0f;
    }
    
    public void SetBounds(Vector2 min, Vector2 max)
    {
        boundsMin = min;
        boundsMax = max;
    }
    
    public void EnableDrag(bool enable)
    {
        useMiddleMouseDrag = enable;
    }
    
    public void SetZoomCenter(Vector2 center)
    {
        defaultZoomCenter = center;
    }
    
    public void EnableZoomTowardsMouse(bool enable)
    {
        zoomTowardsMouse = enable;
    }
    
    #if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (!enableBounds || skillTreeCamera == null) return;
        
        // 绘制边界框
        Gizmos.color = Color.green;
        
        Vector3 center = new Vector3(
            (boundsMin.x + boundsMax.x) * 0.5f,
            (boundsMin.y + boundsMax.y) * 0.5f,
            0
        );
        
        Vector3 size = new Vector3(
            boundsMax.x - boundsMin.x,
            boundsMax.y - boundsMin.y,
            0.1f
        );
        
        Gizmos.DrawWireCube(center, size);
        
        // 绘制当前相机视口范围
        Gizmos.color = Color.yellow;
        
        float cameraHeight = skillTreeCamera.orthographicSize;
        float cameraWidth = cameraHeight * skillTreeCamera.aspect;
        
        Vector3 cameraPos = skillTreeCamera.transform.position;
        Vector3 cameraSize = new Vector3(cameraWidth * 2, cameraHeight * 2, 0.1f);
        
        Gizmos.DrawWireCube(cameraPos, cameraSize);
        
        // 绘制默认缩放中心
        Gizmos.color = Color.cyan;
        Gizmos.DrawSphere(new Vector3(defaultZoomCenter.x, defaultZoomCenter.y, 0), 0.3f);
    }
    #endif
}