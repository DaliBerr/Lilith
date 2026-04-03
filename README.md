# Lilith

## 一句话概览

当前仓库是一个 Unity 6 原型项目。已经明确落地的部分，主要是：

- 一个基于 `MapGridAuthoring` 的网格地图编辑工作流
- 一套基础设施层 `Vocalith`，包含日志、本地化、UI 栈、事件总线、自定义 JSON 存档 `Scribe`
- 一套轻量游戏状态系统 `Kernel.GameState`
- 一个业务 UI 示例 `MainMenuScreen`（脚本已写，资源与启动接线尚未完全闭环）

这份 README 的目标是说明当前仓库里已经存在什么、在哪里，以及 agent 应该从哪里开始读，同时还有未来的规划。

## 工程事实

- Unity 版本：`6000.3.9f1`
- 主场景：[`Assets/Scenes/Start.unity`](Assets/Scenes/Start.unity)
- 运行时代码主要分布在：
  - [`Assets/Scripts/Kernel`](Assets/Scripts/Kernel)
  - [`Assets/Scripts/Vocalith`](Assets/Scripts/Vocalith)
  - [`Assets/Prefabs/UI`](Assets/Prefabs/UI)（当前业务 UI 脚本放在这里）
- Editor 工具与测试位于 [`Assets/Editor`](Assets/Editor)
- 当前仓库里没有 `asmdef/asmref`
  - 运行时代码默认编进 `Assembly-CSharp`
  - Editor 代码默认编进 `Assembly-CSharp-Editor`

## 未来规划
- 这个项目未来希望发展成一款基于文字构筑的俯视角肉鸽射击游戏。核心思路是把“文字”本身作为战斗系统的一部分，而不是单纯的视觉表现：玩家在战斗中击败敌人后，可以收集掉落的文字词元或预组好的词包，并按照固定语法将它们组合成自己的攻击方式。例如，不同的核心词、行为词和数值词可以共同决定子弹的属性、飞行方式和攻击范围，从而形成明显不同的 build。整体目标是让玩家在每一局中不断通过拾取、替换和重组文字来“写出”自己的战斗风格，并在局外通过永久解锁来逐步扩展可用词元、构筑深度和开局选择，使游戏兼具即时战斗的爽感、组合发现的乐趣，以及肉鸽式的成长循环。

## 建议阅读顺序

如果 agent 需要快速建立上下文，建议按这个顺序读：

1. [`Assets/Scenes/Start.unity`](Assets/Scenes/Start.unity)
   先看当前场景里有哪些根对象：`MapRoot`、`UIRoot`、`Startup`、`Main Camera`、各 UI Layer。
2. [`Assets/Scripts/StartUp.cs`](Assets/Scripts/StartUp.cs)
   看代码层设计的启动流程：语言初始化、状态初始化、UI 启动、Addressables 初始化。
3. [`Assets/Scripts/Vocalith/UI/UIManager.cs`](Assets/Scripts/Vocalith/UI/UIManager.cs)
   看 UI 是怎么被实例化、压栈、出栈和销毁的。
4. [`Assets/Prefabs/UI/MainMenuUI.cs`](Assets/Prefabs/UI/MainMenuUI.cs)
   看当前唯一明确写出来的业务 Screen。
5. [`Assets/Scripts/Kernel/Status.cs`](Assets/Scripts/Kernel/Status.cs) 和 [`Assets/Scripts/Kernel/StatusController.cs`](Assets/Scripts/Kernel/StatusController.cs)
   看状态机语义和切换规则。
6. [`Assets/Scripts/Kernel/MapGridAuthoring.cs`](Assets/Scripts/Kernel/MapGridAuthoring.cs) 和 [`Assets/Editor/MapGridEditorUtility.cs`](Assets/Editor/MapGridEditorUtility.cs)
   看地图网格的运行时数据结构与 Editor 生成逻辑。
7. [`Assets/Scripts/Vocalith/Localization/LocalizationManager.cs`](Assets/Scripts/Vocalith/Localization/LocalizationManager.cs)
   看语言包、JSON 补丁和 Addressables 读取。
8. [`Assets/Scripts/Vocalith/Scribe/Scribe.cs`](Assets/Scripts/Vocalith/Scribe/Scribe.cs)
   看当前自定义存档系统的底层协议与读写入口。

## 代码分层

| 层 | 作用 | 关键路径 |
| --- | --- | --- |
| `Kernel` | 当前项目的业务语义层 | [`Assets/Scripts/Kernel`](Assets/Scripts/Kernel) |
| `Vocalith` | 通用基础设施层：日志、UI、存档、本地化、事件、工具类 | [`Assets/Scripts/Vocalith`](Assets/Scripts/Vocalith) |
| 业务 UI 脚本 | 具体 Screen/界面逻辑 | [`Assets/Prefabs/UI`](Assets/Prefabs/UI) |
| Editor | 网格生成、替换、测试 | [`Assets/Editor`](Assets/Editor) |

当前层次关系可以理解为：

