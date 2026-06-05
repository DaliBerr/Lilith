# 法术构筑系统设计说明

本文档描述 Lilith 当前法术构筑系统的稳定设计。它面向接手该系统的人类与 agent，目标是让读者先理解“系统如何思考法术”，再去看代码、资产和具体用例。

相关文档：

- 构筑例子与 warning 对照：[`Docs/SpellConstructionUseCases.md`](SpellConstructionUseCases.md)
- 重构历史与阶段记录：[`Docs/SpellSystemRefactorPlan.md`](SpellSystemRefactorPlan.md)
- 旧系统基线与测试护栏：[`Docs/SpellSystemBaselineSnapshot.md`](SpellSystemBaselineSnapshot.md)

## 一句话模型

法术不再是一个线性 `Core + Behavior + Result + Value` 攻击配置，而是：

```text
SpellBook Executor
  -> token execution sequence
  -> CompiledSpellProgram
       -> outer SpellCastBlock
            -> SpellProjectileNode*
            -> SpellModifierNode*
            -> OnHit SpellPayloadBlock*
                 -> inner SpellCastBlock
                      -> SpellProjectileNode*
                      -> SpellPayloadEffectNode*
```

玩家看到的是一串 token；运行时执行的是编译后的 `CompiledSpellProgram`。背包描述、攻击预览、玩家发射、敌人远程 token 攻击都应读取编译结果，而不是各自重新解释 token。

## 设计目标

1. **法术书是执行器**
   法术书负责槽位、冷却、每次激活次数、激活扇形、能量门槛、固定 token 与不占槽的原生 modifier。它不是单纯背包容器。

2. **token 仍从左到右阅读**
   构筑应能被玩家按摆放顺序理解。特殊结构通过位置和结构 token 形成，不依赖隐藏回头规则。

3. **UI 类别少，编译语义清楚**
   UI 可以保持 Core / Behavior / Value / Result / Modifier / Multicast / Trigger 这类有限分类；payload 是 Trigger 后的结构语义，不是独立可抽取 token。

4. **作用域由编译决定，不由普通 Modifier 资产写死**
   普通 `ModifierTokenData` 只表达“改什么”，不表达“改谁”。`SpellModifierNode.Scope` 和 `TargetCount` 是编译结果。

5. **结构边界必须可解释**
   Trigger 后自动开启 payload；Multicast 收集固定数量 projectile segment；普通 modifier 不跨 trigger/payload 边界。遇到不完整或有歧义的摆法，系统给 warning，而不是静默猜测。

## 核心术语

### SpellBook Executor

法术书是执行器。它提供：

- 装备槽位数量。
- 固定前置 token 与固定后置 token。
- 冷却、每次激活次数、激活扇形。
- 可选能量容量、消耗与恢复。
- 不占槽的 executor modifier。

例子：

- Apprentice：默认执行器，无固定 token。
- Quick：固定前置 `HasteModifier`，冷却更快，但原生伤害倍率降低。
- Wide：固定前置 `BlockAmplifyModifier`，槽位更多，每次激活有多轮扇形。
- Trigger：固定后置 `OnHitTrigger + Explosion`，把装备法术转成命中爆炸 payload。
- Binding：固定后置 `OnHitTrigger + Control`，把装备法术转成命中控制 payload。
- Surge：短冷却、多轮扇形，但受能量门槛约束。

### Token

当前 token 类别：

| 类别 | 作用 |
| --- | --- |
| Core | 生成 projectile 的核心，普通外层法术必须有 Core 才能发射 |
| Behavior | 改变 projectile 的发射或运动行为，例如散射、追踪、穿透 |
| Result | 绑定 projectile 的命中结果，或在 result-only payload 中成为 payload effect |
| Value | 数值词，由紧邻的声明者解释为 Count / Radius / Duration 等 |
| Modifier | 修饰节点，按编译位置解析作用域 |
| Multicast | 结构节点，让同一次施法包含多个 outer projectile |
| Trigger | 结构节点，支持 OnHit / OnTimer / OnExpire / OnKill / OnDistance / OnProximity，后续 token 自动成为 payload |

