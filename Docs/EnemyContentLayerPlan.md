# 普通敌人内容层后续实现计划

## 目标

本文记录普通敌人 AI 基座落地后的内容层后续工作。当前不继续实现这些内容，只把后续调参、敌人身份强化、资源配置、测试与手动验收拆清楚，方便下一轮直接接手。

普通敌人当前已经迁移到 `EnemyAIProfile`：

- `群`：`Assets/Data/Enemies/AIProfiles/AI_Qun.asset`
- `迅`：`Assets/Data/Enemies/AIProfiles/AI_Xun.asset`
- `甲`：`Assets/Data/Enemies/AIProfiles/AI_Jia.asset`
- `爆`：`Assets/Data/Enemies/AIProfiles/AI_Bao.asset`
- `弦`：`Assets/Data/Enemies/AIProfiles/AI_Xian.asset`
- `锁`：`Assets/Data/Enemies/AIProfiles/AI_Suo.asset`
- `愈`：`Assets/Data/Enemies/AIProfiles/AI_Yu.asset`
- `召`：`Assets/Data/Enemies/AIProfiles/AI_Zhao.asset`

## 当前内容基线

- `EnemyAIProfile` 负责 tick 间隔、感知半径、行动列表、评分条件和 fallback 行动。
- `EnemyAIController` 在 profile 存在时接管 movement override、攻击触发和技能触发。
- `EnemyDefinition` 仍是敌人数据入口，包含移动/攻击/技能/数值/远程公式/自爆配置。
- Boss 不在本内容层范围内。

## 内容层优先级

### P0：手动 Play 体感验收

第一件事不是继续加能力，而是确认当前 profile 在真实波次里是否可玩。

验收清单：

- Wave01-Wave06 能正常刷怪、推进、清场。
- `群 / 迅 / 甲 / 爆 / 弦 / 锁 / 愈 / 召` 有可感知差异。
- 敌人不会在暂停、背包、Hint、升级页、结算页打开时继续行动。
- 远程敌人不会因为风筝距离/射程不匹配而长期不攻击。
- 召唤敌人不会无限拖慢清场。
- 自爆敌人不会过早无预警秒杀，也不会永远追不上玩家。
- Boss 波仍保持旧逻辑。

### P1：AI Debug 与调参可视化

建议先做轻量调试工具，而不是立即写更多内容。

最小可用功能：

- 在选中敌人时显示当前 profile、当前 action、movement override、action cooldown。
- 输出最近一次 tick 的各 action 分数。
- 支持只在 Editor / Development build 中启用。
- 可以先做 `EnemyAIDebugSnapshot` + Inspector 展示，不需要完整运行时 UI。

建议字段：

- `CurrentActionId`
- `CurrentActionKind`
- `CurrentMovementOverride`
- `LastTickTime`
- `LastContextDistance`
- `LastContextHealthRatio`
- `LastNearbyAliveFriendCount`
- `LastActionScores`

测试：

- profile 激活时 debug snapshot 会更新。
- profile 为空时 debug snapshot 清空或显示 inactive。
- 暂停时不刷新 tick。

### P2：8 个普通敌人的身份强化

#### 群

定位：基础近战压力单位。

后续方向：

- 保持低复杂度。
- 调近战优先级和追击权重。
- 可考虑加入“附近友军越少越激进”或“附近友军越多越包围”的 consideration。

验收：

- 玩家能很快读出它是普通近战。
- 数量多时形成压力，但单只不应过强。

#### 迅

定位：冲刺突进型。

后续方向：

- 调 `ChaseThenDash` 的距离、前摇、冲刺持续和冷却。
- profile 中让中距离更倾向 dash engage，近距离则近战。
- 如果 dash 前摇缺少视觉/音频提示，应补表现，不要只改数值。

验收：

- 玩家能预判冲刺。
- 冲刺不会频繁穿过玩家后卡住。

#### 甲

定位：慢速高韧近战型。

后续方向：

- 以稳定追击和承压为主，不需要复杂行动。
- 调高血量/减伤/位移重量时，要同步检查推拉、控制和羊变形等效果。
- 可加入低血量仍追击的 consideration，突出“硬”。

验收：

- 不是更快的 `群`，而是更难处理的前排。

#### 爆

定位：自爆型。

后续方向：

- 优先检查自爆触发距离、爆炸半径、伤害、蓄力时间。
- 需要明确预警表现：颜色、缩放、音效或地面圈。
- profile 里应保证近距离优先爆炸，远距离只追击。

验收：

- 玩家能看见危险并有反应窗口。
- 自爆成功/失败都不会破坏波次清场。

#### 弦

定位：远程风筝型。

后续方向：

- 调 `KeepDistance` 的 preferred distance 与 attackRange。
- 检查远程 token 公式是否稳定、可读、伤害合理。
- 若玩家贴脸，优先后撤而不是原地射击。

验收：

- 玩家能识别它是远程射手。
- 不会因为风筝距离太大导致屏外消耗。

#### 锁

定位：控制远程型。

后续方向：