- `Kernel` 负责“游戏里发生什么”
- `Vocalith` 负责“这些系统用什么基础设施运行”
- `Assets/Prefabs/UI` 负责“某个具体界面怎么响应按钮和状态”

## 启动链路

### 代码层设计

[`Assets/Scripts/StartUp.cs`](Assets/Scripts/StartUp.cs) 中的 `Kernel.Startup` 表达了当前意图上的启动顺序：

1. `InitLanguage()`
   - 调用 [`LocalizationManager.InitializeAsync`](Assets/Scripts/Vocalith/Localization/LocalizationManager.cs)
2. `StatusController.Initialize()`
   - 清空状态栈
3. 若 `isEnableDevMode` 为真，加入 `DevMode`
4. `UIManager.Instance.PrePushScreenCo<MainMenuScreen>()`
   - 预压入主菜单
5. 加入 `Loading` 状态
6. `InitGlobal()`
   - `Addressables.InitializeAsync()`
   - 预留 Def / 数据库加载入口

同时，日志系统会在首场景加载前自动初始化：

- [`Assets/Scripts/Vocalith/Log/LogBootStrap.cs`](Assets/Scripts/Vocalith/Log/LogBootStrap.cs)
- [`Assets/Scripts/Vocalith/Log/LogForwarder.cs`](Assets/Scripts/Vocalith/Log/LogForwarder.cs)

### 当前场景实际接线

`Start.unity` 里当前能确认到的事实：

- `MapRoot` 上挂了 [`MapGridAuthoring`](Assets/Scripts/Kernel/MapGridAuthoring.cs)
  - 当前配置是 `96 x 54`
  - `cellSize = (8, 8)`
  - `chunk = 3 x 3`
  - 坐标绑定使用 `CellData.SetCoordinates / GetCoordinates`
- `UIRoot` 上有 [`UIManager`](Assets/Scripts/Vocalith/UI/UIManager.cs)
  - 但 `UIRoot` 当前在场景文件里是未激活状态
  - 序列化类型名仍显示旧命名 `Lonize.UI.UIManager`
- 场景里存在名为 `Startup` 的根对象
  - 但当前已提交的 `Start.unity` 中，没有看到 `Kernel.Startup` 组件的序列化记录

这意味着：

- 代码层已经有“启动器设计”
- 但当前仓库快照的场景接线并不保证这条启动链真的会自动跑通

如果后续 agent 在排查“为什么没有进主菜单 / 为什么 UIManager 是空 / 为什么 Boot 没执行”，先看场景挂载，不要只看 `StartUp.cs`。

## 已实现系统

### 1. 状态系统

关键文件：

- [`Assets/Scripts/Kernel/Status.cs`](Assets/Scripts/Kernel/Status.cs)
- [`Assets/Scripts/Kernel/StatusController.cs`](Assets/Scripts/Kernel/StatusController.cs)
- [`Assets/Scripts/Kernel/StatusSaveData.cs`](Assets/Scripts/Kernel/StatusSaveData.cs)

当前能力：

- `Status` 定义了：
  - `StatusName`
  - 互斥状态 `InActiveWith`
  - 允许切换状态 `allowSwitchWith`
  - 是否持久化 `Persistent`
- `StatusList` 预定义了当前项目使用的状态
  - `DevMode`
  - `NormalMode`
  - `Paused`
  - `Playing`
  - `InPauseMenu`
  - `InMainMenu`
  - `InMenu`
  - `Loading`
  - `SaveLoading`
  - `PopUp`
  - 以及建筑放置/拆除状态
- `StatusController` 维护当前状态列表，并处理：
  - 状态添加
  - 互斥检查
  - 自动切换移除
  - 状态查询
  - 持久化状态导出
  - 从状态名集合恢复
- `SaveStatus` 把持久化状态接入 `Scribe`

这是当前最明确的“运行时业务状态入口”。

### 2. UI 基础设施

关键文件：

- [`Assets/Scripts/Vocalith/UI/UIManager.cs`](Assets/Scripts/Vocalith/UI/UIManager.cs)
- [`Assets/Scripts/Vocalith/UI/UIScreen.cs`](Assets/Scripts/Vocalith/UI/UIScreen.cs)
- [`Assets/Scripts/Vocalith/UI/UIWidget.cs`](Assets/Scripts/Vocalith/UI/UIWidget.cs)
- [`Assets/Scripts/Vocalith/UI/UIPrefabAttribute.cs`](Assets/Scripts/Vocalith/UI/UIPrefabAttribute.cs)
- [`Assets/Prefabs/UI/GameUIScreen.cs`](Assets/Prefabs/UI/GameUIScreen.cs)
- [`Assets/Prefabs/UI/MainMenuUI.cs`](Assets/Prefabs/UI/MainMenuUI.cs)

当前能力：

- `UIManager` 管理四层 UI：
  - `Screen`
  - `Modal`
  - `Overlay`
  - `Toast`
