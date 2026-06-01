# 法术系统完整重构计划

## 目标

把当前 `Core + Behavior + Value + Result` 的线性攻击公式，重构为“法术书执行器 + 法术程序”的体系。新体系允许玩家获得不同槽位和特性的法术书，法术书按规则执行 token 序列；token 序列被编译成包含 CastBlock、Modifier、Trigger/Payload 的可执行结构，而不是只产出一个扁平的 `CompiledAttack`。

本计划允许破坏现有兼容性。旧 `AttackFormulaCompiler`、`CompiledAttack`、`AttackSpec`、`Pre/PostTokenData`、Spell Book UI 与 token 资产可以被迁移、替换或删除，但每一轮提交都必须保持项目可编译，并通过本阶段定义的验证。

## M0 原始基线

- 重构前玩家背包 Spell Book 是 5 格线性物品序列，由 `BackPackUIScreen` 同步到 `AttackFormulaLoadout`。
- `AttackFormulaCompiler` 从左到右读取 `PlaceableTokenData`，展开为 `BaseTokenData`，最多接受一个 Core、一个 Behavior、一个 Result。
- `ValueTokenData` 只在紧跟等待数值的 Behavior 或 Result 后生效。
- `PreTokenData` / `PostTokenData` 已有类型，但运行时语义薄弱。
- `CompiledAttack` 是重构前的最终运行时攻击配置，`AttackProjectileEmitter` 和 `CharBullet` 直接执行它。

## 实施状态

