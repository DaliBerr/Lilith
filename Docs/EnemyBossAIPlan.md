# Boss AI 层后续实现计划

## 目标

本文记录 Boss AI 层的后续接手方案。当前阶段不实现、不改动 Boss 行为，只把后续要做的架构边界、任务顺序、验证项和风险写清楚。

当前普通敌人已经接入 `EnemyAIProfile` / `EnemyAIController` 的 Utility AI；Boss 暂时不迁移到这套 Controller。`Boss_Phase1` / `Boss_Phase2` 的 `AIProfile` 应继续保持为空，由现有 `BossSmartRoam`、攻击组件、技能槽和 `BossPhaseController` 保持当前逻辑。

## 当前 Boss 基线

- Boss 定义资产：`Assets/Data/Enemies/Boss_Phase1.asset`、`Assets/Data/Enemies/Boss_Phase2.asset`。
- Boss 当前仍通过 `EnemyDefinition.MovementKind = BossSmartRoam` 获得主动游走行为。
- Boss 攻击仍通过 `EnemyDefinition.AttackKind` 和现有攻击组件触发。
- Boss 技能仍通过 `EnemyDefinition.SkillSlots`、`EnemyDefinitionBinder` 和 `IEnemySkillCaster` 调度。
- Boss 阶段切换仍由 `BossPhaseController` 管理。
- Boss 不引用 `EnemyAIProfile`，也不由 `EnemyAIController` 接管。

## 建议架构

### 核心原则

Boss AI 不直接复用普通敌人的 `EnemyAIController`。原因是 Boss 需要 phase、时间轴、预警、电报、召唤窗口、场地压力和剧情/演出同步；这些不是杂兵 Utility tick 的简单扩展。

Boss 可以复用以下共享层：

- `EnemyAIContext` 的上下文采集思路。
- `EnemyAIConsiderationDefinition` 的评分输入模型。
- 攻击组件、技能组件、状态效果和暂停门控。
- `EnemyDefinition` 的战斗数值、远程配置、技能槽和视觉定义。

Boss 应新增独立 Brain 层：

- `BossAIProfile : ScriptableObject`
  - 保存多个 phase 的行为配置。
  - 每个 phase 包含行动池、优先级、冷却、锁定时间、血量/时间/场地条件。
  - 可选保存 enraged、低血量、召唤窗口、技能连段等配置。
- `BossAIBrain : MonoBehaviour`
  - 绑定当前 Boss 的 `Enemy`、`EnemyDefinitionBinder`、`BossPhaseController`、攻击器、技能执行器。
  - 维护当前 phase、当前行动、行动锁、全局冷却、phase 局部冷却。
  - 负责调度 Boss 专属行动，不修改普通敌人 `EnemyAIController`。
- `BossAIContext`
  - 可从 `EnemyAIContext` 扩展或内部组合。
  - 增加 `CurrentPhase`、`HealthRatio`、`ElapsedInPhase`、`ElapsedInFight`、`ActiveSummonCount`、`ArenaPressure`、`RecentActionId`、`TargetDistanceBand` 等 Boss 特有输入。
- `BossAIActionDefinition`
  - 行动类型建议覆盖：移动策略切换、远程攻击、技能槽释放、召唤窗口、强制位移、场地危险、等待/蓄力、电报演出。
  - 行动应支持 `telegraphSeconds`、`commitSeconds`、`recoverySeconds`、`globalCooldownSeconds` 和 `phaseCooldownSeconds`。

## 与现有系统的接入点

- `BossPhaseController`
  - 后续只暴露 phase 变化事件或查询接口。
  - 不把 phase controller 改成 AI Brain。
  - phase 切换时通知 `BossAIBrain` 重置 phase 局部行动状态。
- `EnemyDefinitionBinder`
  - Boss profile 仍为空时保持现有逻辑。
  - 若未来新增 `BossAIProfile` 字段，不应与 `EnemyAIProfile` 混用。
- `CharEnemyMovement`
  - Boss Brain 可以复用现有 `BossSmartRoam`，或通过专门接口临时覆盖移动模式。
  - 不建议让 Boss 走普通 `EnemyAIController.TryGetMovementOverride()`。
