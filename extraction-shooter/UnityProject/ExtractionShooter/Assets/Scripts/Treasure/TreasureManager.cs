using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UI;

[Serializable]
public class treasure
{
    public battlePropType type;
    public bool takeEffectImmeditely = false;
    public bool isUnLocked = false;
    public GameObject treasureObject;
    public float timeNeedToHold = 2;
    public string treasureName;
    public Sprite treasureIcon;
    public GameObject propEffectObject;
}

public class TreasureManager : MonoBehaviour
{
    public static TreasureManager treasureManager;
    public List<treasure> treasuresList;
    // Start is called before the first frame update
    //UI层
    public Image propIcon;
    public Text propNameText;
    public bool hasEquipment=false;
    public treasure currentTreasure;
    public GameObject ButtonTips;
    void Start()
    {
        treasureManager = this;
        currentTreasure = null;
        hasEquipment=false;
    }

    // Update is called once per frame
    void Update()
    {
        if (hasEquipment!=false)
        {
            propNameText.text = currentTreasure.treasureName;
            propIcon.sprite = currentTreasure.treasureIcon;

            propIcon.gameObject.SetActive(true);
            propNameText.gameObject.SetActive(true);
            ButtonTips.gameObject.SetActive(true);
        }
        else
        {
            propNameText.text = "";
            propIcon.sprite = null;
            propIcon.gameObject.SetActive(false);
            propNameText.gameObject.SetActive(false);
            ButtonTips.gameObject.SetActive(false);
        }
        if (Input.GetKeyDown(KeyCode.Q))
        {
            if (hasEquipment!=false)
            {
                Debug.Log("启用道具：" + currentTreasure.treasureName);
                GameObject propEffect = GameObject.Instantiate(currentTreasure.propEffectObject);
                propEffect.GetComponent<PropEffect>().TakeEffect();
                Destroy(propEffect, 0.1f);
                propNameText.text = "";
                propIcon.sprite = null;
                currentTreasure.type = battlePropType.none;
                hasEquipment=false;
            }
        }
    }

    public void AddToEquipmentBar(battlePropType type)
    {
        foreach (treasure t in treasuresList)
        {
            if (t.type == type)
            {
                currentTreasure = t;
            }
        }
        propNameText.text = currentTreasure.treasureName;
        propIcon.sprite = currentTreasure.treasureIcon;
        hasEquipment=true;

    }

}
