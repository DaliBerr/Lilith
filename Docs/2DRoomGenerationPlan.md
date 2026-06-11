# Lilith 2D Room Generation Plan

## Summary

Lilith 当前 2D 调试房间仍以手写 `RoomTemplateData` 模板和少量模板选择为主。这种方式能快速验证 Tilemap、2D 玩家和相机接线，但不能长期支撑可控随机房间：房间变化依赖人工新增模板，随机性不足；若直接改成完全随机 tile，又容易产生堵门、孤岛、出生点过近、战斗空间碎裂等不可玩结果。

本计划建立一套新的 2D 单房间生成系统，基底为“路径优先的约束生成 + 验证修复”。系统先生成可玩骨架，再放置受控内容，最后统一验证和修复。替换边界限定在单房间层：保留房间图和 Tilemap 呈现边界，但最终运行时不再让 `RoomTemplateData` 作为房间布局来源。

## Goals And Non-Goals

### Goals

- 使用算法生成 `Start / Combat / Reward / Boss` 四类单房间布局。
- 同一 seed、房间类型、门方向和参数必须生成稳定结果。
- 输出结构必须能被现有 Tilemap 呈现层消费，并保留地面、墙、门、玩家入口、敌人出生点和房间特殊锚点。
- 每个房间都必须通过可达性、通路宽度、可战斗面积、障碍密度、出生距离和房间类型规则验证。
- 生成失败时必须通过修复、重试或算法级 safe room 收敛，不允许回退到旧模板。

### Non-Goals

- 不重写完整房间图拓扑生成。
- 不接正式门交互、房间切换、波次、奖励、Boss 行为或结算流程。
- 不进入 PlayMode 验证；运行态体验由用户手动验证。
- 不保留 `RoomTemplateData` 运行时兼容路径。
- 不在本文末尾留下未定义开放项、可选方向或额外任务列表。

## Step 1 - 建立房间生成输入 / 输出模型

### Intent

建立新生成器的稳定数据边界，让后续算法、验证器、修复器和呈现层都围绕同一份纯逻辑输入输出工作。

### Required Input

- `seed`：确定性随机源。
- `roomKind`：`Start / Combat / Reward / Boss`。
- `widthRange` / `heightRange`：房间尺寸范围。
- `requiredDoorDirections`：本房间必须具备的门方向。
- `difficultyTier` 或等价阶段参数：用于决定障碍密度、敌人出生点数量和特殊锚点数量。

### Required Output

- 房间宽高。
- 每格 surface：至少包含 `Ground` 和 `Wall`。
- 门位置与方向。
- 玩家入口格。
- 敌人出生候选格。
- 房间特殊锚点：`RewardAnchor`、`BossAnchor` 等以类型化数据表达，不直接依赖 GameObject。

### Constraints

- 生成器必须是纯逻辑类型，不依赖 `MonoBehaviour`、场景对象、Tilemap、Prefab 或 Unity Editor 状态。
- 生成器输出必须可转换为现有 `RoomResolvedLayout` 或它的明确替代结构；Tilemap 呈现层不承担生成逻辑。
- 不引入四套完全独立的房间算法；四类房间共享生成管线，通过 profile / rule 约束差异化。
- 不使用 `RoomTemplateData` 作为运行时输入；旧模板资产只可在实施期间作为人工参考。

### Completion Criteria

- 新输入 / 输出模型可在 EditMode 测试中直接构造和断言。
- 同一输入连续生成两次，结果具备可比较的确定性签名。
- 输出能表达四类房间的必要锚点，不需要额外查找场景对象。

## Step 2 - 实现路径优先的基础房间生成

### Intent

先保证房间一定可通行，再引入随机变化。所有随机地形内容必须服从主路径和安全区。

### Required Behavior

