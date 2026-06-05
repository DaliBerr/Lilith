# 法术构筑 Token 参数配置总表（当前代码版）

这份文档聚焦于当前 `TokenType.Core / Behavior / Value / Result / Modifier / Multicast / Trigger` 这套可编译链路，及其执行器 `SpellBookData`。

## 当前正式资产库（2026-06-05）

第一批正式 Token 资产已经通过 `AttackTokenAssetGenerator.GenerateFormalSpellTokenAssets()` 生成，但本轮只进入 staging / hidden library，不进入普通奖励池：

| 库 | 路径 | 数量 | 说明 |
| --- | --- | --- | --- |
| Playable Staging | `Assets/Data/BulletTokens/TokenLib/SpellToken_Playable_Staging_Lib.asset` | 78 | 运行时真实生效、描述不撒谎的第一批 Token |
| Hidden Prototype | `Assets/Data/BulletTokens/TokenLib/SpellToken_Hidden_Prototype_Lib.asset` | 7 | 复杂机制占位；`PrototypeTokenData` 不参与编译；库内权重为 0 |

`Plan2` 暂不引用这两个库，`SpellProgram_Token_Lib.asset` 也没有被本轮扩大。

### Playable Staging tokenId 清单

| 类别 | tokenId |
| --- | --- |
| Core | `core_arrow`、`core_fire`、`core_ice`、`core_thunder`、`core_rock`、`core_blade`、`core_poison`、`core_shadow`、`core_water`、`core_wind`、`core_light`、`core_sheep`、`core_riddle` |
| Behavior | `behavior_spread_formal`、`behavior_pierce_formal`、`behavior_bounce_formal`、`behavior_homing`、`behavior_chain`、`behavior_stasis`、`behavior_rush`、`behavior_slow`、`behavior_snake`、`behavior_wander`、`behavior_split`、`behavior_spin` |
| Result | `result_explosion_formal`、`result_split_formal`、`result_healing`、`result_control_formal`、`result_burn`、`result_bind`、`result_corrode`、`result_mark`、`result_wet`、`result_shock`、`result_drain`、`result_shield`、`result_leave`、`result_push`、`result_pull` |
| Modifier | `modifier_haste`、`modifier_heavy`、`modifier_sharp`、`modifier_field`、`modifier_long`、`modifier_short`、`modifier_light`、`modifier_cold`、`modifier_fierce`、`modifier_focus`、`modifier_count`、`modifier_amplify`、`modifier_expand`、`modifier_stable`、`modifier_wild`、`modifier_greedy`、`modifier_urgent`、`modifier_source` |
| Value | `value_one`、`value_two`、`value_three`、`value_five`、`value_eight`、`value_half`、`value_double`、`value_giant`、`value_zero` |
| Multicast | `multicast_dual`、`multicast_triple`、`multicast_sequence`、`multicast_fork`、`multicast_orbit` |
| Trigger | `trigger_on_hit`、`trigger_timer`、`trigger_expire`、`trigger_kill`、`trigger_distance`、`trigger_proximity` |

### Hidden Prototype tokenId 清单

| 类别 | tokenId |
| --- | --- |
| Core | `prototype_core_mirror`、`prototype_core_summon` |
| Behavior | 无 |
| Result | `prototype_result_illusion`、`prototype_result_replace`、`prototype_result_confuse`、`prototype_result_puppet` |
| Modifier | `prototype_modifier_chaos` |
| Multicast | 无 |

当前已实现的特殊 cast-level Modifier：

- `modifier_stable` / `稳`：降低角度扩散与随机运动扰动，并略降伤害。
- `modifier_wild` / `狂`：发射时立刻掉血，提高伤害，并增加本次法术书能量消耗。
- `modifier_greedy` / `贪`：发射时立刻掉血；若本次法术击败敌人，提高该敌人的所有掉落概率。
- `modifier_urgent` / `急`：减少当前法术书施法间隔/冷却，使这本书更快触发下一次施法。
- `modifier_source` / `源`：减少发射时法术书结算的能量消耗。

当前待实现 Modifier 的最新计划语义：

- `prototype_modifier_chaos` / `乱`：随机实现任意一种合法 Modifier 的效果，按当前语境抽取并应用一次。
- `prototype_modifier_guard` / `卫`：已取消，不再生成游戏内 hidden prototype；仅在设计文档中保留取消记录。

