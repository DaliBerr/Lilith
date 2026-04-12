# AGENTS.md — Unity Repo Guidance for Coding Agents

## Mission / 目标
你是这个 Unity 项目的代码协作代理。你的首要任务是：
1) 聚焦 **可执行逻辑与可维护结构**（C# 脚本、程序集划分、运行时逻辑、Editor 工具、配置与依赖）。

## What to focus on / 优先关注（高价值）
### 核心运行时代码
- `Assets/**/Scripts/**/*.cs`
- `Assets/**/Addressables/**`（若项目使用 Addressables，关注其配置与加载代码）

### 编辑器与工具链（仅当需求相关）
- `Assets/**/Editor/**/*.cs`

### 依赖与构建配置（用于定位包/版本/编译问题）
- `Packages/manifest.json`
- `Packages/packages-lock.json`
- `ProjectSettings/*.asset`（仅在与输入/渲染/打包/脚本执行顺序等相关时阅读）
- `Assets/**/asmdef`（程序集定义，影响编译引用与分层）
- `Assets/**/asmref`

### 场景与Prefab（仅当与逻辑绑定或引用缺失相关时）
- `Assets/**/*.unity`
- `Assets/**/*.prefab`
- `Assets/**/*.asset`（ScriptableObject 配置类等）
- 若 `Unity MCP` 可用，优先通过 `Unity MCP` 查询 Scene / Prefab / GameObject / Component 状态，而不是直接打开这些 YAML 文本

---

## What to ignore / 尽量忽略（低价值、极易干扰）
除非我明确要求，否则不要读取/分析以下内容：

### Unity 自动生成与缓存
- `Library/`
- `Temp/`
- `Obj/`
- `Logs/`
- `UserSettings/`

### 构建产物与导出
- `Build/`, `Builds/`, `bin/`, `dist/`, `out/`
- `*.apk`, `*.aab`, `*.ipa`, `*.xcodeproj`, `*.xcworkspace`
- `*.exe`, `*.app`, `*.dll`（第三方库除外且仅在必要时看）

### 版本控制与IDE杂项（除非是 CI/脚本需求）
- `.git/`, `.svn/`
- `.plastic/`（Plastic/Unity 自动维护文件夹，默认不管理其变更）
- `.vs/`, `.idea/`, `.vscode/`
- `*.sln`, `*.csproj`（除非编译/引用问题必须）
- `*.user`, `*.suo`
- 规则补充：若 `.plastic/**` 与 `*.csproj` 出现 Unity 自动改动，默认视为噪音，不作为异常中断任务，也不主动回滚，除非我明确要求处理

### 素材与大文件（通常与“核心代码逻辑”无关）
- `Assets/**/Textures/`, `Assets/**/Models/`, `Assets/**/Audio/`, `Assets/**/Animations/`
- `*.png`, `*.jpg`, `*.tga`, `*.psd`, `*.fbx`, `*.wav`, `*.mp3`, `*.mp4`
- `*.shader`, `*.mat`（除非我在问渲染/材质/Shader 相关问题）

### Unity 的 .meta 文件（重点）
- `Assets/**/*.meta`
原则：**默认完全忽略** `.meta`。
只有在以下情况才允许查看 `.meta`：
1) 资源 GUID 丢失/引用断裂（Missing Script / Missing Reference）
2) merge 冲突导致 GUID 改变，Prefab/Scene 引用被破坏
3) 我明确要求你定位 GUID/引用问题

---

## Unity MCP / Unity MCP 使用约定
若当前 agent 可以访问 `Unity MCP`，默认把它视为 Unity Editor 的一等入口，而不是可选附加能力。

