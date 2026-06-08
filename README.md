# Lilith

Lilith 是一个 Unity 6 原型项目仓库。当前稳定落地的主线是：`StartUp` 启动场景到 `Main` gameplay 场景的双阶段启动链路、`Kernel` 业务层、`Vocalith` 基础设施层、固定网格地图工作流、`Main` 场景中的起始房间与战斗地图双地图 Run 骨架、基于 Token 的攻击编译与发射、敌人波次系统，以及运行时存档与本地化基础能力。

本 README 只保留当前实现、稳定路径、核心架构、关键入口和已知限制。仓库硬规则见 [AGENTS.md](AGENTS.md)；更细的 agent 操作流程、委派策略与文档分工由 repo-local [`lilith-repo-operator`](.codex/skills/lilith-repo-operator/SKILL.md) skill 承载。Codex repo-local hooks 配置见 [`.codex/hooks.json`](.codex/hooks.json)，当前 hooks 会在每次提交用户消息时注入“允许使用 `gpt-5.4-mini` / `gpt-5.3-codex-spark` 分担边界清晰子任务”的委派偏好，在计划模式下注入“有不确定决策就直接询问用户”的提醒，并要求最终回复明确 Memory Consistency Pass 以及 `README.md`、`memory.md`、`AGENTS.md` 的更新评估。

## 工程事实

- Unity 版本：`6000.3.9f1`
- 渲染管线：URP
- 关键包：`Addressables`、`Input System`、`AI Navigation`、`Newtonsoft Json`、`Unity Test Framework`
- 分辨率 / 全屏 / 垂直同步设置通过 Options 保存，并在启动时应用；目标帧率不可配置，默认上限为 360 FPS，显示器刷新率高于 360 时以显示器刷新率为准
- 启动场景：[`Assets/Scenes/StartUp.unity`](Assets/Scenes/StartUp.unity)
- 主 gameplay 场景：[`Assets/Scenes/Main.unity`](Assets/Scenes/Main.unity)
- 当前仓库使用保守 `asmdef` 分层；项目脚本不再默认编进 `Assembly-CSharp` / `Assembly-CSharp-Editor`
  - `Lilith.Input`：Input System 生成的 `PlayerControls` / `UIControls`
  - `Lilith.Vocalith`：基础设施层
  - `Lilith.Kernel`：游戏业务层
  - `Lilith.Bootstrap`：`StartUp` / `Main` 场景启动交接入口
  - `Lilith.Editor`：Editor 工具
  - `Lilith.Tests.EditMode`：EditMode 测试

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

当前 `asmdef` 只做保守分层，不按 gameplay 子模块细拆。可以把它理解为：

- `Kernel` 负责“游戏里发生什么”
- `Vocalith` 负责“这些系统依赖什么基础设施运行”
- `Bootstrap` 负责“启动场景如何把流程交给游戏层”，通过 `Kernel.StartupFlowBridge` 让启动 UI 请求进入 `GlobalStartup`
- `Input` 负责“Input System 生成代码供游戏层引用”
- `Assets/Data` 与 `Assets/Prefabs` 负责运行时配置和资源真源

## 关键模块

### 启动与场景

- [`Assets/Scripts/GlobalStartup.cs`](Assets/Scripts/GlobalStartup.cs)
  - 位于 `StartUp` 场景
  - 负责日志初始化、本地化初始化、`Addressables` 初始化、`Kernel.GameState.StatusController` 初始化
  - 负责压入启动菜单；点击 `Start` 会直接在最小空存档槽位创建新档，并先把 Loading Panel 后台压栈，在开场 storyteller 播放期间并行加载数据，若剧情结束时仍未完成则继续显示 Loading Panel 等待；点击 `Load` 才会打开 [`Assets/Scripts/Kernel/UI/ProfileManagementUIScreen.cs`](Assets/Scripts/Kernel/UI/ProfileManagementUIScreen.cs) 按最近打开时间展示已有存档，条目需要先选中、再点一次才会加载并切到 `Main`
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
  - `MainUI.prefab` 预埋隐藏的 `ObjectiveArrowView`，后续系统可通过 `MainUIScreen.ObjectiveArrowView.Bind(camera, target)` 显示世界目标箭头；当前不绑定具体玩法目标，HUD 隐藏时会清空目标
  - `MainUI.prefab` 预埋隐藏的 `Notification Panel`；玩家成功拾取场景 Token / 物品、Token Select 接受 Token / 法术书奖励或教程补发 Token 后，会通过 `RewardNotificationEvent` 在 HUD 上显示标题与描述，约 2 秒后自动淡出。当前图片位保留但隐藏，尚不扩展 Token / 法术书资产 icon 字段
