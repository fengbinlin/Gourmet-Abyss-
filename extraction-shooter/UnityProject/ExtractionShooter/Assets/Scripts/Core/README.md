# Core —— 全局对象与场景生命周期

## 当前生效范围

| 文件 | 状态 |
|---|---|
| `MonoSingleton.cs` | **已生效**，26 个管理器已迁移 |
| `GameRoot.cs` | **未激活**，缺 `Resources/GameRoot.prefab` 时自动跳过 |
| `SceneFlow/TransitionRequest.cs` | **已生效**，`LevelManager` 四条转场路径已收敛为一个协程 |
| `SceneFlow/SceneContext.cs` | **仅类型定义**，广播尚未接线 |

---

## 一、写一个新的全局单例

### 1. 选基类

```csharp
using Game.Core;

// 场景内单例：随场景卸载而消失
public class FooManager : MonoSingleton<FooManager> { }

// 跨场景单例：自动 DontDestroyOnLoad
public class BarManager : PersistentMonoSingleton<BarManager> { }
```

不要自己写 `public static T Instance`，基类已提供 `Instance` 与 `Exists`。

### 2. 选重复实例策略

场景里出现第二个实例时怎么办。默认 `DestroyNewcomer`，需要别的行为就覆写：

```csharp
protected override DuplicatePolicy Duplicate => DuplicatePolicy.OverwriteReference;
```

| 策略 | 静态引用指向 | 后来者 | 用于 |
|---|---|---|---|
| `DestroyNewcomer`（默认） | 先来的 | **销毁** | 唯一性由代码保证的管理器 |
| `KeepIncumbent` | 先来的 | 存活（Start/Update 照跑） | 允许多份共存、但只认一份的 |
| `OverwriteReference` | 后来的 | 存活 | 每个场景各带一份、后加载的接管 |
| `DestroyIncumbent` | 后来的 | **销毁先来的** | 新实例要顶替旧实例 |

### 3. 写初始化逻辑

```csharp
public class BarManager : PersistentMonoSingleton<BarManager>
{
    // 相当于原来的 Awake，只在「赢得单例」时调用
    protected override void OnAwake()
    {
        LoadConfig();
    }
}
```

---

## 二、四条硬性约定

违反前三条编译器会报 `CS0114`/`CS0108` 警告，**不要忽略**。

### 1. 不要声明 `Awake`

基类的 `Awake` 负责竞争单例。要初始化逻辑就覆写 `OnAwake`。

### 2. 需要 `OnDestroy` 时必须 `override` 并调 `base`

```csharp
protected override void OnDestroy()
{
    base.OnDestroy();      // 基类在这里清空 Instance
    someEvent -= Handler;  // 你的清理
}
```

写成 `private void OnDestroy()` 会隐藏基类实现，Unity 只派发到你这个，`Instance` 永远不会被清空。

清理逻辑放 `OnDestroy` 还是 `OnSingletonDestroyed`，取决于它是否对**所有实例**都要执行：

| 放哪 | 何时执行 |
|---|---|
| `override OnDestroy` | 每个实例销毁时（含被判为重复的那些） |
| `override OnSingletonDestroyed` | 只有当前单例销毁时 |

### 3. `PersistentMonoSingleton` 的 `Start` 由你自己写

基类刻意不声明 `Start`，直接写 `private void Start()` 即可，不会冲突。

### 4. 落败实例也需要跑逻辑时，覆写 `OnLostSingletonRace`

`OnAwake` 只在赢得单例时调用。如果旧代码是「守卫之后没有 `return`」的写法（落败者也执行初始化），迁移时必须显式补上：

```csharp
protected override void OnAwake()            => Init();
protected override void OnLostSingletonRace() => Init();
```

本次迁移中 `MoneyChest`、`MapDataManager`、`ShopManager` 三处属于这种情况。

---

## 三、三个必须知道的语义

### 1. `?.` 与 `!= null` 在销毁后不等价