- 内部维护 `screenStack` 和 `modalStack`
- 使用导航锁 `_isNavigating`，避免 Push/Pop 并发打乱栈
- 通过 `Addressables.InstantiateAsync` 创建 UI 实例
- 通过 `[UIPrefab("...")]` 决定 prefab 地址
- `UIScreen` 统一处理：
  - `CanvasGroup`
  - 淡入淡出
  - 显隐后的交互开关
- `GameUIScreen` 在显示或初始化时，自动确保自己声明的 `Status` 已经进入状态栈

当前业务 UI：

- `MainMenuScreen`
  - 声明自己的状态是 `InMainMenu`
  - 已绑定四个按钮：
    - `startBtn`
    - `loadBtn`
    - `optionsBtn`
    - `quitBtn`
  - 但实际开始游戏 / 读档 / 设置 / 退出逻辑大多还是注释或 TODO

### 3. 地图网格 Authoring

关键文件：

- [`Assets/Scripts/Kernel/MapGridAuthoring.cs`](Assets/Scripts/Kernel/MapGridAuthoring.cs)
- [`Assets/Scripts/Kernel/MapGridCoordinateBinding.cs`](Assets/Scripts/Kernel/MapGridCoordinateBinding.cs)
- [`Assets/Scripts/Kernel/Cell/CellData.cs`](Assets/Scripts/Kernel/Cell/CellData.cs)
- [`Assets/Editor/MapGridEditorUtility.cs`](Assets/Editor/MapGridEditorUtility.cs)
- [`Assets/Editor/MapGridAuthoringEditor.cs`](Assets/Editor/MapGridAuthoringEditor.cs)
- [`Assets/Editor/MapGridAuthoringTests.cs`](Assets/Editor/MapGridAuthoringTests.cs)

当前能力：

- `MapGridAuthoring` 保存网格配置与 cell 索引：
  - 宽高
  - `cellSize`
  - chunk 切分尺寸
  - 默认 cell prefab
  - 手动 `Frame Camera` 使用的相机 framing 参数
  - `cellEntries`
  - 运行时 `cellLookup`
  - `TryRefreshGroundWallState()`
    - 可在运行时或 Editor Action 中扫描已索引 cell；空白或无文字的格子会被标记为 `Ground` 并禁用 Collider，非空白格子会被标记为 `Wall` 并启用 Collider
  - `TryInitializeCellSurfaceCache() / TryMarkCellSurfaceDirty() / TryRefreshDirtyGroundWallState()`
    - 支持先做一次全量引用缓存，再在运行时只刷新被标脏的格子，避免每次都重扫整张地图
- `MapGridCoordinateBinding` 通过反射把“任意组件上的坐标字段/属性/方法”接到网格系统
  - 当前场景配置使用 `CellData`
  - 既支持 `SetCoordinates(int, int)`，也支持 `SetCoordinates(Vector2Int)`
  - 既支持 `GetCoordinates()`，也支持直接读成员
- `CellData` 当前还负责：
  - 缓存并开关 cell 子物体上的主 3D `Collider`
  - 暴露 cell 根节点或指定子节点的移动控制接口
  - 在绑定了目标 `Transform/Rigidbody` 后，支持位置、平移、速度与停止控制
- `MapGridCameraUtility` 能按网格尺寸手动框住目标相机
- Editor 侧已经支持：
  - `Generate Grid`
  - `Clear Grid`
  - `Rebuild Grid`
  - `Rebuild Index`
  - `Frame Camera`
  - `Disable Empty Text Colliders`
    - 遍历所有已索引 cell；若没有 `TMP_Text` 或文字为空白，则禁用该 cell 的 Collider
  - `Sync Ground/Wall From Text`
    - 遍历所有已索引 cell；若没有 `TMP_Text` 或文字为空白，则将 cell 根节点与受控 Collider 物体标记为 `Ground`，并禁用 Collider
    - 若文字非空白，则将 cell 根节点与受控 Collider 物体标记为 `Wall`，并启用 Collider
  - `Replace Selected Cell`
  - `Scene Cell Edit`
    - 在 Scene 视图里支持两种选择模式：
      - `Paint`：左键拖刷路径上的格子
      - `Rectangle`：点击一个起点后拖到另一个点，松开鼠标时对包围矩形内所有格子应用操作
    - `FillText`：把刷过的格子文字改成当前输入字符串，并启用 Collider
    - `EraseText`：把刷过的格子文字清空为 `string.Empty`，并禁用 Collider
    - `SetColliderState`：按当前 `Enable Collider` 选项，批量启用或禁用 Collider
- 已有编辑器测试覆盖：
  - 局部坐标换算
  - chunk 计算
  - 网格中心计算
  - 索引查询
  - 重复坐标/越界坐标报错
  - Scene Cell Edit 的路径插值、矩形框选坐标、唯一 `TMP_Text` 校验与 Collider 联动

### 4. 基础战斗交互

关键文件：

