# 法术系统 M0 基线快照

本文件记录 `Docs/SpellSystemRefactorPlan.md` 的 M0 基线。后续引入法术书执行器、SpellProgram、Modifier、CastBlock、Trigger/Payload 与 Value 扩展时，先用这里的入口清单和测试护栏判断是否出现非预期断裂。

## 当前运行链路入口

| 入口 | 当前真源 | 依赖的编译结果 | M0 护栏 |
| --- | --- | --- | --- |
| 玩家开火 | `PlayerPlaneMovement` + 同物体 `SpellBookLoadout`；缺失时禁用开火，不再回退旧 `AttackFormulaLoadout` | `CompiledSpellProgram` | `SpellBookLoadoutTests.PlayerPlaneMovement_TryResolveSpellProgramForFiring_UsesSpellBookLoadout` 与 `SpellSystemBaselineTests.SpellBookLoadoutBaseline_ExpandsItemsCompilesAndTracksRevision` 固定法术书发射与编译入口 |
| 背包 Spell Book | `BackPackUIScreen` 的 Spell Book cells | `SpellBookLoadout.EquippedItems` / `CurrentCompiledProgram` | 既有 `BackPackUIScreenTests` 与本快照说明共同覆盖 |
| HUD 法术展示 | `MainUIScreen` 订阅 `SpellBookLoadout.Changed` | `SpellBookLoadout.ExecutionItems` | 既有 `MainUIScreenTests` 覆盖 |
| 背包攻击预览 | `BackPackAttackPreviewController.RefreshPreview` | `CompiledSpellProgram` / primary `SpellProjectileNode`；无 `CompiledAttack` refresh overload | 既有 `BackPackAttackPreviewControllerTests` 覆盖 |
| 敌人远程 token 攻击 | `EnemyDefinition.RangedBulletAttackDefinition.spellBook + formulaItems` + `EnemyRangedTokenAttacker` | 缓存的 `CompiledSpellProgram`；运行时 combat 覆写派生临时 `SpellProjectileNode` | `SpellSystemBaselineTests.EnemyBaseline_RangedTokenAttackerCompilesFormulaItemsAndTargetsPlayer` 与 `EnemyAttackExtensionTests.TryPerformAttack_RangedTokenAttacker_UsesSpellBookExecutionItems` |
| Token 奖励 / 选择 | `BulletTokenLibrary` + `CombatEntryTokenSelectionPlan` + `TokenSelectUIScreen` | `PlaceableTokenData` | 既有 `CombatEntryTokenSelectionPlanTests` 与 `TokenSelectModalTests` 覆盖 |
| 法术描述 | `SpellDescriptionGenerator` + `SpellDescriptionCatalogData` | `CompiledSpellProgram` + Spell Book execution items | 既有 `SpellDescriptionGeneratorTests` 覆盖 |

## 当前编译语义快照

- 没有 Core 时不可发射，并产生 error。
- Core 单独存在时可发射，默认 `Straight + DirectDamage`。
- Behavior / Result 至多各接受一个；重复或位置错误 token 只产生 warning 并被忽略。
- Value 只被最近一个等待数值的 Behavior 或 Result 消费；孤立 Value 产生 warning。
- LinkedToken 会展开为内部基础 token；只有整条链被完整接受时才应用整体伤害倍率。
- Spread 是 Behavior，当前在 SpellProgram 路径通过 `SpellProjectileNode.ProjectileCount` 驱动 `AttackProjectileEmitter` 生成多发；旧 `CompiledAttack.GetProjectileCount()` 与旧线性 wrapper 已删除。
- Explosion 是 Result，命中后会先直击主目标，再按半径结算 AoE。
- Split 是 Result，命中后派生子弹会把自身 Result 改成 `DirectDamage`，避免递归分裂；SpellProgram 与 legacy Split 子弹运行时都以 `SpellProjectileNode` 承载该 DirectDamage 子语义。
- 敌人远程 token 攻击从 `EnemyDefinition` 读取 `spellBook + formulaItems` 执行序列，并按敌人运行时 combat 配置派生临时 projectile node 覆盖伤害、射程和可选速度倍率，不修改缓存 program。

## M0 新增测试

`Assets/Editor/Test/SpellSystemBaselineTests.cs` 集中固定以下行为：

