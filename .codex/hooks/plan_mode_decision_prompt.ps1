$ErrorActionPreference = 'Stop'

function Write-JsonAndExit {
    param(
        [hashtable]$Value,
        [int]$ExitCode = 0
    )

    $Value | ConvertTo-Json -Depth 8 -Compress | Write-Output
    exit $ExitCode
}

$rawInput = [Console]::In.ReadToEnd()

if ([string]::IsNullOrWhiteSpace($rawInput)) {
    Write-JsonAndExit @{ continue = $true }
}

try {
    $payload = $rawInput | ConvertFrom-Json -ErrorAction Stop
}
catch {
    Write-JsonAndExit @{
        continue = $true
        systemMessage = "Plan-mode decision hook could not parse its input, so it allowed the turn to continue."
    }
}

$permissionMode = [string]$payload.permission_mode
if ($permissionMode -ne 'plan') {
    Write-JsonAndExit @{ continue = $true }
}

Write-JsonAndExit @{
    continue = $true
    hookSpecificOutput = @{
        hookEventName = 'UserPromptSubmit'
        additionalContext = '计划模式规则：如果当前任务存在任何不确定的决策点、取舍、实现方向、验证范围或无法从本地上下文安全确认的选择，必须直接向用户提问，不要猜测或自作主张。问题保持简短明确，优先一次提出 1-3 个必要问题。对中大型编码任务，计划时必须评估是否存在边界清晰、可验证、可并行、低风险的子任务；若存在，默认应把这些子任务交给 gpt-5.4-mini 子代理，gpt-5.3-codex-spark 可用于只读探索、影响面盘点、文档审查、局部验证或低风险局部实现。计划文本必须包含“子代理分工”小节：列出拟委派子任务、推荐模型、输入范围、预期产物，以及主模型保留的需求澄清、风险判断、跨模块/架构决策、最终集成、冲突处理与收尾验证。若任务很小、无清晰拆分空间、子任务会阻塞关键路径、涉及高风险/不可逆/跨系统状态一致性/安全或数据损失风险，或当前环境没有可用子代理工具，可以跳过委派；跳过时在“子代理分工”小节中简要说明原因。不要为了形式而创建子代理，目标是降低主线程上下文污染、提升并行效率并节省套餐额度。'
    }
}
