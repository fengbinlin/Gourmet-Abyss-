using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BagRefreshOnEnable : MonoBehaviour
{
    public ItemBagManager itemBagManager;
    void OnEnable()
    {
        itemBagManager.GenerateItems();
    }
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