- M0 已落地：`Docs/SpellSystemBaselineSnapshot.md` 记录旧链路入口和语义快照，`SpellSystemBaselineTests` 固定核心行为护栏。
- M1 已继续落地：新增 `SpellBookData` 与 `SpellBookLoadout`，法术书可以定义槽位数、冷却、每次激活次数、常驻 token 和常驻 token 插入位置。
- 玩家发射入口已改为只读取 `SpellBookLoadout`；缺失法术书 loadout 或可执行 token 时禁用开火并输出法术书编译警告，不再回退旧 `AttackFormulaLoadout`。
- `Main` 场景玩家已挂载 `SpellBookLoadout`，并绑定默认 `Assets/Data/SpellBooks/ApprenticeSpellBook.asset`。
- 背包 UI 和 HUD 已改为读取 `SpellBookLoadout`；背包格只编辑 `EquippedItems`，描述和 HUD 读取法术书常驻 token 合并后的 `ExecutionItems`。
- Run reset 已只重置 `SpellBookLoadout` 的起始装备，不再回退旧 `AttackFormulaLoadout`。
- 敌人远程攻击已接入法术书执行序列：`EnemyDefinition.RangedBulletAttackDefinition` 可选绑定 `SpellBookData`，其 `formulaItems` 作为法术书装备槽位拼入 `BuildExecutionItems()`，再交给旧编译器 adapter。
- M2 骨架已开始落地：新增 `CompiledSpellProgram`、`SpellCastBlock`、`SpellProjectileNode`、`SpellModifierNode` 与 `SpellProgramCompiler`，旧线性构筑会先被包装为一个外层 `CastBlock`。
- `SpellBookLoadout` 当前只公开 `CompiledSpellProgram` 编译结果；玩家法术书与敌人远程攻击都由 `SpellProgramCompiler` 编译执行序列。旧 `AttackFormulaLoadout` 已不再作为玩家发射或 Run reset fallback 入口。
- `AttackProjectileEmitter` 已提供 `CompiledSpellProgram` 重载，并在 SpellProgram 路径以 `SpellProjectileNode` 决定可发射性、散射数量和散射角；SpellProgram 发射不再把 adapter 注入 `CharBullet.CurrentCompiledAttack`。`SpellDescriptionGenerator` 可直接接收 `CompiledSpellProgram`，主句/效果句已改读 primary `SpellProjectileNode` 快照，结构说明继续来自 `PrimaryCastBlock`。
- M3 最小版已开始落地：新增正式 `ModifierTokenData`，当时旧 `PreTokenData` / `PostTokenData` 先迁移为 legacy 子类；Modifier 复用现有 `TokenModifierDefinition` 作为数值载荷，并在 `SpellModifierNode` 中记录 scope、origin 与 target count。后续 M8 已删除 Pre/Post legacy 子类与专用 TokenType 分支；再往后普通 `ModifierTokenData` 的 `Scope/TargetCount` 也从资产真源中移除，Pre/Post 语义统一改为编译时按位置自适应解析。
- 当前 adapter 已支持普通 modifier 按位置解析为 `NextToken` / `NextN` / `CurrentBlock`：`Modifier + Value + ...` 会先消费紧随其后的 Value 作为目标数量，`Modifier + Multicast + ...` 会把 modifier 绑定到 upcoming CastBlock；payload 内沿用同一套“只向后解析、就近最小作用域”的局部规则，不再依赖资产硬编码 `CurrentPayload`，普通 modifier 也不会自动推断成 `GlobalProgram`。
- M4 单层 CastBlock 已落地：新增 `MulticastTokenData`，`SpellProgramCompiler` 能在第一个 Multicast 处收集右侧多个 projectile segment，并编译为同一个 outer `SpellCastBlock` 下的多个 `SpellProjectileNode`。
- `AttackProjectileEmitter`、玩家发射入口、敌人远程 token 攻击和背包攻击预览已改为执行或展示 `CompiledSpellProgram.PrimaryCastBlock.Projectiles` 中的每个 `SpellProjectileNode`；背包预览已移除 `CompiledAttack` 刷新入口，并直接从 projectile node 解析预览目标层、血量和爆炸范围。旧 `PrimaryCompiledAttack` 过渡字段与 `SpellProjectileNode.AdapterAttack` 已移除，SpellProgram IR 只保留 node 快照。
- 当前 Multicast 不做 wrapping，也不启用 result-only 节点：`双重 + 火 + 冰` 产生两个 projectile；`双重 + 火 + 爆` 解释为一个带爆炸结果的火 projectile，并因右侧不足给出 warning；嵌套 Multicast 也会 warning 后被忽略。
- M5 第一版已开始落地：新增 `TriggerTokenData` 与 `PayloadBoundaryTokenData`，当前 payload 边界采用显式 `PayloadStart/PayloadEnd` token，而不是 UI 分区；`SpellProgramCompiler` 支持 `Core + Trigger(OnHit) + PayloadStart + ... + PayloadEnd`。
- Payload 内第一版支持两类节点：带 Core 的普通 inner projectile，以及 result-only `SpellPayloadEffectNode`（当前覆盖 Explosion、StatusEffect/Control、Healing 与 Split）；`CharBullet` 在 actor 命中后执行 OnHit payload，并对非 Bounce 环境命中执行 payload。
- 当前 Trigger/Payload 护栏：不启用 nested trigger / nested payload，不做 payload wrapping；运行时限制 payload depth 和单次派生 projectile 数。payload 内 modifier 默认只在 payload 内向后绑定，result-only payload 首批映射支持 `ImpactRadiusMultiplier` 修饰 Explosion 半径、Healing 范围与 Control 范围，并支持 `ResultCount` / `ResultDuration` / `ResultMultiplier` 改写 Split 数量、Control 阈值/持续、Explosion 延迟/伤害倍率与 Healing 治疗倍率；法术书 executor modifier 现在也能把这些 result 目标映射到 result-only payload effect。
- M5 补强：result-only Healing payload 会按本次命中目标策略对主命中 actor 结算治疗；result-only Split payload 会按 Split 的 Count/倍率在命中点派生无 payload 的 DirectDamage 子弹，并计入单次 payload 派生 projectile 预算，避免 Trigger/Payload 组合递归失控。
- M6 第一刀已开始落地：新增 `SpellValueParameterKind` 与 `SpellValueParameterUtility`，可消费 Value 的 Behavior/Result token 能声明 Count、Radius 或 Duration 槽位；旧 Spread/Bounce/Pierce、Split/Control 在未显式声明时继续按 Count 兼容。
- 当前外层 `SpellProjectileCompiler` 与 payload result-only 编译共用同一套 Value 消费规则；`Explosion.asset` 已开启 Radius 消费，`爆 + 三` 会把爆炸半径设为 3。`StatusEffect` 可显式声明 Duration 槽位后用 Value 改持续时间；Explosion 的 Duration 槽当前映射为延迟爆炸时间。
- M6 描述闭环已落地：`SpellDescriptionGenerator` 会按同一套 Value 消费规则扫描构筑，并用 `SpellDescriptionCatalog` 的 `valueBindings` / `valueBindingSentenceTemplates` 说明 Value 被哪个 Behavior/Result 消费、落到 Count / Radius / Duration 中的哪个槽位；例如 `爆 + 三` 描述为数值词落到爆炸范围。
- M7 已开始落地：背包攻击预览读取 `CurrentCompiledProgram`，可展示 Multicast 的多个外层 projectile，并保留 Trigger/Payload 载荷而不在刷新预览时直接执行内层 payload。
- M7 继续推进：Token Select 卡片与背包格已识别 `Modifier`、`Multicast`、`Trigger`、`PayloadStart/PayloadEnd`，并显示独立目录与类型 tint；新增 `HasteModifier`、`BlockAmplifyModifier`、`DualCast`、`OnHitTrigger`、`PayloadStart`、`PayloadEnd` 第一批资产。
- 奖励池入口已开始迁移：新增 `SpellProgram_Token_Lib`，并把 `Plan2` 接入这条库，使后段波后 Token Select 可以抽到 Core、Value、Modifier、Multicast、Trigger 与 Payload Boundary。
- M7 法术书奖励入口已落地第一版：新增 `RunRewardOption` 与 `SpellBookRewardLibrary`，`TokenSelectUIScreen` 可同时展示 token 与法术书奖励，`MapRunFlowController` 在初始或波后选择到法术书时会替换玩家 `SpellBookLoadout.SpellBook`。
- M7 描述闭环继续推进：`SpellDescriptionGenerator` 现在读取 `CompiledSpellProgram.PrimaryCastBlock`，可说明 CastBlock 多外层 projectile、Modifier 作用域、Trigger/Payload 命中后载荷内容，并通过 `BackPackUIScreen` 传入当前 `SpellBookData` 说明槽位、冷却、每次激活次数和常驻 token 特性；`SpellDescriptionCatalog` 已新增结构与法术书短句模板。
- 第一批可奖励法术书资产已落地：`WideSpellBook`、`QuickSpellBook`、`TriggerSpellBook` 与 `SpellBookReward_Lib`；`Plan2` 现在同时接入 `SpellProgram_Token_Lib` 和 `SpellBookReward_Lib`。`QuickSpellBook` 固定前置 `HasteModifier`，`WideSpellBook` 固定前置 `BlockAmplifyModifier`，`TriggerSpellBook` 固定后置 `OnHitTrigger + PayloadStart + Explosion + PayloadEnd`，并带原生 payload 结果倍率 `*=1.25`，让三本书在运行构筑上有可验证差异。后续 M8 已把法术书冷却、每次激活次数与激活扇形接入玩家 runtime 发射，敌人远程绑定法术书时也会使用同一套激活扇形；完整数值平衡与手动 Play smoke 仍归入后续。
- M8 第一轮清理已开始：`PlayerPlaneMovement` 不再直接解析或发射 `CompiledAttack`，旧 `AttackFormulaLoadout` 也改为缓存 `CompiledSpellProgram`；`BackPackAttackPreviewController` 的主缓存从 `CompiledAttack` 改为 primary `SpellProjectileNode`。该轮当时的主要残留集中在 `CharBullet` 表现/命中结算、`SpellProgramCompiler` 内部旧编译器适配、SpellDescription 兼容入口和测试护栏，其中 `CharBullet` 与描述路径已由后续 M8 第二/三轮继续迁移。
- M8 第二轮继续推进：`SpellProjectileNode` 已复制运行时语义快照，包括 `CoreEffects`、`ResultEffects`、爆炸配置、缩放/命中半径、文字颜色和字体尺寸修饰步骤；`CharBullet` 在拥有 projectile node 时会优先用它解析 Homing/Bounce、爆炸、分裂、控制、治疗和视觉表现，`CharBulletVisualPresenter` 也优先读取 node。Split 与 payload 派生 projectile 现在通过 `SpellProjectileNode` 发射，旧 `CompiledAttack` 仍作为编译期 adapter 和旧线性入口存在。
- M8 第三轮继续推进：`SpellDescriptionGenerator` 的 `CompiledSpellProgram` 路径已改为读取 primary `SpellProjectileNode` 快照生成主句与效果句；该轮当时的旧 `CompiledAttack` overload 已由后续 M8 第六轮移除。
- M8 第四轮继续推进：旧 `AttackFormulaCompiler` 的实际实现已下沉并改名为内部 `SpellProjectileCompiler`；`AttackFormulaCompiler` 只保留为旧线性 API wrapper，`SpellProgramCompiler` 不再直接调用旧公共编译器。该轮当时仍会生成 `PrimaryCompiledAttack` adapter；该过渡字段已由后续 M8 第十五轮移除。
- M8 第五轮继续推进：`BackPackAttackPreviewController` 已移除 `RefreshPreview(PlayerPlaneMovement, CompiledAttack)` 兼容 overload；预览 target layer / dummy health / explosion hint 改为直接读取 primary `SpellProjectileNode`，对应测试也改为走 `SpellProgramCompiler`。
- M8 第六轮继续推进：`SpellDescriptionGenerator` 已移除旧 `CompiledAttack` 描述 overload，描述 API 只接收 `CompiledSpellProgram`；描述测试改为通过 `SpellProgramCompiler` 构造 program，并从 primary `SpellProjectileNode` 验证爆炸半径、控制时长和值消费语义。
- M8 第七轮继续推进：`SpellBookLoadout` 已移除旧 `CurrentCompiledAttack`、`TryGetCompiledAttack(out CompiledAttack)`、`Recompile()` 和内部 `compiledAttack` 缓存；法术书测试改为直接断言 `CompiledSpellProgram` 与 primary `SpellProjectileNode`。
- M8 第八轮继续推进：`AttackFormulaLoadout` 已移除旧 `CurrentCompiledAttack`、`TryGetCompiledAttack(out CompiledAttack)`、`Recompile()` 和内部 `compiledAttack` 缓存；旧 fallback loadout baseline 改为直接断言 `CompiledSpellProgram` 与 primary `SpellProjectileNode`。
- M8 第九轮继续推进：`PlayerPlaneMovement` 删除旧 `AttackFormulaLoadout` fallback 字段、自动绑定、编译缓存路径和旧失败日志；`MapRunFlowController` 的 Run reset 只恢复 `SpellBookLoadout` 起始装备；`SpellBookLoadoutTests`、`MapRunFlowControllerTests`、`RuntimeSaveServiceTests` 与 `SpellSystemBaselineTests` 已改到法术书 loadout 护栏。
- M8 第十轮继续推进：删除 `AttackFormulaLoadout.cs` / `.meta`，并清理 `Main.unity` 与 `_Recovery` 场景中的旧组件 fileID、旧 `attackFormulaLoadout` 字段和旧脚本 GUID 残留；静态搜索已确认项目内不再出现 `AttackFormulaLoadout`；删除后目标 EditMode `SpellBookLoadoutTests` / `MapRunFlowControllerTests` / `RuntimeSaveServiceTests` / `SpellSystemBaselineTests` 合计 39/39 通过，Console error 为 0。
- M8 第十一轮继续推进：SpellProgram 路径的 Split / result-only Split payload 派生子弹不再 `CompiledAttack.Clone()`，改由当前 `SpellProjectileNode` 派生 DirectDamage 子节点；该轮当时 legacy `CompiledAttack` 发射路径仍保留 clone 兼容，后续 M8 第十五轮已删除 `CompiledAttack.Clone()`。
- M8 第十二轮继续推进：SpellProgram 发射路径不再把 `projectile.AdapterAttack` 传入 `CharBullet.CurrentCompiledAttack`，运行时子弹只保存 `CurrentProjectileNode` 与其 `AttackSpec`；敌人远程攻击的伤害、射程和速度倍率覆写改为派生临时 `SpellProjectileNode`，避免反复修改缓存 adapter；legacy Split 子弹测试也改为断言 node 语义。
- M8 第十三轮继续推进：旧 `AttackProjectileEmitter.Emit(CompiledAttack)` 重载改为先从 adapter 创建 `SpellProjectileNode`，再复用 projectile node 发射路径；因此通过 emitter 发射的 legacy compiled attack 子弹运行时也不再携带 `CurrentCompiledAttack`。旧线性影响测试改为检查 `CurrentProjectileNode` / `CurrentAttackSpec`。
- M8 第十四轮继续推进：`SpellProjectileNode.CreateRuntimeSnapshotFromCompiledAttack` 已新增，旧 `AttackProjectileEmitter.Emit(CompiledAttack)` 与直接 `CharBullet.InitializeShot(..., CompiledAttack)` 都会复制旧编译结果为运行时 node；`CharBullet` 删除 split / homing 上最后的 adapter fallback，Split 子弹只从当前 `SpellProjectileNode` 派生 DirectDamage 子节点。旧入口兼容测试当时补充了 runtime node 不保留 adapter 的护栏；后续 M8 第十五轮已移除 `SpellProjectileNode.AdapterAttack` 属性，相关测试改为直接检查 projectile node 快照。
- M8 第十五轮继续推进：`CompiledSpellProgram.PrimaryCompiledAttack` 与 `SpellProjectileNode.AdapterAttack` 已移除，SpellProgram IR 只保留 `Messages`、`SpellCastBlock` 和 projectile node 快照；`CompiledAttack.Clone()` 也已删除。相关测试改为直接断言 `CompiledSpellProgram.Messages`、`PrimaryCastBlock.Projectiles`、modifier node 与 projectile runtime snapshot。
- M8 第十六轮继续推进：`CharBulletVisualPresenter.ApplyCompiledAppearance(CompiledAttack, ...)` 与旧 `CompiledAttack` 颜色 resolver 已移除，presenter 外观刷新只接收 `SpellProjectileNode` 或回退当前 bullet 语义；测试改为从 projectile node 入口断言文字颜色、符文底座和拖尾同步。
- M8 第十七轮继续推进：`CharBullet.InitializeShot(..., CompiledAttack)` 和内部 `shotCompiledAttack` fallback 已移除，子弹初始化只接收 `SpellProjectileNode` 运行时语义快照；旧影响测试显式用 `SpellProjectileNode.CreateRuntimeSnapshotFromCompiledAttack(...)` 生成 node 后再初始化子弹。
- M8 第十八轮继续推进：`AttackProjectileEmitter.Emit(CompiledAttack)` 及相关旧重载已移除，emitter 只接收 `CompiledSpellProgram`；旧发射测试改为先用 `CompiledSpellProgram.CreateFromCompiledAttack(...)` 包装 adapter 输出，再走正式 emitter 路径。
- M8 第十九轮继续推进：新增内部 `SpellProjectileCompileResult`，`SpellProjectileCompiler` 不再输出 `CompiledAttack`；`SpellProgramCompiler` 的单发、Multicast 与 payload projectile 编译路径都直接消费内部 compile result 并生成 `SpellProjectileNode`，旧 `CompiledSpellProgram.CreateFromCastBlock(IReadOnlyList<CompiledAttack>...)` 工厂也已移除。`AttackFormulaCompiler` 当时仍作为旧线性 wrapper，把内部结果显式转换为 `CompiledAttack` 供旧测试护栏使用；该过渡已由 M8 第二十一轮删除。
- M8 第二十轮继续推进：`SpellSystemBaselineTests`、`PermanentUpgradeServiceTests`、`CharBulletImpactTests` 与 `CharBulletVisualPresenterTests` 已从旧 `CompiledAttack` helper 迁到 `SpellProgramCompiler` / `SpellProjectileNode`，系统级 baseline、永久升级、子弹命中和视觉表现护栏不再依赖旧 wrapper。
- M8 第二十一轮继续推进：`AttackFormulaCompilerTests` 已迁移并重命名为 `SpellProgramProjectileCompilerTests`，发射、视觉、命中、Value、LinkedItem 与普通 Modifier 等单 projectile 护栏都直接走 `SpellProgramCompiler` / `SpellProjectileNode`；旧 `CompiledSpellProgram.CreateFromCompiledAttack(...)`、`SpellProjectileNode.CreateFromCompiledAttack(...)` / `CreateRuntimeSnapshotFromCompiledAttack(...)`、`AttackFormulaCompiler` public wrapper 与 `CompiledAttack` 数据类已删除，文件也重命名为 `SpellProjectileCompiler.cs` 与 `AttackCompilation.cs`。
- M8 第二十二轮继续推进：旧 `PreTokenData` / `PostTokenData` 类、`.meta`、`TokenType.Pre/Post` 分支、`SpellModifierOrigin.LegacyPre/LegacyPost`、`SpellProjectileCompileResult.PreTokens/PostTokens` 和对应测试 helper 已删除；原先 Pre/Post 的“前置/后置修饰”护栏改为普通 `ModifierTokenData(CurrentBlock)`，并继续通过 `SpellModifierOrigin.ModifierToken` 记录。
- M8 第二十三轮继续推进：result-only payload modifier 首批扩展已落地，新增 `ResultCount`、`ResultDuration`、`ResultMultiplier` 三类 result 目标，并把它们映射到 Split 数量、Control 阈值/持续、Explosion 延迟/伤害倍率与 Healing 治疗倍率；旧有 `ImpactRadiusMultiplier` 当时继续修饰 Explosion 半径，后续 M8 第三十三轮已扩展到 Healing 范围。
- M8 第二十四轮继续推进：`SpellBookData` 新增激活扇形角，`PlayerPlaneMovement` 会用当前法术书的冷却、每次激活次数和激活扇形执行一次开火；`AttackProjectileEmitter` 可按激活次数重复执行同一个 `CompiledSpellProgram`，并把多轮 cast 均匀展开到扇形内；`EnemyRangedTokenAttacker` 在敌人远程绑定法术书时复用同一套激活扇形。`WideSpellBook` 现在是 7 槽慢冷却、固定 CastBlock amplify、每次 2 轮 10 度扇形的执行器。
- M8 第二十五轮继续推进：`SpellBookData` 新增可选能量容量、每秒恢复和每次激活消耗；`SpellBookLoadout` 维护运行时能量池并在玩家发射时 gating / consume / regen。默认能量容量或消耗为 0 时不启用资源门槛，旧法术书行为不变。新增 `SurgeSpellBook` 作为爆发型执行器：5 槽、0.18 秒冷却、每次 3 轮 8 度扇形、3 点能量容量、每次消耗 1、每秒恢复 0.75，并已加入 `SpellBookReward_Lib`。
- M8 第二十六轮继续推进：`SpellBookData` 新增 `executorModifiers`，`SpellProgramCompiler.Compile(..., spellBook)` 会在玩家 `SpellBookLoadout` 与敌人远程法术书编译时应用不占槽的执行器原生 modifier；这一轮先作用于外层 projectile、多播 projectile 和 payload 内带 Core 的 projectile。`TokenModifierTarget` 新增 `Damage`，`QuickSpellBook` 原生伤害 `*=0.85`，`SurgeSpellBook` 原生伤害 `*=0.8`。验证：Kernel / EditMode csproj build 均 0 warning / 0 error，定向法术回归 97/97，通过扩展系统回归 227/227，Console error 0。
- M8 第二十七轮继续推进：法术书 executor modifier 现在也能作用到 result-only payload effect。`ImpactRadiusMultiplier` 当时映射 Explosion 半径，后续 M8 第三十三轮已扩展到 Healing 范围；`ResultCount` 映射 Split 数量 / Control 阈值，`ResultDuration` 映射 Explosion 延迟 / Control 持续，`ResultMultiplier` 映射 Explosion / Split / Healing 倍率；`Damage` 仍通过外层 projectile base damage 流入 payload 伤害，不在 result effect multiplier 上重复乘。验证：核心 `SpellProgramCompilerTests` 28/28、定向法术回归 98/98、扩展系统回归 228/228 通过，Console error 0。
- M8 第二十八轮继续推进：`TriggerSpellBook` 的执行器特性开始从“固定 token 组合”深入到“法术书原生 payload bonus”，新增 `ResultMultiplier *=1.25`，使其固定 `OnHit -> Explosion` payload 的爆炸伤害倍率提高 25%，同时不改变爆炸半径和外层 projectile 碰撞半径。`CombatEntryTokenSelectionPlanTests.SpellBookAssets_ExposeDistinctExecutorTraits` 现在比较 Trigger 法术书无执行器加成 / 带执行器加成两版 payload effect。验证：Kernel / EditMode csproj build 均 0 warning / 0 error，定向核心回归 43/43、扩展系统回归 228/228 通过，Console error 0。
- M8 第二十九轮继续推进：背包法术描述不再只显示法术书“内建强化 N 项”，现在会列出执行器 modifier 的目标明细，例如 `伤害 x0.85`、`速度 x1.1`、`结果倍率 x1.25`。`SpellDescriptionGeneratorTests.GenerateRichText_WithSpellBook_DescribesExecutorTraits` 已固定这些短句，便于玩家理解不同法术书 bonus。验证：Kernel / EditMode csproj build 均 0 warning / 0 error，描述相关目标回归 56/56、扩展系统回归 228/228 通过，Console error 0。
- M8 第三十轮继续推进：新增 `PayloadAmplifyModifier` 资产，作为玩家可抽取的 `CurrentPayload` / `ResultMultiplier *=1.5` 样本，并加入 `SpellProgram_Token_Lib`。`CombatEntryTokenSelectionPlanTests.PayloadAmplifyModifierAsset_AmplifiesCurrentPayloadResultOnlyEffects` 固定其在 `OnHit + PayloadStart + Explosion + PayloadEnd` 中只放大 result-only Explosion 伤害倍率、不改变爆炸半径的边界。验证：Kernel / EditMode csproj build 均 0 warning / 0 error，新增资产语义测试 2/2、描述/选择/法术书/编译目标回归 57/57、扩展系统回归 229/229 通过，Console error 0。
- M8 第三十一轮继续推进：新增 `PayloadRadiusModifier` 资产，作为第二个玩家可抽取的 `CurrentPayload` 样本，并加入 `SpellProgram_Token_Lib`；它通过 `ImpactRadiusMultiplier *=1.35` 放大当前 payload 内 result-only Explosion 半径，同时保持伤害倍率不变。`CombatEntryTokenSelectionPlanTests.PayloadRadiusModifierAsset_ExpandsCurrentPayloadResultOnlyRadius` 固定该边界。验证：Kernel / EditMode csproj build 均 0 warning / 0 error，新增 payload modifier 资产语义测试 3/3、描述/选择/法术书/编译目标回归 58/58、扩展系统回归 230/230 通过，Console error 0。
- M8 第三十二轮继续推进：新增 `PayloadCountModifier` 资产，作为第三个玩家可抽取的 `CurrentPayload` 样本，并加入 `SpellProgram_Token_Lib`；它通过 `ResultCount +=2` 增加当前 payload 内 result-only Split 派生数量，同时保持 Split 子弹伤害倍率不变。`CombatEntryTokenSelectionPlanTests.PayloadCountModifierAsset_IncreasesCurrentPayloadResultOnlySplitCount` 固定该边界。验证：Kernel / EditMode csproj build 均 0 warning / 0 error，payload modifier 资产语义测试 4/4、描述/选择/法术书/编译目标回归 59/59、扩展系统回归 231/231 通过，Console error 0。
- M8 第三十三轮继续推进：更细 result 语义落地到 Healing。`ResultEffectPayload` 新增通用 `effectRadius`，`Healing` 可声明 Radius 值词消费，真实 `Healing.asset` 已开启 Radius；`ImpactRadiusMultiplier` 现在也能修饰 result-only Healing 范围。运行时 Healing payload 在半径为 0 时保持单体治疗，半径大于 0 时按命中点做范围治疗；普通 Healing 结果若带半径，也会跳过已直击治疗的主目标并治疗附近合法目标。`SpellProgramCompilerTests.Compile_TriggerPayloadHealingValue_ConsumesDeclaredRadiusSlot`、`Impact_WithTriggerHealingPayloadRadius_RestoresNearbyTargets`、`SpellDescriptionGeneratorTests.GenerateRichText_WithHealingRadiusValue_DescribesValueConsumerSlot` 与 `CombatEntryTokenSelectionPlanTests.HealingAsset_ConsumesRadiusValueForPayloadArea` 固定编译、描述、真实资产和运行时边界。验证：Kernel / EditMode csproj build 均 0 warning / 0 error（一次并行 build 命中既有 `Temp\obj\Lilith.Kernel.dll` 文件锁，顺序重跑通过），新增窄测试 5/5、法术/描述/资产/子弹目标回归 146/146、扩展系统回归 235/235 通过，Console error 0；静态旧 adapter / Pre-Post / Vocalith 反向引用 / 旧 UI Text 护栏均无命中，`git diff --check` 仅报告既有 LF/CRLF 提示。
- M8 第三十四轮继续推进：新增 `BindingSpellBook` 作为控制倾向执行器，提供 5 槽、0.32 秒冷却，固定后置 `OnHitTrigger + PayloadStart + Control + PayloadEnd`，并用原生 `ResultCount =1` / `ResultDuration *=1.5` 把 result-only Control payload 调整为一次命中触发、持续 1.5 倍；该法术书已加入 `SpellBookReward_Lib`，并由 `CombatEntryTokenSelectionPlanTests.SpellBookAssets_ExposeDistinctExecutorTraits` 和 `SpellDescriptionGeneratorTests.GenerateRichText_WithSpellBookResultExecutorTraits_DescribesResultTargets` 固定资产语义与描述输出。本轮同时修正 `EnemyResultVisualFeedbackTests` 过旧夹具：`Text` 容器补 `RectTransform`，并把测试脉冲持续时间拉长以避免 EditMode `Time.deltaTime` 吃掉短脉冲。验证：Kernel / EditMode csproj build 均 0 warning / 0 error；Binding 窄测试 5/5、法术书/选择/描述/背包目标回归 104/104、失败夹具复跑 3/3、全 EditMode 506/506 通过，Console error 0；静态旧 adapter / Pre-Post / Vocalith 反向引用 / 旧 UI Text 护栏均无命中，`git diff --check` 仅报告既有 LF/CRLF 提示。
- M8 第三十五轮继续推进：补上真实奖励库的资产级 Play-smoke 替代护栏。`CombatEntryTokenSelectionPlanTests.SpellBookRewardAssets_CompileDescribeAndSampleEveryRewardBook` 会遍历 `SpellBookReward_Lib` 中每本可奖励法术书，验证权重为正、ID / 显示名 / 执行器签名唯一、奖励说明包含 slots 和 cooldown、常驻 token 引用非空、用真实 `FireCore` 经 `SpellProgramCompiler.Compile(..., spellBook)` 可编译可发射且无 error、背包描述能写出法术书名 / 槽位 / 冷却，并验证按库大小抽样可抽到全部奖励书。本轮因此修正 `SurgeSpellBook` 奖励说明缺少 cooldown 的文案缺口。验证：Kernel / EditMode csproj build 均 0 warning / 0 error；新增 smoke 窄测试 1/1、法术书/选择/描述/地图奖励目标回归 38/38、全 EditMode 507/507 通过，Console error 0；静态旧 adapter / Pre-Post / Vocalith 反向引用 / 旧 UI Text 护栏均无命中，`git diff --check` 仅报告既有 LF/CRLF 提示。
- M8 第三十六轮继续推进：更细 result 语义落到 Control 范围。`ImpactRadiusMultiplier` 现在也能作用到 result-only Control payload 的 `effectRadius`，运行时 `CharBullet` 会在半径大于 0 时以命中点为中心对附近合法敌人登记控制命中；半径为 0 时仍保持旧的单体控制。新增 `PayloadControlFieldModifier` 资产作为第四个可抽取 `CurrentPayload` 样本，以 `ImpactRadiusMultiplier =1.25` 把当前 payload 内 result-only Control 扩展成范围控制，并加入 `SpellProgram_Token_Lib`。验证：Kernel / EditMode csproj build 均 0 warning / 0 error；新增控制范围窄测 4/4、法术编译/奖励库/描述/子弹目标回归 81/81、全 EditMode 509/509 通过，Console error 0；静态旧 adapter / Pre-Post / Vocalith 反向引用 / 旧 UI Text 护栏均无命中，`git diff --check` 仅报告既有 LF/CRLF 提示。
- M8 第三十七轮继续推进：补上第一版资产平衡护栏。`CombatEntryTokenSelectionPlanTests.Plan2Asset_KeepsFirstPassRewardBalanceWeights` 固定 `Plan2` 中 `SpellProgram_Token_Lib` 来源权重 0.45、`SpellBookReward_Lib` 来源权重 0.35，确保法术书奖励来源约占全部奖励来源 11.7% 且低于新 token 来源；同时固定 payload modifier / Trigger / Payload Boundary 权重和五本奖励法术书的第一版权重层级（Quick 1、Wide 0.9、Trigger 0.75、Binding 0.7、Surge 0.65），避免爆发 / 控制执行器或 payload 专用 modifier 在后续资产编辑中意外过量出现。验证：Kernel / EditMode csproj build 均 0 warning / 0 error；新增平衡窄测 1/1、`CombatEntryTokenSelectionPlanTests` 13/13、全 EditMode 510/510 通过，Console error 0；静态旧 adapter / Pre-Post / Vocalith 反向引用 / 旧 UI Text 护栏均无命中，`git diff --check` 仅报告既有 LF/CRLF 提示。
- 当前阶段 `CompiledAttack`、`AttackFormulaCompiler`、`PreTokenData` 与 `PostTokenData` 已从运行时代码、测试代码和生成工程文件中消失；`SpellProjectileCompiler` 只作为 SpellProgram 的内部单 projectile 编译器存在。M8 自动化代码面已不再需要围绕旧 adapter、Pre/Post legacy 子类或首批 result-only modifier 映射删除/打通，第一版资产平衡也已有自动护栏；当前收口后只保留一次手动 Play smoke 作为 M8 剩余验证，更多法术书 bonus、更多可抽取 payload modifier 和进一步 result 语义建议进入后续新阶段。当前可奖励法术书已覆盖默认、宽域、快书、爆发、爆炸触发与控制绑定六种执行器方向，奖励库已有自动资产级 smoke 和权重护栏，且可抽取 payload modifier 已覆盖伤害倍率、范围、数量与控制场四个方向。