## 一、通用参数（BaseTokenData / PlaceableTokenData）

以下字段在 `BaseTokenData`（所有基础词元）中都可配：

- `tokenId`: 该词元的唯一 ID，越稳定越好。
- `displayText`: 格式化后显示在法术预览上的文本。
- `displayTextKey`: 多语言 key；有值会优先走本地化。
- `description`: 词元说明。
- `descriptionKey`: 说明的本地化 key。
- `modifiers`: `TokenModifierDefinition[]`，决定词元附加的数值修饰。  
- `hasBulletTextOverride` + `bulletTextOverride`: 是否覆盖最终子弹显示字符。

共同约束（`BaseTokenData` + 运行时清洗）：

- `modifiers` 和字符串会被清洗（`OnValidate`），`tokenId/displayText/description` 等空白会自动修正。
- `tokenType` 由各派生类 `OnEnable/OnValidate` 固定设置，通常不手工改。

### Modifier 统一语义（所有词元共享）

`TokenModifierDefinition` 两个字段：

- `target`: 对应 `TokenModifierTarget`。
- `expression`: 形式如 `=`, `+=`, `-=`, `*=`, `/=`，支持数值或颜色字面量。

`TokenModifierTarget` 当前支持值：

- `TextColor`（仅支持 `= Color.xxx` 或 `= #RRGGBB`）
- `FontSize`（数值）
- `ScaleMultiplier`（数值）
- `ProjectileSpeed`（数值）
- `MaxLifetime`（数值）
- `MaxTravelDistance`（数值）
- `ImpactRadiusMultiplier`（数值）
- `ResultCount`（数值）
- `ResultDuration`（数值）
- `ResultMultiplier`（数值）
- `Damage`（数值）
- `CastCooldownMultiplier`（数值；特殊 cast-level Modifier 使用）
- `EnergyCostMultiplier`（数值；特殊 cast-level Modifier 使用）
- `CasterHealthCost`（数值；特殊 cast-level Modifier 使用）
- `DropChanceMultiplierOnKill`（数值；特殊 cast-level Modifier 使用）
- `AngleSpreadMultiplier`（数值；特殊 cast-level Modifier 使用）
- `MovementVarianceMultiplier`（数值；特殊 cast-level Modifier 使用）

`expression` 解析规则：

- 数值只解析成 float，可带或不带 `f` 后缀（如 `1.5` / `1.5f`）。
- 非法表达式会被警告并忽略，不会报崩溃。
- `/=0` 会被拒绝。

## 二、各类 Token 的可配参数

### 1. Core Token（`CoreTokenData`）

`CreateAssetMenu`: `Lilith/Bullet Tokens/Core Token`

- `coreType`: 攻击核心类型（`Fire/Ice/...`）
- `defaultValueType`: `AttackValueType`（目前运行时暂不参与复杂逻辑，仍可配置）
- `damage`（`>= 0`）
- `projectileLife`（`>= 1`，弹丸基础生命）
- `impactLifeCost`（`>= 1`，单次命中消耗生命）
- `projectileSpeed`（`>= 0`）
- `maxLifetime`（`>= 0`）
- `maxTravelDistance`（`>= 0`）
- `impactMask`: `LayerMask`
- `armoredEnemyId`: 字符串，非空时命中该敌人后有护甲倍率
- `armoredDamageMultiplier`（`>= 1`）
- `burnTriggerCount`（`>= 0`）
- `burnDamagePerSecond`（`>= 0`）
- `burnDuration`（`>= 0`）
- `slowPercent`（`0..1`）
- `slowDuration`（`>= 0`）
- `thunderChainTargetCount`（`>= 0`）
- `thunderChainRadius`（`>= 0`）
- `thunderChainDamage`（`>= 0`）
- `piercesActorsAndEnvironment`: `光` Core 使用；允许 projectile 穿过敌人和环境 collider。
- `penetrationDamageMultiplier`（`0..1`）：`光` Core 每次穿透后对后续直伤的衰减倍率，当前正式值为 `0.7`。
- `suppressImpactEffects`: `光` Core 使用；只做衰减直伤，不触发 Result、Core 状态、OnHit / OnKill payload。
- `windPressureRadius`（`>= 0`）：`风` Core 命中点风压半径，当前正式值为 `3`。
- `windPressureDistance`（`>= 0`）：`风` Core 推开距离，当前正式值为 `1.5`。
- `windDisplacementWeightLimit`（`>= 0`）：`风` Core 可推开敌人的最大位移重量，当前正式值为 `1`。
- `statusApplications`: 统一状态槽写入配置，Core 与 Result 共用 `SpellStatusApplication`

