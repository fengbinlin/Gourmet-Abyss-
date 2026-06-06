using UnityEngine;

/// <summary>
/// 家具 UI 打开时自动刷新列表（仿照 BagRefreshOnEnable）
/// </summary>
public class FurnitureUIRefreshOnEnable : MonoBehaviour
{
    public FurnitureUIManager furnitureUIManager;

    private void OnEnable()
    {
        if (furnitureUIManager != null)
        {
            furnitureUIManager.GenerateItems();
        }
    }
}

