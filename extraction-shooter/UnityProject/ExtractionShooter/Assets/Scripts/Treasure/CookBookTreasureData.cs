using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 配方宝箱的数据类：
/// - 定义可掉落物品及其概率
/// - 记录是否已经被开启过
/// </summary>
[CreateAssetMenu(fileName = "CookBookTreasureData", menuName = "Treasure/CookBook Treasure Data")]
public class CookBookTreasureData : ScriptableObject
{
    [Serializable]
    public class DropItem
    {
        [Tooltip("掉落的物体预制体")]
        public GameObject itemPrefab;

        [Tooltip("掉落权重 / 概率系数（不需要归一化）")]
        public float probability = 1f;
    }

    [Header("掉落配置")]
    public List<DropItem> dropItems = new List<DropItem>();

    [Header("开启次数控制")]
    [Tooltip("是否只允许开启一次")]
    public bool onlyOpenOnce = true;

    [Tooltip("该宝箱是否已经被开启过（运行时会被修改）")]
    public bool hasBeenOpened = false;
}

