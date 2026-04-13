using System;
using System.Collections.Generic;
using Kernel.Bullet;
using UnityEngine;
using Vocalith.Logging;

/// <summary>
/// 维护玩家当前持有的可放置 token 库存，固定为 48 格并按 8 列连续占位。
/// </summary>
[DisallowMultipleComponent]
public sealed class PlayerBulletTokenInventory : MonoBehaviour
{
    public const int Capacity = 48;
    public const int Columns = 8;

    [SerializeField] private List<PlaceableTokenData> startingTokens = new();

    private readonly List<TokenCellOccupancy> runtimeCells = new(Capacity);
    private bool isInitialized;

    public event Action Changed;

    public IReadOnlyList<TokenCellOccupancy> Slots => runtimeCells;

    private void Awake()
    {
        EnsureInitialized();
    }

    /// <summary>
    /// summary: 确保运行时格子已按固定 48 格初始化，并按顺序放入 Inspector 预置物件。
    /// param: 无
    /// returns: 无
    /// </summary>
    public void EnsureInitialized()
    {
        if (isInitialized && runtimeCells.Count == Capacity)
        {
            return;
        }

        runtimeCells.Clear();
        for (int i = 0; i < Capacity; i++)
        {
            runtimeCells.Add(TokenCellOccupancy.Empty);
        }

        isInitialized = true;

        int ignoredCount = 0;
        if (startingTokens != null)
        {
            for (int i = 0; i < startingTokens.Count; i++)
            {
                PlaceableTokenData item = startingTokens[i];
                if (item == null)
                {
                    continue;
                }

                if (!TryAddItemInternal(item, out _))
                {
                    ignoredCount++;
                }
            }
        }

        if (ignoredCount > 0)
        {
            GameDebug.LogWarning($"[PlayerBulletTokenInventory] Starting token count exceeds inventory placement capacity. Ignored {ignoredCount} item(s).");
        }
    }

    /// <summary>
    /// summary: 读取指定格子的占用状态。
    /// param: index 需要读取的格索引
    /// returns: 当前格的占用信息；非法索引返回空占用
    /// </summary>
    public TokenCellOccupancy GetCell(int index)
    {
        EnsureInitialized();
        return IsValidIndex(index) ? runtimeCells[index] : TokenCellOccupancy.Empty;
    }

    /// <summary>
    /// summary: 从指定格子解析该格所属的物件及其锚点信息。
    /// param: index 需要读取的格索引
    /// param: item 输出解析到的物件
    /// param: anchorIndex 输出该物件在库存中的锚点格索引
    /// param: localOffset 输出当前格在物件内部的局部偏移
    /// returns: 当前格被占用时返回 true
    /// </summary>
    public bool TryGetItemAtCell(int index, out PlaceableTokenData item, out int anchorIndex, out int localOffset)
    {
        TokenCellOccupancy cell = GetCell(index);
        item = cell.item;
        anchorIndex = cell.anchorIndex;
        localOffset = cell.localOffset;
        return cell.IsOccupied;
    }

    /// <summary>
    /// summary: 判断指定锚点格是否能容纳一个新的可放置物件。
    /// param: anchorIndex 目标锚点格索引
    /// param: item 待放入的物件
    /// returns: 放置合法时返回 true
    /// </summary>
    public bool CanPlaceItem(int anchorIndex, PlaceableTokenData item)
    {
        EnsureInitialized();
        return CanPlaceItemInternal(anchorIndex, item, anchorIndexToIgnore: -1);
    }

    /// <summary>
    /// summary: 在指定锚点格放入一个新的可放置物件。
    /// param: anchorIndex 目标锚点格索引
    /// param: item 待放入的物件
    /// returns: 成功放入时返回 true
    /// </summary>
    public bool TryPlaceItem(int anchorIndex, PlaceableTokenData item)
    {
        EnsureInitialized();
        if (!CanPlaceItemInternal(anchorIndex, item, anchorIndexToIgnore: -1))
        {
            return false;
        }

        WriteItem(anchorIndex, item);
        NotifyChanged();
        return true;
    }

    /// <summary>
    /// summary: 把一个已存在于库存中的物件移动到新的锚点格位置。
    /// param: fromAnchorIndex 物件当前所在格，可传锚点格或任意被占用格
    /// param: toAnchorIndex 物件新的锚点格索引
    /// returns: 成功移动时返回 true
    /// </summary>
    public bool TryMoveItem(int fromAnchorIndex, int toAnchorIndex)
    {
        EnsureInitialized();
        if (!TryGetItemAtCell(fromAnchorIndex, out PlaceableTokenData item, out int resolvedAnchorIndex, out _))
        {
            return false;
        }

        if (resolvedAnchorIndex == toAnchorIndex)
        {
            return true;
        }

        if (!CanPlaceItemInternal(toAnchorIndex, item, resolvedAnchorIndex))
        {
            return false;
        }

        ClearItemAtAnchor(resolvedAnchorIndex);
        WriteItem(toAnchorIndex, item);
        NotifyChanged();
        return true;
    }

