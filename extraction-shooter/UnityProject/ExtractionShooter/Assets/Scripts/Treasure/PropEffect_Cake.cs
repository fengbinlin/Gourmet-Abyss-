using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;
public class PropEffect_Cake : PropEffect
{
    public float OxygenAddRate = 0.1f;
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

        BattleValManager.Instance.oxygenCurrent = math.min(BattleValManager.Instance.OxygenCurrent + BattleValManager.Instance.OxygenMax * OxygenAddRate, BattleValManager.Instance.OxygenMax);

    }
}
