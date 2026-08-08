using Game.Core;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 设施解锁确认面板（单例）。描述花费、解锁/取消按钮。
/// 解锁花费优先读取 FacilityUnlockable 关联的 RestaurantFacilityConfig。
/// </summary>
public class FacilityUnlockPanelUI : MonoSingleton<FacilityUnlockPanelUI>
{
    [Header("面板")]
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private Text descriptionText;
    [SerializeField] private Button unlockButton;
    [SerializeField] private Button cancelButton;

    private FacilityUnlockable _currentTarget;

    protected override void OnAwake()
    {
        if (unlockButton != null)
            unlockButton.onClick.AddListener(OnUnlockButtonClicked);
        if (cancelButton != null)
            cancelButton.onClick.AddListener(OnCancelButtonClicked);

        Hide();
    }

    // 基类负责清空 Instance；解绑监听对所有实例都要执行，因此放在 OnDestroy 而非 OnSingletonDestroyed。
    protected override void OnDestroy()
    {
        base.OnDestroy();

        if (unlockButton != null)
            unlockButton.onClick.RemoveListener(OnUnlockButtonClicked);
        if (cancelButton != null)
            cancelButton.onClick.RemoveListener(OnCancelButtonClicked);
    }

    private void OnEnable()
    {
        if (GameValManager.Instance != null)
            GameValManager.Instance.OnResourceChanged.AddListener(OnResourceChanged);
    }

    private void OnDisable()
    {
        if (GameValManager.Instance != null)
            GameValManager.Instance.OnResourceChanged.RemoveListener(OnResourceChanged);
    }

    public void Show(FacilityUnlockable target)
    {
        if (target == null || target.IsUnlocked)
        {
            Hide();
            return;
        }

        _currentTarget = target;
        if (panelRoot != null)
            panelRoot.SetActive(true);
        RefreshContent();
    }

    public void Hide()
    {
        _currentTarget = null;
        if (panelRoot != null)
            panelRoot.SetActive(false);
    }

    private void RefreshContent()
    {
        if (_currentTarget == null)
            return;

        FacilityResourceCost[] costs = _currentTarget.UnlockCosts;
        bool canAfford = _currentTarget.CanAffordUnlock();

        if (descriptionText != null)
        {
            string affordHint = canAfford ? "资源足够，可以解锁。" : "资源不足，无法解锁。";
            descriptionText.text =
                $"是否花费 {RestaurantFacilityConfig.FormatCosts(costs)} 解锁「{_currentTarget.DisplayName}」？\n" +
                $"{BuildOwnedResourcesText(costs)}\n{affordHint}";
        }

        if (unlockButton != null)
            unlockButton.interactable = canAfford;
    }

    private static string BuildOwnedResourcesText(FacilityResourceCost[] costs)
    {
        if (costs == null || costs.Length == 0 || GameValManager.Instance == null)
            return string.Empty;

        System.Collections.Generic.List<string> parts = new System.Collections.Generic.List<string>();
        for (int i = 0; i < costs.Length; i++)
        {
            if (costs[i] == null || costs[i].amount <= 0)
                continue;

            int owned = GameValManager.Instance.GetResourceCount(costs[i].resourceType);
            parts.Add($"持有 {owned}");
        }

        return parts.Count > 0 ? string.Join("，", parts) : string.Empty;
    }

    private void OnResourceChanged(ResourceType type, int oldCount, int newCount)
    {
        if (_currentTarget == null)
            return;
        if (panelRoot != null && panelRoot.activeSelf)
            RefreshContent();
    }

    private void OnUnlockButtonClicked()
    {
        if (_currentTarget == null)
        {
            Hide();
            return;
        }

        if (_currentTarget.TryUnlockWithGold())
            Hide();
        else
            RefreshContent();
    }

    private void OnCancelButtonClicked()
    {
        Hide();
    }
}
