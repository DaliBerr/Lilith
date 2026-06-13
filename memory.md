# Memory

## Obsidian Global Memory Migration

Obsidian vault is now the primary cross-session memory layer for agent workflow, project handoff, TODOs, decisions, preferences, and long-term knowledge.

- Global entry: `Index.md`
- Memory rules: `Memory_Rules.md`
- Lilith project note: `Projects/Lilith/Lilith.md`
- Lilith troubleshooting mirror: `Knowledge/Troubleshooting/Lilith_Unity_Troubleshooting.md`
- Obsidian MCP workflow notes: `Knowledge/Tools/Obsidian_MCP.md`

Keep this file as a repo-local compatibility mirror for high-value Lilith troubleshooting patterns. When adding new durable memory, prefer Obsidian first, then update this file only if the knowledge must remain available without Obsidian.

## Agent Must Not Enter Lilith PlayMode

- Problem: Agent 通过 Unity MCP 进入 Lilith PlayMode 会导致 Unity 崩溃或会话断开，影响用户继续手动验证。
- Cause: Lilith 当前运行态 / Editor 环境对由 MCP 触发的 PlayMode 不稳定；用户明确要求所有 PlayMode / 手动运行验证都由用户自己执行。
- Fix: Agent 禁止调用 Unity `play` 或运行 PlayMode 测试。验证策略应限制为 EditMode、Console、编译、静态检查、资源 / prefab / scene 读回和用户手动 Play 回报。
- Verify: 后续 Lilith 任务总结中若涉及运行态验证，应明确区分“agent 已做的非 PlayMode 验证”和“仍需用户手动 Play 验证”。
- Scope: 适用于 `G:\Unity Project\Lilith` 的所有 Unity MCP / Editor 工作流；不自动推广为其他 Unity 项目的硬规则。

## Unity Version Control SDF Asset Conflict UI Can Be Stale

- Problem: Unity Plastic Version Control 对 Unity 生成的 TMP SDF 字体资产（例如 `Assets/Fonts/fusion-pixel-12px-monospaced-zh_hans SDF.asset`）显示冲突，选择保留 ours/theirs 时弹 `An unexpected error has occurred.`。
- Cause: Unity Version Control UI 可能残留一个已不存在或已处理失败的冲突状态；Plastic CLI 对该文件实际只报告 `CO|...|False|...|NO_MERGES`，含义是文件被 checkout 但内容未改变，且没有待处理 merge。
- Fix: 先只读确认状态：`cm status "Assets\Fonts" --all --machinereadable --fieldseparator='|' --includeRevId --iscochanged`；若目标文件为 `CO ... False ... NO_MERGES`，用 `cm unco "Assets\Fonts\fusion-pixel-12px-monospaced-zh_hans SDF.asset"` 释放 checkout，再通过 Unity MCP `refresh_unity` 或 Unity Editor refresh 让编辑器吸收外部变化。不要用 Git 或手删 `.plastic` 来解决 Plastic UI 卡住的问题。
- Verify: `cm status "Assets\Fonts" --all ...` 不再列出该文件；Unity editor state 的 `external_changes_dirty=false`；Console error/warning 为 0。
- Scope: 适用于 Plastic 管理下的 Unity 生成资产大文件，尤其是 TMP SDF font asset 这类可由 Unity 再生成、但已被他人同步并导致 Version Control UI 冲突面板异常的情况。若 CLI 显示内容已变或仍有 merge，不要直接 `unco`，先备份或明确选择保留哪一侧。

## Plastic Recovery Can Leave Unity Console And Generated Projects Stale

- Problem: Plastic metadata-only update / conflict cleanup 已经完成后，Unity Console 仍显示大量 C# error，例如测试调用旧 API、`EnemyDefinition.AIProfile` 看似不存在，或命令行 `dotnet build Lilith.Tests.EditMode.csproj` 报 `UnityEngine.U2D.Animation` / `SpriteLibraryAsset` / `SpriteResolver` 缺失。
- Cause: 这不一定是文件丢失。恢复工作区后可能同时存在三层不同步：Unity AssetDatabase / Console 仍缓存旧编译结果；Unity 生成的 `.csproj` 没有重新同步 asmdef/package 引用；手动 backup shelveset（如 `sh:9`）中的源码、测试、asmdef、Packages 和 ScriptableObject 资产需要按同一版本面精确恢复，不能只恢复触发第一条报错的文件。
- Fix: 先用 Plastic CLI 确认真正 update 状态：`cm status --header`、`cm update --last`、`cm status --changed --short`、`cm status --private --short`。若有手动 backup shelveset，不要整包 apply；用 `cm diff sh:<id> --download=<temp> --changed --added --encoding=utf-8` 下载到临时目录，再按 Console / build 报错精确复制相关文件并备份覆盖前版本。修改 C#、asmdef、Packages 或 Unity 资产后，执行 Unity MCP `refresh_unity(mode=force, scope=all, compile=request, wait_for_ready=true)`。若 Unity Console 已清但 `dotnet build` 仍报 package 类型缺失，执行 Unity 菜单 `Assets/Open C# Project` 触发生成工程文件同步，再重跑 build。
- Verify: Unity Console error 为 0；`dotnet build Lilith.Kernel.csproj --no-restore /p:UseSharedCompilation=false` 和 `dotnet build Lilith.Tests.EditMode.csproj --no-restore /p:UseSharedCompilation=false` 均 0 warning / 0 error；相关 EditMode 测试通过；`cm update --last` 返回 up-to-date；`cm status --changed --short` 只剩预期项目文件，`cm status --private --short` 只剩明确不提交的本地文件。
- Scope: 适用于 Lilith 在 Plastic/Unity Version Control 恢复后出现“文件看起来已经在磁盘上正确，但 Unity 或 dotnet 仍按旧状态报错”的场景。不要把 TMP SDF 字体动态 atlas 误判为这类脚本错误根因；字体文件若不在 `cm status --changed/private` 中，优先查 Unity 缓存、工程文件生成和 sh9 配套文件版本面。

## SystemCallBlocker Should Exclude Imported Third-Party Editor Plugins

- Problem: 导入 ASE / Amplify Shader Editor 后，`SystemCallBlocker` 在编译完成扫描时把第三方 Editor 插件里的 `UnityEngine.Debug` 用法当成项目违规，Console 刷出 58 条 `[SystemCallBlocker] 违规调用：规则 禁止 UnityEngine.Debug，文件 Assets/AmplifyShaderEditor/...`，影响美术同事正常使用 ASE。
- Cause: `Assets/Editor/SystemCallBlocker.cs` 的 `GlobalExcludedPathContains` 只排除了 `Packages`、`Plugins`、`Assets/Editor` 等目录，没有排除 Asset Store 形式导入的 `Assets/AmplifyShaderEditor/`。ASE 作为第三方 Editor 工具不能按项目自有运行时代码规范要求替换为 `Vocalith.GameDebug`。
- Fix: 在 `GlobalExcludedPathContains` 中加入 `"/Assets/AmplifyShaderEditor/"`。后续导入类似大型第三方 Editor 插件时，应优先把插件根目录加入 blocker 的全局排除，而不是修改第三方源码或放宽项目自有代码规则。
- ASE sample package cleanup: `Assets/AmplifyShaderEditor/Examples/*.unitypackage` 本体被 `ignore.conf` 的 `*.unitypackage` 排除且不在磁盘上时，Unity 会持续删除对应的孤儿 `*.unitypackage.meta`。如果这些 meta 已被 Plastic 跟踪，ignore 不会隐藏当前删除项；正确处理是提交删除这些孤儿 meta，并在 `ignore.conf` 中明确忽略 `Assets/AmplifyShaderEditor/Examples/*.unitypackage(.meta)`，防止后续重新纳入版本库。
- Verify: 2026-06-08 反射确认 Unity 当前加载的 `SystemCallBlocker` 排除列表包含 `/Assets/AmplifyShaderEditor/`；清空 Console 后反射调用 `ScanAndReport(false)` 返回 `False`；Unity Console error 0；`dotnet build Lilith.Editor.csproj /p:UseSharedCompilation=false` 0 warning / 0 error。
- Scope: 适用于 Asset Store / 第三方 Editor 工具源码以 `Assets/<PluginName>/` 形式进入仓库，且不应被项目自有 API 使用规范扫描的情况。仍应继续扫描项目自有 `Assets/Scripts/**`、必要 Editor 工具和测试代码。

## Trigger Payload Is Implicit After Trigger Token