- 需要明确控制公式内容。优先复用已有 Control / StatusEffect token，而不是写特殊攻击器。
- profile 中远程攻击权重可高于 `弦`，但伤害应相对保守。
- 可加入“玩家在中远距离时更爱控制”的 consideration。

待确认：

- `锁` 的控制应是定身、减速、冻结、束缚，还是 Control 计数类效果？
- 控制命中是否需要专属视觉/音效？

验收：

- 玩家被命中后能理解自己为什么受限。
- 控制频率不能让玩家长期失去操作。

#### 愈

定位：轻量协同/治疗单位。

当前风险：

- 名字叫 `愈`，但如果只是远程攻击和跟随友军，玩家可能读不出治疗身份。

后续方向：

- 先确认是否要真实治疗友军。
- 若要治疗，建议新增一个普通敌人可复用的 `EnemyHealer` / `EnemyAllySkillCaster`，不要把治疗逻辑塞进 AIController。
- 治疗目标选择可先很简单：附近最低血量友军，排除自己或允许自疗由配置决定。
- profile 中加入“附近友军受伤时优先治疗”的 action。

待确认：

- `愈` 是否必须治疗，还是只是协同远程？
- 治疗是否能治疗召唤物？
- 治疗是否有上限、冷却、预警和可打断窗口？

验收：

- 玩家能看出它在支援敌群。
- 治疗不会让低波次战斗拖太久。

#### 召

定位：召唤炮台/召唤单位型。

后续方向：

- 当前已通过 `SummonEnemy` 技能槽与 profile action 调度召唤。
- 优先调召唤冷却、单次数量、最大存活召唤数和召唤半径。
- profile 中“附近友军不足时召唤”的 consideration 已有雏形，可根据 Play 结果调权重。

验收：

- 召唤行为清晰可读。
- 不会造成波次无限拖延。
- 召唤物死亡/清理后计数正确。

## 内容能力缺口

### 1. 友军治疗

需要新增或复用的能力：

- 查找附近友军。
- 判断友军血量比例。
- 执行治疗。
- 显示治疗表现。
- AI consideration 能读取“附近是否有受伤友军”。

建议新增输入：

- `NearbyDamagedFriendCount`
- `LowestNearbyFriendHealthRatio`

### 2. 控制远程公式

需要确认 `锁` 的公式资产：

- 使用现有 Control / StatusEffect token。
- 明确 projectile 速度、冷却、伤害和控制时长。
- 描述与 Hint 图鉴文案同步。

### 3. 自爆预警表现

当前自爆逻辑已有 windup，但内容层还需要表现：

- 蓄力颜色变化。
- 缩放/闪烁。
- 音效 cue。
- 可选范围提示。

### 4. AI 调参工具

没有调试视图时，profile 调参会非常盲。

建议先实现 Editor-only：

- `EnemyAIController` 暴露只读 debug snapshot。
- 自定义 Inspector 或 SceneView label 显示当前 action 分数。
- 可选写一条短日志，但不要默认刷屏。

## 数据与文案

需要同步检查：

- `EnemyDefinition.Description` 是否描述了敌人身份。
- Hint 图鉴是否能看到 8 个普通敌人的差异。
- 本地化是否需要更新敌人说明。
- profile 名、action id 是否便于策划理解。

建议 action id 采用稳定英文/拼音，不使用临时命名：

- `melee_pressure`
- `dash_engage`
- `prime_explosion`
- `token_shot`
- `control_shot`
- `regroup_with_friend`
- `summon_when_thin`

## 测试计划

### EditMode

- profile 资产非空：8 个普通敌人都有 `AIProfile`。
- Boss profile 为空。
- profile action score 对距离、血量、友军数量有稳定影响。
- AIController 在行动冷却中使用其他行动或 fallback。
- profile 激活时攻击器不会自主 `Update()` 造成双触发。
- profile 激活时 Binder 不走旧技能自动调度。
- 友军统计忽略自己、死亡对象和销毁对象。
- 若新增治疗：治疗目标选择、治疗上限、死亡对象过滤、冷却。

### 手动 Play

- Wave01-Wave06。
- 8 个普通敌人的识别度。
- 暂停/背包/Hint/升级/结算暂停。
- 召唤物上限和清场。
- 自爆预警。
- 控制频率。
- 治疗是否拖战斗。

## 暂不做

- 不做 Boss Brain。
- 不做行为树编辑器。
- 不做全局指挥官。
- 不重做波次系统。
- 不把普通敌人内容调参和法术系统大改混在同一轮。

## 接手建议顺序

1. 跑一轮手动 Play，记录 8 个普通敌人的实际体感。
2. 做 AI debug snapshot / Inspector。
3. 调整 8 个 profile 的权重和距离条件。
4. 单独处理 `愈` 的治疗身份。
5. 单独处理 `锁` 的控制公式和表现。
6. 单独处理 `爆` 的预警表现。
7. 补 Hint 图鉴描述和本地化。
8. 再跑 Wave01-Wave06 与 Boss 波回归。