- 先读 `mcpforunity://editor/state`，确认 `ready_for_tools`、编译状态、活动场景
- 涉及 Scene / Prefab / GameObject / Component / Console / Test / Screenshot / Build / Package / Editor 状态时，优先调用 `Unity MCP`
- 优先使用编辑器真实状态回答“对象是否存在、组件是否挂载、场景是否激活、控制台是否报错、测试是否通过”这类问题，不要先靠手读 `.unity` / `.prefab` 文本猜
- 修改 C# 脚本仍以源码文件为主；但修改后应优先通过 `Unity MCP` 检查编译状态、Console 和场景接线结果
- 只有在以下情况才优先直接读 `.unity` / `.prefab` / `.meta` 文本：
  1. `Unity MCP` 当前不可用
  2. 需要处理 YAML merge / GUID / Missing Script / Missing Reference
  3. 需要做文本级 diff 或审查序列化变更

默认建议优先调用的 `Unity MCP` 能力：

- 查编辑器是否就绪：`mcpforunity://editor/state`
- 查当前场景与根对象：`manage_scene(action="get_active")`、`find_gameobjects`
- 查/改对象与组件：`manage_gameobject`、`manage_components`
- 查/改 Prefab：`manage_prefabs`
- 查 Console / 编译问题：`read_console`
- 运行测试：`run_tests`、`get_test_job`
- 做场景截图或 UI 自检：`manage_camera`



## Subagent workflow / Subagent 使用约定
若当前运行环境支持 subagent，默认允许主模型把**只读、低风险、可并行、上下文压缩型**子任务委派给更轻量的子代理，当前存在：log_triager/test_triager/impact_auditor/doc_auditor/prefab_locator/diff_reviewer/test_runner各subagnet。

### 何时优先使用 subagent
以下任务默认适合优先交给 mini 子代理：
- **代码地图与入口定位**：快速找入口类、状态流、调用链、事件源、注册点、配置加载点
- **只读资料核对**：阅读 README、memory、设计说明、第三方文档、包版本与依赖差异
- **日志/报错归类**：整理 Console、测试失败、堆栈、构建输出，先做去重与归因候选
- **测试结果初筛**：汇总失败用例、按模块分组、定位最可能相关文件
- **Prefab / 场景线索收集**：只做对象名、组件名、挂载关系、引用线索的只读整理
- **大范围搜索压缩**：先扫 10~30 个候选文件，再把真正值得主模型深读的 1~5 个文件筛出来
- **改动后影响面清单**：列出可能受影响的脚本、场景、Prefab、asmdef、测试点
- **文档差异核对**：检查 README / memory / AGENTS 是否需要更新，并给出候选修改点

## Subagent for Unity test tail / Unity 测试收尾子代理约定
- 当任务进入“修改后验证”阶段，且需要等待 Unity MCP 测试结果、轮询测试任务、整理失败摘要时，优先委托 `test_runner` 子代理执行
- `test_runner` 负责：
  - 读取 `mcpforunity://editor/state`
  - 调用 `run_tests` / `get_test_job`
  - 必要时调用 `read_console`
  - 汇总测试通过/失败情况与关键错误线索
- 主模型负责：
  - 判断应该跑哪些测试
  - 判断失败是否与本次改动直接相关
  - 决定是否继续修复、扩大验证范围或结束任务
- 默认不要让 `test_runner` 在未获明确授权时执行全量长耗时测试；优先跑与改动影响面最相关的测试
- 若测试任务耗时较长，主模型可在等待测试期间并行委托其他只读子代理执行 diff 审查、README/memory 评估或影响面复查

### 不应交给 mini 子代理的任务
以下任务默认仍由主模型自己负责，不要下放给 mini 作为最终决策者：
- 跨多个系统的架构改造与最终方案拍板
- 高风险运行时行为修改（自动改位置、显隐、状态机切换、对象生成/销毁等）
- 真实代码修改的最终版本编写与收口
- 需要强一致性判断的分层边界决策
- 需要综合多个子任务结果做最终取舍、权衡和验收的步骤

