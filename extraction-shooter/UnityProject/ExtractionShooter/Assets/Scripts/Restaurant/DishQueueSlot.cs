using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 餐厅烹饪排队 UI 单槽：背景、菜肴图标、数量文本。激活数量由 WeaponStatsManager 控制；
/// 只有列表中第一个槽位上的菜会下锅，其余槽为缓冲；队首煮完或取走后整列左移。
/// </summary>
public class DishQueueSlot : MonoBehaviour
{
    [Header("槽位 UI")]
    [SerializeField] private Image slotBackground;
    [SerializeField] private Image dishIcon;
    [SerializeField] private Sprite lookIcon;
    [SerializeField] private Text itemCountText;

    public void SetVisual(DishRecipe recipe, int count)
    {
        bool has = recipe != null && count > 0;

        if (slotBackground != null)
            slotBackground.enabled = true;

        if (dishIcon != null)
        {
            dishIcon.enabled = has && recipe.dishIcon != null;
            if (has && recipe.dishIcon != null)
                dishIcon.sprite = recipe.dishIcon;
        }

        if (itemCountText != null)
        {
            itemCountText.enabled = true; // 正常槽位显示数量
            itemCountText.text = has ? count.ToString() : string.Empty;
        }
    }

    public void SetEmpty()
    {
        SetVisual(null, 0);
    }

    /// <summary>
    /// 仅用于“多出来的一个空槽”：显示 LookIcon，但隐藏 itemCountText。
    /// </summary>
    public void SetLookEmpty()
    {
        // 背景显示 LookIcon
        if (slotBackground != null)
        {
            slotBackground.enabled = true;
            if (lookIcon != null)
                slotBackground.sprite = lookIcon;
        }

        // 多出来的槽不再使用菜肴图标
        if (dishIcon != null)
            dishIcon.enabled = false;

        if (itemCountText != null)
        {
            itemCountText.enabled = false;
            itemCountText.text = string.Empty;
        }
    }
}
