using System.Collections.Generic;
using Kernel.Bullet;

namespace Kernel.UI
{
    /// <summary>
    /// 提供背包与 Spell Book 之间的 item 级布局规则，便于运行时复用和 EditMode 测试。
    /// </summary>
    public static class BackPackTokenLayoutUtility
    {
        /// <summary>
        /// summary: 清空一组占用格缓存。
        /// param: cells 需要清空的占用格集合
        /// returns: 无
        /// </summary>
        public static void ClearCells(IList<TokenCellOccupancy> cells)
        {
            if (cells == null)
            {
                return;
            }

            for (int i = 0; i < cells.Count; i++)
            {
                cells[i] = TokenCellOccupancy.Empty;
            }
        }

        /// <summary>
        /// summary: 判断一个 item 能否以指定锚点放入一组占用格里。
        /// param: cells 当前占用格集合
        /// param: anchorIndex 目标锚点格索引
        /// param: columns 当前区域的固定列数
        /// param: item 待放入的 item
        /// param: anchorIndexToIgnore 可选的忽略锚点，用于同一 item 的移动判定
        /// returns: 放置合法时返回 true
        /// </summary>
        public static bool CanPlaceItem(IReadOnlyList<TokenCellOccupancy> cells, int anchorIndex, int columns, PlaceableTokenData item, int anchorIndexToIgnore = -1)
        {
            if (cells == null || columns <= 0 || item == null || anchorIndex < 0)
            {
                return false;
            }

            int span = item.SlotSpan > 0 ? item.SlotSpan : 1;
            int endIndex = anchorIndex + span - 1;
            if (endIndex >= cells.Count)
            {
                return false;
            }

            if ((anchorIndex / columns) != (endIndex / columns))
            {
                return false;
            }

            for (int i = 0; i < span; i++)
            {
                TokenCellOccupancy cell = cells[anchorIndex + i];
                if (!cell.IsOccupied || cell.anchorIndex == anchorIndexToIgnore)
                {
                    continue;
                }

                return false;
            }

            return true;
        }

        /// <summary>
        /// summary: 把一个 item 按指定锚点写入占用格集合。
        /// param: cells 目标占用格集合
        /// param: anchorIndex 目标锚点格索引
        /// param: item 需要写入的 item
        /// returns: 无
        /// </summary>
        public static void WriteItem(IList<TokenCellOccupancy> cells, int anchorIndex, PlaceableTokenData item)
        {
            if (cells == null || item == null)
            {
                return;
            }

            int span = item.SlotSpan > 0 ? item.SlotSpan : 1;
            for (int i = 0; i < span; i++)
            {
                cells[anchorIndex + i] = new TokenCellOccupancy(item, anchorIndex, i, i == 0);
            }
        }

        /// <summary>
        /// summary: 按锚点把指定 item 实例从占用格集合里整件移除。
        /// param: cells 当前占用格集合
        /// param: anchorIndex 需要清除的 item 锚点格索引
        /// returns: 无
        /// </summary>
        public static void ClearItem(IList<TokenCellOccupancy> cells, int anchorIndex)
        {
            if (cells == null || anchorIndex < 0)
            {
                return;
            }

            for (int i = 0; i < cells.Count; i++)
            {
                if (cells[i].anchorIndex == anchorIndex)
                {
                    cells[i] = TokenCellOccupancy.Empty;
                }
            }
        }

        /// <summary>
        /// summary: 判断两组 item 序列在长度与引用顺序上是否完全一致。
        /// param: left 左侧 item 序列
        /// param: right 右侧 item 序列
        /// returns: 两侧序列完全一致时返回 true
        /// </summary>
        public static bool SequenceEquals(IReadOnlyList<PlaceableTokenData> left, IReadOnlyList<PlaceableTokenData> right)
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

        /// <summary>
        /// summary: 按从左到右顺序收集一组占用格中所有锚点 item，压缩成新的 loadout 序列。
        /// param: cells 当前 Spell Book 占用格集合
        /// returns: 按锚点顺序压缩后的 item 列表
        /// </summary>
        public static List<PlaceableTokenData> BuildCompactLoadoutItems(IReadOnlyList<TokenCellOccupancy> cells)
        {
            List<PlaceableTokenData> result = new();
            if (cells == null)
            {
                return result;
            }

            for (int i = 0; i < cells.Count; i++)
            {
                TokenCellOccupancy cell = cells[i];
                if (cell.IsOccupied && cell.isAnchor)
                {
                    result.Add(cell.item);
                }
            }

            return result;
        }

        /// <summary>
        /// summary: 用当前 loadout item 顺序填充 Spell Book，并把超出的 item 尝试回填到玩家库存。
        /// param: loadoutItems 当前 loadout 持有的有序 item 列表
        /// param: spellBookCells 需要写入的 Spell Book 占用格缓存
        /// param: inventory 用于接收溢出 item 的玩家库存
        /// param: droppedOverflowCount 输出无法回填的溢出 item 数量
        /// returns: 成功回填进库存的溢出 item 数量
        /// </summary>
        public static int PopulateSpellBookCells(
            IReadOnlyList<PlaceableTokenData> loadoutItems,
            IList<TokenCellOccupancy> spellBookCells,
            PlayerBulletTokenInventory inventory,
            out int droppedOverflowCount)
        {
            droppedOverflowCount = 0;
            if (spellBookCells == null)
            {
                return 0;
            }

            ClearCells(spellBookCells);
            if (loadoutItems == null)
            {
                return 0;
            }

            int nextAnchorIndex = 0;
            int storedOverflowCount = 0;
            for (int i = 0; i < loadoutItems.Count; i++)
            {
                PlaceableTokenData item = loadoutItems[i];
                if (item == null)
                {
                    continue;
                }

                int span = item.SlotSpan > 0 ? item.SlotSpan : 1;
                if (nextAnchorIndex + span <= spellBookCells.Count)
                {
                    WriteItem(spellBookCells, nextAnchorIndex, item);
                    nextAnchorIndex += span;
                    continue;
                }

                if (inventory != null && inventory.TryAddItem(item, out _))
                {
                    storedOverflowCount++;
                    continue;
                }

                droppedOverflowCount++;
            }

            return storedOverflowCount;
        }
    }
}
