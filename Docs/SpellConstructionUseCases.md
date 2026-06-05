# 法术构筑用例

本文档记录当前法术构筑系统的常见摆法、编译解释和运行效果。它描述的是当前实现语义，不是未来理想语法。

系统概念、编译规则和维护边界见 [`Docs/SpellConstructionSystemDesign.md`](SpellConstructionSystemDesign.md)。

## 读法约定

- `+` 表示从左到右摆放 token。
- `类别` 表示这个用例中每个槽位对应的 token 类别；`Payload(...)` 表示 Trigger 之后已经进入 payload 的局部序列。
- `Core` 是可发射投射物核心，例如 `火`、`冰`、`雷`。
- `Behavior` 仍绑定附近 Core 生效，例如 `散`、`弹`、`穿`、`追`。
- `Result` 仍绑定附近 Core 或 payload result-only 效果，例如 `爆`、`裂`、`定`、`愈`。
- `Value` 由前方最近的声明者解释，例如 `三` 可被 `散` 解释为数量，也可被 `爆` / `愈` 解释为半径。
- `Modifier` 是修饰节点，普通 Modifier 不在资产上写死作用域，而是在编译时按位置解析。
- `Multicast` 创建同一次施法里的多个 outer projectile。
- `Trigger` 后自动进入 payload，不再使用 `PayloadStart` / `PayloadEnd`。
- 当前 Trigger 实现只有 `OnHit`：外层 projectile 命中后执行 payload。

## 单发基础构筑

### `火`

类别：Core

解释：生成一个外层 `SpellCastBlock`，里面有一个 Fire projectile。

效果：发射一枚默认直线火弹，使用 Fire core 的基础伤害、速度、生命和核心效果。

### `火 + 散`

类别：Core + Behavior

解释：`散` 作为 Behavior 绑定到前方的 `火`。

效果：火弹改为散射行为，数量使用 `散` 的默认值。

### `火 + 散 + 三`

类别：Core + Behavior + Value

解释：`散` 声明自己可消费 Count 类型 Value，因此 `三` 被解释为散射数量。

效果：同一次 projectile node 会发射三道散射火弹。

### `火 + 爆`

类别：Core + Result

解释：`爆` 作为 Result 绑定到前方的 `火`。

效果：火弹命中时触发爆炸，半径使用 Explosion 的默认半径。

### `火 + 爆 + 三`

类别：Core + Result + Value

解释：当前 Explosion 资产声明 Value 槽为 Radius，因此 `三` 被解释为爆炸半径。

效果：火弹命中后产生半径为 3 的爆炸。

### `火 + 愈 + 三`

类别：Core + Result + Value

解释：当前 Healing 资产声明 Value 槽为 Radius，因此 `三` 被解释为治疗范围。

效果：普通 Healing result 带 3 半径范围语义。命中主目标后的范围治疗会跳过已直击治疗的主目标，治疗附近合法目标。

### `火 + 定 + 三`

类别：Core + Result + Value

解释：Control / StatusEffect 在默认兼容路径中可把 `三` 解释为控制触发次数；若资产显式声明 Duration，则会改解释为持续时间。

效果：取决于 Control token 的 ValueParameterKind。当前常见控制语义是把控制阈值或相关参数写为 3。

### `三`

类别：Value

解释：没有 Core，也没有可消费这个 Value 的前方 token。

效果：不可发射，给出 warning / error。

## Value 消费规则

### `Behavior + Value`

例子：`火 + 散 + 三`

类别：Core + Behavior + Value

解释：当 Behavior 声明可消费 Value 时，紧随其后的 Value 被它消费。

效果：常见是 Count，例如散射数量。

### `Result + Value`

例子：`火 + 爆 + 三`、`火 + 愈 + 三`

类别：Core + Result + Value

解释：当 Result 声明可消费 Value 时，紧随其后的 Value 被它消费。

效果：可落到 Count、Radius 或 Duration，取决于 Result 资产的 `SpellValueParameterKind`。

### `Modifier + Value`

例子：`疾 + 三 + 火 + 散 + 爆`

类别：Modifier + Value + Core + Behavior + Result

解释：Value 紧跟在 Modifier 后时，会优先被 Modifier 消费为 `NextN` 目标数量。

