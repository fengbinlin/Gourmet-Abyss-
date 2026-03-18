using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
public class ItemBagManager : MonoBehaviour
{
    public static ItemBagManager instance;

    [Header("食材背包UI配置")]
    public GameObject ItemPrefabs;
    public Transform ItemParent;
    public Text InformationTitle;
    public Text InformationDescription;
    private List<GameObject> currentItems = new List<GameObject>();
    // Start is called before the first frame update
    public Image customerGiftImage;
    public ResourceType giftResourceType;

    // 当前是否按单一 ResourceKind 过滤显示
    private bool useKindFilter = false;
    private ResourceKind filteredKind;

    void Awake()
    {
        instance=this;
    }
    void Start()
    {
        GenerateItems();
    }
    void OnEnable()
    {
        GenerateItems();
    }
    public void GenerateItems()
    {
        // 先清空旧UI
        ClearItems();

        if (GameValManager.Instance == null)
        {
            Debug.LogError("GameValManager.Instance 未初始化！");
            return;
        }

        // 按 ResourceKind 再按 ResourceType 排序，保证默认“显示全部”时同一类型聚在一起
        var sortedResources = new List<ResourceItem>(GameValManager.Instance.resources);
        sortedResources.Sort((a, b) =>
        {
            int kindCompare = a.resourceKind.CompareTo(b.resourceKind);
            if (kindCompare != 0) return kindCompare;
            return a.type.CompareTo(b.type);
        });

        // 遍历资源列表
        foreach (var item in sortedResources)
        {
            // 只显示有数量的资源
            if (item.count == 0) continue;

            // 如果启用了 Kind 过滤，则只显示指定 Kind
            if (useKindFilter && item.resourceKind != filteredKind) continue;

            // 实例化预制体
            GameObject GO = Instantiate(ItemPrefabs, ItemParent);
            ItemPrefabs script = GO.GetComponent<ItemPrefabs>();
            GO.GetComponent<ItemPrefabs>().resourceType = item.type;
            if (script != null)
            {
                // 设置图标和数量
                script.Icon.sprite = item.Icon;
                script.Amount.text = item.count.ToString();
            }

            currentItems.Add(GO);
        }
    }

    public void ClearItems()
    {
        foreach (var go in currentItems)
        {
            if (go != null) Destroy(go);
        }
        currentItems.Clear();

        // 或者直接清空父物体下所有子物体（更保险）
        for (int i = ItemParent.childCount - 1; i >= 0; i--)
        {
            Destroy(ItemParent.GetChild(i).gameObject);
        }
    }
    // Update is called once per frame
    public void SetFoodInformation(ResourceType resourceType)
    {
        foreach (var item in GameValManager.Instance.resources)
        {
            if (item.type == resourceType)
            {
                InformationTitle.text = item.name;
                InformationDescription.text = item.description;
                break;
            }
        }

    }

    public void ShowOnlyTypeFood()
    {
        ShowOnlyType(ResourceKind.Food);
    }

    public void ShowOnlyTypeFurniture()
    {
        ShowOnlyType(ResourceKind.Furniture);
    }

    public void ShowOnlyTypeOthers()
    {
        ShowOnlyType(ResourceKind.Others);
    }

    public void SendGift()
    {
        print("完成送礼，资源销毁");
        GameValManager.Instance.TryConsumeResource(giftResourceType,1);
        GenerateItems();
    }

    /// <summary>
    /// 仅显示指定 ResourceKind 的物品。
    /// </summary>
    public void ShowOnlyType(ResourceKind kind)
    {
        useKindFilter = true;
        filteredKind = kind;
        GenerateItems();
    }

    /// <summary>
    /// 与 ShowOnlyType 相同，方便在 Inspector 中挂接多个按钮事件。
    /// </summary>
    public void ShowOnlyType_Alt1(ResourceKind kind)
    {
        ShowOnlyType(kind);
    }

    /// <summary>
    /// 与 ShowOnlyType 相同，方便在 Inspector 中挂接多个按钮事件。
    /// </summary>
    public void ShowOnlyType_Alt2(ResourceKind kind)
    {
        ShowOnlyType(kind);
    }

    /// <summary>
    /// 重置为显示所有类型的物品。
    /// </summary>
    public void ShowAllTypes()
    {
        useKindFilter = false;
        GenerateItems();
    }
}
