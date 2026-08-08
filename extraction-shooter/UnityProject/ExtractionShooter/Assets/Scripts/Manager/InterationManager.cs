using Game.Core;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class InterationManager : MonoSingleton<InterationManager>
{
    // 每个场景各一份，后加载的接管——沿用原来的裸赋值语义。
    protected override DuplicatePolicy Duplicate => DuplicatePolicy.OverwriteReference;

    /// <summary>兼容旧调用点的别名，等价于 Instance。</summary>
    public static InterationManager instance => Instance;

    public GameObject mainSceneObject;
    public GameObject mainUI;
    public GameObject skillTreeObject;
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            
        }
    }

    //�л���������
    public void SwitchToHomeScene()
    {
        ShopManager.Instance.ShowShop();
        LevelManager.instance?.RestoreRestaurantFromSkillTree();
        skillTreeObject.SetActive(false);
        mainSceneObject.SetActive(true);
        mainUI.SetActive(true);
        PlayTapBounceDown();
        PlayerStateManager.instance.currentState=PlayerState.UpGround;
    }

    //�л�������������
    public void SwitchToSkillTree()
    {
        ShopManager.Instance.HideShop();
        LevelManager.instance?.MoveRestaurantForSkillTree();
        skillTreeObject.SetActive(true);
        mainSceneObject.SetActive(false);
        mainUI.SetActive(false);
        SkillTree.Instance.ReplayRevealAnimation();
        PlayTapBounceDown();
    }

    private void PlayTapBounceDown()
    {
        UITapBounce bounce = UITapBounce.Instance;
        if (bounce != null) StartCoroutine(bounce.BounceDown());
    }
}