效果：`疾` 尝试修饰后续 3 个合法目标。合法目标通常是 Core / Behavior / Result；不会跨 trigger/payload 边界。具体数值载荷只会在目标支持对应运行时字段时产生可见变化。

### 孤立或无人消费的 Value

例子：`火 + 三`、`火 + 触 + 三`

类别：Core + Value；或 Core + Trigger + Payload(Value)

解释：没有合适消费者时，Value 不会猜测绑定。

效果：Value 被忽略并给 warning。

## Modifier 自适应作用域

### `疾 + 火`

类别：Modifier + Core

解释：Modifier 后面最近合法目标是单个 Core，因此解析为 `NextToken`。

效果：只让这枚火弹应用 `疾` 的数值载荷，例如速度增加。

### `疾 + 火 + 爆`

类别：Modifier + Core + Result

解释：`疾` 只修饰后面的第一个合法目标 `火`。

效果：火弹变快，后面的 `爆` 不会被这个 Modifier 影响。若想同时修饰多个目标，需要用 `Modifier + Value` 或把 Modifier 放在 Multicast 前。

### `疾 + 三 + 火 + 散 + 爆`

类别：Modifier + Value + Core + Behavior + Result

解释：`疾` 消费 `三`，解析为 `NextN`，目标数量为 3。

效果：后续三个合法目标都记录为 `疾` 的目标，也就是 `火`、`散`、`爆`。这不代表自动产生三个 projectile，而是同一个单发结构中的 Core / Behavior / Result 都处在这个 Modifier 的目标范围内。

### `疾 + 三 + 火 + 触 + 冰`

类别：Modifier + Value + Core + Trigger + Payload(Core)

解释：`疾` 想修饰 3 个目标，但 Trigger 开启 payload 后形成边界。外层只剩 `火` 一个合法目标。

效果：`疾` 只作用外层火弹；payload 内的冰不会被修饰。剩余 2 个 NextN 额度作废并给 warning。

### `疾`

类别：Modifier

解释：Modifier 后面没有合法目标。

效果：不可形成有效修饰，给 warning；如果整式没有 Core，还会不可发射。

### `疾 + 爆`

类别：Modifier + Result

解释：Modifier 可以绑定 Result，但没有 Core 时仍不能发射。

效果：`疾` 可解析到 `爆` 这个 Result 目标，但整式缺少 Core，最终不可发射。

### `放 + 双 + 火 + 冰`

类别：Modifier + Multicast + Core + Core

解释：Modifier 紧贴 upcoming Multicast 结构，解析为 `CurrentBlock`。

效果：`放` 作为 CastBlock 级修饰，作用于这个 Multicast 收集出的火和冰两个 projectile。

### `放 + 三 + 双 + 冰 + 火`

类别：Modifier + Value + Multicast + Core + Core

解释：因为 Modifier 紧贴 Multicast 结构时优先成为 block modifier，`三` 不会被当作 NextN 数量。

效果：`放` 修饰整个 CastBlock；`三` 被忽略并给 warning。

### payload 内 `疾 + 冰`

例子：`火 + 触 + 疾 + 冰`

类别：Core + Trigger + Payload(Modifier + Core)

解释：Trigger 后进入 payload，payload 内的 Modifier 只在 payload 局部向后解析。

效果：外层火弹不受 `疾` 影响；命中后 payload 生成的冰弹被 `疾` 修饰。

### payload 内 `域 + 三 + 爆 + 愈 + 定`

类别：Payload(Modifier + Value + Result + Result + Result)

解释：`域` 消费 `三` 作为 NextN，局部修饰 payload 内三个 result-only 效果。

效果：`爆` 的爆炸半径、`愈` 的治疗范围、`定` 的控制范围会按 `域` 的 `ImpactRadiusMultiplier` 映射被修饰。

### 普通 Modifier 不自动成为 GlobalProgram

类别：Modifier 规则说明

解释：尾部 Modifier、外层普通 Modifier 或 payload 外普通 Modifier 都不会自动推断为整式全局效果。

效果：需要全局术式时，应通过专门的全局入口或未来变体实现，不由普通 Modifier 位置猜测。

## Multicast 构筑

### `双 + 火 + 冰`

类别：Multicast + Core + Core

解释：`双` 请求收集右侧两个 projectile segment。

效果：同一次施法发射两个 outer projectile：火和冰。

