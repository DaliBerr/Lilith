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
- 依赖与编译配置：`Packages/manifest.json`、`Packages/packages-lock.json`、相关 `ProjectSettings/*.asset`
- 场景 / Prefab / ScriptableObject：仅在与当前任务直接相关时读取

## Ignore By Default

除非任务明确要求，否则不要读取或分析：

- Unity 缓存与生成目录：`Library/`、`Temp/`、`Obj/`、`Logs/`、`UserSettings/`
- 构建产物与导出目录：`Build/`、`Builds/`、`bin/`、`dist/`、`out/`
- IDE / VCS 噪音：`.git/`、`.svn/`、`.plastic/`、`.vs/`、`.idea/`、`.vscode/`
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
- 本仓库对应的 Unity MCP 端点是 `http://127.0.0.1:8082/mcp`；`G:\Unity Project\4\4` 对应的是 `http://127.0.0.1:8080/mcp`，`G:\Unity Project\Colo-3D` 对应的是 `http://127.0.0.1:8081/mcp`，不要把默认路由打到另一边。
- 不要单独信任默认 MCP namespace `unityMCP`。MCP for Unity 可能自动注入 plain `unityMCP` server/alias，且 Codex 工具列表可能延迟暴露 `unityMCP_0` / `unityMCP_1` / `unityMCP_2`。如果 `tool_search` 可用，使用 Unity 前先搜索 Unity MCP 工具，并检查所有暴露的 Unity MCP server namespace。
- 对本仓库，优先使用解析到 8082 的 server；当前预期是 `unityMCP_2`。`unityMCP_0` 通常对应 `G:\Unity Project\4\4` / 8080，`unityMCP_1` 通常对应 `G:\Unity Project\Colo-3D` / 8081，不要用于本仓库。plain `unityMCP` 只有在 `mcpforunity://instances` 和 `mcpforunity://editor/state` 明确显示 `Lilith`、8082 对应实例、且场景上下文属于本仓库时才可使用。
- 需要确认 Scene / Prefab / GameObject / Component / Console / Test / Screenshot 状态时，优先走 Unity MCP
- 不要先靠手读 `.unity` / `.prefab` 猜编辑器真实状态
- 本 Unity 项目当前不会在外部编辑脚本后自动导入/刷新。编辑 C# 脚本、asmdef、packages 或其他影响编译的资产后，在使用 Unity MCP 检查 Console、场景状态或运行测试前，必须手动触发一次 Unity refresh/import（等效 `Assets/Refresh`），并等待编译/domain reload 完成；然后再读取 `mcpforunity://editor/state`，确认 editor ready 且不在 compiling 后再信任 Console 或测试结果。
- 修改脚本后，优先用 Unity MCP 检查编译状态和 Console
- 若 `mcpforunity://instances` 显示多个 Unity 实例，必须先明确目标实例。`set_active_instance` 当前按 MCP server 全局状态生效，不假设它在两个 Codex 会话之间隔离；并行会话可能互相覆盖 active instance
- 多实例并行工作时，每次读取 MCP resource 前先 `set_active_instance` 到本仓库实例；支持 `unity_instance` 参数的 tool call 优先显式传入目标实例，避免误操作另一个 Unity 项目

## Repo Invariants

- 禁止在 `Assets/Scripts/Vocalith/**` 新增任何 `Kernel.*` 引用
- 若基础设施需要游戏语义，改为在 `Kernel` 增加 adapter / extension / bridge，或将抽象下沉到 `Vocalith`
- 所有文字类组件默认使用 TMP；非兼容性修复场景下，不新增 `UnityEngine.UI.Text`
- 运行时自动状态修改属于高敏感改动；若引入，必须在总结中明确披露对象、字段、触发时机和影响

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

## Document Responsibilities

- `README.md`：稳定结构、入口、依赖、已知限制
- `memory.md`：高复用、难复得的排障经验
- `AGENTS.md`：仓库硬规则
- `lilith-repo-operator` skill：操作流程、委派节奏、文档分工、symptom index

任务结束后必须评估 `README.md` 与 `memory.md` 是否需要更新；若无需更新，在总结中明确说明。

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
