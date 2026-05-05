# Lilith 策划内容配置指南

这份文档面向策划协作者，目标是让你能安全地编写游戏内文本、任务、敌人、波次、词元和数值。

你不需要写代码。大多数内容在 Unity Inspector 的 `.asset` 文件里，或在 `Assets/Data` 下面的 `.json` 文件里。

## 当前游戏系统总览

| 系统 | 玩家体验 | 策划主要改哪里 |
| --- | --- | --- |
| 开场叙事 | 新档先看故事，再进入 Main | `Assets/Data/Story/Introduction.json` |
| Main 开场对话 | 第一次进 Main 后弹出对话 | `Assets/Data/Story/DialogTest.json` |
| 新手任务 | 背包、构筑、传送门引导 | `Assets/Data/Quest/QuestCatalog.json` |
| 起始房间 | 玩家出生、读书升级、进传送门开战 | 场景交互由程序维护；文案和任务可配置 |
| 战斗流程 | 进入战斗、打 6 波、Boss、结算、回起始房间 | `Assets/Data/Waves/*.asset` |
| 敌人 | 不同敌人有生命、速度、攻击、技能、描述 | `Assets/Data/Enemies/*.asset` |
| Bullet Token | 攻击由核心、行为、数值、结果等词元组成 | `Assets/Data/BulletTokens/**/*.asset` |
| Token 抽取 | 进战斗和波后出现 token 选择 | `Assets/Data/BulletTokens/SelectionPlans/*.asset` |
| 掉落 | 敌人死亡后掉落 remnant / healing / token | `Assets/Data/Waves/NonBossWaveSequenceProgression.asset` 和波次 |
| 图鉴 / 帮助 | Tab 打开指南和敌人图鉴 | `Assets/Data/UI/HintCatalog.json`，敌人正文来自 `EnemyDefinition.Description` |
| 永久升级 | 起始房间的书打开升级界面 | `Assets/Data/Upgrades/PermanentUpgradeCatalog.json` |
| 结算 | 胜败标题、统计文本、收益文案 | `Assets/Data/UI/SettlementPresentationCatalog.json` |

## 先记住的安全规则

| 要做 | 不要做 |
| --- | --- |
| 改文案、数字、颜色、掉落概率、波次数量 | 删除脚本组件、改 prefab 组件引用 |
| 复制已有 `.asset` 再改成新配置 | 直接大改不理解的字段 |
| 用小步改动测试结果 | 一次性重写多个系统 |
| 保持 JSON 格式完整 | 漏逗号、用中文标点替代英文符号 |
| 数值先保守，进游戏验证 | 给敌人或子弹极端数值导致流程卡死 |

## 当前 Unity 确认到的流程

当前主场景是 `Assets/Scenes/Main.unity`。

`MapRunFlowController` 已绑定：

| 字段 | 当前值 |
| --- | --- |
| 起始房间地图 | `StartRoomMapRoot` |
| 战斗地图 | `CombatMapRoot` |
| 玩家 | `Player` |
| 敌人生成器 | `EnemyGenerator` |
| 波次管理器 | `WaveManager` |
| 初次进战斗 token 选择 | `Assets/Data/BulletTokens/SelectionPlans/StartRoomCombatEntryTokenSelectionPlan.asset` |

`WaveManager` 当前串联 6 波：

| 顺序 | 资产 |
| --- | --- |
| 第 1 波 | `Assets/Data/Waves/Wave01.asset` |
| 第 2 波 | `Assets/Data/Waves/Wave02.asset` |
| 第 3 波 | `Assets/Data/Waves/Wave03.asset` |
| 第 4 波 | `Assets/Data/Waves/Wave04.asset` |
| 第 5 波 | `Assets/Data/Waves/Wave05.asset` |
| 第 6 波 | `Assets/Data/Waves/Wave06.asset`，Boss 波 |

普通波次的统一掉落和波后 token 选择由：

`Assets/Data/Waves/NonBossWaveSequenceProgression.asset`

## 文本配置

### 开场叙事

路径：

`Assets/Data/Story/Introduction.json`

结构：

```json
{
  "entries": [
    {
      "text": "这里写一段旁白。",
      "displayMode": "append"
    }
  ]
}
```

字段说明：

| 字段 | 怎么填 |
| --- | --- |
| `text` | 实际显示的文字 |
| `displayMode` | `append` 表示追加显示；不要随意改成别的值，除非程序确认 |