- [`Assets/Scripts/Kernel/Player/PlayerPlaneMovement.cs`](Assets/Scripts/Kernel/Player/PlayerPlaneMovement.cs)
- [`Assets/Scripts/Kernel/Bullet/AttackSpec.cs`](Assets/Scripts/Kernel/Bullet/AttackSpec.cs)
- [`Assets/Scripts/Kernel/Bullet/AttackFormulaCompiler.cs`](Assets/Scripts/Kernel/Bullet/AttackFormulaCompiler.cs)
- [`Assets/Scripts/Kernel/Bullet/AttackFormulaLoadout.cs`](Assets/Scripts/Kernel/Bullet/AttackFormulaLoadout.cs)
- [`Assets/Scripts/Kernel/Bullet/CharBullet.cs`](Assets/Scripts/Kernel/Bullet/CharBullet.cs)
- [`Assets/Scripts/Kernel/Enemy/Enemy.cs`](Assets/Scripts/Kernel/Enemy/Enemy.cs)
- [`Assets/Scripts/Kernel/Enemy/BaseCharEnemyNorm1.cs`](Assets/Scripts/Kernel/Enemy/BaseCharEnemyNorm1.cs)

当前能力：

- `AttackSpec` 仍然是单发子弹的底层运行时配置，负责速度、生命周期、命中层级和基础伤害等参数
- `AttackFormulaLoadout` 持有当前装备的有序词元列表，并缓存最新 `CompiledAttack`
- `AttackFormulaCompiler` 负责把 `Pre? + Core + Behavior? + Value? + Result? + Value? + Post?` 编译成 `CompiledAttack`
  - 缺少 `Core` 时为硬失败
  - 其他非法顺序默认给出 warning 并尽力继续编译
  - `Behavior` 缺失时默认 `Straight`
  - `Result` 缺失时默认 `DirectDamage`
  - token 还可以直接声明最终子弹文本覆盖；这部分不走 DSL，按被接受 token 的顺序最后一个生效
  - 已被编译器接受的 token 还会按顺序回放自己的修饰 DSL
  - 当前 DSL 支持 `= / += / -= / *= / /=`
  - 当前可修饰目标包括 `TextColor / FontSize / ScaleMultiplier / ProjectileSpeed / MaxLifetime / MaxTravelDistance / ImpactRadiusMultiplier`
  - `FontSize` 当前不再直接写 TMP 字号，而是驱动文字节点 `RectTransform` 的宽高，默认保持宽高一致，并在运行时基于当前文字容器尺寸执行 `+= / -= / *= / /=`
  - 当 `FontSize` 生效时，球形碰撞体半径会同步锁定为文字容器边长的一半
- `PlayerPlaneMovement` 发射时只会读取 `AttackFormulaLoadout` 的编译结果；缺少 loadout、loadout 为空或编译失败时都不会发射
- `AttackProjectileEmitter` 会把 `CompiledAttack` 落地成实际子弹批次
  - 当前支持 `Straight`
  - 当前支持 `Spread`
- `CharBullet` 会从 `AttackSpec` 读取伤害、弹速、命中消耗和飞行回收参数，并从 `CompiledAttack` 读取最终表现修饰；命中带有 `Enemy_Object` 标签的对象时，会尝试从碰撞体父级解析 `Enemy` 组件并调用统一受伤接口
  - 当前支持 `DirectDamage`
  - 当前支持 `Explosion`，并按“直击 + 爆炸 AoE”两段结算主目标
- `Enemy` 统一暴露 `MaxHealth`、`CurrentHealth`、`IsDead` 和 `TryApplyDamage`
- `BaseCharEnemyNorm1` 当前维护运行时生命值，并在生命归零后销毁自身

这是当前仓库里完成度最高、闭环最好的一组逻辑。

### 4. 本地化系统

关键文件：

- [`Assets/Scripts/Vocalith/Localization/LocalizationManager.cs`](Assets/Scripts/Vocalith/Localization/LocalizationManager.cs)
- [`Assets/Scripts/Vocalith/Localization/LocalizationJsonUtility.cs`](Assets/Scripts/Vocalith/Localization/LocalizationJsonUtility.cs)
- [`Assets/Scripts/Vocalith/Localization/StringLocalizationExtensions.cs`](Assets/Scripts/Vocalith/Localization/StringLocalizationExtensions.cs)

当前能力：

- 当前语言标签保存在 `PlayerPrefs["LanguageTag"]`
- 启动时可按：
  - 手动传入语言
  - 已保存语言
  - 系统语言推断
  依次决定最终语言
- 从 Addressables 读取两类 `TextAsset`
  - 字符串表：默认标签 `Localization`
  - JSON 补丁：默认标签 `LocalizationJson`
- 支持两种 JSON 本地化方式：
  - 外置 patch：`domain + id -> JObject patch`
  - 内嵌 `languageData`
- 提供：
  - `Translate`
  - `TranslateFormat`
  - `string.Translate()`

### 5. 日志系统

关键文件：

- [`Assets/Scripts/Vocalith/Log/Log.cs`](Assets/Scripts/Vocalith/Log/Log.cs)
- [`Assets/Scripts/Vocalith/Log/LogBootStrap.cs`](Assets/Scripts/Vocalith/Log/LogBootStrap.cs)
- [`Assets/Scripts/Vocalith/Log/LogForwarder.cs`](Assets/Scripts/Vocalith/Log/LogForwarder.cs)
- [`Assets/Scripts/Vocalith/Log/LogSink.cs`](Assets/Scripts/Vocalith/Log/LogSink.cs)
- [`Assets/Scripts/Vocalith/Log/GameDebug.cs`](Assets/Scripts/Vocalith/Log/GameDebug.cs)

