using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class LevelManager : MonoBehaviour
{
    public GameObject mainUI;
    public Text TitleText;
    public static LevelManager instance;

    [Header("场景对象")]
    public GameObject homeSceneObject;

    [Header("过渡系统")]
    public EmissionTransition emissionTransition;
    public SaturationTransition saturationTransition;
    public Animator transitionUIAnimator;

    [Header("过渡设置")]
    [SerializeField] private float transitionDuration = 1.0f;
    [SerializeField] private float uiAnimationDelay = 0.5f;

    [Header("场景设置")]
    [SerializeField] private string levelSceneName = "Layer1";

    // 私有变量
    private bool isTransitioning = false;
    private List<string> loadedLevels = new List<string>();

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        TitleText.text = SceneTitle.instance.SceneName;
        // TransitionUIAnimator启用时自动播放第一个动画
        if (transitionUIAnimator != null && transitionUIAnimator.enabled)
        {
            transitionUIAnimator.Play("DefaultState", 0, 0f);
        }
    }

    /// <summary>
    /// 进入关卡
    /// </summary>
    public void EnterLevel(string sceneName = null)
    {
        if (isTransitioning) return;

        string targetScene = sceneName ?? levelSceneName;
        StartCoroutine(EnterLevelProcess(targetScene));
    }

    /// <summary>
    /// 离开关卡返回主场景
    /// </summary>
    public void ExitLevel()
    {
        if (isTransitioning || loadedLevels.Count == 0) return;

        string currentLevel = loadedLevels[loadedLevels.Count - 1];
        StartCoroutine(ExitLevelProcess(currentLevel));
    }

    /// <summary>
    /// 从关卡返回主场景
    /// </summary>
    public void FromLevelToHome(string sceneName = null)
    {
        if (isTransitioning) return;

        string targetScene = sceneName;
        if (string.IsNullOrEmpty(targetScene) && loadedLevels.Count > 0)
        {
            targetScene = loadedLevels[loadedLevels.Count - 1];
        }

        StartCoroutine(FromLevelToHomeProcess(targetScene));
    }

    /// <summary>
    /// 切换关卡
    /// </summary>
    public void SwitchLevel(string fromLevel, string toLevel)
    {
        if (isTransitioning) return;

        StartCoroutine(SwitchLevelProcess(fromLevel, toLevel));
    }

    private IEnumerator EnterLevelProcess(string levelName)
    {
        isTransitioning = true;

        // 1. 触发UI动画
        if (transitionUIAnimator != null)
        {
            transitionUIAnimator.SetTrigger("EnterLevel");
        }

        yield return new WaitForSeconds(uiAnimationDelay);

        // 2. 首先将主场景车辆从原色过渡到白色
        VehicleColorTransition homeVehicle = FindVehicleInScene("UpGround");
        if (homeVehicle != null)
        {
            Debug.Log("主场景车辆开始过渡到白色");

            // 修改：在开始过渡前设置层级为10
            homeVehicle.SetLayer(10);
            homeVehicle.TransitionToWhite(transitionDuration);
        }

        // 3. 其他过渡效果
        if (saturationTransition != null)
        {
            saturationTransition.TransitionToUnsaturated();
        }

        if (emissionTransition != null)
        {
            emissionTransition.EnterLevelTransition();
        }

        // 等待车辆过渡完成
        yield return new WaitForSeconds(transitionDuration);

        // 4. 确保主场景车辆完全变成白色
        if (homeVehicle != null)
        {
            // 立即设置为白色，确保在加载新场景前完成
            homeVehicle.SetToWhiteImmediate();
        }

        Debug.Log($"车辆已变成白色，开始加载场景: {levelName}");

        // 5. 加载关卡场景
        AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(levelName, LoadSceneMode.Additive);
        asyncLoad.allowSceneActivation = true;

        while (!asyncLoad.isDone)
        {
            yield return null;
        }
        UITapBounce.Instance.ResetPosition();
        TitleText.text = SceneTitle.instance.SceneName;
        mainUI.SetActive(false);
        mainUI.SetActive(true);
        // 6. 隐藏主场景物体
        if (homeSceneObject != null)
        {
            homeSceneObject.SetActive(false);
        }
        KeepMainCamera.instance.tKeepMainCamera();

        // 7. 新场景车辆从白色过渡到原色
        // 注意：这里需要等待一帧确保新场景完全加载
        yield return null;

        VehicleColorTransition levelVehicle = FindVehicleInScene(levelName);

        if (levelVehicle != null)
        {
            Debug.Log("新场景车辆开始从白色过渡到原色");

            // 修改：开始时设置层级为10
            levelVehicle.SetLayer(10);

            // 确保车辆脚本启用
            levelVehicle.enabled = true;

            // 立即设置为白色
            levelVehicle.SetToWhiteImmediate();

            // 从白色过渡到原色
            levelVehicle.TransitionToOriginal(transitionDuration);

            // 修改：从不发光变成发光后，层级变为11
            StartCoroutine(SetVehicleLayerAfterDelay(levelVehicle, 11, transitionDuration));
        }
        else
        {
            Debug.LogWarning($"在场景 {levelName} 中未找到VehicleColorTransition组件");
        }

        loadedLevels.Add(levelName);
        isTransitioning = false;
    }

    private IEnumerator ExitLevelProcess(string levelName)
    {
        isTransitioning = true;

        // 1. 触发UI动画
        if (transitionUIAnimator != null)
        {
            transitionUIAnimator.SetTrigger("ExitLevel");
        }

        yield return new WaitForSeconds(uiAnimationDelay);

        // 2. 当前关卡车辆从原色过渡到白色
        VehicleColorTransition levelVehicle = FindVehicleInScene(levelName);
        if (levelVehicle != null)
        {
            Debug.Log("关卡车辆开始过渡到白色");

            // 修改：离开场景前，设置层级为10
            levelVehicle.SetLayer(10);
            levelVehicle.TransitionToWhite(transitionDuration);
        }

        // 3. 其他过渡效果
        if (saturationTransition != null)
        {
            saturationTransition.TransitionToSaturated();
        }

        if (emissionTransition != null)
        {
            emissionTransition.ExitLevelTransition();
        }

        // 等待车辆过渡完成
        yield return new WaitForSeconds(transitionDuration);

        // 4. 确保关卡车辆完全变成白色
        if (levelVehicle != null)
        {
            levelVehicle.SetToWhiteImmediate();
        }

        Debug.Log("关卡车辆已变成白色，开始卸载场景");

        // 5. 卸载关卡场景
        AsyncOperation asyncUnload = SceneManager.UnloadSceneAsync(levelName);
        asyncUnload.allowSceneActivation = true;
        UITapBounce.Instance.ResetPosition();
        mainUI.SetActive(false);
        mainUI.SetActive(true);
        while (!asyncUnload.isDone)
        {
            yield return null;
        }

        // 6. 显示主场景物体
        if (homeSceneObject != null)
        {
            homeSceneObject.SetActive(true);
        }

        // 7. 主场景车辆从白色过渡到原色
        VehicleColorTransition homeVehicle = FindVehicleInScene("HomeScene");
        if (homeVehicle != null)
        {
            Debug.Log("主场景车辆开始从白色过渡到原色");

            // 修改：开始时设置层级为10
            homeVehicle.SetLayer(10);

            // 确保车辆脚本启用
            homeVehicle.enabled = true;

            // 立即设置为白色
            homeVehicle.SetToWhiteImmediate();

            // 从白色过渡到原色
            homeVehicle.TransitionToOriginal(transitionDuration);

            // 修改：从不发光变成发光后，层级变为11
            StartCoroutine(SetVehicleLayerAfterDelay(homeVehicle, 11, transitionDuration));
        }

        loadedLevels.Remove(levelName);
        isTransitioning = false;
    }

    private IEnumerator FromLevelToHomeProcess(string levelName)
    {
        isTransitioning = true;

        // 1. 触发UI动画
        if (transitionUIAnimator != null)
        {
            transitionUIAnimator.SetTrigger("EnterLevel");
        }

        yield return new WaitForSeconds(uiAnimationDelay);

        // 2. 当前关卡车辆从原色过渡到白色
        VehicleColorTransition levelVehicle = FindVehicleInScene(levelName);
        if (levelVehicle != null)
        {
            // 修改：离开场景前，设置层级为10
            levelVehicle.SetLayer(10);
            levelVehicle.TransitionToWhite(transitionDuration);
        }

        // 3. 其他过渡效果
        if (saturationTransition != null)
        {
            saturationTransition.TransitionToSaturated();
        }

        // 等待车辆过渡完成
        yield return new WaitForSeconds(transitionDuration);

        // 4. 确保关卡车辆完全变成白色
        if (levelVehicle != null)
        {
            levelVehicle.SetToWhiteImmediate();
        }

        // 5. 卸载关卡场景
        AsyncOperation asyncUnload = SceneManager.UnloadSceneAsync(levelName);
        asyncUnload.allowSceneActivation = true;

        while (!asyncUnload.isDone)
        {
            yield return null;
        }

        // 6. 显示主场景物体
        if (homeSceneObject != null)
        {
            homeSceneObject.SetActive(true);
        }
        KeepMainCamera.instance.tKeepMainCamera();
        // 7. 主场景车辆从白色过渡到原色
        VehicleColorTransition homeVehicle = FindVehicleInScene("HomeScene");
        if (homeVehicle != null)
        {
            // 修改：开始时设置层级为10
            homeVehicle.SetLayer(10);

            homeVehicle.enabled = true;
            homeVehicle.SetToWhiteImmediate();
            homeVehicle.TransitionToOriginal(transitionDuration);

            // 修改：从不发光变成发光后，层级变为11
            StartCoroutine(SetVehicleLayerAfterDelay(homeVehicle, 11, transitionDuration));
        }
        UITapBounce.Instance.ResetPosition();
        mainUI.SetActive(false);
        mainUI.SetActive(true);
        // 8. 重置游戏状态
        BattleValManager.Instance?.ResetValues();
        BattleValManager.Instance?.StopConsuming();
        GameObject.FindGameObjectWithTag("Player").GetComponent<TopDownController>().enabled = true;

        loadedLevels.Remove(levelName);
        isTransitioning = false;
    }

    private IEnumerator SwitchLevelProcess(string fromLevel, string toLevel)
    {
        isTransitioning = true;

        // 1. 触发UI动画
        if (transitionUIAnimator != null)
        {
            transitionUIAnimator.SetTrigger("SwitchLevel");
        }

        yield return new WaitForSeconds(uiAnimationDelay);

        // 2. 当前关卡车辆从原色过渡到白色
        VehicleColorTransition fromVehicle = FindVehicleInScene(fromLevel);
        if (fromVehicle != null)
        {
            // 修改：离开场景前，设置层级为10
            fromVehicle.SetLayer(10);
            fromVehicle.TransitionToWhite(transitionDuration);
        }

        // 3. 其他过渡效果
        if (saturationTransition != null)
        {
            saturationTransition.TransitionToUnsaturated();
        }

        if (emissionTransition != null)
        {
            emissionTransition.ExitLevelTransition();
        }

        // 等待车辆过渡完成
        yield return new WaitForSeconds(transitionDuration);

        // 4. 确保当前关卡车辆完全变成白色
        if (fromVehicle != null)
        {
            fromVehicle.SetToWhiteImmediate();
        }

        Debug.Log("当前关卡车辆已变成白色，开始切换场景");

        // 5. 卸载当前关卡
        AsyncOperation asyncUnload = SceneManager.UnloadSceneAsync(fromLevel);
        asyncUnload.allowSceneActivation = true;

        // 6. 加载新关卡
        AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(toLevel, LoadSceneMode.Additive);
        asyncLoad.allowSceneActivation = true;

        while (!asyncUnload.isDone || !asyncLoad.isDone)
        {
            yield return null;
        }
        KeepMainCamera.instance.tKeepMainCamera();
        // 7. 新关卡车辆从白色过渡到原色
        VehicleColorTransition toVehicle = FindVehicleInScene(toLevel);
        if (toVehicle != null)
        {
            Debug.Log("新关卡车辆开始从白色过渡到原色");

            // 修改：开始时设置层级为10
            toVehicle.SetLayer(10);

            toVehicle.enabled = true;
            toVehicle.SetToWhiteImmediate();
            toVehicle.TransitionToOriginal(transitionDuration);

            // 修改：从不发光变成发光后，层级变为11
            StartCoroutine(SetVehicleLayerAfterDelay(toVehicle, 11, transitionDuration));
        }
        UITapBounce.Instance.ResetPosition();
        mainUI.SetActive(false);
        mainUI.SetActive(true);
        // 更新关卡列表
        loadedLevels.Remove(fromLevel);
        loadedLevels.Add(toLevel);

        isTransitioning = false;
    }

    /// <summary>
    /// 延迟设置车辆层级
    /// </summary>
    private IEnumerator SetVehicleLayerAfterDelay(VehicleColorTransition vehicle, int layer, float delay)
    {
        yield return new WaitForSeconds(delay);
        vehicle.SetLayer(layer);
    }

    /// <summary>
    /// 在指定场景中查找车辆
    /// </summary>
    private VehicleColorTransition FindVehicleInScene(string sceneName)
    {
        Scene scene = SceneManager.GetSceneByName(sceneName);
        if (!scene.IsValid())
        {
            // 尝试在主活动场景中查找
            scene = SceneManager.GetActiveScene();
            if (scene.name != sceneName)
            {
                return null;
            }
        }

        GameObject[] rootObjects = scene.GetRootGameObjects();
        foreach (GameObject obj in rootObjects)
        {
            VehicleColorTransition vehicle = obj.GetComponentInChildren<VehicleColorTransition>(true);
            if (vehicle != null)
            {
                return vehicle;
            }
        }

        return null;
    }

    /// <summary>
    /// 检查是否正在过渡
    /// </summary>
    public bool IsTransitioning()
    {
        return isTransitioning;
    }
}
