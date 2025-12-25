using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MainMenuUIController : MonoBehaviour
{
    public GameObject MainUI;
    public List<GameObject> subUIs;
    public GameObject SettingUI;
    public GameObject MakersUI;

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
    public void ReturnToMain()
    {
        MainUI.SetActive(true);
        foreach(GameObject ui in subUIs)
        {
            ui.SetActive(false);
        }
    }

    public void EnterSubUI(int id)
    {
        MainUI.SetActive(false);
        foreach(GameObject ui in subUIs)
        {
            ui.SetActive(false);
        }
        subUIs[id].SetActive(true);
    }
}
