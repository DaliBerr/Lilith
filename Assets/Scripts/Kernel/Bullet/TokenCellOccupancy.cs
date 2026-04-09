using System;

namespace Kernel.Bullet
{
    /// <summary>
    /// 描述单个背包格当前由哪个可放置 token 物件占用。
    /// </summary>
    [Serializable]
    public struct TokenCellOccupancy
    {
        public PlaceableTokenData item;
        public int anchorIndex;
        public int localOffset;
        public bool isAnchor;

        public TokenCellOccupancy(PlaceableTokenData item, int anchorIndex, int localOffset, bool isAnchor)
        {
            this.item = item;
            this.anchorIndex = anchorIndex;
            this.localOffset = localOffset;
            this.isAnchor = isAnchor;
        }

        public bool IsOccupied => item != null;
        public BaseTokenData VisualToken => item != null ? item.GetVisualToken(localOffset) : null;

        public static TokenCellOccupancy Empty => new(null, -1, 0, false);
    }
}
