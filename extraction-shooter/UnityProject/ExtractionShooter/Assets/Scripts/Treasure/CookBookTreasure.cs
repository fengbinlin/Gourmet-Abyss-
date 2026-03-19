using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;
using UnityEngine.SceneManagement;

/// <summary>
/// 参考普通宝箱逻辑的配方宝箱：
/// - 支持绑定 CookBookTreasureData，自定义掉落物和概率
/// - Data 记录宝箱是否已经被开启过
/// - 可设置只允许开启一次：如果已经开启过，生成时直接显示为已开启状态并禁止再次开启
/// </summary>
public class CookBookTreasure : MonoBehaviour
{
    [Header("基础设置")]
    public float timeNeedToHold = 2f;
    public bool isOpen = false;

    [Header("掉落动画参数")]
    [Tooltip("宝箱前方生成宝物的水平偏移距离")]
    public float spawnForwardOffset = -2f;
    [Tooltip("宝箱上方生成宝物的垂直高度")]
    public float spawnDropHeight = 2f;
    [Tooltip("下落动画持续时间")]
    public float dropDuration = 0.5f;

    [Header("数据绑定")]
    [Tooltip("配置该宝箱可以掉落哪些物品及其概率，以及是否已被开启过")]
    public CookBookTreasureData data;

    private Animator _animator;

    private void Awake()
    {
        _animator = GetComponent<Animator>();
    }

    private void Start()
    {
        // 如果配置了只允许开启一次，并且 Data 标记为已经开启过
        // 则在生成时直接设置为已开启状态，并禁止再开启
        if (data != null && data.onlyOpenOnce && data.hasBeenOpened)
        {
            isOpen = true;
            if (_animator != null)
            {
                // 按需求调用 TresasureHasBeenOpen Trigger，让宝箱直接显示为开启后的状态
                _animator.SetTrigger("TresasureHasBeenOpen");
            }
        }
    }

    /// <summary>
    /// 对外开放的开启接口（可由交互/触发器等调用）
    /// </summary>
    public void Open()
    {
        // 已经开启过或被 Data 标记为不允许再次开启，则直接返回
        if (isOpen)
        {
            return;
        }

        if (data != null && data.onlyOpenOnce && data.hasBeenOpened)
        {
            // Data 标记为已被拾取对应物品（例如 RecipeBook），不再允许开启
            return;
        }

        isOpen = true;

        if (_animator != null)
        {
            _animator.SetTrigger("Open");
        }

        // 1 秒后根据 Data 生成掉落物
        Invoke(nameof(SpawnItemFromData), 1.5f);
    }

    private void SpawnItemFromData()
    {
        if (data == null || data.dropItems == null || data.dropItems.Count == 0)
        {
            Debug.LogWarning("CookBookTreasureData 未配置或没有可掉落物品！");
            return;
        }

        // 计算总权重
        float totalWeight = 0f;
        foreach (var item in data.dropItems)
        {
            if (item != null && item.itemPrefab != null && item.probability > 0f)
            {
                totalWeight += item.probability;
            }
        }

        if (totalWeight <= 0f)
        {
            Debug.LogWarning("CookBookTreasureData 中所有物品概率权重无效！");
            return;
        }

        // 按权重随机选择一个物品
        float randomValue = Random.Range(0f, totalWeight);
        CookBookTreasureData.DropItem selectedItem = null;

        float cumulative = 0f;
        foreach (var item in data.dropItems)
        {
            if (item == null || item.itemPrefab == null || item.probability <= 0f) continue;

            cumulative += item.probability;
            if (randomValue <= cumulative)
            {
                selectedItem = item;
                break;
            }
        }

        if (selectedItem == null || selectedItem.itemPrefab == null)
        {
            Debug.LogWarning("CookBookTreasureData 权重随机未选中任何有效物品！");
            return;
        }

        // 计算生成位置
        Vector3 spawnPosition = transform.position + transform.forward * spawnForwardOffset + new Vector3(0, 1.5f, 0);
        Vector3 startPosition = spawnPosition + Vector3.up * spawnDropHeight;

        // 实例化物品
        GameObject spawnedItem = Instantiate(selectedItem.itemPrefab, startPosition, Quaternion.identity);

        // 确保生成物体被放入和宝箱相同的场景（支持附加场景）
        SceneManager.MoveGameObjectToScene(spawnedItem, gameObject.scene);

        // 将父物体设置为宝箱所在场景的父级（与宝箱同一层级）
        if (transform.parent != null)
        {
            spawnedItem.transform.SetParent(transform.parent, worldPositionStays: true);
        }

        // 如果掉落的是 RecipeBook，道具上记录是哪一个 Data 生成了它
        Prop_RecipeBook recipeBook = spawnedItem.GetComponent<Prop_RecipeBook>();
        if (recipeBook != null)
        {
            recipeBook.sourceData = data;
        }

        // 使用 DoTween 实现下落和弹跳效果，与普通宝箱保持一致体验
        DropItemWithDoTween(spawnedItem.transform, startPosition, spawnPosition);
    }

    private void DropItemWithDoTween(Transform itemTransform, Vector3 startPos, Vector3 endPos)
    {
        itemTransform.position = startPos;

        Sequence dropSequence = DOTween.Sequence();

        dropSequence.Append(itemTransform.DOMove(endPos, dropDuration)
            .SetEase(Ease.OutBounce));

        dropSequence.Append(itemTransform.DOMoveY(endPos.y + 0.2f, 0.2f)
            .SetEase(Ease.OutQuad));
        dropSequence.Append(itemTransform.DOMoveY(endPos.y, 0.2f)
            .SetEase(Ease.InQuad));

        dropSequence.Append(itemTransform.DOMoveY(endPos.y + 0.05f, 1f)
            .SetEase(Ease.InOutSine)
            .SetLoops(-1, LoopType.Yoyo));
    }
}

