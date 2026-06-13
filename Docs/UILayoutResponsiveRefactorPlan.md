# UI Layout Responsive Refactor Plan

本文档是 Lilith UI 响应式布局改造的执行计划。它只保留一个主线方案：继续使用 uGUI、`UIManager` 和根 `CanvasScaler`，不迁移 UI Toolkit，不推倒重写 UI 系统；把当前全局 `ResponsiveLayoutGroupFitter` 补丁层收口为显式 opt-in 的局部 Grid 适配能力，并让普通 `HorizontalLayoutGroup` / `VerticalLayoutGroup` 回归 Unity 原生 `min / preferred / flexible` 语义。

本文档不留下开放式分叉。后续实施任务必须按 Phase 顺序推进；如果某一 Phase 失败，按该 Phase 的回退方式处理，不跳到下一 Phase 继续堆补丁。

## Execution Contract

- 本文档是执行顺序，不是候选方案列表；实施者不得重新选择 UI 技术栈或新增第二条响应式主线。
- 每个 Phase 只能修改该 Phase 的“允许改动”或“必须改动”范围；发现相邻问题时记录到后续 Phase，不在当前 Phase 顺手解决。
- 每个 Phase 必须满足自己的完成条件和验证方式后才能进入下一 Phase；失败时只使用该 Phase 的回退方式。
- 每个 prefab 家族必须独立验证、独立总结；不得用一个全局截图通过替代单家族验收。
- 如果当前实现和本文档冲突，以本文档为准，先把实现拉回本文档指定路线，再继续执行。
- 本文档中的“不允许改动”是硬边界；如果执行中发现边界外问题，只更新 inventory / TODO，不在当前 Phase 内修。

## Fixed Decisions

- 保留 `UIManager` 的 screen / modal / layer / Addressables 生命周期管理。
- 保留根 Canvas 的 `CanvasScaler.ScaleWithScreenSize` 作为分辨率适配基础；用户 UI scale 入口关闭，运行值固定为 1.0，`UIManager.ApplyUIScale()` 仅保留底层兼容入口。
- 不恢复相机 viewport letterbox；游戏相机、背景、Overlay 和 Toast 继续铺满真实屏幕。
- 宽于 16:9 的超宽屏只收窄主要 `Screen` / `Modal` 内容：需要收窄的 prefab 显式挂 `Vocalith.UI.UIContentSafeFrame`，最大宽高比固定为 16:9。
- 停止扩张根 Canvas 级全局线性 layout fitter；未来不得再向全局 fitter 添加新的 `HorizontalLayoutGroup` / `VerticalLayoutGroup` 特判。
- `GridLayoutGroup` 适配保留，但必须由 prefab 显式 opt-in；默认不由 `UIManager` 自动在根 Canvas 注入。
- `ScrollRect.content` 上的 grid 继续按非滚动轴 fit：纵向滚动只约束 viewport 宽度，横向滚动只约束 viewport 高度。
- Token Select 这类“固定比例卡片行”继续使用局部业务 fitter，不推广成通用线性 layout 规则。
- `ContentSizeFitter` 只用于内容决定自身大小的局部区域；不得再用全局 fitter 修补“同时想 preferred size 又想占满父容器”的结构。

## Layout Pattern Rules

| 场景 | 推荐结构 | 禁止事项 |
| --- | --- | --- |
| 全屏 screen / modal 根节点 | 由 `UIManager` normalize 或 prefab 自身 anchors 表达是否铺满 | 不在根节点写固定设计分辨率宽高来假装适配 |
| 超宽 screen / modal 主要内容 | full-bleed 背景留在 root；主要内容放入 `Content Safe Frame` 并挂 `UIContentSafeFrame`，或普通面板 root 直接挂 `UIContentSafeFrame` | 不收窄 root Canvas、`layerScreen`、`layerModal`、`layerOverlay`、`layerToast` 或相机 viewport |
| 普通横/纵向内容流 | `HorizontalLayoutGroup` / `VerticalLayoutGroup` + 子项 `LayoutElement.min/preferred/flexible` | 不依赖全局脚本改写子项宽高 |
| 背包格、法术槽、目录格 | 显式挂 `ResponsiveLayoutGroupFitter`，只处理子树内 `GridLayoutGroup` | 不让 `UIManager` 自动给所有 screen 套 grid/linear fit |
| 纵向或横向滚动 grid | `ScrollRect` + `Viewport` + `Content(GridLayoutGroup)`，由 grid fitter 按非滚动轴 fit | 不用 content 被 `ContentSizeFitter` 撑出的 rect 当可用区域 |
| 固定比例卡片行 | 局部业务 fitter 统一决定卡片宽度、高度、spacing、padding | 不用全局 HLG fitter 推导比例 |
| 长文本 / 对话 / 说明面板 | TMP 实际排版测量、分页、滚动或明确的 LayoutElement 高度 | 不靠缩小整个布局来掩盖文字溢出 |
| 弹窗 / tooltip | `ContentSizeFitter` 可按内容决定自身大小 | 不和“必须占满父容器”的布局目标混用 |

