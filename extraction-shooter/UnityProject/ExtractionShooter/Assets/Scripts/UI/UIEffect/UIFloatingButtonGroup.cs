using System;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

[DisallowMultipleComponent]
public class UIFloatingButtonGroup : MonoBehaviour
{
    [Serializable]
    public struct AnimConfig
    {
        [Header("基础时长/曲线")]
        public float duration;
        public Ease ease;

        [Header("加强动感（建议开启）")]
        public bool useJuicyMotion;
        public float upDuration;
        public Ease upEase;
        public float downDuration;
        public Ease downEase;
        public float settleDuration;
        public Ease settleEase;
        public float overshootY;
        public float scaleOvershoot;
        public float scaleDelay;
        public float colorDelay;
        public float colorDurationMultiplier;
        public float speedBasedMinDuration;
        public float speedBasedUnitsPerSecond;
        public float selectedOffsetY;
        public float othersOffsetY;
        public float selectedScale;
        [Range(0.05f, 1f)] public float dimMultiplier;
    }

    [Header("目标对象（建议填满 5 个）")]
    [SerializeField] private List<UIFloatingButtonItem> items = new List<UIFloatingButtonItem>(5);

    [Header("相机切换")]
    [SerializeField] private CameraFollow cameraFollow;
    [SerializeField] private bool revertCameraOnDeselect = true;

    [Header("键盘控制")]
    [SerializeField] private bool enableKeyboard = true;
    [SerializeField] private bool includeKeypadNumbers = true;

    [Header("动画参数")]
    [SerializeField] private AnimConfig anim = new AnimConfig
    {
        duration = 0.18f,
        ease = Ease.OutCubic,
        useJuicyMotion = true,
        upDuration = 0.16f,
        upEase = Ease.OutBack,
        downDuration = 0.14f,
        downEase = Ease.InCubic,
        settleDuration = 0.10f,
        settleEase = Ease.OutCubic,
        overshootY = 6f,
        scaleOvershoot = 0.03f,
        scaleDelay = 0.02f,
        colorDelay = 0.00f,
        colorDurationMultiplier = 0.9f,
        speedBasedMinDuration = 0.08f,
        speedBasedUnitsPerSecond = 500f,
        selectedOffsetY = 22f,
        othersOffsetY = -10f,
        selectedScale = 1.08f,
        dimMultiplier = 0.65f
    };

    [Header("初始状态")]
    [SerializeField] private int initialSelectedIndex = -1; // -1 表示不选中

    [Header("餐厅/商店联动（餐厅通常是第2个按钮）")]
    [SerializeField] private bool linkRestaurantToShopUI = true;
    [SerializeField] private int restaurantButtonIndex = 1;
    

    [Header("邻居震荡（下落后波动）")]
    [SerializeField] private bool enableNeighborWave = true;
    [SerializeField] private float neighborPunchStrengthY = 10f;
    [SerializeField] private float neighborPunchDuration = 0.18f;
    [SerializeField] private int neighborPunchVibrato = 12;
    [SerializeField, Range(0f, 1f)] private float neighborPunchElasticity = 0.75f;
    [SerializeField] private float neighborWaveMinInterval = 0.05f;

    private int currentSelectedIndex = -1;
    private float[] lastWaveTime;
    public static UIFloatingButtonGroup Instance;

    [Header("玩家移动取消选中")]
    [SerializeField] private TopDownController playerController;
    private bool wasMovingLastFrame = false;

    // 记录每个按钮当前是否处于选中状态（单选模式：仅一个为 true）
    private readonly List<bool> selectedStates = new List<bool>();

    /// <summary>
    /// 各按钮选中状态（公开只读访问）。
    /// </summary>
    public IReadOnlyList<bool> SelectedStates => selectedStates;

    public bool IsSelected(int index)
    {
        if (index < 0 || index >= selectedStates.Count) return false;
        return selectedStates[index];
    }

    public int CurrentSelectedIndex => currentSelectedIndex;

    private void Awake()
    {
        Instance=this;
        AutoPopulateIfNeeded();
        BindItems();
        EnsureSelectedStateListSize();
        lastWaveTime = new float[items.Count];
        for (int i = 0; i < lastWaveTime.Length; i++) lastWaveTime[i] = -999f;

        if (cameraFollow == null) cameraFollow = FindFirstObjectByType<CameraFollow>();
        if (playerController == null) playerController = FindFirstObjectByType<TopDownController>();
    }

