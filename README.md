# Lilith

Lilith 是一个 Unity 6 原型项目仓库。当前稳定落地的主线是：`StartUp` 启动场景到 `Main` gameplay 场景的双阶段启动链路、`Kernel` 业务层、`Vocalith` 基础设施层、固定网格地图工作流、`Main` 场景中的起始房间与战斗地图双地图 Run 骨架、基于 Token 的攻击编译与发射、敌人波次系统，以及运行时存档与本地化基础能力。

本 README 只保留当前实现、稳定路径、核心架构、关键入口和已知限制。协作约定、agent 工作流和 Unity MCP 使用规则见 [AGENTS.md](AGENTS.md)。

## 工程事实

- Unity 版本：`6000.3.9f1`
- 渲染管线：URP
- 关键包：`Addressables`、`Input System`、`AI Navigation`、`Newtonsoft Json`、`Unity Test Framework`
- 启动场景：[`Assets/Scenes/StartUp.unity`](Assets/Scenes/StartUp.unity)
- 主 gameplay 场景：[`Assets/Scenes/Main.unity`](Assets/Scenes/Main.unity)
- 当前仓库没有 `asmdef` / `asmref`
  - 运行时代码默认编进 `Assembly-CSharp`
  - Editor 代码默认编进 `Assembly-CSharp-Editor`

## 稳定入口路径

| 路径 | 作用 |
| --- | --- |
| [`Assets/Scripts/GlobalStartup.cs`](Assets/Scripts/GlobalStartup.cs) | `StartUp` 场景的全局启动器 |
| [`Assets/Scripts/StartUp.cs`](Assets/Scripts/StartUp.cs) | `Main` 场景的本地启动器，类名为 `Startup` |
| [`Assets/Scripts/Kernel`](Assets/Scripts/Kernel) | 游戏业务层 |
| [`Assets/Scripts/Vocalith`](Assets/Scripts/Vocalith) | 基础设施层 |
| [`Assets/Scenes`](Assets/Scenes) | 当前稳定场景入口 |
| [`Assets/Prefabs/UI`](Assets/Prefabs/UI) | 业务 UI prefab |
| [`Assets/Prefabs/Enemy`](Assets/Prefabs/Enemy) | 敌人运行时壳与相关 prefab |
| [`Assets/Prefabs/Bullet`](Assets/Prefabs/Bullet) | 子弹与攻击表现 prefab |
| [`Assets/Data`](Assets/Data) | 运行时数据资产与剧情文本 |
| [`Assets/Editor`](Assets/Editor) | Editor 工具 |
| [`Assets/Editor/Test`](Assets/Editor/Test) | EditMode 测试 |
| [`Packages/manifest.json`](Packages/manifest.json) | 包依赖与工具链入口 |

## 核心架构

| 层 | 责任 | 关键路径 |
| --- | --- | --- |
| `Kernel` | 游戏语义、运行时逻辑与具体玩法系统 | [`Assets/Scripts/Kernel`](Assets/Scripts/Kernel) |
| `Vocalith` | UI 栈、日志、本地化、事件、存档等基础设施 | [`Assets/Scripts/Vocalith`](Assets/Scripts/Vocalith) |
| Data / Prefab | 运行时配置、剧情文本、Prefab 真源 | [`Assets/Data`](Assets/Data) + [`Assets/Prefabs`](Assets/Prefabs) |
| Editor / Test | 地图与资产工具、EditMode 测试 | [`Assets/Editor`](Assets/Editor) + [`Assets/Editor/Test`](Assets/Editor/Test) |

当前分层以目录和命名空间约定为主，尚未通过 `asmdef` 做编译级隔离。可以把它理解为：

- `Kernel` 负责“游戏里发生什么”
- `Vocalith` 负责“这些系统依赖什么基础设施运行”
- `Assets/Data` 与 `Assets/Prefabs` 负责运行时配置和资源真源

## 关键模块

### 启动与场景

- [`Assets/Scripts/GlobalStartup.cs`](Assets/Scripts/GlobalStartup.cs)
  - 位于 `StartUp` 场景
  - 负责日志初始化、本地化初始化、`Addressables` 初始化、`Kernel.GameState.StatusController` 初始化
  - 负责压入启动菜单；开始流程会先弹出 [`Assets/Scripts/Kernel/UI/ProfileManagementUIScreen.cs`](Assets/Scripts/Kernel/UI/ProfileManagementUIScreen.cs) 选择四个固定栏位，条目需要先选中、再点一次才会进入；新栏位先进入开场 storyteller，再切到 `Main`；已有栏位直接切到 `Main`
