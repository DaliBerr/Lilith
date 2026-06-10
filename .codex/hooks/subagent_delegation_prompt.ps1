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
        additionalContext = '子代理委派策略：收到实现、修复、迁移、重构、测试补齐或代码审查类请求后，先判断是否存在边界清晰、可并行、低风险且不会阻塞主模型下一步的子任务。对中大型编码任务，若这类子任务存在且子代理工具可用，应优先创建 gpt-5.4-mini 子代理执行；gpt-5.3-codex-spark 可用于只读探索、影响面盘点、文档审查、并行验证或低风险局部实现。适合委派的任务包括：代码入口/调用链扫描、影响面盘点、测试覆盖审查、文档一致性检查、局部实现候选、验证结果整理。主模型保留需求澄清、高风险决策、跨模块/架构取舍、最终代码整合、冲突处理和收尾验证。不要为了形式而创建子代理；很小的任务、没有清晰拆分空间的任务、会阻塞关键路径的子任务、高风险/不可逆/跨系统状态一致性/安全或数据损失风险任务，以及当前环境没有可用子代理工具时，可以跳过委派并简要说明原因。默认不要 fork 完整上下文给子代理，只传窄任务、必要路径和预期产物，以降低 token / 套餐额度消耗。'
    }
}
