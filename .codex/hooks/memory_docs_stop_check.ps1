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
        systemMessage = "Memory/docs Stop hook could not parse its input, so it allowed the turn to finish."
    }
}

$lastMessage = [string]$payload.last_assistant_message
$alreadyContinued = $false
if ($null -ne $payload.stop_hook_active) {
    $alreadyContinued = [bool]$payload.stop_hook_active
}

if ([string]::IsNullOrWhiteSpace($lastMessage)) {
    Write-JsonAndExit @{ continue = $true }
}

$hasMemoryCloseout = $lastMessage -match '(Memory Consistency Pass|Memory_Rules|记忆一致性|记忆规则|记忆收尾|收尾检查)'
$hasReadmeCheck = $lastMessage -match 'README(\.md)?'
$hasRepoMemoryCheck = $lastMessage -match 'memory\.md'
$hasAgentsCheck = $lastMessage -match 'AGENTS(\.md)?'

if ($hasMemoryCloseout -and $hasReadmeCheck -and $hasRepoMemoryCheck -and $hasAgentsCheck) {
    Write-JsonAndExit @{ continue = $true }
}

$missing = New-Object System.Collections.Generic.List[string]
if (-not $hasMemoryCloseout) {
    $missing.Add('Memory Consistency Pass / 记忆一致性收尾说明')
}
if (-not $hasReadmeCheck) {
    $missing.Add('README.md 是否需要更新')
}
if (-not $hasRepoMemoryCheck) {
    $missing.Add('memory.md 是否需要更新')
}
if (-not $hasAgentsCheck) {
    $missing.Add('AGENTS.md 是否需要更新')
}

$reason = '收尾前请补齐：' + ($missing -join '；') + '。请先按项目规则完成/说明相关记忆面检查，并明确 repo-local README.md、memory.md、AGENTS.md 是否已更新或无需更新。'

if ($alreadyContinued) {
    Write-JsonAndExit @{
        continue = $true
        systemMessage = "Memory/docs Stop hook still sees missing closeout text after one continuation: $reason"
    }
}

Write-JsonAndExit @{
    decision = 'block'
    reason = $reason
}
