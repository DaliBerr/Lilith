using System.Globalization;
using UnityEngine;

namespace Kernel.Bullet
{
    /// <summary>
    /// 表示受限 DSL 支持的修饰操作符。
    /// </summary>
    public enum TokenModifierOperator
    {
        None = 0,
        Set = 1,
        Add = 2,
        Subtract = 3,
        Multiply = 4,
        Divide = 5,
    }

    /// <summary>
    /// 负责解析并执行 token 修饰 DSL。
    /// </summary>
    public static class TokenModifierExpressionUtility
    {
        /// <summary>
        /// summary: 解析一条受限 DSL 表达式，提取操作符与字面量载荷。
        /// param: expression 需要解析的表达式文本
        /// param: operation 输出的操作符
        /// param: literal 输出的字面量文本
        /// param: errorMessage 解析失败时返回的错误消息
        /// returns: 表达式符合受限 DSL 时返回 true
        /// </summary>
        public static bool TryParseExpression(string expression, out TokenModifierOperator operation, out string literal, out string errorMessage)
        {
            operation = TokenModifierOperator.None;
            literal = string.Empty;
            errorMessage = string.Empty;

            string trimmed = expression != null ? expression.Trim() : string.Empty;
            if (string.IsNullOrWhiteSpace(trimmed))
            {
                errorMessage = "Modifier expression is empty.";
                return false;
            }

            if (TryMatchOperator(trimmed, "+=", out literal) ||
                TryMatchOperator(trimmed, "-=", out literal) ||
                TryMatchOperator(trimmed, "*=", out literal) ||
                TryMatchOperator(trimmed, "/=", out literal) ||
                TryMatchOperator(trimmed, "=", out literal))
            {
                operation = GetOperationFromPrefix(trimmed);
                if (string.IsNullOrWhiteSpace(literal))
                {
                    errorMessage = "Modifier expression is missing a literal value.";
                    return false;
                }

                return true;
            }

            errorMessage = "Modifier expression must start with one of =, +=, -=, *=, /=.";
            return false;
        }

        /// <summary>
        /// summary: 解析一个数值字面量，支持普通浮点数和 C# 风格的 f 后缀。
        /// param: literal 需要解析的数值文本
        /// param: value 输出的数值结果
        /// param: errorMessage 解析失败时返回的错误消息
        /// returns: 文本能被解析为浮点数时返回 true
        /// </summary>
        public static bool TryParseNumericLiteral(string literal, out float value, out string errorMessage)
        {
            value = 0f;
            errorMessage = string.Empty;

            string trimmed = literal != null ? literal.Trim() : string.Empty;
            if (string.IsNullOrWhiteSpace(trimmed))
            {
                errorMessage = "Numeric literal is empty.";
                return false;
            }

            if (trimmed.EndsWith("f", System.StringComparison.OrdinalIgnoreCase))
            {
                trimmed = trimmed.Substring(0, trimmed.Length - 1);
            }

            if (!float.TryParse(trimmed, NumberStyles.Float | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out value))
            {
                errorMessage = $"Unable to parse numeric literal '{literal}'.";
                return false;
            }

            return true;
        }

