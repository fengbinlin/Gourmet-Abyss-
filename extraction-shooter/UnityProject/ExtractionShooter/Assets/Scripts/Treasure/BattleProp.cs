using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum battlePropType
{
    none,
    Noodles,
    Cake,
    Telescope,
    BodyArmor,
    SkateBoard
}

public class BattleProp : MonoBehaviour
{
    public battlePropType propType;
    public bool EffectImmediate;
    public bool isPlayerEnter;

    // Start is called before the first frame update
    void Start()
    {
        //立即生效
        foreach (treasure t in TreasureManager.treasureManager.treasuresList)
        {
            if (t.type == propType)
            {
                EffectImmediate = t.takeEffectImmeditely;
            }
        }

    }

    // Update is called once per frame
    void Update()
    {
        // if (isPlayerEnter)
        // {
        //     if (Input.GetKeyDown(KeyCode.E))
        //     {

        //     }
        // }

    }

    void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.tag == "Player")
        {
            isPlayerEnter = true;
            Debug.Log("拾起道具");
            //如果是食物，直接奏效，如果是道具，添加到便携装备栏
            if (EffectImmediate)
            {
                //transform.GetComponent<PropEffect>().TakeEffect();

                //立即生效
                foreach (treasure t in TreasureManager.treasureManager.treasuresList)
                {
                    if (t.type == propType)
                    {
                        Debug.Log("启用道具：" + t.treasureName);
                        GameObject propEffect = GameObject.Instantiate(t.propEffectObject);
                        propEffect.GetComponent<PropEffect>().TakeEffect();
                        Destroy(propEffect, 0.05f);
                    }
                }
            }
            else
            {
                //放进装备栏
                TreasureManager.treasureManager.AddToEquipmentBar(propType);
            }
            GameObject.Destroy(gameObject, 0.2f);
        }
    }
    void OnTriggerExit(Collider other)
    {
        if (other.gameObject.tag == "Player")
        {
            isPlayerEnter = false;
        }
    }
    //当拾起时
    public void pickUp()
    {

    }
}
