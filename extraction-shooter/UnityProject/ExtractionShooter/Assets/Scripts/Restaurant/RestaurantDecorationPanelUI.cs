using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 挂在 DecorationPanel 上，请在 Inspector 手动绑定面板、按钮与升级项。
/// BDecoration 也可在 OnClick 中直接绑定 TogglePanel()。
/// </summary>
public class RestaurantDecorationPanelUI : MonoBehaviour
{
    public static RestaurantDecorationPanelUI Instance { get; private set; }

    [Header("面板")]
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private bool startHidden = true;

    [Header("显隐按钮（BDecoration，可留空改用手动 OnClick）")]
    [SerializeField] private Button toggleButton;

    [Header("升级项（手动拖入 4 个 KitchenUpgrade 等）")]
    [SerializeField] private RestaurantFacilityUpgradeItemUI[] upgradeItems;

    private void Awake()
    {
        Instance = this;

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

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;

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
