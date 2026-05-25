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
        additionalContext = '计划模式规则：如果当前任务存在任何不确定的决策点、取舍、实现方向、验证范围或无法从本地上下文安全确认的选择，必须直接向用户提问，不要猜测或自作主张。问题保持简短明确，优先一次提出 1-3 个必要问题。'
    }
}