### Main 开场对话

路径：

`Assets/Data/Story/DialogTest.json`

结构：

```json
{
  "entries": [
    {
      "speakerId": "narrator",
      "displayName": "旁白",
      "displayMode": "replace",
      "text": "这里写一句对话。"
    }
  ]
}
```

字段说明：

| 字段 | 怎么填 |
| --- | --- |
| `speakerId` | 内部标识，建议英文或拼音，不显示给玩家 |
| `displayName` | 玩家看到的说话人 |
| `displayMode` | 当前对话用 `replace`，表示替换上一句 |
| `text` | 玩家看到的正文 |

### 指南和帮助

路径：

`Assets/Data/UI/HintCatalog.json`

结构：

```json
{
  "categories": [
    {
      "id": "guide",
      "title": "指南",
      "entries": [
        {
          "id": "controls",
          "title": "操作说明",
          "content": "WASD 移动。\nE 与物品交互。"
        }
      ]
    }
  ]
}
```

字段说明：

| 字段 | 怎么填 |
| --- | --- |
| `categories[].id` | 分类内部 ID，不显示，保持唯一 |
| `categories[].title` | 分类标题 |
| `entries[].id` | 条目内部 ID，保持唯一 |
| `entries[].title` | 条目标题 |
| `entries[].content` | 正文，换行用 `\n` |

敌人图鉴正文不在这个 JSON 里，而是在每个 `Assets/Data/Enemies/*.asset` 的 `Description` 字段里。

### 结算文案

路径：

`Assets/Data/UI/SettlementPresentationCatalog.json`

可改字段：

| 字段 | 含义 |
| --- | --- |
| `victoryTitles` | 胜利标题随机池 |
| `defeatTitles` | 失败标题随机池 |
| `victoryResultTemplate` | 胜利统计句式 |
| `defeatResultTemplate` | 失败统计句式 |
| `harvestHeader` | 收益标题 |
| `harvestEmptyText` | 无长期收益时文本 |
| `summaryHeader` | 击败统计标题 |
| `summaryEmptyText` | 未击败任何敌人时文本 |

模板里可用：

| 占位符 | 运行时替换成 |
| --- | --- |
| `{waves}` | 击败波次数 |
| `{bosses}` | 击败 Boss 数 |

### 永久升级文案和数值

路径：

`Assets/Data/Upgrades/PermanentUpgradeCatalog.json`

示例：

```json
{
  "sections": [
    {
      "id": "combat",
      "title": "战斗强化",
      "entries": [
        {
          "id": "damage_test",
          "title": "攻击力 +100%",
          "costRemnants": 10,
          "maxLevel": 1,
          "effectType": "DamageMultiplierBonus",
          "effectValue": 1.0
        }
      ]
    }
  ]
}
```

字段说明：

| 字段 | 含义 |
| --- | --- |
| `sections[].id` | 升级分类 ID |
| `sections[].title` | 分类显示名 |
| `entries[].id` | 升级项 ID，必须唯一 |
| `entries[].title` | 玩家看到的升级名 |
| `costRemnants` | 消耗的长期资源数量 |
| `maxLevel` | 最大等级 |
| `effectType` | 效果类型，目前已有 `DamageMultiplierBonus` |
| `effectValue` | 效果数值，例如 `1.0` 表示 +100% |

## 任务配置

路径：

`Assets/Data/Quest/QuestCatalog.json`

当前任务链：

| 任务 ID | 玩家看到的目标 |
| --- | --- |
| `tutorial_open_backpack` | 打开背包检查情况 |
| `tutorial_compile_spellbook` | 将核心拖动放置于背包上方的格子中 |
| `tutorial_enter_teleporter` | 走进书籍传送门 |

任务结构：

```json
{
  "id": "tutorial_open_backpack",
  "text": "打开背包检查情况",
  "prerequisites": [],
  "completion": [],
  "rewards": []
}
```

常用条件：

| kind | 用途 |
| --- | --- |
| `story_flag_set` | 某个剧情 / 教程标记已达成 |
| `inventory_contains_token` | 背包里拥有某个 token |
| `inventory_token_count_at_least` | 背包里某 token 数量达到要求 |
| `enemy_kill_count_at_least` | 击杀敌人数达到要求 |
| `combat_victory_count_at_least` | 战斗胜利次数达到要求 |
| `boss_kill_count_at_least` | Boss 击杀数达到要求 |
| `remnants_at_least` | 长期资源达到要求 |

