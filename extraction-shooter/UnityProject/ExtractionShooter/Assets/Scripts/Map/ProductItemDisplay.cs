// MapUIManager.cs
using UnityEngine;
using UnityEngine.EventSystems;


public class ProductItemDisplay : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    private ProductData productData;
    private GameObject tooltip;
    
    public void Initialize(ProductData data)
    {
        productData = data;
    }
    
    public void OnPointerEnter(PointerEventData eventData)
    {
        ShowTooltip();
    }
    
    public void OnPointerExit(PointerEventData eventData)
    {
        HideTooltip();
    }
    
    private void ShowTooltip()
    {
        // 显示Tooltip
    }
    
    private void HideTooltip()
    {
        // 隐藏Tooltip
    }
}