        /// <summary>
        /// summary: 解析一个颜色字面量，支持命名颜色和十六进制颜色。
        /// param: literal 需要解析的颜色文本
        /// param: color 输出的颜色结果
        /// param: errorMessage 解析失败时返回的错误消息
        /// returns: 文本能被解析为颜色时返回 true
        /// </summary>
        public static bool TryParseColorLiteral(string literal, out Color color, out string errorMessage)
        {
            color = Color.white;
            errorMessage = string.Empty;

            string trimmed = literal != null ? literal.Trim() : string.Empty;
            if (string.IsNullOrWhiteSpace(trimmed))
            {
                errorMessage = "Color literal is empty.";
                return false;
            }

            if (trimmed.StartsWith("#", System.StringComparison.Ordinal))
            {
                if (ColorUtility.TryParseHtmlString(trimmed, out color))
                {
                    return true;
                }

                errorMessage = $"Unable to parse hex color literal '{literal}'.";
                return false;
            }

            if (trimmed.StartsWith("Color.", System.StringComparison.OrdinalIgnoreCase))
            {
                trimmed = trimmed.Substring("Color.".Length);
            }

            switch (trimmed.ToLowerInvariant())
            {
                case "red":
                    color = Color.red;
                    return true;
                case "green":
                    color = Color.green;
                    return true;
                case "blue":
                    color = Color.blue;
                    return true;
                case "white":
                    color = Color.white;
                    return true;
                case "black":
                    color = Color.black;
                    return true;
                case "yellow":
                    color = Color.yellow;
                    return true;
                case "cyan":
                    color = Color.cyan;
                    return true;
                case "magenta":
                    color = Color.magenta;
                    return true;
                case "gray":
                case "grey":
                    color = Color.gray;
                    return true;
                case "clear":
                    color = Color.clear;
                    return true;
                default:
                    errorMessage = $"Unknown color literal '{literal}'.";
                    return false;
            }
        }

        /// <summary>
        /// summary: 把一条修饰定义应用到编译结果上。
        /// param: compiledAttack 需要被修改的编译结果
        /// param: modifier 当前修饰定义
        /// param: errorMessage 应用失败时返回的错误消息
        /// returns: 修饰成功生效时返回 true
        /// </summary>
        public static bool TryApplyModifier(CompiledAttack compiledAttack, TokenModifierDefinition modifier, out string errorMessage)
        {
            errorMessage = string.Empty;
            if (compiledAttack == null)
            {
                errorMessage = "Compiled attack is missing.";
                return false;
            }

            if (!TryParseExpression(modifier.expression, out TokenModifierOperator operation, out string literal, out errorMessage))
            {
                return false;
            }

            switch (modifier.target)
            {
                case TokenModifierTarget.TextColor:
                    if (operation != TokenModifierOperator.Set)
                    {
                        errorMessage = "TextColor only supports the = operator.";
                        return false;
                    }

                    if (!TryParseColorLiteral(literal, out Color color, out errorMessage))
                    {
                        return false;
                    }

                    compiledAttack.HasTextColorOverride = true;
                    compiledAttack.TextColor = color;
                    return true;

                case TokenModifierTarget.FontSize:
                    if (!TryParseNumericLiteral(literal, out float fontSize, out errorMessage))
                    {
                        return false;
                    }

                    if (operation == TokenModifierOperator.Divide && Mathf.Approximately(fontSize, 0f))
                    {
                        errorMessage = "Modifier cannot divide by zero.";
                        return false;
                    }

                    compiledAttack.AddFontSizeModifier(operation, fontSize);
                    return true;

                case TokenModifierTarget.ScaleMultiplier:
                    if (!TryParseNumericLiteral(literal, out float scaleOperand, out errorMessage) ||
                        !TryApplyNumericOperator(compiledAttack.ScaleMultiplier, operation, scaleOperand, out float scaleResult, out errorMessage))
                    {
                        return false;
                    }

                    compiledAttack.ScaleMultiplier = Mathf.Max(0f, scaleResult);
                    return true;

                case TokenModifierTarget.ImpactRadiusMultiplier:
                    if (!TryParseNumericLiteral(literal, out float radiusOperand, out errorMessage) ||
                        !TryApplyNumericOperator(compiledAttack.ImpactRadiusMultiplier, operation, radiusOperand, out float radiusResult, out errorMessage))
                    {
                        return false;
                    }

                    compiledAttack.ImpactRadiusMultiplier = Mathf.Max(0f, radiusResult);
                    return true;

                case TokenModifierTarget.ProjectileSpeed:
                    return TryApplyAttackSpecNumericModifier(compiledAttack, TokenModifierTarget.ProjectileSpeed, operation, literal, out errorMessage);

                case TokenModifierTarget.MaxLifetime:
                    return TryApplyAttackSpecNumericModifier(compiledAttack, TokenModifierTarget.MaxLifetime, operation, literal, out errorMessage);

                case TokenModifierTarget.MaxTravelDistance:
                    return TryApplyAttackSpecNumericModifier(compiledAttack, TokenModifierTarget.MaxTravelDistance, operation, literal, out errorMessage);

                default:
                    errorMessage = $"Unsupported modifier target '{modifier.target}'.";
                    return false;
            }
        }