- `CompilerBaseline_RequiresCoreAndDefaultsCoreOnlyAttack`
- `CompilerBaseline_ConsumesValuesAndLinkedItemsWithCurrentRules`
- `SpellBookLoadoutBaseline_ExpandsItemsCompilesAndTracksRevision`
- `EmitterBaseline_SpreadAttackSpawnsConfiguredProjectiles`
- `ImpactBaseline_ExplosionDamagesPrimaryAndNearbyTargets`
- `ImpactBaseline_SplitChildrenLoseSplitResult`
- `EnemyBaseline_RangedTokenAttackerCompilesFormulaItemsAndTargetsPlayer`

后续重构时可以改测试内部断言指向新的 `SpellProgram` / 法术书执行器，但测试名称代表的行为边界不能无意删除。

## M1 过渡测试

`Assets/Editor/Test/SpellBookLoadoutTests.cs` 先固定法术书执行器进入系统的最小边界：

- `SpellBookData_BuildExecutionItems_ComposesFixedItemsAndTrimsEquippedSlots`
- `SpellBookLoadout_CompilesFixedCoreWithEquippedBehavior`
- `SpellBookLoadout_SwitchingBooksRebuildsSlotLimitAndRevision`
- `SpellBookLoadout_ResetToStartingItems_RestoresCapturedEquippedItems`
- `PlayerPlaneMovement_TryResolveSpellProgramForFiring_UsesSpellBookLoadout`
- `SpellBookActivationTraits_FlowIntoLoadoutAndPlayerFirePacing`
- `SpellBookLoadout_ActivationEnergy_GatesAndRegeneratesCasts`
- `PlayerPlaneMovement_ActivationEnergy_UsesSpellBookLoadoutGate`
- `SpellBookLoadout_ExecutorModifiers_ApplyWithoutConsumingSlots`
- `CombatEntryTokenSelectionPlanTests.SpellBookRewardAssets_CompileDescribeAndSampleEveryRewardBook`
- `CombatEntryTokenSelectionPlanTests.Plan2Asset_KeepsFirstPassRewardBalanceWeights`

这些测试确认法术书槽位、冷却、每次激活次数、激活扇形、可选能量门槛、执行器原生 modifier、常驻 token、装备 token 裁剪、run reset 起始装备恢复和玩家发射入口已经有可验证行为。当前玩家发射只解析 `SpellBookLoadout`，并会把当前法术书的冷却、激活次数、激活扇形和能量消耗用于 runtime 发射；不占槽的 `executorModifiers` 会在编译时作用到法术书生成的 projectile，并会把 result 目标映射到 result-only payload effect，当前 `TriggerSpellBook` 资产已用原生 `ResultMultiplier *=1.25` 验证其固定 OnHit Explosion payload 的伤害倍率强化，`BindingSpellBook` 资产已用原生 `ResultCount =1` / `ResultDuration *=1.5` 验证其固定 OnHit Control payload 的触发阈值与持续时间强化；背包法术描述会显示内建强化数量和目标明细。奖励库资产级 smoke 会遍历 `SpellBookReward_Lib` 中所有可奖励法术书，固定正权重、唯一 ID / 显示名 / 执行器签名、说明文案 slots / cooldown、真实 `FireCore` 编译、法术书描述和全量抽样边界；第一版资产平衡护栏固定 `Plan2` 中法术书来源约 11.7% 的低频接入、payload modifier / Trigger / Payload Boundary 权重，以及 Quick / Wide / Trigger / Binding / Surge 的奖励书权重层级。断言目标已迁移为 `CompiledSpellProgram` / primary `SpellProjectileNode`。`SpellBookLoadout` 不再公开旧 `CompiledAttack` 兼容属性或方法。`SpellProjectileCompiler` 现在输出内部 `SpellProjectileCompileResult`，SpellProgram 编译主干不再把 `CompiledAttack` 当作 CastBlock 数据容器；系统级 baseline、永久升级、子弹命中、视觉 presenter 与单 projectile 编译测试都已改用 `SpellProgramCompiler` / `SpellProjectileNode`。旧 `AttackFormulaCompiler` public wrapper 与 `CompiledAttack` 数据类已删除，运行时发射入口已迁到 `CompiledSpellProgram` / `SpellProjectileNode`。

