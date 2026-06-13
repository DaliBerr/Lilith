# UI Layout Responsive Inventory

本文档是 `Docs/UILayoutResponsiveRefactorPlan.md` 的 Phase 0 清单。它只记录 Phase 0 快照和后续固定处理路径，不把布局策略重新打开讨论。

记录日期：2026-06-12。

状态说明：root auto-injection 和截图临时 root fitter 是 Phase 0 快照行为。Phase 1 已将它们移除；下方标为依赖这些路径的 prefab 现在表示 Phase 2 迁移风险，而不是当前仍会自动适配。`Backpack/BackPackUI.prefab` 已在 Phase 2 第一站迁移为本地显式 Grid fitter，`TokenSelect/Token Select Panel.prefab` 已在 Phase 2 Token Select 站完成局部业务 fitter 复核，`MainHUD/MainUI.prefab` 已在 Phase 2 MainHUD 站完成显式 Grid fitter 复核，`Narrative/Dialog UI.prefab` 已在 Phase 2 Narrative/Dialog 站补齐根 `DialogUIScreen` 运行时契约；截图工具现在会按 `[UIPrefab]` 地址在离屏实例上临时补 `NarrativeContentUIScreen`，并给正文和章节栏填审查样例，模拟 `UIManager` 运行时 `AddComponent<T>()` 路径，且不写回 prefab。`Narrative Content Panel.prefab` 已按本地 anchors / TMP auto-size 修标题压正文和翻页按钮占位文字换行，不新增 `ResponsiveLayoutGroupFitter`。`Upgrade/Upgrage Section Prefab.prefab` 已在 Phase 2 Upgrade 站迁移为本地显式 Grid fitter。System 弹窗已修 `Info Popup` 按钮行窄宽溢出与 `PauseUI` 设置面板 / 返回按钮重叠，`PauseUI` 的返回按钮已并入 `Settings ` 白色面板 footer，截图工具会给其嵌入 `OptionsUIScreen` 注入审查样例；`Profile Popup` 已修顶栏 / 关闭按钮锚点契约；`Setting Panel` 已补齐截图审查样例 hydration，并进一步按本地 anchors / TMP auto-size / sibling draw order 修右侧分类按钮可见性与选项行挤压。`Hint UI`、`StartUp UI Prefab` 和 `Boss Info UI` 已完成 Phase 2 线性布局收口：统一不新增通用 HLG/VLG fitter，改用本地 anchors、LayoutElement 和 TMP auto-size/ellipsis；截图工具给 Hint 注入稳定审查正文，并给 BossInfo 注入真实 boss 名称 / 阶段 / 血量样例。这些修复均不新增通用 fitter。Phase 3 代码面已完成清理：`Assets/Scripts/Vocalith/UI/UIScale.cs` 无有效序列化引用，已删除；`ResponsiveLayoutGroupFitter` 中未调用的线性布局 helper 已移除，保留类名和显式 Grid-only 职责。验证为 Vocalith / Editor / Tests.EditMode build 0 warning / 0 error，Unity EditMode 定向 62/62 passed，全量 723/723 passed，Console error/warning 0。

## Phase 0 Conclusion

