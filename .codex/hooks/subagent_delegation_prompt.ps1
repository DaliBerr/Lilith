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
    $null = $rawInput | ConvertFrom-Json -ErrorAction Stop
}
catch {
    Write-JsonAndExit @{
        continue = $true
        systemMessage = "Subagent delegation hook could not parse its input, so it allowed the turn to continue."
    }
}

Write-JsonAndExit @{
    continue = $true
    hookSpecificOutput = @{
        hookEventName = 'UserPromptSubmit'
        additionalContext = '项目偏好：允许在任何时候创建 subagent 分担边界清晰、可并行的子任务。优先候选模型为 gpt-5.4-mini 与 gpt-5.3-codex-spark；适合只读探索、并行验证、影响面盘点、文档审查和局部实现。主模型仍负责需求澄清、高风险决策、最终集成与收尾。'
    }
}