编译后生效到：

- `AttackSpec`（伤害、速度、射程、生命等）
- `CoreEffectPayload`（燃烧/减速/雷击连锁/护甲加成/状态槽写入/光穿透/风压）

当前新增核心类型：

- `AttackCoreType.Arrow`：正式 `箭` Core 使用。
- `AttackCoreType.Rock`：正式 `岩` Core 使用。
- `AttackCoreType.Water`：正式 `水` Core 使用；低伤并写入 `Wet`。
- `AttackCoreType.Wind`：正式 `风` Core 使用；高速低伤，命中点小范围推开低重量敌人。
- `AttackCoreType.Light`：正式 `光` Core 使用；穿过敌人与墙体，只做衰减直伤并抑制命中后效果。
- `AttackCoreType.Sheep`：正式 `羊` Core 使用；写入 `Polymorph`，普通低重量敌人满 3 层后强控 / 变色 4 秒。
- `AttackCoreType.Riddle`：正式 `谜` Core 使用；每发 projectile 发射时随机解析为 `箭/火/冰/雷/岩/刃/毒/影` 之一。

### 2. Behavior Token（`BehaviorTokenData`）

`CreateAssetMenu`: `Lilith/Bullet Tokens/Behavior Token`

- `behaviorType`: `AttackBehaviorType`
- `acceptsNumericValue`（是否允许紧随其后的 Value 被它消费）
- `valueParameterKind`: 默认按行为类型自动推断（`Spread/Bounce/Chain/Pierce` 为 `Count`），也可手动覆盖
- `defaultProjectileCount`（`>= 1`）
- `spreadAngleStep`（`>= 0`）
- `projectileDamageMultiplier`（`>= 0`）
- `pierceLifetimeDistanceScalePerCount`（`>= 0`）

生效逻辑：

- `Spread`：`defaultProjectileCount` 控制发射子弹数（若被值词元覆写），`spreadAngleStep` 控制扇面角间隔。
- `Bounce`：`defaultProjectileCount` 作为反弹计数（值词元可覆写）。
- `Chain`：`defaultProjectileCount` 作为链接跳数（值词元可覆写）。当前运行时命中主目标后向附近未命中过的敌人传导 50% 直伤；链跳伤害不触发 payload，不递归派生。
- `Pierce`：`defaultProjectileCount` 作为穿透计数（值词元可覆写），并按 `pierceLifetimeDistanceScalePerCount` 拉伸寿命和射程。

### 3. Result Token（`ResultTokenData`）

`CreateAssetMenu`: `Lilith/Bullet Tokens/Result Token`

- `resultType`: `AttackResultType`
- `acceptsNumericValue`（是否允许紧随其后的 Value 覆写结果参数）
- `valueParameterKind`: 默认按结果类型自动推断（可覆盖）
- `defaultExplosionRadius`（`>= 0`）
- `explosionDamageMultiplier`（`0..1`）
- `defaultEffectRadius`（`>= 0`）
- `defaultTriggerCount`（`>= 0`）
- `effectDuration`（`>= 0`）
- `childDamageMultiplier`（`0..1`）
- `defaultEffectStrength`（`>= 0`）
- `areaTickSeconds`（`>= 0`）
- `areaDamageMultiplier`（`>= 0`）
- `shieldDuration`（`>= 0`）
- `statusApplications`: 统一状态槽写入配置

生效逻辑：

