# Lilith

## 一句话概览

当前仓库是一个 Unity 6 原型项目。已经明确落地的部分，主要是：

- 一个基于 `MapGridAuthoring` 的网格地图编辑工作流
- 一套基础设施层 `Vocalith`，包含日志、本地化、UI 栈、事件总线、自定义 JSON 存档 `Scribe`
- 一套轻量游戏状态系统 `Kernel.GameState`
- 一个业务 UI 示例 `MainMenuScreen`（脚本已写，资源与启动接线尚未完全闭环）

这份 README 的目标不是介绍“未来想做什么”，而是说明当前仓库里已经存在什么、在哪里，以及 agent 应该从哪里开始读。

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
- [`Assets/Scripts/Kernel/CellData.cs`](Assets/Scripts/Kernel/CellData.cs)
- [`Assets/Editor/MapGridEditorUtility.cs`](Assets/Editor/MapGridEditorUtility.cs)
- [`Assets/Editor/MapGridAuthoringEditor.cs`](Assets/Editor/MapGridAuthoringEditor.cs)
- [`Assets/Editor/MapGridAuthoringTests.cs`](Assets/Editor/MapGridAuthoringTests.cs)

当前能力：

- `MapGridAuthoring` 保存网格配置与 cell 索引：
  - 宽高
  - `cellSize`
  - chunk 切分尺寸
  - 默认 cell prefab
  - 相机 framing 参数
  - `cellEntries`
  - 运行时 `cellLookup`
- `MapGridCoordinateBinding` 通过反射把“任意组件上的坐标字段/属性/方法”接到网格系统
  - 当前场景配置使用 `CellData`
  - 既支持 `SetCoordinates(int, int)`，也支持 `SetCoordinates(Vector2Int)`
  - 既支持 `GetCoordinates()`，也支持直接读成员
- `MapGridCameraUtility` 能按网格尺寸自动框住目标相机
- Editor 侧已经支持：
  - `Generate Grid`
  - `Clear Grid`
  - `Rebuild Grid`
  - `Rebuild Index`
  - `Frame Camera`
  - `Replace Selected Cell`
  - `Scene Text Edit`
    - 在 Scene 视图里左键拖刷，批量修改生成 cell 下唯一的 `TMP_Text`
    - `Fill Text`：把刷过的格子文字改成当前输入字符串
    - `Erase Text`：把刷过的格子文字清空为 `string.Empty`
- 已有编辑器测试覆盖：
  - 局部坐标换算
  - chunk 计算
  - 网格中心计算
  - 索引查询
  - 重复坐标/越界坐标报错
  - Scene 文字刷图的坐标映射、路径插值、唯一 `TMP_Text` 校验

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
- 改网格生成、格子替换或 Scene 文字刷图：[`Assets/Scripts/Kernel/MapGridAuthoring.cs`](Assets/Scripts/Kernel/MapGridAuthoring.cs) + [`Assets/Editor/MapGridEditorUtility.cs`](Assets/Editor/MapGridEditorUtility.cs) + [`Assets/Editor/MapGridAuthoringEditor.cs`](Assets/Editor/MapGridAuthoringEditor.cs)
- 改坐标承载组件：[`Assets/Scripts/Kernel/CellData.cs`](Assets/Scripts/Kernel/CellData.cs) + [`Assets/Scripts/Kernel/MapGridCoordinateBinding.cs`](Assets/Scripts/Kernel/MapGridCoordinateBinding.cs)
- 接存档：[`Assets/Scripts/Vocalith/Scribe`](Assets/Scripts/Vocalith/Scribe) + [`Assets/Scripts/Kernel/StatusSaveData.cs`](Assets/Scripts/Kernel/StatusSaveData.cs)
- 改本地化：[`Assets/Scripts/Vocalith/Localization`](Assets/Scripts/Vocalith/Localization)
- 查日志：[`Assets/Scripts/Vocalith/Log`](Assets/Scripts/Vocalith/Log)

## 最后总结

如果只用一句话描述当前项目状态：

> 当前仓库已经搭好了“基础设施 + 网格编辑 + 状态系统 + 主菜单脚本”的骨架，其中 `MapGrid` 最完整，`UI / 启动 / 存档 / 数据加载` 仍处于基础框架已存在、业务闭环未完成的阶段。
