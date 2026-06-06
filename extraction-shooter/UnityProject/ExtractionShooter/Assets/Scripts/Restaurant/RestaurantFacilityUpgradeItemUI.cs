using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 挂在 KitchenUpgrade 等升级项根物体上，请在 Inspector 手动绑定 UI 引用。
/// </summary>
public class RestaurantFacilityUpgradeItemUI : MonoBehaviour
{
    [Header("设施类型")]
    [SerializeField] private RestaurantFacilityUpgradeType facilityType;

    [Header("UI 引用（手动绑定）")]
    [SerializeField] private Text title;
    [SerializeField] private Text curLevel;
    [SerializeField] private Text targetLevel;
    [SerializeField] private Text information;
    [SerializeField] private Button buttonUpgrade;
    [SerializeField] private Text needUpgrade;

    public RestaurantFacilityUpgradeType FacilityType => facilityType;

    private void Awake()
    {
        if (buttonUpgrade != null)
            buttonUpgrade.onClick.AddListener(OnUpgradeClicked);
    }

    private void OnDestroy()
    {
        if (buttonUpgrade != null)
            buttonUpgrade.onClick.RemoveListener(OnUpgradeClicked);
    }

    private void OnEnable()
    {
        BindEvents();
        Refresh();
    }

    private void OnDisable()
    {
        UnbindEvents();
    }

    public void Refresh()
    {
        RestaurantFacilityUpgradeManager manager = RestaurantFacilityUpgradeManager.Instance;
        if (manager == null)
            return;

        int level = manager.GetLevel(facilityType);
        bool isMax = manager.IsMaxLevel(facilityType);
        FacilityResourceCost[] costs = manager.GetUpgradeCosts(facilityType);

        if (title != null)
            title.text = manager.GetDisplayName(facilityType);

        if (curLevel != null)
            curLevel.text = level.ToString();

        if (targetLevel != null)
            targetLevel.text = isMax ? "—" : (level + 1).ToString();

        if (information != null)
            information.text = manager.BuildUpgradePreviewText(facilityType);

        if (needUpgrade != null)
            needUpgrade.text = isMax ? "已满级" : RestaurantFacilityConfig.FormatCosts(costs);

        if (buttonUpgrade != null)
            buttonUpgrade.interactable = !isMax && manager.CanAffordUpgrade(facilityType);
    }

    private void OnUpgradeClicked()
    {
        if (RestaurantFacilityUpgradeManager.Instance == null)
            return;

        if (RestaurantFacilityUpgradeManager.Instance.TryUpgrade(facilityType))
            Refresh();
    }

    private void BindEvents()
    {
        if (RestaurantFacilityUpgradeManager.Instance != null)
        {
            RestaurantFacilityUpgradeManager.Instance.OnFacilityLevelChanged -= OnFacilityLevelChanged;
            RestaurantFacilityUpgradeManager.Instance.OnFacilityLevelChanged += OnFacilityLevelChanged;
        }

        if (GameValManager.Instance != null)
        {
            GameValManager.Instance.OnResourceChanged.RemoveListener(OnResourceChanged);
            GameValManager.Instance.OnResourceChanged.AddListener(OnResourceChanged);
        }
    }

    private void UnbindEvents()
    {
        if (RestaurantFacilityUpgradeManager.Instance != null)
            RestaurantFacilityUpgradeManager.Instance.OnFacilityLevelChanged -= OnFacilityLevelChanged;

        if (GameValManager.Instance != null)
            GameValManager.Instance.OnResourceChanged.RemoveListener(OnResourceChanged);
    }

    private void OnFacilityLevelChanged(RestaurantFacilityUpgradeType type, int _)
    {
        if (type == facilityType)
            Refresh();
    }

    private void OnResourceChanged(ResourceType type, int _, int __)
    {
        Refresh();
    }
}