## 目标概念

### 法术书执行器

新增“法术书”概念。法术书是玩家可获得、可替换的执行器，不只是 UI 容器。

法术书应至少包含：

- 槽位形状或槽位数量。
- 每次执行从 token 序列中读取的规则。
- 冷却 / 施法间隔。
- 可选能量、热量或充能模型。
- 可选常驻 token，也就是执行器自带的 always-cast 风格能力。
- 可选执行器原生 modifier / bonus，不占装备槽位，用于表达法术书自身的伤害、速度、范围等倾向。
- 可选特性标签，例如快速书、重载书、触发书、巨量槽位书。

第一版不要求做完整数值平衡，但必须让不同法术书在运行行为上有可验证差异。

### 法术程序

当前 `CompiledAttack` 应升级或替换为 `CompiledSpellProgram`。它不是单个攻击结果，而是法术执行图。

建议核心结构：

```text
SpellBookExecutor
  SpellProgram
    CastBlock
      ModifierNode*
      ProjectileNode*
      PayloadBlock*
```

第一版可以先用树或块状 IR，不急着做通用图结构。关键是要明确外层、内层、作用域与执行顺序。

### Modifier

把 Pre/Post 正式升级为 Modifier。Modifier 是 token，不再只是“前后缀”。

Modifier 至少支持这些作用域：