Payload 不是 token 类别。它是 Trigger 之后形成的结构区域。

### CompiledSpellProgram

`CompiledSpellProgram` 是运行时和 UI 共同使用的编译产物。它持有 primary `SpellCastBlock`、编译消息和可发射状态。

关键原则：

- 发射器执行 `PrimaryCastBlock.Projectiles`。
- 描述系统读取 `CompiledSpellProgram` 和 `SpellProjectileNode` 快照。
- 预览系统显示 outer projectiles，但不执行 payload。
- 编译 warning 应保留到 `Messages`，用于描述、调试和测试断言。

### SpellCastBlock

`SpellCastBlock` 是一次施法块。

它可以包含：

- `Projectiles`：普通 projectile 节点。
- `Modifiers`：编译后记录的 modifier 节点。
- `PayloadEffects`：result-only payload 中的效果节点。
- `Payloads`：保留结构关系的 payload block。

外层 block 通常包含 projectile；payload 内 block 可以包含 projectile，也可以只包含 result-only `PayloadEffects`。

### SpellProjectileNode

`SpellProjectileNode` 是 projectile 的运行时语义快照。它保存：

- Core / Behavior / Result 类型。
- `AttackSpec`、伤害、速度、生命周期、射程等发射参数。
- Core / Result effect payload。
- 爆炸、治疗、控制、分裂等命中语义。
- 视觉修饰，例如颜色、字号、缩放。
- 绑定在这个 projectile 上的 payload 列表。

运行时子弹初始化只接收 `SpellProjectileNode`，不再接收旧 `CompiledAttack`。

### SpellPayloadBlock

`SpellPayloadBlock` 表示某个 Trigger 绑定的 payload。当前 Trigger 类型只有 `OnHit`。

当 projectile 命中 actor 或非 Bounce 环境碰撞时，`CharBullet` 会尝试执行 OnHit payload。

### SpellPayloadEffectNode

当 payload 内没有 Core 时，payload 会作为 result-only payload 编译。此时 Result token 不生成 projectile，而是生成 `SpellPayloadEffectNode`。

当前 result-only payload 覆盖：

- Explosion
- Split
- StatusEffect / Control
- Healing

## 编译流程

```text
SpellBookLoadout
  1. 合并法术书固定 token 与玩家装备 token
  2. 形成 ExecutionItems
  3. SpellProgramCompiler.Compile(...)
  4. 生成 CompiledSpellProgram
  5. 描述 / 预览 / 发射 / 敌人远程攻击共同读取这个结果
```

编译时的高层步骤：

1. 识别外层结构：单 projectile、Multicast、Trigger。
2. 按结构边界切出 projectile segment。
3. 用单 projectile 编译链处理 Core / Behavior / Result / Value。
4. 按位置解析普通 Modifier 的作用域。
5. 编译 Trigger 后的 payload。
6. 应用法术书 executor modifier。
7. 生成 warning / error message。

## 单 projectile 规则

普通单 projectile segment 最多接受：

- 一个 Core。
- 一个 Behavior。
- 一个 Result。
- 若干可被消费的 Value / Modifier。

如果没有 Core：

- 外层普通法术不可发射。
- payload 内如果没有 Core，则可以进入 result-only payload 编译。

重复 Core / Behavior / Result 不会自动组合成多个效果。重复项会 warning；需要多个 projectile 时使用 Multicast，需要多个 payload effect 时使用 result-only payload。

## Value 规则

Value 是“数值载荷”，不是独立效果。

消费规则：

- Behavior / Result 可声明自己需要的 `SpellValueParameterKind`。
- 当前 Value 可落到 Count、Radius 或 Duration。
- Value 只在紧跟消费者时被消费。
- 被消费的 Value 不再参与后续解释。
- 无消费者的 Value 会 warning。

特殊优先级：

- `Modifier + Value + ...` 中，紧跟 Modifier 的 Value 会优先被 Modifier 解释为 `NextN` 目标数量。
- 这个 Value 不再交给后面的 Behavior / Result。

例子：