- 先生成外墙和基础可通行区域。
- 按 `requiredDoorDirections` 在边界开门。
- 根据房间类型选择玩家入口位置：
  - `Start`：入口位于安全区中心或靠近主入口。
  - `Combat`：入口远离敌人出生区，且能通向全部门。
  - `Reward`：入口通向奖励锚点和全部门。
  - `Boss`：入口通向 Boss 锚点和全部门，并保留更大中央空间。
- 为玩家入口到所有门生成必通路径。
- 为房间关键锚点生成必通路径。
- 主路径保留最小宽度，不允许被后续内容覆盖。

### Constraints

- 任何随机障碍、掩体、装饰性墙体或出生点都不能先于必通路径生成。
- 主路径和玩家安全区必须被标记为 reserved cells，后续步骤只能读取，不能覆盖。
- 门不能开在角落；门周围必须保留最小进出缓冲区。
- `Boss` 房必须比普通 `Combat` 房使用更低障碍密度和更大的连续可战斗面积。

### Completion Criteria

- 四类房间均能在只生成基础骨架时通过可达性验证。
- 从玩家入口到每个门都至少存在一条有效路径。
- 房间外圈除门以外均为墙。
- 骨架生成不依赖受控内容放置步骤也能产出合法房间。

## Step 3 - 加入受控内容放置

### Intent

在已保证可玩的基础上增加战斗变化。随机只发生在候选区域内，并由 placement rule 限制。

### Required Behavior

- 计算可放置区域：排除主路径、门缓冲区、玩家安全区、房间特殊锚点和边界墙。
- 放置障碍 / 掩体：
  - 仅允许在候选区域放置。
  - 放置后不得破坏必通路径。
  - 每次放置都记录为可撤销 placement。
- 放置敌人出生点：
  - `Combat` 和 `Boss` 必须生成敌人出生候选点。
  - `Start` 不生成敌人出生点，除非明确作为教程测试 profile，但 v1 不启用该 profile。
  - `Reward` 不生成敌人出生点。
- 放置特殊锚点：
  - `Reward` 必须生成奖励锚点。
  - `Boss` 必须生成 Boss 锚点。
  - `Start` 必须生成玩家入口锚点。

### Constraints

- 内容放置不能直接随机改写任意 tile。
- 敌人出生点与玩家入口必须满足最小距离。
- 敌人出生点不能位于单格瓶颈、门缓冲区或主路径必经瓶颈。
- 障碍密度、敌人出生点数量、锚点数量由房间 profile 决定，不在运行时硬编码散落到多个系统。
- 所有 placement 失败必须返回明确原因，供 Step 4 修复策略使用。

### Completion Criteria

- `Start / Reward` 不产生敌人出生点。
- `Combat / Boss` 产生合法敌人出生候选点。
- `Reward` 产生奖励锚点，`Boss` 产生 Boss 锚点。
- 障碍和掩体放置后，入口到所有门仍可达。

## Step 4 - 建立验证、修复和失败策略

### Intent

把“可玩性”变成统一规则，而不是依赖生成步骤的假设。所有最终房间必须通过同一个验证器。

### Required Validation

- 玩家入口存在且位于地面。
- 所有门存在、位于边界且从玩家入口可达。
- 所有特殊锚点从玩家入口可达。
- 地面区域不存在不可达孤岛，或孤岛必须被修复为墙。
- 主路径最小宽度满足 profile 要求。
- 可战斗面积满足房间类型要求。
- 障碍密度在 profile 范围内。
- 敌人出生点数量、距离和可达性满足房间类型要求。
- `Start / Reward` 不含敌人出生点。
- `Boss` 含 Boss 锚点并满足更大连续空间要求。

### Required Failure Strategy

按以下顺序处理失败：

1. 局部修复：打通路径、拓宽瓶颈、把不可达孤岛改为墙。
2. 撤销最近 placement：移除导致失败的障碍、掩体或出生点。
3. 重试生成：使用同 seed 派生出的稳定 retry index，不允许非确定性重试。
4. 算法级 safe room：生成满足当前房间类型最低规则的简化房间。
5. 明确错误：只有 safe room 也无法生成时才返回错误。