- `NextToken`：修饰下一个可被修饰 token。
- `NextN`：修饰后 N 个可被修饰 token。
- `CurrentBlock`：修饰当前 CastBlock。
- `CurrentPayload`：修饰当前 Trigger/Payload 内部。
- `GlobalProgram`：修饰整次法术程序。

第一版建议先实现 `NextToken`、`CurrentBlock`、`GlobalProgram`，其他作用域保留数据结构和测试用例位置。

### CastBlock

CastBlock 是一次施法中被共同执行的一组节点。它解决“多个 projectile 或 result 怎么被同一次施法收集”的问题。

第一版新增 Multicast token，例如：

```text
双重 + 火 + 冰
三重 + 雷 + 雷 + 锋
加速 + 双重 + 火 + 爆
```

Multicast 不应只是 Spread 的别名。Spread 是一个 projectile 行为；Multicast 是执行结构，表示同一 CastBlock 中收集多个可执行节点。

### Trigger/Payload

Trigger 把法术分成外层载体和内层载荷。

示例语义：

```text
火 + 触发 + [爆 + 定 + 三]
```

含义：外层发射火弹；火弹命中后，在命中点执行 payload，payload 里产生爆炸和控制三次。

第一版 Trigger 至少支持：

- `OnHit`：命中目标或环境后执行 payload。
- Payload 可以使用内层 CastBlock。
- Payload 必须可防递归失控，例如最大深度、最大节点数、最大派生 projectile 数。

