using System;
using System.Collections.Generic;
// using UnityEditor.ShaderGraph;
using UnityEngine;

namespace Kernel.Bullet
{
    /// <summary>
    /// 维护当前装备的词元顺序，并缓存对应的编译结果。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AttackFormulaLoadout : MonoBehaviour
    {
        [SerializeField] private List<PlaceableTokenData> items = new();

        private CompiledAttack compiledAttack;
        private readonly List<BaseTokenData> expandedTokens = new();
        private bool isDirty = true;
        private int revision;

        public event Action Changed;

        public IReadOnlyList<PlaceableTokenData> Items => items;
        public IReadOnlyList<BaseTokenData> Tokens => expandedTokens;
        public int Revision => revision;
        public bool HasTokens => expandedTokens.Count > 0;

        public CompiledAttack CurrentCompiledAttack
        {
            get
            {
                EnsureCompiled();
                return compiledAttack;
            }
        }

        private void Awake()
        {
            RebuildExpandedTokens();
            EnsureCompiled();
        }

        private void OnValidate()
        {
            RebuildExpandedTokens();
            MarkDirty();
        }

        /// <summary>
        /// summary: 当外部调整了词元顺序或内容后，显式把当前缓存标记为需要重新编译。
        /// param: 无
        /// returns: 无
        /// </summary>
        public void MarkDirty()
        {
            RebuildExpandedTokens();
            isDirty = true;
            revision++;
            NotifyChanged();
        }

        /// <summary>
        /// summary: 用新的有序可放置 token 物件列表替换当前 loadout，并立即标记为待重新编译。
        /// param: orderedItems 新的物件顺序
        /// returns: 无
        /// </summary>
        public void SetItems(IEnumerable<PlaceableTokenData> orderedItems)
        {
            items.Clear();
            if (orderedItems != null)
            {
                foreach (PlaceableTokenData item in orderedItems)
                {
                    if (item != null)
                    {
                        items.Add(item);
                    }
                }
            }

            MarkDirty();
        }

        /// <summary>
        /// summary: 用单格基础 token 顺序替换当前 loadout，供旧调用方继续复用。
        /// param: orderedTokens 新的单格 token 顺序
        /// returns: 无
        /// </summary>
        public void SetTokens(IEnumerable<BaseTokenData> orderedTokens)
        {
            items.Clear();
            if (orderedTokens != null)
            {
                foreach (BaseTokenData token in orderedTokens)
                {
                    if (token != null)
                    {
                        items.Add(token);
                    }
                }
            }

            MarkDirty();
        }

        /// <summary>
        /// summary: 获取当前可执行的编译结果；若缓存失效则先重新编译。
        /// param: attack 输出的编译结果
        /// returns: 当前编译结果可发射时返回 true
        /// </summary>
        public bool TryGetCompiledAttack(out CompiledAttack attack)
        {
            attack = EnsureCompiled();
            return attack != null && attack.CanFire;
        }

        /// <summary>
        /// summary: 强制基于当前词元顺序执行一次重新编译。
        /// param: 无
        /// returns: 最新的编译结果
        /// </summary>
        public CompiledAttack Recompile()
        {
            compiledAttack = AttackFormulaCompiler.Compile(items);
            isDirty = false;
            return compiledAttack;
        }

        /// <summary>
        /// summary: 统一广播 loadout 内容已变化，供 HUD 和其他只读观察者实时刷新。
        /// param: 无
        /// returns: 无
        /// </summary>
        private void NotifyChanged()
        {
            Changed?.Invoke();
        }

        private void RebuildExpandedTokens()
        {
            expandedTokens.Clear();
            if (items == null)
            {
                return;
            }

            for (int i = 0; i < items.Count; i++)
            {
                PlaceableTokenData item = items[i];
                if (item != null)
                {
                    item.AppendCompileTokens(expandedTokens);
                }
            }
        }

        private CompiledAttack EnsureCompiled()
        {
            if (isDirty || compiledAttack == null)
            {
                return Recompile();
            }

            return compiledAttack;
        }
    }
}