- Phase 0 responsive patch surface 包含两条全局路径：`UIManager` 会在 root Canvas 自动补 `ResponsiveLayoutGroupFitter`，截图工具会在没有本地 fitter 的 screen 上临时补一层 root fitter。
- Phase 0 的 `ResponsiveLayoutGroupFitter` 同时处理 `GridLayoutGroup`、`HorizontalLayoutGroup` 和 `VerticalLayoutGroup`；线性布局分支已在 Phase 1 停用，不再扩展。
- 显式挂 `ResponsiveLayoutGroupFitter` 的 UI prefab 当前包括 `Backpack/BackPackUI.prefab`、`MainHUD/MainUI.prefab`、`Narrative/Narrative Menu Panel.prefab` 和 `Upgrade/Upgrage Section Prefab.prefab`。
- `Backpack/BackPackUI.prefab` 已在 Phase 2 第一站迁移：prefab 根节点显式挂 `ResponsiveLayoutGroupFitter`，只让内部 `GridLayoutGroup` 走本地 Grid opt-in 路线；HLG/VLG 仍回归 Unity 原生 layout。
- `MainHUD/MainUI.prefab` 已在 Phase 2 MainHUD 站复核：prefab 根节点保留唯一 `ResponsiveLayoutGroupFitter`，只用于 `Player Info Panel/Panel/Spell` grid；`Quest Panel` / `Notification Panel` 改为本地 anchors、TMP auto-size/ellipsis 和原生 layout，不走通用线性 fitter。截图工具新增 `Capture MainUI Only` / `-layoutScreenshotSet main-ui` 快速入口，并会注入任务与奖励通知审查样例。验证批次为 `G:\Unity Project\Lilith\UILayoutScreenshotBatches\LilithUILayoutScreenshotBatch_20260613_041630`。
- `Narrative/Dialog UI.prefab` 已在 Phase 2 Narrative/Dialog 站补齐根 `DialogUIScreen`，避免截图和运行时绕过 `UIScreen.__Init()`、直接展示 prefab 占位长文本；截图工具新增 `Capture Narrative Dialog Only` / `-layoutScreenshotSet narrative` 快速入口。
- `Narrative/Narrative Content Panel.prefab` 不新增 `ResponsiveLayoutGroupFitter`，也不强行序列化非同名脚本 screen 组件；截图工具按 `[UIPrefab]` 地址在离屏实例上临时补 `NarrativeContentUIScreen`，再注入 `Chapter Entry.prefab` 和双页审查样例。正文继续通过双页固定文本区、TMP auto-size 和运行时内容分页处理，翻页按钮改为本地箭头热区，章节栏移动到标题下方与翻页按钮上方的安全带；后续若仍有溢出，只允许改 TMP/分页/ScrollRect/固定 LayoutElement，不恢复线性 fitter。验证批次为 `G:\Unity Project\Lilith\UILayoutScreenshotBatches\LilithUILayoutScreenshotBatch_20260613_041946`。
- `Upgrade/Upgrage Section Prefab.prefab` 是带 grid 的 runtime child，已在 prefab 根节点显式挂 `ResponsiveLayoutGroupFitter`；Phase 1 后它不再依赖父 screen root fitter 的隐式适配。
- `TokenSelect/Token Select Panel.prefab` 已在 Phase 2 Token Select 站复核：它不使用通用 responsive fitter，`Main Content` 不恢复 `ContentSizeFitter`，只保留局部 `TokenSelectPanelLayoutFitter` 作为卡片行适配器；`BulletToken Selection Prefab.prefab` 根节点用 `AspectRatioFitter(WidthControlsHeight, 0.92)` 保持卡片比例，Catalog / Description 锚点留出竖向间距，Catalog TMP 开启 auto-size。验证批次为 `G:\Unity Project\Lilith\UILayoutScreenshotBatches\LilithUILayoutScreenshotBatch_20260612_234747`。
- `Assets/Prefabs/UI/System/Info Popup.prefab` 与 `Assets/Prefabs/UI/System/Pause/PauseUI.prefab` 已做 Phase 2 System 局部修复：`Info Popup` 缩小底部按钮 `LayoutElement.minWidth` 与 HLG spacing；`PauseUI` 把嵌入 `Setting Panel` 作为上方内容区，并把返回按钮移动到 `Settings ` 白色面板底部 footer，使它不再像独立悬浮按钮。截图工具会给 PauseUI 内嵌的 `OptionsUIScreen` 注入同一套设置审查样例。验证批次为 `G:\Unity Project\Lilith\UILayoutScreenshotBatches\LilithUILayoutScreenshotBatch_20260613_044208`；后续若继续微调 PauseUI，只能按局部 prefab 结构修，不恢复线性 fitter。
- `Assets/Prefabs/UI/Profile/Profile Popup.prefab` 已做 Phase 2 Options/Profile 局部修复：顶栏从 5% 提到 9%，正文区域下移到顶栏下方，标题改为左侧伸缩锚点，右侧关闭按钮收窄为固定顶栏槽位；prefab 不新增 `ResponsiveLayoutGroupFitter`。验证批次为 `G:\Unity Project\Lilith\UILayoutScreenshotBatches\LilithUILayoutScreenshotBatch_20260613_003932`；5:4 高 UI 缩放与超宽高 UI 缩放下顶栏不再挤压内容。
- `Assets/Prefabs/UI/Options/Setting Panel.prefab` 已做 Phase 2 Options 截图链路和本体布局修复：`UILayoutScreenshotBatcher` 会给 `OptionsUIScreen` 注入固定审查 catalog 和 `Option Entry Entry.prefab`，再调用 `RebuildView()`，让离屏截图能覆盖分类按钮、dropdown、slider、toggle 等真实运行时子节点；`Setting Panel` 扩大可见白框并收窄内容区，右侧分类按钮 panel 留在白框内且 sibling draw order 晚于 `Settings `，避免被白色背景盖住；`Option Entry Entry.prefab` 拆开 label/control 锚点并开启 label TMP auto-size，减少窄逻辑宽度下横向挤压。验证批次为 `G:\Unity Project\Lilith\UILayoutScreenshotBatches\LilithUILayoutScreenshotBatch_20260613_012728`；后续若继续优化 Options 视觉，只能按 anchors、LayoutElement、TMP auto-size/overflow、ScrollRect 内容区继续修，不恢复通用 HLG/VLG fitter。
- `Assets/Prefabs/UI/Hint/Hint UI.prefab`、`Hint Entry.prefab` 和 `Hint Catalog Entry.prefab` 已做 Phase 2 Hint 局部修复：主容器改为安全边距 stretch，顶部 catalog、左侧条目、右侧正文各自用固定 anchors 和本地 padding；entry/catalog entry 用 `LayoutElement` 约束高度/宽度并开启 TMP auto-size + ellipsis。截图工具给 `HintUIScreen` 注入 entry prefab、catalog prefab 和固定审查正文，但不触发分类切换或写回 prefab。验证批次为 `G:\Unity Project\Lilith\UILayoutScreenshotBatches\LilithUILayoutScreenshotBatch_20260613_034431`。
- `Assets/Prefabs/UI/StartUp/StartUp UI Prefab.prefab` 已做 Phase 2 StartUp 局部修复：移除 `Button Panel` 的 `ContentSizeFitter`，把按钮列限制在右侧固定 anchor 区域，按钮通过 `LayoutElement(min/preferred/flexible height)` 交给原生 VLG 分配高度，label 使用 TMP auto-size、NoWrap 和 Ellipsis；不新增 `ResponsiveLayoutGroupFitter`。验证批次为 `G:\Unity Project\Lilith\UILayoutScreenshotBatches\LilithUILayoutScreenshotBatch_20260613_034431`。
- `Assets/Prefabs/UI/MainHUD/Boss Info UI.prefab` 已做 Phase 2 BossInfo 局部修复：血条和名称区改为屏幕底部固定 anchor 区域，血条 HLG 不再 force-expand 宽度，boss 名称 TMP 开启 auto-size、NoWrap 和 Ellipsis；截图工具会给 `BossInfoUIScreen` 注入真实 boss 名称 / 阶段 / 血量审查样例，并通过反射调用 screen 私有处理方法，避免污染全局 EventBus。验证批次为 `G:\Unity Project\Lilith\UILayoutScreenshotBatches\LilithUILayoutScreenshotBatch_20260613_044208`；不恢复通用 HLG fitter。

