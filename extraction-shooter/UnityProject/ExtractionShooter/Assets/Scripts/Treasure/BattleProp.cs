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
        // 所有战斗道具：统一改为“拾取即生效”（不再走装备栏 + Q）
        EffectImmediate = true;

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

            // 不再放入装备栏（也就不需要按 Q 使用）
            if (TreasureManager.treasureManager != null)
            {
                TreasureManager.treasureManager.hasEquipment = false;
            }

            // 立即生效 + 触发 TopDownController 的粒子/提示
            TreasureManager tm = TreasureManager.treasureManager;
            if (tm != null && tm.treasuresList != null)
            {
                foreach (treasure t in tm.treasuresList)
                {
                    if (t.type != propType) continue;

                    Debug.Log("启用道具：" + t.treasureName);

                    if (t.propEffectObject == null) break;

                    GameObject propEffect = GameObject.Instantiate(t.propEffectObject);

                    float effectDuration = 0.6f; // 食物类效果即时，因此粒子/提示给一个短时段

                    // 从具体道具参数读取“起效持续时间”，用于控制粒子特效隐藏时机
                    if (propType == battlePropType.BodyArmor)
                    {
                        var eff = propEffect.GetComponent<PropEffect_BodyArmor>();
                        effectDuration = eff != null ? Mathf.Max(0.01f, eff.reduceDuration) : 0.6f;
                    }
                    else if (propType == battlePropType.SkateBoard)
                    {
                        var eff = propEffect.GetComponent<PropEffect_SkateBoard>();
                        effectDuration = eff != null ? Mathf.Max(0.01f, eff.Duration) : 0.6f;
                    }
                    else if (propType == battlePropType.Telescope)
                    {
                        var eff = propEffect.GetComponent<PropEffect_Telescope>();
                        effectDuration = eff != null ? Mathf.Max(0.01f, eff.Duration) : 0.6f;
                    }
                    else if (propType == battlePropType.Cake || propType == battlePropType.Noodles)
                    {
                    // 食物即时生效：给一个短暂显示时长即可
                    }

                    TopDownController playerController = other.GetComponentInParent<TopDownController>();
                playerController?.PlayTreasurePickupEffect(propType, effectDuration);

                    var prop = propEffect.GetComponent<PropEffect>();
                    prop?.TakeEffect();

                    Destroy(propEffect, 0.05f);
                    break;
                }
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
