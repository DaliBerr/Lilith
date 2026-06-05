using UnityEngine;
using System;
using System.Collections.Generic;

namespace Kernel.Bullet
{
    /// <summary>
    /// 正式的法术修饰词元；使用 BaseTokenData.Modifiers 作为实际数值载荷，作用域由编译位置解析。
    /// </summary>
    [CreateAssetMenu(menuName = "Lilith/Bullet Tokens/Modifier Token", fileName = "ModifierToken")]
    public class ModifierTokenData : BaseTokenData
    {
        [SerializeField] private bool randomModifier;
        [SerializeField] private ModifierTokenData[] randomModifierCandidates = Array.Empty<ModifierTokenData>();

        public bool IsRandomModifier
        {
            get => randomModifier;
            set => randomModifier = value;
        }

        public IReadOnlyList<ModifierTokenData> RandomModifierCandidates => randomModifierCandidates;

        public void SetRandomModifierCandidates(params ModifierTokenData[] candidates)
        {
            randomModifierCandidates = SanitizeRandomModifierCandidates(candidates);
            randomModifier = randomModifierCandidates.Length > 0;
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            SetTokenType(TokenType.Modifier);
        }

        protected override void OnValidate()
        {
            base.OnValidate();
            SetTokenType(TokenType.Modifier);
            randomModifierCandidates = SanitizeRandomModifierCandidates(randomModifierCandidates);
            randomModifier &= randomModifierCandidates.Length > 0;
        }

        private ModifierTokenData[] SanitizeRandomModifierCandidates(IEnumerable<ModifierTokenData> candidates)
        {
            if (candidates == null)
            {
                return Array.Empty<ModifierTokenData>();
            }

            List<ModifierTokenData> sanitized = new();
            HashSet<ModifierTokenData> seen = new();
            foreach (ModifierTokenData candidate in candidates)
            {
                if (candidate == null || candidate == this || !seen.Add(candidate))
                {
                    continue;
                }

                sanitized.Add(candidate);
            }

            return sanitized.ToArray();
        }
    }
}
