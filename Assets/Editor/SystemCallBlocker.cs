#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;
// using lon;
namespace SystemCallBlocker
{
    /// <summary>
    /// 在 Unity Editor 中扫描 C# 源码，检测并禁止对指定 API/调用模式的直接使用（支持多规则）。
    /// 通过监听脚本编译完成事件与进入 Play 模式事件，在控制台输出错误并可阻止进入 Play。
    /// </summary>
    [InitializeOnLoad]
    public static class SystemCallBlocker
    {
        /// <summary>
        /// 是否在发现违规时阻止进入 Play 模式。
        /// </summary>
        private const bool BlockEnterPlayMode = true;

        /// <summary>
        /// 全局扫描排除：第三方/缓存等目录（对所有规则都生效）。
        /// </summary>
        private static readonly string[] GlobalExcludedPathContains = new[]
        {
            "/Packages/",
            "/Library/",
            "/Temp/",
            "/obj/",
            "/Logs/",
            "/Assets/Plugins/",
            "/Assets/AmplifyShaderEditor/",
            "/Assets/InputSystem/",
            "/Assets/Editor/",
            "/Assets/Input/",
            "/Assets/TutorialInfo/",
        };

        /// <summary>
        /// 单条禁用规则：包含匹配模式、替换提示、规则专属排除目录与快速过滤关键词。
        /// </summary>
        private sealed class ForbiddenRule
        {
            /// <summary>规则标识（用于日志显示）。</summary>
            public readonly string RuleId;

            /// <summary>发现违规时的替换建议提示。</summary>
            public readonly string ReplacementHint;

            /// <summary>规则专属排除目录（在全局排除之外再额外排除）。</summary>
            public readonly string[] ExcludedPathContains;

            /// <summary>快速过滤关键词：文件内容不包含任一关键词时，跳过该规则的正则匹配。</summary>
            public readonly string[] QuickContainsAny;

            /// <summary>违规匹配正则列表。</summary>
            public readonly Regex[] Patterns;

            /// <summary>
            /// 创建一条禁用规则。
            /// </summary>
            /// <param name="ruleId">规则标识（用于日志）。</param>
            /// <param name="replacementHint">替换建议提示。</param>
            /// <param name="patterns">要匹配的正则列表。</param>
            /// <param name="excludedPathContains">规则专属排除目录（可空）。</param>
            /// <param name="quickContainsAny">快速过滤关键词（可空）。</param>
            public ForbiddenRule(
                string ruleId,
                string replacementHint,
                Regex[] patterns,
                string[] excludedPathContains = null,
                string[] quickContainsAny = null)
            {
                RuleId = ruleId;
                ReplacementHint = replacementHint;
                Patterns = patterns ?? Array.Empty<Regex>();
                ExcludedPathContains = excludedPathContains ?? Array.Empty<string>();
                QuickContainsAny = quickContainsAny ?? Array.Empty<string>();
            }
        }

