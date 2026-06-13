# UI Layout Screenshot Audit Guide

本指南记录 Lilith UI prefab 在多分辨率和超宽安全框下的截图审查流程。它的目标不是替代 PlayMode 验收，而是在 EditMode 中快速生成可对比的离屏 UI 截图，让人直接看图判断遮挡、贴边、文字裁切、超宽拉伸和观感问题。

## 适用场景

- 检查 `Assets/Prefabs/UI` 下 prefab 在不同纵横比下是否遮挡、被超宽横向拉伸或不美观。
- 复核静态 RectTransform / TMP overflow 探针发现的问题是否真的能在画面里看到。
- 对 UI 修复前后生成同一套 contact sheet，做肉眼对比。
不适合的场景：

- 需要玩家输入、运行时数据加载、Addressables 状态机或动画驱动才能完整呈现的 UI。
- 需要验证“真实数据填充后的完整列表长度、滚动位置、选中状态、动画状态”的 UI。此流程优先看父 screen / panel prefab 在编辑器态 hydration 后的整体结果，最终组合仍要在父 UI / PlayMode 中确认。
- 需要最终产品分辨率真实全屏输出的验收。这个流程是 EditMode prefab 截图审查，最终仍要用户手动 Play / 打包确认。

## 使用工具

- Unity Editor：截图必须在非 batchmode Editor 中运行；2026-06-12 起输出由离屏 Camera / RenderTexture 生成，不再依赖 Unity 窗口或 GameView 前台焦点。
- Unity MCP `unityMCP_2`：确认目标实例、读取 Editor 状态、刷新脚本、查看 Console。
- 临时 `Screen Space - Camera` Canvas：绑定专用截图 Camera，不进入 PlayMode。
- 临时截图 Camera + `RenderTexture`：同步渲染 UI 并写出 PNG，避免 EditMode `ScreenCapture` 只抓到 3D GameView 的空拍问题。
- `VirtualRoot` RectTransform：在截图画布里模拟不同逻辑画布尺寸。
- `Handles.GetMainGameViewSize()`：读取当前 GameView 像素尺寸，用于确定输出图尺寸和把逻辑画布等比放进截图画布；读取失败时回退到 `1920x1080`。
- `PrefabUtility.InstantiatePrefab`：在临时 overlay 下实例化目标 UI prefab。
- `UIScreen.__Init()`：对实现了 `UIScreen` 的屏幕 prefab 触发一次编辑器态初始化，让 `OnInit()` 内创建的运行时子节点也能进入截图。若显式目标 prefab 根节点没有序列化 `UIScreen`，截图工具会先按 `[UIPrefab]` 地址在临时实例上补对应 screen 组件，用来模拟 `UIManager` 运行时的 `GetComponent<T>() ?? AddComponent<T>()` 路径。
- `ResponsiveLayoutGroupFitter.FitNow()`：截图链路只会调用 prefab 子树里已经显式挂载的 fitter，不再给没有本地 fitter 的 screen 临时补 root fitter。2026-06-12 Phase 1 后，该 fitter 只处理 `GridLayoutGroup`；普通 `HorizontalLayoutGroup` / `VerticalLayoutGroup` 回归 Unity 原生 `min / preferred / flexible` 规则，不再由 responsive fitter 改写 spacing、padding 或子项 `LayoutElement`。
- `UIContentSafeFrame`：运行时 UI scale 固定为 1.0；宽于 16:9 的截图场景应看到主要 Screen / Modal 内容居中收窄到最大 16:9，full-bleed 背景、世界、Overlay、Toast 和全屏 dim 不进入安全框。
- `ScrollRect.content` 上的 `GridLayoutGroup`：如果是纵向滚动内容，fit 只按 `ScrollRect.viewport` 宽度计算，允许 content 高度超过 viewport 后由滚动承接；如果是横向滚动内容，则只按 viewport 高度计算。普通非滚动 grid 仍按自身 rect 的宽高同时 fit。
- `Canvas.ForceUpdateCanvases()` + offscreen Camera render：等待 UI 布局刷新后再同步写 PNG。
- `TextMeshProUGUI`：在截图左上角写入 prefab / 场景标签。
- `Texture2D`：把单张截图拼成 per-prefab contact sheet 和总览图。
- PowerShell / `git status`：确认输出目录和工作区状态，避免截图或临时对象误入仓库。

## 快速运行

在 Unity Editor 打开 Lilith 后，使用菜单：