## Scan Sources

本清单来自静态扫描，未进入 PlayMode，未修改 prefab。

```powershell
rg -n "ResponsiveLayoutGroupFitter|TokenSelectPanelLayoutFitter" Assets/Prefabs/UI -g "*.prefab"
rg -l "GridLayoutGroup" Assets/Prefabs/UI -g "*.prefab"
rg -l "HorizontalLayoutGroup" Assets/Prefabs/UI -g "*.prefab"
rg -l "VerticalLayoutGroup" Assets/Prefabs/UI -g "*.prefab"
rg -l "ContentSizeFitter" Assets/Prefabs/UI -g "*.prefab"
rg -n "UIScale|ApplyResponsiveLayoutsForCapture|EnsureResponsiveLayoutFitter|FitGridLayout|ResponsiveLayoutGroupFitter" Assets -g "*.cs"
```

## Current Patch Surface

| 路径 | 当前行为 | Phase 处理 |
| --- | --- | --- |
| `Assets/Scripts/Vocalith/UI/UIManager.cs` | `ApplyResponsiveLayouts()` 调用 `EnsureResponsiveLayoutFitter()`，会给 root Canvas 自动添加 `ResponsiveLayoutGroupFitter` | Phase 1 删除自动创建，只调用已经显式挂载的 fitter |
| `Assets/Editor/UILayoutScreenshotBatcher.cs` | `ApplyResponsiveLayoutsForCapture()` 在没有本地 fitter 的 screen 上临时添加 `ResponsiveLayoutGroupFitter` | Phase 1 删除临时 root fitter，只调用目标 prefab 子树已有 fitter |
| `Assets/Scripts/Vocalith/UI/ResponsiveLayoutGroupFitter.cs` | Phase 0 曾同时 fit Grid 与 HLG/VLG；Phase 3 已删除未调用线性 helper | 保留类名但只处理显式挂载子树中的 Grid；线性布局回归 Unity 原生规则 |
| `Assets/Scripts/Kernel/UI/TokenSelectPanelLayoutFitter.cs` | Token Select 专用卡片行 fitter，控制卡片宽高、spacing、padding 和内容高度 | Phase 2 Token Select 已复核；继续作为唯一卡片行适配器 |

