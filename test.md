我们现在引入新的记忆/文档工作流程。我们现在需要全面的迁移agents.md，并引入跨会话记忆，记录最近的工作流程，需要记住的操作习惯，TODO, 某个任务执行的handoff等等.维护如下结构的obsidian仓库：
vault/
  Index.md
  AGENTS.md
  Memory_Rules.md
  TODO.md
  Inbox.md
  Dashboard/
    Home.md
    Current_Focus.md
    Project_Status_Board.md
    Global_TODO.md
    Waiting_For.md
  Projects/
    _Project_Template.md
    _Handoff_Template.md
    ProjectA/
    ProjectB/
  Sessions/
    _Session_Template.md
    2026/
    └─ 2026-05/
      ├─ 2026-05-22.md
      └─ 2026-05-23.md
  Tasks/
    Task_Index.md
    Active/
    Backlog/
    Blocked/
    Done/
    Cancelled/
  Decisions/
    _Decision_Template.md
    Decision_Index.md
    Active/
    Superseded/
    Archived/
  Preferences/
    Agent_Behavior_Preferences.md
    Coding_Preferences.md
    Writing_Preferences.md
    Tooling_Preferences.md
    Review_Preferences.md
  Knowledge/
        Knowledge_Index.md
        Tools/
        Concepts/
        Troubleshooting/
        References/
  Archive/
        Projects/
        Sessions/
        Tasks/
        Decisions/
        Old_Notes/
  Attachments/
        Images/
        PDFs/
        Exports/
        Misc/


Index.md
全局入口。
负责：

告诉 Agent 这个 Vault 的结构
告诉 Agent 当前应该从哪里开始读
列出最重要的索引文件
链接到 Dashboard / Projects / Tasks / Workflows 等等

AGENTS.md
全局AGENTS.md，各仓库链接至此，维护全局的操作规则

Memory_Rules.md
该记忆仓库的使用规则，什么值得记，什么不值得记，应该什么时候写入，什么时候归档等重要规则


Dashboard/
面向人类的面板

Inbox/
这是“暂存区”，不是长期记忆区。负责：临时想法 临时任务 还没分类的信息 Agent不确定该放哪里的内容 未经整理的内容（如日志，错误信息）

Projects/
维护project note和handoff文档

对于project note:使用如下的模板(保持内容简短，不要过分冗长的写成改动日志）
# Project Name

## Status
- State:
- Priority:
- Last updated:
- Related repo:
- Related local docs:

## Current Focus
- 当前正在做什么：

## Current State
- 目前已经完成：
- 目前卡在哪里：
- 当前假设：

## Next Steps
1.
2.
3.

## Open Questions
- [ ]

## Risks
- [ ]

## Links
- Repo:
- AGENTS.md:
- memory.md:
- Latest handoff:
- Related decisions:

## Recent Handoffs
- [[11_Project_Handoffs/Project_Name/2026-05-22]]

Todo分为两层，gobal Todo and Project TODO(project TODO placed inside Projects folder)

Project Handoff 关注
    某个项目执行到了哪里
    改了什么
    下一步是什么
    有什么风险
    哪些文件受影响

Session 关注：
    今天或这次对话整体做了什么
    可能横跨多个项目

Handoff Template：
# Handoff - Project Name - YYYY-MM-DD

## Goal
- 本次目标：

## Done
- 

## Changed
- 文件：
- 配置：
- 数据：

## Current State
- 

## Next Steps
1.
2.
3.

## Risks / Blockers
- 

## Verification
- 已验证：
- 未验证：

Sessions/
这是按日期记录的工作会话。

适合记录：今天和 Agent 做了什么
今天切换过哪些项目
哪些东西还没整理
哪些决定需要后续固化
推荐按年月日分

# Session - YYYY-MM-DD

## Focus
- 

## Worked On
- Project:
- Task:

## Important Notes
- 

## Decisions Made
- 

## TODO Created
- 

## Handoffs Written
- 

## Needs Processing
- [ ]




Decisions 放重要的决策内容

Projects/文件夹下独立维护每个项目，放入note, todo 以及

以上的内容均需要在完成任务之后判断是否需要更新，追加写入。

Knowledge/
负责放：

工具使用经验
故障排查经验
概念解释
长期参考资料
配置样例
错误解决方案