### `双 + 火 + 爆 + 冰`

类别：Multicast + Core + Result + Core

解释：第一个 segment 是 `火 + 爆`，第二个 segment 是 `冰`。

效果：同一次施法发射一枚带爆炸结果的火弹，以及一枚普通冰弹。

### `双 + 火 + 散 + 三 + 冰`

类别：Multicast + Core + Behavior + Value + Core

解释：第一个 segment 是 `火 + 散 + 三`，第二个 segment 是 `冰`。

效果：同一次施法发射三道散射火弹语义的第一个 projectile node，以及一个普通冰 projectile node。

### `双 + 火 + 冰 + 雷`

类别：Multicast + Core + Core + Core

解释：`双` 只请求两个 outer projectile，所以收集 `火` 和 `冰` 后停止。

效果：发射火和冰；`雷` 是 Multicast 已收集满后的尾随 token，会被忽略并给 warning。

### `双 + 火`

类别：Multicast + Core

解释：`双` 请求两个 projectile segment，但只找到一个。

效果：保留可编译的火 projectile，同时给出“Multicast requested 2 projectile nodes but only found 1”类 warning。

### `双 + 火 + 双 + 冰`

类别：Multicast + Core + Multicast + Core

解释：当前不启用嵌套 Multicast。内层 `双` 会被 warning 忽略。

效果：通常会得到火、冰两个 outer projectile，但会记录 nested multicast warning。

### `放 + 双 + 火 + 冰`

类别：Modifier + Multicast + Core + Core

解释：见 Modifier 章节。`放` 作为 CurrentBlock modifier。

效果：火和冰都被 `放` 修饰。

## Trigger / Payload 构筑

### `火 + 触 + 冰`

类别：Core + Trigger + Payload(Core)

解释：`火` 是外层 projectile；`触` 后面的 `冰` 自动成为 payload。

效果：发射火弹。火弹命中后，在命中位置执行 payload，发射冰弹。

### `火 + 触 + 爆`

类别：Core + Trigger + Payload(Result)

解释：payload 内没有 Core，`爆` 被编译为 result-only payload effect。

效果：火弹命中后，在命中点触发爆炸。

### `火 + 触 + 爆 + 三`

类别：Core + Trigger + Payload(Result + Value)

解释：`爆` 在 result-only payload 中消费 `三` 作为 Radius。

效果：火弹命中后，在命中点触发半径为 3 的爆炸。

### `火 + 时 + 三 + 爆`

类别：Core + Trigger + Value + Payload(Result)

解释：`时` 是 OnTimer Trigger，会优先消费紧随其后的 `三` 作为延迟时间；被消费的 `三` 不进入 payload，payload 只剩 `爆`。

效果：发射火弹。3 秒后，在火弹当前位置执行 payload 爆炸。

### `火 + 程 + 五 + 雷`

类别：Core + Trigger + Value + Payload(Core)

解释：`程` 是 OnDistance Trigger，会优先消费 `五` 作为飞行距离；后续 `雷` 成为 payload 内层 Core。

效果：火弹飞行 5 单位距离后，在当前位置释放一枚雷弹。

### `火 + 近 + 三 + 定`

类别：Core + Trigger + Value + Payload(Result)

解释：`近` 是 OnProximity Trigger，会优先消费 `三` 作为感应半径；payload 内 `定` 是 result-only 控制效果。

效果：火弹进入敌人 3 单位范围内时，在火弹当前位置执行控制 payload，并记录最近目标作为上下文。

### `火 + 终 + 爆`

类别：Core + Trigger + Payload(Result)

解释：`终` 是 OnExpire Trigger，不消费 Value；后续 `爆` 成为 payload。

效果：火弹消失或寿命结束时，在消失点触发爆炸。

### `火 + 灭 + 傀`

类别：Core + Trigger + Payload(Result)

解释：`灭` 是 OnKill Trigger，不消费 Value；后续 `傀` 成为击杀后的 payload 效果。

效果：当这枚火弹造成敌人死亡时，在死亡目标位置执行 payload。正式傀儡资产和召唤表现仍需后续配置。

### `火 + 触 + 愈 + 三`

类别：Core + Trigger + Payload(Result + Value)

解释：`愈` 在 result-only payload 中消费 `三` 作为 Radius。

