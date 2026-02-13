using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class foodItemPrefabs : MonoBehaviour
{
    public ResourceType resourceType;
    public Image foodIcon;
    public Text foodAmount;
    // Start is called before the first frame update
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {

    }

    /// <summary>
    /// 点击事件（需添加 IPointerClickHandler 接口）
    /// </summary>
    public void SetResourcePanelDescription()
    {
        if (RestaurantPanel.instance != null)
        {
            RestaurantPanel.instance.SetFoodInformation(resourceType);
        }
    }
}
