# AGENTS.md — Lilith Repo Rules

## Mission

聚焦 `Lilith` 仓库中的可执行逻辑与可维护结构，优先处理 C# 运行时代码、必要的 Editor 工具、依赖配置，以及与逻辑绑定明确相关的场景和 prefab。

若本机存在 `lilith-repo-operator` skill，优先使用它承接详细操作流程、文档分工、Unity MCP 使用节奏与 troubleshooting 索引；本文件只保留每次任务都必须立刻看到的仓库硬规则。

## Behavioral Guidelines

这些准则用于减少常见 LLM 编码错误，并与本仓库规则合并执行。它们会更偏向谨慎而不是速度；非常简单的任务可以按实际风险裁量。

### Think Before Coding

不要假设，不要隐藏不确定性，主动暴露权衡。

- 实现前明确当前假设；如果关键信息不确定，先问。
- 如果需求存在多种解释，先说出解释差异，不要静默选择。
- 如果有更简单的方案或当前方向不值得做，直接说明并给出理由。
- 如果上下文不清楚，停下来指出困惑点，再请求必要信息。

### Simplicity First

用能解决问题的最小代码，不写推测性扩展。

- 不添加用户没有要求的功能。
- 不为单次使用的逻辑抽象新层。
- 不加入未被要求的灵活性、配置项或通用化设计。
- 不为实际上不可能出现的情况堆叠错误处理。
- 如果 200 行能被清晰地压到 50 行，应优先简化。

自检标准：资深工程师看到这段实现，会不会认为它过度复杂？如果会，先收敛。

### Surgical Changes

只触碰必须触碰的地方，只清理自己造成的问题。

- 不顺手改进相邻代码、注释或格式。
- 不重构未损坏、未被请求处理的代码。
- 匹配现有风格，即使你个人会用另一种写法。
- 发现无关 dead code 时可以提及，不要擅自删除。
- 清理由本次改动制造出的未使用 import、变量、函数或孤儿代码。
- 不删除任务开始前已经存在的 dead code，除非用户明确要求。

每一行 diff 都应该能直接追溯到用户请求。

### Goal-Driven Execution

把任务转换成可验证目标，并循环到验证完成。

- “加验证”意味着先明确无效输入覆盖点，再让测试或检查通过。
- “修 bug”意味着优先复现问题，再让复现用例通过。
- “重构 X”意味着确认重构前后的行为或测试保持一致。
- 多步骤任务应给出简短计划，并为每步标注验证方式。

示例：

```text
1. 定位入口 -> verify: 找到调用链和触发条件
2. 修改实现 -> verify: 相关测试或编译通过
3. 回归检查 -> verify: Console / diff / 影响面符合预期
```

这些准则生效时，diff 应更小，重写次数应更少，澄清问题应出现在实现前而不是出错后。

## Focus First

- 运行时代码：`Assets/**/Scripts/**/*.cs`
- 编辑器代码：`Assets/**/Editor/**/*.cs`
- Addressables 配置与加载代码：仅在任务涉及资源加载、打包、运行时引用解析时读取
- 依赖与编译配置：`Packages/manifest.json`、`Packages/packages-lock.json`、相关 `ProjectSettings/*.asset`
- 程序集定义：`Assets/**/*.asmdef`、`Assets/**/*.asmref`（当前仓库若不存在，则以目录和命名空间约定判断分层）
- 场景 / Prefab / ScriptableObject：仅在与当前任务直接相关时读取

## Ignore By Default

除非任务明确要求，否则不要读取或分析：

- Unity 缓存与生成目录：`Library/`、`Temp/`、`Obj/`、`Logs/`、`UserSettings/`
- 构建产物与导出目录：`Build/`、`Builds/`、`bin/`、`dist/`、`out/`
- 平台导出产物：`*.apk`、`*.aab`、`*.ipa`、`*.xcodeproj`、`*.xcworkspace`、`*.exe`、`*.app`
- IDE / VCS 噪音：`.git/`、`.svn/`、`.plastic/`、`.vs/`、`.idea/`、`.vscode/`
- IDE 生成文件：`*.sln`、`*.csproj`、`*.user`、`*.suo`
- 大体量素材：纹理、模型、音频、视频、动画、普通材质与 shader
- `.meta` 文件

