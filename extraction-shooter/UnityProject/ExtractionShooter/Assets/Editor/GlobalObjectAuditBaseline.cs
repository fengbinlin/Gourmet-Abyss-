using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Debug = UnityEngine.Debug;

namespace Game.Core.EditorTools
{
    /// <summary>
    /// 自动跑完整条基线流程并在每个节点导出运行时快照。
    /// </summary>
    /// <remarks>
    /// <para><b>为什么要自动化</b>：手动走一遍需要满足关卡解锁条件，既慢又不可重复。
    /// 而 <c>LevelManager</c> 的四个转场 API 本身不校验解锁，直接调用即可覆盖全部路径，
    /// 于是整条流程可以完全脚本化——这也让「迁移前 / 迁移后」两次运行的快照具备可比性。</para>
    ///
    /// <para><b>流程覆盖</b>：MainUI → UpGround → 进 Layer1 → 退出 → 进 Layer2 →
    /// 切 Layer3 → 从关卡回家 → 重启游戏。四条转场路径（EnterLevel / ExitLevel /
    /// SwitchLevel / FromLevelToHome）与全局重置路径各覆盖一次。</para>
    ///
    /// <para><b>输出</b>：<c>AuditSnapshots/&lt;标签&gt;/NN-步骤名.txt</c>，外加一份
    /// <c>_run.log</c> 记录每步耗时与运行期间的所有 Error/Exception 日志。
    /// 两次运行用不同标签，直接对目录做文本 diff 即可。</para>
    ///
    /// <para><b>实现方式</b>：用 <c>EditorApplication.update</c> 驱动状态机，步骤序号存在
    /// <c>SessionState</c> 里，因此能跨越「进入 Play 模式」时的程序集重载继续执行。</para>
    /// </remarks>
    [InitializeOnLoad]
    public static class GlobalObjectAuditBaseline
    {
        private const string MenuRoot = "Tools/全局对象审计/";

        private const string KeyActive    = "AuditBaseline.Active";
        private const string KeyStep      = "AuditBaseline.Step";
        private const string KeyLabel     = "AuditBaseline.Label";
        private const string KeyStepStart = "AuditBaseline.StepStart";
        private const string KeyLog       = "AuditBaseline.Log";

        /// <summary>单步超时（秒）。转场本身约 1.5–3 秒，20 秒足够容错。</summary>
        private const double StepTimeout = 20d;

        /// <summary>条件满足后的额外静置时间（秒），让延迟的 Start / 协程收敛，快照更稳定。</summary>
        private const double SettleSeconds = 0.75d;

        private static readonly List<string> CapturedLogs = new List<string>();
        private static bool _logHooked;
        private static double _settleUntil;
        private static bool _settling;

        static GlobalObjectAuditBaseline()
        {
            if (SessionState.GetBool(KeyActive, false))
                EditorApplication.update += Drive;
        }

        #region 菜单入口

        /// <summary>
        /// 输出目录由检测到的代码状态决定，不由使用者选择。
        /// </summary>
        /// <remarks>
        /// 早先做成 before/after 两个菜单项，结果是使用者以为选菜单就能切换代码版本——
        /// 而菜单只决定目录名，版本切换要靠 git。连续两轮都因此把「迁移后」的数据存成了 before。
        /// 现在目录名直接从程序集反射出来，标错在结构上不可能发生。
        /// </remarks>
        [MenuItem(MenuRoot + "运行基线流程", priority = 40)]
        private static void Run()
        {
            string folder = DetectCodeState();

            bool confirmed = EditorUtility.DisplayDialog(
                "运行基线流程",
                $"当前加载的代码：{CodeFingerprint()}\n\n" +
                $"快照将写入：AuditSnapshots/{folder}/\n\n" +
                "要采集另一份用于对比，请先用 git 切换代码版本，\n" +
                "切换后务必等 Unity 编译完成再回来运行。",
                "开始", "取消");

            if (confirmed) StartRun(folder);
        }