```text
Tools/Lilith/UI/Capture Layout Screenshot Batch
```

默认会输出完整 UI 审查集的 5 组模拟截图：

- 自动扫描 `Assets/Prefabs/UI` 下的父级 screen / panel prefab，并按路径排序纳入本次审查集。优先规则是“prefab 根节点挂了 `UIScreen`”；另外兼容当前仓库里少量 legacy 全拉伸 `UI/Panel/Popup/Screen` prefab。到 2026-06-11 当前仓库实跑结果为 18 个对象，覆盖 `Loading`、`Storyteller`、`StartUp`、`MainUI`、`BackPackUI`、`Hint`、`Pause`、`Options`、`Info Popup`、`Profile`、`Dialog`、`Narrative`、`Token Select`、`Settlement`、`Upgrade`、`Boss Info` 等入口。
- 对实现了 `UIScreen` 的目标，会额外调用一次编辑器态 `__Init()`，把 `OnInit()` 里运行时生成的背包格、TokenSelect 卡片、HUD spell slot 等子节点一起带进父 screen 的截图。对 `Capture Narrative Dialog Only` 这类显式目标，若 prefab 本身依赖 `UIManager` 运行时补 screen 组件，截图工具会只在离屏实例上临时补组件，不写回 prefab。
- 在 `__Init()` 之后，脚本会主动应用 responsive layout fit：如果 screen prefab 自身或子树里已经显式挂了 `ResponsiveLayoutGroupFitter`，会直接调用这些本地 fitter；如果没有本地 fitter，则不会发生隐式 grid fit 或线性 fit。
- 如果某个 screen prefab 暂时不希望进入常规审查，可以给该 prefab asset 打上 label：`ui-layout-ignore`。

默认输出目录：

```text
<ProjectRoot>\UILayoutScreenshotBatches\LilithUILayoutScreenshotBatch_yyyyMMdd_HHmmss
```

每次输出包括：

- `目标数 x 5` 张单图；按 2026-06-11 当前仓库的 18 个 screen prefab 计算，是 90 张。
- `目标数` 张 `*_CONTACT.png`；按 2026-06-11 当前仓库是 18 张。
- `00_MASTER_CONTACT.png`：所有 prefab 的总览图。
- `00_TARGETS.txt`：本次截图目标、自动发现规则、分组、布局模式和模拟场景清单。

如果只想缩小范围，可以使用分组菜单：

```text
Tools/Lilith/UI/Capture Screen Prefabs Only
Tools/Lilith/UI/Capture Screen Prefabs UI Scale 1.0 Only
Tools/Lilith/UI/Capture BackPackUI Only
Tools/Lilith/UI/Capture MainUI Only
Tools/Lilith/UI/Capture Narrative Dialog Only
Tools/Lilith/UI/Capture Token Select Panel Only
Tools/Lilith/UI/Capture Runtime Child Prefabs
```

`Capture Screen Prefabs UI Scale 1.0 Only` 仍使用自动发现的 screen / panel prefab 目标集，但只跑 4 个 `ui 1.0` 分辨率场景：`1920x1080`、`2880x1800`、`2560x1080` 和 `1280x1024`。它是当前推荐的超宽安全框审查入口：重点看 `2560x1080` 下主要 UI 是否居中收窄，背景 / 世界是否继续铺满。

`Capture BackPackUI Only` 只跑 `Assets/Prefabs/UI/Backpack/BackPackUI.prefab`，用于快速复看当前背包 grid / 缩放问题，不必每次重拍完整 screen 集。

`Capture MainUI Only` 只跑 `Assets/Prefabs/UI/MainHUD/MainUI.prefab`，用于复核 HUD spell grid、通知和任务块在多分辨率和超宽安全框下是否裁切。

`Capture Narrative Dialog Only` 只跑 `Assets/Prefabs/UI/Narrative/Dialog UI.prefab`、`Narrative Content Panel.prefab` 和 `Narrative Menu Panel.prefab`，用于复核目录 grid、正文页和对话框在多分辨率和超宽安全框下的裁切风险。该入口会给 Dialog 与 Narrative Content 的离屏实例填入审查样例文本，避免默认 prefab 占位文字或空白画面掩盖真实排版风险。