只有在以下场景才允许主动读 `.meta`：

1. GUID 丢失、Missing Script、Missing Reference
2. merge 冲突导致引用损坏
3. 用户明确要求排查 GUID / 引用问题

`.plastic/**`、`*.csproj`、换行符和 Unity 自动改动默认视为噪音，不要把它们当成任务阻塞项，也不要主动回滚。

## Unity MCP First

若 Unity MCP 可用，把它视为 Unity Editor 真实状态的一等入口。

- 先读 `mcpforunity://editor/state`
- 需要确认 Scene / Prefab / GameObject / Component / Console / Test / Screenshot 状态时，优先走 Unity MCP
- 不要先靠手读 `.unity` / `.prefab` 猜编辑器真实状态
- 修改脚本后，优先用 Unity MCP 检查编译状态和 Console
- 只有在 Unity MCP 不可用、需要处理 YAML merge / GUID / Missing Script / Missing Reference，或需要文本级 diff 时，才优先直接读取 `.unity` / `.prefab` / `.meta`

## Unity Write Coordination

Unity Editor 是共享状态；当测试、PlayMode 或其他 agent 正在占用 Editor 时，不要抢占写入面。

- 修改 C# 源码、文档或普通队列请求文件通常可继续进行
- 在调用会改变 Editor 或项目状态的 MCP 操作前，先确认没有正在运行的测试或队列请求
- 高风险写入包括：`manage_gameobject`、`manage_components`、`manage_prefabs`、`manage_asset`、`manage_scene(save/create/load/move_to_scene/validate auto_repair)`、`manage_editor(play/stop/undo/redo)`、`refresh_unity(compile=request)`
- 若发现测试或队列正在运行，先做不争用 Unity 写入面的工作，例如 diff 自审、静态搜索、文档核对、影响面整理
- 不要为了自己的非测试写入而停止 PlayMode、清空 running 请求、移动队列文件，或要求其他 agent 中断测试
- 若仓库提供 `AgentTestQueue/`，按其 `README.md` 提交和等待 Unity Test Framework 请求；不要覆盖 `running/` 或 `results/` 中的文件

## Repo Invariants

- 禁止在 `Assets/Scripts/Vocalith/**` 新增任何 `Kernel.*` 引用
- 若基础设施需要游戏语义，改为在 `Kernel` 增加 adapter / extension / bridge，或将抽象下沉到 `Vocalith`
- 所有文字类组件默认使用 TMP；非兼容性修复场景下，不新增 `UnityEngine.UI.Text`
- 运行时自动状态修改属于高敏感改动；若引入，必须在总结中明确披露对象、字段、触发时机和影响

运行时自动状态修改包括但不限于：

- `Transform.position/rotation/localScale`
- `GameObject.SetActive`
- 组件 `enabled`
- 父子层级切换
- 刚体速度 / 约束
- 相机跟随参数
- UI 显隐 / 交互状态
- 状态机切换
- 自动 snap / teleport
- 运行时生成或销毁对象

这类改动默认先追求最小影响面：限制到明确对象、明确生命周期阶段和明确触发条件；避免在 `Update` 中持续重写状态，避免无条件遍历全场景对象并改写 Transform 或状态。

## Delegation Rules

- 只把只读、低风险、上下文压缩型子任务交给 mini 子代理
- 架构拍板、高风险运行时行为修改、最终代码收口由主模型负责
- Unity 测试收尾默认由主模型先启动测试，再把 `job_id` 交给 `test_runner`
- 严禁并行启动 `dsv4` / DeepSeek V4 Pro 子代理；必须串行执行。并行会污染状态并让后续调用失真或报错

## Write Safely On Windows