        private static bool TryApplyAttackSpecNumericModifier(
            CompiledAttack compiledAttack,
            TokenModifierTarget target,
            TokenModifierOperator operation,
            string literal,
            out string errorMessage)
        {
            errorMessage = string.Empty;
            if (!TryParseNumericLiteral(literal, out float operand, out errorMessage))
            {
                return false;
            }

            AttackSpec spec = compiledAttack.AttackSpec;
            float currentValue;
            switch (target)
            {
                case TokenModifierTarget.ProjectileSpeed:
                    currentValue = spec.projectileSpeed;
                    break;
                case TokenModifierTarget.MaxLifetime:
                    currentValue = spec.maxLifetime;
                    break;
                case TokenModifierTarget.MaxTravelDistance:
                    currentValue = spec.maxTravelDistance;
                    break;
                default:
                    errorMessage = $"Unsupported AttackSpec modifier target '{target}'.";
                    return false;
            }

            if (!TryApplyNumericOperator(currentValue, operation, operand, out float nextValue, out errorMessage))
            {
                return false;
            }

            switch (target)
            {
                case TokenModifierTarget.ProjectileSpeed:
                    spec.projectileSpeed = Mathf.Max(0f, nextValue);
                    break;
                case TokenModifierTarget.MaxLifetime:
                    spec.maxLifetime = Mathf.Max(0f, nextValue);
                    break;
                case TokenModifierTarget.MaxTravelDistance:
                    spec.maxTravelDistance = Mathf.Max(0f, nextValue);
                    break;
            }

            compiledAttack.AttackSpec = spec.GetSanitized();
            return true;
        }

        private static TokenModifierOperator GetOperationFromPrefix(string expression)
        {
            if (expression.StartsWith("+=", System.StringComparison.Ordinal))
            {
                return TokenModifierOperator.Add;
            }

            if (expression.StartsWith("-=", System.StringComparison.Ordinal))
            {
                return TokenModifierOperator.Subtract;
            }

            if (expression.StartsWith("*=", System.StringComparison.Ordinal))
            {
                return TokenModifierOperator.Multiply;
            }

            if (expression.StartsWith("/=", System.StringComparison.Ordinal))
            {
                return TokenModifierOperator.Divide;
            }

            if (expression.StartsWith("=", System.StringComparison.Ordinal))
            {
                return TokenModifierOperator.Set;
            }

            return TokenModifierOperator.None;
        }

        private static bool TryMatchOperator(string expression, string prefix, out string literal)
        {
            literal = string.Empty;
            if (!expression.StartsWith(prefix, System.StringComparison.Ordinal))
            {
                return false;
            }

            literal = expression.Substring(prefix.Length).Trim();
            return true;
        }

        private static bool TryApplyNumericOperator(float currentValue, TokenModifierOperator operation, float operand, out float result, out string errorMessage)
        {
            errorMessage = string.Empty;
            result = currentValue;

            switch (operation)
            {
                case TokenModifierOperator.Set:
                    result = operand;
                    return true;
                case TokenModifierOperator.Add:
                    result = currentValue + operand;
                    return true;
                case TokenModifierOperator.Subtract:
                    result = currentValue - operand;
                    return true;
                case TokenModifierOperator.Multiply:
                    result = currentValue * operand;
                    return true;
                case TokenModifierOperator.Divide:
                    if (Mathf.Approximately(operand, 0f))
                    {
                        errorMessage = "Modifier cannot divide by zero.";
                        return false;
                    }

                    result = currentValue / operand;
                    return true;
                default:
                    errorMessage = $"Unsupported modifier operator '{operation}'.";
                    return false;
            }
        }
    }
}
