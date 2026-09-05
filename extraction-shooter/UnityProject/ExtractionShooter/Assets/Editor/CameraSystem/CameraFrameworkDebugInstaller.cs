using GourmetAbyss.CameraSystem;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class CameraFrameworkDebugInstaller
{
    private const string PendingKey = "GourmetAbyss.CameraDebug.PendingInstall";

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
        SessionState.EraseBool(PendingKey);
    }

    private static void TryInstallPending()
    {
        if (!SessionState.GetBool(PendingKey, false) || !EditorApplication.isPlaying)
            return;

        CameraDirector director = CameraService.Active ?? Object.FindObjectOfType<CameraDirector>();
        if (director == null)
            return;

        if (director.GetComponent<CameraFrameworkDebugHarness>() == null)
            director.gameObject.AddComponent<CameraFrameworkDebugHarness>();

        SessionState.EraseBool(PendingKey);
        Debug.Log("[CameraDebug] 调试器已安装：F1~F6 切换测试，F8 隐藏面板。");
    }
}
