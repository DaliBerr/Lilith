using System.Collections.Generic;
using Kernel.Bullet;

namespace Kernel.UI
{
    /// <summary>
    /// 提供背包与 Spell Book 之间的 token 布局规则，便于运行时复用和 EditMode 测试。
    /// </summary>
    public static class BackPackTokenLayoutUtility
    {
        /// <summary>
        /// summary: 把 loadout 的前 N 个 token 写入 Spell Book，并把溢出的 token 尝试回填到玩家库存。
        /// param: loadoutTokens 当前 loadout 持有的有序 token 列表
        /// param: spellBookSlots 需要写入的 Spell Book 槽位缓存
        /// param: inventory 用于接收溢出 token 的玩家库存
        /// param: droppedOverflowCount 输出无法回填的溢出 token 数量
        /// returns: 成功回填进库存的溢出 token 数量
        /// </summary>
        public static int PopulateSpellBookSlots(
            IReadOnlyList<BaseTokenData> loadoutTokens,
            IList<BaseTokenData> spellBookSlots,
            PlayerBulletTokenInventory inventory,
            out int droppedOverflowCount)
        {
            droppedOverflowCount = 0;
            if (spellBookSlots == null)
            {
                return 0;
            }

            for (int i = 0; i < spellBookSlots.Count; i++)
            {
                spellBookSlots[i] = null;
            }

            if (loadoutTokens == null)
            {
                return 0;
            }

            int populatedCount = 0;
            int slotCount = spellBookSlots.Count;
            for (int i = 0; i < loadoutTokens.Count; i++)
            {
                BaseTokenData token = loadoutTokens[i];
                if (i < slotCount)
                {
                    spellBookSlots[i] = token;
                    populatedCount++;
                    continue;
                }

                if (inventory != null && inventory.TryAddToken(token, out _))
                {
                    continue;
                }

                droppedOverflowCount++;
            }

            return loadoutTokens.Count - populatedCount - droppedOverflowCount;
        }

        /// <summary>
        /// summary: 按槽位顺序收集 Spell Book 中的非空 token，并压缩成传给编译器的有序列表。
        /// param: spellBookSlots 当前 Spell Book 槽位缓存
        /// returns: 按从左到右压缩后的 token 列表
        /// </summary>
        public static List<BaseTokenData> BuildCompactLoadoutTokens(IReadOnlyList<BaseTokenData> spellBookSlots)
        {
            List<BaseTokenData> result = new();
            if (spellBookSlots == null)
            {
                return result;
            }

            for (int i = 0; i < spellBookSlots.Count; i++)
            {
                BaseTokenData token = spellBookSlots[i];
                if (token != null)
                {
                    result.Add(token);
                }
            }

            return result;
        }

        /// <summary>
        /// summary: 判断两组 token 序列在长度与引用顺序上是否完全一致。
        /// param: left 左侧 token 序列
        /// param: right 右侧 token 序列
        /// returns: 两侧序列完全一致时返回 true
        /// </summary>
        public static bool SequenceEquals(IReadOnlyList<BaseTokenData> left, IReadOnlyList<BaseTokenData> right)
        {
            if (ReferenceEquals(left, right))
            {
                return true;
            }

            if (left == null || right == null || left.Count != right.Count)
            {
                return false;
            }

            for (int i = 0; i < left.Count; i++)
            {
                if (left[i] != right[i])
                {
                    return false;
                }
            }

            return true;
        }
    }
}