常用奖励：

| kind | 用途 |
| --- | --- |
| `inventory_token` | 给玩家一个 token |
| `remnants` | 给长期资源 |
| `unlock_id` | 解锁某个 ID |
| `story_flag_set` | 设置剧情 / 教程标记 |
| `lifetime_stat_delta` | 增加长期统计 |

Token 地址写法示例：

`Assets/Data/BulletTokens/Core/InitCore`

注意这里通常不带 `.asset`。

## 敌人配置

路径：

`Assets/Data/Enemies`

当前敌人：

| 资产 | 建议定位 |
| --- | --- |
| `群.asset` | 数量型普通敌人 |
| `迅.asset` | 快速 / 冲刺敌人 |
| `甲.asset` | 装甲 / 减伤敌人 |
| `召.asset` | 召唤敌人 |
| `爆.asset` | 自爆敌人 |
| `弦.asset` | 远程 / 风筝敌人 |
| `锁.asset` | 控制敌人 |
| `愈.asset` | 治疗敌人 |
| `Boss_Phase1.asset` | Boss 一阶段 |
| `Boss_Phase2.asset` | Boss 二阶段 |

常用字段：

| 字段 | 含义 |
| --- | --- |
| `Enemy Id` | 内部 ID，保持唯一 |
| `Display Name` | 玩家看到的名字 |
| `Description` | 图鉴里显示的敌人介绍 |
| `Runtime Prefab` | 生成时用的 prefab，通常保持 `CharEnemy` |
| `Movement Kind` | 移动方式 |
| `Attack Kind` | 攻击方式 |
| `Visual` | 字形、颜色、底座、阴影 |
| `Combat / Max Health` | 最大生命 |
| `Combat / Move Speed` | 移动速度 |
| `Combat / Attack Range` | 攻击范围 |
| `Combat / Attack Cooldown` | 攻击间隔，越小越频繁 |
| `Combat / Attack Damage` | 攻击伤害 |
| `Combat / Damage Reduction Percent` | 减伤比例，`0.3` 表示减伤 30% |
| `Combat / Visual Scale Multiplier` | 视觉缩放 |

移动类型：

| 值 | 含义 |
| --- | --- |
| `None` | 不移动 |
| `ChaseTarget` | 追玩家 |
| `ChaseThenDash` | 接近后冲刺 |
| `KeepDistance` | 保持距离 |
| `AggroOnHit` | 受击后更积极追击 |
| `OrbitTarget` | 环绕目标 |
| `FollowNearestEnemyKeepDistance` | 跟随最近敌人并保持距离 |
| `BossSmartRoam` | Boss 主动游走 |

攻击类型：

| 值 | 含义 |
| --- | --- |
| `None` | 不攻击 |
| `MeleeContact` | 接触近战 |
| `RangedBulletToken` | 发射 token 子弹 |
| `ProximityExplosion` | 近距自爆 |

技能类型：

| 值 | 含义 |
| --- | --- |
| `None` | 无技能 |
| `SummonEnemy` | 召唤敌人 |
| `DelayedGroundBomb` | 玩家脚下延时爆炸预警 |

敌人数值提示：

| 调整方向 | 结果 |
| --- | --- |
| 提高 `Max Health` | 更耐打 |
| 提高 `Move Speed` | 更压迫 |
| 提高 `Attack Range` | 更早能攻击 |
| 降低 `Attack Cooldown` | 攻击更频繁 |
| 提高 `Attack Damage` | 单次伤害更高 |
| 提高 `Damage Reduction Percent` | 更硬，但不要接近 1 |

战斗内每清完一波，普通敌人战力会按 `1 + 0.04 * 已完成波次数` 自动成长。也就是说第 5 波的敌人会天然比第 1 波更强一点。

## 波次配置

路径：

`Assets/Data/Waves`

每个 `WaveXX.asset` 控制一波刷怪。

常用字段：

| 字段 | 含义 |
| --- | --- |
| `Spawn Interval Seconds` | 每只敌人生成间隔，最低约 `0.05` |
| `Randomize Enemy Spawns` | 是否随机打乱本波敌人出现顺序 |
| `Post Wave Token Selection Plan` | 本波结束后的 token 选择，Boss 波可用 |
| `Is Boss Wave` | 是否 Boss 波 |
| `Wave Token Drops` | 本波基础掉落 |
| `Apply Entry Specific Token Drops` | 是否叠加每个敌人条目的额外掉落 |
| `Enemy Spawns` | 本波会刷哪些敌人、各刷多少 |