        /// <summary>
        /// 规则列表：在这里添加/删除你想要禁止的调用（每条都有自己的 Hint 与排除目录）。
        /// </summary>
        private static readonly ForbiddenRule[] Rules = new[]
        {
            // 示例规则：禁止 System.Random
            new ForbiddenRule(
                ruleId: "System.Random",
                replacementHint: "请改用：Vocalith.Random",
                patterns: new[]
                {
                    // 1) new System.Random(...)
                    new Regex(@"\bnew\s+System\.Random\s*\(", RegexOptions.Compiled),
                    // 2) typeof(System.Random)
                    new Regex(@"\btypeof\s*\(\s*System\.Random\s*\)", RegexOptions.Compiled),
                    // 3) System.Random 作为类型/成员访问
                    new Regex(@"\bSystem\.Random\b", RegexOptions.Compiled),
                },
                // 规则专属排除：比如你允许某些目录里继续用 System.Random（示例，按需改/删）
                // excludedPathContains: new[]
                // {
                //     // "/Assets/Scripts/LegacyAllowRandom/",
                // },
                // 快速过滤关键词：不包含这些就不跑正则（提高速度）
                quickContainsAny: new[]
                {
                    "System.Random",
                    "Random"
                }
            ),

            new ForbiddenRule(
                ruleId: "禁止 UnityEngine.Debug",
                replacementHint: "请改用：Vocalith.GameDebug（可控/可测试）",
                patterns: new[]
                {
                    // 1) 禁止 Debug.xxx(...) / UnityEngine.Debug.xxx(...) / global::UnityEngine.Debug.xxx(...)
                    // 覆盖常见方法：Log*、Assert*、Draw*、Break、ClearDeveloperConsole 等
                    new Regex(
                        @"\b(?:global::\s*)?(?:UnityEngine\s*\.\s*)?Debug\s*\.\s*" +
                        @"(?:Log|LogFormat|LogWarning|LogWarningFormat|LogError|LogErrorFormat|LogException|" +
                        @"LogAssertion|LogAssertionFormat|" +
                        @"Assert|AssertFormat|AssertAssertion|AssertAssertionFormat|" +
                        @"DrawLine|DrawRay|Break|ClearDeveloperConsole)\s*\(",
                        RegexOptions.Compiled
                    ),

                    // 2) 禁止 Debug 的常见属性/字段访问（不带括号的那种）
                    // 例如：Debug.isDebugBuild、Debug.unityLogger、Debug.developerConsoleVisible
                    new Regex(
                        @"\b(?:global::\s*)?(?:UnityEngine\s*\.\s*)?Debug\s*\.\s*" +
                        @"(?:isDebugBuild|developerConsoleVisible|unityLogger)\b",
                        RegexOptions.Compiled
                    ),

                    // 3) 禁止直接引用类型本体（例如 typeof(UnityEngine.Debug)、作为参数类型、别名指向等）
                    new Regex(
                        @"\b(?:global::\s*)?UnityEngine\s*\.\s*Debug\b",
                        RegexOptions.Compiled
                    ),

                    // 4) 禁止用 using 别名把 UnityEngine.Debug 引进来（例如：using UDebug = UnityEngine.Debug;）
                    new Regex(
                        @"\busing\s+[A-Za-z_][A-Za-z0-9_]*\s*=\s*(?:global::\s*)?UnityEngine\s*\.\s*Debug\s*;",
                        RegexOptions.Compiled
                    ),
                },
                excludedPathContains: new[]
                {
                    // 你原本的：封装 GameDebug 的目录
                    "/Assets/Scripts/Vocalith/Log/",

                    // 强烈建议：把 Editor 工具目录排除，否则 Editor 工具与扫描器自己会被误杀
                    // 下面两个按需保留/改成你实际路径


                    // 如果你的扫描器脚本（RandomGuarcs）不在 Editor 目录里，建议把它所在目录也加进来，例如：
                    // "/Assets/Scripts/RandomGuard/",
                },
                quickContainsAny: new[]
                {
                    // 用更“定向”的关键词提速，避免每个文件都跑一堆正则
                    "Debug.",
                    "UnityEngine.Debug",
                    "using",
                }
            )

            // 你可以像这样继续加规则（示例模板）：
            // new ForbiddenRule(
            //     ruleId: "禁止 DateTime.Now",
            //     replacementHint: "请改用：MyTime.Now（可控/可测试）",
            //     patterns: new[]
            //     {
            //         new Regex(@"\bDateTime\.Now\b", RegexOptions.Compiled),
            //     },
            //     excludedPathContains: new[] { "/Assed.ts/SomeFolderAllowTime/" },
            //     quickContainsAny: new[] { "DateTime", "Now" }
            // ),
        };

        static SystemCallBlocker()
        {
            // 脚本编译完成后扫描一次
            CompilationPipeline.compilationFinished += OnCompilationFinished;

            // 进入 Play 模式前再扫描一次（更强约束）
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        /// <summary>
        /// 编译完成回调：扫描项目 C# 源码，输出违规错误信息。
        /// </summary>
        /// <param name="obj">回调参数（未使用）。</param>
        private static void OnCompilationFinished(object obj)
        {
            ScanAndReport(blockPlayMode: false);
        }

        /// <summary>
        /// PlayMode 状态变更回调：在即将进入 Play 时扫描并可阻止进入。
        /// </summary>
        /// <param name="state">PlayMode 状态。</param>
        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.ExitingEditMode)
                return;

            bool hasViolation = ScanAndReport(blockPlayMode: true);
            if (hasViolation)
            {
                // 阻止进入 Play
                EditorApplication.isPlaying = false;
            }
        }