- `IEnemySkillCaster`
  - Boss Brain 继续通过技能槽调用 `TryCastSkill`。
  - Boss 专属技能若出现新类型，优先新增 `IEnemySkillCaster` 实现，而不是把特殊逻辑塞进 Brain。

## 分阶段实施

### M0：只读审计

- 梳理 `BossPhaseController`、`Boss_Phase1/2` 资产、Boss 波次、Boss UI 事件和现有技能槽。
- 确认哪些行为是 Boss 当前体感必须保留的。
- 输出一份“当前 Boss 行为快照”，作为后续测试基线。

### M1：无行为变化的 Brain 壳

- 新增 `BossAIProfile`、`BossAIBrain`、`BossAIContext`。
- `BossAIBrain` 只缓存组件、读取 phase、输出 debug 状态，不触发任何行动。
- Boss 资产仍不绑定 Brain profile，或绑定后处于 passive 模式。
- 目标是证明组件可以安全挂到 `CharEnemy.prefab` 或 Boss 专用 prefab，而不改变 Boss 行为。

### M2：接管 Boss 技能调度

- 让 Boss Brain 在 profile 激活时接管 Boss 技能槽调度。
- `EnemyDefinitionBinder` 对 Boss Brain 激活状态跳过旧技能自动调度，避免双重释放。
- 保留普通敌人 profile 与 Boss Brain 的互斥边界。

### M3：Phase-aware 行动调度

- Phase 1 / Phase 2 分别配置行动池。
- 支持按血量、距离、时间、冷却、当前召唤数量选择技能/攻击/移动。
- 增加行动锁和恢复时间，避免 Boss 连续瞬发多个技能。

### M4：电报与场地压力

- 增加显式 telegraph/recovery 状态。
- 让延时地雷、召唤、远程弹幕等行动有清晰预警和节奏。
- 若引入场地危险，应单独建组件，Brain 只调度，不直接生成所有表现。

### M5：资产迁移与回归

- 新增 Boss 专属 profile 资产。
- `Boss_Phase1/2` 绑定 Boss profile 或由 `BossAIBrain` 读取 phase profile。
- 保留 `EnemyDefinition.AIProfile == null`，避免普通敌人 AI 路径误接管 Boss。

## 测试计划

- EditMode：
  - Boss profile 为空时，Boss 当前旧行为路径不变。
  - Boss Brain passive 模式不触发攻击或技能。
  - Phase 切换会重置 phase 局部冷却，但不重置全局冷却。
  - Boss Brain 激活时，`EnemyDefinitionBinder` 不再旧路径自动释放 Boss 技能。
  - 行动锁期间不会触发其他 Boss 行动。
  - `EnemyGameplayPauseGuard` 暂停时 Boss Brain 不 tick。
- 资产测试：
  - `Boss_Phase1 / Boss_Phase2` 当前仍保持 `EnemyAIProfile == null`。
  - 未来 Boss profile 存在时，不会误绑到 8 个普通敌人。
- 手动 Play：
  - Boss 波能进入。
  - 一阶段技能节奏可读。
  - 二阶段切换不丢 UI、血量、目标、技能冷却。
  - 暂停、背包、Hint、升级页、结算页打开时 Boss 停止行动。

## 暂不做

- 暂不实现行为树编辑器。
- 暂不实现 GOAP。
- 暂不做全局敌人指挥官。
- 暂不重做波次系统。
- 暂不把 Boss 迁到普通 `EnemyAIController`。

## 接手前检查清单

- 先跑普通敌人 AI 回归，确保 `EnemyAIProfile` 路径稳定。
- 确认 Boss 当前体感是否需要保留，尤其是 `BossSmartRoam` 与技能释放节奏。
- 确认是否需要 Boss 专属 prefab，还是继续复用 `CharEnemy.prefab`。
- 确认 Boss 行动是否需要美术/音频电报资源。
- 确认 Boss 二阶段是否只是数值/技能变化，还是需要完整策略变化。
