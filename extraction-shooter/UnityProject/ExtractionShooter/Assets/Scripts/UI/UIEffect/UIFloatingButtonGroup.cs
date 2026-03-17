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
    

    [Header("邻居震荡（下落后波动）")]
    [SerializeField] private bool enableNeighborWave = true;
    [SerializeField] private float neighborPunchStrengthY = 10f;
    [SerializeField] private float neighborPunchDuration = 0.18f;
    [SerializeField] private int neighborPunchVibrato = 12;
    [SerializeField, Range(0f, 1f)] private float neighborPunchElasticity = 0.75f;
    [SerializeField] private float neighborWaveMinInterval = 0.05f;

    private int currentSelectedIndex = -1;
    private float[] lastWaveTime;

    private void Awake()
    {
        AutoPopulateIfNeeded();
        BindItems();
        lastWaveTime = new float[items.Count];
        for (int i = 0; i < lastWaveTime.Length; i++) lastWaveTime[i] = -999f;

        if (cameraFollow == null) cameraFollow = FindFirstObjectByType<CameraFollow>();
    }

    private void OnEnable()
    {
        // 确保启用时状态正确（尤其是运行时动态生成 UI 的场景）
        ApplyImmediateState(initialSelectedIndex);
        currentSelectedIndex = initialSelectedIndex;
    }

    private void OnDisable() { }

    private void Update()
    {
        if (!enableKeyboard) return;

        for (int i = 0; i < items.Count && i < 9; i++)
        {
            int number = i + 1;
            if (Input.GetKeyDown(KeyCode.Alpha0 + number) ||
                (includeKeypadNumbers && Input.GetKeyDown(KeyCode.Keypad0 + number)))
            {
                ToggleSelect(i);
                return;
            }
        }
    }

    public void ToggleSelect(int index)
    {
        if (index < 0 || index >= items.Count) return;

        int newIndex = (currentSelectedIndex == index) ? -1 : index;
        SetSelectedIndex(newIndex);
    }

    public void SetSelectedIndex(int index)
    {
        if (index < -1 || index >= items.Count) return;
        if (currentSelectedIndex == index) return;

        int prevSelectedIndex = currentSelectedIndex;
        currentSelectedIndex = index;
        UpdateCameraTarget(currentSelectedIndex);
        AnimateToState(currentSelectedIndex, prevSelectedIndex);
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
    }
}

