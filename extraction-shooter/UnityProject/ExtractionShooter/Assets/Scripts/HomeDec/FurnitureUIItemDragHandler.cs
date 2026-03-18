using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 家具 UI 格子：按下立即生成家具并进入拖拽放置流程。
/// 解决 Button.onClick（抬起才触发）导致“先点一下再去场景拖”的问题。
/// </summary>
public class FurnitureUIItemDragHandler : MonoBehaviour, IPointerDownHandler
{
    private ItemPrefabs itemPrefabs;

    private void Awake()
    {
        itemPrefabs = GetComponent<ItemPrefabs>();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left) return;
        if (itemPrefabs == null) itemPrefabs = GetComponent<ItemPrefabs>();
        if (itemPrefabs == null) return;

        if (FurnitureUIManager.instance != null)
        {
            FurnitureUIManager.instance.BeginPlaceFurniture(itemPrefabs.resourceType);
        }
    }
}