### subagent 的默认使用原则
- 主模型负责：任务拆分、优先级、最终判断、最终修改、最终总结
- mini 子代理负责：只读调查、并行检索、压缩上下文、产出候选事实
- 子代理默认使用**只读 sandbox**；除非我明确要求，否则不要给子代理写权限
- 子代理返回内容应尽量结构化，优先返回：`结论 / 证据 / 候选文件 / 风险 / 建议下一步`
- 若任务已能在主模型当前上下文中低成本完成，不要为了“形式上并行”而强行创建子代理
- 若同一事实已经通过 `Unity MCP` 直接确认，不要再让子代理重复猜测或重复搜索

### 与 Unity MCP 的配合方式
- 涉及 Unity Editor 真实状态时，优先由主模型先调用 `Unity MCP` 建立事实
- 子代理更适合处理 `Unity MCP` 返回结果的整理、归类、摘要与候选定位
- 若某个子任务本质上是在问“场景里现在到底是什么状态”，优先 `Unity MCP`，不要优先 subagent
- 若需要并行做“脚本侧搜索 + 文档侧核对”，可把脚本搜索交给 mapper 类子代理，把文档核对交给 researcher / doc-auditor 类子代理

### 推荐调用模式
- **先建事实，再并行调查，再主模型收口**
- 典型顺序：
  1. 主模型先读取 `README.md` / `memory.md`，并在需要时调用 `Unity MCP`
  2. 主模型判断哪些子任务适合并行、且适合 mini
  3. 主模型把只读调查任务委派给 1~3 个 mini 子代理
  4. 子代理返回候选事实与文件列表
  5. 主模型只深读真正相关的少量文件，并完成最终修改/判断

### 推荐提示词模式（给主模型）
当任务适合拆分时，主模型应显式使用类似指令调用子代理：
- “使用 `code_mapper` 子代理快速扫描相关入口、调用链和候选文件，只读，不修改任何文件。”
- “使用 `log_triager` 子代理整理 Console / 测试失败并按根因分组，只给我候选原因和相关脚本。”
- “使用 `impact_auditor` 子代理评估这次修改可能影响哪些 Prefab、场景、测试和 README 条目。”
- “使用 `doc_auditor` 子代理检查本次任务是否需要更新 README / memory / AGENTS，只返回建议，不直接改文档。”

### 结果使用规则
- 子代理输出默认视为**候选线索**，不是最终事实
- 涉及代码修改、运行时行为、分层边界、MCP 事实确认时，必须由主模型亲自复核
- 若多个子代理结论冲突，主模型必须显式说明冲突点，并基于源码 / MCP / 测试结果做最终裁决

## How to work / 工作方式与策略（重要）
1) **先问目标，再读文件**  
   在动手修改前，先明确要解决的问题是什么（报错、功能、重构、性能、架构）。
2) **最小化读取范围**  
   默认只读取与问题相关的 1~5 个脚本文件；不要“全仓库扫描”。
3) **优先通过入口追踪依赖**  
   通常从以下入口开始定位：
   - `Assets/**/Scripts` 中的 `GameManager`/`Bootstrap`/`Entry`/`Main`/`App` 类
   - `MonoBehaviour` 的 `Awake/Start/Update` 链路
   - `ScriptableObject` 配置加载点
4) **若 `Unity MCP` 可用，相关操作要主动调用它**  
   - 先用 `mcpforunity://editor/state` 判断编辑器是否可操作
   - 先用 `find_gameobjects` / `manage_scene` / `manage_prefabs` / `read_console` 建立事实，再决定读哪些文件
   - 涉及场景挂载、Prefab 层级、组件启停、Console 报错、测试结果时，不要跳过 `Unity MCP`
5) **修改要可编译、可回滚**  
   - 保持 API 变更最小化
   - 给出明确的修改点与原因
   - 若涉及多人协作，优先不改变资源 GUID
6) **当上下文可能超限时，主动摘要**  
   - 用项目结构摘要（模块、关键类、依赖）替代全文粘贴
   - 明确你已忽略哪些目录以节省上下文
7) **为必要的方法/函数添加注释**
   - 注释保持简短
   - 注释至少包含"<summary>,<param>,<returns>"
