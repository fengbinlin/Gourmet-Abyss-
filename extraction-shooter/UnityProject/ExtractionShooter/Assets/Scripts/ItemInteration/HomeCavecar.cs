using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Video;

public class HomeCavecar : MonoBehaviour
{
    public static HomeCavecar homeCavecar;
    public bool canUse = true;
    public GameObject MapUI;
    public bool isPlayerEnter = false;
    // Start is called before the first frame update
    void Start()
    {
        homeCavecar = this;
    }

    // Update is called once per frame
    void Update()
    {
        if (isPlayerEnter)
        {
            if (Input.GetKeyDown(KeyCode.E))
            {
                MapUI.SetActive(true);
            }
        }
    }
    void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.CompareTag("Player"))
        {
            if (canUse)
            {
                isPlayerEnter = true;
                // print("切换场景");
                // LevelManager.instance.EnterLevel();
                // other.gameObject.transform.position += new Vector3(2, 0, 0);
                // canUse = false;
                // GameObject.FindGameObjectWithTag("Player").GetComponent<TopDownController>().enabled=false;
                
            }
        }

    }
    void OnTriggerExit(Collider other)
    {
        if (other.gameObject.CompareTag("Player"))
        {
            isPlayerEnter = false;
            MapUI.SetActive(false);
        }
                
    }
}