`Capture Token Select Panel Only` 只跑 `Assets/Prefabs/UI/TokenSelect/Token Select Panel.prefab`，用于快速复看 Token Select 水平卡片布局，不必每次重拍完整 screen 集。Phase 2 Token Select 复核批次为 `G:\Unity Project\Lilith\UILayoutScreenshotBatches\LilithUILayoutScreenshotBatch_20260612_234747`；该批次覆盖 12 个场景，已重点复看 `ui0.6 / ui1 / ui1.5`、超宽和 5:4，卡片比例、面板高度与文字区可读性符合当前局部 fitter 方案。

`Capture Runtime Child Prefabs` 仍保留为单独调试入口，但不属于常规 UI 审查范围；通常不需要单独拍这些依托父组件运行的小 prefab。

如需取消正在运行的批量截图：

```text
Tools/Lilith/UI/Cancel Layout Screenshot Batch
```

## 命令行入口

可以在已经能打开真实 Editor 的环境中使用 `-executeMethod`：

```powershell
& "C:\Program Files\Unity\Hub\Editor\6000.3.9f1\Editor\Unity.exe" `
  -projectPath "G:\Unity Project\Lilith" `
  -executeMethod UILayoutScreenshotBatcher.CaptureDefaultSetFromCommandLine `
  -layoutScreenshotOutput "C:\Temp\LilithUILayoutShots"
