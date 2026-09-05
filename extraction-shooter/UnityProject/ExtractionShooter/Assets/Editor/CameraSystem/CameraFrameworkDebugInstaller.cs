using GourmetAbyss.CameraSystem;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class CameraFrameworkDebugInstaller
{
    private const string PendingKey = "GourmetAbyss.CameraDebug.PendingInstall";
    private const string GuidedPendingKey = "GourmetAbyss.CameraDebug.GuidedPendingInstall";
    private const string TownScenePath = "Assets/Scenes/UpGround.unity";

    static CameraFrameworkDebugInstaller()
    {
        EditorApplication.update += TryInstallPending;
    }

    [MenuItem("Tools/料理地牢/镜头系统/一键启动运行时调试 &F7")]
    private static void StartRuntimeDebug()
    {
        SessionState.SetBool(PendingKey, true);
        if (!EditorApplication.isPlaying)
            EditorApplication.EnterPlaymode();
        else
            TryInstallPending();
    }

    [MenuItem("Tools/料理地牢/镜头系统/移除运行时调试器")]
    private static void RemoveRuntimeDebug()
    {
        CameraFrameworkDebugHarness[] harnesses = Object.FindObjectsOfType<CameraFrameworkDebugHarness>(true);
        for (int i = 0; i < harnesses.Length; i++)
        {
            if (EditorApplication.isPlaying)
                Object.Destroy(harnesses[i]);
            else
                Object.DestroyImmediate(harnesses[i]);
        }
        CameraGuidedAcceptanceController[] guided =
            Resources.FindObjectsOfTypeAll<CameraGuidedAcceptanceController>();
        for (int i = 0; i < guided.Length; i++)
        {
            if (EditorApplication.isPlaying)
                Object.Destroy(guided[i].gameObject);
            else
                Object.DestroyImmediate(guided[i].gameObject);
        }
        SessionState.EraseBool(PendingKey);
        SessionState.EraseBool(GuidedPendingKey);
    }

    [MenuItem("Tools/料理地牢/镜头系统/一键开始逐步体验验收", priority = 1)]
    private static void StartGuidedAcceptance()
    {
        if (!Application.isPlaying)
            RemoveGuidedControllers(true);

        SessionState.SetBool(GuidedPendingKey, true);
        if (EditorApplication.isPlaying)
        {
            if (!SceneManager.GetSceneByName("UpGround").isLoaded)
                SceneManager.LoadScene("UpGround", LoadSceneMode.Single);
            TryInstallPending();
            return;
        }

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            SessionState.EraseBool(GuidedPendingKey);
            return;
        }

        if (EditorSceneManager.GetActiveScene().path != TownScenePath)
            EditorSceneManager.OpenScene(TownScenePath, OpenSceneMode.Single);
        EditorApplication.EnterPlaymode();
    }

    // ASCII alias is used by the external PowerShell launcher because Windows PowerShell 5
    // can decode UTF-8 scripts without a BOM using the current ANSI code page.
    [MenuItem("Tools/Gourmet Abyss/Camera/Start Guided Acceptance", priority = 1)]
    private static void StartGuidedAcceptanceAsciiAlias()
    {
        StartGuidedAcceptance();
    }

    private static void TryInstallPending()
    {
        // EditorApplication.isPlaying turns true slightly before the runtime world exists.
        // Waiting for Application.isPlaying avoids creating an edit-time ghost object.
        if (!EditorApplication.isPlaying || !Application.isPlaying)
            return;

        if (SessionState.GetBool(GuidedPendingKey, false))
        {
            Scene town = SceneManager.GetSceneByName("UpGround");
            if (!town.IsValid() || !town.isLoaded)
                return;

            CameraGuidedAcceptanceController existing = FindLiveGuidedController();
            if (existing == null)
            {
                GameObject guideObject = new GameObject("~CameraGuidedAcceptance");
                guideObject.hideFlags = HideFlags.DontSave;
                guideObject.AddComponent<CameraGuidedAcceptanceController>();
            }

            SessionState.EraseBool(GuidedPendingKey);
            Debug.Log("[CameraAcceptance] 逐步体验已启动：F9 下一步，F8 隐藏面板，F10 重置。");
        }

        if (!SessionState.GetBool(PendingKey, false))
            return;

        CameraDirector director = CameraService.Active ?? Object.FindObjectOfType<CameraDirector>();
        if (director == null)
            return;

        if (director.GetComponent<CameraFrameworkDebugHarness>() == null)
            director.gameObject.AddComponent<CameraFrameworkDebugHarness>();

        SessionState.EraseBool(PendingKey);
        Debug.Log("[CameraDebug] 调试器已安装：F1~F6 切换测试，F8 隐藏面板。");
    }

    private static CameraGuidedAcceptanceController FindLiveGuidedController()
    {
        CameraGuidedAcceptanceController[] guides =
            Resources.FindObjectsOfTypeAll<CameraGuidedAcceptanceController>();
        for (int i = 0; i < guides.Length; i++)
        {
            CameraGuidedAcceptanceController guide = guides[i];
            if (guide != null && guide.gameObject.scene.IsValid())
                return guide;
        }

        return null;
    }

    private static void RemoveGuidedControllers(bool immediate)
    {
        CameraGuidedAcceptanceController[] guides =
            Resources.FindObjectsOfTypeAll<CameraGuidedAcceptanceController>();
        for (int i = 0; i < guides.Length; i++)
        {
            if (guides[i] == null)
                continue;
            if (immediate)
                Object.DestroyImmediate(guides[i].gameObject);
            else
                Object.Destroy(guides[i].gameObject);
        }
    }
}