- [`Assets/Scripts/StartUp.cs`](Assets/Scripts/StartUp.cs)
  - 文件名是 `StartUp.cs`，类名是 `Startup`
  - 位于 `Main` 场景
  - 负责在全局启动完成后压入 [`Assets/Scripts/Kernel/UI/MainUIScreen.cs`](Assets/Scripts/Kernel/UI/MainUIScreen.cs)，并在新档首次进入 `Main` 时叠加显示 [`Assets/Scripts/Kernel/UI/DialogUIScreen.cs`](Assets/Scripts/Kernel/UI/DialogUIScreen.cs)（modal）作为开场引导对话
- [`Assets/Scenes/Main.unity`](Assets/Scenes/Main.unity)
  - 当前包含两张并存地图：`StartRoomMapRoot` 作为固定起始房间，`CombatMapRoot` 作为单局战斗地图
  - [`Assets/Scripts/Kernel/MapGrid/MapRunFlowController.cs`](Assets/Scripts/Kernel/MapGrid/MapRunFlowController.cs) 负责玩家初始出生、进入战斗、战斗完成返回起始房间的流转
  - [`Assets/Scripts/Kernel/MapGrid/StartRoomBattleTeleporter.cs`](Assets/Scripts/Kernel/MapGrid/StartRoomBattleTeleporter.cs) 挂在起始房间传送装置上，通过 trigger 进入新一局战斗

### 业务层 `Kernel`

- `Kernel.GameState`：位于 [`Assets/Scripts/Kernel/Status`](Assets/Scripts/Kernel/Status)
  - 入口：[`StatusController.cs`](Assets/Scripts/Kernel/Status/StatusController.cs)
- UI：位于 [`Assets/Scripts/Kernel/UI`](Assets/Scripts/Kernel/UI)
  - 当前主入口包括 `StartUpMenuUI`、`StoryTellerUIScreen`、`DialogUIScreen`、`MainUIScreen`、`PauseUIScreen`、`BackPackUIScreen`、`PopUpUIScreen`、`ProfileManagementUIScreen`、`TokenSelectUIScreen`
- 地图与寻路：位于 [`Assets/Scripts/Kernel/MapGrid`](Assets/Scripts/Kernel/MapGrid) 与 [`Assets/Scripts/Kernel/MapGridAuthoring.cs`](Assets/Scripts/Kernel/MapGridAuthoring.cs)
  - 包含固定网格、双地图 Run flow、Seed 布局生成与格子寻路
  - [`Assets/Scripts/Kernel/ArenaSeedMapGenerator.cs`](Assets/Scripts/Kernel/ArenaSeedMapGenerator.cs) 暴露了边界厚度、障碍数量/尺寸、边缘留白、玩家安全区和刷怪环参数，可用来调节更密或更开的战斗地图
  - [`Assets/Scripts/Kernel/MapGrid/MapSpawnUtility.cs`](Assets/Scripts/Kernel/MapGrid/MapSpawnUtility.cs) 统一处理最近 Ground 格解析与角色传送吸附
  - [`Assets/Scripts/Kernel/MapGrid/MapRunFlowController.cs`](Assets/Scripts/Kernel/MapGrid/MapRunFlowController.cs) 负责 `StartRoomMapRoot` 与 `CombatMapRoot` 之间的单局切换
- 攻击与子弹：位于 [`Assets/Scripts/Kernel/Bullet`](Assets/Scripts/Kernel/Bullet)
  - 入口：[`AttackFormulaCompiler.cs`](Assets/Scripts/Kernel/Bullet/AttackFormulaCompiler.cs)、[`CharBullet.cs`](Assets/Scripts/Kernel/Bullet/CharBullet.cs)
  - Token 数据资产位于 [`Assets/Scripts/Kernel/Bullet/TokenData`](Assets/Scripts/Kernel/Bullet/TokenData)
- 敌人与波次：位于 [`Assets/Scripts/Kernel/Enemy`](Assets/Scripts/Kernel/Enemy)
  - 入口：[`EnemyDefinition.cs`](Assets/Scripts/Kernel/Enemy/EnemyDefinition.cs)、[`EnemyGenerator.cs`](Assets/Scripts/Kernel/Enemy/EnemyGenerator.cs)、[`WaveDefinition.cs`](Assets/Scripts/Kernel/Enemy/WaveDefinition.cs)、[`WaveManager.cs`](Assets/Scripts/Kernel/Enemy/WaveManager.cs)
  - `EnemyDefinition` 当前支持独立的移动槽、攻击槽和多技能槽；移动类型包含追踪、冲刺、风筝、受击仇恨与环绕目标；技能槽按列表顺序调度，支持每槽独立冷却与单帧多技能释放
  - `Main` 场景中的 [`WaveManager`](Assets/Scripts/Kernel/Enemy/WaveManager.cs) 不再依赖启用时自动开打，而是由 `MapRunFlowController` 在玩家进入起始房间传送装置后显式启动