### Constraints

- 不使用旧模板 fallback。
- 修复器不能绕过验证器；修复后必须重新验证。
- safe room 必须仍由新生成器算法产出，不能读取旧模板资产。
- 验证错误必须可测试、可定位，不只返回通用失败。

### Completion Criteria

- 人工构造的堵门、孤岛、敌人过近、障碍过密案例会被验证器拒绝。
- 可修复案例能通过修复器转为合法房间。
- 不可修复案例能通过 deterministic retry 或 safe room 收敛。
- 通过验证的房间可被 Tilemap 呈现层正确绘制。

## Step 5 - 替换运行时单房间来源并完成收口

### Intent

让 2D 调试房间系统只通过新单房间生成器产出房间，清理旧模板运行时依赖，并把测试和文档状态收束到新系统。

### Required Changes

- `ProceduralRoomMapDebugController` 从新生成器取得当前房间 layout。
- `RoomGraphGenerator` 仍负责房间图节点和连接关系，但节点解析 layout 时不再选择 `RoomTemplateData`。
- `TilemapRoomPresenter` 继续只负责呈现 layout，不新增生成逻辑。
- 移除运行时对 `roomTemplates` 列表的依赖。
- 清理旧模板运行时测试假设，改为测试新生成器和验证器。
- 更新 README 的 2D 房间说明，明确单房间来源已改为路径优先约束生成。

### Constraints

- 本步骤结束后，2D 调试房间系统只能通过新单房间生成器产出房间。
- 不允许在运行时保留 `RoomTemplateData` fallback、双路径生成、兼容桥接或“临时可选模板模式”。
- Tilemap 呈现边界必须保留：生成器不直接操作 Tilemap。
- 不接正式门交互、波次、奖励、Boss 行为或结算流程。
- 不进入 PlayMode；运行态体验由用户手动验证。
- 文档不得留下未定义开放项、可选方向或额外任务列表。

### Completion Criteria

- `ProceduralRoomMapDebugController` 渲染的当前房间来自新生成器。
- `Start / Combat / Reward / Boss` 四类房间均有 EditMode 覆盖。
- 确定性、可达性、验证失败、修复、safe room、Tilemap 呈现均有 EditMode 覆盖。
- 静态搜索确认 2D 调试房间运行时不再依赖 `RoomTemplateData` 作为布局来源。
- README、测试名和计划文档对系统边界描述一致。

## Test Plan

文档落地阶段：

- 确认 `Docs/2DRoomGenerationPlan.md` 存在。
- 确认文档包含 Summary、Goals And Non-Goals、5 个步骤和 Test Plan。
- 确认文档明确禁止 PlayMode 由 agent 执行。
- 确认文档没有未定义开放项、可选方向或额外任务列表。

后续实现阶段必须包含以下 EditMode 测试名或等价覆盖：

- `RoomGenerator_SameSeed_ProducesSameLayout`
- `RoomGenerator_AllRoomKinds_PassValidation`
- `RoomValidator_RejectsBlockedDoorPaths`
- `RoomRepair_RemovesBlockingObstacle`
- `RoomGenerator_FallsBackToAlgorithmicSafeRoom_WhenRetriesFail`
- `ProceduralRoomMapDebugController_RendersGeneratedCurrentRoom`

## Implementation Ownership

- 本文档落地任务不委派子代理。
- 本文不设置子代理步骤；执行者按本文五个步骤顺序完成。

## Assumptions And Defaults

- 文档路径固定为 `Docs/2DRoomGenerationPlan.md`。
- v1 覆盖 `Start / Combat / Reward / Boss` 四类房间。
- 四类房间通过不同 profile 约束同一生成管线，不建立四套独立算法。
- 最终运行时不保留 `RoomTemplateData` fallback。
- 旧模板资产可作为实施参考，但不作为新系统依赖。
- agent 不进入 Lilith PlayMode，不运行 PlayMode 测试。
