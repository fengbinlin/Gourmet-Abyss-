using System.Collections;
using System.Collections.Generic;
using TransitionsPlus;
using UnityEngine;

public class KeepMainCamera : MonoBehaviour
{
    public static KeepMainCamera instance;
    public TransitionAnimator transitionAnimator;
    public Canvas mainUICanvas;
    void Awake()
    {
        instance=this;

    }
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
    public void tKeepMainCamera()
    {
        print("相机切换");
        print(Camera.main.name);
        transitionAnimator.mainCamera=Camera.main;
        mainUICanvas.worldCamera=Camera.main;
    }
}
