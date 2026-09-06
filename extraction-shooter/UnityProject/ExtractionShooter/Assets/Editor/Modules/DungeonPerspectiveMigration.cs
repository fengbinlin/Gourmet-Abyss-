using System;
using System.IO;
using System.Linq;
using GourmetAbyss.CameraSystem;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace Game.Modules.Editor
{
    public static class DungeonPerspectiveMigration
    {
        public const string ProfilePath = "Assets/Modules/Combat/DungeonPerspective.asset";
        public static readonly string[] Scenes = { "Assets/Scenes/Layer1.unity", "Assets/Scenes/Layer2.unity", "Assets/Scenes/Layer3.unity" };

        [MenuItem("Tools/Modules/Migrate Dungeon Perspective")]
        public static void Upgrade()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play before migration.");
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty) throw new InvalidOperationException("Save scene changes first.");
            Directory.CreateDirectory("Library/DungeonPerspectiveBackup");
            if (!AssetDatabase.IsValidFolder("Assets/Modules/Combat")) AssetDatabase.CreateFolder("Assets/Modules", "Combat");
            var profile = AssetDatabase.LoadAssetAtPath<DungeonPerspectiveProfile>(ProfilePath);
            if (profile == null) { profile = ScriptableObject.CreateInstance<DungeonPerspectiveProfile>(); AssetDatabase.CreateAsset(profile, ProfilePath); }
            var prefabs = AssetDatabase.GetDependencies(Scenes, true).Where(p => p.EndsWith(".prefab") && p.StartsWith("Assets/Prefabas/"))
                .Where(p => AssetDatabase.LoadAssetAtPath<GameObject>(p).GetComponentsInChildren<SpriteRenderer>(true).Length > 0).ToArray();
            foreach (var path in prefabs)
            {
                Backup(path);
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    bool changed = false;
                    foreach (var sprite in root.GetComponentsInChildren<SpriteRenderer>(true))
                    {
                        var t = sprite.transform;
                        // Only migrate authored standing sprites. Ground/physics/effects are never guessed into billboards.
                        if (t == root.transform || t.GetComponentInParent<CameraFacingVisual>() != null ||
                            t.GetComponent<Collider>() != null || t.GetComponent<Rigidbody>() != null ||
                            Mathf.Abs(Vector3.Dot(t.forward, Vector3.up)) > .95f) continue;
                        var wrapper = new GameObject(t.name + "_VisualRoot").transform;
                        wrapper.SetParent(t.parent, false); wrapper.rotation = t.rotation;
                        wrapper.SetSiblingIndex(t.GetSiblingIndex());
                        t.SetParent(wrapper, true);
                        var face = wrapper.gameObject.AddComponent<CameraFacingVisual>();
                        var settings = new SerializedObject(face); settings.FindProperty("updateMode").enumValueIndex = 1; settings.ApplyModifiedPropertiesWithoutUndo();
                        changed = true;
                    }
                    if (changed) PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            foreach (var path in Scenes)
            {
                Backup(path); var scene = EditorSceneManager.OpenScene(path);
                var follow = Object.FindObjectOfType<CameraFollow>(true);
                if (follow == null) throw new InvalidOperationException("Missing camera bootstrap: " + path);
                var settings = new SerializedObject(follow);
                settings.FindProperty("dungeonProfile").objectReferenceValue = profile;
                settings.FindProperty("defaultSource").enumValueIndex = 2;
                var target = settings.FindProperty("target").objectReferenceValue as Transform;
                settings.ApplyModifiedPropertiesWithoutUndo();
                var camera = follow.GetComponent<Camera>();
                camera.orthographic = false; camera.fieldOfView = profile.fieldOfView; camera.nearClipPlane = .1f;
                camera.transform.rotation = Quaternion.Euler(profile.pitch, camera.transform.eulerAngles.y, 0);
                if (target != null) camera.transform.position = target.position - camera.transform.forward * profile.distance;
                var stack = camera.GetComponent<UniversalAdditionalCameraData>();
                var sync = camera.GetComponent<CameraStackPresentation>() ?? camera.gameObject.AddComponent<CameraStackPresentation>();
                sync.source = camera;
                sync.overlays = stack != null ? stack.cameraStack.Where(c => c != null).ToArray() : Array.Empty<Camera>();
                sync.orthographicOnly = camera.GetComponentsInChildren<Behaviour>(true).Where(c => c.GetType().Name == "CameraSnapSRP").ToArray();
                foreach (var component in sync.orthographicOnly) component.enabled = false;
                foreach (var overlay in sync.overlays)
                {
                    overlay.orthographic = false; overlay.fieldOfView = profile.fieldOfView; overlay.nearClipPlane = .1f;
                    overlay.transform.SetPositionAndRotation(camera.transform.position, camera.transform.rotation);
                }
                EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            }
            AssetDatabase.SaveAssets(); EditorSceneManager.OpenScene(Scenes[0]);
            Debug.Log("[Combat] Perspective migration: 3 scenes, standing prefab sources=" + prefabs.Length);
        }
        static void Backup(string path)
        {
            var backup = "Library/DungeonPerspectiveBackup/" + path.Replace('/', '_');
            if (!File.Exists(backup)) File.Copy(path, backup);
        }
    }
}