- Problem: 显式 `PayloadStart/PayloadEnd` token 让构筑语法和 UI 都显得违和，并且奖励池需要额外暴露没有独立玩法价值的边界 token。
- Cause: 新法术系统已经有 Trigger 作为稳定结构入口；payload 边界可以由结构层级决定，而不需要玩家摆放 `[` / `]`。
- Fix: `TriggerTokenData` 后续 token 自动构成 payload。非 Multicast 公式中消费到整式末尾；Multicast 中 Trigger 只作用当前 projectile segment，一旦进入 payload 会吞掉该 segment 后续 token，不再回到兄弟 outer projectile，兄弟段不足会 warning。普通 Modifier 仍只向后解析，`NextToken` / `NextN` 不跨 trigger/payload 边界；payload 内 modifier 只在 payload 局部向后绑定。显式 `PayloadBoundaryTokenData`、`TokenType.PayloadStart/PayloadEnd`、边界资产、奖励库入口和 UI 类型展示已删除。
- Verify: 2026-06-02 Kernel 与 EditMode csproj `dotnet build --no-restore` 均 0 warning / 0 error；Unity EditMode 定向 `SpellProgramCompilerTests` + `SpellDescriptionGeneratorTests` + `CombatEntryTokenSelectionPlanTests` 为 64/64，通过背包 UI / 预览 / 法术书回归 44/44，Token Select / 背包库存回归 32/32；Console 无 error。
- Scope: 后续书写 Trigger 构筑时使用 `火 + 触 + 冰`、`火 + 触 + 爆 + 三`、`OnHitTrigger + Explosion` 这类隐式 payload 形态；不要恢复显式 payload boundary token 或把它重新放回 `SpellProgram_Token_Lib`。

## Spell Token Mechanism Skeleton Now Covers Advanced Trigger, Value, Multicast, Status Slots

- Problem: `Docs/SpellTokenSystemDesign.md` 中规划的 `时/程/近/终/灭`、Value 模式、Multicast pattern、状态槽与元素反应会影响系统表达能力，不能只停留在资产计划里。
- Cause: 新系统已经以 `CompiledSpellProgram` / `SpellProjectileNode` / `SpellPayloadBlock` 作为运行时真源；若 Trigger 参数、payload 触发点、派生安全、状态槽和描述仍分散实现，后续正式 Token 资产会出现 UI/描述/运行时语义不一致。
- Fix: 2026-06-03 已补齐机制骨架：`SpellTriggerType` 覆盖 `OnHit`、`OnTimer`、`OnExpire`、`OnKill`、`OnDistance`、`OnProximity`；`时/程/近` 优先消费紧随 Value 作为 Trigger 参数；`SpellPayloadBlock` 保存触发类型、参数和触发点语义；`CharBullet` 统一执行命中、计时、距离、接近、消失和击杀 payload，非 OnHit payload 对同一 block 默认只触发一次；`ValueTokenData` 支持数值、倍率、规模预设；`MulticastTokenData` / `SpellCastBlock` 支持同时、顺序、分叉、环绕 pattern 骨架；派生 projectile 只复制 Core 状态倾向，不继承 Trigger / Payload / 继续派生能力；敌人状态改为通用状态槽并通过 `SpellStatusApplication` 统一写入，反应默认消耗相关槽 50%。
- Verify: `dotnet build Lilith.Kernel.csproj --no-restore` 0 warning / 0 error；`dotnet build Lilith.Tests.EditMode.csproj --no-restore` 0 warning / 0 error；Unity EditMode 定向 `SpellProgramCompilerTests` + `SpellDescriptionGeneratorTests` + `EnemyStatusEffectControllerTests` + `CombatEntryTokenSelectionPlanTests` 为 83/83 passed；Unity Console error 0。
- Scope: 这只是机制骨架，不表示正式 Token 资产和数值已配置完成。`Docs/SpellTokenSystemDesign.md`、`Docs/SpellConstructionSystemDesign.md`、`Docs/SpellConstructionUseCases.md` 与 `README.md` 已同步当前边界；后续重点是正式资产接入普通奖励池、平衡表、视觉表现、多环绕/可配置环绕扩展、镜像/召唤/傀儡实体和手动 Play smoke。

## Formal Spell Token Staging Libraries Are Asset-Ready But Not In Rewards

- Problem: `Docs/SpellTokenSystemDesign.md` 的完整 Token 清单需要先接到资产层，但不能把高风险机制伪装成可玩内容，也不能在未平衡前扩大普通奖励池。
- Cause: 新系统已经有 Trigger / Value / Multicast / 状态槽机制骨架，但正式玩法资产和隐藏复杂机制需要分层落地：可运行可描述的内容才能进 playable staging，镜像、召唤、傀儡等复杂内容仍应先作为隐藏索引占位。
- Fix: 2026-06-03 已新增 `PrototypeTokenData`，它继承 `PlaceableTokenData` 但不追加编译 token；2026-06-05 已把 `滞 / 驰 / 缓 / 蛇 / 游 / 分 / 旋` 晋升为正式 Behaviour，并移除两个已取消的复杂 Behaviour 计划项；同日 `绕` 晋升为正式 Multicast，固定收集 2 段，第二段以第一段主弹为 movement anchor 环绕，主弹失效时同步过期；随后 `汲 / 护 / 留 / 斥 / 吸` 晋升为正式 Result，`稳 / 狂 / 贪 / 急 / 源` 晋升为正式 cast-level Modifier，`水 / 风 / 光 / 羊 / 谜` 晋升为正式 Core。2026-06-06 `混` 晋升为正式 Result，命中或 result-only payload 执行时随机抽 `爆/裂/愈/定/燃/缚/蚀/标/潮/震/汲/护/留/斥/吸` 之一；`乱` 晋升为正式 Modifier，每次发射前随机替换为一个已实现 Modifier，候选池包含普通修饰与 `稳/狂/贪/急/源`，不消费 Value。`AttackTokenAssetGenerator.GenerateFormalSpellTokenAssets()` 会生成两个独立库：`Assets/Data/BulletTokens/TokenLib/SpellToken_Playable_Staging_Lib.asset`（80 个 playable Token）和 `Assets/Data/BulletTokens/TokenLib/SpellToken_Hidden_Prototype_Lib.asset`（5 个 hidden prototype Token，权重 0）。两者均不在 `Plan2` 中，也不扩展 `SpellProgram_Token_Lib.asset`。隐藏 prototype 现在只保留 `镜 / 召 / 幻 / 替 / 傀`，都写入计划语义和未实装原因，供构筑调试、隐藏库索引和后续迁移使用。`箭 / 岩 / 水 / 风 / 光 / 羊 / 谜` 已接入正式 `AttackCoreType`；`水` 写入潮湿槽，`风` 命中点小范围推开低重量敌人，`光` 穿过敌人和墙体且只造成每次穿透后 `*=0.7` 的衰减直伤、不触发 Result/Core 状态/OnHit/OnKill payload，`羊` 写入变形槽并在普通低重量敌人满 3 层后强控/变色 4 秒，`谜` 每发 projectile 随机解析为 `箭/火/冰/雷/岩/刃/毒/影` 之一；`链` Behavior 已有最小运行时，命中主目标后向附近未命中过的敌人传导 50% 直伤，且链跳伤害不触发 payload、不递归派生；`滞` 停在发射点且只做一次出生点直击检测，`驰 / 缓` 改变速度，`蛇 / 游` 改变飞行方向，`分` 飞行期均匀派生安全小弹，`旋` 围绕施法者环绕；`汲` 按直伤比例治疗 owner，`护` 添加临时吸收盾，`留` 生成 tick 伤害/核心状态场，`斥 / 吸` 按敌人位移重量阈值做一次水平推拉；`稳` 降低角度扩散与蛇/游扰动并略降伤害，`狂` 发射自损换伤害并增加能量消耗，`贪` 发射自损并在击败时提升所有掉落概率，`急` 减少玩家法术书发射间隔，`源` 减少玩家法术书能量消耗；状态类 Result 通过 `SpellStatusApplication` 写入统一槽，描述会显示具体状态名。
- Modifier design update: 2026-06-06 `乱` 已不再是 hidden prototype；它现在是 formal playable Modifier，并在每次 activation 编译前解析为候选 Modifier。`卫` 已取消并从游戏侧 hidden prototype 移除，仅文档留档。
- Verify: 2026-06-06 `混/乱` 实现后，Unity asset 读回确认 playable staging 80、hidden prototype 5，`result_confuse` 候选 15、`modifier_chaos` 候选 18，旧 `prototype_result_confuse` / `prototype_modifier_chaos` 资产已删除；`dotnet build Lilith.Kernel.csproj --no-restore /p:UseSharedCompilation=false`、`dotnet build Lilith.Editor.csproj --no-restore /p:UseSharedCompilation=false`、`dotnet build Lilith.Tests.EditMode.csproj --no-restore /p:UseSharedCompilation=false` 均 0 warning / 0 error；Unity MCP EditMode 目标集合 `FormalSpellTokenAssetTests`、`SpellProgramCompilerTests`、`SpellProgramProjectileCompilerTests`、`SpellDescriptionGeneratorTests`、`CharBulletImpactTests`、`SpellBookLoadoutTests` 为 156/156 passed。Console error-only 查询仅返回 Test Runner 写 `TestResults.xml` 的状态记录；`git diff --check` 无尾随空白错误，但提示大量 Unity 资产下次 Git 触碰会从 LF 转 CRLF。
- Scope: 后续若要让正式新 Token 进入玩家自然流程，必须单独把 staging library 接入 `Plan2` 或迁移 token 到现有奖励库，并重新做权重 / Token Select / 背包预览 / Play smoke 验证；hidden prototype 不能加入普通奖励或普通 Token Select。

## SpellProgram Migration Removed Legacy Attack Adapters After Runtime Migration

