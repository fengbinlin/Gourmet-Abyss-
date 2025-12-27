// MapItemUI.cs
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class MapItemUI : MonoBehaviour, IPointerClickHandler
{
    [Header("UI References")]
    [SerializeField] private Image normalImage; // 正常显示图片
    [SerializeField] private Image lockImage;   // 未解锁图片
    [SerializeField] private Image selectImage; // 选中图片
    [SerializeField] private Text nameText;     // 地图名称文本
    
    private MapData mapData;
    private MapUIManager mapManager;
    private bool isSelected = false;
    
    public void Initialize(MapData data, MapUIManager manager)
    {
        mapData = data;
        mapManager = manager;
        
        UpdateUI();
    }
    
    // 更新UI显示
    public void UpdateUI()
    {
        if (mapData == null) return;
        
        // 设置名称
        if (nameText != null)
        {
            nameText.text = mapData.mapName;
        }
        
        // 根据状态更新显示
        if (!mapData.isUnlocked)
        {
            SetLocked(true);
            SetSelected(false);
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
            //ormalImage.gameObject.SetActive(!locked);
        }
    }
    
    // 设置选中状态
    public void SetSelected(bool selected)
    {
        isSelected = selected;
        
        if (selectImage != null)
        {
            selectImage.gameObject.SetActive(selected);
        }
    }
    public void MClick()
    {
        if (!mapData.isUnlocked) return;
        print("方法点击");
        mapManager.SelectMap(mapData);
    }
    // 鼠标点击事件
    public void OnPointerClick(PointerEventData eventData)
    {
        if (!mapData.isUnlocked) return;
        print("鼠标点击");
        mapManager.SelectMap(mapData);
    }
}