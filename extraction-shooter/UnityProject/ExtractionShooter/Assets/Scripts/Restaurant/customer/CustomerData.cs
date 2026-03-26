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
    public string mbti;
    public bool isCook=false; //是否被转换成厨师
    public Sprite NPCIcon;
    public string NPCDescription;
    public string SkillDescripton;
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
    public List<string> QueueJoinWords;                 // 进入排队时
    public List<string> LikePersonEncounterWords;       // 遇见喜欢的人时
    public List<string> PlayerInteractionWords;         // 与玩家交互开场
    public List<string> ConsumeStartWords;              // 开始吃饭
    public List<string> FavouriteDishWords;             // 吃到最爱
    public List<string> NormalDishWords;                // 普通好吃反馈
    public List<string> GiftLikedWords;                 // 收到喜欢礼物
    public List<string> GiftNormalWords;                // 收到普通礼物
    public List<string> HomeGuestWords;                 // 去玩家家做客台词（按顺序播放）
    public List<string> RecruitCookWords;               // 被雇佣为厨师
    public List<string> CookBoostWords;                 // 厨师加速烹饪时
    public List<string> PairChatGreetingWords;          // 顾客互聊-开场
    public List<string> PairChatReplyWords;             // 顾客互聊-回应
    public List<string> PairChatQuestionWords;          // 顾客互聊-提问
    public List<string> PairChatStatusWords;            // 顾客互聊-近况
    public List<string> PairChatInviteWords;            // 顾客互聊-邀请

    [Header("厨师属性")]
    [Tooltip("好感度达到该等级后，允许出现“雇佣/转职厨师”按钮")]
    public int recruitCookRequiredAffectionLevel = 3;
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