当前能力：

- 结构化日志事件 `LogEvent`
- 多 Sink 输出
  - Console
  - 文件
  - Editor 下可选 UnitySink
- 支持作用域上下文 `BeginScope`
- 支持最近日志环形缓冲 `SnapshotRecent`
- 支持去重输出 `InfoDedup`
- `UnityLogForwarder` 会把原生 `Debug.Log*` 转发进自定义 `Log`
- 文件日志默认写到 `Application.persistentDataPath/Logs/game.log`

### 6. 自定义存档系统 `Scribe`

关键文件：

- [`Assets/Scripts/Vocalith/Scribe/Scribe.cs`](Assets/Scripts/Vocalith/Scribe/Scribe.cs)
- [`Assets/Scripts/Vocalith/Scribe/ScribeValue.cs`](Assets/Scripts/Vocalith/Scribe/ScribeValue.cs)
- [`Assets/Scripts/Vocalith/Scribe/ScribeCollections.cs`](Assets/Scripts/Vocalith/Scribe/ScribeCollections.cs)
- [`Assets/Scripts/Vocalith/Scribe/ScribeDeep.cs`](Assets/Scripts/Vocalith/Scribe/ScribeDeep.cs)
- [`Assets/Scripts/Vocalith/Scribe/ScribeCrossRefs.cs`](Assets/Scripts/Vocalith/Scribe/ScribeCrossRefs.cs)
- [`Assets/Scripts/Vocalith/Scribe/ScrobePolymorph.cs`](Assets/Scripts/Vocalith/Scribe/ScrobePolymorph.cs)
- [`Assets/Scripts/Vocalith/Scribe/ScribeBootstrap.cs`](Assets/Scripts/Vocalith/Scribe/ScribeBootstrap.cs)

当前能力：

- JSON-only 存档格式
- `SaveDocument -> Root NodeFrame -> SerializedField` 的树形结构
- 支持：
  - 基础值
  - 枚举
  - `List<int>` / `List<string>`
  - DTO JSON 列表
  - `IExposable` 深拷贝对象
  - 交叉引用 `ILoadReferenceable`
  - 多态 `ISaveItem`
- `CodecRegistry` / `ScribeBootstrap` 负责基础 codec 注册
- `PolymorphRegistry` 限制可反序列化类型，避免任意反射构造

当前仓库里已经接入 `Scribe` 的具体业务对象，能明确看到的是：

- [`SaveStatus`](Assets/Scripts/Kernel/StatusSaveData.cs)

### 7. 事件系统

关键文件：

- [`Assets/Scripts/Vocalith/Event/EventBus.cs`](Assets/Scripts/Vocalith/Event/EventBus.cs)
- [`Assets/Scripts/Vocalith/Event/Events.cs`](Assets/Scripts/Vocalith/Event/Events.cs)

当前能力：

- 类型安全的发布订阅 `EventBus`
- `Subscribe / Unsubscribe / Publish`
- 订阅返回 `IDisposable`
- 异常 handler 会被捕获并写日志
- `EventList` 中已经声明了一批事件类型
  - 地图初始化
  - 主场景初始化
  - Item/Building 加载
  - 设置变化
  - Save/Load 请求
  - 关闭弹窗
  - 选中工厂/格子等

### 8. 工具类

关键文件：

- [`Assets/Scripts/Vocalith/Random.cs`](Assets/Scripts/Vocalith/Random.cs)
- [`Assets/Scripts/Vocalith/Math.cs`](Assets/Scripts/Vocalith/Math.cs)

当前能力：

- `Vocalith.Random`
  - 基于 PCG32 的稳定伪随机数
  - 支持确定性种子
- `Vocalith.Math`
  - 线性映射
  - fBM 噪声
  - 区块矿物 seed 计算
  - 二次贝塞尔绳索采样辅助

### 9. 输入、玩家平面移动与主相机跟随

关键文件：

- [`Assets/Scripts/Kernel/Input/InputActionManager.cs`](Assets/Scripts/Kernel/Input/InputActionManager.cs)
- [`Assets/Scripts/Kernel/Player/PlayerPlaneMovement.cs`](Assets/Scripts/Kernel/Player/PlayerPlaneMovement.cs)
- [`Assets/Scripts/Kernel/Player/PlayerFollowCamera.cs`](Assets/Scripts/Kernel/Player/PlayerFollowCamera.cs)
- [`Assets/Input/Player Controls.cs`](Assets/Input/Player%20Controls.cs)

当前能力：

- `InputActionManager` 统一创建、启用、禁用并释放 `PlayerControls`
  - 若场景中未手动放置，会在首个场景加载前自动创建运行时实例
