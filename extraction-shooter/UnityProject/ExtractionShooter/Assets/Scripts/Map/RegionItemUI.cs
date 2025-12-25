// RegionItemUI.cs
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class RegionItemUI : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
    [Header("UI References")]
    [SerializeField] private Image normalImage; // 正常显示图片
    [SerializeField] private Image lockImage;   // 未解锁图片
    [SerializeField] private Image selectImage; // 选中图片
    [SerializeField] private Text nameText;     // 区域名称文本
    
    private RegionData regionData;
    private MapUIManager mapManager;
    private bool isHovered = false;
    
    public void Initialize(RegionData data, MapUIManager manager)
    {
        regionData = data;
        mapManager = manager;
        
        UpdateUI();
    }
    
    // 更新UI显示
    public void UpdateUI()
    {
        if (regionData == null) return;
        
        // 设置名称
        if (nameText != null)
        {
            nameText.text = regionData.regionName;
        }
        
        // 根据状态更新显示
        if (!regionData.isUnlocked)
        {
            SetLocked(true);
            SetHovered(false);
        }
        else
        {
            SetLocked(false);
        }
    }
    
    // 设置锁定状态
    public void SetLocked(bool locked)
    {
        if (lockImage != null)
        {
            lockImage.gameObject.SetActive(locked);
        }
        
        if (normalImage != null)
        {
            normalImage.gameObject.SetActive(!locked);
        }
    }
    
    // 设置悬停状态
    public void SetHovered(bool hovered)
    {
        isHovered = hovered;
        
        if (selectImage != null)
        {
            selectImage.gameObject.SetActive(hovered);
        }
    }
    
    // 鼠标点击事件
    public void OnPointerClick(PointerEventData eventData)
    {
        if (!regionData.isUnlocked) return;
        
        mapManager.EnterRegion(regionData);
    }
    
    // 鼠标进入事件
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!regionData.isUnlocked) return;
        
        mapManager.OnRegionHover(regionData);
    }
    public void MClick()
    {
        if (!regionData.isUnlocked) return;
        
        mapManager.EnterRegion(regionData);
    }
    // 鼠标离开事件
    public void OnPointerExit(PointerEventData eventData)
    {
        if (!regionData.isUnlocked) return;
        
        mapManager.OnRegionHoverEnd(regionData);
    }
}