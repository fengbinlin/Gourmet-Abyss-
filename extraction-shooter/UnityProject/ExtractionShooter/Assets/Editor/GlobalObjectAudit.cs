using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Core.EditorTools
{
    /// <summary>
    /// 导出运行时全局状态快照：已加载场景、DontDestroyOnLoad 根物体、每个单例的指向。
    /// </summary>
    /// <remarks>
    /// 输出为稳定排序的纯文本，供改动前后做文本 diff。
    /// 整条流程的自动化采集见 <see cref="GlobalObjectAuditBaseline"/>。
    /// </remarks>
    public static class GlobalObjectAudit
    {
        private const string OutputDirectory = "AuditSnapshots";
        private const string MenuRoot = "Tools/全局对象审计/";

        [MenuItem(MenuRoot + "导出运行时快照 %#a", priority = 0)]
        private static void ExportRuntimeSnapshot()
        {
            if (!Application.isPlaying)
            {
                EditorUtility.DisplayDialog("需要 Play 模式", "运行时快照需要在 Play 模式下导出。", "知道了");
                return;
            }

            WriteSnapshot("runtime", BuildRuntimeReport());
        }

        #region 报告

        internal static string BuildRuntimeReport()
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== 运行时快照 ===");
            sb.AppendLine($"帧号: {Time.frameCount}");
            sb.AppendLine();

            AppendLoadedScenes(sb);
            AppendPersistentObjects(sb);
            AppendSingletonStates(sb);

            return sb.ToString();
        }

        private static void AppendLoadedScenes(StringBuilder sb)
        {
            sb.AppendLine("--- 已加载场景 ---");
            sb.AppendLine($"活动场景: {SceneManager.GetActiveScene().name}");

            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                sb.AppendLine($"    [{i}] {scene.name}  loaded={scene.isLoaded}  rootCount={scene.rootCount}");
            }
            sb.AppendLine();
        }

        /// <remarks>
        /// 靠新建临时物体拿到 DontDestroyOnLoad 场景的句柄——Unity 没有公开的直接获取方式。
        /// </remarks>
        private static void AppendPersistentObjects(StringBuilder sb)
        {
            sb.AppendLine("--- DontDestroyOnLoad 根物体 ---");

            var probe = new GameObject("~AuditProbe");
            UnityEngine.Object.DontDestroyOnLoad(probe);
            Scene ddol = probe.scene;

            List<string> names = ddol.GetRootGameObjects()
                .Where(go => go != probe)
                .Select(go => $"{go.name}  [{DescribeComponents(go)}]")
                .OrderBy(s => s, StringComparer.Ordinal)
                .ToList();

            UnityEngine.Object.DestroyImmediate(probe);

            if (names.Count == 0) sb.AppendLine("    （空）");
            else foreach (string name in names) sb.AppendLine($"    {name}");

            sb.AppendLine($"    合计: {names.Count} 个根物体");
            sb.AppendLine();
        }

        private static string DescribeComponents(GameObject go)
        {
            IEnumerable<string> names = go.GetComponents<MonoBehaviour>()
                .Where(c => c != null && IsProjectType(c.GetType()))
                .Select(c => c.GetType().Name)
                .OrderBy(n => n, StringComparer.Ordinal);

            string joined = string.Join(", ", names);
            return string.IsNullOrEmpty(joined) ? "-" : joined;
        }

        private static void AppendSingletonStates(StringBuilder sb)
        {
            sb.AppendLine("--- 单例状态 ---");

            var rows = new List<string>();

            foreach (Type type in CollectProjectMonoBehaviourTypes())
            {
                // 列出全部匹配成员而非只取第一个：反射的成员顺序不保证稳定，
                // 而迁移后的类同时拥有基类的 Instance 与兼容别名 instance。
                foreach (MemberInfo member in FindStaticSelfReferences(type))
                    rows.Add($"    {type.Name}.{member.Name} = {DescribeSingletonTarget(ReadStaticValue(member))}");
            }

            rows.Sort(StringComparer.Ordinal);

            if (rows.Count == 0) sb.AppendLine("    （未发现单例）");
            else foreach (string row in rows) sb.AppendLine(row);

            sb.AppendLine($"    合计: {rows.Count} 个单例");
        }

        private static string DescribeSingletonTarget(object value)
        {
            if (value == null) return "<null>";

            if (value is Component component)
            {
                // Unity 重载的 == 会把已销毁对象判为 null，这正是要区分出来的状态。
                if (component == null) return "<已销毁但静态引用未清空>";

                GameObject go = component.gameObject;
                string sceneName = string.IsNullOrEmpty(go.scene.name) ? "<无场景>" : go.scene.name;
                return $"{sceneName} / {GetHierarchyPath(go)}";
            }

            return value.ToString();
        }

        private static string GetHierarchyPath(GameObject go)
        {
            var parts = new List<string>();
            for (Transform t = go.transform; t != null; t = t.parent) parts.Add(t.name);
            parts.Reverse();
            return string.Join("/", parts);
        }

        /// <summary>找出类型上所有「指向自己」的 public static 成员，按名排序以保证输出稳定。</summary>
        private static List<MemberInfo> FindStaticSelfReferences(Type type)
        {
            const BindingFlags flags = BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy;
            var found = new List<MemberInfo>();

            foreach (PropertyInfo property in type.GetProperties(flags))
            {
                if (property.GetIndexParameters().Length == 0 &&
                    property.CanRead &&
                    property.PropertyType.IsAssignableFrom(type))
                    found.Add(property);
            }

            foreach (FieldInfo field in type.GetFields(flags))
            {
                if (field.FieldType.IsAssignableFrom(type)) found.Add(field);
            }

            found.Sort((a, b) => string.CompareOrdinal(a.Name, b.Name));
            return found;
        }

        private static object ReadStaticValue(MemberInfo member)
        {
            try
            {
                return member is PropertyInfo property
                    ? property.GetValue(null)
                    : ((FieldInfo)member).GetValue(null);
            }
            catch (Exception e)
            {
                return $"<读取失败: {e.GetType().Name}>";
            }
        }

        private static IEnumerable<Type> CollectProjectMonoBehaviourTypes()
        {
            Assembly gameAssembly = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => a.GetName().Name == "Assembly-CSharp") ?? typeof(GlobalObjectAudit).Assembly;

            Type[] types;
            try
            {
                types = gameAssembly.GetTypes();
            }
            catch (ReflectionTypeLoadException e)
            {
                types = e.Types.Where(t => t != null).ToArray();
            }

            return types.Where(t => t != null && !t.IsAbstract && typeof(MonoBehaviour).IsAssignableFrom(t));
        }

        private static bool IsProjectType(Type type)
        {
            return type.Assembly.GetName().Name == "Assembly-CSharp";
        }

        #endregion

        #region 输出

        /// <summary>快照根目录（Assets 同级的 AuditSnapshots，避免被 Unity 当作资源导入）。</summary>
        internal static string SnapshotDirectory()
        {
            DirectoryInfo projectRoot = Directory.GetParent(Application.dataPath);
            string basePath = projectRoot != null ? projectRoot.FullName : Application.dataPath;
            return Path.Combine(basePath, OutputDirectory);
        }

        private static void WriteSnapshot(string label, string content)
        {
            string dir = SnapshotDirectory();
            Directory.CreateDirectory(dir);

            string path = Path.Combine(dir, $"{label}-{DateTime.Now:yyyyMMdd-HHmmss}.txt");
            File.WriteAllText(path, content, Encoding.UTF8);

            Debug.Log($"[审计] 快照已写入: {path}");
        }

        #endregion
    }
}