效果：火弹命中后，在命中点附近做范围治疗。半径为 0 时保持主目标单体治疗，半径大于 0 时治疗附近合法目标。

### `火 + 触 + 裂 + 三`

类别：Core + Trigger + Payload(Result + Value)

解释：`裂` 在 result-only payload 中消费 `三` 作为 Split 派生数量。

效果：火弹命中后，在命中点派生 3 枚无 payload 的 DirectDamage 子弹，避免递归失控。

### `火 + 触 + 定 + 三`

类别：Core + Trigger + Payload(Result + Value)

解释：`定` 在 result-only payload 中消费 `三`，当前常见语义是控制触发次数或资产声明的相关 Value 槽。

效果：火弹命中后对目标登记控制效果。若控制范围被写入为大于 0，则会在命中点附近对合法敌人登记控制命中。

### `火 + 触 + 疾 + 冰`

类别：Core + Trigger + Payload(Modifier + Core)

解释：payload 内有 Core，因此 payload 被编译为 inner projectile program；`疾` 只在 payload 内修饰 `冰`。

效果：火弹命中后发射一枚被加速的冰弹；外层火弹不被 `疾` 修饰。

### `火 + 触 + 域 + 爆`

类别：Core + Trigger + Payload(Modifier + Result)

解释：payload 内没有 Core，`域` 作为 payload 局部 Modifier 修饰后续 result-only `爆`。

效果：火弹命中后爆炸，并按 `域` 的数值载荷调整爆炸半径。

### `火 + 触`

类别：Core + Trigger

解释：Trigger 后没有 payload token。

效果：外层火弹仍可发射，但不会附着 payload，并给出 empty trigger payload warning。

### `火 + 触 + 冰 + 雷`

类别：Core + Trigger + Payload(Core + Core)

解释：Trigger 后到整式末尾都是 payload。payload 内出现多个 Core，但没有 inner Multicast。

效果：当前会按单 projectile payload 编译：第一个 Core `冰` 成为 payload projectile，后续 `雷` 会作为重复 Core 被 warning 忽略。若想让 payload 同时发射冰和雷，应写成 `火 + 触 + 双 + 冰 + 雷`。

### `火 + 触 + 双 + 冰 + 雷`

类别：Core + Trigger + Payload(Multicast + Core + Core)

解释：payload 内有 inner Multicast，`双` 收集冰和雷两个 payload projectile。

效果：外层火弹命中后，payload 同时发射冰和雷。

### `火 + 触 + 火 + 触 + 冰`

类别：Core + Trigger + Payload(Core + Trigger + Core)

解释：当前不启用 nested trigger / nested payload / wrapping。

效果：不要这样 author。嵌套 Trigger 会被视为未启用能力，可能 warning 或只保留可安全编译的外层结构。

## Multicast 与 Trigger 的组合

### `双 + 火 + 触 + 冰 + 雷`

类别：Multicast + Core + Trigger + Payload(Core + Core)

解释：`双` 请求两个 outer projectile。第一个 segment 读到 `火 + 触` 后进入 payload，随后 `冰 + 雷` 都归这个 segment 的 payload，不会回到兄弟 outer projectile。

效果：只形成一个 outer 火 projectile，并附带 payload。由于第二个 outer projectile 读不到足够 token，会给 Multicast 不足 warning。payload 内 `冰 + 雷` 没有 inner Multicast，因此按单 projectile payload 编译，冰成为 payload projectile，雷作为重复 Core warning。

### `双 + 火 + 触 + 双 + 冰 + 雷`

类别：Multicast + Core + Trigger + Payload(Multicast + Core + Core)

解释：第一个 outer segment 是 `火 + 触 + 双 + 冰 + 雷`，其中 payload 自己有 inner Multicast。

效果：外层只发射火 projectile，并因第二个 outer projectile 不足给 warning；火命中后 payload 同时发射冰和雷。

### `双 + 火 + 冰 + 触 + 爆`

类别：Multicast + Core + Core + Trigger + Payload(Result)

解释：第一个 outer segment 是火，第二个 outer segment 是 `冰 + 触 + 爆`。

效果：同一次施法发射火和冰。冰命中后触发 result-only 爆炸 payload；火没有 payload。

### `放 + 双 + 火 + 触 + 爆 + 冰`

类别：Modifier + Multicast + Core + Trigger + Payload(Result + Core)