- 当前 `Player Controls` 已定义 `Movement/MovVector2`
  - 默认使用 `W/A/S/D` 输出 `Vector2`
  - `Accelerate` 当前绑定为 `Left Shift`
  - `Fire` 当前绑定为鼠标左键，并按住连续发射
- `PlayerPlaneMovement` 会把该输入映射到世界坐标 `(x, 0, z)`
  - 挂有动态 `Rigidbody` 时直接写入刚体 `velocity`
  - 挂有 `isKinematic = true` 的 `Rigidbody` 时走 `MovePosition`
  - 没有 `Rigidbody` 时直接改 `transform.position` 的 `XZ`
  - `transform.position.y` 保持不变
  - 按住加速键时，移动速度会乘以 `1.5`
  - 同时会基于主相机（或显式指定相机）把鼠标屏幕坐标投影到角色所在水平面，并让角色沿 Y 轴朝向该点
  - `rotationSpeed <= 0` 时会立即转向，否则按给定角速度平滑转向
  - 按住鼠标左键时，会按 `fireInterval` 连续向当前点击地面方向发射 `CharBullet`
  - 子弹瞄准优先使用真实地面/格子碰撞体命中点；命不中时回退到玩家高度平面
- `PlayerFollowCamera` 当前挂在 `Start.unity` 的 `Main Camera`
  - 只跟随玩家位置，不继承玩家旋转
  - 进入场景时会把正交尺寸恢复到局部战斗视野，不再自动切到“显示整张地图”的 framing

### 10. 文字子弹运行时组件

关键文件：

- [`Assets/Scripts/Kernel/Bullet/AttackSpec.cs`](Assets/Scripts/Kernel/Bullet/AttackSpec.cs)
- [`Assets/Scripts/Kernel/Bullet/CompiledAttack.cs`](Assets/Scripts/Kernel/Bullet/CompiledAttack.cs)
- [`Assets/Scripts/Kernel/Bullet/AttackFormulaCompiler.cs`](Assets/Scripts/Kernel/Bullet/AttackFormulaCompiler.cs)
- [`Assets/Scripts/Kernel/Bullet/AttackFormulaLoadout.cs`](Assets/Scripts/Kernel/Bullet/AttackFormulaLoadout.cs)
- [`Assets/Scripts/Kernel/Bullet/AttackProjectileEmitter.cs`](Assets/Scripts/Kernel/Bullet/AttackProjectileEmitter.cs)
- [`Assets/Scripts/Kernel/Bullet/TokenData`](Assets/Scripts/Kernel/Bullet/TokenData)
- [`Assets/Scripts/Kernel/Bullet/CharBullet.cs`](Assets/Scripts/Kernel/Bullet/CharBullet.cs)
- [`Assets/Prefabs/Bullet/CharBullet.prefab`](Assets/Prefabs/Bullet/CharBullet.prefab)
- [`Assets/Editor/AttackTokenAssetGenerator.cs`](Assets/Editor/AttackTokenAssetGenerator.cs)

当前能力：

- `CharBullet` 面向“文字即元素”的运行时子弹表达，默认使用 `TMP_Text` 作为字形承载
- 文字子弹现在分成两层数据：
  - `CompiledAttack` 表达由 token 公式编译出的高层语义结果
  - `AttackSpec` 表达单发子弹真正运行时要消费的底层参数
- 当前 token 资产全部继承自 `BaseTokenData : ScriptableObject`
  - `CoreTokenData`
  - `BehaviorTokenData`
  - `ValueTokenData`
  - `ResultTokenData`
  - 以及预留的 `PreTokenData / PostTokenData`
- 每个 token 资产都可以挂一组有序修饰表达式
  - 例如 `=Color.red` `#FF0000`
  - 例如 `+=10f`
  - 例如 `*=0.8`
- 每个 token 资产也可以直接设置最终子弹文本覆盖
  - 这部分不走表达式解析
  - 若多个已接受 token 都设置了文本覆盖，则按公式顺序最后一个生效
- `AttackFormulaCompiler` 当前支持从左到右编译：
  - `Core`
  - `Behavior`
  - `Value`
  - `Result`
  - `Post` 的位置校验与透传
  - 已被接受的 token 会继续按出现顺序回放修饰表达式
- 当前可执行的行为/结果组合是：
  - `Straight`
  - `Spread`
  - `DirectDamage`
  - `Explosion`
- `Value` 当前只支持单个数值载荷
  - 跟在 `Spread` 后时表示投射物数量
  - 跟在 `Explosion` 后时表示爆炸半径
- 所有与单发子弹有关的主要参数已集中到 `AttackSpec`
  - 当前包含 `CoreType / BehaviorType / ValueType / ResultType`
  - 以及 `Damage / ProjectileCount / BounceCount / ChainCount / PierceCount / ProjectileLife / ImpactLifeCost / ProjectileSpeed / MaxLifetime / MaxTravelDistance / ImpactMask`
  - 当前运行时已直接消费 `Damage / ProjectileLife / ImpactLifeCost / ProjectileSpeed / MaxLifetime / MaxTravelDistance / ImpactMask`