    private void OnEnable()
    {
        // 确保启用时状态正确（尤其是运行时动态生成 UI 的场景）
        ApplyImmediateState(initialSelectedIndex);
        currentSelectedIndex = initialSelectedIndex;
        EnsureSelectedStateListSize();
        UpdateSelectedStates(currentSelectedIndex);

        if (cameraFollow != null)
        {
            cameraFollow.OnOverrideClearedByPlayerMove -= HandleCameraOverrideClearedByPlayerMove;
            cameraFollow.OnOverrideClearedByPlayerMove += HandleCameraOverrideClearedByPlayerMove;
        }
    }

    private void OnDisable()
    {
        if (cameraFollow != null)
        {
            cameraFollow.OnOverrideClearedByPlayerMove -= HandleCameraOverrideClearedByPlayerMove;
        }
    }

    private void Update()
    {
        if (!enableKeyboard) return;

        for (int i = 0; i < items.Count && i < 9; i++)
        {
            int number = i + 1;
            if (Input.GetKeyDown(KeyCode.Alpha0 + number) ||
                (includeKeypadNumbers && Input.GetKeyDown(KeyCode.Keypad0 + number)))
            {
                // 键盘触发也走“按钮点击”路径，确保和鼠标点击效果一致（会触发 onClick 上的其它监听）
                if (items[i] != null) items[i].InvokeButtonClick();
                return;
            }
        }

        // 玩家开始移动时取消选中（餐厅范围内选中餐厅按钮例外）
        if (playerController != null)
        {
            bool isMoving = playerController.IsMoving();
            if (isMoving && !wasMovingLastFrame)
            {
                HandlePlayerMoveCancelSelection();
            }
            wasMovingLastFrame = isMoving;
        }
    }

    private void HandlePlayerMoveCancelSelection()
    {
        if (currentSelectedIndex == -1) return;

        // 例外：餐厅按钮在触发范围内时，移动不应取消其激活状态
        if (linkRestaurantToShopUI &&
            currentSelectedIndex == restaurantButtonIndex &&
            ShopInteraction.Instance != null &&
            ShopInteraction.Instance.playerInRange)
        {
            return;
        }

        SetSelectedIndex(-1);
    }

    public void ToggleSelect(int index)
    {
        if (index < 0 || index >= items.Count) return;

        int newIndex = (currentSelectedIndex == index) ? -1 : index;
        SetSelectedIndex(newIndex);
    }
    public void ToggleSelectButton(int index)
    {
        if (index < 0 || index >= items.Count) return;

        int newIndex = (currentSelectedIndex == index) ? -1 : index;
        if ( newIndex < -1 ||  newIndex >= items.Count) return;
        if (currentSelectedIndex ==  newIndex) return;

        int prevSelectedIndex = currentSelectedIndex;
        currentSelectedIndex =  newIndex;
        EnsureSelectedStateListSize();
        UpdateSelectedStates(currentSelectedIndex);
        HandleRestaurantSelectionChanged(prevSelectedIndex, currentSelectedIndex);
        UpdateCameraTarget(currentSelectedIndex);
        AnimateToState(currentSelectedIndex, prevSelectedIndex);
    }
    public void SetSelectedIndex(int index)
    {
        if (index < -1 || index >= items.Count) return;
        if (currentSelectedIndex == index) return;

        int prevSelectedIndex = currentSelectedIndex;
        currentSelectedIndex = index;
        EnsureSelectedStateListSize();
        UpdateSelectedStates(currentSelectedIndex);
        HandleRestaurantSelectionChanged(prevSelectedIndex, currentSelectedIndex);
        UpdateCameraTarget(currentSelectedIndex);
        AnimateToState(currentSelectedIndex, prevSelectedIndex);
    }

    /// <summary>
    /// 如果当前正选中指定 index，则取消选中（设为 -1）。
    /// 用于触发区离开时“强制餐厅按钮未激活”，但不影响其它按钮已选中状态。
    /// </summary>
    public void DeselectIfSelected(int index)
    {
        if (currentSelectedIndex == index)
        {
            SetSelectedIndex(-1);
        }
    }

    private void UpdateCameraTarget(int selectedIndex)
    {
        if (cameraFollow == null) return;

        if (selectedIndex >= 0 && selectedIndex < items.Count && items[selectedIndex] != null)
        {
            Transform camTarget = items[selectedIndex].GetCameraTarget();
            if (camTarget != null)
            {
                cameraFollow.SetOverrideTarget(camTarget);
                return;
            }
        }

        if (revertCameraOnDeselect)
        {
            cameraFollow.ClearOverrideTarget();
        }
    }

    private void HandleCameraOverrideClearedByPlayerMove()
    {
        // 玩家移动导致相机回到 Player 跟随时，同时复原 UI 选中状态
        // 例外：餐厅按钮在触发范围内时，移动不应取消其激活状态
        if (linkRestaurantToShopUI &&
            currentSelectedIndex == restaurantButtonIndex &&
            ShopInteraction.Instance != null &&
            ShopInteraction.Instance.playerInRange)
        {
            return;
        }

        if (currentSelectedIndex != -1)
        {
            SetSelectedIndex(-1);
        }
    }

