using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ItemPrefabs : MonoBehaviour
{
    public ResourceType resourceType;
    public Image Icon;
    public Text Amount;
    // Start is called before the first frame update
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {

    }
    public void SetResourcePanelDescription()
    {
        if (ItemBagManager.instance != null)
        {
            ItemBagManager.instance.SetFoodInformation(resourceType);
        }
        ItemBagManager.instance.giftResourceType = resourceType;
        if (ItemBagManager.instance.customerGiftImage)
        {
            foreach (var item in GameValManager.Instance.resources)
            {
                if (item.type == resourceType)
                {
                    ItemBagManager.instance.customerGiftImage.sprite = item.Icon;
                }
            }
        }


    }

    /// <summary>
    /// 在家具背包 UI 中点击，开始摆放对应家具
    /// </summary>
    public void BeginPlaceFurnitureFromUI()
    {
        if (FurnitureUIManager.instance != null)
        {
            FurnitureUIManager.instance.BeginPlaceFurniture(resourceType);
        }
    }

}