Unity 重载了 `==`，让已销毁对象等于 `null`；但 `?.` 用的是 CLR 的引用判空，**不走重载**。

```csharp
// 对象已销毁、静态引用未清空时：
if (Foo.Instance != null) Foo.Instance.Bar();  // 不执行（Unity 重载）
Foo.Instance?.Bar();                            // 会执行 → MissingReferenceException
```

基类会在 `OnDestroy` 里把 `Instance` 置为真正的 `null`，所以继承基类之后两种写法一致。

这个坑实测发生过：跑完一轮关卡回到地面后，`SceneTitle.instance` 与 `levelCaveCar.instance` 都处于「已销毁但静态引用未清空」状态，`LevelManager` 正是在这个时刻读 `SceneTitle.instance.SceneName`，标题静默显示成上一个关卡的名字。两者已迁移，读取点改为 `SceneTitle.Resolve(sceneName)` 按目标场景解析。

**尚未迁移的单例仍有这个坑**，改动它们时先确认调用点。

### 2. `OverwriteReference` 单例在持有者卸载后会变成 null

多个场景各挂一份时，后加载的接管 `Instance`；它所在的场景卸载后 `Instance` 变成 null，**其余场景那几份不会自动补位**（Awake 只跑一次）。

`SceneTitle` 就是这种：关卡接管后卸载，地面那份还在却取不到。需要「当前场景总有一份」的语义时，加一个自愈入口：

```csharp
public static SceneTitle Current
{
    get
    {
        if (Instance != null) return Instance;              // 有主时短路，无每帧开销
        if (_fallback == null) _fallback = Resolve(SceneManager.GetActiveScene().name);
        return _fallback;
    }
}
```

反例是 `levelCaveCar`：它只存在于关卡里，回到地面后 `Instance` 为 null 是**正确**的，不要给它加自愈。区别在于该类型是否本就该在当前场景存在。

### 3. `DontDestroyOnLoad` 只对根物体生效

挂在子物体上时 Unity 打个警告就静默失效。实测 `ShopManager`（`Resturant&House/Restaurant`）与 `FlyObjectPool`（`Resturant&House/ProjectionManager`）都是这种情况——调了 `DontDestroyOnLoad` 却从未跨场景存活。两者已改为 `MonoSingleton`，如实声明为场景内单例，运行时状态不变。

选基类时按**实际**需要，不要按意图：物体不是根节点就别用 `PersistentMonoSingleton`。基类默认保留「子物体 DDOL 失效」这个既有行为（`DetachBeforePersist => false`），确需生效再覆写：

```csharp
protected override bool DetachBeforePersist => true;   // 先脱离父节点再 DDOL
```

改之前先确认：脱离父节点会改变 Transform 层级，可能影响 UI 布局与相对坐标。

---

## 四、GameRoot

### 启用条件

`GameRoot` 在首个场景加载前尝试 `Resources.Load<GameObject>("GameRoot")`。**该预制体不存在时静默跳过**，所以当前对运行时零影响。

创建 `Assets/Resources/GameRoot.prefab`（根物体挂 `GameRoot`）的那一刻自动生效，不需要改代码。

### 阶段化初始化

用来替代手排 `[DefaultExecutionOrder]` 数字。管理器实现 `IGameSystem`：

```csharp
public class FooManager : PersistentMonoSingleton<FooManager>, IGameSystem
{
    public BootPhase Phase => BootPhase.Systems;

    public void InitializeSystem()
    {
        // 此时 Boot / Config / Data 阶段的系统都已初始化完毕
        var cfg = ExcelConfigReader.Instance;
    }
}
```

阶段顺序：`Boot → Config → Data → Systems → Ui → Gameplay`。挂在 `GameRoot` 子节点下的会被自动收集；运行时创建的用 `GameRoot.Instance.RegisterSystem(this)`。

**迁移期不要删除现有的 `[DefaultExecutionOrder]`**，两套并存，等依赖关系全部改为阶段声明后再删。

### 全局重置