- Problem: 法术系统 M8 清理若直接删除 `AttackFormulaCompiler` / `CompiledAttack`，会同时打断 `CharBullet` 命中特效、视觉 presenter、旧描述入口和大量 baseline 测试；正确顺序是先把运行时入口迁到 `CompiledSpellProgram` / `SpellProjectileNode`，再删除旧 adapter。
- Cause: 新 `CompiledSpellProgram` 已经是玩家、敌人、背包预览、描述和 loadout 的缓存/发射真源；`SpellProjectileNode` 也已承载 CharBullet、背包预览与描述系统需要的效果/爆炸/视觉快照，并且 SpellProgram 子弹运行时都通过 node 发射且不携带 `CurrentCompiledAttack`。旧桥接逐步清理后，`AttackFormulaCompiler` / `CompiledAttack` 的最后真实引用只剩单 projectile 编译测试护栏。
- Fix: 先把运行时入口逐步改为 `CompiledSpellProgram` / `SpellProjectileNode`，再把 `AttackFormulaCompilerTests` 迁移并重命名为 `SpellProgramProjectileCompilerTests`，使发射、视觉、命中、Value、LinkedItem 与普通 Modifier 等单 projectile 护栏都直接走 `SpellProgramCompiler` / `SpellProjectileNode`。随后删除 `CompiledSpellProgram.CreateFromCompiledAttack(...)`、`SpellProjectileNode.CreateFromCompiledAttack(...)` / `CreateRuntimeSnapshotFromCompiledAttack(...)`、`AttackFormulaCompiler` public wrapper、`CompiledAttack` 数据类和 `TokenModifierExpressionUtility` 上服务旧数据类的重载，并把文件重命名为 `SpellProjectileCompiler.cs` 与 `AttackCompilation.cs`。下一轮又删除旧 `PreTokenData` / `PostTokenData` 类、`TokenType.Pre/Post` 分支和 `LegacyPre/LegacyPost` origin；再往后把普通 `ModifierTokenData` 的 `Scope/TargetCount` 从资产语义中移除，改为编译时按位置自适应解析，Pre/Post 旧认知也一起收敛为“向后绑定 + 就近最小作用域”。
- Verify: Unity refresh/compile 后 Console error 0；2026-05-31 定向 EditMode `SpellBookLoadoutTests`、`SpellProgramCompilerTests`、`BackPackAttackPreviewControllerTests`、`BackPackUIScreenTests`、`EnemyAttackExtensionTests`、`SpellSystemBaselineTests`、`CharBulletImpactTests`、`CharBulletVisualPresenterTests`、`AttackFormulaCompilerTests`、`SpellDescriptionGeneratorTests` 合计 139/139 通过；编译器适配收缩后同一组重跑仍为 139/139；背包预览 `CompiledAttack` overload 移除后先跑相关 67/67，再跑同一组 139/139 仍通过；描述生成器旧 overload 移除后 `SpellDescriptionGeneratorTests` 13/13 通过，同一组 139/139 仍通过；法术书 loadout 旧 `CompiledAttack` API 移除后 `SpellBookLoadoutTests` 5/5 通过，同一组 139/139 仍通过；旧 fallback loadout 旧 `CompiledAttack` API 移除后 `SpellSystemBaselineTests` 7/7 通过，同一组 139/139 仍通过；旧 fallback 运行时引用移除后，`SpellBookLoadoutTests` + `MapRunFlowControllerTests` + `RuntimeSaveServiceTests` + `SpellSystemBaselineTests` 为 39/39，通过包含背包/预览/子弹/描述/跑图/存档的 wider EditMode 回归 166/166，Console error 0；删除旧 `AttackFormulaLoadout` 类和场景残留后，静态搜索旧名称 / 字段 / GUID / fileID 无命中，Kernel 与 EditMode csproj `dotnet build` 均 0 warning / 0 error，同一目标 EditMode 39/39 通过，Console error 0；Split 子弹去 adapter clone 后，目标 53/53 与更宽法术回归 139/139 通过，Console error 0；SpellProgram 发射运行时 adapter 注入移除、敌人远程覆写改为派生 node 后，目标 EditMode 39/39 与更宽法术回归 139/139 通过，Console error 0；旧 `CompiledAttack` emitter 包装成 node 后，目标 EditMode 72/72 与更宽法术回归 139/139 通过，Console error 0；旧 emitter 与直接 legacy InitializeShot 改用 runtime snapshot 后，Kernel / EditMode csproj build 均 0 warning / 0 error，目标 EditMode 86/86 与更宽法术回归 139/139 通过，Console error 0；移除 `PrimaryCompiledAttack` / `AdapterAttack` / `CompiledAttack.Clone()` 后，Kernel / EditMode csproj build 均 0 warning / 0 error，目标 EditMode 92/92 与更宽法术回归 139/139 通过，Console error 0；移除 `CharBulletVisualPresenter.ApplyCompiledAppearance(CompiledAttack, ...)` 后，Kernel / EditMode csproj build 均 0 warning / 0 error，`CharBulletVisualPresenterTests` 3/3 与更宽法术回归 139/139 通过，Console error 0；移除 `CharBullet.InitializeShot(..., CompiledAttack)` 后，Kernel / EditMode csproj build 均 0 warning / 0 error，目标 EditMode 51/51 与更宽法术回归 139/139 通过，Console error 0；移除 `AttackProjectileEmitter.Emit(CompiledAttack)` 后，Kernel / EditMode csproj build 均 0 warning / 0 error，目标 EditMode 65/65 与更宽法术回归 139/139 通过，Console error 0；引入内部 `SpellProjectileCompileResult` 并让 SpellProgramCompiler 脱离 `CompiledAttack` 数据容器后，Kernel / EditMode csproj build 均 0 warning / 0 error，目标 EditMode 72/72 与扩展法术/背包/子弹/描述回归 212/212 通过，Console error 0；迁移系统级 baseline / permanent upgrade / impact / visual presenter 测试到 SpellProgram 后，Kernel / EditMode csproj build 均 0 warning / 0 error，目标 EditMode 28/28、旧 wrapper 与子弹核心 86/86、扩展法术/背包/子弹/描述回归 219/219 均通过，Console error 0；最终删除 `AttackFormulaCompiler` / `CompiledAttack` 并重命名文件后，`rg "AttackFormulaCompiler|CompiledAttack"` 在 `Assets/**` 代码和测试中无命中，Kernel / EditMode csproj build 均 0 warning / 0 error，`SpellProgramProjectileCompilerTests` 33/33 与扩展法术/背包/子弹/描述回归 219/219 通过，Console error 0。全量 EditMode 488/488 曾因既有 `EnemyResultVisualFeedbackTests` 3 个颜色断言与 `StorySequenceParserTests` 超时 / Unity services 网络异常失败，未作为本轮法术改动通过条件。
- Verify latest: 第一版资产平衡护栏补齐后，Kernel 与 EditMode csproj `dotnet build` 均 0 warning / 0 error；Unity refresh/compile 后 Console error 0；新增 `Plan2Asset_KeepsFirstPassRewardBalanceWeights` 窄测 1/1、`CombatEntryTokenSelectionPlanTests` 13/13、全 EditMode 510/510 通过；精确静态搜索旧 adapter、旧 Pre/Post、`Assets/Scripts/Vocalith/**/*.cs` 的 `Kernel.*`、新增 `UnityEngine.UI.Text` 均无命中，`git diff --check` 仅报告既有 LF/CRLF 提示。
- Scope: 适用于继续清理法术系统旧 adapter、推进 Trigger/Payload 语义和加深法术书执行器特性的后续轮次；当前 `rg "AttackFormulaCompiler|CompiledAttack|PreTokenData|PostTokenData|LegacyPre|LegacyPost"` 在 `Assets/**` 代码和测试中应无命中，普通 `ModifierTokenData` 已不再持有资产级 `Scope/TargetCount`，而是在编译时按位置解析为 `NextToken`、`NextN` 或 `CurrentBlock`；`Modifier + Value + ...` 会把紧随其后的 Value 当作目标数量，payload 内 modifier 默认只在 payload 内向后绑定，普通 modifier 不会自动推断为 `GlobalProgram`。result-only payload modifier 映射仍覆盖 `ImpactRadiusMultiplier`、`ResultCount`、`ResultDuration` 与 `ResultMultiplier`，且法术书 executor modifier 的这些 result 目标也会作用到 result-only payload effect；`ImpactRadiusMultiplier` 现在会修饰 result-only Explosion 半径、Healing 范围与 Control 范围，`Healing.asset` 已开启 Radius 值词消费，`愈 + 三` 可写入 3 半径治疗范围；Healing payload 半径为 0 时保持单体治疗，半径大于 0 时在命中点治疗附近合法目标；Control payload 半径为 0 时保持单体控制，半径大于 0 时在命中点附近对合法敌人登记控制命中；`PayloadAmplifyModifier`、`PayloadRadiusModifier`、`PayloadCountModifier` 与 `PayloadControlFieldModifier` 继续作为 payload 内容样本存在，但其 payload 局部效果现在来自摆放位置，而不是资产硬编码 `CurrentPayload`；法术书冷却、每次激活次数、激活扇形、可选能量门槛和原生 executor modifier 已接入玩家法术书编译，敌人远程绑定法术书时也复用激活扇形与原生 modifier；背包法术描述会显示法术书内建强化数量和目标明细；`QuickSpellBook` 是 4 槽快冷却、固定 Haste、原生伤害 `*=0.85` 的快书，`TriggerSpellBook` 是 6 槽、固定 OnHit Explosion payload、原生 payload 结果倍率 `*=1.25` 的触发书，`SurgeSpellBook` 是 5 槽、三轮扇形、能量受限、原生伤害 `*=0.8` 的爆发执行器，`BindingSpellBook` 是 5 槽中速冷却、固定 OnHit Control payload、原生 `ResultCount =1` / `ResultDuration *=1.5` 的控制执行器；`SpellBookReward_Lib` 现在有自动资产级 smoke 和第一版权重护栏，要求每本奖励法术书有正权重、唯一身份和执行器签名、奖励说明包含 slots / cooldown、可用真实 `FireCore` 编译并可生成描述；`Plan2` 中法术书来源权重固定为 0.35，低于 `SpellProgram_Token_Lib` 来源 0.45，约占全部奖励来源 11.7%；后续重点转向一次手动 Play smoke，更多法术书 bonus、更多可抽取 payload modifier 和进一步 result 语义进入后续新阶段。
- Current continuation: M8 自动化代码面以 Control 范围 payload / `PayloadControlFieldModifier` 与第一版资产平衡护栏为收口点，不再在当前循环里继续追加法术系统功能；剩余 M8 工作只保留一次手动 Play smoke，更多法术书 bonus、更多可抽取 payload modifier 和进一步 result 语义应作为后续新阶段处理。