- 地图与寻路：位于 [`Assets/Scripts/Kernel/MapGrid`](Assets/Scripts/Kernel/MapGrid) 与 [`Assets/Scripts/Kernel/MapGridAuthoring.cs`](Assets/Scripts/Kernel/MapGridAuthoring.cs)
  - 包含固定网格、双地图 Run flow、Seed 布局生成与格子寻路
  - [`Assets/Scripts/Kernel/ArenaSeedMapGenerator.cs`](Assets/Scripts/Kernel/ArenaSeedMapGenerator.cs) 暴露了边界厚度、障碍数量/尺寸、边缘留白、玩家安全区和刷怪环参数，可用来调节更密或更开的战斗地图
  - [`Assets/Scripts/Kernel/MapGrid/Rooms`](Assets/Scripts/Kernel/MapGrid/Rooms) 是独立的调试版房间地图系统：`RoomGraphGenerator` 维护本局房间连接，`GeneratedRoomMapRoot` 上的 `ProceduralRoom2DDebugBootstrap` 运行时统一调用 `ProceduralRoomMapDebugController` 生成当前 Tilemap 房间，并把 [`Assets/Prefabs/Player/Test2DCharacterSprite.prefab`](Assets/Prefabs/Player/Test2DCharacterSprite.prefab) 生到第一个 `P` 入口格中心；2D 调试玩家使用 [`Assets/Scripts/Kernel/Player/Player2DMovementController.cs`](Assets/Scripts/Kernel/Player/Player2DMovementController.cs) 做 XY 平面移动/冲刺/鼠标朝向，`Main Camera` 上的 [`Assets/Scripts/Kernel/Player/Player2DIsometricCamera.cs`](Assets/Scripts/Kernel/Player/Player2DIsometricCamera.cs) 在运行时接管为沿 Z 轴的透视跟随镜头；当前仍不接入传送门、波次、敌人、攻击、拾取或正式 run flow，旧 3D 地图与 `MapRunFlowController` 链路仍保留
  - [`Assets/Scripts/Kernel/MapGrid/MapSpawnUtility.cs`](Assets/Scripts/Kernel/MapGrid/MapSpawnUtility.cs) 统一处理最近 Ground 格解析与角色传送吸附
  - [`Assets/Scripts/Kernel/MapGrid/MapRunFlowController.cs`](Assets/Scripts/Kernel/MapGrid/MapRunFlowController.cs) 负责 `StartRoomMapRoot` 与 `CombatMapRoot` 之间的单局切换
