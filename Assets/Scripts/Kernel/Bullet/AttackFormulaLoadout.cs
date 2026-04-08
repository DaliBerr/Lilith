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
        [SerializeField] private List<BaseTokenData> tokens = new();

        private CompiledAttack compiledAttack;
        private bool isDirty = true;
        private int revision;

        public event Action Changed;

        public IReadOnlyList<BaseTokenData> Tokens => tokens;
        public int Revision => revision;
        public bool HasTokens => tokens != null && tokens.Count > 0;

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
            EnsureCompiled();
        }

        private void OnValidate()
        {
            MarkDirty();
        }

        /// <summary>
        /// summary: 当外部调整了词元顺序或内容后，显式把当前缓存标记为需要重新编译。
        /// param: 无
        /// returns: 无
        /// </summary>
        public void MarkDirty()
        {
            isDirty = true;
            revision++;
            NotifyChanged();
        }

        /// <summary>
        /// summary: 用新的有序词元列表替换当前 loadout，并立即标记为待重新编译。
        /// param: orderedTokens 新的词元顺序
        /// returns: 无
        /// </summary>
        public void SetTokens(IEnumerable<BaseTokenData> orderedTokens)
        {
            tokens.Clear();
            if (orderedTokens != null)
            {
                tokens.AddRange(orderedTokens);
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
            compiledAttack = AttackFormulaCompiler.Compile(tokens);
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