        [MenuItem(MenuRoot + "中止基线流程", priority = 42)]
        private static void AbortRun()
        {
            Finish("用户手动中止");
            if (EditorApplication.isPlaying) EditorApplication.isPlaying = false;
        }

        private static void StartRun(string label)
        {
            if (SessionState.GetBool(KeyActive, false))
            {
                EditorUtility.DisplayDialog("已在运行", "基线流程正在进行中。如需重来，请先执行「中止基线流程」。", "知道了");
                return;
            }

            if (EditorApplication.isPlaying) EditorApplication.isPlaying = false;

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            string dir = RunDirectory(label);
            RotatePreviousRun(dir);
            Directory.CreateDirectory(dir);

            EditorSceneManager.OpenScene("Assets/Scenes/MainUI.unity", OpenSceneMode.Single);

            SessionState.SetString(KeyLabel, label);
            SessionState.SetInt(KeyStep, 0);
            SessionState.SetString(KeyLog, "");
            SessionState.SetBool(KeyActive, true);
            SessionState.SetFloat(KeyStepStart, 0f); // 0 = 尚未进入首步，让 Drive 走完整的「进入步骤」分支

            EditorApplication.update -= Drive;
            EditorApplication.update += Drive;

            Log($"=== 基线流程开始，标签: {label} ===");
            Log($"代码指纹: {CodeFingerprint()}");
            EditorApplication.EnterPlaymode();
        }

        #endregion

        #region 状态机

        /// <summary>
        /// 每步的定义：一个「动作」和一个「完成条件」。
        /// 动作只在进入该步时执行一次；条件满足并静置后写快照，然后推进到下一步。
        /// </summary>
        private sealed class Step
        {
            public string Name;                 // 非空则在本步结束时写快照
            public Action Action;               // 进入本步时执行一次
            public Func<bool> Done;             // 完成条件
            public string Description;
        }

        private static Step[] BuildSteps()
        {
            return new[]
            {
                new Step {
                    Description = "等待 Play 模式与 MainUI 就绪",
                    Name = "01-MainUI",
                    Action = null,
                    Done = () => EditorApplication.isPlaying && ActiveSceneIs("MainUI")
                },
                new Step {
                    Description = "MainUI → UpGround（SmoothCameraMovement.EnterGame）",
                    Name = "02-UpGround",
                    Action = EnterGameFromMainUI,
                    Done = () => ActiveSceneIs("UpGround") && LevelManagerReady()
                },
                new Step {
                    Description = "进入 Layer1（EnterLevel）",
                    Name = "03-EnterLevel-Layer1",
                    Action = () => Level().EnterLevel("Layer1"),
                    Done = () => TransitionSettled() && SceneLoaded("Layer1")
                },
                new Step {
                    Description = "退出关卡回地面（ExitLevel）",
                    Name = "04-ExitLevel-UpGround",
                    Action = () => Level().ExitLevel(),
                    Done = () => TransitionSettled() && !SceneLoaded("Layer1")
                },
                new Step {
                    Description = "进入 Layer2（EnterLevel，绕过解锁）",
                    Name = "05-EnterLevel-Layer2",
                    Action = () => Level().EnterLevel("Layer2"),
                    Done = () => TransitionSettled() && SceneLoaded("Layer2")
                },
                new Step {
                    Description = "Layer2 → Layer3（SwitchLevel）",
                    Name = "06-SwitchLevel-Layer3",
                    Action = () => Level().SwitchLevel("Layer2", "Layer3"),
                    Done = () => TransitionSettled() && SceneLoaded("Layer3") && !SceneLoaded("Layer2")
                },
                new Step {
                    Description = "从关卡回家（FromLevelToHome，死亡/矿车路径）",
                    Name = "07-FromLevelToHome",
                    Action = () => Level().FromLevelToHome("Layer3"),
                    Done = () => TransitionSettled() && !SceneLoaded("Layer3")
                },
                new Step {
                    Description = "重启游戏（PlayerStateManager.RestartGame）",
                    Name = "08-AfterRestart",
                    Action = RestartGame,
                    Done = () => ActiveSceneIs("UpGround") && LevelManagerReady()
                }
            };
        }

