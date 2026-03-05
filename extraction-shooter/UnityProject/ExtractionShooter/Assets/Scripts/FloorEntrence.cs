using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FloorEntrence : MonoBehaviour
{
    public GameObject objectUp;
    public GameObject objectDown;
    public bool isPlayerUp=false;
    public bool isPlayerEnterArea=false;
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        if (isPlayerEnterArea)
        {
            if (Input.GetKeyDown(KeyCode.E))
            {
                if (isPlayerUp)
                {
                    GameObject.FindGameObjectWithTag("Player").transform.position=objectDown.transform.position;
                }
                else
                {
                    GameObject.FindGameObjectWithTag("Player").transform.position=objectUp.transform.position;
                }
                isPlayerUp=!isPlayerUp;
                
            }
        }
    }
    public void OnTriggerEnter(Collider other)
    {
        if(other.gameObject.CompareTag("Player"))
        isPlayerEnterArea=true;
    }

    void OnTriggerExit(Collider other)
    {
        if(other.gameObject.CompareTag("Player"))
        isPlayerEnterArea=false;
    }

}