## Phase 0 - Freeze Current Patch Surface

目标：冻结当前补丁扩张，给后续改造建立安全边界。

允许改动：
- 给 `ResponsiveLayoutGroupFitter` 和相关测试加注释或待改标记，说明线性布局分支是待移除路径。
- 在 `Docs/UILayoutScreenshotAuditGuide.md` 中标注当前截图工具仍会模拟 root fitter，属于待收口行为。
- 新增或整理只读 inventory，列出当前显式挂 `ResponsiveLayoutGroupFitter` 的 prefab，以及依赖临时 root fitter 才能生效的 screen；Phase 0 清单固定为 `Docs/UILayoutResponsiveInventory.md`。

不允许改动：
- 不修改 prefab 结构。
- 不删除现有 fitter 行为。
- 不新增任何新的线性 layout fit 特判。

完成条件：
- 有一份清单列出：显式 fitter prefab、依赖 root auto-injection 的 prefab、需要局部业务 fitter 的 prefab。
- `dotnet build Lilith.Tests.EditMode.csproj --no-restore` 通过，或若已有无关失败，记录具体无关失败。
- Unity Console error/warning 为 0；不进入 PlayMode。

验证方式：
- 确认 `Docs/UILayoutResponsiveInventory.md` 存在，并覆盖所有当前显式 fitter、隐式 root fitter 依赖和局部业务 fitter。
- 运行 `dotnet build Lilith.Tests.EditMode.csproj --no-restore`。
- 通过 Unity Console 读取 error/warning 数；只允许非本轮引入且已记录的无关项。
- 运行 `git diff --check`；若仓库已有无关 whitespace 失败，记录失败文件并对本 Phase 目标文件单独运行 diff-check。

回退方式：
- Phase 0 只允许文档、注释和清单变更；若引入噪音，直接回退该 Phase 的文档/注释改动。

## Phase 1 - Convert Responsive Fit To Explicit Grid Opt-In

目标：把 responsive fit 从根 Canvas 自动扫描改为 prefab 显式 opt-in，并先只保留 Grid 路线。

必须改动：
- `UIManager.ApplyResponsiveLayouts()` 不再调用 `EnsureResponsiveLayoutFitter()` 自动创建 root fitter。
- `UIManager.ApplyResponsiveLayouts()` 只调用当前 rootCanvas 子树里已经存在的 `ResponsiveLayoutGroupFitter.FitNow()`。
- `ResponsiveLayoutGroupFitter` 保留类名以避免 prefab 序列化迁移成本，但 `FitLayoutGroup()` 只处理 `GridLayoutGroup`。
- `ResponsiveLayoutGroupFitter.LateUpdate()` 可保留，但只对显式挂载该组件的 prefab 子树生效。
- `UILayoutScreenshotBatcher` 不再创建临时 screen-root fitter；只调用目标 prefab 子树里已有的 fitter。
- 更新 `ResponsiveLayoutGroupFitterTests` 和 `UILayoutScreenshotBatcherTests`：线性布局不再被缩放；没有本地 fitter 的 screen 截图不应发生隐式 grid fit。

不允许改动：
- 不迁移 UI Toolkit。
- 不引入新的全局 layout service。
- 不改 Token Select 局部 fitter。
- 不改任意业务 prefab，除非测试 fixture 临时构造对象。

