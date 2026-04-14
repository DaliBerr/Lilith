# Memory

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