using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class InterationManager : MonoBehaviour
{
    public static InterationManager instance;
    public GameObject mainSceneObject;
    public GameObject mainUI;
    public GameObject skillTreeObject;
    private void Awake()
    {
        instance = this;
    }
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (skillTreeObject.activeInHierarchy)
            {
                SwitchToHomeScene();
            }
        }
    }

    //�л���������
    public void SwitchToHomeScene()
    {
        ShopManager.Instance.ShowShop();
        skillTreeObject.SetActive(false);
        mainSceneObject.SetActive(true);
        mainUI.SetActive(true);
        StartCoroutine(UITapBounce.Instance.BounceDown()); 
    }

    //�л�������������
    public void SwitchToSkillTree()
    {
        ShopManager.Instance.HideShop();
        skillTreeObject.SetActive(true);
        mainSceneObject.SetActive(false);
        mainUI.SetActive(false);
        SkillTree.Instance.ReplayRevealAnimation();
        StartCoroutine(UITapBounce.Instance.BounceDown()); 
    }
}
