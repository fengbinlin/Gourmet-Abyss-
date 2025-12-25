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

    //切换回主场景
    public void SwitchToHomeScene()
    {
        skillTreeObject.SetActive(false);
        mainSceneObject.SetActive(true);
        mainUI.SetActive(true);
    }

    //切换到技能树场景
    public void SwitchToSkillTree()
    {
        skillTreeObject.SetActive(true);
        mainSceneObject.SetActive(false);
        mainUI.SetActive(false);
        SkillTree.Instance.ReplayRevealAnimation();
    }
}