- 输入、玩家、存档：位于 [`Assets/Scripts/Kernel/Input`](Assets/Scripts/Kernel/Input)、[`Assets/Scripts/Kernel/Player`](Assets/Scripts/Kernel/Player)、[`Assets/Scripts/Kernel/Save`](Assets/Scripts/Kernel/Save)
  - 存档入口：[`RuntimeSaveService.cs`](Assets/Scripts/Kernel/Save/RuntimeSaveService.cs)
  - 当前存档使用四个固定栏位：`profile-slot-1.json` 到 `profile-slot-4.json` 分别保存每个栏位的永久数据，`global-mode.json` 保存 `DevMode` / `NormalMode` 与四个栏位的摘要状态

### 基础设施层 `Vocalith`

- UI 基础设施：[`Assets/Scripts/Vocalith/UI`](Assets/Scripts/Vocalith/UI)
  - 入口：[`UIManager.cs`](Assets/Scripts/Vocalith/UI/UIManager.cs)
- 本地化：[`Assets/Scripts/Vocalith/Localization`](Assets/Scripts/Vocalith/Localization)
  - 入口：[`LocalizationManager.cs`](Assets/Scripts/Vocalith/Localization/LocalizationManager.cs)
- 日志：目录是 [`Assets/Scripts/Vocalith/Log`](Assets/Scripts/Vocalith/Log)，命名空间是 `Vocalith.Logging`
- 事件总线：[`Assets/Scripts/Vocalith/Event`](Assets/Scripts/Vocalith/Event)
- 存档底层：[`Assets/Scripts/Vocalith/Scribe`](Assets/Scripts/Vocalith/Scribe)
  - 入口：[`Scribe.cs`](Assets/Scripts/Vocalith/Scribe/Scribe.cs)

## 数据与资源真源

- Bullet Token 数据：[`Assets/Data/BulletTokens`](Assets/Data/BulletTokens)
  - `BulletTokenLibrary.asset` 是 `TokenSelectUIScreen` 使用的可选 token 库
- 子弹视觉配置：[`Assets/Data/BulletVisuals`](Assets/Data/BulletVisuals)
- 敌人定义：[`Assets/Data/Enemies`](Assets/Data/Enemies)
- 波次定义：[`Assets/Data/Waves`](Assets/Data/Waves)
- 开场剧情文本与 Main 场景开场引导对话：[`Assets/Data/Story`](Assets/Data/Story)
- 业务 UI prefab：[`Assets/Prefabs/UI`](Assets/Prefabs/UI)
- 敌人 prefab：[`Assets/Prefabs/Enemy`](Assets/Prefabs/Enemy)
- 子弹 prefab：[`Assets/Prefabs/Bullet`](Assets/Prefabs/Bullet)

## 命名说明

当前仓库里有几处容易混淆但实际不同的命名，本文统一按真实文件与类型书写：

- `StartUp`：场景名与脚本文件名，例如 [`Assets/Scenes/StartUp.unity`](Assets/Scenes/StartUp.unity) 和 [`Assets/Scripts/StartUp.cs`](Assets/Scripts/StartUp.cs)
- `Startup`：[`Assets/Scripts/StartUp.cs`](Assets/Scripts/StartUp.cs) 中定义的 `Main` 场景本地启动器类
- `Kernel.GameState`：命名空间；对应磁盘目录是 [`Assets/Scripts/Kernel/Status`](Assets/Scripts/Kernel/Status)
- `Vocalith.Logging`：命名空间；对应磁盘目录是 [`Assets/Scripts/Vocalith/Log`](Assets/Scripts/Vocalith/Log)

## 已知限制

- [`Assets/Scenes/Main.unity`](Assets/Scenes/Main.unity) 不能作为独立入口直接运行；当前必须先经过 [`Assets/Scenes/StartUp.unity`](Assets/Scenes/StartUp.unity) 中的 [`GlobalStartup`](Assets/Scripts/GlobalStartup.cs) 交接
- [`Assets/Scripts/Kernel/UI/StartUpMenuUI.cs`](Assets/Scripts/Kernel/UI/StartUpMenuUI.cs) 中的 `Option` 仍是占位入口
- [`Assets/Scripts/GlobalStartup.cs`](Assets/Scripts/GlobalStartup.cs) 中的 `LoadAllDefsCoroutine()` 仍是预留加载入口
- 当前没有 `asmdef` / `asmref`，模块边界依赖目录与命名空间约定维护