8) **README 优先与维护**  
   - 执行任务前，先阅读根目录 `README.md`以及，优先理解现有架构说明与约定
   - 任务完成后，评估本次改动是否影响架构、流程、入口、依赖或关键脚本说明
   - 若有影响，需同步更新 `README.md`，确保文档与当前实现一致
   - `README.md` 的职责是作为仓库入口文档，帮助快速理解当前项目的稳定结构、核心模块、关键入口、主要场景、重要依赖与已知限制
   - 不要把 `README.md` 写成开发日志、变更记录、排查笔记、任务流水账、系统百科、逐功能详细规格书，或 agent 工作手册
   - 对高变动、实现细节很多、容易过期的内容，只保留简短概述与入口路径；不要在 `README.md` 中展开过细的运行时流程、按钮细节、状态跳转细节、逐步交互规则或临时排查结论
   - agent 工作方式、工具调用规则、阅读顺序、排查策略、MCP 使用约定，应优先写在 `AGENTS.md`；特殊坑点、非直观结论和高成本排查经验，应优先写在 `memory.md`
   - 更新 `README.md` 时，优先做“收敛和纠偏”，而不是“追加更多细节”；若某段内容已经超过 README 入口文档应有的粒度，应删除、压缩，或迁移到其他文档
   - 若本次任务不会改变仓库的稳定结构、关键入口、模块边界、公共约定或已知限制，则不要为了“显得完整”而扩写 `README.md`
9) **UI 文本组件规范**
   - 所有文字类条目默认使用 `TMPro`（如 `TMP_Text` / `TextMeshProUGUI`）
   - 非兼容性修复场景下，不再新增 `UnityEngine.UI.Text`
10) **Memory 优先复用特殊经验**
   - `README.md` 用于理解项目当前结构与约定，`memory.md` 用于复用过去高成本获得的特殊经验；两者职责不同，禁止重复维护相同内容
   - 执行任务前，先读 `README.md`，再检查并按需阅读 `memory.md`
   - 若 `memory.md` 中已有与当前问题高度相关的经验，优先复用其结论，再决定是否需要继续搜索、排查或试错
   - 只有当某条结论未来复用价值高、容易遗忘、排查成本高、或能显著减少重复试错时，才允许写入 `memory.md`
   - `memory.md` 不是 changelog，不记录普通改动或一次性任务结果
   - 任务结束后必须评估是否需要更新 `memory.md`；若无需更新，必须在总结中明确说明
11) **分层边界是硬规则**
   - 禁止在 `Assets/Scripts/Vocalith/**` 新增任何 `Kernel.*` 引用
   - 若基础设施需要游戏语义，必须改为在 `Kernel` 增加 adapter / extension / bridge，或将抽象下沉到 `Vocalith`
   - 当前其他 `Vocalith` 子模块若仍存在历史反向依赖，属于技术债，不代表规则例外
12) **运行时自动状态修改必须谨慎且显式披露**
   - 只要改动会让对象在运行时被代码自动调整，就视为高敏感改动；典型例子包括但不限于：`Transform.position/rotation/localScale`、`SetActive`、组件 `enabled`、父子层级切换、刚体速度/约束、相机跟随参数、UI 显隐/交互状态、状态机切换、自动 snap/teleport、运行时生成或销毁对象
   - 这类改动默认先追求**最小影响面**：优先限制到明确的对象、明确的生命周期阶段（如 `Awake/Start/OnEnable`、进入某个 UI、切场景后）和明确的触发条件，不要把“自动纠正”写成全局、持续、无条件执行
   - 若存在非直观副作用，必须优先选择更可预测的实现：例如增加显式开关、缩小搜索范围、避免在 `Update` 中重复重写状态、避免无条件遍历全场景对象并改写其 Transform/状态
   - 若任务确实需要引入这类自动调整，完成任务后的总结里必须单独明确说明“哪些对象/组件会在运行时被自动调整、调整哪些字段、何时触发、触发后表现是什么、是否会影响已有场景摆放或 prefab authoring 预期”
   - 若本次改动不会引入新的运行时自动调整，也应在总结中直接说明“本次未新增运行时自动状态调整”
