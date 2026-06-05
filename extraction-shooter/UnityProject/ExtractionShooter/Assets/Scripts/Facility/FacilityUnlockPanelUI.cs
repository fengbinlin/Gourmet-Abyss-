using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 设施解锁确认面板（单例）。描述花费、解锁/取消按钮。
/// </summary>
public class FacilityUnlockPanelUI : MonoBehaviour
{
    public static FacilityUnlockPanelUI Instance { get; private set; }

    [Header("面板")]
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private Text descriptionText;
    [SerializeField] private Button unlockButton;
    [SerializeField] private Button cancelButton;

    private FacilityUnlockable _currentTarget;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (unlockButton != null)
            unlockButton.onClick.AddListener(OnUnlockButtonClicked);
        if (cancelButton != null)
            cancelButton.onClick.AddListener(OnCancelButtonClicked);

        Hide();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;

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

        int cost = _currentTarget.UnlockCost;
        int owned = GameValManager.Instance != null
            ? GameValManager.Instance.GetResourceCount(ResourceType.Money)
            : 0;
        bool canAfford = _currentTarget.CanAffordUnlock();

        if (descriptionText != null)
        {
            string affordHint = canAfford
                ? "金币足够，可以解锁。"
                : "金币不足，无法解锁。";
            descriptionText.text =
                $"是否花费 {cost} 金币解锁「{_currentTarget.DisplayName}」？\n" +
                $"当前持有：{owned} 金币\n{affordHint}";
        }

        if (unlockButton != null)
            unlockButton.interactable = canAfford;
    }

    private void OnResourceChanged(ResourceType type, int oldCount, int newCount)
    {
        if (type != ResourceType.Money || _currentTarget == null)
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
