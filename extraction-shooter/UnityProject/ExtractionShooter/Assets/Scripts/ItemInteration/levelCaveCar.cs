using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class levelCaveCar : MonoBehaviour
{
    public static levelCaveCar instance;
    public bool canUse = true;
    private bool isPlayerInTrigger = false; // 标记玩家是否在触发区域内
    private GameObject player; // 存储玩家引用
    public string levelName = "Layer1";
    // Start is called before the first frame update
    void Awake()
    {
        instance=this;
    }
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        // 如果玩家在触发区域内，且按下E键，且canUse为true
        if (isPlayerInTrigger && canUse && Input.GetKeyDown(KeyCode.E))
        {
            ToHome();
        }
    }
    public void ToHome()
    {
        // 执行场景切换
            LevelManager.instance.FromLevelToHome(levelName);
            canUse = false;
            HomeCavecar.homeCavecar.canUse = true;
            
            // 禁用玩家移动
            if (player != null)
            {
                player.GetComponent<TopDownController>().enabled = false;
            }
    }
    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            isPlayerInTrigger = true;
            player = other.gameObject; // 保存玩家引用
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            isPlayerInTrigger = false;
            player = null; // 清空玩家引用
        }
    }
}