```text
火 + 散 + 三      => 三是散射数量
火 + 爆 + 三      => 三是爆炸半径
疾 + 三 + 火 + 散 + 爆
                 => 三是疾的 NextN 目标数
```

## Modifier 规则

普通 Modifier 是自适应作用域。

资产语义：

- `ModifierTokenData` 只保存数值修饰载荷，例如速度、尺寸、伤害、结果倍率。
- 普通 Modifier 不保存 Scope / TargetCount 真源。

编译语义：

- `Modifier + Core/Behavior/Result`：解析为 `NextToken`。
- `Modifier + Value + Core/Behavior/Result...`：Value 被消费为 `NextN` 目标数量。
- `Modifier + Multicast + ...`：解析为 upcoming `CurrentBlock`。
- payload 内 Modifier 只在 payload 局部向后解析。
- 普通 Modifier 不回头影响已经结束的 token。
- 普通 Modifier 不自动推断为 `GlobalProgram`。
- Trigger / payload 边界会截断外层 `NextN`。

`SpellModifierNode.Scope` 与 `TargetCount` 只表示本次编译结果。描述和 UI 预览应读取编译结果，不要读取资产字段推断。

### Modifier 可修饰的目标

对 projectile 可见的目标包括：

- TextColor
- FontSize
- ScaleMultiplier
- ProjectileSpeed
- MaxLifetime
- MaxTravelDistance
- ImpactRadiusMultiplier
- Damage

对 result-only payload effect 可见的目标包括：

- ImpactRadiusMultiplier
- ResultCount
- ResultDuration
- ResultMultiplier

映射例子：

| Modifier 目标 | Result-only payload 映射 |
| --- | --- |
| ImpactRadiusMultiplier | Explosion 半径、Healing 范围、Control 范围 |
| ResultCount | Split 数量、Control 触发阈值 |
| ResultDuration | Explosion 延迟、Control 持续时间 |
| ResultMultiplier | Explosion 伤害倍率、Split 子弹倍率、Healing 治疗倍率 |

命名为 `PayloadAmplifyModifier`、`PayloadRadiusModifier`、`PayloadCountModifier`、`PayloadControlFieldModifier` 的资产只是内容表达；它们能修饰 payload，是因为它们被摆在 payload 内，而不是因为资产硬编码了 payload 作用域。

## Multicast 规则

Multicast 是结构 token，用于创建同一次施法里的多个 outer projectile。

当前规则：

- 第一个 Multicast 从右侧收集固定数量 projectile segment。
- 每个 segment 编译成一个 `SpellProjectileNode`。
- 所有收集到的 projectile 放在同一个 outer `SpellCastBlock` 中。
- `Modifier + Multicast` 会成为 block modifier，作用于这个 CastBlock。
- 右侧不足时保留能编译的 projectile，并给 warning。
- 收集满后的尾随 token 会 warning / ignored。
- 当前不启用 nested Multicast。
- 当前不做 Noita 风格 wrapping 或 shuffle。

例子：

```text
双 + 火 + 冰
=> 一个 CastBlock，两个 outer projectile：火、冰

放 + 双 + 火 + 冰
=> 放作为 CurrentBlock modifier，修饰火和冰
```

## Trigger / Payload 规则

Trigger 是 payload 的入口。当前实现只有 `OnHit`。

核心规则：

- Trigger 后续 token 自动构成 payload。
- 不再支持显式 `PayloadStart` / `PayloadEnd`。
- 非 Multicast 公式中，payload 消费到整式末尾。
- Multicast 中，Trigger 只属于当前 projectile segment。
- 一旦 segment 进入 payload，后续 token 都归这个 payload，不再回到兄弟 outer projectile。
- 空 payload 会 warning，不附着 payload。
- 当前不启用 nested trigger / nested payload / wrapping。

例子：

```text
火 + 触 + 冰
=> 火命中后发射冰

火 + 触 + 爆 + 三
=> 火命中后触发半径 3 的 result-only 爆炸

双 + 火 + 触 + 冰 + 雷
=> 第一个 segment 的火进入 payload，冰和雷都被吞进这个 payload；
   第二个 outer segment 不足，给 warning
```

