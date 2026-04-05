using System;
using System.Collections.Generic;
using Kernel.Bullet;
using UnityEngine;
using Vocalith.Logging;

/// <summary>
/// 维护玩家当前持有的 BulletToken 库存，固定为 48 格并允许重复持有。
/// </summary>
[DisallowMultipleComponent]
public sealed class PlayerBulletTokenInventory : MonoBehaviour
{
    public const int Capacity = 48;

    [SerializeField] private List<BaseTokenData> startingTokens = new();

    private readonly List<BaseTokenData> runtimeSlots = new(Capacity);
    private bool isInitialized;

    public event Action Changed;

    public IReadOnlyList<BaseTokenData> Slots => runtimeSlots;

    private void Awake()
    {
        EnsureInitialized();
    }

    /// <summary>
    /// summary: 确保运行时槽位已按固定 48 格初始化；若 inspector 预置过多 token，会截断并给出告警。
    /// param: 无
    /// returns: 无
    /// </summary>
    public void EnsureInitialized()
    {
        if (isInitialized && runtimeSlots.Count == Capacity)
        {
            return;
        }

        runtimeSlots.Clear();
        int seedCount = Mathf.Min(startingTokens != null ? startingTokens.Count : 0, Capacity);
        for (int i = 0; i < seedCount; i++)
        {
            runtimeSlots.Add(startingTokens[i]);
        }

        for (int i = seedCount; i < Capacity; i++)
        {
            runtimeSlots.Add(null);
        }

        isInitialized = true;

        if (startingTokens != null && startingTokens.Count > Capacity)
        {
            GameDebug.LogWarning($"[PlayerBulletTokenInventory] Starting token count exceeds {Capacity}. Extra entries were ignored.");
        }
    }

    /// <summary>
    /// summary: 按索引读取一个库存槽位中的 token。
    /// param: index 需要读取的槽位索引
    /// returns: 槽位中的 token；空槽返回 null
    /// </summary>
    public BaseTokenData GetToken(int index)
    {
        EnsureInitialized();
        return IsValidIndex(index) ? runtimeSlots[index] : null;
    }

    /// <summary>
    /// summary: 按索引写入一个库存槽位，并在内容变化后广播刷新事件。
    /// param: index 目标槽位索引
    /// param: token 需要写入的 token；传入 null 表示清空
    /// returns: 写入成功时返回 true
    /// </summary>
    public bool SetToken(int index, BaseTokenData token)
    {
        EnsureInitialized();
        if (!IsValidIndex(index))
        {
            return false;
        }

        if (runtimeSlots[index] == token)
        {
            return true;
        }

        runtimeSlots[index] = token;
        NotifyChanged();
        return true;
    }

    /// <summary>
    /// summary: 交换两个库存槽位的内容；若索引非法则拒绝执行。
    /// param: firstIndex 第一个槽位索引
    /// param: secondIndex 第二个槽位索引
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

        (runtimeSlots[firstIndex], runtimeSlots[secondIndex]) = (runtimeSlots[secondIndex], runtimeSlots[firstIndex]);
        NotifyChanged();
        return true;
    }

    /// <summary>
    /// summary: 将一个 token 放入首个空槽，用于把 loadout 溢出的词元回填到背包。
    /// param: token 需要放入库存的 token
    /// param: insertedIndex 输出实际写入的槽位索引
    /// returns: 成功放入空槽时返回 true
    /// </summary>
    public bool TryAddToken(BaseTokenData token, out int insertedIndex)
    {
        EnsureInitialized();
        insertedIndex = -1;
        if (token == null)
        {
            return false;
        }

        insertedIndex = FindFirstEmptySlot();
        if (insertedIndex < 0)
        {
            return false;
        }

        runtimeSlots[insertedIndex] = token;
        NotifyChanged();
        return true;
    }

    /// <summary>
    /// summary: 查找当前库存中的首个空槽。
    /// param: 无
    /// returns: 空槽索引；若已满则返回 -1
    /// </summary>
    public int FindFirstEmptySlot()
    {
        EnsureInitialized();
        for (int i = 0; i < runtimeSlots.Count; i++)
        {
            if (runtimeSlots[i] == null)
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>
    /// summary: 清空全部库存槽位，并广播一次刷新事件。
    /// param: 无
    /// returns: 无
    /// </summary>
    public void Clear()
    {
        EnsureInitialized();
        bool changed = false;
        for (int i = 0; i < runtimeSlots.Count; i++)
        {
            if (runtimeSlots[i] == null)
            {
                continue;
            }

            runtimeSlots[i] = null;
            changed = true;
        }

        if (changed)
        {
            NotifyChanged();
        }
    }

    /// <summary>
    /// summary: 判断一个索引是否落在固定容量的合法范围内。
    /// param: index 需要判断的槽位索引
    /// returns: 索引有效时返回 true
    /// </summary>
    private static bool IsValidIndex(int index)
    {
        return index >= 0 && index < Capacity;
    }

    /// <summary>
    /// summary: 统一触发库存变更事件，供背包 UI 在打开期间刷新显示。
    /// param: 无
    /// returns: 无
    /// </summary>
    private void NotifyChanged()
    {
        Changed?.Invoke();
    }
}