```

注意：

- 不要加 `-batchmode`。当前工具仍显式拒绝 batchmode，避免 Editor UI / prefab hydration / 字体渲染状态不可靠。
- 不建议加 `-quit`。如果确实希望工具完成后自动退出，使用本工具自己的参数：

```powershell
-layoutScreenshotQuit
```

可以临时指定 prefab 列表，用分号分隔：

```powershell
-layoutScreenshotPrefabs "Assets/Prefabs/UI/System/Info Popup.prefab;Assets/Prefabs/UI/MainHUD/MainUI.prefab"
```

也可以用内置分组：

```powershell
-layoutScreenshotSet screens
-layoutScreenshotSet backpack
-layoutScreenshotSet main-ui
-layoutScreenshotSet narrative
-layoutScreenshotSet token-select
-layoutScreenshotSet children
-layoutScreenshotSet all
```

`all` / `default` / `full` 都表示默认 screen 审查集；`screens` 与默认一致；`backpack` / `backpack-ui` / `backpackui` 只跑 `BackPackUI.prefab`；`main-ui` / `mainui` / `main-hud` / `mainhud` 只跑 `MainUI.prefab`；`narrative` / `dialog` / `narrative-dialog` / `narrative-ui` 只跑 Narrative/Dialog 三个父级 prefab；`token-select` / `tokenselect` / `token-select-panel` / `tokenselectpanel` 只跑 `Token Select Panel.prefab`；`children` 仅用于特殊调试，不是常规审查范围。默认 / `screens` 都会自动扫描 `Assets/Prefabs/UI` 下的父级 screen / panel prefab，优先匹配根节点挂 `UIScreen` 的对象，并兼容少量 legacy 全拉伸 `UI/Panel/Popup/Screen` prefab；带 `ui-layout-ignore` label 的对象会被忽略。

## 默认模拟场景

脚本使用当前根 CanvasScaler 的审查假设：`referenceResolution = 1920x1080`，`matchWidthOrHeight = 0.5`。运行时用户 UI scale 已关闭且固定为 1.0；默认推荐审查入口只使用 `ui 1.0` 场景。

| 标签 | 模拟目标 | 逻辑画布 |
| --- | --- | --- |
| `01_1920x1080_ui1` | 16:9，UI 1.0 基线 | `1920x1080` |
| `02_2880x1800_16x10` | 16:10，UI 1.0 基线 | `1822x1138` |
| `03_2560x1080_ultra` | 超宽，UI 1.0，主要内容应受 `UIContentSafeFrame` 收窄 | `2217x935` |
| `04_1280x1024_5x4` | 5:4，UI 1.0 基线，不强制 16:9 letterbox | `1610x1288` |
| `05_1920x1080_ui1p5` | 历史兼容场景，非当前推荐基线 | `1280x720` |

`VirtualRoot` 会按逻辑画布的比例等比缩进截图输出尺寸，因此 5:4 或超宽场景可能在 16:9 输出图中出现可见留边。这是审查框架用于模拟不同安全画布的表现，不等同于最终真实显示器输出。

如果你手动扩充 `DefaultScenarios`：

- 脚本仍会优先使用你直接填写的 `logicalSize`。
- 旧的非 `ui 1.0` 场景只作为历史兼容调试手段保留；当前布局验收以 `Capture Screen Prefabs UI Scale 1.0 Only` 为准。

## 推荐审查流程

1. 确认 Unity MCP 目标是 `unityMCP_2` / `Lilith@...`，Editor 不在 PlayMode、未编译、当前场景属于 Lilith。
2. 确认 GameView 尺寸符合本次审查期望，最好固定为 `Full HD (1920x1080)` 或项目常用截图尺寸；不需要手动让 GameView 抢前台焦点。
3. 运行 `Tools/Lilith/UI/Capture Layout Screenshot Batch`。
4. 打开输出目录里的 `00_MASTER_CONTACT.png` 做总览。
5. 先读 `00_TARGETS.txt` 确认本次覆盖范围，再对可疑项打开对应 `*_CONTACT.png` 或单张 PNG 细看。
6. 记录结论时分开写：
   - 静态探针确认的问题。
   - 离屏 UI 截图肉眼确认的问题。
   - 超宽场景中主要内容是否进入 16:9 safe frame，full-bleed 背景 / world / overlay 是否仍铺满。
   - 因缺少运行时数据 / 父布局而可能是假阳性的项。
7. 修复 UI 后用同一菜单重新跑一遍，比较前后 contact sheet。
8. 最后仍由用户手动 Play 或打包验证关键 UI。

## 已知限制

- 截图脚本会实例化 prefab，但不会走完整 `UIManager` / Addressables / 运行时数据初始化。
- `UIContentSafeFrame` 只限制挂载节点自身和其子内容；如果某个全屏背景、遮罩、Overlay 或 Toast 被放进 safe frame，超宽截图会表现为被错误收窄，应回到 prefab 结构修复。
- 对实现了 `UIScreen` 的屏幕 prefab，脚本会额外调用一次编辑器态 `__Init()`；因此像背包 48 格、TokenSelect 卡片、HUD 顶部运行时 spell 槽这类在 `OnInit()` 里生成的子节点，默认也会进入截图。若显式目标的 `UIScreen` 是运行时由 `UIManager` 补到实例上的，截图工具只在临时实例补组件，不会修改 prefab 资产。
- 截图脚本不会真的创建 `UIManager` 或完整运行 Addressables/流程状态机；responsive fit 只覆盖 prefab 子树里显式挂载 `ResponsiveLayoutGroupFitter` 的 `GridLayoutGroup`。普通 `HorizontalLayoutGroup` / `VerticalLayoutGroup` 不会被截图工具或 responsive fitter 改写。
- 如果某个 grid 同时是 `ScrollRect.content`，fit 不会再被 `ContentSizeFitter` 撑出的 content rect 误导；纵向滚动 grid 会按 viewport 宽度放大或缩小，内容高度可溢出并滚动。
- 对依赖运行时填充内容的 UI，截图只能验证 prefab 默认状态和布局承压能力。
- 脱离父组件就缺少真实语义的小子 prefab，不应作为常规审查结论来源；父子组合问题应以屏幕 prefab 截图、专门的 hydrated sample 或用户手动 Play 为准。
- 自动发现优先依赖“prefab 根节点挂 `UIScreen`”这一约定；当前仓库中另有一小批 legacy 全拉伸 panel 通过兼容规则被纳入。后续新增父级 UI 最稳妥的做法仍然是继续走 `UIScreen` 基类链，这样不会落入命名 / 布局启发式的灰区。
- 脚本会用临时 `Screen Space - Camera` Canvas、专用 Camera 和 RenderTexture 同步写 PNG；输出背景是审查用纯色背景，不再包含当前 3D 场景。
- 脚本会创建临时截图 Canvas / Camera，使用 `DontSaveInEditor` / `DontSaveInBuild`，完成后立即清理，不保存场景或 prefab。

## 排障

- 只看到 3D 地图，没有 UI：说明仍在跑旧的 `ScreenCapture.CaptureScreenshot` 版本或 Unity 还没刷新到最新 Editor 代码。先执行 Unity refresh / compile，确认 `UILayoutScreenshotBatcher` 使用 RenderTexture 输出，再重跑菜单。
- 截图尺寸不符合预期：先确认 GameView 当前分辨率。脚本模拟的是逻辑画布，不会自动修改 GameView 分辨率。
- 输出里缺图：查看 Unity Console 中 `[UILayoutScreenshotBatcher]` 日志，确认是否有 prefab 路径加载失败或截图超时。
- 运行后场景变脏：先检查是否有其他 Unity 自动生成变更；本脚本的临时对象使用 DontSave flags 并在每张截图后清理，不应保存到场景。
