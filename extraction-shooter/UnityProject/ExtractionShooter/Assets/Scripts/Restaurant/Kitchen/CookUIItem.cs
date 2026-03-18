using UnityEngine;
using UnityEngine.UI;

public class CookUIItem : MonoBehaviour
{
    [Header("UI组件绑定")]
    public Image npcIconImage;
    public Text npcNameText;
    public Text npcDescriptionText;
    public Text npcSkillDescriptionText;

    [Header("数据引用")]
    public CustomerNPC currentChefNPC; // 当前条目对应的雇佣NPC

    [Header("按钮")]
    public Button fireButton; // 解雇按钮

    private CookUIManager ownerManager;

    public void Initialize(CustomerNPC chefNpc, CookUIManager manager)
    {
        ownerManager = manager;
        currentChefNPC = chefNpc;

        if (chefNpc == null || chefNpc.data == null) return;

        if (npcIconImage != null) npcIconImage.sprite = chefNpc.data.NPCIcon;
        if (npcNameText != null) npcNameText.text = chefNpc.data.customerName;
        if (npcDescriptionText != null) npcDescriptionText.text = chefNpc.data.NPCDescription;
        if (npcSkillDescriptionText != null) npcSkillDescriptionText.text = chefNpc.data.SkillDescripton;

        if (fireButton != null)
        {
            fireButton.onClick.RemoveAllListeners();
            fireButton.onClick.AddListener(() =>
            {
                if (ownerManager == null) return;
                ownerManager.FireChef(currentChefNPC);
            });
        }
    }
}