    /// <summary>
    /// summary: 把一个物件放入库存中的首个合法连续空位。
    /// param: item 待放入的物件
    /// param: insertedIndex 输出实际写入的锚点格索引
    /// returns: 成功放入时返回 true
    /// </summary>
    public bool TryAddItem(PlaceableTokenData item, out int insertedIndex)
    {
        EnsureInitialized();
        if (!TryAddItemInternal(item, out insertedIndex))
        {
            insertedIndex = -1;
            return false;
        }

        NotifyChanged();
        return true;
    }

    /// <summary>
    /// summary: 移除指定格所属的整个物件。
    /// param: index 当前格索引
    /// param: item 输出被移除的物件
    /// param: anchorIndex 输出被移除物件原本的锚点格索引
    /// returns: 成功移除时返回 true
    /// </summary>
    public bool TryRemoveItemAtCell(int index, out PlaceableTokenData item, out int anchorIndex)
    {
        EnsureInitialized();
        if (!TryGetItemAtCell(index, out item, out anchorIndex, out _))
        {
            item = null;
            anchorIndex = -1;
            return false;
        }

        ClearItemAtAnchor(anchorIndex);
        NotifyChanged();
        return true;
    }

    /// <summary>
    /// summary: 查找当前库存中的首个空格索引。
    /// param: 无
    /// returns: 空格索引；若已满则返回 -1
    /// </summary>
    public int FindFirstEmptySlot()
    {
        EnsureInitialized();
        for (int i = 0; i < runtimeCells.Count; i++)
        {
            if (!runtimeCells[i].IsOccupied)
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>
    /// summary: 读取指定格上当前显示的基础 token，兼容旧调用方。
    /// param: index 需要读取的格索引
    /// returns: 该格应显示的基础 token；空格返回 null
    /// </summary>
    public BaseTokenData GetToken(int index)
    {
        return GetCell(index).VisualToken;
    }

    /// <summary>
    /// summary: 向指定格写入一个单格基础 token，兼容旧调用方。
    /// param: index 目标格索引
    /// param: token 需要写入的单格基础 token；传入 null 表示清空该格所属物件
    /// returns: 写入成功时返回 true
    /// </summary>
    public bool SetToken(int index, BaseTokenData token)
    {
        EnsureInitialized();
        if (!IsValidIndex(index))
        {
            return false;
        }

        if (runtimeCells[index].IsOccupied)
        {
            ClearItemAtAnchor(runtimeCells[index].anchorIndex);
        }

        if (token == null)
        {
            NotifyChanged();
            return true;
        }

        if (!CanPlaceItemInternal(index, token, anchorIndexToIgnore: -1))
        {
            return false;
        }

        WriteItem(index, token);
        NotifyChanged();
        return true;
    }

    /// <summary>
    /// summary: 交换两个格所属的单格物件，兼容旧测试场景。
    /// param: firstIndex 第一个格索引
    /// param: secondIndex 第二个格索引
    /// returns: 成功交换时返回 true
    /// </summary>
    public bool SwapSlots(int firstIndex, int secondIndex)
    {
        EnsureInitialized();
        if (!IsValidIndex(firstIndex) || !IsValidIndex(secondIndex))
        {
            return false;
        }

        if (firstIndex == secondIndex)
        {
            return true;
        }

        bool firstOccupied = TryGetItemAtCell(firstIndex, out PlaceableTokenData firstItem, out int firstAnchorIndex, out _);
        bool secondOccupied = TryGetItemAtCell(secondIndex, out PlaceableTokenData secondItem, out int secondAnchorIndex, out _);

        if (firstAnchorIndex == secondAnchorIndex)
        {
            return true;
        }

        if ((firstItem != null && firstItem.SlotSpan > 1) || (secondItem != null && secondItem.SlotSpan > 1))
        {
            return false;
        }

        if (firstOccupied && !secondOccupied)
        {
            return TryMoveItem(firstAnchorIndex, secondIndex);
        }

        if (!firstOccupied && secondOccupied)
        {
            return TryMoveItem(secondAnchorIndex, firstIndex);
        }

        ClearItemAtAnchor(firstAnchorIndex);
        ClearItemAtAnchor(secondAnchorIndex);

        bool canRestoreFirst = secondItem == null || CanPlaceItemInternal(firstAnchorIndex, secondItem, anchorIndexToIgnore: -1);
        bool canRestoreSecond = firstItem == null || CanPlaceItemInternal(secondAnchorIndex, firstItem, anchorIndexToIgnore: -1);
        if (!canRestoreFirst || !canRestoreSecond)
        {
            if (firstItem != null && firstAnchorIndex >= 0)
            {
                WriteItem(firstAnchorIndex, firstItem);
            }

            if (secondItem != null && secondAnchorIndex >= 0)
            {
                WriteItem(secondAnchorIndex, secondItem);
            }

            return false;
        }

        if (secondItem != null)
        {
            WriteItem(firstAnchorIndex, secondItem);
        }

        if (firstItem != null && secondAnchorIndex >= 0)
        {
            WriteItem(secondAnchorIndex, firstItem);
        }

        NotifyChanged();
        return true;
    }

    /// <summary>
    /// summary: 用单格基础 token 的首个可用位置添加方式兼容旧调用方。
    /// param: token 需要放入库存的单格基础 token
    /// param: insertedIndex 输出实际写入的锚点格索引
    /// returns: 成功放入时返回 true
    /// </summary>
    public bool TryAddToken(BaseTokenData token, out int insertedIndex)
    {
        return TryAddItem(token, out insertedIndex);
    }

    /// <summary>
    /// summary: 清空全部库存格，并广播一次刷新事件。
    /// param: 无
    /// returns: 无
    /// </summary>
    public void Clear()
    {
        EnsureInitialized();
        bool changed = false;
        for (int i = 0; i < runtimeCells.Count; i++)
        {
            if (!runtimeCells[i].IsOccupied)
            {
                continue;
            }

            runtimeCells[i] = TokenCellOccupancy.Empty;
            changed = true;
        }

        if (changed)
        {
            NotifyChanged();
        }
    }

    /// <summary>
    /// summary: 把当前运行时库存恢复到 Inspector 预置的起始 token 布局。
    /// param: 无
    /// returns: 无
    /// </summary>
    public void ResetToStartingTokens()
    {
        EnsureInitialized();
        runtimeCells.Clear();
        for (int i = 0; i < Capacity; i++)
        {
            runtimeCells.Add(TokenCellOccupancy.Empty);
        }

        if (startingTokens != null)
        {
            for (int i = 0; i < startingTokens.Count; i++)
            {
                PlaceableTokenData item = startingTokens[i];
                if (item != null)
                {
                    TryAddItemInternal(item, out _);
                }
            }
        }

        NotifyChanged();
    }

    private bool TryAddItemInternal(PlaceableTokenData item, out int insertedIndex)
    {
        insertedIndex = -1;
        if (item == null)
        {
            return false;
        }

        for (int i = 0; i < runtimeCells.Count; i++)
        {
            if (!CanPlaceItemInternal(i, item, anchorIndexToIgnore: -1))
            {
                continue;
            }

            WriteItem(i, item);
            insertedIndex = i;
            return true;
        }

        return false;
    }

    private bool CanPlaceItemInternal(int anchorIndex, PlaceableTokenData item, int anchorIndexToIgnore)
    {
        if (!IsValidIndex(anchorIndex) || item == null)
        {
            return false;
        }

        int slotSpan = Mathf.Max(1, item.SlotSpan);
        int endIndex = anchorIndex + slotSpan - 1;
        if (endIndex >= Capacity)
        {
            return false;
        }

        int row = anchorIndex / Columns;
        int endRow = endIndex / Columns;
        if (row != endRow)
        {
            return false;
        }

        for (int i = 0; i < slotSpan; i++)
        {
            TokenCellOccupancy cell = runtimeCells[anchorIndex + i];
            if (!cell.IsOccupied || cell.anchorIndex == anchorIndexToIgnore)
            {
                continue;
            }

            return false;
        }

        return true;
    }

    private void WriteItem(int anchorIndex, PlaceableTokenData item)
    {
        int slotSpan = Mathf.Max(1, item.SlotSpan);
        for (int i = 0; i < slotSpan; i++)
        {
            runtimeCells[anchorIndex + i] = new TokenCellOccupancy(item, anchorIndex, i, i == 0);
        }
    }

    private void ClearItemAtAnchor(int anchorIndex)
    {
        if (anchorIndex < 0)
        {
            return;
        }

        for (int i = 0; i < runtimeCells.Count; i++)
        {
            if (runtimeCells[i].anchorIndex == anchorIndex)
            {
                runtimeCells[i] = TokenCellOccupancy.Empty;
            }
        }
    }

    private static bool IsValidIndex(int index)
    {
        return index >= 0 && index < Capacity;
    }

    private void NotifyChanged()
    {
        Changed?.Invoke();
    }
}