完成条件：
- `ResponsiveLayoutGroupFitter` 对普通 grid 和 `ScrollRect.content` grid 的现有 Grid 行为测试通过。
- HLG/VLG 测试明确断言不会被 `ResponsiveLayoutGroupFitter` 改写。
- 截图工具测试明确断言没有本地 fitter 时不会临时补 root fitter。
- `dotnet build Lilith.Vocalith.csproj --no-restore`、`dotnet build Lilith.Editor.csproj --no-restore`、`dotnet build Lilith.Tests.EditMode.csproj --no-restore` 均通过。
- Unity refresh 后 Console error/warning 为 0；不进入 PlayMode。

验证方式：
- 运行 `dotnet build Lilith.Vocalith.csproj --no-restore`。
- 运行 `dotnet build Lilith.Editor.csproj --no-restore`。
- 运行 `dotnet build Lilith.Tests.EditMode.csproj --no-restore`。
- 运行定向 EditMode：`ResponsiveLayoutGroupFitterTests` 和 `UILayoutScreenshotBatcherTests`。
- 读取 Unity Console error/warning，确认没有本轮新增项。
- 运行 `git diff --check`；若仓库已有无关 whitespace 失败，必须对 Phase 1 修改文件单独运行 diff-check。

回退方式：
- 回退 Phase 1 的代码提交即可恢复 root auto-injection 和截图临时 fitter；不得在失败状态下继续 Phase 2。

## Phase 2 - Migrate High-Risk UI Prefabs By Family

目标：把当前依赖全局补丁或容易失真的 UI 逐组迁移到明确布局范式。

执行顺序固定如下。前五项是本轮架构决策指定的主线迁移顺序；第六项是 Phase 0 inventory 已经识别出的 grid 依赖，作为同一阶段的固定补收口项执行，不另开架构分叉。

每个家族执行时都使用同一模板：
- 只修改该家族 prefab、该家族专属局部 fitter、该家族测试和 inventory 记录。
- 若需要跨家族共享逻辑，停止当前家族任务，先把共享逻辑作为 Phase 1 或 Phase 3 范围重新排入计划；不得在 Phase 2 偷偷新增全局 fitter 行为。
- 每个家族完成后立即运行对应测试和截图；不把多个家族攒在一起验收。

1. BackPack
   - 在 `BackPackUI.prefab` 根或最小稳定父节点显式挂 `ResponsiveLayoutGroupFitter`。
   - 只允许该 fitter 处理背包相关 `GridLayoutGroup`。
   - 保留 HLG/VLG flexible 分栏为 Unity 原生布局，不允许用线性 fitter 改 spacing/padding。
   - 验证 `Capture BackPackUI Only` 下中心背包 grid 横向填满 viewport，右侧 Book / Special grid 不裁切。

2. Token Select
   - 保留 `TokenSelectPanelLayoutFitter` 作为唯一卡片行适配器。
   - `Main Content` 不恢复 `ContentSizeFitter` 驱动主轴 preferred size。
   - 验证 `Capture Token Select Panel Only` 下 `ui1.0` 的 16:9、16:10、超宽和 5:4 场景中的卡片比例、面板高度、文字区可读性。

3. MainHUD
   - 保留 `MainUI.prefab` 上的显式 grid fitter，用于 spell grid。
   - 右侧通知、任务块和其他线性区域改 anchors / LayoutElement / TMP 设置，不新增线性 fitter。
   - 验证完整 screen batch 中 `1920x1080_ui1`、`2560x1080_ultra_ui1p5`、`1280x1024_5x4` 不裁切。

4. Narrative / Dialog
   - Narrative menu 的左右目录 grid 保留显式 grid fitter。
   - Dialog 和 Narrative content 的正文区域只用 TMP 测量、分页、滚动或固定 LayoutElement 高度解决。
   - 不使用全局缩放压缩正文文字或按钮。

5. Options / System Popup / Profile Popup
   - 用 anchors、LayoutElement、TMP auto-size/overflow、ScrollRect 解决按钮和文本承压。
   - 不给这些普通线性布局新增 `ResponsiveLayoutGroupFitter`。

6. Upgrade
   - 在 `Upgrage Section Prefab.prefab` 或其最小稳定父节点显式挂 `ResponsiveLayoutGroupFitter`，只处理 section grid。
   - `Upgrade UI Screen.prefab` 的 HLG/VLG/CSF 结构回归 Unity 原生 layout，不新增线性 fitter。
   - 验证完整 screen batch 中 Upgrade screen 不裁切，runtime child 调试入口不依赖 root auto-injection。

