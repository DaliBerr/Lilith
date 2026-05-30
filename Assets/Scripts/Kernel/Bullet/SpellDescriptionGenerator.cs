using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using VocalithRandom = Vocalith.Random;

namespace Kernel.Bullet
{
    /// <summary>
    /// 根据已经编译好的攻击语义生成背包中展示的短法术描述。
    /// </summary>
    public static class SpellDescriptionGenerator
    {
        private const string CoreToken = "{core}";
        private const string BehaviorToken = "{behavior}";
        private const string ResultToken = "{result}";
        private const string CountToken = "{count}";
        private const string EffectsToken = "{effects}";

        private static readonly SpellDescriptionCatalogData DefaultCatalog = SpellDescriptionCatalogData.CreateDefault();

        /// <summary>
        /// summary: 生成可直接写入 TMP_Text 的 rich text 法术描述。
        /// param name="compiledAttack": 当前 Spell Book 编译结果
        /// param name="items": 当前 Spell Book 中的可放置 token 物件；第一版仅用于判断是否为空
        /// returns: 短中文 rich text 描述
        /// </summary>
        public static string GenerateRichText(CompiledAttack compiledAttack, IReadOnlyList<PlaceableTokenData> items = null)
        {
            return GenerateRichText(compiledAttack, items, null, new VocalithRandom());
        }

        public static string GenerateRichText(CompiledAttack compiledAttack, IReadOnlyList<PlaceableTokenData> items, SpellDescriptionCatalogData catalog)
        {
            return GenerateRichText(compiledAttack, items, catalog, new VocalithRandom());
        }

        internal static string GenerateRichText(CompiledAttack compiledAttack, IReadOnlyList<PlaceableTokenData> items, VocalithRandom random)
        {
            return GenerateRichText(compiledAttack, items, null, random);
        }

        internal static string GenerateRichText(
            CompiledAttack compiledAttack,
            IReadOnlyList<PlaceableTokenData> items,
            SpellDescriptionCatalogData catalog,
            VocalithRandom random)
        {
            VocalithRandom rng = random ?? new VocalithRandom();
            SpellDescriptionCatalogData resolvedCatalog = catalog ?? DefaultCatalog;
            if (compiledAttack == null || !compiledAttack.CanFire || compiledAttack.CoreType == AttackCoreType.None)
            {
                return FormatTemplate(Pick(resolvedCatalog.emptySpellPrompts, rng), resolvedCatalog, null);
            }

            string core = Highlight(ResolveCoreLabel(compiledAttack.CoreType, resolvedCatalog), resolvedCatalog.colors.core);
            string behavior = ResolveBehaviorPhrase(compiledAttack, resolvedCatalog, rng);
            string result = ResolveResultPhrase(compiledAttack, resolvedCatalog, rng);
            string main = PickMainSentence(core, behavior, result, resolvedCatalog, rng);
            string special = BuildSpecialSentence(compiledAttack, items, resolvedCatalog, rng);
            return string.IsNullOrEmpty(special) ? main : main + "\n" + special;
        }

        private static string PickMainSentence(
            string core,
            string behavior,
            string result,
            SpellDescriptionCatalogData catalog,
            VocalithRandom random)
        {
            string template = Pick(catalog.mainSentenceTemplates, random);
            Dictionary<string, string> replacements = new()
            {
                { CoreToken, core },
                { BehaviorToken, behavior },
                { ResultToken, result },
            };
            return FormatTemplate(template, catalog, replacements);
        }

        private static string ResolveCoreLabel(AttackCoreType coreType, SpellDescriptionCatalogData catalog)
        {
            string coreName = coreType.ToString();
            if (catalog?.coreLabels != null)
            {
                for (int i = 0; i < catalog.coreLabels.Count; i++)
                {
                    SpellDescriptionCoreEntry entry = catalog.coreLabels[i];
                    if (entry == null || string.IsNullOrEmpty(entry.label))
                    {
                        continue;
                    }

                    if (string.Equals(entry.coreType, coreName, StringComparison.OrdinalIgnoreCase))
                    {
                        return entry.label;
                    }
                }
            }

            return coreType == AttackCoreType.None ? "核心" : coreName;
        }

