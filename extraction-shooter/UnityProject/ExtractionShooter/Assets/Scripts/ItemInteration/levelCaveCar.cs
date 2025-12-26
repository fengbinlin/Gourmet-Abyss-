// levelCaveCar.cs
using UnityEngine;

public class levelCaveCar : MonoBehaviour
{
    public static levelCaveCar instance;
    public bool canUse = true;
    public string levelName = "Layer1";
    
    private bool isPlayerInTrigger = false;
    private GameObject player;
    
    // 颜色过渡组件引用
    private VehicleColorTransition colorTransition;
    
    private void Awake()
    {
        instance = this;
    }
    
    private void Start()
    {
        // 获取颜色过渡组件
        colorTransition = GetComponent<VehicleColorTransition>();
    }
    
    private void Update()
    {
        if (isPlayerInTrigger && canUse && Input.GetKeyDown(KeyCode.E))
        {
            ToHome();
        }
    }
    
    public void ToHome()
    {
        if (LevelManager.instance == null || LevelManager.instance.IsTransitioning())
            return;
            
        LevelManager.instance.FromLevelToHome(levelName);
        
        canUse = false;
        if (HomeCavecar.homeCavecar != null)
        {
            HomeCavecar.homeCavecar.canUse = true;
        }
        
        if (player != null)
        {
            player.GetComponent<TopDownController>().enabled = false;
        }
    }
    
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            isPlayerInTrigger = true;
            player = other.gameObject;
        }
    }
    
    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            isPlayerInTrigger = false;
            player = null;
        }
    }
}