## Payload 编译模式

payload 有两种模式。

### Projectile payload

payload 内存在 Core 时，payload 被编译成 inner projectile program。

例子：

```text
火 + 触 + 冰
=> 外层火命中后，在命中点发射内层冰 projectile
```

payload 内也可以有 Multicast：

```text
火 + 触 + 双 + 冰 + 雷
=> 火命中后，同时发射冰和雷
```

### Result-only payload

payload 内没有 Core，但存在 Result 时，Result 被编译成 `SpellPayloadEffectNode`。

例子：

```text
火 + 触 + 爆
=> 火命中后在命中点爆炸

火 + 触 + 愈 + 三
=> 火命中后在命中点做半径 3 的范围治疗
```

result-only payload 可包含多个 Result：

```text
火 + 触 + 爆 + 愈 + 定
=> 同一个命中 payload 中依次保留爆炸、治疗、控制效果
```

## Runtime 执行

### 发射

`AttackProjectileEmitter` 只接收 `CompiledSpellProgram`，并逐个执行 `PrimaryCastBlock.Projectiles`。

发射形状来自：

- projectile node 自身的 Behavior，例如 Spread。
- outer CastBlock 中的多个 projectile。
- 法术书每次激活次数与激活扇形。

### 命中

`CharBullet` 持有当前 `SpellProjectileNode` 的运行时快照。

命中后：

1. 处理直击伤害。
2. 处理外层 Result，例如爆炸、分裂、控制、治疗。
3. 执行 OnHit payload。

result-only Split 和 payload Split 派生出的子弹会降级为无 payload 的 DirectDamage 子弹，避免递归失控。

### 运行时护栏

系统限制 payload 深度和单次派生 projectile 数，防止 Trigger / Split / payload 组合无限递归。

## UI 与描述系统

### UI 分类

UI 不需要为 payload 单独做可选 token 类型。

推荐理解：

- Core / Behavior / Value / Result 是基础法术词。
- Modifier 是修饰词。
- Multicast / Trigger 是结构词。
- Payload 是 Trigger 后的结构区域。
- SpellBook 是执行器，不是 token。

### 汉字标识

每个 token 可以继续用一个汉字或短字作为视觉标识。这个标识只表达内容身份，不决定编译类别。

例如：

- `火` 是 Core。
- `散` 是 Behavior。
- `三` 是 Value。
- `触` 是 Trigger。
- 同样显示为 `火` 的变体，应通过资产、角标、边框或变体标记表达差异，而不是让显示字承担全部语义。

### 描述与预览

描述系统与背包预览必须读取 `CompiledSpellProgram`：

- Modifier 描述读取 `SpellModifierNode.Scope` 和 `TargetCount`。
- Value 描述读取实际消费落点。
- CastBlock 描述读取 `PrimaryCastBlock.Projectiles`。
- Trigger/Payload 描述读取 projectile 上的 payload。
- projectile payload 描述不能只写“释放 N 枚内层法术”，还要继续摘要内层 projectile 的 Core / Behavior / Result。
- 法术书描述读取当前 `SpellBookData` 的槽位、冷却、激活次数、固定 token 与 executor modifier。

不要让 UI 再维护一套独立解析规则。

## 法术书与奖励

当前玩家默认使用 `ApprenticeSpellBook`。Run 内奖励可提供 token 或 spellbook。

法术书奖励改变的是执行器：

- 替换槽位数量。
- 替换冷却与激活规则。
- 插入固定 token。
- 应用原生 executor modifier。

法术书不应偷偷改玩家装备 token 本身；它改变的是执行这些 token 的环境。

当前奖励库已有资产级 smoke 和权重护栏，确保每本可奖励法术书：

- 权重为正。
- ID / 显示名 / 执行器签名唯一。
- 奖励说明包含 slots / cooldown。
- 能用真实 Core 编译并生成描述。

## Warning 语义

Warning 是系统边界的一部分，不只是异常日志。

常见 warning：

