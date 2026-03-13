using UnityEngine;
using System.Collections.Generic;
using System;

[CreateAssetMenu(fileName = "CustomerData_", menuName = "GameData/CustomerData", order = 1)]
[Serializable]
public class CustomerData : ScriptableObject
{
    [Header("基本信息")]
    public int id;
    public string customerName;
    public bool isCook=false; //是否被转换成厨师

    public bool isMan;
    public float buyprobability = 0.2f;
    public bool wantToBuyDish;
    public float moveSpeed;

    [Header("好感度")]
    public float affectionValue = 0f;                     // 当前好感度值
    public int affectionLevel = 0;                        // 当前好感度等级
    public List<float> affectionLevelNeeds;               // 每个等级需要的经验（好感度值）

    [Header("喜好配置")]
    public List<int> favouriteFood;                       // 喜欢吃的菜ID
    public List<ResourceType> favouriteItems;             // 喜欢的礼物资源
    public List<int> likePeopleList;
    public List<int> dislikePeopleList;

    [Header("聊天文本")]
    public List<string> SpawningWords;
    public List<string> WalkingToQueueWords;
    public List<string> QueueingWords;
    public List<string> InsideRestaurantQueueingWords;
    public List<string> InsideRestaurantConsumingWords;
    public List<string> LeavingRestaurantWords;
    public List<string> noPlateFoodWords;

    [Header("厨师属性")]
    public float timeReductionRate=1; //时间减少率
    public float outputIncreaseRate=1; //产出增加率

    public float priceIncreaseRate=1; //价格增加率
    //可能得补充特殊能力：能够拷贝顾客、能够消除仇恨、更快生成顾客等


    // 添加好感度增减逻辑
    public void AddAffection(float amount)
    {
        affectionValue += amount;
        CheckAffectionLevel();
    }

    private void CheckAffectionLevel()
    {
        for (int i = 0; i < affectionLevelNeeds.Count; i++)
        {
            if (affectionValue < affectionLevelNeeds[i])
            {
                affectionLevel = i;
                return;
            }
        }
        affectionLevel = affectionLevelNeeds.Count;
    }
}