## Rigidbody Ground Snap Must Sync Physics Position

- Problem: grounded 角色或敌人根节点先用 `transform.position` 吸附到地图平面后，后续再次按 collider 计算 grounded 位置时，Y 可能被重复抬高。
- Cause: 带 `Rigidbody` 的对象如果只改 `Transform`，物理世界里的 `Rigidbody.position` 和 collider 参考位置可能还停留在旧位置；下一次 grounded 计算会基于过时位置再加一次高度补偿。
- Fix: 只要是对带 `Rigidbody` 的 grounded 根节点做传送或贴地吸附，就同时写回 `Rigidbody.position`，并清零线速度与角速度。当前实现入口见 [`Assets/Scripts/Kernel/MapGrid/MapSpawnUtility.cs`](Assets/Scripts/Kernel/MapGrid/MapSpawnUtility.cs) 与 [`Assets/Scripts/Kernel/Enemy/EnemyGenerator.cs`](Assets/Scripts/Kernel/Enemy/EnemyGenerator.cs)。
- Verify: [`Assets/Editor/Test/EnemyGeneratorTests.cs`](Assets/Editor/Test/EnemyGeneratorTests.cs) 中的 `TrySpawnEnemy_SnapsSpawnedEnemyRootToMapPlaneHeight`。
- Scope: 适用于所有依赖 grounded collider 高度补偿、且根节点挂有 `Rigidbody` 的玩家、敌人或可传送物体。


## Unity Inspector List Plus Does Not Call Custom Add Methods

- Problem: 在 `ScriptableObject` 或 `MonoBehaviour` 的 Inspector 中，给 `[SerializeField] private List<T>` 点击 `+` 后，看起来无法新增元素，列表项会立刻消失，容易让 agent 误以为是 Unity 序列化失效、字段类型不可编辑，或 `AddToken()` 之类的自定义方法没有生效。
- Cause: Unity 默认 Inspector 的列表 `+` 按钮只会直接往序列化 `List` 里插入一个新元素，不会调用任何自定义的 `AddXxx()` 方法。对于 `UnityEngine.Object` 引用类型（例如 `ScriptableObject`），这个新元素初始通常是 `null`。如果脚本在 `OnValidate()` 里立即执行“清理空项”逻辑（如移除 `null` / 去重 / sanitize），刚新增的空槽位会被瞬间删掉，于是 Inspector 中表现为“点了 + 但加不上”。
- Fix: 不要在 `OnValidate()` 中删除 Inspector 刚创建的 `null` 列表项。可保留去重逻辑，但应允许空槽位暂时存在，等用户手动赋值后再在运行时或显式清理入口中移除 `null`。同时要明确：Inspector 列表的 `+` 只是添加一个空引用槽位，不会自动创建新的 `ScriptableObject asset`，若元素类型是 `ScriptableObject`，需要先创建对应 asset，再拖入或选择引用。
- Verify: 临时移除或放宽 `OnValidate()` 中的 `null` 清理后，点击 Inspector 列表 `+` 应能稳定出现新的空元素槽位；随后可手动指定已有的 `PlaceableTokenData` 资产，且不会被立即删除。
- Scope: 适用于所有在 Unity 默认 Inspector 中编辑 `[SerializeField] List<T>` 的场景，尤其是 `T` 为 `ScriptableObject` 或其他 `UnityEngine.Object` 引用类型，且脚本在 `OnValidate()` / sanitize 流程中会移除 `null` 元素的情况。


## OnValidate Forced Overwrite Can Clear Inspector References

- Problem: 在 prefab 或场景对象上手动拖拽 `Transform/Collider` 引用后，Inspector 显示会立刻被清空，看起来像“字段无法挂载”。
- Cause: `OnValidate()` 调用了自动缓存逻辑并传入 `overwriteExisting: true`，导致每次属性变更都会重算并覆盖已序列化引用；如果自动查找路径与实际层级命名不一致（例如脚本找 `Model/wall Model`，实际是 `Model/wall`），结果会被覆盖为 `null`。
- Fix: `OnValidate()` 中默认使用 `overwriteExisting: false`，只在引用无效时回填；同时为自动查找增加命名兼容和兜底匹配（如按 token `wall/ground` 在 `Model` 下递归查找）。
- Verify: 手动拖拽引用后不再被立即清空；组件重载/编译后可自动解析到目标子节点；`get_errors` 对应脚本无错误。
- Scope: 适用于带自动绑定逻辑的 Unity 组件（尤其在 `OnValidate`、`Reset`、Editor 工具批处理里会重建引用的脚本）。


## Wall Tag Occlusion Needs Non-Trigger Collider

- Problem: 某些 `Wall` Tag 的美术对象在玩家被遮挡时没有进入半透明，尽管 `CameraOcclusionFader` 已支持按 `Wall` Tag 处理。
- Cause: `CameraOcclusionFader` 先用 `Physics.RaycastNonAlloc` 命中 collider，再解析 `Wall` Tag；很多导入模型（如 `node_0_3k.obj`）没有非 Trigger collider，因此不会被射线命中。
- Fix: 使用批处理工具 [`Assets/Editor/WallTagColliderBatchTool.cs`](Assets/Editor/WallTagColliderBatchTool.cs) 给 `Wall` Tag 对象自动补 `BoxCollider`（基于 renderer bounds）；工具会跳过 `CellData` 层级，避免给真实网格墙体重复加 collider。
- Verify: 运行菜单 `Tools/Lilith/Wall Collider/Add Missing Colliders In Open Scenes`（场景）或 `Tools/Lilith/Wall Collider/Add Missing Colliders In Selected Prefabs`（Prefab），然后在遮挡路径上复测墙体是否切到幽灵材质。
- Scope: 适用于所有“使用 `Wall` Tag 参与相机遮挡淡出、但缺少可射线命中非 Trigger collider”的美术资源；后续遇到同类问题优先提醒先跑该 Editor 工具。


## Bullet Aim Ray Should Prefer Non-Wall Hits

- Problem: 给 `Wall` Tag 美术对象补了 collider 后，玩家鼠标瞄准射线容易先打到近处墙体，导致子弹方向异常（看起来“穿不过半透明墙”）。
- Cause: `PlayerPlaneMovement.TryGetRaycastAimPoint()` 默认使用最近命中点，新增墙体 collider 会抢占地面/目标点。
- Fix: 在 [`Assets/Scripts/Kernel/Player/PlayerPlaneMovement.cs`](Assets/Scripts/Kernel/Player/PlayerPlaneMovement.cs) 中改为“优先命中非墙体，墙体仅作兜底”：先跳过 `CellData.Wall` 和 `Wall` Tag 层级命中，若整条射线只有墙体再回退使用最近墙体点。
- Verify: [`Assets/Editor/Test/PlayerPlaneMovementTests.cs`](Assets/Editor/Test/PlayerPlaneMovementTests.cs) 的 `TryGetRaycastAimPoint_PrefersNonWallHitOverNearWallCollider`。
- Scope: 适用于“相机遮挡半透明依赖墙体 collider”与“鼠标射线瞄准子弹方向”并存的场景；后续若再出现鼠标贴墙时子弹方向异常，优先检查该策略是否被回归。


## Hint Enemy Catalog Should Scan EnemyDefinition Assets In Editor

- Problem: Hint 图鉴里的敌人列表在编辑器里会漏掉“刚新建的 EnemyDefinition 资产”，看起来像是 Hint 没有读取到新敌人。
- Cause: 若只用 `Resources.FindObjectsOfTypeAll<EnemyDefinition>()`，只能拿到“当前已加载到内存”的定义对象；新建但尚未被任何场景/资源链加载的资产不会出现。
- Fix: 在编辑器环境下优先使用 `AssetDatabase.FindAssets("t:EnemyDefinition")` + `LoadAssetAtPath` 全量读取资产；运行时再用已加载对象兜底。当前实现见 [`Assets/Scripts/Kernel/UI/HintUIScreen.cs`](Assets/Scripts/Kernel/UI/HintUIScreen.cs) 的 `CollectEnemyDefinitions()`。
- Verify: 新建 EnemyDefinition 资产（即使未放入场景），打开 Hint 后应能在图鉴分类看到该敌人条目；若 ID 重复，按 `EnemyId` 去重并优先保留有 Description 的定义。
- Scope: 适用于所有“编辑器内从资产库收集定义并渲染目录 UI”的场景，尤其是图鉴、手册、配置浏览器这类不依赖运行时实例化链路的功能。