- `PlayerPlaneMovement` 当前会优先读取 `AttackFormulaLoadout`
  - 编译成功时按 `CompiledAttack` 发射
  - 仅接受来自 `AttackFormulaLoadout` 的 `CompiledAttack`
  - 编译失败时不会发射，并输出编译错误
- `CompiledAttack` 除了战斗语义之外，还会承载最终表现修饰
  - `DisplayText`
  - `ScaleMultiplier`
  - `ImpactRadiusMultiplier`
  - `TextColor`
  - `FontSize`
    - 当前语义是文字容器的方形宽高尺寸；相对运算会以当前 prefab 文字容器尺寸为基准
- `AttackProjectileEmitter` 会根据 `CompiledAttack` 生成一批实际子弹
  - `Straight` 发射 1 发
  - `Spread` 按对称角度发射多发
- 默认优先依赖 Inspector 手动拖拽 `glyphText / movementTarget / sizeTarget / movementRigidbody`
  - 如果未手动指定，只会在当前 prefab 层级内做轻量缓存，不会自动新建 GameObject
- 提供文字与尺寸控制接口
  - `TrySetText`
  - `TrySetTextColor`
  - `TrySetFontSize`
    - 当前会修改文字节点 `RectTransform` 的宽高，而不是直接写 `TMP_Text.fontSize`
    - 当前还会同步把 `SphereCollider.radius` 设为尺寸的一半
  - `TrySetBaseLocalScale`
  - `TrySetBaseUniformScale`
  - `TrySetScaleMultiplier`
  - `TrySetImpactRadiusMultiplier`
- 提供运动控制接口
  - `TrySetDirection`
  - `TrySetSpeed`
  - `TrySetDirectionAndSpeed`
  - `TrySetVelocity`
  - `MoveStep`
  - `TrySetWorldPosition`
  - `TrySetLocalPosition`
  - `TryTranslate`
  - `TryStopMovement`
- 提供 Hybrid 生命周期接口
  - `InitializeShot`
  - `ApplyLifeCost`
  - `Expire`
- 生命周期同时支持三种回收条件，并由 `AttackSpec` 统一给出参数
  - 超过最大存活时间
  - 超过最大飞行距离
  - 命中后扣减生命直至归零
- 当前默认命中配置为一次命中耗尽全部生命，但保留 `impactLifeCost` 用于后续穿透法术
- `Explosion` 命中时会先结算直击，再以命中点为中心做一次 `OverlapSphere` 范围伤害
- 可在 `Transform` 直驱、`Rigidbody.isKinematic` 和动态 `Rigidbody` 三种模式下复用
- 支持 `World / Self` 两种移动空间，可用于后续拼装法术、轨迹和表现层逻辑
- `CharBullet.prefab` 当前使用球形 Trigger Collider 作为命中体，并会按 `scaleMultiplier * impactRadiusMultiplier` 叠加缩放判定半径
- 可通过 Editor 菜单 `Tools/Lilith/Bullet/Generate Default Token Assets` 生成一组默认 token 资产到 `Assets/Data/BulletTokens`
  - `FireCore` 默认把文字设为红色
  - `EdgeCore` 默认会缩小子弹并提高弹速

### 11. 敌人生成与追踪

关键文件：

- [`Assets/Scripts/Kernel/Enemy/EnemyGenerator.cs`](Assets/Scripts/Kernel/Enemy/EnemyGenerator.cs)
- [`Assets/Scripts/Kernel/Enemy/Enemy.cs`](Assets/Scripts/Kernel/Enemy/Enemy.cs)
- [`Assets/Scripts/Kernel/Enemy/BaseCharEnemyNorm1.cs`](Assets/Scripts/Kernel/Enemy/BaseCharEnemyNorm1.cs)
- [`Assets/Scripts/Kernel/Enemy/CharEnemyMovement.cs`](Assets/Scripts/Kernel/Enemy/CharEnemyMovement.cs)
- [`Assets/Prefabs/Enemy/CharEnemy.prefab`](Assets/Prefabs/Enemy/CharEnemy.prefab)

当前能力：

- `EnemyGenerator` 可按随机区间 `spawnIntervalRange` 持续刷新敌人
- 刷怪位置以玩家为圆心，在 XZ 平面上按 `spawnDistance` 固定半径随机采样
- 每次刷怪会校验候选点是否落在 tag 为 `Ground` 的格子上；若命中非地面或越界区域，会在 `maxGroundSpawnRolls` 次数内继续重 roll
- 生成器支持手动指定 `targetPlayer`，未指定时会自动查找场景中的 `PlayerPlaneMovement`
- 生成器支持手动指定 `targetMapGrid`，未指定时会自动查找场景中的 `MapGridAuthoring`
- `EnemyGenerator` 期望 `CharEnemy.prefab` 已预挂 `CharEnemyMovement`，生成后只负责注入当前玩家目标
- `Enemy` 只定义敌人数据契约；当前由派生类对外提供 `moveSpeed / rotationSpeed / stoppingDistance`
- `BaseCharEnemyNorm1` 是当前默认的基础文字敌人类型，自身持有并校验默认移动参数与基础战斗参数
- `CharEnemyMovement` 会在 XZ 平面持续朝向并追踪玩家，并从同物体上的 `Enemy` 组件读取移动参数
- 同时兼容 `Transform` 直驱、`Rigidbody.isKinematic` 和动态 `Rigidbody` 三种移动模式
- 仅当 `Rigidbody` 挂在敌人自身根节点时才使用刚体驱动；若 prefab 误绑到子刚体，会自动回退到 `Transform` 驱动
- 进入 `stoppingDistance` 后会停止继续贴近，避免敌人持续抖动穿模

