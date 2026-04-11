using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Kernel.Bullet
{
    /// <summary>
    /// 表示一个可被放置到背包或 Spell Book 里的 token 物件。
    /// </summary>
    public abstract class PlaceableTokenData : ScriptableObject
    {
        public abstract int SlotSpan { get; }
        public virtual float DamageMultiplier => 1f;

        /// <summary>
        /// summary: 返回当前物件在指定局部 offset 上应显示的视觉 token。
        /// param: localOffset 当前物件内部的局部格索引
        /// returns: 该格需要显示的 token；无效 offset 返回 null
        /// </summary>
        public abstract BaseTokenData GetVisualToken(int localOffset);

        /// <summary>
        /// summary: 追加当前物件参与攻击编译的成员 token 序列。
        /// param: buffer 用于接收成员 token 的输出缓冲区
        /// returns: 无
        /// </summary>
        public abstract void AppendCompileTokens(List<BaseTokenData> buffer);

        /// <summary>
        /// summary: 返回当前拾取物在世界中的显示文本。
        /// param: 无
        /// returns: 当前物件的拾取显示文本
        /// </summary>
        public virtual string GetPickupDisplayText()
        {
            List<BaseTokenData> tokens = new();
            AppendCompileTokens(tokens);
            return BuildConcatenatedDisplayText(tokens);
        }

        /// <summary>
        /// summary: 返回当前物件在 Token Select 弹窗里的说明文本。
        /// param: 无
        /// returns: 当前物件的选择描述；默认返回空字符串
        /// </summary>
        public virtual string GetSelectionDescription()
        {
            return string.Empty;
        }

        protected static string BuildConcatenatedDisplayText(IReadOnlyList<BaseTokenData> tokens)
        {
            if (tokens == null || tokens.Count <= 0)
            {
                return string.Empty;
            }

            StringBuilder builder = new();
            for (int i = 0; i < tokens.Count; i++)
            {
                BaseTokenData token = tokens[i];
                if (token == null)
                {
                    continue;
                }

                builder.Append(token.GetResolvedDisplayText());
            }

            return builder.ToString();
        }
    }
}