- 攻击与子弹：位于 [`Assets/Scripts/Kernel/Bullet`](Assets/Scripts/Kernel/Bullet)
  - 入口：[`SpellBookData.cs`](Assets/Scripts/Kernel/Bullet/SpellBookData.cs)、[`SpellBookLoadout.cs`](Assets/Scripts/Kernel/Bullet/SpellBookLoadout.cs)、[`SpellProgram.cs`](Assets/Scripts/Kernel/Bullet/SpellProgram.cs)、[`SpellProgramCompiler.cs`](Assets/Scripts/Kernel/Bullet/SpellProgramCompiler.cs)、[`SpellProjectileCompiler.cs`](Assets/Scripts/Kernel/Bullet/SpellProjectileCompiler.cs)、[`AttackCompilation.cs`](Assets/Scripts/Kernel/Bullet/AttackCompilation.cs)、[`SpellDescriptionGenerator.cs`](Assets/Scripts/Kernel/Bullet/SpellDescriptionGenerator.cs)、[`CharBullet.cs`](Assets/Scripts/Kernel/Bullet/CharBullet.cs)
  - 玩家侧当前以 `SpellBookLoadout` 作为法术真源：法术书定义槽位、冷却、每次激活次数、激活扇形、可选能量门槛、执行器原生 modifier 和常驻 token；背包 UI 编辑 `EquippedItems`，玩家发射、HUD 与描述使用合并常驻 token 后的 `ExecutionItems`
  - `SpellBookLoadout` 是当前玩家法术装备与编译缓存真源，只公开 `CompiledSpellProgram` 编译结果；旧线性构筑会被 `SpellProgramCompiler` 包装为一个外层 `SpellCastBlock` 与一个 primary `SpellProjectileNode`；`SpellProjectileNode` 已携带效果、爆炸和视觉修饰快照，SpellProgram 与旧线性 emitter 发射路径初始化运行时子弹时只传 `SpellProjectileNode`，运行时不再暴露 `CurrentCompiledAttack`；`CompiledSpellProgram` 不再公开 `PrimaryCompiledAttack`，`SpellProjectileNode` 也不再持有 `AdapterAttack`
  - `ModifierTokenData` 是新 modifier 词元入口，复用 `TokenModifierDefinition` 配置数值载荷，但普通 modifier 不再在资产上写死作用域；编译器会按摆放位置把它解析成 `NextToken`、`NextN` 或 `CurrentBlock`，并遵守“只向后解析、就近最小作用域、payload 内不泄漏到外层”的规则。`Modifier + Value + ...` 会优先把紧随其后的 Value 解释成 `NextN` 目标数；`Modifier + Multicast + ...` 会把 modifier 绑定到这个 upcoming CastBlock；普通 modifier 不会自动推断为 `GlobalProgram`。result-only payload 继续支持用 `ImpactRadiusMultiplier` 修饰 Explosion / Healing / Control / Leave / Push / Pull 范围，并用 `ResultCount` / `ResultDuration` / `ResultMultiplier` 修饰 Split 数量、Control 阈值/持续、Explosion 延迟/伤害倍率、Healing 治疗倍率，以及 Drain / Shield / Push / Pull 强度；`PayloadAmplifyModifier`、`PayloadRadiusModifier`、`PayloadCountModifier` 与 `PayloadControlFieldModifier` 现在只是内容命名上的 payload 样本，不再依赖资产硬编码 `CurrentPayload`
  - `MulticastTokenData` 是当前 CastBlock 的多节点入口：第一个 Multicast 会从右侧收集固定数量 projectile segment，编译成同一个 outer `SpellCastBlock` 下的多个 `SpellProjectileNode`；`SpellCastBlock.CastPattern` 支持同时、顺序、分叉与环绕，其中 `绕` 已实现 2 段主从环绕发射；当前仍不开放 Multicast 消费 Value、wrapping、result-only 节点或嵌套 Multicast，右侧不足会保留 warning
  - `AttackProjectileEmitter` 只接收 `CompiledSpellProgram`，会逐个执行 `PrimaryCastBlock.Projectiles`，并以 `SpellProjectileNode` 决定可发射性、散射数量和散射角；玩家发射会把当前法术书的冷却、每次激活次数、激活扇形和可选能量消耗落到 runtime，玩家与敌人远程绑定法术书时都会通过 `SpellProgramCompiler.Compile(..., spellBook)` 应用不占槽的执行器原生 modifier；当前这些 modifier 覆盖外层 projectile、多播 projectile、payload 内带 Core 的 projectile，并会把 `ImpactRadiusMultiplier` / `ResultCount` / `ResultDuration` / `ResultMultiplier` 映射到 result-only payload effect；`Damage` 目标通过外层 projectile 的 base damage 流入 payload 伤害，不在 effect multiplier 上重复应用；旧 `CompiledAttack` 不再有 emitter 重载，也不再被注入 `CharBullet`；`CharBullet.InitializeShot` 现在只接收 `SpellProjectileNode` 运行时语义快照；`CharBullet` 在 SpellProgram 路径优先读取 `SpellProjectileNode` 解析 Homing/Bounce、爆炸、分裂、控制、治疗和视觉表现，因此玩家法术书与敌人远程 token 攻击都走同一条 SpellProgram 发射路径
  - `TriggerTokenData` 是当前 Trigger/Payload 入口：Trigger 后的后续 token 会自动构成 payload，非 Multicast 公式中一直消费到整式末尾；Multicast 内 Trigger 只对当前 projectile segment 生效，一旦进入 payload 会吞掉该 segment 后续 token，不再留给兄弟 outer projectile，兄弟段不足会保留 warning。当前 Trigger 类型覆盖 `OnHit`、`OnTimer`、`OnExpire`、`OnKill`、`OnDistance`、`OnProximity`；`时/程/近` 这类参数化 Trigger 会优先消费紧随其后的 Value 作为时间、距离或半径参数，`OnTimer` / `OnDistance` / `OnProximity` / `OnExpire` / `OnKill` 对同一个 payload 默认只触发一次。payload 可包含普通 inner projectile 或 result-only `SpellPayloadEffectNode`；runtime 覆盖命中、计时、距离、接近、消失和击杀触发后的 Explosion、StatusEffect/Control、Healing、Split、Drain、Shield、Leave、Push 与 Pull；不再支持显式 `PayloadStart/PayloadEnd` 边界 token
  - `ValueTokenData` 当前支持普通数值、倍率和规模预设三类模式，并通过消费者 token 声明的 `SpellValueParameterKind` 解释为 Count、Radius、Duration、Strength 或 TriggerParameter；Count 类参数四舍五入且最小为 1，允许 0 的半径/持续/特殊参数由消费者声明。Behavior 的 Spread/Bounce/Pierce 与 Result 的 Split/Control 保留旧 Count 兼容，Explosion 与 Healing 资产已开启 Radius 消费，Drain / Shield / Push / Pull 使用 Strength，Leave 使用 Duration，因此 `爆 + 三` 会把爆炸半径设为 3，`汲 + 三` 会把汲取强度设为 3，`留 + 五` 会把残留场持续时间设为 5 秒
  - 当前构筑系统的稳定设计说明见 [`Docs/SpellConstructionSystemDesign.md`](Docs/SpellConstructionSystemDesign.md)，常见摆法、槽位类别、warning 边界和运行效果见 [`Docs/SpellConstructionUseCases.md`](Docs/SpellConstructionUseCases.md)
  - `CharBullet` 现在会保存当前 `SpellProjectileNode` 的 payload 列表和运行时语义快照；命中 actor 后执行 OnHit payload，非 Bounce 环境命中也会执行 payload，并用最大 payload 深度 / 派生 projectile 数限制防止递归失控；Split 与 result-only Split payload 派生出的子弹会降级为无 payload 的 DirectDamage 子弹，并继续带自己的 `SpellProjectileNode`；Healing result-only payload 在范围为 0 时保持单体治疗，带 Radius 值词或半径 modifier 时会在命中点做范围治疗；`汲` 会按直伤比例治疗 owner，`护` 会给 owner 添加 6 秒吸收盾，`留` 会在命中点生成 tick 伤害/核心状态场，`斥/吸` 会按敌人位移重量阈值做一次水平推拉
  - `BackPackAttackPreviewController` 现在只接收 `CompiledSpellProgram` 并缓存 primary `SpellProjectileNode`，会预览 `PrimaryCastBlock.Projectiles` 中的所有外层 projectile，并直接从 projectile node 解析预览目标层、血量和爆炸范围；它会保留 Trigger/Payload 载荷而不在刷新预览时直接执行内层 payload
  - Token Select 与背包格已识别新体系 token 类型：Modifier / Multicast / Trigger 会显示独立目录与颜色；payload 是 Trigger 后的结构语义，不再作为独立可选 token 类型。后段波后奖励的 `Plan2` 已接入 [`SpellProgram_Token_Lib.asset`](Assets/Data/BulletTokens/TokenLib/SpellProgram_Token_Lib.asset)，也能通过 [`SpellBookReward_Lib.asset`](Assets/Data/SpellBooks/SpellBookReward_Lib.asset) 抽到法术书奖励
  - Run 内奖励现在用 `RunRewardOption` 同时表达 token 与法术书；`TokenSelectUIScreen` 可混合展示 token 库和法术书库，`MapRunFlowController` 在玩家选择法术书奖励时会替换玩家 `SpellBookLoadout.SpellBook`
  - `Main` 场景玩家已绑定默认法术书资产 [`Assets/Data/SpellBooks/ApprenticeSpellBook.asset`](Assets/Data/SpellBooks/ApprenticeSpellBook.asset)，玩家发射与 Run reset 不再回退旧 `AttackFormulaLoadout`；旧 `AttackFormulaLoadout` 脚本与 `Main` / `_Recovery` 场景中的历史组件序列化残留已删除
  - 敌人远程 token 攻击也已支持法术书执行器：`EnemyDefinition.RangedBulletAttackDefinition.spellBook` 可选提供常驻 token 与槽位规则，`formulaItems` 作为装备槽位拼入后经 `SpellProgramCompiler` 编译；敌人侧伤害、射程和子弹速度倍率覆写会派生临时 `SpellProjectileNode`，不再修改缓存中的 adapter 数据
  - 单 projectile 编译逻辑已收敛为内部 `SpellProjectileCompiler`，并输出内部 `SpellProjectileCompileResult`；`SpellProgramCompiler` 会直接把该内部结果转成 `SpellProjectileNode`。旧 `AttackFormulaCompiler` public wrapper、`CompiledAttack` 数据类、`CompiledSpellProgram.CreateFromCompiledAttack(...)` 和 `SpellProjectileNode.CreateRuntimeSnapshotFromCompiledAttack(...)` 已删除，`BackPackAttackPreviewController`、`SpellDescriptionGenerator`、`SpellBookLoadout`、`CharBulletVisualPresenter`、`CharBullet.InitializeShot` 和 `AttackProjectileEmitter` 都不再保留旧 `CompiledAttack` 兼容入口；系统级 baseline、永久升级、子弹命中、视觉 presenter 与单 projectile 编译测试已全部改用 `SpellProgramCompiler` / `SpellProjectileNode`
  - `SpellDescriptionGenerator` 基于当前 `CompiledSpellProgram` 生成背包 Spell Book 的短中文 TMP rich text 描述；文案库位于 [`Assets/Data/UI/SpellDescriptionCatalog.json`](Assets/Data/UI/SpellDescriptionCatalog.json)，可配置 Core / Behavior / Result / 特殊效果 / Value 消费落点 / 结构 / 法术书特性短句。描述 API 只接收 `CompiledSpellProgram`，会从 primary `SpellProjectileNode` 快照生成主句与效果句，并读取 `PrimaryCastBlock` 说明 CastBlock、Modifier 作用域、Trigger/Payload 载荷，以及当前法术书槽位、冷却、激活次数、常驻 token 和内建强化数量 / 目标明细
  - Token 数据资产位于 [`Assets/Scripts/Kernel/Bullet/TokenData`](Assets/Scripts/Kernel/Bullet/TokenData)