## 当前未闭环或需要特别注意的点

这些不是“猜测”，而是当前仓库快照里能直接看到的事实：

- [`Assets/Prefabs/UI`](Assets/Prefabs/UI) 当前只有 `MainMenuUI.cs` / `GameUIScreen.cs`，没有实际 `.prefab` 资产
- `UIManager` 默认通过 Addressables 实例化 UI，因此如果不补 prefab 资产或 Addressables 配置，`MainMenuScreen` 无法按当前设计被创建
- [`Assets/Scripts/StartUp.cs`](Assets/Scripts/StartUp.cs) 中很多真正的游戏启动内容还是预留：
  - `GameLoading` 压栈
  - `BuildingDatabase.LoadAllAsync`
  - `ItemDatabase.LoadAllAsync`
  - 其他全局系统初始化
- `MainMenuScreen` 的按钮逻辑目前大多还是空实现或注释
- 当前仓库中没有看到顶层 Save/Load 管理器
  - 只看到了 `Scribe` 基础设施和 `SaveStatus` 适配器
  - 没看到 `PolymorphRegistry.Register<SaveStatus>(...)` 之类的注册调用
- 当前仓库中没有看到本地化包、Def 数据或 Addressables 组配置资产的明确提交内容
- `Start.unity` 中的 `Startup` 根对象目前没有明显序列化 `Kernel.Startup` 组件
- `Start.unity` 中的 `UIRoot` 当前是未激活状态

## 常见修改入口

如果 agent 想快速落点，可以按需求直接跳到下面这些位置：

- 改启动流程：[`Assets/Scripts/StartUp.cs`](Assets/Scripts/StartUp.cs) + [`Assets/Scenes/Start.unity`](Assets/Scenes/Start.unity)
- 改游戏状态：[`Assets/Scripts/Kernel/Status.cs`](Assets/Scripts/Kernel/Status.cs) + [`Assets/Scripts/Kernel/StatusController.cs`](Assets/Scripts/Kernel/StatusController.cs)
- 加新 UI Screen：[`Assets/Prefabs/UI`](Assets/Prefabs/UI) + [`Assets/Scripts/Vocalith/UI/UIManager.cs`](Assets/Scripts/Vocalith/UI/UIManager.cs)
- 改网格生成、格子替换或 Scene Cell Edit：[`Assets/Scripts/Kernel/MapGridAuthoring.cs`](Assets/Scripts/Kernel/MapGridAuthoring.cs) + [`Assets/Editor/MapGridEditorUtility.cs`](Assets/Editor/MapGridEditorUtility.cs) + [`Assets/Editor/MapGridAuthoringEditor.cs`](Assets/Editor/MapGridAuthoringEditor.cs)
- 改坐标承载组件或 cell 位移控制：[`Assets/Scripts/Kernel/Cell/CellData.cs`](Assets/Scripts/Kernel/Cell/CellData.cs) + [`Assets/Scripts/Kernel/MapGridCoordinateBinding.cs`](Assets/Scripts/Kernel/MapGridCoordinateBinding.cs)
- 接存档：[`Assets/Scripts/Vocalith/Scribe`](Assets/Scripts/Vocalith/Scribe) + [`Assets/Scripts/Kernel/StatusSaveData.cs`](Assets/Scripts/Kernel/StatusSaveData.cs)
- 改本地化：[`Assets/Scripts/Vocalith/Localization`](Assets/Scripts/Vocalith/Localization)
- 查日志：[`Assets/Scripts/Vocalith/Log`](Assets/Scripts/Vocalith/Log)
- 改玩家输入或平面移动：[`Assets/Scripts/Kernel/Input`](Assets/Scripts/Kernel/Input) + [`Assets/Scripts/Kernel/Player`](Assets/Scripts/Kernel/Player) + [`Assets/Input/Player Controls.cs`](Assets/Input/Player%20Controls.cs)
- 改敌人生成、敌人数据或追踪：[`Assets/Scripts/Kernel/Enemy`](Assets/Scripts/Kernel/Enemy) + [`Assets/Prefabs/Enemy/CharEnemy.prefab`](Assets/Prefabs/Enemy/CharEnemy.prefab)
- 改文字子弹或文字法术表现：[`Assets/Scripts/Kernel/Bullet`](Assets/Scripts/Kernel/Bullet) + [`Assets/Prefabs/Bullet/CharBullet.prefab`](Assets/Prefabs/Bullet/CharBullet.prefab)