`BackPackUIScreenTests` 与 `MainUIScreenTests` 已同步到 `SpellBookLoadout`：背包 UI 编辑 `EquippedItems`，描述和 HUD 展示包含法术书常驻 token 的 `ExecutionItems`。旧 `AttackFormulaLoadout` 已不再作为玩家 fallback 或 run reset 护栏；脚本与场景序列化残留也已删除。

`EnemyAttackExtensionTests.TryPerformAttack_RangedTokenAttacker_UsesSpellBookExecutionItems` 覆盖敌人远程入口的 M1 过渡：敌人定义可选绑定 `SpellBookData`，法术书常驻 token 会与远程 `formulaItems` 合并成 `ExecutionItems` 后再编译，且会通过 `SpellProgramCompiler.Compile(..., spellBook)` 接收同一套执行器原生 modifier。

## M2 过渡测试

`Assets/Editor/Test/SpellProgramCompilerTests.cs` 固定 SpellProgram IR 的第一层边界：

- `Compile_CoreOnly_BuildsSingleOuterCastBlockProjectile`
- `Compile_CurrentBlockModifiers_RecordsModifierNodes`
- `Compile_WithoutCore_PreservesErrorsWithoutProjectileBlock`
- `Emit_WithSpellProgram_UsesProjectileNodeShapeWithoutRuntimeAdapter`

这些测试确认旧线性构筑现在会生成单个外层 `SpellCastBlock` 与 `SpellProjectileNode`，普通 `ModifierTokenData(CurrentBlock)` 会被记录为 block modifier 节点，缺少 Core 的错误会保留到 `CompiledSpellProgram.Messages`，并且 `AttackProjectileEmitter` 已只用 `CompiledSpellProgram` / `SpellProjectileNode` 驱动发射形状；`CharBullet` 在这些 emitter 路径优先读取 `SpellProjectileNode` 的效果/爆炸/视觉快照，不再接收运行时 `CurrentCompiledAttack`。`CompiledSpellProgram.PrimaryCompiledAttack` 与 `SpellProjectileNode.AdapterAttack` 已移除，测试目标也改为直接检查 `Messages` / projectile node 快照。

`SpellBookLoadout_CompilesFixedCoreWithEquippedBehavior` 已新增断言：法术书缓存的 `CompiledSpellProgram` 可通过 primary `SpellProjectileNode` 读取散射数量、Core 和 Behavior，不再依赖旧 `CompiledAttack` adapter 入口。

## M3 过渡测试

`SpellProgramCompilerTests` 继续固定 Modifier 第一版边界：

- `Compile_NextTokenModifier_AppliesAndRecordsModifierScope`
- `Compile_CurrentBlockModifier_AppliesWithoutWaitingForNextToken`
- `Compile_UnboundNextTokenModifier_WarnsWithoutRecordingModifierNode`

这些测试确认正式 `ModifierTokenData` 已进入编译链路：`NextToken` 会绑定到下一个有效 Core / Behavior / Result，`CurrentBlock` 可直接作用于当前单层 attack，孤立 `NextToken` 会给 warning 且不记录到 `SpellModifierNode`。旧 `PreTokenData` / `PostTokenData` 类、`TokenType.Pre/Post` 分支和 `LegacyPre/LegacyPost` origin 已删除，Pre/Post 语义统一用普通 Modifier 表达。

## M4 过渡测试

`SpellProgramCompilerTests` 继续固定 CastBlock / Multicast 第一版边界：

- `Compile_MulticastTwoCores_BuildsSingleCastBlockWithTwoProjectileNodes`
- `Compile_MulticastInsufficientRightSide_WarnsAndKeepsValidProjectile`
- `Compile_MulticastCurrentBlockModifier_AppliesToEveryProjectileNode`
- `Compile_NestedMulticast_IgnoresNestedTokenWithoutWrapping`
- `Emit_WithMulticastProgram_EmitsEveryProjectileNode`

这些测试确认 `MulticastTokenData` 会在单个 outer `SpellCastBlock` 中收集多个 projectile 节点，`CurrentBlock` modifier 会作用到每个被收集的 projectile node，发射器会逐个执行 `PrimaryCastBlock.Projectiles`，运行时不再依赖 adapter。当前边界是：不做 wrapping，不启用 result-only 节点，不展开嵌套 Multicast；右侧不足或嵌套时必须保留清晰 warning。

## M5 过渡测试

`SpellProgramCompilerTests` 继续固定 Trigger/Payload 第一版边界：

