# Lilith

Lilith 是一个 Unity 6 原型项目仓库。当前稳定落地的主线是：`StartUp` 启动场景到 `Main` gameplay 场景的双阶段启动链路、`Kernel` 业务层、`Vocalith` 基础设施层、固定网格地图工作流、`Main` 场景中的起始房间与战斗地图双地图 Run 骨架、基于 Token 的攻击编译与发射、敌人波次系统，以及运行时存档与本地化基础能力。

本 README 只保留当前实现、稳定路径、核心架构、关键入口和已知限制。仓库硬规则见 [AGENTS.md](AGENTS.md)；更细的 agent 操作流程、委派策略与文档分工由 repo-local [`lilith-repo-operator`](.codex/skills/lilith-repo-operator/SKILL.md) skill 承载。

## 工程事实

- Unity 版本：`6000.3.9f1`
- 渲染管线：URP
- 关键包：`Addressables`、`Input System`、`AI Navigation`、`Newtonsoft Json`、`Unity Test Framework`
- 运行时不再强制固定宽高比；分辨率 / 全屏设置通过 Options 保存，并在启动时应用
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
  - [`Assets/Scripts/Kernel/MapGrid/MapRunFlowController.cs`](Assets/Scripts/Kernel/MapGrid/MapRunFlowController.cs) 负责玩家初始出生、进入战斗、战斗结束后弹出结算，再在关闭结算后返回起始房间的流转；起始房间传送装置触发后，会先按控制器上单独配置的初始 `CombatEntryTokenSelectionPlan` 打开一次 [`Assets/Scripts/Kernel/UI/TokenSelectUIScreen.cs`](Assets/Scripts/Kernel/UI/TokenSelectUIScreen.cs)，写入玩家选中的 token 后再启动第一波；普通波次结束后的 token 抽取计划由 [`WaveSequenceProgressionConfig`](Assets/Scripts/Kernel/Enemy/WaveSequenceProgressionConfig.cs) 按“第 x 波”统一驱动，`WaveManager` 在波次清空后暂停推进并打开同一套选择界面，完成后继续下一波或结束整场战斗
  - [`Assets/Scripts/Kernel/MapGrid/StartRoomBattleTeleporter.cs`](Assets/Scripts/Kernel/MapGrid/StartRoomBattleTeleporter.cs) 挂在起始房间传送装置上，通过 trigger 请求进入新一局战斗；在 `tutorial_enter_teleporter` 完成后，每次成功进入 teleporter 都会在记录 `tutorial.teleporter_triggered_once` 后补发 `InitCore` 到背包
  - `StartRoomMapRoot/Book` 的 trigger 会打开永久升级界面 [`Assets/Scripts/Kernel/UI/UpdateUIScreen.cs`](Assets/Scripts/Kernel/UI/UpdateUIScreen.cs)

### 业务层 `Kernel`

- `Kernel.GameState`：位于 [`Assets/Scripts/Kernel/Status`](Assets/Scripts/Kernel/Status)
  - 入口：[`StatusController.cs`](Assets/Scripts/Kernel/Status/StatusController.cs)
- UI：位于 [`Assets/Scripts/Kernel/UI`](Assets/Scripts/Kernel/UI)
  - 当前主入口包括 `StartUpMenuUI`、`OptionsUIScreen`、`StoryTellerUIScreen`、`DialogUIScreen`、`MainUIScreen`、`PauseUIScreen`、`BackPackUIScreen`、`HintUIScreen`、`PopUpUIScreen`、`ProfileManagementUIScreen`、`TokenSelectUIScreen`、`UpdateUIScreen`、`SettlementUIScreen`
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
  - `EnemyDefinition` 当前支持独立的移动槽、攻击槽、技能槽与基础战斗数值；移动类型包含追踪、冲刺、风筝、受击仇恨、环绕目标、Boss 主动游走与“跟随最近敌人并保持距离”，攻击类型包含接触近战、远程 Token 子弹与近距自爆；技能类型除召唤外新增了 Boss 一阶段可用的 `DelayedGroundBomb`（玩家脚下延时爆炸与范围红圈预警），并引入通用技能行动锁使敌人释放技能期间不触发平A；远程子弹现支持 Homing 追踪与 Healing 命中结算，并支持敌人侧子弹速度倍率覆写；当前普通敌人原型统一收敛为 `群 / 迅 / 甲 / 召 / 爆 / 弦 / 锁 / 愈`，并新增双阶段 Boss 定义 `Boss_Phase1 / Boss_Phase2`
  - `WaveDefinition` 当前负责刷怪组合、Boss 波次开关与 Boss 元数据（Boss 显示名、二阶段定义、血量阈值）；普通波次掉落与波后 `CombatEntryTokenSelectionPlan` 由 [`WaveSequenceProgressionConfig`](Assets/Scripts/Kernel/Enemy/WaveSequenceProgressionConfig.cs) 按“第 x 波”统一驱动，`WaveDefinition` 仅保留条目级额外掉落扩展位；Boss 波次只走自身内部掉落（及可选自身波后计划），不参与普通波次映射；敌人数值真源位于 [`EnemyDefinition`](Assets/Scripts/Kernel/Enemy/EnemyDefinition.cs)
  - `Main` 场景中的 [`WaveManager`](Assets/Scripts/Kernel/Enemy/WaveManager.cs) 不再依赖启用时自动开打，而是由 `MapRunFlowController` 在玩家进入起始房间传送装置后显式启动；每波清空后若解析到了波后计划，会先请求一轮 token 选择，再恢复波次推进；同一场战斗内每清完一波，`WaveManager` 会把 `completedWaveCount` 同步给 `EnemyGenerator`，并按 `1 + 0.04 * completedWaveCount` 对敌人的战力向数值做统一成长；Boss 条目会触发 Boss 生命周期事件，并可在运行时按血量阈值切换到二阶段定义
