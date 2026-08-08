using Game.Core;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 挂在 DecorationPanel 上，请在 Inspector 手动绑定面板、按钮与升级项。
/// BDecoration 也可在 OnClick 中直接绑定 TogglePanel()。
/// </summary>
public class RestaurantDecorationPanelUI : MonoSingleton<RestaurantDecorationPanelUI>
{
    // 每个场景各一份，后加载的接管——沿用原来的裸赋值语义。
    protected override DuplicatePolicy Duplicate => DuplicatePolicy.OverwriteReference;

    [Header("面板")]
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private bool startHidden = true;

    [Header("显隐按钮（BDecoration，可留空改用手动 OnClick）")]
    [SerializeField] private Button toggleButton;

    [Header("升级项（手动拖入 4 个 KitchenUpgrade 等）")]
    [SerializeField] private RestaurantFacilityUpgradeItemUI[] upgradeItems;

    protected override void OnAwake()
    {
        if (panelRoot == null)
            panelRoot = gameObject;

        if (toggleButton != null)
            toggleButton.onClick.AddListener(TogglePanel);

        if (startHidden && panelRoot != null)
            panelRoot.SetActive(false);
    }

    private void OnEnable()
    {
        RefreshAllItems();
    }

    // 基类负责清空 Instance；解绑监听对所有实例都要执行。
    protected override void OnDestroy()
    {
        base.OnDestroy();

        if (toggleButton != null)
            toggleButton.onClick.RemoveListener(TogglePanel);
    }

    public void TogglePanel()
    {
        if (panelRoot == null)
            return;

        bool show = !panelRoot.activeSelf;
        panelRoot.SetActive(show);
        if (show)
            RefreshAllItems();
    }

    public void ShowPanel()
    {
        if (panelRoot == null)
            return;

        panelRoot.SetActive(true);
        RefreshAllItems();
    }

    public void HidePanel()
    {
        if (panelRoot == null)
            return;

        panelRoot.SetActive(false);
    }

    public void RefreshAllItems()
    {
        if (upgradeItems == null)
            return;

        for (int i = 0; i < upgradeItems.Length; i++)
        {
            if (upgradeItems[i] != null)
                upgradeItems[i].Refresh();
        }
    }
}
