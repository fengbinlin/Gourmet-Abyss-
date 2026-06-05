using UnityEngine;

/// <summary>
/// 碟子、锅、桌子等设施的金币解锁逻辑。请手动挂到设施根物体，并配置 Click Target / Content。
/// </summary>
public class FacilityUnlockable : MonoBehaviour
{
    public static event System.Action<FacilityUnlockable> OnFacilityUnlocked;

    [Header("设施类型")]
    [SerializeField] private FacilityType facilityType = FacilityType.Plate;

    [Header("显示引用")]
    [Tooltip("未解锁时显示、供玩家点击（挂 FacilityUnlockClickTarget + Collider，勿指向根物体）")]
    [SerializeField] private GameObject unlockClickTarget;
    [Tooltip("解锁后才显示的功能内容（碟子/锅/桌子模型与 UI）；未解锁时隐藏")]
    [SerializeField] private GameObject content;

    [Header("解锁配置")]
    [SerializeField] private int unlockCost = 100;
    [SerializeField] private string facilityDisplayName = "设施";
    [Tooltip("勾选则进入场景时视为已解锁（不写入本地存档）")]
    [SerializeField] private bool unlockedByDefault;

    private bool _isUnlocked;

    public FacilityType Type => facilityType;
    public int UnlockCost => Mathf.Max(0, unlockCost);
    public string DisplayName => string.IsNullOrEmpty(facilityDisplayName) ? gameObject.name : facilityDisplayName;
    public bool IsUnlocked => _isUnlocked;

    public void RefreshVisualState()
    {
        ApplyVisualState();
    }

    public void SetFacilityType(FacilityType type)
    {
        facilityType = type;
    }

    private void Awake()
    {
        _isUnlocked = unlockedByDefault;
        ApplyVisualState();
    }

    private void OnEnable()
    {
        ApplyVisualState();
    }

    private void Start()
    {
        ApplyVisualState();
        NotifyRestaurantListsIfNeeded();
    }

    public bool OnUnlockClickTargetClicked()
    {
        if (_isUnlocked)
            return false;

        if (FacilityUnlockPanelUI.Instance == null)
        {
            Debug.LogWarning("[FacilityUnlockable] 场景中未找到 FacilityUnlockPanelUI，请在 Canvas 上挂载并绑定面板。");
            GlobalMessageUI.Show("未配置解锁面板（FacilityUnlockPanelUI）", 1.5f);
            return false;
        }

        FacilityUnlockPanelUI.Instance.Show(this);
        return true;
    }

    public bool CanAffordUnlock()
    {
        if (_isUnlocked || UnlockCost <= 0)
            return true;
        return GameValManager.Instance != null
               && GameValManager.Instance.CanAffordFacilityUnlock(UnlockCost);
    }

    public bool TryUnlockWithGold()
    {
        if (_isUnlocked)
            return true;

        if (GameValManager.Instance == null)
            return false;

        if (UnlockCost > 0 && !GameValManager.Instance.TryPayFacilityUnlock(UnlockCost))
        {
            GlobalMessageUI.Show("金币不足，无法解锁", 1.2f);
            return false;
        }

        SetUnlocked(true);
        GlobalMessageUI.Show($"已解锁 {DisplayName}", 1.2f);
        return true;
    }

    public void SetUnlocked(bool unlocked)
    {
        _isUnlocked = unlocked;
        ApplyVisualState();
        if (unlocked)
        {
            OnFacilityUnlocked?.Invoke(this);
            NotifyRestaurantListsIfNeeded();
        }
    }

    private void ApplyVisualState()
    {
        if (content != null && content != gameObject)
            content.SetActive(_isUnlocked);

        ApplyClickTargetState();
    }

    private void ApplyClickTargetState()
    {
        if (unlockClickTarget == null)
            return;

        if (unlockClickTarget == gameObject)
        {
            Debug.LogWarning(
                $"[FacilityUnlockable] {name} 的 Unlock Click Target 指向了根物体，已改为仅关闭点击组件。请改用单独子物体 UnlockClick。",
                this);
            SetClickInteractionEnabled(!_isUnlocked);
            return;
        }

        unlockClickTarget.SetActive(!_isUnlocked);

        if (!_isUnlocked)
            EnsureClickTargetCollidersEnabled();
    }

    private void SetClickInteractionEnabled(bool enabled)
    {
        FacilityUnlockClickTarget[] clicks = GetComponentsInChildren<FacilityUnlockClickTarget>(true);
        for (int i = 0; i < clicks.Length; i++)
        {
            if (clicks[i] != null)
                clicks[i].enabled = enabled;
        }

        Collider[] colliders = GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] != null)
                colliders[i].enabled = enabled;
        }

        Collider2D[] colliders2D = GetComponentsInChildren<Collider2D>(true);
        for (int i = 0; i < colliders2D.Length; i++)
        {
            if (colliders2D[i] != null)
                colliders2D[i].enabled = enabled;
        }
    }

    private void EnsureClickTargetCollidersEnabled()
    {
        Collider[] colliders = unlockClickTarget.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] != null)
                colliders[i].enabled = true;
        }
    }

    private void NotifyRestaurantListsIfNeeded()
    {
        if (facilityType == FacilityType.Plate || facilityType == FacilityType.Pot)
        {
            if (RestaurantPanel.instance != null)
                RestaurantPanel.instance.RefreshRestaurantUnits();
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        unlockCost = Mathf.Max(0, unlockCost);

        if (unlockClickTarget == gameObject)
        {
            Debug.LogWarning(
                $"[FacilityUnlockable] {name}：Unlock Click Target 不应指向根物体，请创建子物体 UnlockClick。",
                this);
        }

        if (content == gameObject)
        {
            Debug.LogWarning(
                $"[FacilityUnlockable] {name}：Content 不应指向根物体，请拖入子物体 Content。",
                this);
        }

        if (unlockClickTarget != null
            && unlockClickTarget.GetComponent<FacilityUnlockClickTarget>() == null
            && unlockClickTarget.GetComponentInChildren<FacilityUnlockClickTarget>(true) == null)
        {
            Debug.LogWarning(
                $"[FacilityUnlockable] {name}：Unlock Click Target 上缺少 FacilityUnlockClickTarget 组件。",
                this);
        }
    }
#endif
}
