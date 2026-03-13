using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using DG.Tweening; // DoTween 命名空间

public class Treasure : MonoBehaviour
{
    public float timeNeedToHold = 2;
    public bool isOpen = false;

    // 宝箱前方生成宝物的水平偏移距离
    public float spawnForwardOffset = -2f;
    // 宝箱上方生成宝物的垂直高度
    public float spawnDropHeight = 2f;
    // 下落动画持续时间
    public float dropDuration = 0.5f;

    // Start is called before the first frame update
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {

    }

    public void Open()
    {
        isOpen = true;
        transform.GetComponent<Animator>().SetTrigger("Open");
        print("宝箱打开");
        // 调用生成随机已解锁宝物的方法
        Invoke("SpawnRandomUnlockedTreasure", 1f);


    }

    private void SpawnRandomUnlockedTreasure()
    {
        // 确保 TreasureManager 存在
        if (TreasureManager.treasureManager == null)
        {
            Debug.LogError("找不到 TreasureManager 实例！");
            return;
        }

        // 使用 Linq 简化已解锁宝物的筛选
        List<treasure> unlockedTreasures = TreasureManager.treasureManager.treasuresList
            .Where(t => t.isUnLocked)
            .ToList();

        // 检查是否有已解锁的宝物
        if (unlockedTreasures.Count == 0)
        {
            Debug.LogWarning("没有已解锁的宝物可供生成！");
            return;
        }

        // 随机选择一个已解锁的宝物
        int randomIndex = Random.Range(0, unlockedTreasures.Count);
        treasure selectedTreasure = unlockedTreasures[randomIndex];

        // 计算生成位置
        Vector3 spawnPosition = transform.position + transform.forward * spawnForwardOffset + new Vector3(0, 1.5f, 0);
        Vector3 startPosition = spawnPosition + Vector3.up * spawnDropHeight; // 起始位置（高空）

        // 实例化宝物
        GameObject spawnedTreasure = Instantiate(selectedTreasure.treasureObject, startPosition, Quaternion.identity);

        // 使用 DoTween 实现下落效果
        DropTreasureWithDoTween(spawnedTreasure.transform, startPosition, spawnPosition);

        Debug.Log($"已生成宝物: {selectedTreasure.treasureName}");
        Destroy(gameObject, 1f);
    }

    private void DropTreasureWithDoTween(Transform treasureTransform, Vector3 startPos, Vector3 endPos)
    {
        // 设置初始位置
        treasureTransform.position = startPos;

        // // 添加随机旋转
        // Vector3 randomRotation = new Vector3(
        //     Random.Range(0, 360f),
        //     Random.Range(0, 360f),
        //     Random.Range(0, 360f)
        // );

        // 使用 DoTween 同时进行下落和旋转
        Sequence dropSequence = DOTween.Sequence();

        // 下落动画，使用 OutBounce 缓动函数模拟弹跳效果
        dropSequence.Append(treasureTransform.DOMove(endPos, dropDuration)
            .SetEase(Ease.OutBounce));

        // 旋转动画
        // dropSequence.Join(treasureTransform.DORotate(randomRotation * 3, dropDuration, RotateMode.LocalAxisAdd));

        // 轻微弹跳效果
        dropSequence.Append(treasureTransform.DOMoveY(endPos.y + 0.2f, 0.2f)
            .SetEase(Ease.OutQuad));
        dropSequence.Append(treasureTransform.DOMoveY(endPos.y, 0.2f)
            .SetEase(Ease.InQuad));

        // 循环轻微浮动
        dropSequence.Append(treasureTransform.DOMoveY(endPos.y + 0.05f, 1f)
            .SetEase(Ease.InOutSine)
            .SetLoops(-1, LoopType.Yoyo));
    }
}