- 默认小步、顺序、可验证地写
- 单次 patch 最多 3 个文件
- 单次 patch 默认不超过约 600 行有效改动
- 大文件先骨架后分段补充
- 大型 Unity YAML 若 patch 不稳定，改用更小范围的精确替换
- 新文件默认逐个创建，不要一次性生成一批新文件
- 若一次 patch 失败，立刻降级为更小粒度：多文件改单文件、整段改分段、大段替换改精确替换
- 每完成一小批写入后，先验证文件是否已成功创建、内容是否完整、编码与换行是否正常，再继续下一批

## Windows Search Tools

- 搜索文本或文件时先缩小目录、扩展名、文件名模式，再递归搜索
- 本机已验证可用的 ripgrep 路径是 `C:\Users\15933\AppData\Local\Microsoft\WinGet\Links\rg.exe`
- 若使用 `rg`，优先显式调用上述路径；不要依赖可能解析到 WindowsApps 的裸 `rg`
- 若显式 `rg` 失败一次，立即回退到 PowerShell：文件名用 `Get-ChildItem -Recurse -File`，文本用 `Select-String`
- 不要把搜索失败当成业务结论；先换工具或缩小范围复核

## Document Responsibilities

- `README.md`：稳定结构、入口、依赖、已知限制
- `memory.md`：高复用、难复得的排障经验
- `AGENTS.md`：仓库硬规则
- `lilith-repo-operator` skill：操作流程、委派节奏、文档分工、symptom index

任务结束后必须评估 `README.md` 与 `memory.md` 是否需要更新；若无需更新，在总结中明确说明。

## README Workflow

- 任务前优先阅读 `README.md`，将其作为项目理解与路径追踪起点
- 若任务影响稳定架构、核心入口、模块边界、主要流程、关键依赖或已知限制，必须同步更新 `README.md`
- `README.md` 只描述当前状态，不写“从 AAA 改为 BBB”的历史变化描述
- 不要把 `README.md` 写成开发日志、排查笔记、任务流水账、系统百科或 agent 工作手册
- 对高变动、实现细节多、容易过期的内容，只保留简短概述与入口路径
- 更新 `README.md` 时优先做收敛和纠偏，而不是追加更多细节

## Memory Workflow

- `memory.md` 用于复用高成本、非直观、未来可能再次遇到的排障经验
- `memory.md` 不是 changelog，不记录普通改动、临时状态、一次性需求或无复用价值的小修小补
- 已有相同或相近问题时，优先更新原条目，不新增重复条目
- 推荐条目结构：`Problem` / `Cause` / `Fix` / `Verify` / `Scope`
- 若 `memory.md` 不存在，但本次任务确实形成了高价值可复用经验，可以创建它
- 若本次没有产生可复用经验，在总结中明确说明无需更新 `memory.md`

## Work Strategy

- 先明确目标，再读文件：报错、功能、重构、性能或架构问题要先分清
- 默认只读取与问题直接相关的少量脚本；不要无目的全仓库扫描
- 优先通过入口追踪依赖：`GameManager` / `Bootstrap` / `Entry` / `Main` / `App`，`Awake` / `Start` / `Update`，以及 `ScriptableObject` 配置加载点
- 上下文可能超限时，主动摘要项目结构、候选文件和已忽略目录
- 修改保持 API 变更最小化，给出明确修改点与验证方式
- 为必要方法添加简短 XML 注释；不要为显而易见的实现堆注释

## Quick Questions

信息不足时，优先向用户索要：

- Unity Console 堆栈或异常类型
- 目标行为与预期行为
- 涉及的场景或 Prefab 名称 / 路径
- 相关脚本路径
- Unity 版本、渲染管线、是否使用 Addressables / Netcode

## Default Assumptions

- 业务逻辑以 `Assets/**/Scripts` 的 C# 代码为准
- Unity MCP 返回结果优先于手读场景 / prefab 文本
- 若发现自己开始大量读取资源文件，应立即收缩读取范围