        /// <summary>
        /// 扫描所有 C# 文件并输出违规位置。可选择是否阻止进入 Play。
        /// </summary>
        /// <param name="blockPlayMode">是否作为阻止进入 Play 的扫描。</param>
        /// <returns>是否发现违规。</returns>
        private static bool ScanAndReport(bool blockPlayMode)
        {
            try
            {
                var parent = Directory.GetParent(Application.dataPath);
                if (parent == null)
                {
                    Debug.LogError("[SystemRandomBlocker] 扫描失败：无法获取项目根目录。");
                    return false;
                }

                string projectRoot = parent.FullName;

                // 扫描 Assets 下的 .cs
                var csFiles = Directory.GetFiles(Application.dataPath, "*.cs", SearchOption.AllDirectories);
                bool hasViolation = false;

                foreach (var filePath in csFiles)
                {
                    string normalized = filePath.Replace("\\", "/");

                    // 全局排除（对所有规则生效）
                    if (IsExcludedByList(normalized, GlobalExcludedPathContains))
                        continue;

                    string text = File.ReadAllText(filePath);
                    string scanText = StripCommentsPreserveLines(text, stripStrings: true);

                    // 对每条规则分别判断（规则可有自己专属排除目录）
                    foreach (var rule in Rules)
                    {
                        if (rule == null || rule.Patterns.Length == 0)
                            continue;

                        // 规则专属排除（在全局排除之外再额外排除）
                        if (IsExcludedByList(normalized, rule.ExcludedPathContains))
                            continue;

                        // 快速过滤：不包含关键词就跳过该规则
                        if (!ContainsAny(scanText, rule.QuickContainsAny))
                            continue;

                        string rel = ToRelativePath(projectRoot, filePath);
                        if (ScanOneRuleAndReport(rule, scanText, rel))
                        {
                            Debug.LogError($"[SystemCallBlocker] 违规调用：规则 {rule.RuleId}，文件 {rel}。{rule.ReplacementHint}");
                            hasViolation = true;
                        }
                            
                    }
                }

                if (hasViolation && blockPlayMode && BlockEnterPlayMode)
                {
                    Debug.LogError("[SystemRandomBlocker] 已阻止进入 Play 模式：请先清理所有违规调用。");
                    
                }

                return hasViolation;
            }
            catch (Exception e)
            {
                Debug.LogError($"[SystemRandomBlocker] 扫描失败：{e}");
                return false;
            }
        }

        /// <summary>
        /// 扫描单条规则并输出违规位置（同一文件同一规则：默认每行只报一次，减少重复刷屏）。
        /// </summary>
        /// <param name="rule">规则配置。</param>
        /// <param name="scanText">已去注释/字面量后的扫描文本。</param>
        /// <param name="relativePath">相对项目根目录路径。</param>
        /// <returns>是否发现违规。</returns>
        private static bool ScanOneRuleAndReport(ForbiddenRule rule, string scanText, string relativePath)
        {
            bool hasViolation = false;
            var reportedLines = new HashSet<int>();

            foreach (var pattern in rule.Patterns)
            {
                if (pattern == null) continue;

                var matches = pattern.Matches(scanText);
                if (matches.Count <= 0) continue;

                foreach (Match m in matches)
                {
                    int line = GetLineNumber(scanText, m.Index);

                    // 同一规则同一文件：每行只提示一次（避免多个正则同一行重复报）
                    if (!reportedLines.Add(line))
                        continue;



                    hasViolation = true;
                }
            }

            return hasViolation;
        }