- 敌人与波次：位于 [`Assets/Scripts/Kernel/Enemy`](Assets/Scripts/Kernel/Enemy)
  - 入口：[`EnemyDefinition.cs`](Assets/Scripts/Kernel/Enemy/EnemyDefinition.cs)、[`EnemyGenerator.cs`](Assets/Scripts/Kernel/Enemy/EnemyGenerator.cs)、[`WaveDefinition.cs`](Assets/Scripts/Kernel/Enemy/WaveDefinition.cs)、[`WaveManager.cs`](Assets/Scripts/Kernel/Enemy/WaveManager.cs)
  - `EnemyStatusEffectController` 现在提供通用状态槽骨架：点燃、冻结、潮湿、腐蚀、失能、绑缚、标记、变形和傀儡标记；Core 与 Result 都可通过 `SpellStatusApplication` 写入同一套槽位。当前反应解析器覆盖热裂、感电、导雷和毒爆的基础判断，并默认消耗相关槽 50%；完整平衡表和视觉表现仍在 `Docs/SpellTokenSystemDesign.md` 中作为内容后续
  - `EnemyDefinition` 当前支持独立的移动槽、攻击槽、技能槽与基础战斗数值；移动类型包含追踪、冲刺、风筝、受击仇恨、环绕目标、Boss 主动游走与“跟随最近敌人并保持距离”，攻击类型包含接触近战、远程 Token 子弹与近距自爆；技能类型除召唤外新增了 Boss 一阶段可用的 `DelayedGroundBomb`（玩家脚下延时爆炸与范围红圈预警），并引入通用技能行动锁使敌人释放技能期间不触发平A；远程子弹现支持 Homing 追踪与 Healing 命中结算，并支持敌人侧子弹速度倍率覆写；当前普通敌人原型统一收敛为 `群 / 迅 / 甲 / 召 / 爆 / 弦 / 锁 / 愈`，并新增双阶段 Boss 定义 `Boss_Phase1 / Boss_Phase2`
  - `WaveDefinition` 当前负责刷怪组合、Boss 波次开关与 Boss 元数据（Boss 显示名、二阶段定义、血量阈值）；普通波次掉落与波后 `CombatEntryTokenSelectionPlan` 由 [`WaveSequenceProgressionConfig`](Assets/Scripts/Kernel/Enemy/WaveSequenceProgressionConfig.cs) 按“第 x 波”统一驱动，`WaveDefinition` 仅保留条目级额外掉落扩展位；Boss 波次只走自身内部掉落（及可选自身波后计划），不参与普通波次映射；敌人数值真源位于 [`EnemyDefinition`](Assets/Scripts/Kernel/Enemy/EnemyDefinition.cs)
  - `Main` 场景中的 [`WaveManager`](Assets/Scripts/Kernel/Enemy/WaveManager.cs) 不再依赖启用时自动开打，而是由 `MapRunFlowController` 在玩家进入起始房间传送装置后显式启动；每波清空后若解析到了波后计划，会先请求一轮 token 选择，再恢复波次推进；同一场战斗内每清完一波，`WaveManager` 会把 `completedWaveCount` 同步给 `EnemyGenerator`，并按 `1 + 0.04 * completedWaveCount` 对敌人的战力向数值做统一成长；Boss 条目会触发 Boss 生命周期事件，并可在运行时按血量阈值切换到二阶段定义