## Explicit Fitters

| Prefab | Fitter | 当前职责 | 后续处理 |
| --- | --- | --- | --- |
| `Assets/Prefabs/UI/Backpack/BackPackUI.prefab` | `ResponsiveLayoutGroupFitter` | 背包中心 grid 与右侧 Book/Special grid 显式适配 | Phase 2 BackPack 已完成；不得扩大到线性 fit |
| `Assets/Prefabs/UI/MainHUD/MainUI.prefab` | `ResponsiveLayoutGroupFitter` | HUD spell grid 显式适配；prefab 同时含 HLG/VLG/CSF | Phase 2 MainHUD 已复核；任务 / 通知线性区域已改本地 anchors + TMP auto-size，继续用原生 layout 修 |
| `Assets/Prefabs/UI/Narrative/Narrative Menu Panel.prefab` | `ResponsiveLayoutGroupFitter` | Narrative menu 目录 grid 显式适配 | Phase 2 Narrative 保留 grid fitter；不得扩大到正文/按钮线性 fit |
| `Assets/Prefabs/UI/Upgrade/Upgrage Section Prefab.prefab` | `ResponsiveLayoutGroupFitter` | Upgrade section runtime child grid 显式适配 | Phase 2 Upgrade 已完成；不得扩大到 screen 线性 fit |
| `Assets/Prefabs/UI/TokenSelect/Token Select Panel.prefab` | `TokenSelectPanelLayoutFitter` | 固定比例卡片行局部适配 | Phase 2 Token Select 已完成；不改成通用线性 fitter |

## Grid Dependencies

| Prefab | Grid 状态 | 是否已有本地 `ResponsiveLayoutGroupFitter` | 当前依赖 | 固定处理路径 |
| --- | --- | --- | --- | --- |
| `Assets/Prefabs/UI/Backpack/BackPackUI.prefab` | 有 grid，且同 prefab 内有 HLG/VLG/CSF | 是，Phase 2 已迁移 | 本地显式 fitter | 已在根节点显式挂 `ResponsiveLayoutGroupFitter`，只保留 Grid 适配 |
| `Assets/Prefabs/UI/MainHUD/MainUI.prefab` | 有 grid | 是 | 本地显式 fitter | Phase 2 MainHUD 已复核；任务 / 通知截图样例已覆盖，不得扩大到线性 fit |
| `Assets/Prefabs/UI/Narrative/Narrative Menu Panel.prefab` | 有 grid | 是 | 本地显式 fitter | Phase 2 Narrative 保留 |
| `Assets/Prefabs/UI/Upgrade/Upgrage Section Prefab.prefab` | 有 grid runtime child | 是，Phase 2 已迁移 | 本地显式 fitter | 已在 runtime child prefab 根节点显式挂 `ResponsiveLayoutGroupFitter`；不改 `Upgrade UI Screen.prefab` 的 HLG/VLG/CSF |

## Linear And ContentSizeFitter Risk Map

这些 prefab 含 `HorizontalLayoutGroup`、`VerticalLayoutGroup` 或 `ContentSizeFitter`，但不应再通过全局 `ResponsiveLayoutGroupFitter` 获得线性布局适配。它们的固定策略是回归 Unity 原生 `min / preferred / flexible`、anchors、TMP overflow/auto-size、ScrollRect 或局部业务 fitter。