        private static void Drive()
        {
            if (!SessionState.GetBool(KeyActive, false))
            {
                EditorApplication.update -= Drive;
                return;
            }

            HookLogs();

            Step[] steps = BuildSteps();
            int index = SessionState.GetInt(KeyStep, 0);

            if (index >= steps.Length)
            {
                Finish("全部步骤完成");
                return;
            }

            Step step = steps[index];

            // 进入 Play 模式会触发程序集重载，第 0 步之前不执行任何动作。
            if (index > 0 && !EditorApplication.isPlaying)
            {
                Fail($"第 {index + 1} 步「{step.Description}」执行时已退出 Play 模式");
                return;
            }

            // 动作只执行一次：用 StepStart 是否为 0 判定「刚进入本步」。
            if (SessionState.GetFloat(KeyStepStart, 0f) == 0f)
            {
                StampStepStart();
                _settling = false;
                Log($"[步骤 {index + 1}/{steps.Length}] {step.Description}");
                if (step.Action != null)
                {
                    // 动作抛异常同样不中止整轮：记录后继续，让完成条件去判定（多半会超时并推进）。
                    try { step.Action(); }
                    catch (Exception e) { Log($"    !! 动作抛异常: {e.GetType().Name}: {e.Message}"); }
                }
                return; // 让动作生效一帧后再判定条件
            }

            double elapsed = EditorApplication.timeSinceStartup - SessionState.GetFloat(KeyStepStart, 0f);

            bool done;
            try { done = step.Done(); }
            catch (Exception e)
            {
                Log($"    !! 条件判定抛异常，按超时处理: {e.GetType().Name}: {e.Message}");
                done = false;
                elapsed = StepTimeout + 1;
            }

            if (!done)
            {
                if (elapsed <= StepTimeout) return;

                // 超时不中止：照样出快照并推进。
                // 某一步卡住（例如转场协程抛异常导致 isTransitioning 永远为 true）时，
                // 中止会让后面所有步骤都拿不到数据；而「继续」能在一次运行里榨出最多信息，
                // 且迁移前/后两次运行会卡在同一处，快照仍然可比。
                Log($"    !! 超时（{StepTimeout} 秒）：条件未满足，仍导出快照并继续下一步");
                if (!string.IsNullOrEmpty(step.Name))
                {
                    try { WriteSnapshot(step.Name, step.Description + "  [超时：完成条件未满足]"); }
                    catch (Exception e) { Log($"    !! 写快照失败: {e.Message}"); }
                }

                _settling = false;
                SessionState.SetInt(KeyStep, index + 1);
                SessionState.SetFloat(KeyStepStart, 0f);
                return;
            }

            // 条件已满足，静置一小段时间再取快照
            if (!_settling)
            {
                _settling = true;
                _settleUntil = EditorApplication.timeSinceStartup + SettleSeconds;
                return;
            }
            if (EditorApplication.timeSinceStartup < _settleUntil) return;

            if (!string.IsNullOrEmpty(step.Name))
            {
                try
                {
                    WriteSnapshot(step.Name, step.Description);
                    Log($"    → 快照 {step.Name}.txt 已写入（耗时 {elapsed:F1}s）");
                }
                catch (Exception e)
                {
                    Fail($"写快照 {step.Name} 失败: {e}");
                    return;
                }
            }

            _settling = false;
            SessionState.SetInt(KeyStep, index + 1);
            SessionState.SetFloat(KeyStepStart, 0f);
        }

        #endregion

        #region 步骤动作