- 输入、玩家、存档：位于 [`Assets/Scripts/Kernel/Input`](Assets/Scripts/Kernel/Input)、[`Assets/Scripts/Kernel/Player`](Assets/Scripts/Kernel/Player)、[`Assets/Scripts/Kernel/Save`](Assets/Scripts/Kernel/Save)
  - 存档入口：[`RuntimeSaveService.cs`](Assets/Scripts/Kernel/Save/RuntimeSaveService.cs)
  - `UIInputRouter` 当前支持 `Hint(Tab)`：在 `MainUIScreen` 与 `BackPackUIScreen` 上开关 [`Assets/Scripts/Kernel/UI/HintUIScreen.cs`](Assets/Scripts/Kernel/UI/HintUIScreen.cs)；背包顶部 Hint 按钮与 Tab 走同一条路由
  - `HintUIScreen` 打开时会进入 `InHint` 状态，并与背包一致阻断玩家战斗输入和敌人行为
  - 当前存档使用动态槽位：`profile-slot-N.json` 保存每个栏位的永久数据，`global-mode.json` 保存 `DevMode` / `NormalMode`、已知槽位摘要与最近打开时间；新档会复用最小空槽位，加载弹窗只展示已有存档，并按最近打开时间从上到下排序，旧档缺少打开时间时回退保存时间

### 基础设施层 `Vocalith`