- `Explosion`: 半径用 `defaultExplosionRadius`，伤害倍率用 `explosionDamageMultiplier`，时延用 `effectDuration`。
- `Split`: 默认分裂数量 `defaultTriggerCount`、子弹伤害倍率 `childDamageMultiplier`。
- `StatusEffect`: 常用为触发次数（通常映射到 `defaultTriggerCount`）和持续时间（`effectDuration`）。
- `Healing`: 作用范围 `defaultEffectRadius`。
- `Drain`: 命中后按 `directDamage * 0.5 * defaultEffectStrength` 治疗施法者；Value 使用 `Strength`。
- `Shield`: 命中后按 `directDamage * defaultEffectStrength` 给施法者添加吸收盾；护盾默认用 `shieldDuration` 持续，重复获得叠加数值并刷新持续。
- `Leave`: 命中点生成持续场；半径用 `defaultEffectRadius`，持续用 `effectDuration`，tick 间隔用 `areaTickSeconds`，每 tick 伤害用 `directDamage * areaDamageMultiplier`，并周期性应用当前 Core 状态。
- `Push / Pull`: 命中点半径 `defaultEffectRadius` 内按 `defaultEffectStrength` 作为敌人位移重量阈值，距离为 `2 * Strength`，只移动敌人。
- 状态类 Result 若只写入 `statusApplications` 且不带控制阈值，描述会显示具体状态槽，例如点燃、绑缚、腐蚀、标记、潮湿、失能。

### 4. Value Token（`ValueTokenData`）

`CreateAssetMenu`: `Lilith/Bullet Tokens/Value Token`

- `numericValue`: 可为小数，默认会四舍五入用于 `Count` 类型场景。
- `valueMode`: `Number`、`Multiplier`、`ScalePreset`。
- `scalePreset`: `Small`、`Large`、`Zero`。

注意：

- 值词元只会被“最近、可消费”的行为词元、结果词元或参数化 Trigger 消费。
- 未被消费会警告。
- `Modifier + Value + ...` 中，紧随 Modifier 的 Value 优先解释为 `NextN` 目标数量。

### 5. Modifier Token（`ModifierTokenData`）

`CreateAssetMenu`: `Lilith/Bullet Tokens/Modifier Token`

- 不再有 `scope/targetCount` 配置字段，编译时按词元位置决定作用域：
  - `Modifier + Core/Behavior/Result`：默认下一个
  - `Modifier + Value + 目标`：`NextN` 计数修饰
  - `Modifier + Multicast`：当前块修饰（`CurrentBlock`）
- 真正可调的只有 `modifiers[]`（`target + expression`）。

### 6. Multicast Token（`MulticastTokenData`）

`CreateAssetMenu`: `Lilith/Bullet Tokens/Multicast Token`

- `castCount`（`>= 2`）：本次多发要收集的 projectile segment 数
- `castPattern`: `Simultaneous`、`Sequential`、`Fork`、`Orbit`
- `sequentialIntervalSeconds`: 顺序发射间隔
- `patternAngleStep`: 分叉 / 环绕等 pattern 使用的角度步进

生效逻辑：

- 当前位置右侧按顺序收集 `castCount` 个 projectile 区段。
- 小于 `castCount` 会编译警告。
- 当前不开放 Multicast 消费 Value。
- `multicast_orbit` / `绕` 固定 `castCount = 2`：第 1 段为主弹，第 2 段发射后以主弹 `MovementTarget` 为运动锚点环绕，主弹失效时环绕弹同步过期。

### 7. Trigger Token（`TriggerTokenData`）

`CreateAssetMenu`: `Lilith/Bullet Tokens/Trigger Token`

- `triggerType`: `OnHit`、`OnTimer`、`OnExpire`、`OnKill`、`OnDistance`、`OnProximity`。
- `parameterKind`: `None`、`TimeSeconds`、`Distance`、`Radius`；不填时由 trigger type 自动推断。
- `defaultParameterValue`: 参数缺失时使用的默认值。
- 触发词后的所有可解析 token 进入 payload 区域。
- `OnTimer / OnDistance / OnProximity` 会优先消费紧随 Trigger 后的 Value 作为触发参数，该 Value 不再进入 payload。
- nested Trigger / nested Payload 当前仍 warning，不开放。

### 10. Prototype Token（`PrototypeTokenData`）

`CreateAssetMenu`: `Lilith/Bullet Tokens/Prototype Token`

隐藏设计占位专用，不参与普通编译。

- `tokenId`
- `displayText`
- `prototypeCategory`
- `description`
- `unimplementedReason`

特点：

- `AppendCompileTokens()` 不追加任何 `BaseTokenData`。
- 当前只用于 `SpellToken_Hidden_Prototype_Lib.asset`，隐藏库内权重为 0。
- 每个 Prototype 都应写清计划语义和未实装原因，供构筑调试、隐藏库检索和后续迁移使用。
- 普通奖励、普通 Token Select 和玩家自然流程不应引用它。

