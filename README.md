# Lilith

## 一句话概览

当前仓库是一个 Unity 6 原型项目。已经明确落地的部分，主要是：

- 一个固定网格 + Seed 布局生成预览复用的地图工作流
- 一套基础设施层 `Vocalith`，包含日志、本地化、UI 栈、事件总线、自定义 JSON 存档 `Scribe`
- 一套轻量游戏状态系统 `Kernel.GameState`
- 一套已经落地到 prefab 的业务 UI：`StartUpMenuUI`、`MainUIScreen`、`PauseUIScreen`、`BackPackUIScreen`、`PopUpUIScreen`

这份 README 的目标是说明当前仓库里已经存在什么、在哪里，以及 agent 应该从哪里开始读，同时还有未来的规划。

## 工程事实

- Unity 版本：`6000.3.9f1`
- 启动场景：[`Assets/Scenes/StartUp.unity`](Assets/Scenes/StartUp.unity)
- 主 gameplay 场景：[`Assets/Scenes/Main.unity`](Assets/Scenes/Main.unity)
- 运行时代码主要分布在：
  - [`Assets/Scripts/Kernel`](Assets/Scripts/Kernel)
  - [`Assets/Scripts/Vocalith`](Assets/Scripts/Vocalith)
  - [`Assets/Scripts/Kernel/UI`](Assets/Scripts/Kernel/UI)（当前业务 UI 运行时代码）
  - [`Assets/Prefabs/UI`](Assets/Prefabs/UI)（当前业务 UI prefab 资源）
- Editor 工具位于 [`Assets/Editor`](Assets/Editor)，EditMode 测试位于 [`Assets/Editor/Test`](Assets/Editor/Test)
- 当前仓库里没有 `asmdef/asmref`
  - 运行时代码默认编进 `Assembly-CSharp`
  - Editor 代码默认编进 `Assembly-CSharp-Editor`

## Agent 与 Unity MCP

如果 agent 当前可以访问 `Unity MCP`，本仓库默认采用“源码编辑 + Editor 联动验证”的工作流。

- 涉及 `Scene`、`Prefab`、`GameObject`、`Component`、`Console`、`Test`、`Screenshot`、`Build`、`Package`、`Editor` 状态时，优先调用 `Unity MCP`
- 先读 `mcpforunity://editor/state`，确认 `ready_for_tools`、编译状态、活动场景，再决定读哪些文件
- 不要为了确认对象是否存在、组件是否挂载、场景是否激活、Console 是否报错而不必要的使用`unity MCP`，优先去手读 `.unity` / `.prefab` YAML 文本
- 纯 C# 业务逻辑修改仍以 `Assets/**/Scripts` 源码为主；修改后再通过 `Unity MCP` 检查编译、Console 和场景接线结果
- 只有在 `Unity MCP` 不可用，或要处理 `GUID` / `Missing Script` / `Missing Reference` / YAML merge / 文本级 diff 时，才回退到直接读 `.unity`、`.prefab`、`.meta`

建议优先使用的 `Unity MCP` 能力：

- 编辑器就绪与活动场景：`mcpforunity://editor/state`、`manage_scene(action="get_active")`
- 查找对象与组件：`find_gameobjects`、`manage_gameobject`、`manage_components`
- 查改 Prefab：`manage_prefabs`
- 检查编译与 Console：`read_console`
- 跑测试：`run_tests`、`get_test_job`
- 场景截图与视觉自检：`manage_camera`
- 批量执行编辑器操作：`batch_execute`


## 建议阅读顺序

如果 agent 需要快速建立上下文，建议按这个顺序读：

1. 若 `Unity MCP` 可用，先看 `mcpforunity://editor/state`、`manage_scene(action="get_active")`，并用 `find_gameobjects` 确认 `Startup`、`UIRoot`、`EnemyGenerator`、`WaveManager` 等对象是否实际存在
2. [`Assets/Scenes/StartUp.unity`](Assets/Scenes/StartUp.unity)
   先看当前启动菜单场景里的根对象：`Main Camera`、`Directional Light`、`UIRoot`、`GlobalStartup`。
3. [`Assets/Scripts/GlobalStartup.cs`](Assets/Scripts/GlobalStartup.cs)
   看启动场景的全局启动流程：语言初始化、状态初始化、Addressables 初始化，以及从启动菜单切到 `Main` 场景。
4. [`Assets/Scripts/StartUp.cs`](Assets/Scripts/StartUp.cs)
   看 `Main` 场景自己的本地启动流程：校验全局引导已完成，并在场景内容准备后 push `MainUIScreen`。
5. [`Assets/Scripts/Vocalith/UI/UIManager.cs`](Assets/Scripts/Vocalith/UI/UIManager.cs)
   看 UI 是怎么被实例化、压栈、出栈、跨场景保留和销毁的。
6. [`Assets/Scripts/Kernel/UI/StartUpMenuUI.cs`](Assets/Scripts/Kernel/UI/StartUpMenuUI.cs)
   看启动菜单四个按钮当前的绑定方式，以及 `Start` / `Load` / `Settings` / `Quit` 的入口。
7. [`Assets/Scripts/Kernel/UI/MainUIScreen.cs`](Assets/Scripts/Kernel/UI/MainUIScreen.cs) 和 [`Assets/Scripts/Kernel/UI/BackPackUIScreen.cs`](Assets/Scripts/Kernel/UI/BackPackUIScreen.cs)
   看当前战斗 HUD、暂停菜单和背包 Spell Book 的 UI 入口是怎么组织的。
8. [`Assets/Scripts/Kernel/Status.cs`](Assets/Scripts/Kernel/Status.cs) 和 [`Assets/Scripts/Kernel/StatusController.cs`](Assets/Scripts/Kernel/StatusController.cs)
   看状态机语义和切换规则。
9. [`Assets/Scripts/Kernel/MapGridAuthoring.cs`](Assets/Scripts/Kernel/MapGridAuthoring.cs)、[`Assets/Scripts/Kernel/ArenaSeedMapGenerator.cs`](Assets/Scripts/Kernel/ArenaSeedMapGenerator.cs) 和 [`Assets/Editor/MapGridAuthoringEditor.cs`](Assets/Editor/MapGridAuthoringEditor.cs)
   看地图网格的运行时契约、Seed 布局生成和 Editor 预览入口。
10. [`Assets/Scripts/Vocalith/Localization/LocalizationManager.cs`](Assets/Scripts/Vocalith/Localization/LocalizationManager.cs)
   看语言包、JSON 补丁和 Addressables 读取。
11. [`Assets/Scripts/Vocalith/Scribe/Scribe.cs`](Assets/Scripts/Vocalith/Scribe/Scribe.cs)
   看当前自定义存档系统的底层协议与读写入口。

## 代码分层

| 层 | 作用 | 关键路径 |
| --- | --- | --- |
| `Kernel` | 当前项目的业务语义层 | [`Assets/Scripts/Kernel`](Assets/Scripts/Kernel) |
| `Vocalith` | 通用基础设施层：日志、UI、存档、本地化、事件、工具类 | [`Assets/Scripts/Vocalith`](Assets/Scripts/Vocalith) |
| 业务 UI 脚本与 prefab | 具体 Screen/界面逻辑与资源 | [`Assets/Scripts/Kernel/UI`](Assets/Scripts/Kernel/UI) + [`Assets/Prefabs/UI`](Assets/Prefabs/UI) |
| Editor | 网格生成、替换工具与 EditMode 测试 | [`Assets/Editor`](Assets/Editor) + [`Assets/Editor/Test`](Assets/Editor/Test) |

当前层次关系可以理解为：

- `Kernel` 负责“游戏里发生什么”
- `Vocalith` 负责“这些系统用什么基础设施运行”
- `Assets/Scripts/Kernel/UI` 负责“某个具体界面怎么响应按钮和状态”
- `Assets/Prefabs/UI` 负责“这些界面的 prefab 资源和默认层级长什么样”

## 启动链路

### 代码层设计

当前仓库把启动链路拆成两个启动器：

- [`Assets/Scripts/GlobalStartup.cs`](Assets/Scripts/GlobalStartup.cs) 负责 `StartUp` 场景的全局系统初始化与场景切换
- [`Assets/Scripts/StartUp.cs`](Assets/Scripts/StartUp.cs) 负责 `Main` 场景的本地内容启动

[`Assets/Scripts/GlobalStartup.cs`](Assets/Scripts/GlobalStartup.cs) 当前会按这个顺序进入启动菜单：

1. `InitLanguage()`
   - 调用 [`LocalizationManager.InitializeAsync`](Assets/Scripts/Vocalith/Localization/LocalizationManager.cs)
2. `StatusController.Initialize()`
   - 清空状态栈
3. 若 `isEnableDevMode` 为真，加入 `DevMode`
4. 加入 `Loading` 状态
5. `InitGlobal()`
   - `Addressables.InitializeAsync()`
   - 预留 Def / 数据库加载入口
6. 移除 `Loading` 状态
7. 通过 [`UIManager.PushScreenAndWait<StartUpMenuUI>()`](Assets/Scripts/Vocalith/UI/UIManager.cs) 压入启动菜单
   - [`Assets/Scripts/Kernel/UI/StartUpMenuUI.cs`](Assets/Scripts/Kernel/UI/StartUpMenuUI.cs) 通过 `[UIPrefab("Assets/Prefabs/UI/StartUp UI Prefab")]` 实例化 [`Assets/Prefabs/UI/StartUp UI Prefab.prefab`](Assets/Prefabs/UI/StartUp%20UI%20Prefab.prefab)
   - `StartUpMenuUI` 自己维护 `InMainMenu` 状态，并接管开始 / 加载 / 设置 / 退出按钮