- 输入、玩家、存档：位于 [`Assets/Scripts/Kernel/Input`](Assets/Scripts/Kernel/Input)、[`Assets/Scripts/Kernel/Player`](Assets/Scripts/Kernel/Player)、[`Assets/Scripts/Kernel/Save`](Assets/Scripts/Kernel/Save)
  - 存档入口：[`RuntimeSaveService.cs`](Assets/Scripts/Kernel/Save/RuntimeSaveService.cs)
  - `UIInputRouter` 当前支持 `Hint(Tab)`：在 `MainUIScreen` 与 `BackPackUIScreen` 上开关 [`Assets/Scripts/Kernel/UI/HintUIScreen.cs`](Assets/Scripts/Kernel/UI/HintUIScreen.cs)；背包顶部 Hint 按钮与 Tab 走同一条路由
  - `HintUIScreen` 打开时会进入 `InHint` 状态，并与背包一致阻断玩家战斗输入和敌人行为
  - 当前存档使用四个固定栏位：`profile-slot-1.json` 到 `profile-slot-4.json` 分别保存每个栏位的永久数据，`global-mode.json` 保存 `DevMode` / `NormalMode` 与四个栏位的摘要状态

### 基础设施层 `Vocalith`

- UI 基础设施：[`Assets/Scripts/Vocalith/UI`](Assets/Scripts/Vocalith/UI)
  - 入口：[`UIManager.cs`](Assets/Scripts/Vocalith/UI/UIManager.cs)，负责持久化 UI 栈、EventSystem 收敛、根 CanvasScaler UI 缩放与固定尺寸 LayoutGroup 自适应，不再强制 16:9 视口
- 音频基础设施：[`Assets/Scripts/Vocalith/Audio`](Assets/Scripts/Vocalith/Audio)
  - 入口：[`AudioManager.cs`](Assets/Scripts/Vocalith/Audio/AudioManager.cs)，负责持久化单例、BGM crossfade、SFX 播放池与 Master/Music/SFX 三路音量
- 本地化：[`Assets/Scripts/Vocalith/Localization`](Assets/Scripts/Vocalith/Localization)
  - 入口：[`LocalizationManager.cs`](Assets/Scripts/Vocalith/Localization/LocalizationManager.cs)
- 日志：目录是 [`Assets/Scripts/Vocalith/Log`](Assets/Scripts/Vocalith/Log)，命名空间是 `Vocalith.Logging`
- 事件总线：[`Assets/Scripts/Vocalith/Event`](Assets/Scripts/Vocalith/Event)
- 存档底层：[`Assets/Scripts/Vocalith/Scribe`](Assets/Scripts/Vocalith/Scribe)
  - 入口：[`Scribe.cs`](Assets/Scripts/Vocalith/Scribe/Scribe.cs)

## 数据与资源真源

- Bullet Token 数据：[`Assets/Data/BulletTokens`](Assets/Data/BulletTokens)
  - 当前起始房间初始抽取默认通过 [`Assets/Data/BulletTokens/SelectionPlans/StartRoomCombatEntryTokenSelectionPlan.asset`](Assets/Data/BulletTokens/SelectionPlans/StartRoomCombatEntryTokenSelectionPlan.asset) 指向 [`Assets/Data/BulletTokens/TokenLib/CoreOnly_Token_Lib.asset`](Assets/Data/BulletTokens/TokenLib/CoreOnly_Token_Lib.asset) 作为可选 token 库
  - BulletToken 现已支持四种 Core 的二级效果，以及 Bounce / Pierce / Spread / Homing / Explosion / Split / Control / Healing 等行为与结果词
  - [`Assets/Data/BulletTokens/SelectionPlans`](Assets/Data/BulletTokens/SelectionPlans) 保存可复用的 token 抽取计划；`Main` 场景中的 [`MapRunFlowController`](Assets/Scripts/Kernel/MapGrid/MapRunFlowController.cs) 可单独引用一份“初始抽取”计划，普通波次的“波后抽取”则由 [`Assets/Data/Waves/NonBossWaveSequenceProgression.asset`](Assets/Data/Waves/NonBossWaveSequenceProgression.asset) 按波次映射配置