### 8. Linked（连锁）词元（`LinkedTokenData`）

`CreateAssetMenu`: `Lilith/Bullet Tokens/Linked Token`

- `itemId`
- `description / descriptionKey`
- `linkedTokens`：顺序会按数组顺序展开到公式中
- `damageMultiplier`（`>= 1`，按整个连锁物件统一乘伤害）
- `pickupDisplayTextOverride / pickupDisplayTextKey`

特点：占用槽位=`linkedTokens.Count`，适合“把几个词元组合成一个词条”。

### 9. Pickup Token（`PickupTokenData` 及派生）

用于场景拾取，不参与法术编译。  

- `HealingPickupTokenData`: `healingAmount`（`>= 0.01`）
- `RemnantPickupTokenData`: `remnantAmount`（`>= 1`）

## 三、SpellBook（执行器）可配参数（`SpellBookData`）

`CreateAssetMenu`: `Lilith/Spell Books/Spell Book`

- `spellBookId`
- `displayName`
- `selectionDescription`
- `slotCount`（`>= 1`）
- `castCooldownSeconds`（`>= 0`）
- `castsPerActivation`（`>= 1`）
- `activationSpreadAngleStep`（`>= 0`）
- `energyCapacity`（`>= 0`）
- `energyRegenPerSecond`（`>= 0`）
- `energyCostPerActivation`（`>= 0`）
- `executorModifiers`：同 `TokenModifierDefinition`，作用于整本法术（可对 `TextColor/FontSize/ScaleMultiplier/ProjectileSpeed/MaxLifetime/MaxTravelDistance/ImpactRadiusMultiplier/ResultCount/ResultDuration/ResultMultiplier/Damage` 生效；特殊 cast-level Modifier 还会读取 `CastCooldownMultiplier/EnergyCostMultiplier/CasterHealthCost/DropChanceMultiplierOnKill/AngleSpreadMultiplier/MovementVarianceMultiplier`；不支持的会被忽略）
- `fixedItemPlacement`：`BeforeEquipped` / `AfterEquipped`
- `fixedCastItems`：固定常驻词元列表（会与玩家词元按位置拼接）

## 四、给你一个“新增 Token”最少清单

### A. 新增一个“现有类型”的词元（最常见）

1. 新建对应类型 Asset（Core/Behavior/Result/Value/Modifier/Multicast/Trigger）。  
2. 配 `tokenId`（唯一）、显示文本、描述。  
3. 按类型填写字段（上面各节）。  
4. 如需数字增强，给 `modifiers` 增加 `TokenModifierDefinition`：
   - `target` 选作用目标；
   - `expression` 按操作符规则填写（例：`*=1.2`、`*=Color.red`）。  
5. 放到 `Assets/Data/BulletTokens/...` 对应目录并加入词库（如果有库管理）。

### B. 新增一个“语法结构词元”类型（需要代码）

只有在有新语义（新触发类型/新结构类型）才需要：

- 扩展枚举（如新增 `TokenType`/`SpellTriggerType`/`AttackResultType`）。
- 在编译链路中加解析与 fallback 警告。
- 添加展示/描述映射。
- 补充行为测试（至少编译器测试 + 描述文本测试）。

### C. 新增一个“效果词元（Core/Behavior/Result）”时建议的数值区间（经验值）

- `damage`：`1~10` 常见。
- `projectileSpeed`：`200~400` 常见。
- `spreadAngleStep`：`8~20` 常见（Spread）。  
- 半径类参数：`20~80` 常见。  
- `defaultTriggerCount`：`2~5` 常见。  
- 乘法类倍率默认 `1` 起步，小于 `0.5`/大于 `2.5` 建议再评估平衡。  

## 五、当前版本重要提醒

- `ModifierTokenData` 不需要也不再使用 `scope / targetCount` 字段；若在旧资源中看到该字段（如历史序列化），可忽略。
- Value 消费链路严格是“紧邻消费”，不要期待跨段回溯。
- `Trigger` 后自动开启 payload，不再使用显式 `PayloadStart/PayloadEnd`。
- 第一批 formal staging / hidden library 是调试与内容迁移入口，不代表已经接入奖励池。
