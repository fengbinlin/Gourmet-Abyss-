using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PlayerLevelManager : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Text levelTitleText;
    [SerializeField] private Image progressBarImage;  // 使用Image而不是Slider
    [SerializeField] private Text progressText;
    [SerializeField] private Text levelText;

    [Header("Progress Bar Settings")]
    [SerializeField] private Color progressStartColor = Color.red;
    [SerializeField] private Color progressMidColor = Color.yellow;
    [SerializeField] private Color progressEndColor = Color.green;
    [SerializeField] private AnimationCurve colorTransitionCurve = AnimationCurve.Linear(0, 0, 1, 1);

    [Header("Level Configuration")]
    [SerializeField] private LevelData levelData;

    [Header("Visual Settings")]
    [SerializeField] private string levelFormat = "声望-等级{0:D2}";
    [SerializeField] private string progressFormat = "{0}/{1}";

    private int currentLevel = 1;
    private int currentLearnedSkills = 0;
    private Material progressBarMaterial;  // 用于动态改变颜色

    public static PlayerLevelManager Instance { get; private set; }

    public event Action<int> OnLevelUp;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        InitializeLevelSystem();
    }

    private void Update()
    {
        // 从SkillTree获取当前已学习的技能数量
        UpdateLearnedSkills();
    }

    private void InitializeLevelSystem()
    {
        if (levelData == null)
        {
            Debug.LogError("LevelData is not assigned!");
            return;
        }

        // 初始化进度条
        if (progressBarImage != null)
        {
            // 确保Image类型是Filled
            progressBarImage.type = Image.Type.Filled;

            // 创建材质实例，以便独立修改颜色
            if (progressBarImage.material != null)
            {
                progressBarMaterial = new Material(progressBarImage.material);
                progressBarImage.material = progressBarMaterial;
            }
        }

        // 初始化UI
        UpdateUI();
    }

    private void UpdateLearnedSkills()
    {
        // 这里从你的SkillTree获取已学习技能数量
        int newLearnedSkills = 0;

        if (SkillTree.Instance != null)
        {
            newLearnedSkills = SkillTree.Instance.learnedSkillNum;
        }

        if (newLearnedSkills != currentLearnedSkills)
        {
            currentLearnedSkills = newLearnedSkills;
            CheckLevelUp();
            UpdateUI();
        }
    }

    private void CheckLevelUp()
    {
        int newLevel = levelData.GetCurrentLevel(currentLearnedSkills);

        if (newLevel > currentLevel)
        {
            int oldLevel = currentLevel;
            currentLevel = newLevel;
            OnLevelUp?.Invoke(currentLevel);

            // 播放升级效果
            PlayLevelUpEffect(oldLevel, newLevel);

            // 解锁系统会自动监听OnLevelUp事件，不需要额外调用
        }
    }

    private void UpdateUI()
    {
        // 更新等级标题
        if (levelTitleText != null)
        {
            levelTitleText.text = string.Format(levelFormat, currentLevel.ToString("D2"));
        }

        // 更新等级文本
        if (levelText != null)
        {
            levelText.text = $"Lv.{currentLevel}";
        }

        // 更新进度条
        if (progressBarImage != null)
        {
            float progress = levelData.GetProgressToNextLevel(currentLevel, currentLearnedSkills);
            print("进度：" + progress);
            progressBarImage.fillAmount = progress;

            // 更新进度条颜色
            UpdateProgressBarColor(progress);
        }

        // 更新进度文本
        if (progressText != null)
        {
            int currentReq = levelData.GetRequiredSkillsForLevel(currentLevel);
            int nextReq = (currentLevel < levelData.GetMaxLevel())
                ? levelData.GetRequiredSkillsForLevel(currentLevel + 1)
                : currentReq;

            progressText.text = string.Format(progressFormat, currentLearnedSkills, nextReq);
        }
    }
    // 在PlayerLevelManager中添加

    private void UpdateProgressBarColor(float progress)
    {
        if (progressBarImage == null) return;

        Color targetColor;

        // 根据进度渐变颜色
        if (progress < 0.5f)
        {
            float t = progress * 2f;
            t = colorTransitionCurve.Evaluate(t);
            targetColor = Color.Lerp(progressStartColor, progressMidColor, t);
        }
        else
        {
            float t = (progress - 0.5f) * 2f;
            t = colorTransitionCurve.Evaluate(t);
            targetColor = Color.Lerp(progressMidColor, progressEndColor, t);
        }

        // 应用颜色
        if (progressBarMaterial != null)
        {
            progressBarMaterial.color = targetColor;
        }
        else
        {
            progressBarImage.color = targetColor;
        }
    }

    private void PlayLevelUpEffect(int oldLevel, int newLevel)
    {
        Debug.Log($"等级提升! {oldLevel} → {newLevel}");

        // 这里可以添加更多的升级效果：
        // 1. 粒子效果
        // 2. 音效
        // 3. 屏幕震动
        // 4. UI动画

        // 简单的闪烁效果
        if (levelText != null)
        {
            StartCoroutine(LevelUpFlashEffect());
        }
    }

    private System.Collections.IEnumerator LevelUpFlashEffect()
    {
        if (levelText == null) yield break;

        Color originalColor = levelText.color;
        float duration = 0.5f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            float t = Mathf.PingPong(elapsed * 4f, 1f);
            levelText.color = Color.Lerp(originalColor, Color.yellow, t);
            elapsed += Time.deltaTime;
            yield return null;
        }

        levelText.color = originalColor;
    }

    public int GetCurrentLevel()
    {
        return currentLevel;
    }

    public int GetCurrentLearnedSkills()
    {
        return currentLearnedSkills;
    }

    public float GetProgressPercentage()
    {
        return levelData.GetProgressToNextLevel(currentLevel, currentLearnedSkills);
    }

    public int GetSkillsToNextLevel()
    {
        return levelData.GetSkillsToNextLevel(currentLevel, currentLearnedSkills);
    }

    public int GetRequiredSkillsForNextLevel()
    {
        if (currentLevel >= levelData.GetMaxLevel())
            return levelData.GetRequiredSkillsForLevel(currentLevel);

        return levelData.GetRequiredSkillsForLevel(currentLevel + 1);
    }

    // 用于测试的方法
    [ContextMenu("增加10个技能")]
    public void AddTestSkills()
    {
        if (SkillTree.Instance != null)
        {
            SkillTree.Instance.learnedSkillNum += 10;
        }
    }

    [ContextMenu("重置进度")]
    public void ResetProgress()
    {
        if (SkillTree.Instance != null)
        {
            SkillTree.Instance.learnedSkillNum = 0;
        }
        currentLevel = 1;
        UpdateUI();
    }
}