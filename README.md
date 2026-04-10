# Lilith

Lilith 是一个 Unity 6 原型项目仓库。当前稳定落地的主线是：`StartUp` 启动场景到 `Main` gameplay 场景的双阶段启动链路、`Kernel` 业务层、`Vocalith` 基础设施层、固定网格地图工作流、基于 Token 的攻击编译与发射、敌人波次系统，以及运行时存档与本地化基础能力。

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
  - 负责压入启动菜单，并在完成后切到 `Main`
- [`Assets/Scripts/StartUp.cs`](Assets/Scripts/StartUp.cs)
  - 文件名是 `StartUp.cs`，类名是 `Startup`
  - 位于 `Main` 场景
  - 负责在全局启动完成后压入 [`Assets/Scripts/Kernel/UI/MainUIScreen.cs`](Assets/Scripts/Kernel/UI/MainUIScreen.cs)

### 业务层 `Kernel`

- `Kernel.GameState`：位于 [`Assets/Scripts/Kernel/Status`](Assets/Scripts/Kernel/Status)
  - 入口：[`StatusController.cs`](Assets/Scripts/Kernel/Status/StatusController.cs)
- UI：位于 [`Assets/Scripts/Kernel/UI`](Assets/Scripts/Kernel/UI)
  - 当前主入口包括 `StartUpMenuUI`、`StoryTellerUIScreen`、`MainUIScreen`、`PauseUIScreen`、`BackPackUIScreen`、`PopUpUIScreen`
- 地图与寻路：位于 [`Assets/Scripts/Kernel/MapGrid`](Assets/Scripts/Kernel/MapGrid) 与 [`Assets/Scripts/Kernel/MapGridAuthoring.cs`](Assets/Scripts/Kernel/MapGridAuthoring.cs)
  - 包含固定网格、Seed 布局生成与格子寻路
- 攻击与子弹：位于 [`Assets/Scripts/Kernel/Bullet`](Assets/Scripts/Kernel/Bullet)
  - 入口：[`AttackFormulaCompiler.cs`](Assets/Scripts/Kernel/Bullet/AttackFormulaCompiler.cs)、[`CharBullet.cs`](Assets/Scripts/Kernel/Bullet/CharBullet.cs)
  - Token 数据资产位于 [`Assets/Scripts/Kernel/Bullet/TokenData`](Assets/Scripts/Kernel/Bullet/TokenData)
- 敌人与波次：位于 [`Assets/Scripts/Kernel/Enemy`](Assets/Scripts/Kernel/Enemy)
  - 入口：[`EnemyDefinition.cs`](Assets/Scripts/Kernel/Enemy/EnemyDefinition.cs)、[`EnemyGenerator.cs`](Assets/Scripts/Kernel/Enemy/EnemyGenerator.cs)、[`WaveDefinition.cs`](Assets/Scripts/Kernel/Enemy/WaveDefinition.cs)、[`WaveManager.cs`](Assets/Scripts/Kernel/Enemy/WaveManager.cs)
- 输入、玩家、存档：位于 [`Assets/Scripts/Kernel/Input`](Assets/Scripts/Kernel/Input)、[`Assets/Scripts/Kernel/Player`](Assets/Scripts/Kernel/Player)、[`Assets/Scripts/Kernel/Save`](Assets/Scripts/Kernel/Save)
  - 存档入口：[`RuntimeSaveService.cs`](Assets/Scripts/Kernel/Save/RuntimeSaveService.cs)

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
- 子弹视觉配置：[`Assets/Data/BulletVisuals`](Assets/Data/BulletVisuals)
- 敌人定义：[`Assets/Data/Enemies`](Assets/Data/Enemies)
- 波次定义：[`Assets/Data/Waves`](Assets/Data/Waves)
- 开场剧情文本：[`Assets/Data/Story`](Assets/Data/Story)
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
- [`Assets/Scripts/Kernel/UI/StartUpMenuUI.cs`](Assets/Scripts/Kernel/UI/StartUpMenuUI.cs) 中的 `Load` / `Option` 仍是占位入口；运行时存档服务已经存在，但尚未完整接入启动菜单流程
- [`Assets/Scripts/GlobalStartup.cs`](Assets/Scripts/GlobalStartup.cs) 中的 `LoadAllDefsCoroutine()` 仍是预留加载入口
- 当前没有 `asmdef` / `asmref`，模块边界依赖目录与命名空间约定维护