- `Compile_TriggerOnHitExplicitPayload_BuildsPayloadBlockWithResultEffect`
- `Compile_TriggerPayloadStatusValue_ConsumesValueAsControlCount`
- `Compile_TriggerPayloadHealingValue_ConsumesDeclaredRadiusSlot`
- `Compile_ResultOnlyPayloadCurrentPayloadRadiusModifier_ModifiesEffectRadius`
- `Compile_ResultOnlyPayloadCurrentPayloadResultModifiers_ModifiesEffectSemantics`
- `Compile_SpellBookExecutorModifiers_ModifyResultOnlyPayloadEffects`
- `CombatEntryTokenSelectionPlanTests.SpellBookAssets_ExposeDistinctExecutorTraits`
- `SpellDescriptionGeneratorTests.GenerateRichText_WithSpellBookResultExecutorTraits_DescribesResultTargets`
- `Emit_WithTriggerPayload_AttachesPayloadBlockToSpawnedBullet`
- `Impact_WithTriggerExplosionPayload_DamagesNearbyTargetOnHit`
- `Impact_WithTriggerHealingPayloadRadius_RestoresNearbyTargets`
- `Impact_WithTriggerControlPayloadRadius_ControlsNearbyTargets`
- `CombatEntryTokenSelectionPlanTests.PayloadAmplifyModifierAsset_AmplifiesCurrentPayloadResultOnlyEffects`
- `CombatEntryTokenSelectionPlanTests.PayloadRadiusModifierAsset_ExpandsCurrentPayloadResultOnlyRadius`
- `CombatEntryTokenSelectionPlanTests.PayloadCountModifierAsset_IncreasesCurrentPayloadResultOnlySplitCount`
- `CombatEntryTokenSelectionPlanTests.PayloadControlFieldModifierAsset_GivesCurrentPayloadControlArea`
- `CombatEntryTokenSelectionPlanTests.HealingAsset_ConsumesRadiusValueForPayloadArea`

这些测试确认当前 Trigger/Payload 采用显式 `PayloadStart/PayloadEnd` token 作为边界。`TriggerTokenData(OnHit)` 会把 payload block 绑定到外层 projectile；payload 内无 Core 时可编译 result-only `SpellPayloadEffectNode`，当前覆盖 Explosion、StatusEffect/Control、Healing 与 Split。result-only payload modifier 首批映射支持 `ImpactRadiusMultiplier` 修饰 Explosion 半径、Healing 范围与 Control 范围，并支持 `ResultCount` / `ResultDuration` / `ResultMultiplier` 改写 Split 数量、Control 阈值/持续、Explosion 延迟/伤害倍率与 Healing 治疗倍率；法术书 executor modifier 也会用同一套 result 目标映射作用到 result-only payload effect，`TriggerSpellBook` 的原生 `ResultMultiplier *=1.25` 是 Explosion payload 的资产级护栏，`BindingSpellBook` 的原生 `ResultCount =1` / `ResultDuration *=1.5` 是 Control payload 的资产级护栏。Healing 资产已声明 Radius 值词消费，`愈 + 三` 可把治疗范围写为 3；运行时范围 Healing payload 会治疗命中点附近目标，半径为 0 时保持旧的主目标单体治疗；Control payload 半径为 0 时保持单体控制，半径大于 0 时会在命中点附近对合法敌人登记控制命中。当前可抽取 token 资产级护栏有四个：`PayloadAmplifyModifier` 以 `CurrentPayload` / `ResultMultiplier *=1.5` 放大当前 payload 内 result-only Explosion 伤害倍率并保持半径不变，`PayloadRadiusModifier` 以 `CurrentPayload` / `ImpactRadiusMultiplier *=1.35` 放大 Explosion / Healing 半径并保持伤害/治疗倍率不变，`PayloadCountModifier` 以 `CurrentPayload` / `ResultCount +=2` 增加 result-only Split 数量并保持 Split 子弹伤害倍率不变，`PayloadControlFieldModifier` 以 `CurrentPayload` / `ImpactRadiusMultiplier =1.25` 把 result-only Control 扩展为范围控制。发射器会把 `SpellProjectileNode` 注入到 `CharBullet`，命中后 `CharBullet` 会执行 OnHit payload。当前不启用嵌套 payload / trigger，也不做 payload wrapping。
