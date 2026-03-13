using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

public class PropEffect_Noodles : PropEffect
{
    public float OxygenAddRate=0.3f;
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
        
        BattleValManager.Instance.oxygenCurrent=math.min(BattleValManager.Instance.OxygenCurrent+BattleValManager.Instance.OxygenMax*OxygenAddRate,BattleValManager.Instance.OxygenMax);
        
    }
}
