#if UNITY_EDITOR
using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GourmetAbyss.CameraSystem
{
    /// <summary>
    /// 编辑器专用的逐步体验验收面板。由 CameraFrameworkDebugInstaller 动态创建，
    /// 不挂入正式场景，也不改变正式镜头配置。
    /// </summary>
    [DefaultExecutionOrder(1000)]
    public sealed class CameraGuidedAcceptanceController : MonoBehaviour
    {
        private enum Stage
        {
            Welcome,
            Town,
            Restaurant,
            Portal,
            Dungeon,
            DungeonExit,
            Complete
        }

        private const string TownScene = "UpGround";
        private const string DungeonScene = "Layer1";

        private Stage _stage;
        private bool _showOverlay = true;
        private string _notice = "按 N 或 F9 开始第一项，也可以点击面板底部按钮。";

        private Transform _player;
        private Vector3 _townPlayerStart;
        private Vector3 _townCameraStart;
        private Vector3 _firstTownFacing;
        private bool _hasFirstTownFacing;
        private bool _townPlayerMoved;
        private bool _townCameraMoved;
        private bool _townOppositeFacingObserved;

        private Vector3 _restaurantDoorPosition;
        private bool _restaurantEntered;
        private bool _restaurantPlayerLocked;
        private bool _restaurantCameraActive;
        private bool _restaurantReturnedToDoor;

        private bool _mapOpened;
        private bool _dungeonLoaded;
        private bool _dungeonReturned;
        private bool _hasDungeonCenteredOffset;
        private Vector2 _dungeonCenteredOffset;
        private float _dungeonObservedPointerTravel;

        private GUIStyle _titleStyle;
        private GUIStyle _headingStyle;
        private GUIStyle _bodyStyle;
        private GUIStyle _statusStyle;

        private void Awake()
        {
            DontDestroyOnLoad(gameObject);
            SceneManager.sceneLoaded += OnSceneLoaded;
            SceneManager.sceneUnloaded += OnSceneUnloaded;
        }

        private void Start()
        {
            CaptureActivePlayer();
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneUnloaded -= OnSceneUnloaded;
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.F8))
                _showOverlay = !_showOverlay;
            if (Input.GetKeyDown(KeyCode.F9) || Input.GetKeyDown(KeyCode.N))
                Advance();
            if (Input.GetKeyDown(KeyCode.F10))
                RestartGuide();

            ObserveCurrentStage();
        }

        public void Advance()
        {
            switch (_stage)
            {
                case Stage.Welcome:
                    BeginTown();
                    break;
                case Stage.Town:
                    BeginRestaurant();
                    break;
                case Stage.Restaurant:
                    BeginPortal();
                    break;
                case Stage.Portal:
                    if (!IsSceneLoaded(DungeonScene))
                    {
                    _notice = "先在传送点按 E 打开地图，并点击已解锁的 Layer1；进入后再按 N/F9。";
                        return;
                    }
                    BeginDungeon();
                    break;
                case Stage.Dungeon:
                    BeginDungeonExit();
                    break;
                case Stage.DungeonExit:
                    if (IsSceneLoaded(DungeonScene))
                    {
                        _notice = "先在地牢出口按 E 返回小镇；返回完成后再按 N/F9。";
                        return;
                    }
                    _stage = Stage.Complete;
                    _notice = "逐步体验完成。可以按 F10 从头再验，或停止 Play Mode。";
                    break;
                case Stage.Complete:
                    RestartGuide();
                    break;
            }
        }

        private void RestartGuide()
        {
            _stage = Stage.Welcome;
            _notice = IsSceneLoaded(TownScene)
                ? "已重置。按 N/F9 开始第一项。"
                : "请先返回 UpGround，再按 N/F9 开始。";
            _townPlayerMoved = false;
            _townCameraMoved = false;
            _townOppositeFacingObserved = false;
            _hasFirstTownFacing = false;
            _restaurantEntered = false;
            _restaurantPlayerLocked = false;
            _restaurantCameraActive = false;
            _restaurantReturnedToDoor = false;
            _mapOpened = false;
            _dungeonLoaded = false;
            _dungeonReturned = false;
            _hasDungeonCenteredOffset = false;
            _dungeonObservedPointerTravel = 0f;
            CaptureActivePlayer();
        }

        private void BeginTown()
        {
            if (!IsSceneLoaded(TownScene))
            {
                _notice = "当前不在 UpGround。停止 Play Mode 后重新启动逐步验收。";
                return;
            }

            _stage = Stage.Town;
            CaptureActivePlayer();
            CameraDirector director = CameraService.Active;
            _townPlayerStart = _player != null ? _player.position : Vector3.zero;
            _townCameraStart = director != null ? director.CurrentPose.Position : Vector3.zero;
            _notice = "用 WASD 朝一个方向走并停下，再朝相反方向走；观察镜头平滑跟随和朝向前瞻。";
        }

        private void BeginRestaurant()
        {
            _stage = Stage.Restaurant;
            MonoBehaviour restaurant = FindBehaviour("RestaurantEntryPoint");
            if (!TeleportPlayerIntoTrigger(restaurant, out _restaurantDoorPosition))
            {
                _notice = "未找到正式餐厅入口或玩家，无法定位。";
                return;
            }

            _notice = "已到餐厅入口：按 E 进入；尝试 WASD，观察角色固定；按住鼠标中键拖动；按 Esc 离开。";
        }

        private void BeginPortal()
        {
            MonoBehaviour restaurant = FindBehaviour("RestaurantEntryPoint");
            if (restaurant != null && GetBoolProperty(restaurant, "IsEntered"))
            {
                InvokeMethod(restaurant, "LeaveRestaurant");
                CaptureActivePlayer();
                _restaurantReturnedToDoor = _player != null &&
                                                Vector3.Distance(_player.position, _restaurantDoorPosition) < 0.2f;
            }

            _stage = Stage.Portal;
            MonoBehaviour portal = FindBehaviour("HomeCavecar");
            if (!TeleportPlayerIntoTrigger(portal, out _))
            {
                _notice = "未找到地面传送点或玩家，无法定位。";
                return;
            }

            _notice = "已到地面传送点：按 E 打开地图，确认出现区域选择，再点击已解锁的 Layer1。进入后按 N/F9。";
        }

        private void BeginDungeon()
        {
            _stage = Stage.Dungeon;
            CaptureActivePlayer();
            _hasDungeonCenteredOffset = false;
            _dungeonObservedPointerTravel = 0f;
            _notice = "先把鼠标放屏幕中心，再缓慢移到半程和边缘；悬停 UI 验证镜头回中；最后用 WASD 验证平滑跟随。";
        }

        private void BeginDungeonExit()
        {
            _stage = Stage.DungeonExit;
            MonoBehaviour exit = FindBehaviour("levelCaveCar");
            if (!TeleportPlayerIntoTrigger(exit, out _))
            {
                _notice = "未找到 Layer1 地牢出口或玩家，无法定位。";
                return;
            }

            _notice = "已到地牢出口：按 E 返回小镇。确认镜头恢复为 Town、玩家仍在原地面传送点附近，再按 N/F9。";
        }

        private void ObserveCurrentStage()
        {
            CameraDirector director = CameraService.Active;
            if (director == null)
                return;

            if (_stage == Stage.Town)
                ObserveTown(director);
            else if (_stage == Stage.Restaurant)
                ObserveRestaurant(director);
            else if (_stage == Stage.Portal)
                ObservePortal();
            else if (_stage == Stage.Dungeon)
                ObserveDungeon(director);
            else if (_stage == Stage.DungeonExit && !IsSceneLoaded(DungeonScene))
                _dungeonReturned = director.GetDebugSummary().IndexOf("Town", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void ObserveTown(CameraDirector director)
        {
            CaptureActivePlayer();
            if (_player == null)
                return;

            _townPlayerMoved |= Vector3.Distance(_player.position, _townPlayerStart) > 0.5f;
            _townCameraMoved |= Vector3.Distance(director.CurrentPose.Position, _townCameraStart) > 0.1f;

            MonoBehaviour controller = FindBehaviourOnObject(_player.gameObject, "TopDownController");
            Vector3 facing = GetVector3Property(controller, "CameraFacingDirection");
            if (facing.sqrMagnitude < 0.01f)
                return;

            facing.Normalize();
            if (!_hasFirstTownFacing)
            {
                _firstTownFacing = facing;
                _hasFirstTownFacing = true;
            }
            else if (Vector3.Dot(_firstTownFacing, facing) < 0.25f)
            {
                _townOppositeFacingObserved = true;
            }
        }

        private void ObserveRestaurant(CameraDirector director)
        {
            MonoBehaviour restaurant = FindBehaviour("RestaurantEntryPoint");
            bool enteredNow = restaurant != null && GetBoolProperty(restaurant, "IsEntered");
            _restaurantEntered |= enteredNow;
            if (enteredNow)
            {
                CaptureActivePlayer();
                MonoBehaviour controller = _player != null
                    ? FindBehaviourOnObject(_player.gameObject, "TopDownController")
                    : null;
                Rigidbody body = _player != null ? _player.GetComponent<Rigidbody>() : null;
                _restaurantPlayerLocked |= controller != null &&
                                           !GetBoolField(controller, "canPlayerMove") &&
                                           body != null && body.isKinematic;
                _restaurantCameraActive |= director.GetDebugSummary()
                    .IndexOf("Restaurant", StringComparison.OrdinalIgnoreCase) >= 0;
            }
            else if (_restaurantEntered)
            {
                CaptureActivePlayer();
                _restaurantReturnedToDoor |= _player != null &&
                                              Vector3.Distance(_player.position, _restaurantDoorPosition) < 0.2f;
            }
        }

        private void ObservePortal()
        {
            MonoBehaviour portal = FindBehaviour("HomeCavecar");
            _mapOpened |= portal != null && GetBoolByMethod(portal, "IsMapUIActive");
            _dungeonLoaded |= IsSceneLoaded(DungeonScene);
        }

        private void ObserveDungeon(CameraDirector director)
        {
            CaptureActivePlayer();
            if (_player == null)
                return;

            Vector2 pointer = new Vector2(
                (Input.mousePosition.x / Mathf.Max(1f, Screen.width) - 0.5f) * 2f,
                (Input.mousePosition.y / Mathf.Max(1f, Screen.height) - 0.5f) * 2f);
            pointer = Vector2.ClampMagnitude(pointer, 1f);

            CameraPlane plane = CameraPlane.FromRotation(director.CurrentPose.Rotation, _player.position);
            Vector3 relative = director.CurrentPose.Position - _player.position;
            Vector2 planarOffset = new Vector2(
                Vector3.Dot(relative, plane.Right),
                Vector3.Dot(relative, plane.Up));

            if (pointer.magnitude < 0.12f)
            {
                _dungeonCenteredOffset = planarOffset;
                _hasDungeonCenteredOffset = true;
            }
            else if (_hasDungeonCenteredOffset && pointer.magnitude > 0.75f)
            {
                _dungeonObservedPointerTravel = Mathf.Max(
                    _dungeonObservedPointerTravel,
                    Vector2.Distance(planarOffset, _dungeonCenteredOffset));
            }
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name == DungeonScene)
            {
                _dungeonLoaded = true;
                _notice = "Layer1 已加载。按 N/F9 进入地牢镜头体验步骤。";
            }
            CaptureActivePlayer();
        }

        private void OnSceneUnloaded(Scene scene)
        {
            if (scene.name == DungeonScene)
            {
                _dungeonReturned = true;
                _notice = "已返回 UpGround。确认画面和控制恢复后按 N/F9 完成。";
            }
        }

        private void CaptureActivePlayer()
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
                _player = player.transform;
        }

        private bool TeleportPlayerIntoTrigger(MonoBehaviour target, out Vector3 destination)
        {
            destination = Vector3.zero;
            CaptureActivePlayer();
            if (target == null || _player == null)
                return false;

            Collider trigger = target.GetComponent<Collider>();
            destination = trigger != null ? trigger.bounds.center : target.transform.position;
            Rigidbody body = _player.GetComponent<Rigidbody>();
            if (body != null)
            {
                body.velocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
                body.position = destination;
            }
            else
            {
                _player.position = destination;
            }
            Physics.SyncTransforms();
            return true;
        }

        private void OnGUI()
        {
            if (!_showOverlay)
                return;

            EnsureStyles();
            float width = Mathf.Min(680f, Mathf.Max(440f, Screen.width - 32f));
            GUILayout.BeginArea(new Rect(16f, 16f, width, Mathf.Min(590f, Screen.height - 32f)), GUI.skin.box);
            GUILayout.Label($"料理地牢镜头逐步验收  {GetStageProgress()}", _titleStyle);
            GUILayout.Label("N / F9 下一步定位    F8 隐藏面板    F10 从头开始", _headingStyle);
            GUILayout.Space(6f);
            GUILayout.Label(GetStageTitle(), _headingStyle);
            GUILayout.Label(GetStageInstructions(), _bodyStyle);
            GUILayout.Space(6f);
            GUILayout.Label("通过标准", _headingStyle);
            GUILayout.Label(GetPassCriteria(), _bodyStyle);
            GUILayout.Space(6f);
            GUILayout.Label("自动观测", _headingStyle);
            GUILayout.Label(GetObservedStatus(), _statusStyle);
            GUILayout.Space(6f);
            GUILayout.Label("提示：" + _notice, _bodyStyle);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button(_stage == Stage.Complete ? "重新开始（F10）" : "下一步（点击这里或按 N/F9）", GUILayout.Height(34f)))
                Advance();
            GUILayout.EndArea();
        }

        private string GetStageTitle()
        {
            switch (_stage)
            {
                case Stage.Welcome: return "准备：验收范围";
                case Stage.Town: return "1. 小镇跟随与朝向前瞻";
                case Stage.Restaurant: return "2. 餐厅固定座位与镜头拖拽";
                case Stage.Portal: return "3. 地面传送点与地图选关";
                case Stage.Dungeon: return "4. 地牢鼠标偏移与平滑跟随";
                case Stage.DungeonExit: return "5. 地牢出口与小镇恢复";
                default: return "完成";
            }
        }

        private string GetStageProgress()
        {
            switch (_stage)
            {
                case Stage.Welcome: return "准备";
                case Stage.Town: return "1/5";
                case Stage.Restaurant: return "2/5";
                case Stage.Portal: return "3/5";
                case Stage.Dungeon: return "4/5";
                case Stage.DungeonExit: return "5/5";
                default: return "完成";
            }
        }

        private string GetStageInstructions()
        {
            switch (_stage)
            {
                case Stage.Welcome:
                    return "此模式使用正式 UpGround、Layer1、餐厅和转场逻辑。先确认玩家从小镇指定建筑旁出生；按 N/F9 或点击底部按钮后，每一步会自动把玩家送到对应入口。";
                case Stage.Town:
                    return "用 WASD 朝一个方向移动 2 秒并停下，再朝相反方向移动。注意镜头开始、追赶、停稳以及画面前方留白。";
                case Stage.Restaurant:
                    return "按 E 进入餐厅；尝试 WASD；观察角色座位、中心构图；按住鼠标中键拖动；按 Esc 离开。";
                case Stage.Portal:
                    return "按 E 打开地图，确认区域选择 UI 正常，再点击已解锁的 Layer1。进入地牢后按 N/F9。";
                case Stage.Dungeon:
                    return "先静止，鼠标依次放中心、半程、边缘；再把鼠标移到 UI 上。随后把鼠标保持在边缘并使用 WASD 移动，观察偏移不会继续累计；同时检查非地面画面仍正对镜头。";
                case Stage.DungeonExit:
                    return "按 E 走正式地牢返回流程。返回后确认镜头、玩家和控制都恢复，再按 N/F9。";
                default:
                    return "所有引导步骤已走完。下面的自动观测只辅助判断，最终以你的画面手感为准。";
            }
        }

        private string GetPassCriteria()
        {
            switch (_stage)
            {
                case Stage.Welcome:
                    return "正式小镇场景可运行；玩家出生在指定建筑旁；面板可见；验收工具不会修改场景资源。";
                case Stage.Town:
                    return "镜头不瞬移、不抖动；玩家改变方向后，镜头平滑把更多空间留在朝向一侧；停下后稳定。";
                case Stage.Restaurant:
                    return "E 后玩家固定且不能移动；镜头位于餐厅中心；不足一屏不能乱拖，超出一屏时可拖且不越界；退出回入口。";
                case Stage.Portal:
                    return "E 能打开地图；可选择已解锁区域；进入 Layer1 时画面平滑切换且没有残留双相机。";
                case Stage.Dungeon:
                    return "中心附近无偏移；越靠近边缘偏移越大但最多 3 米；静止和移动时使用同一上限且不累计漂移；指针在 UI 上时回中；非地面视觉不应躺倒或改变物理根节点。";
                case Stage.DungeonExit:
                    return "E 返回 UpGround；恢复 Town 镜头；地面玩家仍在进入前传送点附近且可以移动。";
                default:
                    return "以上五段均符合画面和操作标准。";
            }
        }

        private string GetObservedStatus()
        {
            CameraDirector director = CameraService.Active;
            string camera = director != null ? director.GetDebugSummary() : "CameraDirector 未找到";
            switch (_stage)
            {
                case Stage.Welcome:
                    return $"{Mark(director != null)} 正式 CameraDirector\n{Mark(director != null && director.Camera.orthographic)} 正交相机\n{camera}";
                case Stage.Town:
                    return $"{Mark(_townPlayerMoved)} 玩家发生移动\n{Mark(_townCameraMoved)} 镜头平滑跟随发生位移\n{Mark(_townOppositeFacingObserved)} 已观察两个不同朝向\n{camera}";
                case Stage.Restaurant:
                    return $"{Mark(_restaurantEntered)} 已进入餐厅\n{Mark(_restaurantPlayerLocked)} 输入和刚体均锁定\n{Mark(_restaurantCameraActive)} Restaurant 镜头取得控制\n{Mark(_restaurantReturnedToDoor)} 离开后回入口\n{camera}";
                case Stage.Portal:
                    return $"{Mark(_mapOpened)} 地图 UI 已打开\n{Mark(_dungeonLoaded)} Layer1 已 Additive 加载\n{camera}";
                case Stage.Dungeon:
                    bool tilted = director != null && Mathf.Abs((director.CurrentPose.Rotation * Vector3.forward).y) > 0.1f;
                    return $"{Mark(director != null && director.Camera.orthographic)} 正交相机\n{Mark(tilted)} 地牢俯角\n{Mark(_hasDungeonCenteredOffset)} 已采样鼠标中心\n{Mark(_dungeonObservedPointerTravel > 0.5f)} 已观察边缘偏移（{_dungeonObservedPointerTravel:F2}m）\n{camera}";
                case Stage.DungeonExit:
                    return $"{Mark(_dungeonReturned)} Layer1 已卸载并返回小镇\n{camera}";
                default:
                    return $"{Mark(_townPlayerMoved && _townCameraMoved)} 小镇跟随\n{Mark(_restaurantEntered && _restaurantPlayerLocked && _restaurantCameraActive)} 餐厅\n{Mark(_mapOpened && _dungeonLoaded)} 地图与进地牢\n{Mark(_hasDungeonCenteredOffset && _dungeonObservedPointerTravel > 0.5f)} 地牢鼠标偏移\n{Mark(_dungeonReturned)} 返回小镇";
            }
        }

        private void EnsureStyles()
        {
            if (_titleStyle != null)
                return;

            _titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 21,
                fontStyle = FontStyle.Bold,
                wordWrap = true
            };
            _headingStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 16,
                fontStyle = FontStyle.Bold,
                wordWrap = true
            };
            _bodyStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 15,
                wordWrap = true
            };
            _statusStyle = new GUIStyle(_bodyStyle)
            {
                richText = true
            };
        }

        private static string Mark(bool observed)
        {
            return observed ? "<color=#65D27A>[已观察]</color>" : "<color=#F2C94C>[待观察]</color>";
        }

        private static bool IsSceneLoaded(string sceneName)
        {
            Scene scene = SceneManager.GetSceneByName(sceneName);
            return scene.IsValid() && scene.isLoaded;
        }

        private static MonoBehaviour FindBehaviour(string typeName)
        {
            MonoBehaviour[] behaviours = FindObjectsOfType<MonoBehaviour>(true);
            for (int i = 0; i < behaviours.Length; i++)
            {
                MonoBehaviour behaviour = behaviours[i];
                if (behaviour != null && behaviour.GetType().Name == typeName)
                    return behaviour;
            }
            return null;
        }

        private static MonoBehaviour FindBehaviourOnObject(GameObject gameObject, string typeName)
        {
            if (gameObject == null)
                return null;
            MonoBehaviour[] behaviours = gameObject.GetComponentsInParent<MonoBehaviour>(true);
            for (int i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] != null && behaviours[i].GetType().Name == typeName)
                    return behaviours[i];
            }
            return null;
        }

        private static bool GetBoolField(object target, string fieldName)
        {
            if (target == null)
                return false;
            FieldInfo field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            return field != null && field.GetValue(target) is bool value && value;
        }

        private static bool GetBoolProperty(object target, string propertyName)
        {
            if (target == null)
                return false;
            PropertyInfo property = target.GetType().GetProperty(
                propertyName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            return property != null && property.GetValue(target) is bool value && value;
        }

        private static Vector3 GetVector3Property(object target, string propertyName)
        {
            if (target == null)
                return Vector3.zero;
            PropertyInfo property = target.GetType().GetProperty(
                propertyName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            return property != null && property.GetValue(target) is Vector3 value ? value : Vector3.zero;
        }

        private static bool GetBoolByMethod(object target, string methodName)
        {
            object value = InvokeMethod(target, methodName);
            return value is bool result && result;
        }

        private static object InvokeMethod(object target, string methodName)
        {
            if (target == null)
                return null;
            MethodInfo method = target.GetType().GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (method == null)
                return null;
            try
            {
                return method.Invoke(target, null);
            }
            catch (TargetInvocationException exception)
            {
                Debug.LogException(exception.InnerException ?? exception);
                return null;
            }
        }
    }
}
#endif
