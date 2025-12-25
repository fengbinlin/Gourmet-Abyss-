// MapUIManager.cs
using UnityEngine;
using UnityEngine.EventSystems;


public class CreatureItemDisplay : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    private CreatureData creatureData;
    private GameObject tooltip;
    
    public void Initialize(CreatureData data)
    {
        creatureData = data;
    }
    
    public void OnPointerEnter(PointerEventData eventData)
    {
        // 显示详细信息Tooltip
        ShowTooltip();
    }
    
    public void OnPointerExit(PointerEventData eventData)
    {
        HideTooltip();
    }
    
    private void ShowTooltip()
    {
        // 创建或显示Tooltip
        // 这里可以根据需要实现Tooltip显示逻辑
    }
    
    private void HideTooltip()
    {
        // 隐藏Tooltip
    }
}