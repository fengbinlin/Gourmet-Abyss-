using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RestaurantUIRefreshOnEnable : MonoBehaviour
{
    public RestaurantPanel restaurantPanel;
    // Start is called before the first frame update
    void Start()
    {

    }
    void OnEnable()
    {
        if (restaurantPanel)
        {
            restaurantPanel.GenerateFoodItems();
            restaurantPanel.GenerateDishList();
        }


    }
    // Update is called once per frame
    void Update()
    {

    }
}
