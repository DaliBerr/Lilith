# AGENTS.md - Lilith Repo Rules

## Mandatory Bootstrap

新来的 agent 不能只读本文件就开始工作。进入本仓库后，必须先按顺序读取共享记忆入口，再回到本文件执行 Lilith 特有硬规则：

1. `C:\Tools\Codex\Codex-shared-memory\Index.md`
2. `C:\Tools\Codex\Codex-shared-memory\AGENTS.md`
3. `C:\Tools\Codex\Codex-shared-memory\Memory_Rules.md`
4. `C:\Tools\Codex\Codex-shared-memory\Projects\Lilith\Lilith.md`

如果共享 vault 不可访问，先明确说明这个限制，再仅按本文件和 `lilith-repo-operator` 继续；不要假装已经读取共享 AGENTS。

## Mission

聚焦 `Lilith` 仓库中的可执行逻辑与可维护结构，优先处理 C# 运行时代码、必要的 Editor 工具、依赖配置，以及与逻辑绑定明确相关的场景和 prefab。

若本机存在 `lilith-repo-operator` skill，优先使用它承接详细操作流程、文档分工、Unity MCP 使用节奏与 troubleshooting 索引；本文件只保留每次任务都必须立刻看到的仓库硬规则。

## Global Memory And Common Rules

通用 Agent 行为、Unity 仓库操作规则、Obsidian 记忆工作流、Windows 写入策略、常见 ignore 目录、文档收尾检查和多实例 Unity MCP 安全规则不再在本文件维护，必须读取共享 vault 中的正式版本：

- 全局入口：`C:\Tools\Codex\Codex-shared-memory\Index.md`
- 全局规则：`C:\Tools\Codex\Codex-shared-memory\AGENTS.md`
- 记忆规则：`C:\Tools\Codex\Codex-shared-memory\Memory_Rules.md`
- Unity 仓库通用规则：`C:\Tools\Codex\Codex-shared-memory\Knowledge\References\Unity_Repo_Common_Agent_Rules.md`
- Unity MCP 多项目路由：`C:\Tools\Codex\Codex-shared-memory\Knowledge\Tools\Unity_MCP_Multi_Project.md`
- 本项目页：`C:\Tools\Codex\Codex-shared-memory\Projects\Lilith\Lilith.md`
- Lilith 长期排障知识：`C:\Tools\Codex\Codex-shared-memory\Knowledge\Troubleshooting\Lilith_Unity_Troubleshooting.md`
- Obsidian MCP 使用经验：`C:\Tools\Codex\Codex-shared-memory\Knowledge\Tools\Obsidian_MCP.md`

执行本仓库任务时，共享 AGENTS 提供默认行为和跨仓库流程，本文件与 `lilith-repo-operator` 只覆盖 Lilith 特有硬规则。若共享 vault 内容与本文件或 repo-local skill 冲突，以本文件和 repo-local skill 为准。

若 Obsidian MCP 工具超时或不可用，不要卡在 MCP 入口；直接通过文件系统读取/写入 Markdown vault：`C:\Tools\Codex\Codex-shared-memory`。普通记忆、handoff、TODO、project note 更新优先使用文件系统串行写入并读回验证；只有需要 active file、periodic note、Obsidian UI 打开文件、tags/frontmatter/backlinks metadata 或 Obsidian 内置搜索时，才优先尝试 Obsidian MCP。

## Focus First

- 运行时代码：`Assets/**/Scripts/**/*.cs`
- 编辑器代码：`Assets/**/Editor/**/*.cs`
- 依赖与编译配置：`Packages/manifest.json`、`Packages/packages-lock.json`、相关 `ProjectSettings/*.asset`
- 场景 / Prefab / ScriptableObject：仅在与当前任务直接相关时读取

## Unity MCP Target

- 本仓库 Unity MCP 端点：`http://127.0.0.1:8082/mcp`
- 当前预期 namespace：`unityMCP_2`
- `unityMCP_0` 通常是 `G:\Unity Project\4\4` / 8080，不要用于本仓库。
- `unityMCP_1` 通常是 `G:\Unity Project\Colo-3D` / 8081，不要用于本仓库。
- plain `unityMCP` 只有在 `mcpforunity://instances` 和 `mcpforunity://editor/state` 明确显示 `Lilith`、8082、且 scene context 属于本仓库时才可使用。

需要确认 Scene / Prefab / GameObject / Component / Console / Test / Screenshot 状态时，优先走 Unity MCP，并先确认目标实例没有路由到其他 Unity 项目。

## Repo Invariants

- 禁止在 `Assets/Scripts/Vocalith/**` 新增任何 `Kernel.*` 引用。
- 若基础设施需要游戏语义，改为在 `Kernel` 增加 adapter / extension / bridge，或将抽象下沉到 `Vocalith`。
- 所有文字类组件默认使用 TMP；非兼容性修复场景下，不新增 `UnityEngine.UI.Text`。
- Runtime automatic state changes 属于高敏感改动；若引入，最终总结必须说明对象、字段、触发时机和影响。

## Documentation

- `README.md`：稳定结构、入口、依赖、已知限制。
- `memory.md`：高复用、难复得的排障经验；Obsidian 是跨会话主记忆层，本文件保留兼容镜像。
- `AGENTS.md`：本仓库必须立即可见的硬规则。
- `.codex/hooks.json`：本仓库 Codex hook 配置；当前 hooks 会在每次提交用户消息时注入“允许使用 `gpt-5.4-mini` / `gpt-5.3-codex-spark` 分担边界清晰子任务”的委派偏好，在计划模式下注入“有不确定决策就直接询问用户”的提醒，并要求最终总结明确 Memory Consistency Pass 与 `README.md`、`memory.md`、`AGENTS.md` 更新评估。
- `lilith-repo-operator` skill：操作流程、委派节奏、文档分工、symptom index。
- Obsidian project note / handoff / TODO：跨会话状态与交接。

任务结束后必须执行共享记忆 `Memory_Rules.md` 中的 Memory Consistency Pass：读取并评估相关 Session、Project、Project TODO、latest handoff、Dashboard、Decision、Knowledge、Preferences 与 repo-local 文档，修正过期、矛盾或遗漏内容并读回验证。与此同时，必须额外评估 `README.md`、`memory.md`、本文件与 repo-local skill 是否需要更新；若无需更新，在总结中明确说明。