    private void AnimateToState(int selectedIndex, int prevSelectedIndex)
    {
        // 只在“取消选中”（从某个选中 -> -1）时触发邻居震荡
        bool isCancel = (prevSelectedIndex >= 0 && selectedIndex == -1);

        for (int i = 0; i < items.Count; i++)
        {
            UIFloatingButtonItem it = items[i];
            if (it == null) continue;

            bool isSelected = (i == selectedIndex);
            bool anySelected = (selectedIndex >= 0);

            // 只监听“原来选中的 item”落回默认位置的完成时刻
            if (enableNeighborWave && isCancel && i == prevSelectedIndex)
            {
                int fallingIndex = i;
                it.Play(anim, isSelected, anySelected, () => TriggerNeighborWave(fallingIndex));
            }
            else
            {
                it.Play(anim, isSelected, anySelected);
            }
        }
    }

    private void TriggerNeighborWave(int index)
    {
        if (!enableNeighborWave) return;
        if (items == null || items.Count == 0) return;
        if (index < 0 || index >= items.Count) return;

        float now = Time.unscaledTime;
        if (lastWaveTime != null && index < lastWaveTime.Length)
        {
            if (now - lastWaveTime[index] < neighborWaveMinInterval) return;
            lastWaveTime[index] = now;
        }

        int left = index - 1;
        int right = index + 1;

        // 左右邻居做上下方向震荡
        if (left >= 0 && left < items.Count && items[left] != null)
            items[left].PunchVertical(neighborPunchStrengthY, neighborPunchDuration, neighborPunchVibrato, neighborPunchElasticity);

        if (right >= 0 && right < items.Count && items[right] != null)
            items[right].PunchVertical(neighborPunchStrengthY, neighborPunchDuration, neighborPunchVibrato, neighborPunchElasticity);
    }

    private void ApplyImmediateState(int selectedIndex)
    {
        EnsureSelectedStateListSize();
        UpdateSelectedStates(selectedIndex);

        for (int i = 0; i < items.Count; i++)
        {
            UIFloatingButtonItem it = items[i];
            if (it == null) continue;

            bool isSelected = (i == selectedIndex);
            bool anySelected = (selectedIndex >= 0);
            it.ApplyImmediate(anim, isSelected, anySelected);
        }
    }

    private void BindItems()
    {
        for (int i = 0; i < items.Count; i++)
        {
            UIFloatingButtonItem it = items[i];
            if (it == null) continue;
            it.Bind(this, i);
        }
    }

    private void AutoPopulateIfNeeded()
    {
        if (items != null && items.Count > 0) return;

        items = new List<UIFloatingButtonItem>();
        int count = Mathf.Min(5, transform.childCount);
        for (int i = 0; i < count; i++)
        {
            Transform child = transform.GetChild(i);
            UIFloatingButtonItem it = child.GetComponent<UIFloatingButtonItem>();
            if (it == null) it = child.gameObject.AddComponent<UIFloatingButtonItem>();
            items.Add(it);
        }

        // 重新初始化波动节流数组
        lastWaveTime = new float[items.Count];
        for (int i = 0; i < lastWaveTime.Length; i++) lastWaveTime[i] = -999f;

        EnsureSelectedStateListSize();
    }

    private void EnsureSelectedStateListSize()
    {
        if (items == null) return;
        int count = items.Count;
        if (selectedStates.Count == count) return;

        if (selectedStates.Count < count)
        {
            int add = count - selectedStates.Count;
            for (int i = 0; i < add; i++) selectedStates.Add(false);
        }
        else
        {
            selectedStates.RemoveRange(count, selectedStates.Count - count);
        }
    }

    private void UpdateSelectedStates(int selectedIndex)
    {
        for (int i = 0; i < selectedStates.Count; i++)
        {
            selectedStates[i] = (selectedIndex >= 0 && i == selectedIndex);
        }
    }

    private void HandleRestaurantSelectionChanged(int prevIndex, int newIndex)
    {
        if (!linkRestaurantToShopUI) return;
        if (restaurantButtonIndex < 0) return;

        bool wasRestaurant = prevIndex == restaurantButtonIndex;
        bool isRestaurant = newIndex == restaurantButtonIndex;
        if (wasRestaurant == isRestaurant) return;

        if (ShopInteraction.Instance == null) return;

        if (isRestaurant)
        {
            ShopInteraction.Instance.ShowShopUI();
        }
        else
        {
            ShopInteraction.Instance.HideShopUI();
        }
    }
}