## Enemy Spawn Should Re-Snap After Bind And Wave Config

- Problem: 某些敌人（尤其是召唤链路中的单位）在实例化后会出现高度异常，视觉上像“半埋地”。
- Cause: 生成流程先做了一次 grounded snap，但后续绑定目标或应用波次配置时，仍可能通过组件回调改写 Transform Y，导致最终位置偏离地面。
- Fix: 在 [`Assets/Scripts/Kernel/Enemy/EnemyGenerator.cs`](Assets/Scripts/Kernel/Enemy/EnemyGenerator.cs) 的 `TryInitializeSpawnedEnemy()` 中，保留原有首次贴地后，再在“绑定目标 + ApplyWaveConfigToReceivers”之后补一次最终 grounded snap。
- Verify: [`Assets/Editor/Test/EnemyGeneratorTests.cs`](Assets/Editor/Test/EnemyGeneratorTests.cs) 的 `TrySpawnEnemy_ReSnapsAfterWaveConfigReceiverMutatesTransformHeight`，通过注入会主动下拉 Y 的测试接收器，验证生成器最后一次贴地能纠正高度。
- Scope: 适用于所有通过 `EnemyGenerator` 生成的敌人，包括波次刷怪与召唤技能产物，尤其是存在后置回调可能改写 Transform 的链路。


## Pause Guard Must Zero Dynamic Bullet Rigidbody Velocity

- Problem: 战斗进入暂停菜单后，已发射且使用非运动学刚体的子弹仍会继续飞行。
- Cause: 仅在 `Update/FixedUpdate` 里跳过移动逻辑并不会自动清掉 `Rigidbody.linearVelocity`，物理系统会继续沿上一帧速度推进。
- Fix: 在 [`Assets/Scripts/Kernel/Bullet/CharBullet.cs`](Assets/Scripts/Kernel/Bullet/CharBullet.cs) 的 `Update/FixedUpdate` 增加暂停门控（`InBackPackStatus` / `InPauseMenuStatus` / `PausedStatus`），暂停中对动态刚体子弹显式把 `linearVelocity` 置零；不要直接复用 `TryStopMovement()`，因为它会把 `speed` 清零导致恢复后不再前进。同时新增 `SetIgnoreGameplayPauseStatus(bool)` 给背包预览子弹使用，并在 [`Assets/Scripts/Kernel/UI/BackPackAttackPreviewController.cs`](Assets/Scripts/Kernel/UI/BackPackAttackPreviewController.cs) 发射后显式设置为 `true`，避免预览动画被战斗暂停门控冻结。
- Verify: [`Assets/Editor/Test/CharBulletImpactTests.cs`](Assets/Editor/Test/CharBulletImpactTests.cs) 的 `FixedUpdate_InPauseMenuStatus_StopsAndResumesDynamicBulletVelocity`、`FixedUpdate_InBackPackStatus_StopsAndResumesDynamicBulletVelocity`、`FixedUpdate_InBackPackStatus_IgnoresPauseWhenConfigured`。
- Scope: 适用于所有依赖非运动学刚体推进的运行时投射物暂停逻辑；目标是“背包/暂停时冻结战斗子弹，UI 预览子弹可按需继续播放”。


## Unity MCP ManageComponents Needs Script Reimport After Field Additions

- Problem: 新增了组件序列化字段后，`manage_components(action=set_property)` 仍提示 `SerializedProperty '<fieldName>' not found`，但老字段（如 `waves`、`interWaveDelay`）可正常写入。
- Cause: Unity 编辑器域里该脚本的元数据有时未及时刷新，MCP 读到的是旧字段集合。
- Fix: 先对脚本执行一次 reimport（例如 `manage_asset(action=import, path='Assets/Scripts/Kernel/Enemy/WaveManager.cs')`），再重试 `set_property`。
- Verify: reimport 后同一字段可被 `set_property` 成功写入，并在场景 YAML 中看到新序列化节点（如 `defaultNonBossWaveTokenDrops`、`nonBossWaveTokenDropsByWave`）。
- Scope: 适用于所有通过 MCP 直接改组件属性、且近期刚改过脚本字段结构的场景。


## Unity EditMode Full Run Can Stall When Editor Is Unfocused

- Problem: 通过 Unity MCP 跑完整 EditMode 时，测试进度可能卡住，`get_test_job` 显示 `blocked_reason=editor_unfocused`，最终把某个本来无关的测试记成 timeout。
- Cause: Unity Editor 未聚焦时，某些 Editor / UI / coroutine 风格的 EditMode 测试会被节流或推进极慢。
- Fix: 先把 Unity 窗口强制拉到前台。Windows 上可用 `WScript.Shell.AppActivate(<UnityPid>)`，必要时再用 `user32.dll` 的 `ShowWindow` / `SwitchToThisWindow` / `SetForegroundWindow`。测试收束后单独重跑被 timeout 的测试，确认是否为环境性超时。
- Verify: 本次完整 EditMode 中 `StorySequenceParserTests.Service_AppendDisplayMode_KeepsPreviousLineAndAddsNewline` 因未聚焦 timeout；强制聚焦后单独重跑 1/1 通过，Console error 为 0。
- Scope: 适用于 Lilith 中长时间运行完整 EditMode 或 Story/UI/Editor 更新相关测试时的 MCP 验证。


## Enemy Result Visual Feedback Tests Need Real UI Container And Stable Pulse Duration

- Problem: `EnemyResultVisualFeedbackTests` 在全量 EditMode 中出现默认颜色 / 控制黄闪 / 治疗绿闪断言失败，看起来像运行时视觉回归。
- Cause: 测试夹具里的 `Text` 容器如果只是普通 `GameObject`，`CharEnemyVisualPresenter.TryCacheBindings()` 会因缺少 `RectTransform` 而无法缓存完整绑定；另外 EditMode 中 `Time.deltaTime` 可能大于很短的脉冲持续时间，`LateUpdate()` 首帧就把 0.16s / 0.2s 脉冲吃完，颜色直接回白。
- Fix: 测试夹具按真实 UI/视觉层级给 `Text` 容器补 `RectTransform`，并在单元测试中把 `controlHitPulseDuration` / `healingHitPulseDuration` 拉长，避免把 Unity EditMode 的环境帧间隔当成视觉逻辑失败。
- Verify: `EnemyResultVisualFeedbackTests` 3/3 通过；随后完整 EditMode 506/506 通过，Console error 为 0。
- Scope: 适用于 Lilith 中所有通过反射直接调用 `LateUpdate()` / `Update()` 验证短时颜色、脉冲、淡入淡出或倒计时的 EditMode 测试；测试夹具必须满足真实组件绑定条件，并避免依赖不稳定的 Editor `Time.deltaTime`。


## Unity Fake Null Must Not Use Null Coalescing

- Problem: 多个 EditMode 测试在全量或连续运行后出现顺序相关失败，表现为 `RuntimeSaveService` 写入/加载的 Remnant 数量没有同步到当前测试创建的 `PlayerRemnantWallet`。
- Cause: `UnityEngine.Object` 被销毁后可能处于 fake-null 状态；`wallet != null` 会按 Unity 重载判空，但 C# 的 `??` 只看托管引用是否为 null。若静态 `PlayerRemnantWallet.Instance` 指向已销毁对象，`PlayerRemnantWallet.Instance ?? FindFirstObjectByType<PlayerRemnantWallet>()` 不会 fallback，随后 `wallet != null` 又判 false，导致当前场景里的钱包实例被跳过。
- Fix: 不要对 `UnityEngine.Object` 静态实例使用 `??` 做 fallback。先取局部变量，再用 Unity 判空决定是否 `FindFirstObjectByType`；当前修复见 [`Assets/Scripts/Kernel/Save/RuntimeSaveService.cs`](Assets/Scripts/Kernel/Save/RuntimeSaveService.cs) 的 `ResolveRuntimeWallet()`。
- Verify: 连续运行 `QuestServiceTests` 与 `RuntimeSaveServiceTests` 中涉及 Remnant/profile 的用例应稳定通过；EditMode 全量中这类顺序相关 Remnant 失败不应复现。
- Scope: 适用于所有 Unity singleton / cached component fallback，尤其是 EditMode 测试、Domain Reload、`DestroyImmediate` 之后仍可能留下托管引用的路径。


## Large Unity Scene YAML On Windows Should Use Targeted Replacement When apply_patch Overflows

