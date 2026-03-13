using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PropEffect_BodyArmor : PropEffect
{
    public float reduceDuration;
    public float reduceRate;
    // Start is called before the first frame update
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {

    }
    override public void TakeEffect()
    {
        GameObject.FindGameObjectWithTag("Player").GetComponent<TopDownController>().ApplyDamageReduction(reduceDuration,reduceRate);
        //BattleValManager.Instance.oxygenCurrent = math.min(BattleValManager.Instance.OxygenCurrent + BattleValManager.Instance.OxygenMax * OxygenAddRate, BattleValManager.Instance.OxygenMax);

    }
}