延迟触发、计时触发、死亡触发、区域触发可以后续扩展。

### Value 小幅扩展

Value 从“只给 Behavior/Result 填数”升级为上下文参数。

第一版支持：

- Count：数量，例如发数、弹跳次数、分裂数量、控制触发次数。
- Radius：范围，例如爆炸半径、治疗范围。
- Duration：持续时间，例如减速、控制、延迟。

Value 的解释由消费它的 token 声明，不由 Value 自己硬编码全部语义。

## 非目标与边界

- 不在第一轮实现 Noita 风格 wrapping。Wrapping 很强，但可读性和调试成本高，等 CastBlock / Payload / UI 描述稳定后再评估。
- 不在第一轮实现 shuffle 法术书。执行器先保持可解释的左到右规则。
- 不在第一轮做完整法力经济和平衡。可以预留能量字段，但只要求一种可验证资源或冷却差异。
- 不把 UI 美术作为本重构主目标。UI 只做必要的信息呈现、槽位支持和调试可视化。
- 不改地图、波次、敌人 AI、存档主流程，除非它们直接依赖法术发射入口。
- 不在 `Assets/Scripts/Vocalith/**` 新增 `Kernel.*` 引用。
- 不新增 `UnityEngine.UI.Text`，法术描述和调试文本继续用 TMP。