解释：`放` 修饰整个 upcoming CastBlock。第一个 segment `火 + 触 + 爆 + 冰` 进入 payload 后吞掉 `爆 + 冰`，第二个 outer segment 不足。

效果：外层只有被 `放` 修饰的火 projectile，命中后执行 payload；同时给 Multicast 不足 warning。若想让冰成为第二个 outer projectile，需要避免让它落在第一个 Trigger 后，例如调整为 `放 + 双 + 火 + 冰 + 触 + 爆`。

## Result-only Payload 的 Modifier 映射

### `火 + 触 + PayloadAmplifyModifier + 爆`

类别：Core + Trigger + Payload(Modifier + Result)

解释：命名上是 payload 样本 Modifier；实际生效来自它摆在 payload 内。

效果：放大 result-only Explosion 的伤害倍率，不改变爆炸半径。

### `火 + 触 + PayloadRadiusModifier + 爆`

类别：Core + Trigger + Payload(Modifier + Result)

解释：payload 内 Modifier 修饰 result-only Explosion 的半径目标。

效果：放大 Explosion 半径，不改变伤害倍率。

### `火 + 触 + PayloadRadiusModifier + 愈`

类别：Core + Trigger + Payload(Modifier + Result)

解释：同一个 `ImpactRadiusMultiplier` 目标也映射到 Healing 的通用 `effectRadius`。

效果：放大 Healing 范围，不改变治疗倍率。

### `火 + 触 + PayloadCountModifier + 裂`

类别：Core + Trigger + Payload(Modifier + Result)

解释：`ResultCount` 映射到 Split 的派生 projectile 数量。

效果：增加 Split 派生数量，不改变 Split 子弹伤害倍率。

### `火 + 触 + PayloadControlFieldModifier + 定`

类别：Core + Trigger + Payload(Modifier + Result)

解释：`ImpactRadiusMultiplier =1.25` 写入或修饰 Control 的 `effectRadius`。

效果：把原本单体控制扩展为命中点附近的范围控制。

### `火 + 触 + PayloadAmplifyModifier + 愈`

类别：Core + Trigger + Payload(Modifier + Result)

解释：`ResultMultiplier` 对 Healing 映射到 healing multiplier。

效果：放大治疗倍率，不改变治疗范围。

## 法术书执行器用例

### Apprentice Spellbook

类别：SpellBook Executor；固定 token：无

结构：无固定 token，5 槽，基础冷却。

效果：玩家装备什么 token，就按装备序列编译。适合作为默认执行器。

### Quick Spellbook

类别：SpellBook Executor；固定前置：Modifier；装备例：Core

结构：固定前置 `HasteModifier`，4 槽，快冷却，原生伤害 `*=0.85`。

例子：装备 `火` 后，执行序列近似为 `HasteModifier + 火`。

效果：火弹更快，冷却更短，但基础伤害被压低。

### Wide Spellbook

类别：SpellBook Executor；固定前置：Modifier；装备例：Multicast + Core + Core

结构：固定前置 `BlockAmplifyModifier`，7 槽，慢冷却，每次激活 2 轮，激活扇形 10 度。

例子：装备 `双 + 火 + 冰` 后，整次 CastBlock 会被放大，并且每次激活发射多轮扇形。

效果：偏大范围和多 projectile 输出，节奏较慢。

### Trigger Spellbook

类别：SpellBook Executor；固定后置：Trigger + Payload(Result)；装备例：Core

结构：固定后置 `OnHitTrigger + Explosion`，6 槽，原生 payload `ResultMultiplier *=1.25`。

例子：装备 `火` 后，执行序列近似为 `火 + OnHitTrigger + Explosion`。

效果：火弹命中后自动触发爆炸 payload，且爆炸伤害倍率获得法术书原生加成。

### Binding Spellbook

类别：SpellBook Executor；固定后置：Trigger + Payload(Result)；装备例：Core

结构：固定后置 `OnHitTrigger + Control`，5 槽，原生 `ResultCount =1` / `ResultDuration *=1.5`。

例子：装备 `冰` 后，执行序列近似为 `冰 + OnHitTrigger + Control`。

效果：冰弹命中后触发控制 payload，控制阈值降为 1 次，持续时间放大到 1.5 倍。

### Surge Spellbook