不允许改动：
- 不一次性重排全部 UI prefab。
- 不把 `ResponsiveLayoutGroupFitter` 挂到所有 screen 根节点。
- 不用对象名称查找作为长期运行时契约。

完成条件：
- 对每个家族分别跑对应单目标截图入口或完整 screen batch。
- 关键截图批次留在 `UILayoutScreenshotBatches/`，并在任务总结中写明批次路径。
- 相关 prefab 接线测试或截图工具测试补齐。
- Unity Console error/warning 为 0；不进入 PlayMode。

验证方式：
- BackPack：运行相关 EditMode 测试，执行 `Tools/Lilith/UI/Capture BackPackUI Only`，目检 `ui1.0` 的 16:9、16:10、超宽和 5:4 场景。
- Token Select：运行 `TokenSelectModalTests` 和截图工具相关测试，执行 `Tools/Lilith/UI/Capture Token Select Panel Only`，目检卡片比例和文字区。
- MainHUD：运行 `MainUIScreenTests` 和截图工具相关测试，执行 `Tools/Lilith/UI/Capture MainUI Only` 或完整 screen batch，目检 HUD 裁切。
- Narrative / Dialog：运行 `NarrativeReaderTests`、`DialogUIScreenTests` 和截图工具相关测试，执行 `Tools/Lilith/UI/Capture Narrative Dialog Only`，目检正文分页、按钮宽度和目录 grid。
- Options / System Popup / Profile Popup：运行对应 UI screen tests 和截图工具相关测试，执行完整 screen batch 或对应单目标入口，目检窄宽和超宽 safe frame。
- Upgrade：运行 `UpdateUIScreenTests` 和截图工具相关测试，执行 runtime child 或完整 screen batch，目检 upgrade grid 不依赖 root auto-injection。
- 每个家族改动后运行 `dotnet build Lilith.Editor.csproj --no-restore` 和 `dotnet build Lilith.Tests.EditMode.csproj --no-restore`；若改到 runtime 代码，再运行对应 runtime asmdef build。
- 每个家族改动后读取 Unity Console error/warning 并运行目标文件 `git diff --check`。

回退方式：
- 每个家族单独提交或单独 patch；若某个家族失败，只回退该家族改动，不回退 Phase 1。

## Phase 3 - Remove Obsolete Responsive Paths And Update Docs

目标：删除或降级旧补偿路径，保持文档和实现一致。

必须改动：
- 搜索 `UIScale` 的脚本和序列化引用。若无有效引用，删除 `Assets/Scripts/Vocalith/UI/UIScale.cs`；若有引用，同一 Phase 内先迁到 `UIManager.ApplyUIScale()` 再删除。
- 删除或私有化 `ResponsiveLayoutGroupFitter` 中已不再调用的线性 layout helper。
- 更新 `Docs/UILayoutScreenshotAuditGuide.md`：截图工具只调用本地显式 fitter，不再临时模拟 root fitter；fitter 只处理 Grid。
- 更新 README 中关于 `UIManager` “固定尺寸 LayoutGroup 自适应”的描述，改为“根 CanvasScaler 兼容入口、显式 UI layout 适配组件与超宽 safe frame”。
- 按实际最终状态更新 repo `memory.md` 中 UI 截图和 fitter 的长期排障记录。
- 删除 Options UI 中的用户 UI scale 可见入口；保留 `Options.Display.UIScale` 旧 key 兼容读取，但归一化结果固定为 1.0。

不允许改动：
- 不改变 `UIManager` screen / modal 行为。
- 不删除 `UIManager.ApplyUIScale()` 的底层兼容入口。
- 不删除 `ResponsiveLayoutGroupFitter` 类名，除非同一 Phase 内完成所有 prefab 序列化迁移和测试更新。

完成条件：
- `rg "UIScale|ResponsiveLayoutGroupFitter|ApplyResponsiveLayouts|EnsureResponsiveLayoutFitter"` 的结果与新职责一致。
- `dotnet build Lilith.Vocalith.csproj --no-restore`、`dotnet build Lilith.Editor.csproj --no-restore`、`dotnet build Lilith.Tests.EditMode.csproj --no-restore` 均通过。
- Unity Console error/warning 为 0。
- `git diff --check` 通过。
- 不进入 PlayMode。