- UI 基础设施：[`Assets/Scripts/Vocalith/UI`](Assets/Scripts/Vocalith/UI)
  - 入口：[`UIManager.cs`](Assets/Scripts/Vocalith/UI/UIManager.cs)，负责持久化 UI 栈、EventSystem 收敛、根 CanvasScaler UI 缩放与固定尺寸 LayoutGroup 自适应，不再强制 16:9 视口
- 音频基础设施：[`Assets/Scripts/Vocalith/Audio`](Assets/Scripts/Vocalith/Audio)
  - 入口：[`AudioManager.cs`](Assets/Scripts/Vocalith/Audio/AudioManager.cs)，负责持久化单例、BGM crossfade、SFX 播放池与 Master/Music/SFX 三路音量
- 本地化：[`Assets/Scripts/Vocalith/Localization`](Assets/Scripts/Vocalith/Localization)
  - 入口：[`LocalizationManager.cs`](Assets/Scripts/Vocalith/Localization/LocalizationManager.cs)
  - 字符串表资源位于 [`Assets/Data/Localization/StringTables`](Assets/Data/Localization/StringTables)，通过 Addressables label `Localization` 加载
  - JSON 外置补丁位于 [`Assets/Data/Localization/JsonPatches`](Assets/Data/Localization/JsonPatches)，通过 Addressables label `LocalizationJson` 加载；运行时 JSON 目录可通过 `LocalizedJsonUtility` 按 `domain + id` 应用本地化覆盖
- 日志：目录是 [`Assets/Scripts/Vocalith/Log`](Assets/Scripts/Vocalith/Log)，命名空间是 `Vocalith.Logging`
- 事件总线：[`Assets/Scripts/Vocalith/Event`](Assets/Scripts/Vocalith/Event)
- 存档底层：[`Assets/Scripts/Vocalith/Scribe`](Assets/Scripts/Vocalith/Scribe)
  - 入口：[`Scribe.cs`](Assets/Scripts/Vocalith/Scribe/Scribe.cs)

## 数据与资源真源

- Bullet Token 数据：[`Assets/Data/BulletTokens`](Assets/Data/BulletTokens)
  - 当前起始房间初始抽取默认通过 [`Assets/Data/BulletTokens/SelectionPlans/StartRoomCombatEntryTokenSelectionPlan.asset`](Assets/Data/BulletTokens/SelectionPlans/StartRoomCombatEntryTokenSelectionPlan.asset) 指向 [`Assets/Data/BulletTokens/TokenLib/CoreOnly_Token_Lib.asset`](Assets/Data/BulletTokens/TokenLib/CoreOnly_Token_Lib.asset) 作为可选 token 库
  - BulletToken 现已支持 `箭 / 火 / 冰 / 雷 / 岩 / 刃 / 毒 / 影 / 水 / 风 / 光 / 羊 / 谜` 等正式 Core，以及 Bounce / Pierce / Spread / Homing / Chain / Stasis / Rush / Slow / Snake / Wander / Behavior Split / Spin / Explosion / Result Split / Control / Healing / Drain / Shield / Leave / Push / Pull 等行为与结果词
  - 第一批正式新 Token 已生成到独立 staging 库：[`SpellToken_Playable_Staging_Lib.asset`](Assets/Data/BulletTokens/TokenLib/SpellToken_Playable_Staging_Lib.asset) 包含 80 个 playable Token，[`SpellToken_Hidden_Prototype_Lib.asset`](Assets/Data/BulletTokens/TokenLib/SpellToken_Hidden_Prototype_Lib.asset) 包含 5 个 hidden prototype Token；两者都不在 `Plan2` 中，也不会扩大当前普通奖励池。生成入口是 `Tools/Lilith/Bullet/Generate Formal Spell Token Assets`
  - `PrototypeTokenData` 仅用于隐藏设计占位，不追加编译 token；隐藏库当前只覆盖 `镜/召`、`幻/替/傀`，每个资产都记录计划语义和未实装原因，供构筑调试和后续迁移使用；`卫` 已取消并从游戏侧 hidden prototype 移除，仅在设计文档中留档；`箭 / 岩 / 水 / 风 / 光 / 羊 / 谜` 已作为正式 `AttackCoreType` 接入，其中 `水` 写入潮湿，`风` 命中点小范围推开低重量敌人，`光` 可穿敌人和墙体但只造成衰减直伤且不触发 Result/Core 状态/OnHit/OnKill payload，`羊` 满 3 层后强控并变色普通低重量敌人，`谜` 每发随机解析为 `箭/火/冰/雷/岩/刃/毒/影`；`链` Behavior 已有最小运行时：命中主目标后向附近未命中过的敌人传导 50% 直伤，且不触发 payload 或递归派生；`滞 / 驰 / 缓 / 蛇 / 游 / 分 / 旋` 已作为正式 Behaviour 接入，其中 `滞` 停在发射点并只做一次出生点直击检测，`分` 在飞行期均匀派生安全小弹，`旋` 围绕施法者环绕；`绕` 已作为正式 Multicast 接入，固定收集 2 段，第二段环绕第一段主弹飞行；`汲 / 护 / 留 / 斥 / 吸 / 混` 已作为正式 Result 接入，分别提供吸血、吸收盾、残留场、推开、拉近和随机已实现 Result；`稳 / 狂 / 贪 / 急 / 源 / 乱` 已作为正式 Modifier 接入，分别提供稳定降散/降扰动、发射自损换伤害并增加能量消耗、发射自损换击败掉率、降低法术书发射间隔、降低法术书能量消耗，以及每次发射随机套用一个已实现 Modifier
  - 新体系 token 第一批资产位于 `Modifier`、`Multicast`、`Trigger` 等子目录；[`SpellProgram_Token_Lib.asset`](Assets/Data/BulletTokens/TokenLib/SpellProgram_Token_Lib.asset) 会混合 Core、Value、Modifier、Multicast 与 Trigger，不再包含 Payload Boundary，其中 Modifier 样本包含 `HasteModifier`、`BlockAmplifyModifier`，以及面向 payload result-only 效果的 `PayloadAmplifyModifier` / `PayloadRadiusModifier` / `PayloadCountModifier` / `PayloadControlFieldModifier`
  - [`Assets/Data/BulletTokens/SelectionPlans`](Assets/Data/BulletTokens/SelectionPlans) 保存可复用的 token 抽取计划；`Main` 场景中的 [`MapRunFlowController`](Assets/Scripts/Kernel/MapGrid/MapRunFlowController.cs) 可单独引用一份“初始抽取”计划，普通波次的“波后抽取”则由 [`Assets/Data/Waves/NonBossWaveSequenceProgression.asset`](Assets/Data/Waves/NonBossWaveSequenceProgression.asset) 按波次映射配置