8. 点击 `Start`
   - [`Assets/Scripts/Kernel/UI/StartUpMenuUI.cs`](Assets/Scripts/Kernel/UI/StartUpMenuUI.cs) 会调用 [`GlobalStartup.RequestStartGame()`](Assets/Scripts/GlobalStartup.cs)
   - [`Assets/Scripts/Kernel/UI/StoryTellerUIScreen.cs`](Assets/Scripts/Kernel/UI/StoryTellerUIScreen.cs) 会实例化 [`Assets/Prefabs/UI/Storyteller Panel.prefab`](Assets/Prefabs/UI/Storyteller%20Panel.prefab)，只负责订阅剧情快照并显示正文
   - [`Assets/Scripts/Kernel/UI/StorySequenceParser.cs`](Assets/Scripts/Kernel/UI/StorySequenceParser.cs) 会作为持久化运行时服务，从 Addressables 地址 `Assets/Data/Story/Introduction` 读取剧情 JSON，按 `entries[].text` 或带可选 `speakerId/displayName/displayMode/text` 的协议逐句逐字播放；按空格或鼠标左键都会立刻补完当前句，句末停留后自动进入下一句
   - 首次使用空格或鼠标左键手动推进后，剧情界面里的 Skip Button 才会显示；点击它会直接快进到下一条 `replace` 边界
   - 若后续已经没有新的 `replace` 块，第一次点击 Skip Button 只会把最终显示块剩余文本全部展开；当最终块已经完整显示后，再次点击 Skip Button 才会结束播放
   - 最后一条文本播放完成后，才会调用 [`GlobalStartup.RequestEnterMainScene()`](Assets/Scripts/GlobalStartup.cs) 清理当前 UI 栈并切到 [`Assets/Scenes/Main.unity`](Assets/Scenes/Main.unity)

[`Assets/Scripts/StartUp.cs`](Assets/Scripts/StartUp.cs) 当前在 `Main` 场景里只负责：

1. 校验 [`GlobalStartup`](Assets/Scripts/GlobalStartup.cs) 已经存在且完成全局引导
2. 通过持久化的 [`UIManager`](Assets/Scripts/Vocalith/UI/UIManager.cs) 压入 [`MainUIScreen`](Assets/Scripts/Kernel/UI/MainUIScreen.cs)
3. 在场景内容准备完成后移除 `Loading` 状态
4. 回调 `GlobalStartup.NotifyMainSceneStartupComplete()` 结束全局启动器交接

同时，日志系统会在首场景加载前自动初始化：

- [`Assets/Scripts/Vocalith/Log/LogBootStrap.cs`](Assets/Scripts/Vocalith/Log/LogBootStrap.cs)
- [`Assets/Scripts/Vocalith/Log/LogForwarder.cs`](Assets/Scripts/Vocalith/Log/LogForwarder.cs)

### 当前场景实际接线

`StartUp.unity` 里当前能确认到的事实：

- `Main Camera` 当前挂有 [`PlayerFollowCamera`](Assets/Scripts/Kernel/Player/PlayerFollowCamera.cs) 和 [`CameraOcclusionFader`](Assets/Scripts/Kernel/Camera/CameraOcclusionFader.cs)
  - 当前使用斜俯角透视跟随，默认参数为 `pitch = 55`、`yaw = 35`、`distance = 260`、`fieldOfView = 35`，按住鼠标中键拖拽可绕 `Y` 轴旋转镜头
  - 关键 gameplay 视觉通过 [`GameplayBillboard`](Assets/Scripts/Kernel/Camera/GameplayBillboard.cs) 面向相机
  - 相机与玩家焦点之间的墙体会临时切到幽灵材质，降低透视遮挡
- `UIRoot` 上有 [`UIManager`](Assets/Scripts/Vocalith/UI/UIManager.cs)
  - `UIRoot` 当前在场景文件里是激活状态
- 场景里存在名为 `GlobalStartup` 的根对象
  - 当前显式挂有 [`Assets/Scripts/GlobalStartup.cs`](Assets/Scripts/GlobalStartup.cs)
  - `GlobalStartup` 会跨场景保留，直到 `Main` 场景自己的 [`Startup`](Assets/Scripts/StartUp.cs) 完成交接
- 启动菜单不是场景静态对象，而是由 [`GlobalStartup`](Assets/Scripts/GlobalStartup.cs) 通过 [`UIManager`](Assets/Scripts/Vocalith/UI/UIManager.cs) 动态压入的 [`Assets/Prefabs/UI/StartUp UI Prefab.prefab`](Assets/Prefabs/UI/StartUp%20UI%20Prefab.prefab)
  - 根节点当前挂的是 [`Assets/Scripts/Kernel/UI/StartUpMenuUI.cs`](Assets/Scripts/Kernel/UI/StartUpMenuUI.cs)
  - 该界面通过 `[UIPrefab("Assets/Prefabs/UI/StartUp UI Prefab")]` 进入 UI 栈
  - 四个按钮分别对应 `Start`、`Load`、`Option`、`Quit`
  - `Start` 当前会调用 `GlobalStartup.RequestStartGame()`，先显示 [`Assets/Prefabs/UI/Storyteller Panel.prefab`](Assets/Prefabs/UI/Storyteller%20Panel.prefab)，再由 [`Assets/Scripts/Kernel/UI/StorySequenceParser.cs`](Assets/Scripts/Kernel/UI/StorySequenceParser.cs) 播放世界观介绍，播放结束后切到 `Main`
  - `Load` 和 `Option` 当前会弹出通用信息弹窗，提示对应功能尚未实现
  - `Quit` 当前会在构建中直接退出应用，在 Unity Editor 中停止 Play
- `Main.unity` 当前保留一个显式 `Startup` 根对象
  - 当前挂的是 [`Assets/Scripts/StartUp.cs`](Assets/Scripts/StartUp.cs)
  - 若直接单独打开 `Main` 场景运行，会明确报错要求先从 `StartUp` 进入
- `Main.unity` 里的 `Player` 当前使用“主字 + 地面尖角影子”的视觉合同
  - 根节点挂有 [`Assets/Scripts/Kernel/Enemy/CharGlyphPresenter.cs`](Assets/Scripts/Kernel/Enemy/CharGlyphPresenter.cs) 与 [`Assets/Scripts/Kernel/Player/PlayerVisualPresenter.cs`](Assets/Scripts/Kernel/Player/PlayerVisualPresenter.cs)
  - `Text/Glyph` 使用 `TMP_Text` 作为 billboard 主字，默认显示 `"火"`
  - `GroundShadow` 使用单张尖角地影 sprite 平放在 `XZ` 地面，并复用玩家根节点已有的朝鼠标旋转来指向目标方向
- `Main.unity` 当前额外放置了一个正式的 `BackPackAttackPreviewRig`
  - 根节点挂有 [`Assets/Scripts/Kernel/UI/BackPackAttackPreviewRig.cs`](Assets/Scripts/Kernel/UI/BackPackAttackPreviewRig.cs)
  - 当前位于远离主玩法区域的位置，作为背包左侧预览的唯一运行时 rig 真源；背包打开时会临时启用其 `PreviewCamera` 并把输出绑定到 UI 的 `RawImage`

这意味着：

- 代码层已经有“启动器设计”
- 当前启动链路已经拆成“全局启动器 + Main 场景本地启动器”的两段流程

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
- [`Assets/Scripts/GlobalStartup.cs`](Assets/Scripts/GlobalStartup.cs)
- [`Assets/Scripts/Kernel/UI/GameUIScreen.cs`](Assets/Scripts/Kernel/UI/GameUIScreen.cs)
- [`Assets/Scripts/Kernel/UI/MainUIScreen.cs`](Assets/Scripts/Kernel/UI/MainUIScreen.cs)
- [`Assets/Scripts/Kernel/UI/PauseUIScreen.cs`](Assets/Scripts/Kernel/UI/PauseUIScreen.cs)
- [`Assets/Scripts/Kernel/UI/BackPackUIScreen.cs`](Assets/Scripts/Kernel/UI/BackPackUIScreen.cs)
- [`Assets/Scripts/Kernel/UI/PopUpUIScreen.cs`](Assets/Scripts/Kernel/UI/PopUpUIScreen.cs)
- [`Assets/Scripts/Kernel/UI/BackPackAttackPreviewController.cs`](Assets/Scripts/Kernel/UI/BackPackAttackPreviewController.cs)
- [`Assets/Scripts/Kernel/UI/BackPackAttackPreviewRig.cs`](Assets/Scripts/Kernel/UI/BackPackAttackPreviewRig.cs)
- [`Assets/Scripts/Kernel/UI/BackPackPreviewDummyEnemy.cs`](Assets/Scripts/Kernel/UI/BackPackPreviewDummyEnemy.cs)
- [`Assets/Scripts/Kernel/UI/StartUpMenuUI.cs`](Assets/Scripts/Kernel/UI/StartUpMenuUI.cs)
- [`Assets/Scripts/Kernel/UI/BackPackGridSlotView.cs`](Assets/Scripts/Kernel/UI/BackPackGridSlotView.cs)
- [`Assets/Prefabs/UI/MainMenuUI.cs`](Assets/Prefabs/UI/MainMenuUI.cs)
- [`Assets/Prefabs/UI/StartUp UI Prefab.prefab`](Assets/Prefabs/UI/StartUp%20UI%20Prefab.prefab)
- [`Assets/Prefabs/UI/BackPackUI.prefab`](Assets/Prefabs/UI/BackPackUI.prefab)
- [`Assets/Prefabs/UI/Info Popup.prefab`](Assets/Prefabs/UI/Info%20Popup.prefab)
- [`Assets/Prefabs/UI/BackPackAttackPreviewRig.prefab`](Assets/Prefabs/UI/BackPackAttackPreviewRig.prefab)
- [`Assets/Prefabs/UI/BackPack Grid Prefab.prefab`](Assets/Prefabs/UI/BackPack%20Grid%20Prefab.prefab)