        private static string ResolveBehaviorPhrase(CompiledAttack compiledAttack, SpellDescriptionCatalogData catalog, VocalithRandom random)
        {
            SpellDescriptionBehaviorEntry entry = FindBehaviorEntry(compiledAttack.BehaviorType, catalog)
                ?? FindBehaviorEntry(AttackBehaviorType.Straight, catalog);
            if (entry == null)
            {
                return Highlight("笔直射出", catalog.colors.behavior);
            }

            int count = ResolveBehaviorCount(compiledAttack);
            bool needsCount = !string.IsNullOrEmpty(entry.countUnit);
            IReadOnlyList<string> templates = needsCount && count <= 0 && entry.fallbackPhraseTemplates != null && entry.fallbackPhraseTemplates.Count > 0
                ? entry.fallbackPhraseTemplates
                : entry.phraseTemplates;

            string countText = needsCount ? ToChineseNumber(Mathf.Max(0, count)) + entry.countUnit : string.Empty;
            Dictionary<string, string> replacements = needsCount
                ? new Dictionary<string, string> { { CountToken, countText } }
                : null;
            return FormatTemplate(Pick(templates, random), catalog, replacements);
        }

        private static int ResolveBehaviorCount(CompiledAttack compiledAttack)
        {
            return compiledAttack.BehaviorType switch
            {
                AttackBehaviorType.Spread => Mathf.Max(1, compiledAttack.SpreadProjectileCount),
                AttackBehaviorType.Bounce => Mathf.Max(0, compiledAttack.AttackSpec.bounceCount),
                AttackBehaviorType.Pierce => Mathf.Max(0, compiledAttack.AttackSpec.pierceCount),
                _ => 0,
            };
        }

        private static SpellDescriptionBehaviorEntry FindBehaviorEntry(AttackBehaviorType behaviorType, SpellDescriptionCatalogData catalog)
        {
            string behaviorName = behaviorType.ToString();
            if (catalog?.behaviorPhrases == null)
            {
                return null;
            }

            for (int i = 0; i < catalog.behaviorPhrases.Count; i++)
            {
                SpellDescriptionBehaviorEntry entry = catalog.behaviorPhrases[i];
                if (entry != null && string.Equals(entry.behaviorType, behaviorName, StringComparison.OrdinalIgnoreCase))
                {
                    return entry;
                }
            }

            return null;
        }

        private static string ResolveResultPhrase(CompiledAttack compiledAttack, SpellDescriptionCatalogData catalog, VocalithRandom random)
        {
            SpellDescriptionResultEntry entry = FindResultEntry(compiledAttack.ResultType, catalog)
                ?? FindResultEntry(AttackResultType.DirectDamage, catalog);
            if (entry == null)
            {
                return Highlight("直击", catalog.colors.result);
            }

            return FormatTemplate(Pick(entry.phraseTemplates, random), catalog, null);
        }

        private static SpellDescriptionResultEntry FindResultEntry(AttackResultType resultType, SpellDescriptionCatalogData catalog)
        {
            string resultName = resultType.ToString();
            if (catalog?.resultPhrases == null)
            {
                return null;
            }

            for (int i = 0; i < catalog.resultPhrases.Count; i++)
            {
                SpellDescriptionResultEntry entry = catalog.resultPhrases[i];
                if (entry != null && string.Equals(entry.resultType, resultName, StringComparison.OrdinalIgnoreCase))
                {
                    return entry;
                }
            }

            return null;
        }

        private static string BuildSpecialSentence(
            CompiledAttack compiledAttack,
            IReadOnlyList<PlaceableTokenData> items,
            SpellDescriptionCatalogData catalog,
            VocalithRandom random)
        {
            List<string> effects = new();
            CoreEffectPayload coreEffects = compiledAttack.CoreEffects.GetSanitized();
            ResultEffectPayload resultEffects = compiledAttack.ResultEffects.GetSanitized();

            AddEffectIf(effects, coreEffects.HasBurn, "Burn", catalog);
            AddEffectIf(effects, coreEffects.HasSlow, "Slow", catalog);
            AddEffectIf(effects, coreEffects.HasThunderChain, "ThunderChain", catalog);
            AddEffectIf(effects, coreEffects.HasArmoredBonus, "ArmoredBonus", catalog);
            AddEffectIf(effects, compiledAttack.ResultType == AttackResultType.Split && resultEffects.HasSplit, "Split", catalog);
            AddEffectIf(effects, compiledAttack.ResultType == AttackResultType.StatusEffect && resultEffects.HasControl, "Control", catalog);

            if (effects.Count <= 0)
            {
                return string.Empty;
            }

            string joined = JoinEffects(effects, catalog.effectSeparator);
            bool hasManyItems = items != null && items.Count > 2;
            IReadOnlyList<string> templates = hasManyItems && catalog.manyItemSpecialSentenceTemplates != null && catalog.manyItemSpecialSentenceTemplates.Count > 0
                ? catalog.manyItemSpecialSentenceTemplates
                : catalog.specialSentenceTemplates;

            Dictionary<string, string> replacements = new()
            {
                { EffectsToken, joined },
            };
            return FormatTemplate(Pick(templates, random), catalog, replacements);
        }

