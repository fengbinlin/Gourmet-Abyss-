using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
using System;

public enum PlayerState
{
    UpGround,
    UI,
    Battle
}

public class PlayerStateManager : MonoBehaviour
{
    public static PlayerStateManager instance;
    public PlayerState currentState;
    public bool isSettingUIActive = false;
    public GameObject SetttingPanel;
    // Start is called before the first frame update
    void Awake()
    {
        instance = this;
    }
    void Start()
    {
        DontDestroyOnLoad(this.gameObject);
        currentState = PlayerState.UpGround;
    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (InterationManager.instance.skillTreeObject.activeInHierarchy)
            {
                InterationManager.instance.SwitchToHomeScene();
            }
            else
            {
                if (currentState == PlayerState.Battle || currentState == PlayerState.UpGround)
                {
                    if (SetttingPanel.activeInHierarchy)
                    {
                        SetttingPanel.SetActive(false);
                        isSettingUIActive = false;
                    }
                    else
                    {
                        SetttingPanel.SetActive(true);
                        isSettingUIActive = true;
                    }
                }
            }

        }
    }
    public void ExitGame()
    {
        // 1. 获取并销毁所有DontDestroyOnLoad对象
        DestroyAllDontDestroyOnLoadObjects();

        // 2. 清理所有场景
        ClearAllLoadedScenes();
        // 可选：触发GC清理
        System.GC.Collect();
        Resources.UnloadUnusedAssets();
        SceneManager.LoadScene("MainUI", LoadSceneMode.Single);


    }
    public void CloseSettingUI()
    {
        SetttingPanel.SetActive(false);
        isSettingUIActive = false;
    }
    public void RestartGame()
    {
        ClearAllAndReloadScene();

    }

    public void ClearAllAndReloadScene(string sceneName = "UpGround")
    {
        // 1. 获取并销毁所有DontDestroyOnLoad对象
        DestroyAllDontDestroyOnLoadObjects();

        // 2. 清理所有场景
        ClearAllLoadedScenes();

        // 3. 重新加载目标场景
        SceneManager.LoadScene(sceneName, LoadSceneMode.Single);

        // 可选：触发GC清理
        System.GC.Collect();
        Resources.UnloadUnusedAssets();
    }

    /// <summary>
    /// 销毁所有标记为DontDestroyOnLoad的游戏对象
    /// </summary>
    private void DestroyAllDontDestroyOnLoadObjects()
    {
        // 查找DontDestroyOnLoad场景中的所有根物体
        List<GameObject> ddolObjects = new List<GameObject>();
        GameObject[] allObjects = GameObject.FindObjectsOfType<GameObject>(true);

        foreach (GameObject obj in allObjects)
        {
            // 检查对象是否在DontDestroyOnLoad场景中
            if (obj.scene.name == "DontDestroyOnLoad" || obj.scene.buildIndex == -1)
            {
                ddolObjects.Add(obj);
            }
        }

        // 销毁这些对象
        foreach (GameObject obj in ddolObjects)
        {
            if (obj != null)
            {
                DestroyImmediate(obj);
            }
        }
    }

    /// <summary>
    /// 清理所有已加载的场景
    /// </summary>
    private void ClearAllLoadedScenes()
    {
        // 获取当前所有已加载的场景
        List<Scene> loadedScenes = new List<Scene>();
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);
            if (scene.isLoaded && scene.name != "DontDestroyOnLoad")
            {
                loadedScenes.Add(scene);
            }
        }

        // 清理每个场景中的对象
        foreach (Scene scene in loadedScenes)
        {
            GameObject[] rootObjects = scene.GetRootGameObjects();
            foreach (GameObject obj in rootObjects)
            {
                if (obj != null)
                {
                    DestroyImmediate(obj);
                }
            }
        }
    }

    /// <summary>
    /// 替代方法：通过重新加载场景来清理DontDestroyOnLoad对象
    /// 这个方法更彻底，但会有一个短暂的加载时间
    /// </summary>
    public void ForceReloadWithSceneRestart(string sceneName = "UpGround")
    {
        // 方法1：创建一个临时场景，切换到这个场景清理DontDestroyOnLoad
        StartCoroutine(ReloadSceneWithCleanup(sceneName));
    }

    private System.Collections.IEnumerator ReloadSceneWithCleanup(string sceneName)
    {
        // 1. 先加载一个空的临时场景
        Scene tempScene = SceneManager.CreateScene("TempCleanupScene");
        SceneManager.SetActiveScene(tempScene);

        // 2. 销毁DontDestroyOnLoad对象
        yield return new WaitForEndOfFrame();
        DestroyAllDontDestroyOnLoadObjects();

        // 3. 清理资源
        yield return new WaitForEndOfFrame();
        Resources.UnloadUnusedAssets();

        // 4. 重新加载目标场景
        SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
    }
}