当前能力：

- `UIManager` 管理四层 UI：
  - `Screen`
  - `Modal`
  - `Overlay`
  - `Toast`
- `UIRoot` 与 `EventSystem` 会跨场景保留，因此启动菜单切到 `Main` 时可以继续复用同一个 `UIManager`
- `UIManager` 现在会在运行时保证只保留一个兼容 Input System 的 `EventSystem`
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

- `StartUpMenuUI`
  - 挂在 [`Assets/Prefabs/UI/StartUp UI Prefab.prefab`](Assets/Prefabs/UI/StartUp%20UI%20Prefab.prefab) 根节点
  - 会自动绑定四个按钮：
    - `Start`
    - `Load`
    - `Option`
    - `Quit`
  - `Start` 会调用 [`GlobalStartup.RequestStartGame()`](Assets/Scripts/GlobalStartup.cs)
  - `Load` 和 `Option` 当前会弹出 [`PopUpUIScreen`](Assets/Scripts/Kernel/UI/PopUpUIScreen.cs) 提示功能尚未实现
  - `Quit` 当前直接退出游戏（Editor 下停止 Play）
- `StoryTellerUIScreen`
  - 挂在 [`Assets/Prefabs/UI/Storyteller Panel.prefab`](Assets/Prefabs/UI/Storyteller%20Panel.prefab) 根节点
  - 当前只负责绑定 `TMP_Text`、订阅 [`Assets/Scripts/Kernel/UI/StorySequenceParser.cs`](Assets/Scripts/Kernel/UI/StorySequenceParser.cs) 的剧情快照，并按 `text + maxVisibleCharacters` 刷新正文显示
  - prefab 内的 Skip Button 当前默认隐藏；在剧情服务标记可见后才显示，并把点击事件转发给剧情服务执行块级快进
- `StorySequenceParser`
  - 作为跨场景保留的运行时剧情播放服务，负责 Addressables 文本加载、JSON 解析、逐字播放、空格或鼠标左键补完当前句、句间停留和完成回调
  - 默认会从 Addressables 地址 `Assets/Data/Story/Introduction` 读取 [`Assets/Data/Story/Introduction.json`](Assets/Data/Story/Introduction.json)
  - 文本协议当前支持 `{ "entries": [{ "text": "..." }] }`，也兼容可选 `speakerId/displayName/displayMode/text` 字段，为后续多人对话扩展预留数据位
  - `displayMode` 当前支持 `replace` 和 `append`
  - `replace` 会从当前句开始覆盖显示；`append` 会保留上一段已完成文本，并在下一行继续逐字显示当前句，便于在 JSON 里显式控制“何时开始清屏重写”
  - 首次检测到空格或鼠标左键后，会把当前快照标记为“可显示 Skip Button”；Skip Button 会直接快进到下一条 `replace` 边界
  - 若后续已无新的 `replace` 块，第一次 Skip 只会把最终显示块全部显示出来；只有在最终块已经完整显示后再次点击 Skip，才会结束播放
  - 播放失败、解析失败或资源缺失时会记日志并直接继续进入 `Main`
- `MainUIScreen`
  - 作为战斗 HUD 模板，当前暴露血条区、顶部 `Spell Panel` 和暂停按钮引用
  - `TopPanel/Spell Panel` 会复用 prefab 中已有的 `BackPack Grid Prefab` 子节点作为模板，并在运行时按 `AttackFormulaLoadout` 的展开占格结果生成只读展示槽位；连锁件会按自身跨度显示为多个相邻 HUD 槽位
  - `Spell Panel` 当前还会通过独立 overlay layer 给多格连锁件绘制整件外框，保持与背包内的连锁提示一致
  - 顶部 `PauseButton` 当前会通过 `UIInputRouter` 打开 `PauseUIScreen`
- `PauseUIScreen`
  - 作为暂停菜单模板，当前暴露遮罩、主面板和三个按钮引用
  - `Resume` 当前会通过 [`UIInputRouter`](Assets/Scripts/Kernel/UI/UIInputRouter.cs) 关闭暂停菜单
  - `Option` 当前会弹出 [`PopUpUIScreen`](Assets/Scripts/Kernel/UI/PopUpUIScreen.cs) 提示功能尚未实现
  - `Back` 当前会清空战斗 UI 栈并切回 [`Assets/Scenes/StartUp.unity`](Assets/Scenes/StartUp.unity)
- `PopUpUIScreen`
  - 挂在 [`Assets/Prefabs/UI/Info Popup.prefab`](Assets/Prefabs/UI/Info%20Popup.prefab) 根节点
  - 当前会自动绑定正文文本、顶部关闭按钮和底部 `Confirm / Close` 两个按钮
  - 对外提供统一 `Configure` / `SetInfoText` / `SetConfirmButton` / `SetCloseButton` 接口，便于复用成通用信息弹窗
  - [`Assets/Scripts/Kernel/UI/PopUpUIUtility.cs`](Assets/Scripts/Kernel/UI/PopUpUIUtility.cs) 当前统一封装了弹窗复用与提示文案写入逻辑，供启动菜单和暂停菜单复用
  - 关闭时会兼容自己被作为 `Screen` 或 `Modal` 打开两种情况，并清理 `PopUp` 状态
- `BackPackUIScreen`
  - `BackPack Grid Panel/Grid` 当前固定为 `8` 列，并在运行时生成 `48` 个背包槽位
  - `Spell Book` 当前在 prefab 内预放 `5` 个静态槽位，并通过 [`BackPackGridSlotView`](Assets/Scripts/Kernel/UI/BackPackGridSlotView.cs) 接收拖拽
  - 打开背包时会把 `AttackFormulaLoadout` 的有序 `PlaceableTokenData` 逐件映射到 `Spell Book`；连锁件会占用多个相邻格
  - 若历史 loadout 的总宽度超过 `5` 格，超出的整件会尝试回填到玩家背包库存；若背包不存在同一行连续空位，则该整件会被丢弃并记录 warning
  - 每次 `Spell Book` 变更后，会按从左到右压缩整件锚点并实时写回 `AttackFormulaLoadout`
  - 背包区与 `Spell Book` 区当前支持整件拖拽；拖拽连锁件任一 segment 时都会移动整件，且不允许拆开、旋转、跨行放置或与其他占格部分重叠
  - 背包区、`Spell Book` 区和拖拽预览当前都会通过独立 overlay layer 给多格连锁件绘制整件外框；开始拖拽连锁件时，源区域外框会临时隐藏，`DragPreviewLayer` 里的白色拖拽预览框也会扩展到整件跨度并保持鼠标抓取点不变
  - `Left Panel/Preview Animation` 当前会显示一个离屏攻击预览；它复用玩家当前的 `CharBullet` prefab 和 `CompiledAttack`
  - 预览通过 [`BackPackAttackPreviewController`](Assets/Scripts/Kernel/UI/BackPackAttackPreviewController.cs) 解析 `Main` 场景里唯一的 [`BackPackAttackPreviewRig`](Assets/Scripts/Kernel/UI/BackPackAttackPreviewRig.cs)，并把该 rig 的 `PreviewCamera` 输出直接绑定到 `Preview Render`
  - 机位、地板、伪玩家、伪敌人编队、爆炸圈等布局由 [`Assets/Prefabs/UI/BackPackAttackPreviewRig.prefab`](Assets/Prefabs/UI/BackPackAttackPreviewRig.prefab) authoring，并由 `Main` 场景中的静态实例在运行时直接复用；控制器不会重写相机位置、旋转或 `orthographicSize`
  - 左侧预览会循环演示 `Straight / Spread / Explosion` 的实际表现，并让 rig 内全部 [`BackPackPreviewDummyEnemy`](Assets/Scripts/Kernel/UI/BackPackPreviewDummyEnemy.cs) 一起参与重置、命中反馈和爆炸提示；主瞄准点默认优先取 `PreviewDummy-M`
  - 当公式缺少 `Core` 或当前玩家缺少子弹 prefab 时，左侧会显示状态文案，而不是尝试发射无效预览

### 3. 地图网格 Authoring

关键文件：

- [`Assets/Scripts/Kernel/MapGridAuthoring.cs`](Assets/Scripts/Kernel/MapGridAuthoring.cs)
- [`Assets/Scripts/Kernel/ArenaSeedLayoutBuilder.cs`](Assets/Scripts/Kernel/ArenaSeedLayoutBuilder.cs)
- [`Assets/Scripts/Kernel/ArenaSeedMapGenerator.cs`](Assets/Scripts/Kernel/ArenaSeedMapGenerator.cs)
- [`Assets/Scripts/Kernel/MapGridCoordinateBinding.cs`](Assets/Scripts/Kernel/MapGridCoordinateBinding.cs)
- [`Assets/Scripts/Kernel/Cell/CellData.cs`](Assets/Scripts/Kernel/Cell/CellData.cs)
- [`Assets/Editor/MapGridEditorUtility.cs`](Assets/Editor/MapGridEditorUtility.cs)
- [`Assets/Editor/MapGridAuthoringEditor.cs`](Assets/Editor/MapGridAuthoringEditor.cs)
- [`Assets/Editor/MapGridMigrationUtility.cs`](Assets/Editor/MapGridMigrationUtility.cs)
- [`Assets/Editor/Test/MapGridAuthoringTests.cs`](Assets/Editor/Test/MapGridAuthoringTests.cs)
- [`Assets/Editor/Test/ArenaSeedMapGenerationTests.cs`](Assets/Editor/Test/ArenaSeedMapGenerationTests.cs)

