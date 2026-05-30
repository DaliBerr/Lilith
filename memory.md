# Memory

## Obsidian Global Memory Migration

Obsidian vault is now the primary cross-session memory layer for agent workflow, project handoff, TODOs, decisions, preferences, and long-term knowledge.

- Global entry: `Index.md`
- Memory rules: `Memory_Rules.md`
- Lilith project note: `Projects/Lilith/Lilith.md`
- Lilith troubleshooting mirror: `Knowledge/Troubleshooting/Lilith_Unity_Troubleshooting.md`
- Obsidian MCP workflow notes: `Knowledge/Tools/Obsidian_MCP.md`

Keep this file as a repo-local compatibility mirror for high-value Lilith troubleshooting patterns. When adding new durable memory, prefer Obsidian first, then update this file only if the knowledge must remain available without Obsidian.

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