- 普通外层缺少 Core。
- 重复 Core / Behavior / Result。
- 孤立 Value。
- 尾部 Modifier 无目标。
- Modifier `NextN` 被 Trigger 边界截断。
- Multicast 右侧不足。
- Multicast 收集满后的尾随 token 被忽略。
- 嵌套 Multicast。
- Trigger 后 payload 为空。
- payload 内重复 Core 且没有 inner Multicast。
- 显式 payload boundary token 已删除，不应再出现。

测试与描述系统应保留这些 warning，不要把它们吞掉。

## 当前不支持

这些不是 bug，而是当前明确边界：

- 显式 `PayloadStart` / `PayloadEnd`。
- nested trigger / nested payload。
- Noita 风格 wrapping / shuffle。
- 普通 Modifier 自动回头修饰。
- 普通 Modifier 自动变成 `GlobalProgram`。
- 多个 Core 在无 Multicast 的普通公式中自动成为多发。
- payload 结束后回到外层继续解析。

如需新增这些能力，应先更新本文档、用例文档和编译器测试，再改实现。

## 扩展指南

### 新增 Core

新增 Core 应提供：

- 基础 `AttackSpec`。
- core effect payload。
- 显示文本 / 本地化。
- 描述 catalog 条目。
- 必要的编译、描述、运行时测试。

### 新增 Behavior

新增 Behavior 应声明：

- 行为类型。
- 是否消费 Value。
- Value 消费类型。
- 发射器或 `CharBullet` 的运行时执行逻辑。

### 新增 Result

新增 Result 应声明：

- Result 类型。
- 默认 result effect payload。
- 是否消费 Value，以及消费类型。
- 普通 projectile Result 与 result-only payload 的运行时语义是否一致。

如果 result-only payload 需要 Modifier 支持，必须补充 `ApplyPayloadEffectModifier` 映射和测试。

### 新增 Modifier

新增 Modifier 只需要表达数值载荷。

不要在普通 Modifier 资产中写死作用域。作用域由摆放位置决定。

如果它希望成为专用全局术式或特殊 Technique，应新增专门入口，而不是复活普通 Modifier 的 `GlobalProgram` 自动推断。

### 新增 Trigger 或扩展 Trigger

当前已支持 `OnHit`、`OnTimer`、`OnExpire`、`OnKill`、`OnDistance`、`OnProximity`。继续新增 Trigger 类型或改变现有 Trigger 语义时，需要同时定义：

- 编译边界。
- runtime 触发时机。
- payload 执行位置和触发点语义。
- 是否消费紧随其后的 Value，以及缺省参数。
- 与 Multicast 的交互。
- 空 payload、嵌套 payload 的 warning 语义。
- 描述文案。

Trigger 不能只新增 enum；它还需要 runtime 触发、payload 生命周期、预览/描述和测试护栏。

## 测试护栏

任何改动当前系统语义的工作，至少考虑这些测试面：

- `SpellProgramCompilerTests`
- `SpellDescriptionGeneratorTests`
- `CombatEntryTokenSelectionPlanTests`
- `BackPackAttackPreviewControllerTests`
- `BackPackUIScreenTests`
- `SpellBookLoadoutTests`
- `TokenSelectModalTests`
- payload / impact 相关运行时测试

语义改动后，还应确认：

- `Lilith.Kernel.csproj` build。
- `Lilith.Tests.EditMode.csproj` build。
- Unity Console error。
- 静态搜索没有恢复旧 `CompiledAttack` / `AttackFormulaCompiler` / `PreTokenData` / `PostTokenData` / PayloadBoundary token。

## 维护规则

修改系统时按这个顺序更新文档：

1. 本文档：改概念、边界或规则。
2. [`Docs/SpellConstructionUseCases.md`](SpellConstructionUseCases.md)：补具体构筑例子和类别。
3. `README.md`：只补稳定入口或高层摘要。
4. `memory.md`：只记录难以重新发现的排障经验。
5. 共享记忆：按 Memory Consistency Pass 更新项目状态、TODO 或 handoff。

不要把迁移流水账写进本文档；历史阶段仍放在 `SpellSystemRefactorPlan.md`。