当前能力：

- `MapGridAuthoring` 保存网格配置与 cell 索引：
  - 宽高
  - `cellSize`
  - chunk 切分尺寸
  - 默认 cell prefab
  - `cellEntries`
  - 运行时 `cellLookup`
  - 网格坐标直接工作在 `XZ` 平面
    - `GetCellLocalPosition(x, y)` 返回 `(x * cellSize.x, 0, y * cellSize.y)`
    - `TryGetCellCoordinateFromLocalPoint()` 读取 `localPoint.x / localPoint.z`
  - `WorldPlaneY`
    - 暴露当前地图统一的世界平面高度，供 grounded 角色落地、鼠标投影、刷怪点和 pickup 生成共用
  - `TryRefreshGroundWallState()`
    - 可在运行时或 Editor Action 中按 `CellData.SurfaceType` 同步 cell 的 tag、模型显示与当前激活 Collider
  - `TryApplySurfaceLayout(layout, out error)`
    - 接收一份按 `y` 外层、`x` 内层排列的 row-major 墙地布局，并只通过现有 `CellData` 批量刷新整张地图的表面状态
  - `TryInitializeCellSurfaceCache() / TryMarkCellSurfaceDirty() / TryRefreshDirtyGroundWallState()`
    - 支持先做一次全量 `CellData + SurfaceType + ManagedCollider` 缓存，再在运行时只刷新被标脏的格子，避免每次都重扫整张地图
- `ArenaSeedLayoutBuilder` 负责纯布局算法：
  - 从固定 grid size、seed、玩家参考格和可选刷怪环半径，生成一份确定性的 row-major `Ground/Wall` 布局
  - 当前算法会保留边界墙、玩家安全区和刷怪环，并在放置矩形障碍时保证保留区仍然可达
- `ArenaSeedMapGenerator` 负责把 seed 布局接到场景：
  - 可挂在 `MapRoot` 上，复用 `MapGridAuthoring` 作为运行时唯一地图契约
  - `Awake` 时可按当前 seed 一次性重写整张地图的墙地表面
  - 可在生成后把玩家一次性 snap 到最近的合法地面格子
  - 会读取场景中首个 `EnemyGenerator.spawnDistance`，把默认刷怪环一起保留为可生成区域
- `MapGridCoordinateBinding` 通过反射把“任意组件上的坐标字段/属性/方法”接到网格系统
  - 当前场景配置使用 `CellData`
  - 既支持 `SetCoordinates(int, int)`，也支持 `SetCoordinates(Vector2Int)`
  - 既支持 `GetCoordinates()`，也支持直接读成员
- `CellData` 当前还负责：
  - 显式保存 `CellSurfaceType { Ground, Wall }`
  - 缓存 `wallCollider / groundCollider / wallModelRoot / groundModelRoot`
  - 将 `ManagedCollider` 定义为“当前激活表面对应的 Collider”
  - 在 `Ground/Wall` 切换时同步模型、Collider 与 tag
  - 暴露 cell 根节点或指定子节点的移动控制接口
  - 在绑定了目标 `Transform/Rigidbody` 后，支持位置、平移、速度与停止控制
- Editor 侧已经支持：
  - `Generate Grid`
  - `Clear Grid`
  - `Rebuild Grid`
  - `Rebuild Index`
  - `Sync Surface State`
    - 遍历所有已索引 cell，按 `CellData.SurfaceType` 统一刷新 tag、模型与当前表面 Collider
  - `Normalize Cell Presentation`
    - 当前等价于一次显式 `Sync Surface State`
  - `Seed Generation`
    - `Preview From Seed`：调用与运行时相同的 seed 生成路径，直接覆写当前场景里的墙地布局
    - `Randomize Seed`：生成新 seed 并立即预览
    - `Snap Player To Generated Cell`：把当前玩家一次性对齐到最近的地面格子
  - `Replace Selected Cell`
  - `Tools/Lilith/Map/Migrate Start Scene To Cell3D`
    - 规范 [`Assets/Prefabs/Map/Cell3D.prefab`](Assets/Prefabs/Map/Cell3D.prefab) 的 `CellData` 和根 `Rigidbody`
    - 把 `Start.unity` 的 `MapRoot` 迁移到 `XZ` 平面并重建整张 grid
  - `Scene Cell Edit`
    - 在 Scene 视图里支持两种选择模式：
      - `Paint`：左键拖刷路径上的格子
      - `Rectangle`：点击一个起点后拖到另一个点，松开鼠标时对包围矩形内所有格子应用操作
    - `PaintGround`：把刷过的格子切到地面状态，并同步地面模型/Collider
    - `PaintWall`：把刷过的格子切到墙体状态，并同步墙体模型/Collider
    - `SetColliderState`：按当前 `Enable Collider` 选项，仅对当前激活表面 Collider 做调试开关
    - 当前仍保留为手动调试 / 特殊修图入口，但不再是主地图 authoring 工作流
- 已有编辑器测试覆盖：
  - `XZ` 平面的局部坐标换算
  - chunk 计算
  - 网格中心计算
  - 索引查询
  - 重复坐标/越界坐标报错
  - `CellData` 的表面切换、模型/Collider/tag 同步
  - Scene Cell Edit 的路径插值、矩形框选坐标与墙地绘制
  - seed 布局的稳定性、边界墙、玩家安全区、刷怪环、障碍拒绝规则、批量 surface 应用和玩家 snap

### 4. 基础战斗交互

关键文件：

- [`Assets/Scripts/Kernel/Player/PlayerPlaneMovement.cs`](Assets/Scripts/Kernel/Player/PlayerPlaneMovement.cs)
- [`Assets/Scripts/Kernel/Player/PlayerFollowCamera.cs`](Assets/Scripts/Kernel/Player/PlayerFollowCamera.cs)
- [`Assets/Scripts/Kernel/Camera/GameplayBillboard.cs`](Assets/Scripts/Kernel/Camera/GameplayBillboard.cs)
- [`Assets/Scripts/Kernel/Camera/CameraOcclusionFader.cs`](Assets/Scripts/Kernel/Camera/CameraOcclusionFader.cs)
- [`Assets/Scripts/Kernel/WorldHeightUtility.cs`](Assets/Scripts/Kernel/WorldHeightUtility.cs)
- [`Assets/Scripts/Kernel/Bullet/AttackSpec.cs`](Assets/Scripts/Kernel/Bullet/AttackSpec.cs)
- [`Assets/Scripts/Kernel/Bullet/AttackFormulaCompiler.cs`](Assets/Scripts/Kernel/Bullet/AttackFormulaCompiler.cs)
- [`Assets/Scripts/Kernel/Bullet/AttackFormulaLoadout.cs`](Assets/Scripts/Kernel/Bullet/AttackFormulaLoadout.cs)
- [`Assets/Scripts/Kernel/Bullet/AttackProjectileEmitter.cs`](Assets/Scripts/Kernel/Bullet/AttackProjectileEmitter.cs)
- [`Assets/Scripts/Kernel/Bullet/BulletTargetPolicy.cs`](Assets/Scripts/Kernel/Bullet/BulletTargetPolicy.cs)
- [`Assets/Scripts/Kernel/Bullet/CharBullet.cs`](Assets/Scripts/Kernel/Bullet/CharBullet.cs)
- [`Assets/Scripts/Kernel/Enemy/Enemy.cs`](Assets/Scripts/Kernel/Enemy/Enemy.cs)
- [`Assets/Scripts/Kernel/Enemy/BaseCharEnemyNorm1.cs`](Assets/Scripts/Kernel/Enemy/BaseCharEnemyNorm1.cs)

当前能力：

- `WorldHeightUtility` 当前负责统一高度契约：
  - grounded 对象通过“地图平面高度 + 参考 Collider”计算根节点 `Y`
  - floating 对象通过“地图平面高度 + 显式偏移”计算生成高度
  - 鼠标射线和其他平面投影统一按指定 `planeY` 处理，不再要求 gameplay 代码写死 `y = 0`
- `AttackSpec` 仍然是单发子弹的底层运行时配置，负责速度、生命周期、命中层级和基础伤害等参数
- `AttackFormulaLoadout` 持有当前装备的有序 `PlaceableTokenData` 列表，并缓存最新 `CompiledAttack`
  - 对旧调用方仍保留展开后的 `BaseTokenData` 只读视图
- `AttackFormulaCompiler` 负责先把 `PlaceableTokenData` 展开成成员 token，再按 `Pre? + Core + Behavior? + Value? + Result? + Value? + Post?` 编译成 `CompiledAttack`
  - 缺少 `Core` 时为硬失败
  - 其他非法顺序默认给出 warning 并尽力继续编译
  - `Behavior` 缺失时默认 `Straight`
  - `Result` 缺失时默认 `DirectDamage`
  - token 还可以直接声明最终子弹文本覆盖；这部分不走 DSL，按被接受 token 的顺序最后一个生效
  - 已被编译器接受的 token 还会按顺序回放自己的修饰 DSL
  - `LinkedTokenData` 只有在其全部成员 token 都被接受时，才会按配置对最终 `AttackSpec.damage` 乘上 `damageMultiplier`
  - 当前 DSL 支持 `= / += / -= / *= / /=`
  - 当前可修饰目标包括 `TextColor / FontSize / ScaleMultiplier / ProjectileSpeed / MaxLifetime / MaxTravelDistance / ImpactRadiusMultiplier`
  - `FontSize` 当前不再直接写 TMP 字号，而是驱动文字节点 `RectTransform` 的宽高，默认保持宽高一致，并在运行时基于当前文字容器尺寸执行 `+= / -= / *= / /=`
  - 当 `FontSize` 生效时，球形碰撞体半径会同步锁定为文字容器边长的一半
