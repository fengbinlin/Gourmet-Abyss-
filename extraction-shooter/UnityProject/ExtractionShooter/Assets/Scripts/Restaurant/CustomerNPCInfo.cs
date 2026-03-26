using UnityEngine;
using UnityEngine.UI;

public class CustomerNPCInfo : MonoBehaviour
{
    [Header("NPC信息显示")]
    public Text nameText;
    public Text mbtiText;

    public void SetInfo(string customerName, string mbti)
    {
        if (nameText != null)
        {
            nameText.text = customerName;
        }

        if (mbtiText != null)
        {
            mbtiText.text = mbti;
        }
    }
}
