using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class LevelManager : MonoBehaviour
{
    public static LevelManager instance;

    public GameObject homeSceneObject;

    public EmissionTransition emissionTransition;
    public SaturationTransition saturationTransition;
    public Animator TransitionUIAnimator;

    [Header("过渡设置")]
    [Tooltip("是否在进入关卡时自动触发过渡效果")]
    [SerializeField] private bool autoEnterLevel = false;

    [Tooltip("触发过渡效果的延迟时间（秒）")]
    [SerializeField] private float transitionDelay = 0.5f;

    [Tooltip("是否在过渡完成后自动切换状态")]
    [SerializeField] private bool autoToggleAfterTransition = true;

    [Tooltip("自动切换状态的延迟时间（秒）")]
    [SerializeField] private float toggleDelay = 3.0f;

    [Header("场景设置")]
    [Tooltip("要加载的关卡场景名称")]
    [SerializeField] private string levelSceneName = "Layer1";

    // 私有变量
    private bool isInLevel = false;

    // Start is called before the first frame update
    void Start()
    {
        instance = this;

        // 如果启用自动进入关卡
        if (autoEnterLevel)
        {
            // 延迟后自动进入关卡
            StartCoroutine(DelayedEnterLevel(transitionDelay));
        }
    }

    /// <summary>
    /// 进入关卡
    /// 逻辑：从发光变成不发光，目标层级是0
    /// </summary>
    public void EnterLevel(string sceneName = null)
    {
        //if (isInLevel) return;

        //Debug.Log("进入关卡...");

        // 触发过渡效果
        StartCoroutine(EnterLevelTransition(sceneName));

        //isInLevel = true;
    }

    /// <summary>
    /// 离开关卡
    /// 逻辑：从发光变成不发光，目标层级是10
    /// </summary>
    public void ExitLevel()
    {
        if (!isInLevel) return;

        //Debug.Log("离开关卡...");

        // 触发离开过渡效果
        StartCoroutine(ExitLevelTransition());

        isInLevel = false;
    }


    public void FromLevelToHome(string sceneName = null)
    {
        StartCoroutine(FromLevelToHomeProcess(sceneName));


    }

    private IEnumerator FromLevelToHomeProcess(string sceneName = null)
    {

        if (TransitionUIAnimator != null)
        {
            TransitionUIAnimator.SetTrigger("EnterLevel");
            //Debug.Log("触发UI动画: EnterLevel");
        }

        yield return new WaitForSeconds(1f);

        // 异步加载场景
        AsyncOperation asyncLoad = SceneManager.UnloadSceneAsync(sceneName);
        saturationTransition.TransitionToSaturated();
        homeSceneObject.SetActive(true);
        asyncLoad.allowSceneActivation = true;
        BattleValManager.Instance.ResetValues();
        BattleValManager.Instance.StopConsuming();
        // 等待场景加载完成
        while (!asyncLoad.isDone)
        {
            yield return null;
        }
        ShopManager.Instance.ShowShop();
        StartCoroutine(UITapBounce.Instance.BounceDown()); 
        GameObject.FindGameObjectWithTag("Player").GetComponent<TopDownController>().enabled = true;

    }


    /// <summary>
    /// 进入关卡的过渡效果
    /// 从发光变成不发光，目标层级是0
    /// </summary>
    private IEnumerator EnterLevelTransition(string sceneName = null)
    {
        //Debug.Log("开始进入关卡过渡：发光->不发光，层级设为0");

        // 1. 首先触发UI动画
        if (TransitionUIAnimator != null)
        {
            TransitionUIAnimator.SetTrigger("EnterLevel");
            //Debug.Log("触发UI动画: EnterLevel");
        }

        // 2. 触发饱和度过渡（从饱和变为不饱和）
        if (saturationTransition != null)
        {
            // 从当前饱和度过渡到不饱和(0)
            saturationTransition.TransitionToUnsaturated(); // 过渡到不饱和
            //Debug.Log("开始饱和度过渡: 从饱和到不饱和");
        }
        else
        {
            //Debug.LogWarning("SaturationTransition 未赋值！");
        }

        // 3. 触发自发光过渡（从发光变为不发光，层级设为0）
        if (emissionTransition != null)
        {
            // 调用专门的进入关卡过渡方法
            emissionTransition.ExitLevelTransition();
            //Debug.Log("开始自发光过渡: 从发光到不发光，层级设为0");
        }
        else
        {
            //Debug.LogWarning("EmissionTransition 未赋值！");
        }

        // 等待过渡完成
        yield return new WaitForSeconds(1f);

        // 异步加载场景
        AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
        ShopManager.Instance.HideShop();
        StartCoroutine(UITapBounce.Instance.BounceDown()); 
        homeSceneObject.SetActive(false);
        asyncLoad.allowSceneActivation = true;
        BattleValManager.Instance.ResetValues();
        BattleValManager.Instance.StartConsuming();
        // 等待场景加载完成
        while (!asyncLoad.isDone)
        {
            yield return null;
        }

        //Debug.Log($"加载场景完成: {levelSceneName}");
    }

    /// <summary>
    /// 离开关卡的过渡效果
    /// 从发光变成不发光，目标层级是10
    /// </summary>
    private IEnumerator ExitLevelTransition()
    {
        //Debug.Log("开始离开关卡过渡：发光->不发光，层级设为10");

        // 1. 触发UI动画
        if (TransitionUIAnimator != null)
        {
            TransitionUIAnimator.SetTrigger("ExitLevel");
            //Debug.Log("触发UI动画: ExitLevel");
        }

        // 2. 触发饱和度过渡（从不饱和变为饱和）
        if (saturationTransition != null)
        {
            // 从不饱和到饱和
            saturationTransition.ReverseTransition();
            //Debug.Log("开始饱和度过渡: 从不饱和到饱和");
        }
        else
        {
            //Debug.LogWarning("SaturationTransition 未赋值！");
        }

        // 3. 触发自发光过渡（从发光变为不发光，层级设为10）
        if (emissionTransition != null)
        {
            // 调用专门的离开关卡过渡方法
            emissionTransition.ExitLevelTransition();
            //Debug.Log("开始自发光过渡: 从发光到不发光，层级设为10");
        }
        else
        {
            //Debug.LogWarning("EmissionTransition 未赋值！");
        }

        // 等待过渡完成
        yield return new WaitForSeconds(transitionDelay);
        ShopManager.Instance.ShowShop();
        StartCoroutine(UITapBounce.Instance.BounceDown()); 
        // 这里可以加载其他场景，比如主菜单
        // SceneManager.LoadSceneAsync("MainMenu");

        //Debug.Log("离开关卡完成");
    }

    /// <summary>
    /// 切换关卡状态
    /// </summary>
    public void ToggleLevelState()
    {
        if (isInLevel)
        {
            ExitLevel();
        }
        else
        {
            EnterLevel();
        }
    }

    /// <summary>
    /// 手动触发进入关卡
    /// </summary>
    public void TriggerEnterLevel()
    {
        if (!isInLevel)
        {
            EnterLevel();
        }
    }

    /// <summary>
    /// 手动触发离开关卡
    /// </summary>
    public void TriggerExitLevel()
    {
        if (isInLevel)
        {
            ExitLevel();
        }
    }

    /// <summary>
    /// 设置要加载的关卡场景
    /// </summary>
    public void SetLevelScene(string sceneName)
    {
        levelSceneName = sceneName;
        //Debug.Log($"设置关卡场景为: {sceneName}");
    }

    /// <summary>
    /// 加载指定场景
    /// </summary>
    public void LoadScene(string sceneName)
    {
        if (!string.IsNullOrEmpty(sceneName))
        {
            StartCoroutine(LoadSceneWithTransition(sceneName));
        }
        else
        {
            //Debug.LogError("场景名称为空！");
        }
    }

    /// <summary>
    /// 带过渡效果的场景加载
    /// </summary>
    private IEnumerator LoadSceneWithTransition(string sceneName)
    {
        //Debug.Log($"开始加载场景: {sceneName}");

        // 1. 先执行离开过渡效果
        yield return StartCoroutine(ExitLevelTransition());

        // 2. 异步加载场景
        AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
        homeSceneObject.SetActive(false);
        asyncLoad.allowSceneActivation = true;

        // 等待场景加载完成
        while (!asyncLoad.isDone)
        {
            yield return null;
        }

        //Debug.Log($"场景加载完成: {sceneName}");
    }

    /// <summary>
    /// 延迟进入关卡
    /// </summary>
    private IEnumerator DelayedEnterLevel(float delay)
    {
        yield return new WaitForSeconds(delay);
        EnterLevel();
    }

    /// <summary>
    /// 测试功能：在编辑器中触发进入关卡
    /// </summary>
    [ContextMenu("测试进入关卡")]
    public void TestEnterLevel()
    {
        EnterLevel();
    }

    /// <summary>
    /// 测试功能：在编辑器中触发离开关卡
    /// </summary>
    [ContextMenu("测试离开关卡")]
    public void TestExitLevel()
    {
        ExitLevel();
    }

    /// <summary>
    /// 测试功能：切换关卡状态
    /// </summary>
    [ContextMenu("测试切换状态")]
    public void TestToggleLevelState()
    {
        ToggleLevelState();
    }
}