```csharp
GameRoot.ResetAllAndLoad("UpGround");
```

替代现有的三套实现（`PlayerStateManager.DestroyAllDontDestroyOnLoadObjects` / `ClearAllLoadedScenes` / `SmoothCameraMovement.ClearDontDestroyOnLoadObjects`）。用 `Destroy` 而非 `DestroyImmediate`，销毁与 `LoadScene(Single)` 都在本帧末生效，不会互相踩。

---

## 五、SceneFlow

### 新增一条转场路径

`LevelManager` 的四个协程已收敛为一个 `RunTransition(TransitionRequest)`，差异全部由配置描述。加新路径不要再复制协程，照着 `TransitionPresets` 加预设、再加一个公开入口即可：

```csharp
public void EnterBossLevel(string sceneName)
{
    if (isTransitioning) return;
    StartCoroutine(RunTransition(TransitionPresets.EnterBossLevel(sceneName)));
}
```

字段含义见 `TransitionRequest.cs`。`TransitionPresets` 里的四个预设是对**当前行为的逐位复刻，含已知缺陷**，每处都有 `[现状]` 注释；修缺陷是独立一轮改动。

### 改配置时注意执行顺序

`HudRefreshTiming` 有三档，**不能合并**：`mainUI` 关开会让子面板重跑 `OnEnable`，它相对于 `homeSceneObject` 显隐的先后决定这些面板启动时看到的状态。EnterLevel 是「先刷 HUD 再隐藏地面」，FromLevelToHome 是「先显示地面再刷 HUD」，ExitLevel 更特殊——跨帧。

`ExtraFrameBeforeFadeIn` 只有 EnterLevel 用，作用是等新加载的场景完全就绪再取它的车辆组件。

### 场景相关逻辑不要再写进 LevelManager

实现 `ISceneLifecycleListener`：

```csharp
public class FooHud : MonoBehaviour, ISceneLifecycleListener
{
    public void OnSceneEnter(in SceneContext ctx)
    {
        if (ctx.Kind == TransitionKind.EnterLevel) Show();
    }

    public void OnSceneExit(in SceneContext ctx) => Hide();
}
```

现在 `LevelManager` 必须认识 `KeepMainCamera`、`UITapBounce`、`SceneTitle`、`mainUI` 这些 UI 细节，四份复制代码互相漏步骤（`ExitLevel` 漏了刷新主相机、`FromLevelToHome` 漏了发光过渡）。改成广播之后，新增场景相关逻辑不再需要动 `LevelManager`。

---

## 六、验证工具

改动全局对象或转场逻辑后，用审计工具确认没有行为漂移：

| 菜单 | 用途 |
|---|---|
| `Tools/全局对象审计/运行基线流程` | 自动跑完整条流程，每步导出运行时快照 |
| `Tools/全局对象审计/导出运行时快照` | Play 模式下手动导出单张（`Ctrl+Shift+A`） |
| `Tools/全局对象审计/中止基线流程` | 流程卡住时用 |

快照内容：已加载场景、DontDestroyOnLoad 根物体清单、每个单例当前指向哪个对象（含「已销毁但静态引用未清空」这种状态）。

基线流程覆盖 8 步：MainUI → UpGround → 进 Layer1 → 退出 → 进 Layer2 → 切 Layer3 → 从关卡回家 → 重启，四条转场路径与全局重置路径各一次。它直接调 `LevelManager` 的转场 API，**不受关卡解锁状态限制**。

输出目录由工具反射当前程序集自动判定（`pre-migration/` 或 `post-migration/`），**使用者无法指定**——菜单只负责跑流程，代码版本切换要靠 git，两者分开才不会把版本标错。运行前的确认框会显示检测到的代码状态。

做 A/B 对比：跑一次 → git 切到另一版本 → **等 Unity 编译完成** → 再跑一次 → diff 两个目录。重点比对每个单例指向哪个对象、DontDestroyOnLoad 根物体清单、`_run.log` 的 Error 条数。