类别：SpellBook Executor；固定 token：无；原生执行器修饰：Damage Modifier

结构：5 槽，短冷却，每次激活 3 轮，8 度扇形，带能量容量 / 消耗 / 回复，原生伤害 `*=0.8`。

效果：短时间爆发强，但受能量门槛约束，适合高频短链构筑。

## Warning 与不推荐摆法

### 没有 Core

例子：`爆 + 三`

类别：Result + Value

结果：普通外层公式缺少 Core，不能发射。若它位于 Trigger 后且 payload 内无 Core，则可作为 result-only payload effect。

### 重复 Core

例子：`火 + 冰`

类别：Core + Core

结果：普通单 projectile 公式中第一个 Core 建立 attack base，后续 Core 会作为重复 Core warning。若要同次发射多个 Core，使用 `双 + 火 + 冰`。

### 重复 Behavior 或 Result

例子：`火 + 散 + 追`、`火 + 爆 + 裂`

类别：Core + Behavior + Behavior；或 Core + Result + Result

结果：当前单 projectile 只保留第一类对应槽位，重复项 warning。若要组合多个命中后效果，应优先考虑 payload result-only 或未来扩展。

### Modifier 在尾部

例子：`火 + 疾`

类别：Core + Modifier

结果：普通 Modifier 只向后解析，不回头影响已结束 token，因此 `疾` 不会修饰火，给 warning。

### Trigger 后想回到外层

例子：`火 + 触 + 冰 + 雷`

类别：Core + Trigger + Payload(Core + Core)

结果：Trigger 后到整式末尾都是 payload，不会自动把 `雷` 解释回外层。需要 outer 多发时用 Multicast 并把 Trigger 放到对应 segment 的尾部，或调整顺序。

### 显式 payload 边界

例子：`火 + 触 + PayloadStart + 爆 + PayloadEnd`

类别：Core + Trigger + 已删除 PayloadBoundary + Result + 已删除 PayloadBoundary

结果：当前系统已删除这类 token。不要再创建、抽取或摆放 payload boundary。

### 普通 Modifier 想全局生效

例子：`火 + 爆 + 疾`

类别：Core + Result + Modifier

结果：尾部 `疾` 不会自动变成全局。全局术式应使用专门入口，不由普通 Modifier 猜测。

## 快速对照表

| 构筑 | 类别 | 编译解释 | 运行效果 |
| --- | --- | --- | --- |
| `火` | Core | 单 Core projectile | 发射火弹 |
| `火 + 散 + 三` | Core + Behavior + Value | Spread 消费 Count Value | 三道散射火弹 |
| `火 + 爆 + 三` | Core + Result + Value | Explosion 消费 Radius Value | 火弹命中后半径 3 爆炸 |
| `疾 + 火` | Modifier + Core | Modifier -> NextToken | 只修饰火 |
| `疾 + 三 + 火 + 散 + 爆` | Modifier + Value + Core + Behavior + Result | Modifier -> NextN(3) | 目标范围覆盖火、散、爆 |
| `疾 + 三 + 火 + 触 + 冰` | Modifier + Value + Core + Trigger + Payload(Core) | NextN 被 Trigger 边界截断 | 只修饰外层火，payload 冰不受影响，warning |
| `放 + 双 + 火 + 冰` | Modifier + Multicast + Core + Core | Modifier -> CurrentBlock | 火和冰都被 CastBlock 修饰 |
| `双 + 火 + 冰` | Multicast + Core + Core | Multicast 两个 segment | 同次施法发射火和冰 |
| `火 + 触 + 冰` | Core + Trigger + Payload(Core) | OnHit payload projectile | 火命中后发射冰 |
| `火 + 触 + 爆 + 三` | Core + Trigger + Payload(Result + Value) | OnHit result-only payload | 火命中后半径 3 爆炸 |
| `火 + 触 + 双 + 冰 + 雷` | Core + Trigger + Payload(Multicast + Core + Core) | payload 内 Multicast | 火命中后同时发射冰和雷 |
| `双 + 火 + 触 + 冰 + 雷` | Multicast + Core + Trigger + Payload(Core + Core) | Trigger segment 吞掉后续 payload | 外层只保留火，Multicast 不足 warning |
| `火 + 触` | Core + Trigger | 空 payload | 火可发射，但无 payload，warning |