        /// <summary>
        /// 判断文本是否包含 needles 中任意一个关键词；若 needles 为空则默认返回 true（不做过滤）。
        /// </summary>
        /// <param name="text">要检查的文本。</param>
        /// <param name="needles">关键词列表。</param>
        /// <returns>是否包含任意关键词。</returns>
        private static bool ContainsAny(string text, string[] needles)
        {
            if (needles == null || needles.Length == 0)
                return true;

            if (string.IsNullOrEmpty(text))
                return false;

            foreach (var n in needles)
            {
                if (string.IsNullOrEmpty(n)) continue;
                if (text.Contains(n, StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// 判断路径是否属于某个排除列表。
        /// </summary>
        /// <param name="normalizedPath">规范化后的路径（使用 /）。</param>
        /// <param name="excludedContainsList">排除片段列表。</param>
        /// <returns>是否排除。</returns>
        private static bool IsExcludedByList(string normalizedPath, string[] excludedContainsList)
        {
            if (excludedContainsList == null || excludedContainsList.Length == 0)
                return false;

            foreach (var s in excludedContainsList)
            {
                if (string.IsNullOrEmpty(s)) continue;

                if (normalizedPath.Contains(s, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// 将绝对路径转换为相对项目根目录的路径。
        /// </summary>
        /// <param name="projectRoot">项目根目录。</param>
        /// <param name="filePath">绝对文件路径。</param>
        /// <returns>相对路径。</returns>
        private static string ToRelativePath(string projectRoot, string filePath)
        {
            projectRoot = projectRoot.Replace("\\", "/").TrimEnd('/') + "/";
            string fp = filePath.Replace("\\", "/");
            return fp.StartsWith(projectRoot, StringComparison.OrdinalIgnoreCase)
                ? fp.Substring(projectRoot.Length)
                : fp;
        }

        /// <summary>
        /// 通过字符索引估算行号（1-based）。
        /// </summary>
        /// <param name="text">全文内容。</param>
        /// <param name="index">匹配起始索引。</param>
        /// <returns>行号（从 1 开始）。</returns>
        private static int GetLineNumber(string text, int index)
        {
            int line = 1;
            for (int i = 0; i < index && i < text.Length; i++)
            {
                if (text[i] == '\n') line++;
            }
            return line;
        }

        /// <summary>
        /// 移除 C# 源码中的注释（// 与 /* */），并可选移除字符串/字符字面量内容。
        /// 会保留换行符，以保证后续行号计算仍然准确。
        /// </summary>
        /// <param name="code">原始源码文本。</param>
        /// <param name="stripStrings">是否同时移除字符串与字符字面量内容以避免误报。</param>
        /// <returns>处理后的文本（注释/字面量内容被空格替换，换行保留）。</returns>
        private static string StripCommentsPreserveLines(string code, bool stripStrings = true)
        {
            if (string.IsNullOrEmpty(code))
                return code;

            var chars = code.ToCharArray();

            bool inLineComment = false;
            bool inBlockComment = false;
            bool inString = false;          // "..."
            bool inVerbatimString = false;  // @"..."
            bool inChar = false;            // '...'
            bool escape = false;

            for (int i = 0; i < chars.Length; i++)
            {
                char c = chars[i];
                char next = (i + 1 < chars.Length) ? chars[i + 1] : '\0';

                // 行注释：直到换行结束
                if (inLineComment)
                {
                    if (c != '\n' && c != '\r')
                        chars[i] = ' ';
                    else
                        inLineComment = false;
                    continue;
                }

                // 块注释：直到 */ 结束（保留换行）
                if (inBlockComment)
                {
                    if (c == '*' && next == '/')
                    {
                        chars[i] = ' ';
                        chars[i + 1] = ' ';
                        i++; // 跳过 '/'
                        inBlockComment = false;
                    }
                    else if (c != '\n' && c != '\r')
                    {
                        chars[i] = ' ';
                    }
                    continue;
                }

                // 字符字面量 'x'（可选移除，保留换行）
                if (inChar)
                {
                    if (stripStrings && c != '\n' && c != '\r')
                        chars[i] = ' ';

                    if (escape)
                    {
                        escape = false;
                        continue;
                    }

                    if (c == '\\')
                    {
                        escape = true;
                        continue;
                    }

                    if (c == '\'')
                        inChar = false;

                    continue;
                }

                // 普通字符串 "..."（可选移除，保留换行）
                if (inString)
                {
                    if (stripStrings && c != '\n' && c != '\r')
                        chars[i] = ' ';

                    if (escape)
                    {
                        escape = false;
                        continue;
                    }

                    if (c == '\\')
                    {
                        escape = true;
                        continue;
                    }

                    if (c == '"')
                        inString = false;

                    continue;
                }

                // 逐字字符串 @"..."（"" 表示转义引号）
                if (inVerbatimString)
                {
                    if (stripStrings && c != '\n' && c != '\r')
                        chars[i] = ' ';

                    if (c == '"' && next == '"')
                    {
                        // "" 视为一个引号字符，继续保持在逐字字符串里
                        if (stripStrings)
                            chars[i + 1] = ' ';
                        i++;
                        continue;
                    }

                    if (c == '"')
                        inVerbatimString = false;

                    continue;
                }

                // 进入注释？
                if (c == '/' && next == '/')
                {
                    chars[i] = ' ';
                    chars[i + 1] = ' ';
                    i++;
                    inLineComment = true;
                    continue;
                }

                if (c == '/' && next == '*')
                {
                    chars[i] = ' ';
                    chars[i + 1] = ' ';
                    i++;
                    inBlockComment = true;
                    continue;
                }

                // 进入字符串/字符？
                if (c == '@' && next == '"')
                {
                    // @"..."
                    if (stripStrings)
                    {
                        chars[i] = ' ';
                        chars[i + 1] = ' ';
                    }
                    i++;
                    inVerbatimString = true;
                    continue;
                }

                if (c == '"')
                {
                    if (stripStrings) chars[i] = ' ';
                    inString = true;
                    escape = false;
                    continue;
                }

                if (c == '\'')
                {
                    if (stripStrings) chars[i] = ' ';
                    inChar = true;
                    escape = false;
                    continue;
                }
            }

            return new string(chars);
        }
    }
}
#endif