- `PlayerPlaneMovement` 发射时只会读取 `AttackFormulaLoadout` 的编译结果；缺少 loadout、loadout 为空或编译失败时都不会发射
- `AttackProjectileEmitter` 会把 `CompiledAttack` 落地成实际子弹批次
  - 当前支持 `Straight`
  - 当前支持 `Spread`
- `AttackProjectileEmitter` 当前还会把 `BulletTargetPolicy` 一并下发给每发 `CharBullet`
- `CharBullet` 会从 `AttackSpec` 读取伤害、弹速、命中消耗和飞行回收参数，并从 `CompiledAttack` 读取最终表现修饰
  - 当前目标策略支持 `EnemiesOnly / PlayerOnly / Both`
  - 命中合法目标时会尝试从碰撞体父级解析 `Enemy` 或 `PlayerHealth` 并调用统一受伤接口
  - 命中墙体等环境仍会消耗子弹生命；命中不在当前目标策略内的 actor 不会结算伤害，也不会消耗生命
  - 当前支持 `DirectDamage`
  - 当前支持 `Explosion`，并按“直击 + 爆炸 AoE”两段结算主目标
- `Enemy` 统一暴露 `MaxHealth`、`CurrentHealth`、`IsDead`、`TryApplyDamage`，并提供 `Damaged / Died` 两类运行时通知
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
- [`Assets/Scripts/Kernel/Player/PlayerHealth.cs`](Assets/Scripts/Kernel/Player/PlayerHealth.cs)
- [`Assets/Scripts/Kernel/Player/PlayerFollowCamera.cs`](Assets/Scripts/Kernel/Player/PlayerFollowCamera.cs)
- [`Assets/Input/Player Controls.cs`](Assets/Input/Player%20Controls.cs)

当前能力：

- `InputActionManager` 统一创建、启用、禁用并释放 `PlayerControls`
  - 若场景中未手动放置，会在首个场景加载前自动创建运行时实例
- 当前 `Player Controls` 已定义 `Movement/MovVector2`
  - 默认使用 `W/A/S/D` 输出 `Vector2`
  - `Accelerate` 当前绑定为 `Left Shift`
  - `Fire` 当前绑定为鼠标左键，并按住连续发射
- 当前 `UI Controls` 已定义 `MainScene UI/Backpack` 与 `MainScene UI/Router`
  - [`Assets/Scripts/Kernel/Input/InputActionManager.cs`](Assets/Scripts/Kernel/Input/InputActionManager.cs) 统一持有 `UIControls`
  - [`Assets/Scripts/Kernel/UI/UIInputRouter.cs`](Assets/Scripts/Kernel/UI/UIInputRouter.cs) 负责消费这份输入并执行 UI 路由
  - 只有当 [`Assets/Scripts/Kernel/UI/MainUIScreen.cs`](Assets/Scripts/Kernel/UI/MainUIScreen.cs) 位于栈顶时，按下 `Backpack` 才会打开 [`Assets/Scripts/Kernel/UI/BackPackUIScreen.cs`](Assets/Scripts/Kernel/UI/BackPackUIScreen.cs)
  - 当 `BackPackUIScreen` 位于栈顶时，再次按下 `Backpack` 会直接关闭它
  - 按下 `Router`（当前默认 `Esc`）会优先关闭顶层 modal，其次关闭 `BackPackUIScreen` / [`Assets/Scripts/Kernel/UI/PauseUIScreen.cs`](Assets/Scripts/Kernel/UI/PauseUIScreen.cs)，或在 `MainUIScreen` 位于栈顶时打开 `PauseUIScreen`
- `PlayerPlaneMovement` 会把该输入映射到当前主相机视角对应的 gameplay plane 方向，其中 grounded 根节点高度由 `MapGridAuthoring.WorldPlaneY` 和参考 `Collider` 共同决定
  - 会优先解析 `targetMapGrid` 和 `groundingReferenceCollider`
  - `Awake / OnValidate` 时会尝试把玩家 root snap 到地图平面，并把参考 `Collider` 的底部贴到地面
  - 挂有 `Rigidbody` 时会统一配置为 `useGravity = false` 并冻结 `Y` 轴位置，避免重新沉回地面
  - 挂有动态 `Rigidbody` 时直接写入刚体 `velocity`
  - 挂有 `isKinematic = true` 的 `Rigidbody` 时走 `MovePosition`
  - 没有 `Rigidbody` 时直接改 `transform.position` 的 `XZ`
  - `transform.position.y` 保持不变
  - 按住加速键时，移动速度会乘以 `1.5`
  - 前后左右会基于当前主相机投影到水平面的 `forward/right` 重映射，因此旋转镜头后，移动方向会跟着当前视角变化
  - 同时会基于主相机（或显式指定相机）把鼠标屏幕坐标投影到 `MapGridAuthoring.WorldPlaneY` 对应的 gameplay plane，并让角色沿 Y 轴朝向该点
  - `rotationSpeed <= 0` 时会立即转向，否则按给定角速度平滑转向
  - 按住鼠标左键时，会按 `fireInterval` 连续向当前点击地面方向发射 `CharBullet`
  - 当前通过 `BulletPrefab` 只读属性暴露实际发射使用的子弹 prefab，供背包攻击预览复用同一份表现资源
  - 子弹瞄准优先使用真实地面/格子碰撞体命中点；命不中时回退到地图 gameplay plane，而不是玩家当前 root 高度
  - 当 `InBackPack` 或 `InPauseMenu` 状态存在时，会暂停移动、转向和开火；动态刚体玩家还会清零平面速度避免继续滑行
- `PlayerFollowCamera` 当前挂在 `Start.unity` 的 `Main Camera`
  - 当前使用斜俯角透视跟随，不继承玩家旋转，并支持按住鼠标中键拖拽绕 `Y` 轴调整镜头偏航
  - 当前默认参数为 `focusOffset = (0, 8, 0)`、`distance = 260`、`pitch = 55`、`yaw = 35`、`fieldOfView = 35`
  - `Camera.orthographic` 会被强制关闭，改为透视镜头
- `GameplayBillboard` 当前用于关键 gameplay 视觉层
  - `Player/Text`、`BaseCharObject/Text`、`CharBullet/Text`、`BulletTokenPickup/Glyph` 和 `BulletTokenPickup/Shadow` 都会对齐主相机朝向
  - 环境文字和 `Cell3D` 墙地模型不使用 billboard
- [`Assets/Scripts/Kernel/Player/PlayerVisualPresenter.cs`](Assets/Scripts/Kernel/Player/PlayerVisualPresenter.cs) 当前负责玩家主字与尖角地影布局
  - `Player/Text` 只负责 billboard，`Player/Text/Glyph` 承载主字显示
  - `Player/GroundShadow` 使用 `SpriteRenderer` 平放在 `XZ` 地面，并按玩家根 `BoxCollider` 底边自动重排
  - 主字不会跟着玩家朝向一起转动；尖角地影会跟随玩家根节点 yaw 指向当前鼠标方向
- `CameraOcclusionFader` 当前挂在 `Main Camera`
  - 每帧检测相机与玩家焦点之间的遮挡
  - 只处理 `CellData.SurfaceType == Wall` 的墙体模型
  - 被遮挡的墙体会临时切到幽灵材质，离开遮挡后恢复原材质
- `PlayerHealth` 当前提供最小生命值与受伤入口
  - 当前只负责维护 `maxHealth / currentHealth` 和 `TryApplyDamage`
  - 当前还没有 UI 血条、死亡演出、复活或状态机接线

### 10. 文字子弹运行时组件

关键文件：