## 里程碑

### M0：基线冻结与测试护栏

目标：

- 列出当前法术链路所有入口：背包同步、玩家开火、敌人远程攻击、预览子弹、token 奖励、描述生成。
- 为当前行为补最小基线测试，作为重构前的“行为快照”。

范围：

- 只新增或整理测试与文档，不改变运行时语义。

可验证目标：

- 编译通过。
- 现有相关 EditMode 测试通过。
- 至少覆盖：无 Core 不可发射、有 Core 可发射、Value 上下文消费、LinkedToken 展开、Spread 多发、Explosion 命中效果、Split 防递归。

停止条件：

- 当前链路入口不完整，无法判断哪些系统依赖 `CompiledAttack`。
- 基线测试不稳定，且失败原因不是本轮可解释的既有问题。

### M1：法术书执行器数据模型

目标：

- 引入 `SpellBookData` 或同等 ScriptableObject，作为玩家可获得的执行器配置。
- 引入运行时 `SpellBookLoadout` 或替换 `AttackFormulaLoadout`，把“槽位物品序列”和“执行器配置”分开。

范围：

- 可直接破坏旧 `AttackFormulaLoadout` API，但必须同步玩家、背包和敌人远程攻击入口。
- 先支持线性槽位，形状槽位可以只预留结构。

