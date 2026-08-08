using Game.Core;
using System.Collections;
using System.Collections.Generic;
using TransitionsPlus;
using UnityEngine;

public class KeepMainCamera : MonoSingleton<KeepMainCamera>
{
    // 每个场景各一份，后加载的接管——沿用原来的裸赋值语义。
    protected override DuplicatePolicy Duplicate => DuplicatePolicy.OverwriteReference;

    /// <summary>兼容旧调用点的别名，等价于 Instance。</summary>
    public static KeepMainCamera instance => Instance;

    public TransitionAnimator transitionAnimator;
    public Canvas mainUICanvas;
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