- [`Assets/Scripts/Kernel/Bullet/AttackSpec.cs`](Assets/Scripts/Kernel/Bullet/AttackSpec.cs)
- [`Assets/Scripts/Kernel/Bullet/CompiledAttack.cs`](Assets/Scripts/Kernel/Bullet/CompiledAttack.cs)
- [`Assets/Scripts/Kernel/Bullet/AttackFormulaCompiler.cs`](Assets/Scripts/Kernel/Bullet/AttackFormulaCompiler.cs)
- [`Assets/Scripts/Kernel/Bullet/AttackFormulaLoadout.cs`](Assets/Scripts/Kernel/Bullet/AttackFormulaLoadout.cs)
- [`Assets/Scripts/Kernel/Bullet/AttackProjectileEmitter.cs`](Assets/Scripts/Kernel/Bullet/AttackProjectileEmitter.cs)
- [`Assets/Scripts/Kernel/Bullet/TokenData`](Assets/Scripts/Kernel/Bullet/TokenData)
- [`Assets/Scripts/Kernel/Bullet/CharBullet.cs`](Assets/Scripts/Kernel/Bullet/CharBullet.cs)
- [`Assets/Scripts/Kernel/Bullet/CharBulletVisualPresenter.cs`](Assets/Scripts/Kernel/Bullet/CharBulletVisualPresenter.cs)
- [`Assets/Scripts/Kernel/Bullet/CharBulletVisualLibrary.cs`](Assets/Scripts/Kernel/Bullet/CharBulletVisualLibrary.cs)
- [`Assets/Scripts/Kernel/Player/PlayerBulletTokenInventory.cs`](Assets/Scripts/Kernel/Player/PlayerBulletTokenInventory.cs)
- [`Assets/Scripts/Kernel/Bullet/BulletTokenPickup.cs`](Assets/Scripts/Kernel/Bullet/BulletTokenPickup.cs)
- [`Assets/Scripts/Kernel/UI/BackPackTokenLayoutUtility.cs`](Assets/Scripts/Kernel/UI/BackPackTokenLayoutUtility.cs)
- [`Assets/Scripts/Kernel/UI/BackPackUIScreen.cs`](Assets/Scripts/Kernel/UI/BackPackUIScreen.cs)
- [`Assets/Prefabs/Bullet/CharBullet.prefab`](Assets/Prefabs/Bullet/CharBullet.prefab)
- [`Assets/Prefabs/Bullet/BulletTokenPickup.prefab`](Assets/Prefabs/Bullet/BulletTokenPickup.prefab)
- [`Assets/Editor/AttackTokenAssetGenerator.cs`](Assets/Editor/AttackTokenAssetGenerator.cs)
- [`Assets/Editor/CharBulletVisualAssetGenerator.cs`](Assets/Editor/CharBulletVisualAssetGenerator.cs)
- [`Assets/Data/BulletVisuals`](Assets/Data/BulletVisuals)
- [`Assets/Art/BulletRunes`](Assets/Art/BulletRunes)

当前能力：

- `CharBullet` 面向“文字即元素”的运行时子弹表达，默认使用 `TMP_Text` 作为字形承载
- 当前子弹视觉已落地为“悬浮符文弹”组织
  - 主字保持俯视角可读的平放方案
  - 立体感由阴影字、双层符文底座和短拖尾提供
- 文字子弹现在分成两层数据：
  - `CompiledAttack` 表达由 token 公式编译出的高层语义结果
  - `AttackSpec` 表达单发子弹真正运行时要消费的底层参数
- 当前可放置 token 资产全部继承自 `PlaceableTokenData : ScriptableObject`
  - `BaseTokenData` 表示单格 item，同时继续作为 `Core / Behavior / Value / Result / Pre / Post` 的基础类型
  - `LinkedTokenData` 表示横向连续的多格 item，内部持有有序 `BaseTokenData` 成员列表
- 每个 token 资产都可以挂一组有序修饰表达式
  - 例如 `=Color.red` `#FF0000`
  - 例如 `+=10f`
  - 例如 `*=0.8`
- 每个 token 资产也可以直接设置最终子弹文本覆盖
  - 这部分不走表达式解析
  - 若多个已接受 token 都设置了文本覆盖，则按公式顺序最后一个生效
- `LinkedTokenData` 额外支持：
  - 固定横向跨度 `2..N`
  - `pickupDisplayTextOverride`
  - `damageMultiplier`
  - 内部成员按顺序展开参与公式编译，但在背包与 `Spell Book` 中始终作为不可拆整件移动
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
- `PlayerBulletTokenInventory` 当前作为玩家“拥有 token”的独立库存层
  - 固定 `48` 格、`8` 列
  - 允许重复 token
  - Inspector 可预置起始 `PlaceableTokenData`；运行时会复制到固定占格数组
  - 连锁件只能放入同一行的连续空格，不能跨行、拆开或自动重排
- `BulletTokenPickup` 当前提供最小世界拾取物闭环
  - 使用 `TMP_Text` 显示当前 item 的拾取文本；优先使用 `LinkedTokenData.pickupDisplayTextOverride`，否则按成员 token 文本拼接
  - 当前 prefab 采用 `Root -> Glyph` 的单字形显示组织，根节点脚本持有 `glyphText` 引用并负责刷新文本
  - 玩家碰到 trigger 后，会尝试把 item 放入 `PlayerBulletTokenInventory`
  - 缺少连续空位或背包已满时 pickup 会保留在场景中，不会吞掉掉落
- `BackPackUIScreen + BackPackTokenLayoutUtility` 当前提供一条战斗中的 Spell Book 编译入口
  - `BackPackUI.prefab` 的 `Spell Book` 内固定有 `5` 个静态槽位
  - `BackPackUI.prefab` 的背包网格固定为 `8` 列，运行时会额外生成 `48` 个背包槽位到 `BackPack Grid Panel/Grid`
  - 背包区与 `Spell Book` 区支持整件拖拽；连锁件从任意占用格开始拖拽都会移动整件
  - 背包区、`Spell Book` 区和拖拽预览会额外绘制独立连锁外框，用于高亮整件范围；连锁件拖拽时，白色预览框本身也会扩成整件宽度
  - `Spell Book` 会按整件锚点压缩成新的 `AttackFormulaLoadout`
- `Left Panel` 会把最新 `AttackFormulaLoadout` 编译结果同步到 `Main` 场景里的静态 preview rig；进入 Prefab Mode 调整 [`BackPackAttackPreviewRig.prefab`](Assets/Prefabs/UI/BackPackAttackPreviewRig.prefab) 后，运行时会复用同一套地板、伪玩家、伪敌人编队和正交相机布局
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
- `CharBullet.prefab` 当前也复用了 `BaseCharObject.prefab` 的 `Text/Glyph` 显示契约
  - `glyphText` 默认绑定到 `Text/Glyph`
  - `sizeTarget` 默认绑定到 `Text` 容器，供倍率缩放使用
- `CharBullet.prefab` 当前的显示层级是：
  - `Text/Glyph` 作为主字
  - `Text/GlyphShadow` 作为阴影字
  - `RuneBaseCore` 作为核心底座
  - `RuneBaseResult` 作为结果覆盖纹样
  - `Trail` 作为短拖尾锚点
- `CharBulletVisualPresenter` 当前只负责 secondary visuals
  - 会同步主字和阴影字的文本、颜色与尺寸
  - 会按 `CoreType + ResultType` 解析底座 sprite、覆盖纹样、强调色和拖尾渐变
  - 会给核心底座提供轻微脉冲，给结果覆盖层提供轻微旋转
- `CharBulletVisualLibrary` 当前以 `ScriptableObject` 保存视觉映射
  - `CoreVisualEntry` 按 `AttackCoreType` 配置底座 sprite、fallback tint、基础缩放和拖尾渐变
  - `ResultVisualEntry` 按 `AttackResultType` 配置覆盖纹样 sprite、缩放、透明度、旋转速度和脉冲幅度
- 若 `CompiledAttack` 带有文字颜色覆盖，主字和核心底座会优先使用该颜色
  - 若没有颜色覆盖，则回退到 `CharBulletVisualLibrary` 里该核心的 `fallback tint`
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
  - 当前还会额外生成一个示例连锁件 `Linked/FireDirectChain.asset`，内容为 `FireCore + DirectDamage`，默认 `damageMultiplier = 1.5`
- 可通过 Editor 菜单 `Tools/Lilith/Bullet/Generate Char Bullet Visual Assets` 生成默认的符文底座 sprite 与 `CharBulletVisualLibrary`
  - 当前输出到 `Assets/Art/BulletRunes` 和 `Assets/Data/BulletVisuals`

### 11. 敌人波次、生成与行为

关键文件：

- [`Assets/Scripts/Kernel/Enemy/EnemyGenerator.cs`](Assets/Scripts/Kernel/Enemy/EnemyGenerator.cs)
- [`Assets/Scripts/Kernel/Enemy/WaveManager.cs`](Assets/Scripts/Kernel/Enemy/WaveManager.cs)
- [`Assets/Scripts/Kernel/Enemy/WaveDefinition.cs`](Assets/Scripts/Kernel/Enemy/WaveDefinition.cs)
- [`Assets/Scripts/Kernel/Enemy/Enemy.cs`](Assets/Scripts/Kernel/Enemy/Enemy.cs)
- [`Assets/Scripts/Kernel/Enemy/BaseCharEnemyNorm1.cs`](Assets/Scripts/Kernel/Enemy/BaseCharEnemyNorm1.cs)
- [`Assets/Scripts/Kernel/Enemy/EnemyDefinition.cs`](Assets/Scripts/Kernel/Enemy/EnemyDefinition.cs)
- [`Assets/Scripts/Kernel/Enemy/EnemyDefinitionBinder.cs`](Assets/Scripts/Kernel/Enemy/EnemyDefinitionBinder.cs)
- [`Assets/Scripts/Kernel/Enemy/CharGlyphPresenter.cs`](Assets/Scripts/Kernel/Enemy/CharGlyphPresenter.cs)
- [`Assets/Scripts/Kernel/Enemy/CharEnemyVisualPresenter.cs`](Assets/Scripts/Kernel/Enemy/CharEnemyVisualPresenter.cs)
- [`Assets/Scripts/Kernel/Enemy/EnemyBulletTokenDropper.cs`](Assets/Scripts/Kernel/Enemy/EnemyBulletTokenDropper.cs)
- [`Assets/Scripts/Kernel/Enemy/CharEnemyMovement.cs`](Assets/Scripts/Kernel/Enemy/CharEnemyMovement.cs)
- [`Assets/Scripts/Kernel/Enemy/EnemyMeleeAttacker.cs`](Assets/Scripts/Kernel/Enemy/EnemyMeleeAttacker.cs)
- [`Assets/Scripts/Kernel/Enemy/EnemyRangedTokenAttacker.cs`](Assets/Scripts/Kernel/Enemy/EnemyRangedTokenAttacker.cs)
- [`Assets/Scripts/Kernel/Enemy/EnemySummoner.cs`](Assets/Scripts/Kernel/Enemy/EnemySummoner.cs)
- [`Assets/Data/Enemies`](Assets/Data/Enemies)
- [`Assets/Data/Waves`](Assets/Data/Waves)
- [`Assets/Prefabs/Enemy/CharEnemy.prefab`](Assets/Prefabs/Enemy/CharEnemy.prefab)

