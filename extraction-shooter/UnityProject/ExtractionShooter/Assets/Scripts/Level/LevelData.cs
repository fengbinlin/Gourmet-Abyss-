using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "NewLevelData", menuName = "Level System/Level Data")]
public class LevelData : ScriptableObject
{
    [System.Serializable]
    public class LevelRequirement
    {
        public int level;
        public int requiredSkills;
        public string levelTitle = "声望";
    }

    [SerializeField] private List<LevelRequirement> levelRequirements = new List<LevelRequirement>();

    public List<LevelRequirement> LevelRequirements => levelRequirements;

    public int GetRequiredSkillsForLevel(int level)
    {
        if (level < 1 || level > levelRequirements.Count)
            return 0;

        return levelRequirements[level - 1].requiredSkills;
    }

    public string GetLevelTitleForLevel(int level)
    {
        if (level < 1 || level > levelRequirements.Count)
            return "声望";

        return levelRequirements[level - 1].levelTitle;
    }

    public int GetMaxLevel()
    {
        return levelRequirements.Count;
    }

    public int GetCurrentLevel(int learnedSkills)
    {
        for (int i = levelRequirements.Count - 1; i >= 0; i--)
        {
            if (learnedSkills >= levelRequirements[i].requiredSkills)
            {
                return i + 1;
            }
        }
        return 1;
    }

    public int GetSkillsToNextLevel(int currentLevel, int learnedSkills)
    {
        // 调试信息
        Debug.Log($"GetSkillsToNextLevel调用参数: currentLevel={currentLevel}, learnedSkills={learnedSkills}");
        Debug.Log($"等级总数: {levelRequirements.Count}");

        // 如果已经是最大等级，返回0
        if (currentLevel >= levelRequirements.Count)
        {
            Debug.Log($"已达到最大等级 {currentLevel}/{levelRequirements.Count}，返回0");
            return 0;
        }

        // 调试等级要求
        for (int i = 0; i < levelRequirements.Count; i++)
        {
            Debug.Log($"等级 {i + 1} 需要 {levelRequirements[i].requiredSkills} 技能");
        }

        int nextLevelReq = levelRequirements[currentLevel].requiredSkills;
        Debug.Log($"当前等级索引: {currentLevel}，下一等级需求技能数: {nextLevelReq}");

        int result = Mathf.Max(0, nextLevelReq - learnedSkills);
        Debug.Log($"计算结果: {nextLevelReq} - {learnedSkills} = {result}");

        return result;
    }

    public float GetProgressToNextLevel(int currentLevel, int learnedSkills)
    {
        // 调试信息
        //Debug.Log($"GetProgressToNextLevel调用参数: currentLevel={currentLevel}, learnedSkills={learnedSkills}");
        //Debug.Log($"等级总数: {levelRequirements.Count}");

        // 如果已经是最大等级，进度为100%
        if (currentLevel >= levelRequirements.Count)
        {
            //Debug.Log($"已达到最大等级 {currentLevel}/{levelRequirements.Count}，返回进度1f");
            return 1f;
        }

        // 检查索引是否在有效范围内
        if (currentLevel <= 0)
        {
            //Debug.LogError($"currentLevel值错误: {currentLevel}，必须大于0");
            return 0f;
        }

        if (currentLevel > levelRequirements.Count)
        {
            //Debug.LogError($"currentLevel超出范围: {currentLevel}/{levelRequirements.Count}");
            return 1f;
        }

        int currentLevelReq = levelRequirements[currentLevel - 1].requiredSkills;
        int nextLevelReq = levelRequirements[currentLevel].requiredSkills;

        //Debug.Log($"当前等级需求: {currentLevelReq}，下一等级需求: {nextLevelReq}");

        int diff = nextLevelReq - currentLevelReq;
        //Debug.Log($"等级差值: {nextLevelReq} - {currentLevelReq} = {diff}");

        if (diff <= 0)
        {
            //Debug.LogWarning($"等级差值小于等于0: {diff}，返回进度1f");
            return 1f;
        }

        int progress = learnedSkills - currentLevelReq;
        //Debug.Log($"当前进度: {learnedSkills} - {currentLevelReq} = {progress}");

        float progressRatio = Mathf.Clamp01((float)progress / diff);
        //Debug.Log($"进度比率: {progress} / {diff} = {progressRatio:F2}");

        return progressRatio;
    }
}