`Enemy Spawns` 里的字段：

| 字段 | 含义 |
| --- | --- |
| `Enemy Definition` | 敌人类型 |
| `Spawn Count` | 生成数量 |
| `Token Drops` | 这个敌人条目的额外掉落 |
| `Is Boss Encounter` | 这个条目是否作为 Boss 遭遇 |
| `Boss Display Name Override` | Boss UI 显示名覆写 |
| `Boss Phase Two Definition` | 二阶段敌人定义 |
| `Boss Phase Transition Health Ratio` | 血量低于多少比例切二阶段，`0.5` 表示半血 |

普通波次掉落和波后奖励优先改：

`Assets/Data/Waves/NonBossWaveSequenceProgression.asset`

字段说明：

| 字段 | 含义 |
| --- | --- |
| `Default Non Boss Token Drops` | 未单独配置时，普通波默认掉落 |
| `Rewards By Wave` | 按第几波配置掉落和波后 token 选择 |
| `Wave Number` | 第几波，从 1 开始 |
| `Token Drops` | 这一波的掉落 |
| `Post Wave Token Selection Plan` | 这一波结束后弹出的 token 选择计划 |

掉落字段：

| 字段 | 含义 |
| --- | --- |
| `Token` | 掉落哪个 token |
| `Drop Chance` | 掉落概率，`0` 到 `1`；`0.2` 表示 20% |
| `Drop Count` | 掉几个 |

## Bullet Token 配置

路径：

`Assets/Data/BulletTokens`

### Token 类型

| 目录 | 类型 | 用途 |
| --- | --- | --- |
| `Core` | 核心词 | 决定基础属性、伤害、速度、生命周期 |
| `Behavior` | 行为词 | 决定直射、散射、穿透、反弹、追踪等 |
| `Value` | 数值词 | 给行为或结果提供数字 |
| `Result` | 结果词 | 决定命中后爆炸、分裂、控制、治疗等 |
| `Linked` | 连锁词 | 把多个基础词打包成一个可拾取物 |
| `Pickup` | 拾取词 | 长期资源或治疗等拾取物 |
| `TokenLib` | token 库 | 抽取池 |
| `SelectionPlans` | 抽取计划 | 选择时按权重抽哪个库 |

基础 token 通用字段：

| 字段 | 含义 |
| --- | --- |
| `Token Id` | 内部 ID，保持唯一 |
| `Display Text` | 玩家看到的字 |
| `Description` | 选择界面 / 帮助里显示的说明 |
| `Has Bullet Text Override` | 是否覆盖子弹最终显示文字 |
| `Bullet Text Override` | 覆盖后的子弹显示文字 |
| `Modifiers` | 视觉或运行时修饰，建议由程序协助配置 |

### Core Token

常见资产：

`FireCore`、`IceCore`、`ThunderCore`、`EdgeCore`、`InitCore`

关键字段：

| 字段 | 含义 |
| --- | --- |
| `Core Type` | 核心属性，如 Fire / Ice / Thunder / Edge |
| `Damage` | 基础伤害 |
| `Projectile Life` | 子弹可承受命中次数相关生命 |
| `Impact Life Cost` | 每次命中消耗多少生命 |
| `Projectile Speed` | 子弹速度 |
| `Max Lifetime` | 最长存在时间 |
| `Max Travel Distance` | 最远飞行距离 |
| `Burn Trigger Count` | 火焰触发燃烧所需次数 |
| `Burn Damage Per Second` | 燃烧每秒伤害 |
| `Burn Duration` | 燃烧持续时间 |
| `Slow Percent` | 冰缓速比例 |
| `Slow Duration` | 缓速时间 |
| `Thunder Chain Target Count` | 雷链目标数 |
| `Thunder Chain Radius` | 雷链范围 |
| `Thunder Chain Damage` | 雷链伤害 |

### Behavior Token

常见资产：

`Straight`、`Spread`、`Bounce`、`Pierce`、`Homing`

关键字段：