- Problem: 对超大 `.unity` 文件执行 `apply_patch`（即使是很小的片段）可能直接报 `Maximum call stack size exceeded`，导致无法落地单行字段调整。
- Cause: 大文件补丁在当前链路下存在稳定性上限，patch 引擎在巨大 YAML 文本上容易触发栈溢出。
- Fix: 先在同文件内通过唯一锚点确认目标行，再用 PowerShell 做“最小范围字符串替换”完成写入，并立即回读目标片段校验格式；若替换中误写入字面 `` `r`n ``，再做一次精确修复。
- Verify: 回读场景目标块，确认字段已插入、缩进和换行合法，且仅目标片段变化（例如 `WaveManager` 下新增 `nonBossWaveSequenceProgression` 引用）。
- Scope: 适用于 Windows 下修改超大 Unity YAML（`*.unity` / `*.prefab`）时 `apply_patch` 不稳定的场景。


## UI Image White Tint Can Still Look Gray When Sprite Alpha Is Low

- Problem: UI `Image.color` 在运行时显示为纯白且 alpha 为 1，但按钮 hover 背景仍然视觉发灰或被底图染色。
- Cause: `Image.color` 只是顶点 tint；Unity UI 最终输出仍会乘以 sprite texture 的 alpha。如果 sprite 自身是半透明白（例如 `Assets/Art/UI/Start up/Button Background.png` 平均 alpha 约 141/255），即使 tint 是 `#FFFFFFFF`，也会和背后的 StartUp 背景混合，看起来像淡灰/偏黄。
- Fix: 最终采用资源侧修图，而不是运行时 shader/material。按钮背景图主体区域应导出为纯白 RGB `#FFFFFF` 且 alpha 255；只在圆角外沿保留少量抗锯齿半透明像素；不要在图内烘焙灰色、阴影或整体半透明。当前 `StartUpButtonHoverFeedback` 只保留普通 UI Image tint：默认 `Color.white` alpha 0，hover `Color.white`。
- Verify: 检查源 PNG 像素 alpha；主体白底区域应存在大量 alpha 255 像素，而不是所有像素都低于 255。Unity refresh/compile 无 C# error；`StartUpMenuUITests` EditMode 3/3 通过。
- Scope: 适用于所有“UI Image tint 已是白色但视觉仍被背景染灰/偏色”的 Unity UI 问题，尤其是半透明按钮背景、hover 高亮和经过底图混合的白色装饰。

## StartUp Background Should Use Cover Image On Non-16:9 Displays

- Problem: StartUp 主菜单背景素材是 16:9（`Assets/Art/UI/Start up/Background.png`），在 16:10 原生分辨率如 `2880x1800` 下使用原生 `Image.preserveAspect=true` 会完整显示图片并在上下露出边条；不同分辨率还可能因为原生输出、GPU 缩放或显示器缩放路径不同而产生观感色差。
- Cause: 原生 uGUI `Image` 的 preserve aspect 更像 contain：保证整张图可见，但不会铺满非同宽高比容器。StartUp prefab 根背景挂在全屏 root 上，不能直接用 `AspectRatioFitter` 缩放根节点，否则标题、印章和按钮锚点也会跟着改变。
- Fix: 使用 `Vocalith.UI.CoverImage` 替代根背景的原生 `Image`。该组件继承 `Image`，只在 `Simple` sprite 下重建四边形 UV，按容器比例中心裁切图片，实现类似 CSS `background-size: cover` 的铺满效果；16:10 会裁左右，超宽屏会裁上下，RectTransform 和子 UI 布局保持不变。素材侧必须同时检查 Default 和目标平台覆盖项；StartUp `Background.png` 在 Windows 包中应让 Default / Standalone 都保持 8192 + uncompressed + Bilinear，否则只改 Default 仍可能被 Standalone 的 2048 / compressed 覆盖导致色差、降采样或 banding。
- Verify: `StartUp UI Prefab` 根背景组件为 `CoverImage` 且 sprite 仍指向 `Background.png`；`CoverImageTests` 覆盖 16:10 水平裁切、超宽垂直裁切和 prefab 绑定；Unity refresh/compile 后 Console error/warning 0，`dotnet build Lilith.Vocalith.csproj` 与 `dotnet build Lilith.Tests.EditMode.csproj` 0 warning / 0 error，EditMode `CoverImageTests` 3/3 passed。最终视觉仍需用户手动打包或运行验证。
- Scope: 适用于全屏 UI 背景、启动图、章节封面等“要铺满但不能拉伸，也不能让 UI 子节点随背景缩放”的 uGUI 画面。若目标是完整展示构图而不是铺满，应继续使用 contain / preserve aspect 并显式设置边条底色。


## UI Prefab Layout Issues Should Be Rechecked With Offscreen Screenshot Batches

