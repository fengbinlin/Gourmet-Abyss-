using Game.Core;
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

[DefaultExecutionOrder(-10)] // 在 CookManager 初始化之后不久
public class CookUIManager : MonoSingleton<CookUIManager>
{
    // 每个场景各一份，后加载的接管——沿用原来的裸赋值语义。
    protected override DuplicatePolicy Duplicate => DuplicatePolicy.OverwriteReference;

    /// <summary>兼容旧调用点的别名，等价于 Instance。</summary>
    public static CookUIManager instance => Instance;


    [Header("厨师面板")]
    public Transform cookListParent; // 厨师条目生成父物体

    public CookUIItem cookItemPrefab; // 单个厨师条目预制体

    [Header("最大雇佣数量")]
    [SerializeField] private int maxCookCount = 3;

    private readonly List<CookUIItem> spawnedItems = new List<CookUIItem>();


    public void SetMaxCookCount(int newMax)
    {
        maxCookCount = Mathf.Max(0, newMax);
        RefreshUI();
    }

    public bool CanRecruitMore()
    {
        if (CookManager.cookManager == null) return true;

        int count = 0;
        for (int i = 0; i < CookManager.cookManager.curCookList.Count; i++)
        {
            var npc = CookManager.cookManager.curCookList[i];
            if (npc != null && npc.data != null && npc.data.isCook) count++;
        }

        return count < maxCookCount;
    }

    public void OnChefRecruited(CustomerNPC npc)
    {
        RefreshUI();
    }

    public void OnChefFired(CustomerNPC npc)
    {
        RefreshUI();
    }

    public void RefreshUI()
    {
        if (cookListParent == null) return;

        // 清理旧条目
        for (int i = spawnedItems.Count - 1; i >= 0; i--)
        {
            var item = spawnedItems[i];
            if (item != null) Destroy(item.gameObject);
        }
        spawnedItems.Clear();

        if (CookManager.cookManager == null || cookItemPrefab == null) return;

        // 生成当前厨师条目
        for (int i = 0; i < CookManager.cookManager.curCookList.Count; i++)
        {
            CustomerNPC npc = CookManager.cookManager.curCookList[i];
            if (npc == null) continue;

            GameObject go = Instantiate(cookItemPrefab.gameObject, cookListParent);
            CookUIItem item = go.GetComponent<CookUIItem>();
            if (item == null)
            {
                Debug.LogWarning("CookUIItem prefab 缺少 CookUIItem 脚本组件");
                continue;
            }

            item.Initialize(npc, this);
            spawnedItems.Add(item);
        }
    }

    public void FireChef(CustomerNPC npc)
    {
        if (npc == null) return;
        if (CookManager.cookManager == null) return;

        CookManager.cookManager.FireCook(npc);
        RefreshUI();
    }
}