| Prefab | Layout 迹象 | 处理路径 |
| --- | --- | --- |
| `Assets/Prefabs/UI/Backpack/BackPackUI.prefab` | HLG + VLG + CSF + grid | Phase 2 BackPack 已迁移；grid 显式 fitter，线性分栏不再 fit |
| `Assets/Prefabs/UI/MainHUD/MainUI.prefab` | HLG + VLG + CSF + grid | Phase 2 MainHUD 已复核；grid 保留，任务 / 通知线性区域已改本地 anchors、LayoutElement 和 TMP auto-size，继续按原生 layout 局部修 |
| `Assets/Prefabs/UI/TokenSelect/Token Select Panel.prefab` | HLG + 局部业务 fitter | Phase 2 Token Select 已复核；继续用 `TokenSelectPanelLayoutFitter`，`Main Content` 不恢复 `ContentSizeFitter` |
| `Assets/Prefabs/UI/Narrative/Narrative Content Panel.prefab` | VLG | Phase 2 Narrative/Dialog 已修标题 / 正文 / 翻页按钮和截图样例章节栏；章节栏位于标题与翻页按钮之间，不新增 fitter，正文继续用 TMP/分页/滚动/固定高度 |
| `Assets/Prefabs/UI/Options/Options.prefab` | HLG + VLG + CSF | Phase 2 Options/System/Profile；原生 layout 修 |
| `Assets/Prefabs/UI/Options/Setting Panel.prefab` | HLG + VLG + CSF | Phase 2 Options 截图 hydration 和本体布局已补齐；后续只按本 prefab 原生 layout 局部微调，不加 fitter |
| `Assets/Prefabs/UI/Profile/Profile Popup.prefab` | VLG + CSF | Phase 2 Options/Profile 已修顶栏 / 关闭按钮锚点；继续按原生 layout，不加 fitter |
| `Assets/Prefabs/UI/System/Info Popup.prefab` | HLG | Phase 2 System 第一轮已修按钮行窄宽溢出；继续按原生 layout，不加 fitter |
| `Assets/Prefabs/UI/System/Pause/PauseUI.prefab` | HLG + 嵌入 Setting Panel | Phase 2 System 已修设置内容 / 返回按钮重叠，并把返回按钮并入 `Settings ` footer；继续按局部 prefab 结构修，不加 fitter |
| `Assets/Prefabs/UI/Hint/Hint UI.prefab` | HLG + VLG + CSF | Phase 2 Hint 已修主容器 / catalog / 左侧条目 / 正文 anchors 与 TMP auto-size；截图工具注入固定审查正文，不新增 fitter |
| `Assets/Prefabs/UI/StartUp/StartUp UI Prefab.prefab` | VLG + CSF | Phase 2 StartUp 已移除按钮列 `ContentSizeFitter`，改为本地 anchor + VLG + LayoutElement；不新增 fitter |
| `Assets/Prefabs/UI/Upgrade/Upgrade UI Screen.prefab` | HLG + VLG + CSF | Phase 2 Upgrade；screen 线性结构按 Unity 原生 layout 保持 |
| `Assets/Prefabs/UI/MainHUD/Boss Info UI.prefab` | HLG | Phase 2 BossInfo 已修底部 anchor、血条 HLG、boss 名称 TMP 与截图真实 boss 样例；不新增 fitter |

## Phase 1 Guardrails

- Phase 1 可以先完成代码行为翻转，但该中间状态不作为可发布状态；BackPack 与 Upgrade runtime grid 已在 Phase 2 补成本地显式 fitter，后续不得再恢复隐式 root fit。
- Phase 1 测试必须翻转截图工具预期：没有本地 `ResponsiveLayoutGroupFitter` 的 screen 不再被临时 fit。
- Phase 1 测试必须翻转线性布局预期：`ResponsiveLayoutGroupFitter` 不再修改 `HorizontalLayoutGroup` / `VerticalLayoutGroup`。
- Phase 1 不新增 prefab 迁移；所有 prefab 显式 fitter 接线放到 Phase 2。

## Completion Check For This Inventory

- 显式 fitter prefab 已列出。
- 依赖 root auto-injection 或截图临时 root fitter 的 grid prefab 已列出。
- 局部业务 fitter prefab 已列出。
- 高风险线性布局 prefab 已列出，并给出固定处理路径。
