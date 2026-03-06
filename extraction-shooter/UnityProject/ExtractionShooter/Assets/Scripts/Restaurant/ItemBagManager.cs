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

        // 遍历资源列表
        foreach (var item in GameValManager.Instance.resources)
        {
            // 只显示食物类资源
            if (item.resourceKind == ResourceKind.Food||item.count==0) continue;

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

    public void SendGift()
    {
        print("完成送礼，资源销毁");
        GameValManager.Instance.TryConsumeResource(giftResourceType,1);
        GenerateItems();
    }
}