- 子弹视觉配置：[`Assets/Data/BulletVisuals`](Assets/Data/BulletVisuals)
- 敌人定义：[`Assets/Data/Enemies`](Assets/Data/Enemies)
  - `EnemyDefinition` 资产持有敌人的行为开关、基础战斗数值、远程/自爆配置与技能槽；当前包含 8 个普通敌人原型与双阶段 Boss 定义 `Boss_Phase1`、`Boss_Phase2`
- 波次定义：[`Assets/Data/Waves`](Assets/Data/Waves)
  - `WaveDefinition` 资产声明每波会刷哪些 `EnemyDefinition`、刷多少只、可选条目级额外掉落、Boss 波次标记与 Boss 元数据；普通波次掉落与波后抽取映射由 [`Assets/Data/Waves/NonBossWaveSequenceProgression.asset`](Assets/Data/Waves/NonBossWaveSequenceProgression.asset) 维护，Boss 波次使用自身 `WaveDefinition` 内部配置；当前 `Main` 场景默认串联 `Wave01` 到 `Wave06`，其中 `Wave01` 到 `Wave05` 为普通波次，`Wave06` 为 Boss 波次
- 开场剧情文本与 Main 场景开场引导对话：[`Assets/Data/Story`](Assets/Data/Story)
- 永久升级目录：[`Assets/Data/Upgrades`](Assets/Data/Upgrades)
- UI 文案目录：[`Assets/Data/UI`](Assets/Data/UI)
  - [`Assets/Data/UI/OptionsCatalog.json`](Assets/Data/UI/OptionsCatalog.json) 负责设置界面分类和条目配置
  - [`Assets/Data/UI/SettlementPresentationCatalog.json`](Assets/Data/UI/SettlementPresentationCatalog.json) 负责结算文案
  - [`Assets/Data/UI/HintCatalog.json`](Assets/Data/UI/HintCatalog.json) 负责 Hint 的手工帮助条目（敌人图鉴正文由 `EnemyDefinition.Description` 提供）
- 业务 UI prefab：[`Assets/Prefabs/UI`](Assets/Prefabs/UI)
- 敌人 prefab：[`Assets/Prefabs/Enemy`](Assets/Prefabs/Enemy)
- 子弹 prefab：[`Assets/Prefabs/Bullet`](Assets/Prefabs/Bullet)

## 协作文档

| 文档 | 面向对象 | 用途 |
| --- | --- | --- |
| [`Docs/ArtistGuide.md`](Docs/ArtistGuide.md) | 美术 | 替换临时美术资源、确认场景 / prefab / UI 视觉入口与注意事项 |
| [`Docs/DesignGuide.md`](Docs/DesignGuide.md) | 策划 | 编写游戏内文本、任务、敌人、波次、Token、掉落和数值配置 |
| [`Docs/AudioGuide.md`](Docs/AudioGuide.md) | 音频 / 美术 / 策划 | 导入音乐与音效、设置 Addressable 地址、向程序交付播放需求 |

## 命名说明

当前仓库里有几处容易混淆但实际不同的命名，本文统一按真实文件与类型书写：

- `StartUp`：场景名与脚本文件名，例如 [`Assets/Scenes/StartUp.unity`](Assets/Scenes/StartUp.unity) 和 [`Assets/Scripts/StartUp.cs`](Assets/Scripts/StartUp.cs)
- `Startup`：[`Assets/Scripts/StartUp.cs`](Assets/Scripts/StartUp.cs) 中定义的 `Main` 场景本地启动器类
- `Kernel.GameState`：命名空间；对应磁盘目录是 [`Assets/Scripts/Kernel/Status`](Assets/Scripts/Kernel/Status)
- `Vocalith.Logging`：命名空间；对应磁盘目录是 [`Assets/Scripts/Vocalith/Log`](Assets/Scripts/Vocalith/Log)

## 已知限制

- [`Assets/Scenes/Main.unity`](Assets/Scenes/Main.unity) 不能作为独立入口直接运行；当前必须先经过 [`Assets/Scenes/StartUp.unity`](Assets/Scenes/StartUp.unity) 中的 [`GlobalStartup`](Assets/Scripts/GlobalStartup.cs) 交接
- [`Assets/Scripts/Kernel/UI/OptionsUIScreen.cs`](Assets/Scripts/Kernel/UI/OptionsUIScreen.cs) 当前负责按 JSON 生成设置 UI，并在 `Apply` 时把暂存控件值写入 `PlayerPrefs`；按键项会通过 Input System binding override 保存，显示项中的分辨率 / 全屏会通过 `Kernel.Display.LilithDisplaySettings` 调用 `Screen.SetResolution` 应用并在下次启动恢复，UI 缩放会通过 `UIManager` 根 CanvasScaler 应用，音频音量会通过 `Vocalith.Audio.AudioManager` 应用；首版尚未配置实际音乐资源和玩法音效触发点
- [`Assets/Scripts/GlobalStartup.cs`](Assets/Scripts/GlobalStartup.cs) 中的 `LoadAllDefsCoroutine()` 仍是预留加载入口
- 当前没有 `asmdef` / `asmref`，模块边界依赖目录与命名空间约定维护