| 字段 | 含义 |
| --- | --- |
| `Behavior Type` | 行为类型 |
| `Accepts Numeric Value` | 是否读取相邻数值词 |
| `Default Projectile Count` | 默认发射数量 |
| `Spread Angle Step` | 散射角度间隔 |
| `Projectile Damage Multiplier` | 子弹伤害倍率 |
| `Pierce Lifetime Distance Scale Per Count` | 穿透相关距离倍率 |

### Result Token

常见资产：

`DirectDamage`、`Explosion`、`Split`、`Control`、`Healing`

关键字段：

| 字段 | 含义 |
| --- | --- |
| `Result Type` | 结果类型 |
| `Accepts Numeric Value` | 是否读取数值词 |
| `Default Explosion Radius` | 默认爆炸半径 |
| `Explosion Damage Multiplier` | 爆炸伤害倍率 |
| `Default Trigger Count` | 分裂数量 / 控制触发次数等 |
| `Effect Duration` | 延迟、控制、状态持续时间 |
| `Child Damage Multiplier` | 分裂子弹伤害倍率 |

### Linked Token

用于把多个 token 合成一个可拾取物，比如 `Fire-Explosion`。

关键字段：

| 字段 | 含义 |
| --- | --- |
| `Item Id` | 内部 ID |
| `Description` | 选择界面说明 |
| `Linked Tokens` | 组成它的 token 列表 |
| `Damage Multiplier` | 额外伤害倍率，最低为 1 |
| `Pickup Display Text Override` | 掉落物显示文本覆写 |

### Selection Plan

路径：

`Assets/Data/BulletTokens/SelectionPlans`

选择计划决定“弹出 token 选择界面时，从哪些 token 库抽”。

字段：

| 字段 | 含义 |
| --- | --- |
| `Library Entries` | 候选 token 库列表 |
| `Library` | 一个 `BulletTokenLibrary` |
| `Selection Weight` | 抽到这个库的权重，越大越容易抽到 |

当前进入起始房间传送门后的初始选择使用：

`Assets/Data/BulletTokens/SelectionPlans/StartRoomCombatEntryTokenSelectionPlan.asset`

## 战斗地图数值

战斗地图生成器挂在 `CombatMapRoot` 上。

常用字段：

| 字段 | 含义 |
| --- | --- |
| `Seed` | 地图随机种子 |
| `Border Wall Thickness` | 边界墙厚度 |
| `Obstacle Count Min / Max` | 障碍数量范围 |
| `Obstacle Width / Height Range` | 障碍尺寸范围 |
| `Edge Clearance Cells` | 边缘留白 |
| `Player Safe Radius Cells` | 玩家出生安全区 |
| `Spawn Annulus Half Width Cells` | 刷怪环宽度 |

建议让程序或关卡同学协助改这些字段，因为它们会直接影响可走区域、刷怪位置和战斗难度。

## JSON 编辑注意

JSON 最容易出错的地方：

| 错误 | 正确做法 |
| --- | --- |
| 少逗号 | 每个对象之间用英文逗号 |
| 多逗号 | 最后一项后面不要加逗号 |
| 中文引号 | 用英文双引号 `"` |
| 直接换行 | 字符串内部换行写 `\n` |
| ID 重复 | 同一类 ID 保持唯一 |

简单自检：

1. 改完保存文件。
2. 回到 Unity，等右下角转圈结束。
3. 看 Console 有没有红色报错。
4. 从 `StartUp` 场景进入游戏测试。

## 策划改动优先级

建议先从低风险内容开始：

1. 改 `HintCatalog.json` 和 `SettlementPresentationCatalog.json` 文案。
2. 改 `Introduction.json` 和 `DialogTest.json` 剧情。
3. 改敌人 `Description`，补完整图鉴。
4. 微调敌人生命、速度、伤害。
5. 微调 `Wave01` 到 `Wave06` 的敌人数量和间隔。
6. 调整 token 描述、掉落概率、抽取计划。
7. 最后再做新敌人、新 token、新任务链。

## 需要找程序确认的改动

以下内容不是不能做，但建议先确认：

| 改动 | 风险 |
| --- | --- |
| 新增 `effectType` | 代码里可能还没有对应效果 |
| 新增任务 `kind` | 代码里可能还没有对应条件 / 奖励 |
| 新增 Token 类型 | 代码里可能还没有编译规则 |
| 删除已有 ID | 存档、任务、引用可能找不到 |
| 大幅提高敌人数值 | 可能导致流程无法完成 |
| 改 prefab 引用 | 可能导致生成失败 |