验证方式：
- 运行 `rg "UIScale|ResponsiveLayoutGroupFitter|ApplyResponsiveLayouts|EnsureResponsiveLayoutFitter"` 并确认结果只剩新职责所需引用。
- 运行 `dotnet build Lilith.Vocalith.csproj --no-restore`。
- 运行 `dotnet build Lilith.Editor.csproj --no-restore`。
- 运行 `dotnet build Lilith.Tests.EditMode.csproj --no-restore`。
- 运行相关 EditMode 测试：`ResponsiveLayoutGroupFitterTests`、`UILayoutScreenshotBatcherTests` 和 Phase 2 涉及的 prefab 家族测试。
- 读取 Unity Console error/warning，确认没有本轮新增项。
- 运行 `git diff --check`，且 Phase 3 结束时不得留下由本轮新引入的 whitespace 失败。

回退方式：
- 若删除旧路径导致 prefab 缺脚本或编译失败，回退 Phase 3；不得重新启用 root auto-injection 作为修复。

## Acceptance Checklist

- 根 Canvas 不再自动创建全局 `ResponsiveLayoutGroupFitter`。
- 截图工具不再给没有本地 fitter 的 screen 临时补 root fitter。
- `ResponsiveLayoutGroupFitter` 只负责显式挂载子树中的 `GridLayoutGroup`。
- 普通 HLG/VLG 不被 responsive fitter 改写。
- BackPack / MainHUD / Narrative menu 的 grid 通过显式 fitter 适配。
- Upgrade section grid 通过显式 fitter 适配，不依赖 root auto-injection。
- Token Select 通过 `TokenSelectPanelLayoutFitter` 适配，不依赖全局线性 fitter。
- Dialog / Narrative content / Options / popup 的文本和按钮问题通过原生 layout、TMP、分页或滚动解决。
- README、`Docs/UILayoutScreenshotAuditGuide.md`、repo `memory.md` 与实现一致。

## Ultrawide Safe Frame Follow-Up

目标：在 Phase 1-3 收口全局 fitter 后，采用“世界全屏、UI 内容收窄”的固定方案处理超宽屏。

已选定方案：
- 新增 `Vocalith.UI.UIContentSafeFrame`，默认 `maxAspect = 16 / 9`。
- 只在父容器宽高比大于 16:9 时收窄；16:9、16:10、5:4 等非超宽比例继续使用父容器全尺寸。
- full-bleed screen 的背景、遮罩和装饰留在 root，下方主要交互内容移动到 `Content Safe Frame`。
- 普通弹窗 / 面板可直接在主 panel root 上挂 `UIContentSafeFrame`。
- Overlay、Toast、UI Guide 高亮和全屏 dim 不挂 safe frame。

迁移完成条件：
- 所有截图工具自动发现的父级 screen / panel prefab 都存在且仅存在必要的 `UIContentSafeFrame`。
- StartUp、MainUI、Loading、Storyteller 这类 full-bleed prefab 的背景 / full-screen 层不在 safe frame 内。
- `Capture Screen Prefabs UI Scale 1.0 Only` 的超宽场景显示主要 UI 居中收窄；背景或世界仍铺满真实显示区域。
- `UIContentSafeFrameTests` 和 `UILayoutScreenshotBatcherTests` 通过；受 safe frame 迁移影响的 UI screen 绑定测试通过。

## Validation Commands

按 Phase 需要选择最小验证集，禁止进入 PlayMode：

```powershell
dotnet build Lilith.Vocalith.csproj --no-restore
dotnet build Lilith.Editor.csproj --no-restore
dotnet build Lilith.Tests.EditMode.csproj --no-restore
git diff --check
```

需要视觉验收时，在 Unity Editor 可见且非 PlayMode 状态下使用：

```text
Tools/Lilith/UI/Capture BackPackUI Only
Tools/Lilith/UI/Capture Token Select Panel Only
Tools/Lilith/UI/Capture Screen Prefabs Only
```

所有 PlayMode 和打包视觉验收由用户手动执行。