        /// <remarks>
        /// 一律使用小写 <c>LevelManager.instance</c>：迁移前它是字段，迁移后是指向
        /// <c>Instance</c> 的只读别名，两个版本都存在。本脚本必须在迁移前后都能编译，
        /// 否则就没法跑出可对比的两份快照。同理，下面只用 <c>PlayerStateManager.instance</c>。
        /// </remarks>
        private static LevelManager Level()
        {
            LevelManager lm = LevelManager.instance;
            if (lm == null) throw new InvalidOperationException("LevelManager.instance 为空");
            return lm;
        }

        private static void EnterGameFromMainUI()
        {
            SmoothCameraMovement entry = UnityEngine.Object.FindObjectOfType<SmoothCameraMovement>(true);
            if (entry == null)
                throw new InvalidOperationException("MainUI 场景中找不到 SmoothCameraMovement，无法进入游戏");
            entry.EnterGame();
        }

        private static void RestartGame()
        {
            PlayerStateManager psm = PlayerStateManager.instance;
            if (psm == null) throw new InvalidOperationException("PlayerStateManager.instance 为空");
            psm.RestartGame();
        }

        #endregion

        #region 条件判定

        private static bool ActiveSceneIs(string name)
        {
            return Application.isPlaying && SceneManager.GetActiveScene().name == name;
        }

        private static bool SceneLoaded(string name)
        {
            if (!Application.isPlaying) return false;
            Scene s = SceneManager.GetSceneByName(name);
            return s.IsValid() && s.isLoaded;
        }

        private static bool LevelManagerReady()
        {
            return LevelManager.instance != null;
        }

        /// <summary>转场已结束：LevelManager 存在且不在过渡中。</summary>
        private static bool TransitionSettled()
        {
            LevelManager lm = LevelManager.instance;
            return lm != null && !lm.IsTransitioning();
        }

        #endregion

        #region 输出

        private static string RunDirectory(string label)
        {
            return Path.Combine(GlobalObjectAudit.SnapshotDirectory(), label);
        }

        /// <summary>
        /// 把上一次同标签的快照挪到 &lt;标签&gt;.prev，再开始新的一轮。
        /// </summary>
        /// <remarks>
        /// 目录名由代码状态决定，所以连续改两轮迁移后的代码会落进同一个目录。
        /// 不留一份上轮结果就没法做「本轮改动前 vs 改动后」的对比。
        /// </remarks>
        private static void RotatePreviousRun(string dir)
        {
            if (!Directory.Exists(dir)) return;

            string prev = dir + ".prev";
            try
            {
                if (Directory.Exists(prev)) Directory.Delete(prev, true);
                Directory.Move(dir, prev);
                Debug.Log($"[基线] 上一轮快照已保留到: {prev}");
            }
            catch (Exception e)
            {
                // 轮转失败不该挡住本次运行，退回原来的「清空重写」。
                Debug.LogWarning($"[基线] 轮转上一轮快照失败，将直接覆盖: {e.Message}");
                foreach (string old in Directory.GetFiles(dir, "*.txt")) File.Delete(old);
                string log = Path.Combine(dir, "_run.log");
                if (File.Exists(log)) File.Delete(log);
            }
        }

        private static void WriteSnapshot(string name, string description)
        {
            string dir = RunDirectory(SessionState.GetString(KeyLabel, "run"));
            Directory.CreateDirectory(dir);

            var sb = new StringBuilder();
            sb.AppendLine($"# 步骤: {name}");
            sb.AppendLine($"# 说明: {description}");
            sb.AppendLine();
            sb.Append(GlobalObjectAudit.BuildRuntimeReport());

            File.WriteAllText(Path.Combine(dir, name + ".txt"), sb.ToString(), Encoding.UTF8);
        }

        /// <summary>输出目录名：pre-migration / post-migration。</summary>
        private static string DetectCodeState()
        {
            return CountMigratedTypes() == 0 ? "pre-migration" : "post-migration";
        }