当前能力：

- `EnemyGenerator` 当前是纯生成服务
  - 只负责“刷在哪里”和“实例化哪个 `EnemyDefinition`”
  - 不再维护自治随机刷怪循环，也不再持有默认 / 附加 prefab 目录
- 刷怪位置以玩家为圆心，在 `XZ` 平面上按 `spawnDistance` 固定半径随机采样
  - 候选点的 `Y` 会统一落到 `targetMapGrid.WorldPlaneY`
- 每次刷怪会校验候选点是否落在 tag 为 `Ground` 的格子上；若命中非地面或越界区域，会在 `maxGroundSpawnRolls` 次数内继续重 roll
- `EnemyGenerator` 当前同时支持两类显式入口
  - `TrySpawnEnemyAt(...)` 可直接在给定世界坐标生成敌人
  - `TryGetSpawnPositionAround(...)` 可在任意中心点附近抽样一个合法地面出生点，供召唤类行为复用
- 生成器支持手动指定 `targetPlayer`，未指定时会自动查找场景中的 `PlayerPlaneMovement`
- 生成器支持手动指定 `targetMapGrid`，未指定时会自动查找场景中的 `MapGridAuthoring`
- `EnemyGenerator` 当前直接接收 `EnemyDefinition`
  - 会从 `EnemyDefinition.runtimePrefab` 上绑定的 `EnemyDefinitionBinder` 实例化敌人壳
  - 会在生成后调用 `EnemyDefinitionBinder` 把定义写回根节点上的行为与视觉组件
  - 仍会注入当前玩家目标，并同步给 `CharEnemyMovement`、`EnemyMeleeAttacker`、`EnemyRangedTokenAttacker`、`EnemySummoner`
  - 会把同一份 `EnemyWaveConfig` 广播给根节点上的所有 `IEnemyWaveConfigReceiver`
  - 实例化完成后会尝试按敌人的参考 `Collider` 重新 snap 到地图平面，避免 prefab 自身根节点高度与当前地面契约不一致
- `EnemyDefinition` 当前是“敌人种类”的唯一真源
  - 定义稳定 `enemyId`、运行时敌人壳、内建移动方式、内建攻击方式和视觉表现
  - `runtimePrefab` 当前不是裸 `GameObject`，而是一个根节点带 `EnemyDefinitionBinder` 的可生成 prefab 壳；现有 [`CharEnemy.prefab`](Assets/Prefabs/Enemy/CharEnemy.prefab) 可以直接作为这类引用
  - 当前支持 `None / ChaseTarget / ChaseThenDash / KeepDistance / AggroOnHit` 五种移动方式，以及 `None / MeleeContact / RangedBulletToken / SummonEnemy` 四种攻击方式
  - 当前还按敌人类型保存冲刺、风筝、受击仇恨、远程 BulletToken、召唤等专属配置块；这些配置不走 wave override
  - 当前不承载基础生命和移动等通用数值；生命、移动速度、攻击距离、攻击伤害和掉落概率仍全部由 wave 提供
- `WaveDefinition` 当前采用“每波一个 ScriptableObject” 的配置方式
  - 当前每波可配置统一刷怪间隔，以及多个 `enemyDefinition + spawnCount + EnemyWaveConfig` 条目
  - `EnemyWaveConfig` 除了生命、移动和近战数值外，还支持一组 `Bullet Token` 掉落项；每项独立配置 `PlaceableTokenData` 资产和 `0..1` 概率
  - 勾选 `randomizeEnemySpawns` 后，同一波内会按各条目的剩余数量做加权随机抽取；不勾选时仍按 Inspector 中条目顺序依次刷出
- `WaveManager` 当前负责“何时刷、刷多少、这一波是什么参数”
  - 首波自动开始
  - 当前波会按固定间隔刷满配额，并根据 `WaveDefinition` 选择“顺序刷”或“随机刷”
  - 场上敌人清空并等待 `interWaveDelay` 后自动切到下一波
  - 最后一波完成后停止，不循环
- `Enemy` 现在统一暴露名称、移动、生命和攻击只读契约
- `BaseCharEnemyNorm1` 是当前默认的基础文字敌人类型
  - 当前持有运行时 `EnemyDefinition` 绑定与本波 `EnemyWaveConfig`
  - 支持接收 `EnemyWaveConfig` 作为运行时覆写
  - 应用波次配置时不再强制把 `stoppingDistance` 改写为 `attackRange`，避免不同 movement kind 互相覆盖
  - 敌人死亡时会先广播统一死亡通知，再销毁自身，供掉落组件和其他死亡后行为复用
- `BaseCharObject.prefab` 当前提供文字角色的统一显示骨架
  - 根节点挂有 `CharGlyphPresenter`，负责缓存并刷新唯一的 `TMP_Text`
  - 当前显示层级固定为 `Text/Glyph`；`Text` 是容器节点，`Glyph` 才承载实际的 `TMP_Text`
- `CharEnemy.prefab` 和 `CharBullet.prefab` 当前都沿用 `BulletTokenPickup` 风格的字形组织
  - 通过 `CharGlyphPresenter.defaultDisplayText` 覆写默认文字，而不是直接在子节点 `TMP_Text` 上写死展示字符
  - `CharEnemy.prefab` 当前还预挂了 `EnemyDefinitionBinder`，作为定义驱动的运行时壳
  - `CharEnemy.prefab` 当前额外挂有 `CharEnemyVisualPresenter`、`EnemyRangedTokenAttacker`、`EnemySummoner`
  - 当前 prefab 合同固定为 `Text`、`Text/Glyph`、`Collider`、`RuneBaseCore`、`GroundShadow`
  - `Text` 和 `Text/Glyph` 都保持局部 `identity`；`Text` 继续挂 `GameplayBillboard`，负责让主字始终面向主相机
  - `RuneBaseCore` 和 `GroundShadow` 使用 `SpriteRenderer` 平放在 `XZ` 地面，并按 grounded 参考 `BoxCollider` 的底边自动重排
  - `CharEnemyVisualPresenter` 只负责主字高度、底座和地影的布局，不参与移动、碰撞或敌人数值逻辑
  - `EnemyDefinitionBinder` 会根据 `EnemyDefinition` 启停 `CharEnemyMovement`、`EnemyMeleeAttacker`、`EnemyRangedTokenAttacker`、`EnemySummoner`，并把主字与底座 / 地影素材写给 `CharGlyphPresenter` / `CharEnemyVisualPresenter`
- `EnemyBulletTokenDropper` 当前负责敌人的波次掉落逻辑
  - 只缓存当前波次写入的掉落表，不持有敌人数值逻辑
  - 每个掉落项都会在敌人死亡时独立判定，因此单只敌人可以同时掉落多个 token / 连锁件
  - 当前成功判定的 item 会实例化成 [`BulletTokenPickup`](Assets/Scripts/Kernel/Bullet/BulletTokenPickup.cs) 世界拾取物
  - pickup 的根节点高度当前按 `targetMapGrid.WorldPlaneY + pickupHeightOffset` 计算，不再相对敌人 root 高度二次抬升
- `CharEnemyMovement` 当前会按 `EnemyDefinition.MovementKind` 进入统一状态机
  - `ChaseTarget` 和 `AggroOnHit` 当前会通过 [`Assets/Scripts/Kernel/MapGrid/GridPathfinder.cs`](Assets/Scripts/Kernel/MapGrid/GridPathfinder.cs) 计算格子路径，再沿路径点绕开墙体追踪玩家；同一格内才会回退到直接贴近
  - `ChaseThenDash` 会按“追踪 -> 蓄力 -> 冲刺 -> 冷却”循环，并优先复用当前路径方向来生成 dash 朝向
  - `KeepDistance` 在地图格可解析时会沿网格路径接近或撤离玩家：过远时追近到距离带外缘，过近时先寻找可达退让点再绕开墙体后退；只有地图或格子无法解析时才回退到直接位移
  - `AggroOnHit` 会在首次受击前保持静止，受击后切到与 `ChaseTarget` 相同的路径追踪逻辑，只是速度倍率更高
- `CharEnemyMovement` 当前也采用 grounded 根节点规则：
  - 会优先解析 `targetMapGrid` 和 `groundingReferenceCollider`
  - `Awake / OnValidate` 时会按参考 `Collider` 把敌人 root snap 到 `MapGridAuthoring.WorldPlaneY`
  - 挂有 `Rigidbody` 时会统一配置为 `useGravity = false` 并冻结 `Y` 轴位置
- 同时兼容 `Transform` 直驱、`Rigidbody.isKinematic` 和动态 `Rigidbody` 三种移动模式
- 仅当 `Rigidbody` 挂在敌人自身根节点时才使用刚体驱动；若 prefab 误绑到子刚体，会自动回退到 `Transform` 驱动
- 当 `InBackPack`、`InPauseMenu` 或 `Paused` 状态存在时，会立即停止移动并清零动态刚体的平面速度
- `EnemyMeleeAttacker` 当前提供最小近战攻击闭环
  - 只在进入 `attackRange`、冷却结束且玩家仍存活时结算一次 `attackDamage`
  - 当 `InBackPack`、`InPauseMenu` 或 `Paused` 状态存在时，不会继续执行近战攻击判定
  - 当前不包含攻击动画、特效、击退或玩家死亡演出