- Problem: 静态 RectTransform / TMP overflow 探针能找到大量候选，但有些告警来自 prefab 脱离运行时父布局后的假阳性；旧版 EditMode `ScreenCapture.CaptureScreenshot` 在 Unity/GameView 失焦或刷新不充分时可能只抓到 3D 地图背景，看不到刚实例化的 UI。
- Cause: uGUI 布局、Canvas rebuild、GameView repaint 和 `ScreenCapture.CaptureScreenshot` 在 EditMode 下不够可靠；即使等待数帧和聚焦 GameView，也可能只得到当前 GameView 相机帧而不包含临时 overlay UI。
- Fix: 使用 `Assets/Editor/UILayoutScreenshotBatcher.cs`。它现在创建临时 `Screen Space - Camera` Canvas、专用截图 Camera 和 `RenderTexture`，用 `VirtualRoot` 模拟逻辑画布，`Canvas.ForceUpdateCanvases()` 后同步渲染 UI 并写出 PNG；输出背景是审查用纯色，不再包含当前 3D 场景，也不再依赖 Unity 窗口或 GameView 前台焦点。2026-06-11 起工具默认不再手写维护 screen prefab 名单，而是自动扫描 `Assets/Prefabs/UI` 下的父级 screen / panel prefab：优先匹配根节点挂了 `UIScreen` 的对象，并兼容当前仓库里少量 legacy 全拉伸 `UI/Panel/Popup/Screen` prefab；带 `ui-layout-ignore` label 的对象会被跳过。这让后续新增父级 UI prefab 通常不再需要额外改截图名单。常规审查集继续排除脱离父级就失真、只依托父组件运行的小子 prefab；对实现了 `UIScreen` 的 screen prefab，会额外调用编辑器态 `UIScreen.__Init()`，让 `BackPackUIScreen` 的运行时背包格、`TokenSelectUIScreen` 卡片、`MainUIScreen` 的 spell slot 等 `OnInit()` 内生成的子节点也能跟随父 screen 一起进入截图。2026-06-12 继续补了逻辑画布修正：如果 `DefaultScenarios` 中某个非 `ui 1.0` 条目的 `description` 已声明 `screen WxH, ui S`，但手填 `logicalSize` 仍等于该分辨率在 `ui 1.0` 下的基准值，脚本会按 `referenceResolution = 1920x1080`、`matchWidthOrHeight = 0.5` 自动换算成正确逻辑画布，避免 `ui 0.6 / 1.5` 仍拍出和 `ui 1.0` 相同的结果。Phase 1 后，截图链路只会调用 prefab 子树里已经显式挂载的顶层 `ResponsiveLayoutGroupFitter`，不会再给没有本地 fitter 的 screen 临时补 root fitter；`ResponsiveLayoutGroupFitter` 当前只处理 `GridLayoutGroup`，普通 `HorizontalLayoutGroup` / `VerticalLayoutGroup` 回归 Unity 原生 `min / preferred / flexible`。Grid 对 `cellSize` / `spacing` / `padding` 做可放大也可缩小的比例调整；如果 grid 是纵向 `ScrollRect.content`，则按 viewport 宽度 fit，允许 content 高度超过 viewport 并滚动；横向 scroll content 则按 viewport 高度 fit。`BackPackUI.prefab` 已在 Phase 2 第一站把 `ResponsiveLayoutGroupFitter` 显式挂到 prefab 根节点；截图菜单另有 `Capture BackPackUI Only` 入口，命令行 `-layoutScreenshotSet backpack` / `backpack-ui` / `backpackui` 只跑该 prefab。操作指南见 `Docs/UILayoutScreenshotAuditGuide.md`。
- Verify: 在非 batchmode Unity Editor 中运行 `Tools/Lilith/UI/Capture Layout Screenshot Batch`；默认输出目录应位于 `<ProjectRoot>\\UILayoutScreenshotBatches\\LilithUILayoutScreenshotBatch_yyyyMMdd_HHmmss`，默认 screen 审查集会生成 `自动发现目标数 x 场景数` 张单图、`自动发现目标数` 张 contact sheet、1 张总览图和 1 份 `00_TARGETS.txt`。若只想验证屏幕 prefab，可改用 `Capture Screen Prefabs Only`；当前推荐的超宽安全框验收入口是 `Capture Screen Prefabs UI Scale 1.0 Only`，它只跑 4 个 `ui 1.0` 分辨率场景；若只想复看背包，可用 `Capture BackPackUI Only`；`Capture Runtime Child Prefabs` 只保留给特殊局部调试。Unity 不应进入 PlayMode，不应保存场景 / prefab；如果仍只看到 3D 地图，优先确认 Unity 已 refresh 到 RenderTexture 版截图工具。
- MainHUD shortcut: 2026-06-12 起可用 `Tools/Lilith/UI/Capture MainUI Only` 单独拍 `Assets/Prefabs/UI/MainHUD/MainUI.prefab`；命令行别名为 `-layoutScreenshotSet main-ui` / `mainui` / `main-hud` / `mainhud`。2026-06-13 起截图工具会给 `MainUIScreen` 注入任务与奖励通知审查样例，覆盖 `Quest Panel` / `Notification Panel` 的本地 anchors、TMP auto-size/ellipsis 和原生 layout；最新验证批次为 `G:\Unity Project\Lilith\UILayoutScreenshotBatches\LilithUILayoutScreenshotBatch_20260613_041630`。该批次仍不替代运行态任务 / 通知内容的用户 Play 验收。
- Phase 3 cleanup note: 2026-06-13 用户 UI 缩放入口已关闭，运行值固定为 1.0；`UIManager.ApplyUIScale()` 仅保留底层兼容入口，旧 `Options.Display.UIScale` PlayerPrefs 会被 `OptionsUIScreen.NormalizeUIScaleValue()` 归一到 1.0。旧 `Assets/Scripts/Vocalith/UI/UIScale.cs` 已确认无有效序列化引用并删除；`ResponsiveLayoutGroupFitter` 也已删掉未调用的 HLG/VLG 线性 helper，只保留显式挂载子树内的 `GridLayoutGroup` 适配。后续不要恢复旧全局 layout compensation 或线性 fitter 双路径；普通 H/V layout 继续用 Unity 原生 `min / preferred / flexible`、anchors、TMP auto-size/ellipsis 或局部业务 fitter 修。
- Ultrawide safe frame note: 2026-06-13 超宽适配主线改为“世界 / 背景全屏，主要 UI 内容收窄”。`Vocalith.UI.UIContentSafeFrame` 默认 `maxAspect = 16 / 9`，只在父容器宽高比大于 16:9 时把挂载节点居中限制到 `parentHeight * 16 / 9`，非超宽维持父容器全尺寸。full-bleed prefab 应把背景 / 遮罩 / 装饰留在 root，把主要交互内容放进 `Content Safe Frame`；普通弹窗可直接在主 panel root 挂 safe frame。`UIScreen` 基类提供 `FindInContentSafeFrame()` 兼容旧根路径和新 `Content Safe Frame/...` 路径，避免 StartUp / MainUI / Pause / Loading / Storyteller / Popup / Profile / Settlement 的自动绑定因层级迁移失效。验证批次：`G:\Unity Project\Lilith\UILayoutScreenshotBatches\LilithUILayoutScreenshotBatch_20260613_143409`；重点抽查 BackPack、StartUp、Token Select、MainUI 超宽图。
- Upgrade opt-in: 2026-06-12 起 `Assets/Prefabs/UI/Upgrade/Upgrage Section Prefab.prefab` 根节点显式挂 `ResponsiveLayoutGroupFitter`，只让其子级 `Grid` 走 Grid-only fit；`Upgrade UI Screen.prefab` 的 HLG/VLG/CSF 不挂通用线性 fitter。截图验证批次：runtime child `G:\Unity Project\Lilith\UILayoutScreenshotBatches\LilithUILayoutScreenshotBatch_20260612_190443`，screen batch `G:\Unity Project\Lilith\UILayoutScreenshotBatches\LilithUILayoutScreenshotBatch_20260612_190642`。
- Narrative/Dialog shortcut: 2026-06-12 起可用 `Tools/Lilith/UI/Capture Narrative Dialog Only` 单独拍 `Assets/Prefabs/UI/Narrative/Dialog UI.prefab`、`Narrative Content Panel.prefab` 与 `Narrative Menu Panel.prefab`；命令行别名为 `-layoutScreenshotSet narrative` / `dialog` / `narrative-dialog` / `narrative-ui`。截图工具会给 `Dialog UI` 与 `Narrative Content Panel` 填入审查样例文本，避免空白或 prefab 占位字符串掩盖排版风险。2026-06-13 复核后，`Narrative Content Panel` 章节栏位于标题下方与翻页按钮上方的安全带，正文 / 翻页按钮继续用本地 anchors + TMP auto-size，不新增通用 fitter；最新验证批次为 `G:\Unity Project\Lilith\UILayoutScreenshotBatches\LilithUILayoutScreenshotBatch_20260613_041946`。
- Narrative Content runtime component note: `Narrative Content Panel.prefab` 根节点不能直接序列化 `Kernel.UI.NarrativeContentUIScreen`，因为该 MonoBehaviour 当前定义在 `NarrativeUIScreen.cs` 中，脚本文件名与类名不同，Unity 会把 prefab 上的组件保存成 missing script。截图工具现在按 `[UIPrefab]` 地址在临时离屏实例上模拟 `UIManager` 的 `GetComponent<T>() ?? AddComponent<T>()`，运行期补 `NarrativeContentUIScreen` 并调用 sample hydration；这不会写回 prefab。若未来要把该组件序列化到 prefab，应先把类移到同名 `NarrativeContentUIScreen.cs` 或确认 Unity 脚本导入规则已改变。
- Token Select shortcut: 2026-06-12 起可用 `Tools/Lilith/UI/Capture Token Select Panel Only` 单独拍 `Assets/Prefabs/UI/TokenSelect/Token Select Panel.prefab`；命令行别名为 `-layoutScreenshotSet token-select` / `tokenselect` / `token-select-panel` / `tokenselectpanel`。Phase 2 Token Select 复核后，该 prefab 不使用通用 `ResponsiveLayoutGroupFitter`，`Main Content` 不恢复 `ContentSizeFitter`，只保留局部 `TokenSelectPanelLayoutFitter` 作为卡片行适配器；`BulletToken Selection Prefab.prefab` 用 `AspectRatioFitter(WidthControlsHeight, 0.92)` 保持卡片比例，并通过 Catalog / Description 锚点间距与 Catalog TMP auto-size 保护小卡片文字。验证批次：`G:\Unity Project\Lilith\UILayoutScreenshotBatches\LilithUILayoutScreenshotBatch_20260612_234747`，重点复看超宽 `ui0.6 / ui1.5` 和 5:4 后未见竖向越界、大块空白或明显文字区不可读。
- System popup note: 2026-06-13 `Info Popup` / `PauseUI` 这类系统弹窗不要用通用线性 fitter 修。`Info Popup` 的窄宽按钮溢出应收敛底部按钮 `LayoutElement.minWidth` 与 `HorizontalLayoutGroup.spacing`；`PauseUI` 嵌入 `Setting Panel` 时，返回按钮应作为 `Settings ` 白色面板内的 footer，而不是挂在橙色 `Settings` 内容容器内靠负锚点越界，也不要作为白框外的独立悬浮按钮。截图工具会给 PauseUI 内嵌 `OptionsUIScreen` 注入设置审查样例。最新验证批次：`G:\Unity Project\Lilith\UILayoutScreenshotBatches\LilithUILayoutScreenshotBatch_20260613_044208`。
- Profile popup note: 2026-06-13 `Profile Popup` 顶栏 / 关闭按钮问题应按 prefab 本地锚点契约修，不恢复通用线性 fitter。当前做法是让顶栏占弹窗高度 9%，正文锚点停在顶栏下方，标题使用左侧伸缩区域，右侧 `Close Button` 保持固定顶栏槽位；prefab 不挂 `ResponsiveLayoutGroupFitter`。验证批次：`G:\Unity Project\Lilith\UILayoutScreenshotBatches\LilithUILayoutScreenshotBatch_20260613_003932`，5:4 高 UI 缩放与超宽高 UI 缩放下顶栏不再挤压正文。
- Options screenshot/layout note: 2026-06-13 `Setting Panel` 的 batch 截图若只出现空壳，优先检查 `UILayoutScreenshotBatcher` 是否给 `OptionsUIScreen` 注入审查 catalog，而不是恢复 root 级或线性 `ResponsiveLayoutGroupFitter`。当前截图工具会加载 `Assets/Prefabs/UI/Options/Option Entry Entry.prefab`，反射设置 `entryPrefab`、`catalog`、`hasLoadedCatalog`，再调用私有 `RebuildView()`，用固定 `显示` / `音频` 样例覆盖 dropdown、slider、toggle 行。`Setting Panel.prefab` 本体修复应继续走局部原生 layout：扩大可见白框、收窄内容区、让 `Setting Button Panel` 留在白框内，并确保它的 sibling draw order 晚于 `Settings `，否则分类按钮会被白色背景盖住；`Option Entry Entry.prefab` 通过拆开 label/control 锚点与开启 label TMP auto-size 处理窄逻辑宽度挤压。验证：`UILayoutScreenshotBatcherTests.TryHydrateRuntimeContent_OptionsUIScreen_AppliesScreenshotAuditSample`、`OptionsUIScreenTests.SettingPanelPrefab_UsesLocalSidebarLayoutWithoutResponsiveFitter` 与 `OptionsUIScreenTests.OptionEntryPrefab_SeparatesLabelAndControlsAtNarrowWidths` 通过；截图批次 `G:\Unity Project\Lilith\UILayoutScreenshotBatches\LilithUILayoutScreenshotBatch_20260613_012728` 已显示右侧分类按钮和选项行。不新增通用 HLG/VLG fitter。
- Hint/StartUp/BossInfo layout note: 2026-06-13 继续处理 screen 级线性风险时，`Hint UI`、`StartUp UI Prefab` 和 `Boss Info UI` 均不要恢复通用 HLG/VLG `ResponsiveLayoutGroupFitter`。`Hint UI` 应走本地 safe-margin anchors、catalog/left/content 三段固定区域、entry/catalog entry `LayoutElement` 与 TMP auto-size/ellipsis；截图工具可给 `HintUIScreen` 注入 entry/catalog prefab 和稳定审查正文，但不要在 EditMode 中触发分类切换，因为 `HintUIScreen.SelectCategory()` 会清理运行时对象并走 `Destroy`，在编辑器测试里会报错。`StartUp UI Prefab` 的按钮列应移除 `ContentSizeFitter`，用固定右侧 anchor + 原生 VLG + button `LayoutElement` 分配高度；背景 cover 裁切仍需用户手动视觉确认。`Boss Info UI` 现在由截图工具注入真实 boss 名称 / 阶段 / 血量样例；该 hydration 不能通过全局 `EventBus.Publish` 实现，否则 prefab unload 后会留下已销毁 `BossInfoUIScreen` 订阅并污染后续 WaveManager 测试，应反射调用 screen 私有处理方法并立即释放订阅。最新验证批次：`G:\Unity Project\Lilith\UILayoutScreenshotBatches\LilithUILayoutScreenshotBatch_20260613_044208`；Unity EditMode 全量 723/723 passed。
- Scope: 适用于 UI prefab 多比例 / UI 缩放肉眼审查、静态探针结果复核、UI 修复前后截图对比。`UIScreen.__Init()` 能覆盖一部分运行时生成子节点，但依赖 Addressables 异步加载、真实玩家数据、滚动状态或 PlayMode 逻辑的页面仍需专门 hydrated sample 或用户手动验证。