- 法术书数据：[`Assets/Data/SpellBooks`](Assets/Data/SpellBooks)
  - [`ApprenticeSpellBook.asset`](Assets/Data/SpellBooks/ApprenticeSpellBook.asset) 是当前默认玩家执行器：5 槽，0.25 秒冷却，每次激活 1 次，无常驻 token
  - [`WideSpellBook.asset`](Assets/Data/SpellBooks/WideSpellBook.asset)、[`QuickSpellBook.asset`](Assets/Data/SpellBooks/QuickSpellBook.asset)、[`TriggerSpellBook.asset`](Assets/Data/SpellBooks/TriggerSpellBook.asset)、[`SurgeSpellBook.asset`](Assets/Data/SpellBooks/SurgeSpellBook.asset)、[`BindingSpellBook.asset`](Assets/Data/SpellBooks/BindingSpellBook.asset) 是第一批可奖励法术书：Wide 固定前置 `BlockAmplifyModifier`、提供 7 槽慢冷却，并以 10 度激活扇形每次发射 2 轮；Quick 固定前置 `HasteModifier`、提供 4 槽快冷却，并带原生伤害 `*=0.85`；Trigger 固定后置 `OnHitTrigger + Explosion`，会把玩家装备的外层法术变成命中触发爆炸 payload 的构筑，并带原生 payload 结果倍率 `*=1.25`；Surge 提供 5 槽、0.18 秒冷却、每次 3 轮 8 度扇形，带 3 点能量容量、每次消耗 1、每秒恢复 0.75 的爆发资源门槛，并带原生伤害 `*=0.8`；Binding 提供 5 槽、0.32 秒中速冷却，固定后置 `OnHitTrigger + Control`，会把玩家装备的外层法术变成命中触发控制 payload 的构筑，并用原生 `ResultCount =1` / `ResultDuration *=1.5` 把控制阈值降为 1 次、持续时间放大到 1.5 倍
  - [`SpellBookReward_Lib.asset`](Assets/Data/SpellBooks/SpellBookReward_Lib.asset) 是当前 Run 内法术书奖励库；`Plan2` 已将其作为后段波后奖励来源之一，并用低于新 token 来源的保守权重接入，避免后段奖励过早被法术书替换压过 token 构筑
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
- 本地化资源：[`Assets/Data/Localization`](Assets/Data/Localization)
  - `StringTables` 保存 key/value 字符串包，当前首批语言为 `zh-Hans-CN` 与 `en-US`
  - `JsonPatches` 保存按语言拆分的外置 JSON patch，当前用于 Options、Hint、Settlement、Quest、Story 与永久升级目录
- 业务 UI prefab：[`Assets/Prefabs/UI`](Assets/Prefabs/UI)
- 敌人 prefab：[`Assets/Prefabs/Enemy`](Assets/Prefabs/Enemy)
- 子弹 prefab：[`Assets/Prefabs/Bullet`](Assets/Prefabs/Bullet)

## 协作文档

