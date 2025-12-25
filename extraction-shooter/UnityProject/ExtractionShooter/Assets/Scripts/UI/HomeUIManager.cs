using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class HomeUIManager : MonoBehaviour
{
    public Text textMoneyVal;
    public Text textF1; 
    public Text textF2;
    public Text textF3;
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        if(GameValManager.Instance!=null)
        {
            textMoneyVal.text = GameValManager.Instance.GetResourceCount(ResourceType.Money).ToString();
            textF1.text = GameValManager.Instance.GetResourceCount(ResourceType.LootPumkin).ToString();
            textF2.text = GameValManager.Instance.GetResourceCount(ResourceType.LootOnion).ToString();
            textF3.text = GameValManager.Instance.GetResourceCount(ResourceType.LootPear).ToString();
        }
    }
}
