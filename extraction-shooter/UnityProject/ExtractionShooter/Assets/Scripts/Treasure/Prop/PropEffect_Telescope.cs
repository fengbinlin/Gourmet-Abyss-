using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PropEffect_Telescope : PropEffect
{
    public float Duration;
    public float Rate;
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
        GameObject.FindGameObjectWithTag("Player").GetComponent<TopDownController>().ApplyRangeBuff(Duration,Rate);
        //BattleValManager.Instance.oxygenCurrent = math.min(BattleValManager.Instance.OxygenCurrent + BattleValManager.Instance.OxygenMax * OxygenAddRate, BattleValManager.Instance.OxygenMax);

    }
}