- `EnemyRangedTokenAttacker` 当前复用玩家同一条 `Compile -> Emit -> CharBullet` 链路
  - 远程攻击公式来自 `EnemyDefinition.RangedBulletAttack`
  - 发射节奏仍然读取 `Enemy.AttackCooldown`
  - 射程仍然读取 `Enemy.AttackRange`
  - 远程子弹默认会使用 `PlayerOnly` 目标策略，但也可以在定义里改为其他策略
- `EnemySummoner` 当前会在攻击距离内按冷却尝试召唤配置好的敌人
  - 召唤落点会复用 `EnemyGenerator.TryGetSpawnPositionAround(...)`
  - 召唤物会复用普通 `EnemyDefinition + EnemyWaveConfig` 初始化流程
  - 召唤配置会在写入前强制清空 `tokenDrops`，因此召唤物不会掉落 `BulletTokenPickup`
  - 召唤物不进入 `WaveManager` 的波次存活统计，因此不会影响清波条件

## 当前未闭环或需要特别注意的点

这些不是“猜测”，而是当前仓库快照里能直接看到的事实：

- [`Assets/Scripts/StartUp.cs`](Assets/Scripts/StartUp.cs) 中很多真正的游戏启动内容还是预留：
  - `GameLoading` 压栈
  - `BuildingDatabase.LoadAllAsync`
  - `ItemDatabase.LoadAllAsync`
  - 其他全局系统初始化
- [`Assets/Scripts/Kernel/UI/StartUpMenuUI.cs`](Assets/Scripts/Kernel/UI/StartUpMenuUI.cs) 的 `Load` / `Option` 当前只提供未实现提示弹窗
  - 还没有真正接入读档流程或设置界面
- 当前仓库中没有看到顶层 Save/Load 管理器
  - 只看到了 `Scribe` 基础设施和 `SaveStatus` 适配器
  - 没看到 `PolymorphRegistry.Register<SaveStatus>(...)` 之类的注册调用
- 当前仓库中没有看到本地化包、Def 数据或 Addressables 组配置资产的明确提交内容
- `StartUp.unity` 当前主要承担启动菜单展示，不再直接承载战斗地图与敌人逻辑
- `Main.unity` 当前不能作为独立入口单场景运行
  - [`Assets/Scripts/StartUp.cs`](Assets/Scripts/StartUp.cs) 会明确要求必须先经过 [`Assets/Scripts/GlobalStartup.cs`](Assets/Scripts/GlobalStartup.cs) 的全局引导
- `StartUp.unity` 中的 `UIRoot` 当前是激活状态

## 常见修改入口

如果 agent 想快速落点，可以按需求直接跳到下面这些位置：

- 若 `Unity MCP` 可用，涉及 Scene / Prefab / Component / Console / Test / Screenshot 的任务，先调用对应 `Unity MCP` 工具，再决定读哪些文件
- 查当前场景真实挂载与对象存在性：`manage_scene(action="get_active")` + `find_gameobjects`
- 查当前组件状态、启用状态和对象层级：`manage_gameobject` + `manage_components`
- 查 Prefab 当前结构、子物体和组件：`manage_prefabs`
- 查编译报错和运行期告警：`read_console`
- 跑 EditMode / PlayMode 测试：`run_tests` + `get_test_job`

- 改启动流程：[`Assets/Scripts/GlobalStartup.cs`](Assets/Scripts/GlobalStartup.cs) + [`Assets/Scripts/StartUp.cs`](Assets/Scripts/StartUp.cs) + [`Assets/Scenes/StartUp.unity`](Assets/Scenes/StartUp.unity) + [`Assets/Scenes/Main.unity`](Assets/Scenes/Main.unity)
- 改游戏状态：[`Assets/Scripts/Kernel/Status.cs`](Assets/Scripts/Kernel/Status.cs) + [`Assets/Scripts/Kernel/StatusController.cs`](Assets/Scripts/Kernel/StatusController.cs)
- 加新 UI Screen：[`Assets/Scripts/Kernel/UI`](Assets/Scripts/Kernel/UI) + [`Assets/Prefabs/UI`](Assets/Prefabs/UI) + [`Assets/Scripts/Vocalith/UI/UIManager.cs`](Assets/Scripts/Vocalith/UI/UIManager.cs)
- 改固定网格 seed 生成、格子替换或 Scene Cell Edit：[`Assets/Scripts/Kernel/MapGridAuthoring.cs`](Assets/Scripts/Kernel/MapGridAuthoring.cs) + [`Assets/Scripts/Kernel/ArenaSeedLayoutBuilder.cs`](Assets/Scripts/Kernel/ArenaSeedLayoutBuilder.cs) + [`Assets/Scripts/Kernel/ArenaSeedMapGenerator.cs`](Assets/Scripts/Kernel/ArenaSeedMapGenerator.cs) + [`Assets/Editor/MapGridEditorUtility.cs`](Assets/Editor/MapGridEditorUtility.cs) + [`Assets/Editor/MapGridAuthoringEditor.cs`](Assets/Editor/MapGridAuthoringEditor.cs)
- 改主相机透视跟随、关键视觉 billboard 或墙体遮挡淡出：[`Assets/Scripts/Kernel/Player/PlayerFollowCamera.cs`](Assets/Scripts/Kernel/Player/PlayerFollowCamera.cs) + [`Assets/Scripts/Kernel/Camera/GameplayBillboard.cs`](Assets/Scripts/Kernel/Camera/GameplayBillboard.cs) + [`Assets/Scripts/Kernel/Camera/CameraOcclusionFader.cs`](Assets/Scripts/Kernel/Camera/CameraOcclusionFader.cs)
- 改统一高度契约或 grounded / floating / projectile 的根节点高度规则：[`Assets/Scripts/Kernel/WorldHeightUtility.cs`](Assets/Scripts/Kernel/WorldHeightUtility.cs) + [`Assets/Scripts/Kernel/MapGridAuthoring.cs`](Assets/Scripts/Kernel/MapGridAuthoring.cs) + [`Assets/Scripts/Kernel/Player/PlayerPlaneMovement.cs`](Assets/Scripts/Kernel/Player/PlayerPlaneMovement.cs) + [`Assets/Scripts/Kernel/Enemy/CharEnemyMovement.cs`](Assets/Scripts/Kernel/Enemy/CharEnemyMovement.cs) + [`Assets/Scripts/Kernel/Enemy/EnemyGenerator.cs`](Assets/Scripts/Kernel/Enemy/EnemyGenerator.cs) + [`Assets/Scripts/Kernel/Enemy/EnemyBulletTokenDropper.cs`](Assets/Scripts/Kernel/Enemy/EnemyBulletTokenDropper.cs)
- 改坐标承载组件或 cell 位移控制：[`Assets/Scripts/Kernel/Cell/CellData.cs`](Assets/Scripts/Kernel/Cell/CellData.cs) + [`Assets/Scripts/Kernel/MapGridCoordinateBinding.cs`](Assets/Scripts/Kernel/MapGridCoordinateBinding.cs)
- 接存档：[`Assets/Scripts/Vocalith/Scribe`](Assets/Scripts/Vocalith/Scribe) + [`Assets/Scripts/Kernel/StatusSaveData.cs`](Assets/Scripts/Kernel/StatusSaveData.cs)
- 改本地化：[`Assets/Scripts/Vocalith/Localization`](Assets/Scripts/Vocalith/Localization)
- 查日志：[`Assets/Scripts/Vocalith/Log`](Assets/Scripts/Vocalith/Log)
- 改玩家输入或平面移动：[`Assets/Scripts/Kernel/Input`](Assets/Scripts/Kernel/Input) + [`Assets/Scripts/Kernel/Player`](Assets/Scripts/Kernel/Player) + [`Assets/Input/Player Controls.cs`](Assets/Input/Player%20Controls.cs)
- 改敌人生成、波次、敌人数据或追踪：[`Assets/Scripts/Kernel/Enemy`](Assets/Scripts/Kernel/Enemy) + [`Assets/Data/Waves`](Assets/Data/Waves) + [`Assets/Prefabs/Enemy/CharEnemy.prefab`](Assets/Prefabs/Enemy/CharEnemy.prefab)
  - 改敌人种类定义或默认敌人资产：[`Assets/Scripts/Kernel/Enemy/EnemyDefinition.cs`](Assets/Scripts/Kernel/Enemy/EnemyDefinition.cs) + [`Assets/Scripts/Kernel/Enemy/EnemyDefinitionBinder.cs`](Assets/Scripts/Kernel/Enemy/EnemyDefinitionBinder.cs) + [`Assets/Data/Enemies`](Assets/Data/Enemies)
  - 改敌人主字贴地、底座或地影布局：[`Assets/Scripts/Kernel/Enemy/CharEnemyVisualPresenter.cs`](Assets/Scripts/Kernel/Enemy/CharEnemyVisualPresenter.cs) + [`Assets/Prefabs/Enemy/CharEnemy.prefab`](Assets/Prefabs/Enemy/CharEnemy.prefab)
- 改文字子弹或文字法术表现：[`Assets/Scripts/Kernel/Bullet`](Assets/Scripts/Kernel/Bullet) + [`Assets/Prefabs/Bullet/CharBullet.prefab`](Assets/Prefabs/Bullet/CharBullet.prefab)