| 文档 | 面向对象 | 用途 |
| --- | --- | --- |
| [`Docs/ArtistGuide.md`](Docs/ArtistGuide.md) | 美术 | 替换临时美术资源、确认场景 / prefab / UI 视觉入口与注意事项 |
| [`Docs/DesignGuide.md`](Docs/DesignGuide.md) | 策划 | 编写游戏内文本、任务、敌人、波次、Token、掉落和数值配置 |
| [`Docs/SpellConstructionSystemDesign.md`](Docs/SpellConstructionSystemDesign.md) | 程序 / 策划 / Agent | 理解当前法术构筑系统的概念、编译规则、运行边界和扩展方式 |
| [`Docs/SpellConstructionUseCases.md`](Docs/SpellConstructionUseCases.md) | 程序 / 策划 / Agent | 查询具体构筑摆法、槽位类别、编译解释、运行效果和 warning |
| [`Docs/LocalizationGuide.md`](Docs/LocalizationGuide.md) | 策划 / 翻译 / 测试 | 添加和验收多语言文案、维护字符串表与 JSON patch |
| [`Docs/AudioGuide.md`](Docs/AudioGuide.md) | 音频 / 美术 / 策划 | 导入音乐与音效、设置 Addressable 地址、向程序交付播放需求 |

## 命名说明

当前仓库里有几处容易混淆但实际不同的命名，本文统一按真实文件与类型书写：

- `StartUp`：场景名与脚本文件名，例如 [`Assets/Scenes/StartUp.unity`](Assets/Scenes/StartUp.unity) 和 [`Assets/Scripts/StartUp.cs`](Assets/Scripts/StartUp.cs)
- `Startup`：[`Assets/Scripts/StartUp.cs`](Assets/Scripts/StartUp.cs) 中定义的 `Main` 场景本地启动器类
- `Kernel.GameState`：命名空间；对应磁盘目录是 [`Assets/Scripts/Kernel/Status`](Assets/Scripts/Kernel/Status)
- `Vocalith.Logging`：命名空间；对应磁盘目录是 [`Assets/Scripts/Vocalith/Log`](Assets/Scripts/Vocalith/Log)

## 已知限制

- [`Assets/Scenes/Main.unity`](Assets/Scenes/Main.unity) 不能作为独立入口直接运行；当前必须先经过 [`Assets/Scenes/StartUp.unity`](Assets/Scenes/StartUp.unity) 中的 [`GlobalStartup`](Assets/Scripts/GlobalStartup.cs) 交接
- [`Assets/Scripts/Kernel/UI/OptionsUIScreen.cs`](Assets/Scripts/Kernel/UI/OptionsUIScreen.cs) 当前负责按 JSON 生成设置 UI，使用 `Assets/Prefabs/UI/Options/Setting Panel.prefab` 作为 Addressables 设置页；当前主菜单和暂停菜单的 Setting Panel 都带 Reset / Apply，控件值变更后先暂存，点击 Apply 后写入 `PlayerPrefs` 并应用，Reset 会把控件恢复默认值并等待 Apply 提交；按键项会通过 Input System binding override 保存，显示项中的分辨率 / 全屏 / 垂直同步会通过 `Kernel.Display.LilithDisplaySettings` 应用并在下次启动恢复，UI 缩放会通过 `UIManager` 根 CanvasScaler 应用，音频音量会通过 `Vocalith.Audio.AudioManager` 应用，游戏项中的屏幕震动开关会被 `ScreenShakeState` 读取；目标帧率不可配置，默认上限为 360 FPS，显示器刷新率高于 360 时以显示器刷新率为准；首版尚未配置实际音乐资源和玩法音效触发点
- [`Assets/Scripts/Kernel/UI/PauseUIScreen.cs`](Assets/Scripts/Kernel/UI/PauseUIScreen.cs) 当前直接内嵌同一个 `Assets/Prefabs/UI/Options/Setting Panel.prefab` 设置页；`PauseUI.prefab` 的旧 `Main Panel`、Resume / Option / Quit 按钮只作为可选兼容引用保留，不再是初始化必需项。暂停设置页的 `Close Button` 通过 `UIInputRouter.RequestClosePauseMenu()` 恢复游戏，`Menu Button` / 旧 Quit 入口会先打开通用 `PopUpUIScreen` 二级确认，确认后再通过 `UIInputRouter.RequestReturnToStartUpScene()` 返回开始菜单；开始菜单仍继续以独立 modal 方式打开 `OptionsUIScreen`
- [`Assets/Scripts/GlobalStartup.cs`](Assets/Scripts/GlobalStartup.cs) 中的 `LoadAllDefsCoroutine()` 当前预加载进入 `Main` 前需要的 Addressables 数据层资源；非 Addressable 且由场景直接引用的资产仍随场景加载
- 当前 `asmdef` 是第一版保守切分：`Kernel` 内部 UI / Bullet / Enemy / MapGrid / Quest 等子模块仍在同一程序集内，后续若要细拆需要先解开这些子模块之间的双向或横向依赖。
