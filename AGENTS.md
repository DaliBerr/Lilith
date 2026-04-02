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
4) **修改要可编译、可回滚**  
   - 保持 API 变更最小化
   - 给出明确的修改点与原因
   - 若涉及多人协作，优先不改变资源 GUID
5) **当上下文可能超限时，主动摘要**  
   - 用项目结构摘要（模块、关键类、依赖）替代全文粘贴
   - 明确你已忽略哪些目录以节省上下文
6) **为必要的方法/函数添加注释**
   - 注释保持简短
   - 注释至少包含"<summary>,<param>,<returns>"
7) **README 优先与维护**  
   - 执行任务前，先阅读根目录 `README.md`，优先理解现有架构说明与约定
   - 任务完成后，评估本次改动是否影响架构、流程、入口、依赖或关键脚本说明
   - 若有影响，需同步更新 `README.md`，确保文档与当前实现一致
8) **UI 文本组件规范**
   - 所有文字类条目默认使用 `TMPro`（如 `TMP_Text` / `TextMeshProUGUI`）
   - 非兼容性修复场景下，不再新增 `UnityEngine.UI.Text`
9) **分层边界是硬规则**
   - 禁止在 `Assets/Scripts/Vocalith/**` 新增任何 `Kernel.*` 引用
   - 若基础设施需要游戏语义，必须改为在 `Kernel` 增加 adapter / extension / bridge，或将抽象下沉到 `Vocalith`
   - 当前其他 `Vocalith` 子模块若仍存在历史反向依赖，属于技术债，不代表规则例外
10) **按通知阈值发送完成通知**
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
- 如果你发现自己在读大量资源文件，请立刻停下并改用“按需读取”的方式。