13) **按通知阈值发送完成通知**
   - 默认不要在每个任务结束后都发送 `ntfy_me`
   - 只有在任务满足“通知阈值”时，才发送当前任务的完成情况
   - 通知阈值可按以下任一条件判断：
     1. 任务持续时间明显较长，适合异步提醒我回来查看结果
     2. 涉及多文件修改、关键逻辑改动、较高回归风险或需要我重点验收
     3. 运行了耗时命令、构建、测试、批处理或较长链路排查
     4. 任务失败、被阻塞，或需要我提供额外信息才能继续
     5. 我明确要求“完成后通知我”或表达了等待异步提醒的意图
   - 若任务只是简单问答、少量文本修改、轻量单文件改动或即时就能看完的结果，则不要发送通知
   - 标题放置“任务名称 + 成功/失败”，并且标题必须使用英文
   - 正文放置本次实现的简短概要
---

## README workflow 
- **Before task /**：优先查看 `README.md`，将其作为项目理解与路径追踪的起点。  
- **After task /**：必须进行一次“README 是否需要更新”的检查。  
- **Update when needed /**：当涉及核心逻辑、模块边界、启动流程、数据流、关键脚本路径变化时，更新 `README.md` 对应条目。  
- **Write current state only /**：若与旧版本存在差异，不保留“从 AAA 变为 BBB”这类变化状态描述，只描述当前实现（例如直接写“当前支持 BBB”）。  
- **If no update needed /**：在任务总结中明确说明“已评估，本次无需更新 README”。

## Memory workflow
- **Before task /**：执行任务前，先读 `README.md`，再读 `memory.md`（若存在）。`README.md` 用于理解项目当前结构与约定，`memory.md` 用于避免对历史上已经解决过的特殊问题重复思考。
- **Strict separation /**：`README.md` 负责“当前项目是什么、如何组织、如何工作”；`memory.md` 负责“哪些特殊问题曾经很难、为什么难、以后遇到该怎么更快解决”。禁止把两者写成重复内容。
- **After task /**：每次任务结束后，都必须判断本次是否产生了值得保留的长期记忆。
- **Only save reusable knowledge /**：只有当某条结论未来复用价值高、容易遗忘、排查成本高、或能明显减少重复试错时，才写入 `memory.md`。
- **Not a changelog /**：`memory.md` 不是任务日志，不记录普通改动、临时状态、一次性需求、无复用价值的小修小补。
- **Update existing entries first /**：已有相同或相近问题时，优先更新原条目，而不是新增重复条目。
- **Required entry shape /**：建议每条使用固定结构：`Problem` / `Cause` / `Fix` / `Verify` / `Scope`。
- **If absent, create when justified /**：若 `memory.md` 不存在，但本次任务确实形成了高价值可复用经验，则创建它。
- **If no memory needed /**：若评估后无须沉淀，则在总结中明确写出“已评估，本次无需更新 memory.md”。

## Quick questions to ask me 
当信息不足时，优先问我这些（不要全量扫描）：
- 报错日志（Unity Console 堆栈）或具体异常类型
- 目标行为/预期行为
- 涉及的场景/Prefab 名称与路径
- 相关脚本路径（例如 `Assets/Scripts/...`）
- Unity 版本、使用的渲染管线（Built-in/URP/HDRP）、是否用 Addressables/Netcode 等

---

## Default assumption 
- `.meta` 文件与 `Library/Temp` 等不参与业务逻辑讨论。
- 功能实现以 `Assets/**/Scripts` 的 C# 代码为准。
- 若 `Unity MCP` 可用，Editor / Scene / Prefab / Console / Test 的真实状态以 `Unity MCP` 返回结果为准。
- 如果你发现自己在读大量资源文件，请立刻停下并改用“按需读取”的方式。
