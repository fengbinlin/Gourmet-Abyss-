using UnityEngine;
using UnityEngine.EventSystems;

public class SkillTreeDragZoom : MonoBehaviour, IDragHandler, IPointerDownHandler, IScrollHandler
{
    [Header("拖拽设置")]
    [SerializeField] private float dragSensitivity = 1f;
    [SerializeField] private bool enableDrag = true;
    
    [Header("缩放设置")]
    [SerializeField] private float zoomSpeed = 0.5f;
    [SerializeField] private float minZoom = 0.3f;
    [SerializeField] private float maxZoom = 3f;
    [SerializeField] private Vector2 zoomCenterOffset = Vector2.zero; // 缩放中心偏移
    
    [Header("边界限制")]
    [SerializeField] private bool enableBounds = true;
    [SerializeField] private Vector4 bounds = new Vector4(-1000, 1000, -1000, 1000); // 左,右,下,上
    
    private RectTransform rectTransform;
    private Canvas canvas;
    private Vector2 lastDragPosition;
    private Vector2 originalPosition;
    private Vector2 originalScale = Vector2.one;
    
    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        canvas = GetComponentInParent<Canvas>();
        originalPosition = rectTransform.anchoredPosition;
        originalScale = rectTransform.localScale;
    }
    
    public void OnPointerDown(PointerEventData eventData)
    {
        if (enableDrag)
        {
            lastDragPosition = GetMousePositionInCanvas(eventData.position);
        }
    }
    
    public void OnDrag(PointerEventData eventData)
    {
        if (!enableDrag) return;
        
        Vector2 currentMousePos = GetMousePositionInCanvas(eventData.position);
        Vector2 delta = currentMousePos - lastDragPosition;
        
        // 应用拖拽
        rectTransform.anchoredPosition += delta * dragSensitivity;
        
        // 边界限制
        if (enableBounds)
        {
            ClampToBounds();
        }
        
        lastDragPosition = currentMousePos;
    }
    
    public void OnScroll(PointerEventData eventData)
    {
        if (canvas == null) return;
        
        // 计算缩放前后的鼠标位置（相对于技能树中心）
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            rectTransform, 
            eventData.position, 
            canvas.worldCamera, 
            out Vector2 localMousePos
        );
        
        float scrollDelta = eventData.scrollDelta.y;
        float zoomFactor = 1 + scrollDelta * zoomSpeed * 0.1f;
        float newScale = Mathf.Clamp(rectTransform.localScale.x * zoomFactor, minZoom, maxZoom);
        
        // 计算缩放比例
        float scaleChange = newScale / rectTransform.localScale.x;
        
        // 以鼠标位置为中心进行缩放
        Vector2 pivotPosition = localMousePos;
        rectTransform.localScale = Vector3.one * newScale;
        
        // 调整位置，使鼠标指向的点保持在原位
        rectTransform.anchoredPosition += (rectTransform.localScale.x - 1f) * zoomCenterOffset;
        rectTransform.anchoredPosition += (pivotPosition * (1 - scaleChange));
        
        // 边界限制
        if (enableBounds)
        {
            ClampToBounds();
        }
    }
    
    private Vector2 GetMousePositionInCanvas(Vector2 screenPosition)
    {
        if (canvas.renderMode == RenderMode.ScreenSpaceCamera && canvas.worldCamera != null)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvas.GetComponent<RectTransform>(),
                screenPosition,
                canvas.worldCamera,
                out Vector2 localPoint
            );
            return localPoint;
        }
        
        return screenPosition;
    }
    
    private void ClampToBounds()
    {
        Vector2 pos = rectTransform.anchoredPosition;
        Vector2 scale = rectTransform.localScale;
        
        // 计算当前UI的实际边界（考虑缩放）
        float scaledWidth = rectTransform.rect.width * scale.x;
        float scaledHeight = rectTransform.rect.height * scale.y;
        
        // 限制位置
        pos.x = Mathf.Clamp(
            pos.x, 
            bounds.x + scaledWidth * 0.5f, 
            bounds.y - scaledWidth * 0.5f
        );
        
        pos.y = Mathf.Clamp(
            pos.y, 
            bounds.z + scaledHeight * 0.5f, 
            bounds.w - scaledHeight * 0.5f
        );
        
        rectTransform.anchoredPosition = pos;
    }
    
    // 重置位置和缩放
    public void ResetView()
    {
        rectTransform.anchoredPosition = originalPosition;
        rectTransform.localScale = originalScale;
    }
    
    // 设置边界
    public void SetBounds(Vector4 newBounds)
    {
        bounds = newBounds;
    }
    
    // 获取当前边界
    public Vector4 GetCurrentBounds()
    {
        return bounds;
    }
}