        private static void AddEffectIf(List<string> effects, bool condition, string effect, SpellDescriptionCatalogData catalog)
        {
            if (!condition)
            {
                return;
            }

            string phrase = ResolveSpecialEffectPhrase(effect, catalog);
            if (!string.IsNullOrEmpty(phrase))
            {
                effects.Add(phrase);
            }
        }

        private static string ResolveSpecialEffectPhrase(string effect, SpellDescriptionCatalogData catalog)
        {
            if (catalog?.specialEffects != null)
            {
                for (int i = 0; i < catalog.specialEffects.Count; i++)
                {
                    SpellDescriptionSpecialEffectEntry entry = catalog.specialEffects[i];
                    if (entry != null && string.Equals(entry.effect, effect, StringComparison.OrdinalIgnoreCase))
                    {
                        return FormatTemplate(entry.phrase, catalog, null);
                    }
                }
            }

            return string.Empty;
        }

        private static string JoinEffects(IReadOnlyList<string> effects, string separator)
        {
            if (effects == null || effects.Count <= 0)
            {
                return string.Empty;
            }

            if (effects.Count == 1)
            {
                return effects[0];
            }

            StringBuilder builder = new();
            string resolvedSeparator = separator ?? string.Empty;
            for (int i = 0; i < effects.Count; i++)
            {
                if (i > 0)
                {
                    builder.Append(resolvedSeparator);
                }

                builder.Append(effects[i]);
            }

            return builder.ToString();
        }

        private static string FormatTemplate(
            string template,
            SpellDescriptionCatalogData catalog,
            IReadOnlyDictionary<string, string> replacements)
        {
            string text = template ?? string.Empty;
            if (replacements != null)
            {
                foreach (KeyValuePair<string, string> replacement in replacements)
                {
                    text = text.Replace(replacement.Key, replacement.Value ?? string.Empty);
                }
            }

            return ApplySemanticMarkup(text, catalog.colors);
        }

        private static string ApplySemanticMarkup(string text, SpellDescriptionHighlightColors colors)
        {
            SpellDescriptionHighlightColors palette = colors ?? SpellDescriptionHighlightColors.CreateDefault();
            return (text ?? string.Empty)
                .Replace("<core>", $"<color={palette.core}>")
                .Replace("<behavior>", $"<color={palette.behavior}>")
                .Replace("<result>", $"<color={palette.result}>")
                .Replace("<value>", $"<color={palette.value}>")
                .Replace("<special>", $"<color={palette.special}>")
                .Replace("</core>", "</color>")
                .Replace("</behavior>", "</color>")
                .Replace("</result>", "</color>")
                .Replace("</value>", "</color>")
                .Replace("</special>", "</color>");
        }

        private static string Pick(IReadOnlyList<string> candidates, VocalithRandom random)
        {
            if (candidates == null || candidates.Count <= 0)
            {
                return string.Empty;
            }

            VocalithRandom rng = random ?? new VocalithRandom();
            int index = rng.Next(candidates.Count);
            return candidates[Mathf.Clamp(index, 0, candidates.Count - 1)];
        }

        private static string Highlight(string text, string color)
        {
            string safeText = text ?? string.Empty;
            return $"<color={color}>{safeText}</color>";
        }

        private static string ToChineseNumber(int value)
        {
            return value switch
            {
                1 => "一",
                2 => "二",
                3 => "三",
                4 => "四",
                5 => "五",
                6 => "六",
                7 => "七",
                8 => "八",
                9 => "九",
                10 => "十",
                _ => value.ToString(),
            };
        }
    }
}
