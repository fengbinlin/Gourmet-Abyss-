# 镜头系统配置

## 场景相机

| 场景 | 投影 | X 轴俯角 | Orthographic Size |
|---|---|---:|---:|
| `UpGround` | Orthographic | 0° | 沿用场景值 |
| `Layer1` / `Layer2` / `Layer3` | Orthographic | 45° | 10 |

`CameraFollow.defaultSource` 保持 `Auto`：相机有俯角时使用地牢模式，无俯角时使用小镇模式。`target` 必须指向玩家，`autoOffset` 保持开启。

## 默认参数

当前场景未绑定 Profile，运行时使用 `CameraFollow` 的代码默认值。

| 模式 | 参数 | 值 |
|---|---|---:|
| 通用 | Position Smooth Time | 0.30 s |
| 小镇 | Look Ahead Distance | 1.50 m |
| 小镇 | Look Ahead Smooth Time | 0.20 s |
| 地牢 | Pointer Center Dead Zone | 0.10 |
| 地牢 | Pointer Max Offset | 3.00 m |
| 地牢 | Pointer Response Exponent | 1.40 |
| 地牢 | Pointer Smooth Time | 0.14 s |

玩家静止和移动时均使用同一个 `3 m` 鼠标偏移上限；指针位于 UI 上时不计算鼠标偏移。

需要按场景调整时创建并绑定 `TownCameraProfile`、`DungeonCameraProfile` 或 `RestaurantCameraProfile`。`orthographicSize <= 0` 表示沿用场景相机值。

## 人物、怪物、建筑视觉规则

- 3D 模型不挂 `CameraFacingVisual`，由相机俯角产生空间纵深。
- 2D 对象必须使用独立 `VisualRoot`；渲染器和 Animator 放在该节点，碰撞体、刚体、AI、交互脚本保留在物理根节点。
- 需要始终朝向镜头的 `VisualRoot` 挂 `CameraFacingVisual`。固定镜头使用 `OnceOnEnable`；运行中会改变旋转才使用 `EveryLateUpdate`。
- 2D 素材必须按三分之四俯视角绘制。朝向镜头只能调整贴片平面，不能把正面素材转换成俯视素材。
- 不在带 `Collider` 或 `Rigidbody` 的节点上挂 `CameraFacingVisual`。

当前 `EnemtMush2D` 使用正面 2D 素材并手动设置 X=45°；正式怪物和建筑预制体尚未统一接入 `CameraFacingVisual`。新增或替换素材时必须按以上规则处理。