## UIManager Navigation Lock Must Own Self-Deactivating Screen Close

- Problem: 在 StartUp 主菜单打开 Options 后点击关闭，画面能回到主菜单，但随后 Start/Load/Settings 等需要走 `UIManager` 的按钮看起来失效。
- Cause: 关闭流程如果由即将被隐藏的 `OptionsUIScreen` 自己 `yield return ui.PopModalAndWait()`，或 `UIManager` 直接 `yield return screen.Hide()`，`UIScreen.Hide()` 末尾的 `gameObject.SetActive(false)` 可能截断/吞掉仍在等待的 coroutine 收尾，导致 `UIManager.RunNavigationLockedWait()` 的 `_isNavigating` 没有恢复为 `false`。此时 modal 已弹栈、`PopUp` 状态也可能已移除，所以表面像“状态切回去了”，实际导航锁仍卡住。
- Fix: 自关闭 UI 不要等待会销毁/失活自己的 `PopModalAndWait()`；改为把最终关闭排队给 `UIManager.CloseModal(this)` 或 `UIManager.PopScreen()`。`UIManager` 内部对 `Show/Hide/DestroyAfterHide` 这类导航 routine 要由 manager 自己逐帧推进，避免把会自失活的 screen coroutine 作为嵌套 coroutine 交给 Unity 调度；EditMode 非 Addressables 实例销毁使用 `DestroyImmediate`。
- Verify: [`Assets/Editor/Test/OptionsUIScreenTests.cs`](Assets/Editor/Test/OptionsUIScreenTests.cs) 的 `RequestClose_WhenOptionsIsTopModal_ReleasesUIManagerNavigationLock`；相邻 [`Assets/Editor/Test/StartUpMenuUITests.cs`](Assets/Editor/Test/StartUpMenuUITests.cs) 仍应通过。
- Scope: 适用于所有通过 `UIManager` 管理、关闭时会 `SetActive(false)` 或销毁自身的 Screen / Modal。症状是 UI 已视觉返回上一层，但后续按钮点击无法继续 push/show/pop。


## UIManager ClearAll Must Own Interrupted Close Transitions

- Problem: 在 StartUp Profile / Load 存档界面快速点两次同一栏位进入旧档后，Main 场景正常进入，但一个半透明 Profile Popup 残留在 gameplay 画面上且不可交互。
- Cause: 旧档进入流程会先请求 `GlobalStartup.RequestEnterMainScene()`，再排队关闭 Profile modal；随后 `LoadMainSceneCo()` 会调用 `UIManager.ClearAllScreensAndModals()`。旧 `CloseModal()` 在关闭协程真正运行前就把 modal 从 `modalStack` 弹出，若清屏在淡出期间 `StopAllCoroutines()`，`DestroyAfterHide()` 被打断，而这个 modal 已不在 stack 中，清屏也就找不到它，最终留下已淡出一半的孤儿 UI。
- Fix: `CloseModal()` / `PopModalAndWait()` / `CloseTopModal()` 不在调度协程前弹栈，而是在导航锁内确认栈顶后再弹出并销毁；`UIManager` 追踪已弹栈但仍在 Hide 动画中的 `closingScreens`；`ClearAllScreensAndModals()` 改为同步停掉过渡、销毁 closing screens 与剩余 screen/modal stack，并当场释放 `_isNavigating`。
- Verify: [`Assets/Editor/Test/OptionsUIScreenTests.cs`](Assets/Editor/Test/OptionsUIScreenTests.cs) 的 `ClearAllScreensAndModals_WhenModalCloseIsInterrupted_DestroysPoppedModal`；相邻 `RequestClose_WhenOptionsIsTopModal_ReleasesUIManagerNavigationLock`、`StartUpMenuUITests` 四个用例与 `TokenSelectModalTests` 仍应通过。
- Scope: 适用于所有“切场景 / 回主菜单 / 强制清屏”与 UI 淡出关闭竞态。典型症状是下一个画面已经进入，但上一层 UI 以半透明、无交互状态残留。


## UIScreen Root Stretch Can Come From UIManager NormalizeRect

- Problem: 修改 UI prefab 根 `RectTransform` 的 anchors / offsets 后，运行时打开该 UI 仍会被拉伸到整个 Canvas 或 Modal layer。
- Cause: `UIManager.CreateScreenCo<T>()` 在 Addressables 实例化后会默认调用 `NormalizeRect(go.transform as RectTransform)`，把 `anchorMin/anchorMax` 改成 `(0,0)` / `(1,1)`，并把 offset 清零。只改 prefab 根锚点会在运行时被这一步覆盖。
- Fix: 对确实不是全屏铺满的 `UIScreen` 子类覆盖 `PreservePrefabRootRectTransform => true`；同时确保该 prefab 的关键序列化引用已重绑，并让自动绑定兼容当前层级。背包修复见 [`Assets/Scripts/Kernel/UI/BackPackUIScreen.cs`](Assets/Scripts/Kernel/UI/BackPackUIScreen.cs)、[`Assets/Scripts/Kernel/UI/BackPackAttackPreviewController.cs`](Assets/Scripts/Kernel/UI/BackPackAttackPreviewController.cs) 与 [`Assets/Prefabs/UI/Backpack/BackPackUI.prefab`](Assets/Prefabs/UI/Backpack/BackPackUI.prefab)。
- Verify: Unity refresh/compile 后 Console error 为 0；`BackPackUIScreenTests` + `BackPackInventoryTests` 26/26 通过；手动 Play 时背包根应保持 prefab root anchors，不再被 UIManager 自动铺满。
- Scope: 适用于所有通过 `UIManager` 创建、但根节点不应全屏铺满的 `UIScreen` / Modal；全屏 UI 继续使用默认 normalize 行为即可。


## MCPForUnity Runtime Assembly Can Enter Player Builds

- Problem: 打包产物的 `Lilith_Data/Managed` 中出现 `MCPForUnity.Runtime.dll`，看起来像 MCP 工具被打进了游戏成品。
- Cause: `Packages/manifest.json` 把 `com.coplaydev.unity-mcp` 作为正式 UPM 依赖安装；该包的 `Runtime/MCPForUnity.Runtime.asmdef` 中 `includePlatforms` 与 `excludePlatforms` 都为空、`autoReferenced` 为 true，因此 Unity 会把它视为 Player 可用运行时程序集。该包的 `Editor/MCPForUnity.Editor.asmdef` 已限制 `includePlatforms: ["Editor"]`，所以 Editor 侧桥接服务不会随 Player 一起打包；进入 `Managed` 的只是 runtime helper assembly。
- Fix: 若 release build 不希望包含任何 MCP 程序集，优先从 release 分支/构建配置中移除 `com.coplaydev.unity-mcp`。若仍希望开发环境保留 MCP，则用 fork 或 embedded package 固定版本，并把 `Runtime/MCPForUnity.Runtime.asmdef` 限制为 Editor-only（例如 `includePlatforms: ["Editor"]`）；不要直接改 `Library/PackageCache`，因为缓存会被 Unity/UPM 重建。避免长期使用 `#main` 浮动依赖，最好 pin 到 commit/tag 或本地包。
- Verify: 检查 `Packages/manifest.json` / `Packages/packages-lock.json` 是否仍含 `com.coplaydev.unity-mcp`；检查包内 `Runtime/MCPForUnity.Runtime.asmdef` 平台限制；重新打包后确认 `*_Data/Managed` 下不再有 `MCPForUnity*.dll`。
- Scope: 适用于所有“开发工具 UPM 包在 Runtime 文件夹或 runtime asmdef 中包含脚本，导致打包产物出现工具 DLL”的 Unity 包管理问题。