可验证目标：

- 玩家能使用默认法术书开火。
- 两本测试法术书至少在槽位数、冷却或常驻 token 上有可验证差异。
- 背包 Spell Book UI 能显示或绑定当前法术书槽位。

循环验证：

1. 静态搜索所有 `AttackFormulaLoadout` 引用并迁移。
2. 编译。
3. 跑玩家开火、背包、敌人远程攻击相关 EditMode 测试。
4. 读 Unity Console error。

停止条件：

- 新执行器无法同时服务玩家和敌人远程攻击。
- UI 槽位模型与运行时槽位模型出现两套真源。

### M2：SpellProgram IR 与兼容编译器替换

目标：

- 新增 `SpellProgram`、`CastBlock`、`SpellNode` 等 IR。
- 旧 `Core + Behavior + Result` 编译路径改为生成一个最简单的外层 CastBlock。
- `AttackProjectileEmitter` 和 `CharBullet` 从读取 `CompiledAttack` 过渡到读取 `SpellProgram` 执行结果。

范围：

- 第一版可以保留 `AttackSpec` 作为 projectile 节点的底层数值载体。
- `CompiledAttack` 可以临时作为 adapter，但最终应收敛到 `CompiledSpellProgram`。

可验证目标：

- 所有现有核心、行为、结果 token 都能被编译为等价的单层 SpellProgram。
- 现有常用构筑表现与 M0 快照一致。
- SpellDescription 能从 SpellProgram 生成描述，或通过 adapter 保持描述可用。

循环验证：

1. 每迁移一个入口，跑对应测试。
2. 每删除一个旧 API，`rg` 确认没有残留误用。
3. 每改 `CharBullet` 命中逻辑，跑 impact / result / pause 相关测试。

停止条件：

- IR 结构开始反向依赖 UI 或资产层。
- 旧 `CompiledAttack` 与新 `SpellProgram` 长时间并行导致行为分叉。

### M3：Modifier 第一版

目标：

- 把 `PreTokenData` / `PostTokenData` 替换或迁移为 `ModifierTokenData`。
- Modifier 能声明 target、operation、scope 和可选参数。
- 编译器按左到右规则解析 Modifier 并应用到目标节点或块。

范围：

- 第一版实现 `NextToken`、`CurrentBlock`、`GlobalProgram`。
- 先支持已有 modifier 能力：文字颜色、字体大小、缩放、速度、寿命、射程、命中半径。
- 新增 1 到 2 个 gameplay modifier，例如增伤、扩大范围或降低冷却。

可验证目标：

- `加速 + 火` 只加速火弹。
- `加速 + 双重 + 火 + 冰` 在 CurrentBlock 作用域下能同时影响火和冰。
- `GlobalProgram` modifier 能影响 payload 内外的指定目标，且描述中能说明作用范围。

循环验证：

1. 每加一种 scope，先写编译器单元测试。
2. 再写 runtime 执行测试。
3. 最后验证 SpellDescription 输出。

停止条件：

- Modifier 的消费规则无法通过描述系统清楚解释。
- Modifier 应用顺序出现不可预测行为，且没有调试输出可定位。

### M4：CastBlock 与 Multicast

目标：

- 引入 Multicast token，让一个 CastBlock 能收集多个 projectile 或 result 节点。
- 区分 Behavior 的多发和 Multicast 的多节点执行。

范围：

- 第一版支持固定数量 Multicast：双重、三重。
- 不做 wrapping；Multicast 右侧不足时按 warning 或 compile error 处理。

可验证目标：

- `双重 + 火 + 冰` 同次施法产生两枚不同核心 projectile。
- `双重 + 火 + 爆` 能表达一个火 projectile 与一个爆炸结果节点，或按规则拒绝并给出清晰 warning。具体规则必须在实现前写入测试。
- CurrentBlock modifier 能作用于整个 Multicast 收集结果。

循环验证：

1. 编译器测试覆盖收集数量、右侧不足、嵌套限制。
2. Runtime 测试覆盖 projectile 数量、方向、伤害、视觉描述。
3. 背包预览验证不会无限生成。

停止条件：

- Multicast 与 Behavior/Result 的边界无法稳定定义。
- 一个 token 同时被多个 CastBlock 消费而没有明确规则。

### M5：Trigger/Payload 第一版

目标：

- 引入 Trigger token 和 PayloadBlock。
- 支持外层 projectile 命中后执行内层 payload。

范围：

- 第一版只支持 `OnHit`。
- Payload 需要显式边界。当前实现选择 `PayloadStart/PayloadEnd` token，不使用 UI 层分隔槽位。
- 必须设置最大递归深度、最大 payload 节点数、最大派生 projectile 数。

可验证目标：

- `火 + 触发 + [爆]`：火弹命中后在命中点爆炸。
- `冰 + 触发 + [定 + 三]`：冰弹命中后触发三次控制语义或对应控制参数。
- Payload 内的 Modifier 只影响 payload，除非 scope 声明为 GlobalProgram。
- Split、Explosion、Healing 与 Trigger 组合不会递归失控。

循环验证：

1. 先测试编译结构：外层与内层分离。
2. 再测试命中事件触发 payload。
3. 再测试暂停、预览、敌人远程攻击是否安全。
4. 最后测试描述系统能明确说出“命中后释放”。

停止条件：

- Payload 执行需要在 `CharBullet` 中堆积大量特殊分支，无法保持可读。
- 缺少 recursion guard 或派生数量 guard。

### M6：Value 语义扩展

目标：

- Value 不再只由编译器硬编码给少数 Behavior/Result。
- 可消费 Value 的 token 自己声明参数槽，例如 Count、Radius、Duration。

范围：

- 第一版只做 Count、Radius、Duration。
- 不做复杂表达式，不做变量引用。

可验证目标：

- `散 + 三` 解释为三发。
- `爆 + 三` 可以解释为范围或延迟，取决于 Explosion token 的参数槽声明。
- `定 + 三` 解释为控制次数或持续时间，取决于 Control token 的参数槽声明。
- 孤立 Value 给出 warning，不静默失败。

循环验证：

1. 资产声明测试。
2. 编译器消费测试。
3. 描述输出测试。
4. Runtime 数值效果测试。

停止条件：

