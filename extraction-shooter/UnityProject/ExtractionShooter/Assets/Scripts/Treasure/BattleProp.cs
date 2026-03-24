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

    [Header("拾取后销毁动效")]
    [Min(1f)]
    public float pickupPulseScale = 1.25f;
    [Min(0.01f)]
    public float pickupPulseDuration = 0.2f;

    private bool hasPickedUp = false;

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
        if (hasPickedUp)
        {
            return;
        }

        if (other.gameObject.tag == "Player")
        {
            hasPickedUp = true;
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

            Collider col = GetComponent<Collider>();
            if (col != null)
            {
                col.enabled = false;
            }

            StartCoroutine(PlayPickupPulseAndDestroy());
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

    private IEnumerator PlayPickupPulseAndDestroy()
    {
        Vector3 originScale = transform.localScale;
        Vector3 peakScale = originScale * pickupPulseScale;
        float halfDuration = pickupPulseDuration * 0.5f;

        float t = 0f;
        while (t < halfDuration)
        {
            t += Time.deltaTime;
            float lerp = Mathf.Clamp01(t / halfDuration);
            transform.localScale = Vector3.Lerp(originScale, peakScale, lerp);
            yield return null;
        }

        t = 0f;
        while (t < halfDuration)
        {
            t += Time.deltaTime;
            float lerp = Mathf.Clamp01(t / halfDuration);
            transform.localScale = Vector3.Lerp(peakScale, originScale, lerp);
            yield return null;
        }

        transform.localScale = originScale;
        Destroy(gameObject);
    }
}