        /// <summary>写进 _run.log 的代码状态描述。</summary>
        private static string CodeFingerprint()
        {
            int migrated = CountMigratedTypes();
            return migrated == 0
                ? "迁移前（无类型继承 MonoSingleton）"
                : $"迁移后（继承 MonoSingleton 的类型数 = {migrated}）";
        }

        /// <remarks>
        /// Core/ 是未跟踪目录，git stash 不会移除它，基类始终存在；
        /// 真正的判据是有多少业务类型继承了它。
        /// </remarks>
        private static int CountMigratedTypes()
        {
            Assembly game = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => a.GetName().Name == "Assembly-CSharp");
            if (game == null) return 0;

            Type singletonBase = game.GetType("Game.Core.MonoSingleton`1");
            if (singletonBase == null) return 0;

            Type[] types;
            try { types = game.GetTypes(); }
            catch (ReflectionTypeLoadException e) { types = e.Types.Where(t => t != null).ToArray(); }

            int migrated = 0;
            foreach (Type t in types)
            {
                for (Type b = t.BaseType; b != null; b = b.BaseType)
                {
                    if (b.IsGenericType && b.GetGenericTypeDefinition() == singletonBase) { migrated++; break; }
                }
            }

            return migrated;
        }

        private static void StampStepStart()
        {
            SessionState.SetFloat(KeyStepStart, (float)EditorApplication.timeSinceStartup);
        }

        private static void Log(string line)
        {
            string all = SessionState.GetString(KeyLog, "") + line + "\n";
            SessionState.SetString(KeyLog, all);
            Debug.Log("[基线] " + line);
        }

        private static void HookLogs()
        {
            if (_logHooked) return;
            _logHooked = true;
            Application.logMessageReceived += OnLogMessage;
        }

        private static void UnhookLogs()
        {
            if (!_logHooked) return;
            _logHooked = false;
            Application.logMessageReceived -= OnLogMessage;
        }

        private static void OnLogMessage(string condition, string stackTrace, LogType type)
        {
            if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
            if (condition != null && condition.StartsWith("[基线]")) return;

            // 只记首行，避免堆栈把 diff 噪声撑爆（堆栈含内存地址，两次运行必然不同）
            string first = condition ?? "";
            int nl = first.IndexOf('\n');
            if (nl >= 0) first = first.Substring(0, nl);
            CapturedLogs.Add($"{type}: {first}");
        }

        private static void Fail(string reason)
        {
            Log("!! 失败: " + reason);
            Finish("失败中止");
        }

        private static void Finish(string reason)
        {
            string label = SessionState.GetString(KeyLabel, "run");
            string dir = RunDirectory(label);

            try
            {
                Directory.CreateDirectory(dir);
                var sb = new StringBuilder();
                sb.AppendLine(SessionState.GetString(KeyLog, ""));
                sb.AppendLine($"=== 结束: {reason} ===");
                sb.AppendLine();
                sb.AppendLine($"--- 运行期间的 Error/Exception（共 {CapturedLogs.Count} 条）---");
                if (CapturedLogs.Count == 0) sb.AppendLine("（无）");
                else foreach (string l in CapturedLogs) sb.AppendLine("  " + l);

                File.WriteAllText(Path.Combine(dir, "_run.log"), sb.ToString(), Encoding.UTF8);
                Debug.Log($"[基线] 结束（{reason}）。输出目录: {dir}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[基线] 写 _run.log 失败: {e}");
            }

            UnhookLogs();
            CapturedLogs.Clear();
            _settling = false;

            SessionState.SetBool(KeyActive, false);
            SessionState.SetInt(KeyStep, 0);
            SessionState.SetFloat(KeyStepStart, 0f);
            EditorApplication.update -= Drive;

            if (EditorApplication.isPlaying) EditorApplication.isPlaying = false;
        }

        #endregion
    }
}