- 同一个上下文里 Value 有多个可能消费者且无法稳定消歧。
- 描述无法说明 Value 被谁消费。

### M7：UI、奖励和资产迁移

目标：

- 背包、Token Select、HUD、描述、预览和奖励池全部切到新体系。
- 创建第一批新法术书资产和新 token 资产。

范围：

- 可以删除旧 token 资产或批量迁移。
- 不追求最终美术，只保证功能和描述清楚。

可验证目标：

- 玩家能获得不同法术书。
- Token Select 能奖励 Core、Modifier、Multicast、Trigger、Value 等新 token。
- 背包描述能解释 CastBlock、Trigger/Payload、Value 消费和法术书特性。
- 背包预览能安全展示常见构筑，不因 Trigger/Payload 无限触发。

循环验证：

1. 每类资产先做最小样本。
2. 跑 token 库、选择计划、背包、描述、预览测试。
3. Unity Console error 为 0。
4. 手动 Play 验证一轮获取、装备、开火、命中、替换法术书。

停止条件：

- UI 中出现两个独立的法术真源。
- 奖励池能发出无法放置或无法解释的 token。

### M8：清理旧系统与稳定化

目标：

- 删除旧 `AttackFormulaCompiler` / `CompiledAttack` / 旧 Pre/Post 残留，或把它们明确标记为已废弃并无运行时引用。当前 `AttackFormulaCompiler`、`CompiledAttack`、`PreTokenData` 与 `PostTokenData` 已删除，Pre/Post 语义已收敛到普通 `ModifierTokenData`。
- 更新 README、DesignGuide、SpellDescriptionCatalog、相关测试和记忆。

范围：

- 清理只限法术系统相关文件。
- 不做无关格式化或项目设置整理。

可验证目标：

- `rg "AttackFormulaCompiler|CompiledAttack|PreTokenData|PostTokenData|LegacyPre|LegacyPost"` 在 `Assets/**` 代码和测试中无命中。
- 全部目标 EditMode 测试通过。
- Unity Console error 为 0。
- README 的“攻击与子弹”段落描述新架构。

停止条件：

- 旧系统仍被玩家、敌人或 UI 任一路径使用。
- 新系统没有足够测试覆盖就删除旧测试护栏。

## 全局循环验证

每个开发循环按以下顺序执行：

1. 明确本轮只改一个层级：数据模型、编译器、运行时执行、UI、资产或测试。
2. `rg` 搜索受影响旧入口，确认没有遗漏真源。
3. 用最小补丁修改代码或资产。
4. 运行窄测试。
5. Unity refresh/compile。
6. 读取 Console error。
7. 检查 `git diff --check`，若 Unity YAML 或既有资产空白阻塞，至少对本轮 C# / JSON / Markdown 文件单独检查。
8. 更新对应文档或描述 catalog。
9. 记录本轮剩余风险。

每完成一个里程碑，再执行一次更宽验证：

- 相关 EditMode 测试全跑。
- 背包 UI 手动 Play smoke。
- 玩家开火 smoke。
- 敌人远程攻击 smoke。
- 背包预览 smoke。
- 一次 token 奖励选择 smoke。

## 全局停止条件

任一条件成立时停止推进并先修正或询问：

- Unity 编译错误无法在当前里程碑内解释。
- 法术运行时出现无限递归、无限 projectile、无限 payload 或不可控对象生成。
- 法术书、背包 UI、运行时执行器出现多个互相不同的真源。
- Modifier 或 Value 的消费者无法通过描述系统清晰解释。
- Trigger/Payload 的外层和内层边界在资产、编译器或 UI 中不一致。
- 新体系要求修改 `Vocalith` 反向引用 `Kernel`。
- 需要决定玩家体验层面的不可逆语义，例如 payload 边界用 token 还是 UI 嵌套槽，而本计划没有预先定义。

## 第一批建议资产

法术书：

- `ApprenticeSpellBook`：5 槽，普通冷却，无常驻 token。
- `WideSpellBook`：7 槽，较慢冷却，每次 2 轮 10 度扇形。
- `QuickSpellBook`：4 槽，较快冷却，固定前置 Haste，原生伤害 `*=0.85`。
- `TriggerSpellBook`：6 槽，自带一次 OnHit payload，并通过原生 `ResultMultiplier *=1.25` 强化 payload 结果倍率。
- `SurgeSpellBook`：5 槽，快速冷却，每次 3 轮 8 度扇形，受能量容量 / 消耗 / 恢复门槛限制，原生伤害 `*=0.8`。
- `BindingSpellBook`：5 槽，中速冷却，自带一次 OnHit Control payload，并通过原生 `ResultCount =1` / `ResultDuration *=1.5` 强化控制触发和持续。

Modifier：

- `加速`：NextToken 或 CurrentBlock projectile speed。
- `扩域`：NextToken 或 CurrentBlock impact radius / explosion radius。
- `放大`：CurrentBlock damage multiplier。
- `载荷放大`：CurrentPayload 的 `ResultMultiplier *=1.5`，强化当前 payload 内 result-only 效果。
- `载荷扩域`：CurrentPayload 的 `ImpactRadiusMultiplier *=1.35`，扩大当前 payload 内 result-only 范围。
- `载荷增殖`：CurrentPayload 的 `ResultCount +=2`，增加当前 payload 内 result-only 数量。
- `轻盈`：降低 cooldown 或 projectile lifetime tradeoff。

Multicast：

- `双重`：收集后 2 个可执行节点。
- `三重`：收集后 3 个可执行节点。

Trigger：

- `触发`：OnHit 执行 payload。
- `延迟`：预留，后续执行 OnTimer。

Value：

- `二`、`三`、`五` 保留。
- 新增 `短`、`长` 或 `小`、`大` 这类非数字参数时，需要先确认 UI 描述是否足够清晰。

## 第一阶段完成定义

当 M0 到 M2 完成时，系统应达成第一阶段稳定态：

- 玩家和敌人都通过法术书执行器开火。
- 旧线性构筑能被新 SpellProgram 表达。
- 背包、描述、预览都读取同一份新编译结果。
- 旧 `CompiledAttack` 不再作为主要真源。

当 M3 到 M6 完成时，系统应达成第二阶段稳定态：

- Modifier、CastBlock、Trigger/Payload、Value 扩展均可用。
- 至少存在 6 个代表性构筑测试，覆盖单层、多重、触发、内层 payload、范围参数、持续参数。
- 法术描述能解释“谁修饰谁”和“命中后发生什么”。

当 M7 到 M8 完成时，系统应达成第三阶段稳定态：

- 玩家可在实际流程中获取不同法术书和新 token。
- 旧系统无运行时引用。
- README 与 DesignGuide 能指导策划继续配置新体系内容。
