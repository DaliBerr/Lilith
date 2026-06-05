using System;
using System.Collections.Generic;
using System.Globalization;
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
        private const string BindingsToken = "{bindings}";
        private const string ConsumerToken = "{consumer}";
        private const string ValueTextToken = "{value}";
        private const string StructuresToken = "{structures}";
        private const string SpellBookToken = "{spellBook}";
        private const string TraitsToken = "{traits}";

        private static readonly SpellDescriptionCatalogData DefaultCatalog = SpellDescriptionCatalogData.CreateDefault();

        public static string GenerateRichText(CompiledSpellProgram spellProgram, IReadOnlyList<PlaceableTokenData> items = null)
        {
            return GenerateRichText(spellProgram, items, null, null, new VocalithRandom());
        }

        public static string GenerateRichText(CompiledSpellProgram spellProgram, IReadOnlyList<PlaceableTokenData> items, SpellDescriptionCatalogData catalog)
        {
            return GenerateRichText(spellProgram, items, catalog, null, new VocalithRandom());
        }

        public static string GenerateRichText(
            CompiledSpellProgram spellProgram,
            IReadOnlyList<PlaceableTokenData> items,
            SpellDescriptionCatalogData catalog,
            SpellBookData spellBook)
        {
            return GenerateRichText(spellProgram, items, catalog, spellBook, new VocalithRandom());
        }

        internal static string GenerateRichText(
            CompiledSpellProgram spellProgram,
            IReadOnlyList<PlaceableTokenData> items,
            SpellDescriptionCatalogData catalog,
            SpellBookData spellBook,
            VocalithRandom random)
        {
            VocalithRandom rng = random ?? new VocalithRandom();
            SpellDescriptionCatalogData resolvedCatalog = catalog ?? DefaultCatalog;
            SpellProjectileNode primaryProjectile = null;
            spellProgram?.TryGetPrimaryProjectile(out primaryProjectile);
            if (primaryProjectile == null || !primaryProjectile.CanFire || primaryProjectile.CoreType == AttackCoreType.None)
            {
                return FormatTemplate(Pick(resolvedCatalog.emptySpellPrompts, rng), resolvedCatalog, null);
            }

            string core = Highlight(ResolveCoreLabel(primaryProjectile.CoreType, resolvedCatalog), resolvedCatalog.colors.core);
            string behavior = ResolveBehaviorPhrase(primaryProjectile, resolvedCatalog, rng);
            string result = ResolveResultPhrase(primaryProjectile, resolvedCatalog, rng);
            string main = PickMainSentence(core, behavior, result, resolvedCatalog, rng);
            string special = BuildProgramSpecialSentence(spellProgram, primaryProjectile, items, resolvedCatalog, spellBook, rng);
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

        private static string ResolveBehaviorPhrase(SpellProjectileNode projectile, SpellDescriptionCatalogData catalog, VocalithRandom random)
        {
            SpellDescriptionBehaviorEntry entry = FindBehaviorEntry(projectile.BehaviorType, catalog)
                ?? FindBehaviorEntry(AttackBehaviorType.Straight, catalog);
            if (entry == null)
            {
                return Highlight("笔直射出", catalog.colors.behavior);
            }

            int count = ResolveBehaviorCount(projectile);
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

        private static int ResolveBehaviorCount(SpellProjectileNode projectile)
        {
            return projectile.BehaviorType switch
            {
                AttackBehaviorType.Spread => Mathf.Max(1, projectile.ProjectileCount),
                AttackBehaviorType.Bounce => Mathf.Max(0, projectile.AttackSpec.bounceCount),
                AttackBehaviorType.Chain => Mathf.Max(0, projectile.AttackSpec.chainCount),
                AttackBehaviorType.Pierce => Mathf.Max(0, projectile.AttackSpec.pierceCount),
                AttackBehaviorType.Split => Mathf.Max(1, Mathf.RoundToInt(projectile.AttackSpec.behaviorParameter)),
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

        private static string ResolveResultPhrase(SpellProjectileNode projectile, SpellDescriptionCatalogData catalog, VocalithRandom random)
        {
            if (projectile.ResultType == AttackResultType.StatusEffect &&
                projectile.ResultEffects.HasStatusApplications &&
                !projectile.ResultEffects.HasControl)
            {
                string statusLabel = ResolveStatusApplicationsLabel(projectile.ResultEffects.statusApplications);
                if (!string.IsNullOrEmpty(statusLabel))
                {
                    return $"施加<result>{statusLabel}</result>";
                }
            }

            SpellDescriptionResultEntry entry = FindResultEntry(projectile.ResultType, catalog)
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

        private static string BuildProgramSpecialSentence(
            CompiledSpellProgram spellProgram,
            SpellProjectileNode primaryProjectile,
            IReadOnlyList<PlaceableTokenData> items,
            SpellDescriptionCatalogData catalog,
            SpellBookData spellBook,
            VocalithRandom random)
        {
            List<string> sentences = new();
            AddSentenceIfNotEmpty(sentences, BuildEffectSentence(primaryProjectile, items, catalog, random));
            AddSentenceIfNotEmpty(sentences, BuildValueBindingSentence(items, catalog, random));
            AddSentenceIfNotEmpty(sentences, BuildProgramStructureSentence(spellProgram, catalog, random));
            AddSentenceIfNotEmpty(sentences, BuildSpellBookTraitSentence(spellBook, catalog, random));
            return JoinSentences(sentences);
        }

        private static string BuildProgramStructureSentence(
            CompiledSpellProgram spellProgram,
            SpellDescriptionCatalogData catalog,
            VocalithRandom random)
        {
            SpellCastBlock block = spellProgram != null ? spellProgram.PrimaryCastBlock : null;
            if (block == null)
            {
                return string.Empty;
            }

            List<string> structures = new();
            AddMulticastStructure(structures, block);
            AddModifierStructure(structures, block);
            AddTriggerPayloadStructure(structures, block, catalog, random);
            if (structures.Count <= 0)
            {
                return string.Empty;
            }

            Dictionary<string, string> replacements = new()
            {
                { StructuresToken, JoinEffects(structures, catalog.structureSeparator) },
            };
            return FormatTemplate(Pick(catalog.structureSentenceTemplates, random), catalog, replacements);
        }

        private static string BuildSpellBookTraitSentence(
            SpellBookData spellBook,
            SpellDescriptionCatalogData catalog,
            VocalithRandom random)
        {
            if (spellBook == null)
            {
                return string.Empty;
            }

            List<string> traits = new()
            {
                $"<value>{spellBook.SlotCount}</value>槽",
                $"冷却<value>{FormatSeconds(spellBook.CastCooldownSeconds)}</value>秒",
            };
            if (spellBook.CastsPerActivation > 1)
            {
                traits.Add($"每次激活<value>{ToChineseNumber(spellBook.CastsPerActivation)}</value>轮");
                if (spellBook.ActivationSpreadAngleStep > 0f)
                {
                    traits.Add($"激活扇形<value>{FormatSeconds(spellBook.ActivationSpreadAngleStep)}</value>度");
                }
            }

            if (spellBook.UsesEnergy)
            {
                traits.Add($"能量<value>{FormatSeconds(spellBook.EnergyCapacity)}</value>");
                traits.Add($"消耗<value>{FormatSeconds(spellBook.EnergyCostPerActivation)}</value>");
                if (spellBook.EnergyRegenPerSecond > 0f)
                {
                    traits.Add($"回复<value>{FormatSeconds(spellBook.EnergyRegenPerSecond)}</value>/秒");
                }
            }

            if (spellBook.HasExecutorModifiers)
            {
                traits.Add($"内建强化<value>{ToChineseNumber(spellBook.ExecutorModifiers.Count)}</value>项");
                AddExecutorModifierTraits(traits, spellBook);
            }

            int fixedItemCount = CountFixedItems(spellBook);
            if (fixedItemCount > 0)
            {
                string placement = spellBook.FixedItemPlacement == SpellBookFixedItemPlacement.BeforeEquipped ? "前置" : "后置";
                traits.Add($"{placement}常驻<value>{ToChineseNumber(fixedItemCount)}</value>词元");
            }

            Dictionary<string, string> replacements = new()
            {
                { SpellBookToken, $"<special>{spellBook.DisplayName}</special>" },
                { TraitsToken, JoinEffects(traits, "、") },
            };
            return FormatTemplate(Pick(catalog.spellBookTraitSentenceTemplates, random), catalog, replacements);
        }

        private static void AddExecutorModifierTraits(List<string> traits, SpellBookData spellBook)
        {
            if (traits == null || spellBook == null || !spellBook.HasExecutorModifiers)
            {
                return;
            }

            List<string> modifierTraits = new();
            for (int i = 0; i < spellBook.ExecutorModifiers.Count; i++)
            {
                TokenModifierDefinition modifier = spellBook.ExecutorModifiers[i].GetSanitized();
                if (string.IsNullOrWhiteSpace(modifier.expression))
                {
                    continue;
                }

                modifierTraits.Add($"{ResolveExecutorModifierTargetLabel(modifier.target)}<value>{FormatModifierExpression(modifier.expression)}</value>");
            }

            if (modifierTraits.Count > 0)
            {
                traits.Add($"内建{JoinLimited(modifierTraits, "、", 3)}");
            }
        }

        private static string ResolveExecutorModifierTargetLabel(TokenModifierTarget target)
        {
            return target switch
            {
                TokenModifierTarget.TextColor => "文字颜色",
                TokenModifierTarget.FontSize => "字号",
                TokenModifierTarget.ScaleMultiplier => "弹体尺寸",
                TokenModifierTarget.ProjectileSpeed => "速度",
                TokenModifierTarget.MaxLifetime => "寿命",
                TokenModifierTarget.MaxTravelDistance => "射程",
                TokenModifierTarget.ImpactRadiusMultiplier => "碰撞半径",
                TokenModifierTarget.ResultCount => "结果数量",
                TokenModifierTarget.ResultDuration => "结果时长",
                TokenModifierTarget.ResultMultiplier => "结果倍率",
                TokenModifierTarget.Damage => "伤害",
                TokenModifierTarget.CastCooldownMultiplier => "施法间隔",
                TokenModifierTarget.EnergyCostMultiplier => "能量消耗",
                TokenModifierTarget.CasterHealthCost => "生命代价",
                TokenModifierTarget.DropChanceMultiplierOnKill => "击败掉率",
                TokenModifierTarget.AngleSpreadMultiplier => "角度扩散",
                TokenModifierTarget.MovementVarianceMultiplier => "运动扰动",
                _ => target.ToString(),
            };
        }

        private static string FormatModifierExpression(string expression)
        {
            string trimmed = expression != null ? expression.Trim() : string.Empty;
            if (trimmed.StartsWith("*=", StringComparison.Ordinal))
            {
                return "x" + trimmed.Substring(2);
            }

            if (trimmed.StartsWith("+=", StringComparison.Ordinal) ||
                trimmed.StartsWith("-=", StringComparison.Ordinal) ||
                trimmed.StartsWith("/=", StringComparison.Ordinal))
            {
                return trimmed[0] + trimmed.Substring(2);
            }

            return trimmed;
        }

        private static void AddMulticastStructure(List<string> structures, SpellCastBlock block)
        {
            if (block == null || block.Projectiles.Count <= 1)
            {
                return;
            }

            string pattern = ResolveCastPatternLabel(block.CastPattern);
            structures.Add($"<special>CastBlock</special>{pattern}释放<value>{ToChineseNumber(block.Projectiles.Count)}</value>枚外层法术");
        }

        private static void AddModifierStructure(List<string> structures, SpellCastBlock block)
        {
            List<string> modifiers = new();
            AddModifierPhrases(modifiers, block);
            if (block?.Payloads != null)
            {
                for (int i = 0; i < block.Payloads.Count; i++)
                {
                    AddModifierPhrases(modifiers, block.Payloads[i]?.InnerBlock);
                }
            }

            if (modifiers.Count <= 0)
            {
                return;
            }

            structures.Add($"<special>Modifier</special>{JoinLimited(modifiers, "、", 3)}");
        }

        private static void AddTriggerPayloadStructure(
            List<string> structures,
            SpellCastBlock block,
            SpellDescriptionCatalogData catalog,
            VocalithRandom random)
        {
            if (block?.Payloads == null || block.Payloads.Count <= 0)
            {
                return;
            }

            List<string> payloads = new();
            for (int i = 0; i < block.Payloads.Count; i++)
            {
                SpellPayloadBlock payload = block.Payloads[i];
                if (payload == null)
                {
                    continue;
                }

                string trigger = ResolveTriggerLabel(payload.TriggerType);
                string parameter = ResolveTriggerParameterLabel(payload);
                string content = ResolvePayloadContent(payload.InnerBlock, catalog, random);
                payloads.Add($"{trigger}{parameter}后{content}");
            }

            if (payloads.Count > 0)
            {
                structures.Add($"<special>Trigger/Payload</special>{JoinEffects(payloads, "、")}");
            }
        }

        private static void AddModifierPhrases(List<string> modifiers, SpellCastBlock block)
        {
            if (modifiers == null || block?.Modifiers == null)
            {
                return;
            }

            for (int i = 0; i < block.Modifiers.Count; i++)
            {
                SpellModifierNode modifier = block.Modifiers[i];
                if (modifier?.SourceToken == null)
                {
                    continue;
                }

                string label = ResolveTokenLabel(modifier.SourceToken);
                string scope = ResolveModifierScopeLabel(modifier.Scope, modifier.TargetCount);
                string phrase = $"<special>{label}</special>指向{scope}";
                if (!ContainsExact(modifiers, phrase))
                {
                    modifiers.Add(phrase);
                }
            }
        }

        private static string ResolvePayloadContent(
            SpellCastBlock innerBlock,
            SpellDescriptionCatalogData catalog,
            VocalithRandom random)
        {
            if (innerBlock == null)
            {
                return "执行载荷";
            }

            List<string> content = new();
            if (innerBlock.Projectiles.Count > 0)
            {
                string projectileDetails = ResolvePayloadProjectileDetails(innerBlock.Projectiles, catalog, random);
                string projectileContent = $"释放<value>{ToChineseNumber(innerBlock.Projectiles.Count)}</value>枚内层法术";
                if (!string.IsNullOrEmpty(projectileDetails))
                {
                    projectileContent += $"：{projectileDetails}";
                }

                content.Add(projectileContent);
            }

            if (innerBlock.PayloadEffects.Count > 0)
            {
                List<string> effects = new();
                for (int i = 0; i < innerBlock.PayloadEffects.Count; i++)
                {
                    string label = ResolvePayloadEffectLabel(innerBlock.PayloadEffects[i]);
                    if (!string.IsNullOrEmpty(label))
                    {
                        effects.Add(label);
                    }
                }

                if (effects.Count > 0)
                {
                    content.Add($"结算{JoinEffects(effects, "、")}");
                }
            }

            return content.Count > 0 ? JoinEffects(content, "并") : "执行载荷";
        }

        private static string ResolvePayloadProjectileDetails(
            IReadOnlyList<SpellProjectileNode> projectiles,
            SpellDescriptionCatalogData catalog,
            VocalithRandom random)
        {
            if (projectiles == null || projectiles.Count <= 0)
            {
                return string.Empty;
            }

            List<string> details = new();
            for (int i = 0; i < projectiles.Count; i++)
            {
                SpellProjectileNode projectile = projectiles[i];
                if (projectile == null || !projectile.CanFire || projectile.CoreType == AttackCoreType.None)
                {
                    continue;
                }

                string detail = ResolveProjectileDetail(projectile, catalog, random);
                if (!string.IsNullOrEmpty(detail))
                {
                    details.Add(detail);
                }
            }

            return details.Count > 0 ? JoinLimited(details, "；", 3) : string.Empty;
        }

        private static string ResolveProjectileDetail(
            SpellProjectileNode projectile,
            SpellDescriptionCatalogData catalog,
            VocalithRandom random)
        {
            string core = Highlight(ResolveCoreLabel(projectile.CoreType, catalog), catalog.colors.core);
            string behavior = ResolveBehaviorPhrase(projectile, catalog, random);
            string result = ResolveResultPhrase(projectile, catalog, random);
            return TrimSentenceTerminator(PickMainSentence(core, behavior, result, catalog, random));
        }

        private static string TrimSentenceTerminator(string sentence)
        {
            string trimmed = sentence?.Trim() ?? string.Empty;
            while (trimmed.EndsWith("。", StringComparison.Ordinal) ||
                   trimmed.EndsWith(".", StringComparison.Ordinal))
            {
                trimmed = trimmed.Substring(0, trimmed.Length - 1).TrimEnd();
            }

            return trimmed;
        }

        private static string ResolvePayloadEffectLabel(SpellPayloadEffectNode effect)
        {
            if (effect == null)
            {
                return string.Empty;
            }

            string label = effect.ResultType switch
            {
                AttackResultType.Explosion => "爆炸",
                AttackResultType.StatusEffect => ResolvePayloadStatusEffectLabel(effect),
                AttackResultType.Split => "分裂",
                AttackResultType.Healing => "治疗",
                AttackResultType.Drain => "汲取",
                AttackResultType.Shield => "护盾",
                AttackResultType.Leave => "残留",
                AttackResultType.Push => "排斥",
                AttackResultType.Pull => "牵引",
                AttackResultType.DirectDamage => "直击",
                _ => !string.IsNullOrWhiteSpace(effect.DisplayText) ? effect.DisplayText : effect.ResultType.ToString(),
            };
            return $"<result>{label}</result>";
        }

        private static string ResolvePayloadStatusEffectLabel(SpellPayloadEffectNode effect)
        {
            if (effect == null)
            {
                return string.Empty;
            }

            if (effect.ResultEffects.HasStatusApplications && !effect.ResultEffects.HasControl)
            {
                if (!string.IsNullOrWhiteSpace(effect.DisplayText))
                {
                    return effect.DisplayText;
                }

                string statusLabel = ResolveStatusApplicationsLabel(effect.ResultEffects.statusApplications);
                if (!string.IsNullOrEmpty(statusLabel))
                {
                    return statusLabel;
                }
            }

            return !string.IsNullOrWhiteSpace(effect.DisplayText) ? effect.DisplayText : "控制";
        }

        private static string ResolveStatusApplicationsLabel(IReadOnlyList<SpellStatusApplication> applications)
        {
            if (applications == null || applications.Count <= 0)
            {
                return string.Empty;
            }

            List<string> labels = new();
            for (int i = 0; i < applications.Count; i++)
            {
                SpellStatusSlot slot = applications[i].slot;
                if (slot == SpellStatusSlot.None)
                {
                    continue;
                }

                string label = ResolveStatusSlotLabel(slot);
                if (!string.IsNullOrEmpty(label) && !labels.Contains(label))
                {
                    labels.Add(label);
                }
            }

            return JoinEffects(labels, "、");
        }

        private static string ResolveStatusSlotLabel(SpellStatusSlot slot)
        {
            return slot switch
            {
                SpellStatusSlot.Ignite => "点燃",
                SpellStatusSlot.Freeze => "冻结",
                SpellStatusSlot.Wet => "潮湿",
                SpellStatusSlot.Corrosion => "腐蚀",
                SpellStatusSlot.Disable => "失能",
                SpellStatusSlot.Bind => "绑缚",
                SpellStatusSlot.Mark => "标记",
                SpellStatusSlot.Polymorph => "变形",
                SpellStatusSlot.PuppetMark => "傀儡标记",
                _ => string.Empty,
            };
        }

        private static string ResolveTriggerLabel(SpellTriggerType triggerType)
        {
            return triggerType switch
            {
                SpellTriggerType.OnTimer => "计时",
                SpellTriggerType.OnExpire => "消失",
                SpellTriggerType.OnKill => "击杀",
                SpellTriggerType.OnDistance => "飞行距离",
                SpellTriggerType.OnProximity => "接近目标",
                _ => "命中",
            };
        }

        private static string ResolveTriggerParameterLabel(SpellPayloadBlock payload)
        {
            if (payload == null || payload.ParameterKind == SpellTriggerParameterKind.None)
            {
                return string.Empty;
            }

            string value = $"<value>{FormatSeconds(payload.ParameterValue)}</value>";
            return payload.ParameterKind switch
            {
                SpellTriggerParameterKind.TimeSeconds => $"{value}秒",
                SpellTriggerParameterKind.Distance => $"{value}距离",
                SpellTriggerParameterKind.Radius => $"{value}范围内",
                _ => value,
            };
        }

        private static string ResolveCastPatternLabel(SpellCastPattern castPattern)
        {
            return castPattern switch
            {
                SpellCastPattern.Sequential => "顺序",
                SpellCastPattern.Fork => "分叉",
                SpellCastPattern.Orbit => "环绕",
                _ => "同轮",
            };
        }

        private static string ResolveModifierScopeLabel(SpellModifierScope scope, int targetCount)
        {
            return scope switch
            {
                SpellModifierScope.NextToken => "下一个词元",
                SpellModifierScope.NextN => $"后<value>{ToChineseNumber(Mathf.Max(1, targetCount))}</value>个词元",
                SpellModifierScope.CurrentBlock => "当前<special>CastBlock</special>",
                SpellModifierScope.CurrentPayload => "当前<special>Payload</special>",
                SpellModifierScope.GlobalProgram => "整次法术程序",
                _ => "未知作用域",
            };
        }

        private static string ResolveTokenLabel(BaseTokenData token)
        {
            string label = token != null ? token.GetResolvedDisplayText() : string.Empty;
            return string.IsNullOrWhiteSpace(label) && token != null ? token.name : label;
        }

        private static int CountFixedItems(SpellBookData spellBook)
        {
            int count = 0;
            IReadOnlyList<PlaceableTokenData> fixedItems = spellBook != null ? spellBook.FixedCastItems : null;
            if (fixedItems == null)
            {
                return count;
            }

            for (int i = 0; i < fixedItems.Count; i++)
            {
                if (fixedItems[i] != null)
                {
                    count++;
                }
            }

            return count;
        }

        private static void AddSentenceIfNotEmpty(List<string> sentences, string sentence)
        {
            if (!string.IsNullOrEmpty(sentence))
            {
                sentences.Add(sentence);
            }
        }

        private static string JoinSentences(IReadOnlyList<string> sentences)
        {
            if (sentences == null || sentences.Count <= 0)
            {
                return string.Empty;
            }

            StringBuilder builder = new();
            for (int i = 0; i < sentences.Count; i++)
            {
                builder.Append(sentences[i]);
            }

            return builder.ToString();
        }

        private static string JoinLimited(IReadOnlyList<string> entries, string separator, int maxEntries)
        {
            if (entries == null || entries.Count <= 0)
            {
                return string.Empty;
            }

            int limit = Mathf.Clamp(maxEntries, 1, entries.Count);
            StringBuilder builder = new();
            string resolvedSeparator = separator ?? string.Empty;
            for (int i = 0; i < limit; i++)
            {
                if (i > 0)
                {
                    builder.Append(resolvedSeparator);
                }

                builder.Append(entries[i]);
            }

            if (entries.Count > limit)
            {
                builder.Append("等");
            }

            return builder.ToString();
        }

        private static bool ContainsExact(IReadOnlyList<string> entries, string value)
        {
            if (entries == null)
            {
                return false;
            }

            for (int i = 0; i < entries.Count; i++)
            {
                if (string.Equals(entries[i], value, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static string FormatSeconds(float seconds)
        {
            return Mathf.Max(0f, seconds).ToString("0.##", CultureInfo.InvariantCulture);
        }

        private static string BuildEffectSentence(
            SpellProjectileNode projectile,
            IReadOnlyList<PlaceableTokenData> items,
            SpellDescriptionCatalogData catalog,
            VocalithRandom random)
        {
            List<string> effects = new();
            CoreEffectPayload coreEffects = projectile.CoreEffects.GetSanitized();
            ResultEffectPayload resultEffects = projectile.ResultEffects.GetSanitized();

            AddEffectIf(effects, coreEffects.HasBurn, "Burn", catalog);
            AddEffectIf(effects, coreEffects.HasSlow, "Slow", catalog);
            AddEffectIf(effects, coreEffects.HasThunderChain, "ThunderChain", catalog);
            AddEffectIf(effects, coreEffects.HasArmoredBonus, "ArmoredBonus", catalog);
            AddEffectIf(effects, coreEffects.HasPiercingSuppression, "LightPierce", catalog);
            AddEffectIf(effects, coreEffects.HasWindPressure, "WindPressure", catalog);
            AddEffectIf(effects, coreEffects.HasStatusApplications || resultEffects.HasStatusApplications, "StatusSlot", catalog);
            AddEffectIf(effects, projectile.ResultType == AttackResultType.Split && resultEffects.HasSplit, "Split", catalog);
            AddEffectIf(effects, projectile.ResultType == AttackResultType.StatusEffect && resultEffects.HasControl, "Control", catalog);
            AddEffectIf(effects, projectile.ResultType == AttackResultType.Drain, "Drain", catalog);
            AddEffectIf(effects, projectile.ResultType == AttackResultType.Shield, "Shield", catalog);
            AddEffectIf(effects, projectile.ResultType == AttackResultType.Leave && resultEffects.HasLingeringArea, "Leave", catalog);
            AddEffectIf(effects, (projectile.ResultType == AttackResultType.Push || projectile.ResultType == AttackResultType.Pull) && resultEffects.HasDisplacement, "Displacement", catalog);

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

        private static string BuildValueBindingSentence(
            IReadOnlyList<PlaceableTokenData> items,
            SpellDescriptionCatalogData catalog,
            VocalithRandom random)
        {
            List<string> bindings = ResolveValueBindingPhrases(items, catalog);
            if (bindings.Count <= 0)
            {
                return string.Empty;
            }

            Dictionary<string, string> replacements = new()
            {
                { BindingsToken, JoinEffects(bindings, catalog.valueBindingSeparator) },
            };
            return FormatTemplate(Pick(catalog.valueBindingSentenceTemplates, random), catalog, replacements);
        }

        private static List<string> ResolveValueBindingPhrases(IReadOnlyList<PlaceableTokenData> items, SpellDescriptionCatalogData catalog)
        {
            List<BaseTokenData> tokens = ExpandDescriptionTokens(items);
            List<string> phrases = new();
            CoreTokenData coreToken = null;
            BehaviorTokenData behaviorToken = null;
            ResultTokenData resultToken = null;
            BaseTokenData pendingConsumer = null;
            bool hasExplicitResult = false;

            for (int i = 0; i < tokens.Count; i++)
            {
                BaseTokenData token = tokens[i];
                if (token == null)
                {
                    continue;
                }

                switch (token.TokenType)
                {
                    case TokenType.Core:
                        if (coreToken == null && token is CoreTokenData candidateCore)
                        {
                            coreToken = candidateCore;
                        }

                        break;

                    case TokenType.Behavior:
                        if (coreToken != null &&
                            !hasExplicitResult &&
                            behaviorToken == null &&
                            token is BehaviorTokenData candidateBehavior)
                        {
                            behaviorToken = candidateBehavior;
                            pendingConsumer = SpellValueParameterUtility.CanConsumeValue(candidateBehavior)
                                ? candidateBehavior
                                : null;
                        }

                        break;

                    case TokenType.Result:
                        if (coreToken != null &&
                            resultToken == null &&
                            token is ResultTokenData candidateResult)
                        {
                            resultToken = candidateResult;
                            hasExplicitResult = true;
                            pendingConsumer = SpellValueParameterUtility.CanConsumeValue(candidateResult)
                                ? candidateResult
                                : null;
                        }

                        break;

                    case TokenType.Trigger:
                        if (token is TriggerTokenData triggerToken && triggerToken.ConsumesValueAsTriggerParameter)
                        {
                            pendingConsumer = triggerToken;
                        }

                        break;

                    case TokenType.Value:
                        if (pendingConsumer != null && token is ValueTokenData valueToken)
                        {
                            string phrase = CreateValueBindingPhrase(pendingConsumer, valueToken, catalog);
                            if (!string.IsNullOrEmpty(phrase))
                            {
                                phrases.Add(phrase);
                            }

                            pendingConsumer = null;
                        }

                        break;
                }
            }

            return phrases;
        }

        private static List<BaseTokenData> ExpandDescriptionTokens(IReadOnlyList<PlaceableTokenData> items)
        {
            List<BaseTokenData> tokens = new();
            if (items == null)
            {
                return tokens;
            }

            for (int i = 0; i < items.Count; i++)
            {
                items[i]?.AppendCompileTokens(tokens);
            }

            return tokens;
        }

        private static string CreateValueBindingPhrase(
            BaseTokenData consumer,
            ValueTokenData valueToken,
            SpellDescriptionCatalogData catalog)
        {
            SpellValueParameterKind parameterKind = ResolveConsumerParameterKind(consumer);
            if (parameterKind == SpellValueParameterKind.None)
            {
                return string.Empty;
            }

            string tokenType = ResolveConsumerTokenTypeName(consumer);
            SpellDescriptionValueBindingEntry entry = FindValueBindingEntry(tokenType, parameterKind, catalog);
            string template = entry != null
                ? entry.phrase
                : BuildFallbackValueBindingTemplate(consumer);
            Dictionary<string, string> replacements = new()
            {
                { ValueTextToken, ResolveValueLabel(valueToken) },
                { ConsumerToken, ResolveConsumerLabel(consumer) },
            };
            return FormatTemplate(template, catalog, replacements);
        }

        private static SpellValueParameterKind ResolveConsumerParameterKind(BaseTokenData consumer)
        {
            if (consumer is BehaviorTokenData behaviorToken)
            {
                return SpellValueParameterUtility.CanConsumeValue(behaviorToken)
                    ? behaviorToken.ValueParameterKind
                    : SpellValueParameterKind.None;
            }

            if (consumer is ResultTokenData resultToken)
            {
                return SpellValueParameterUtility.CanConsumeValue(resultToken)
                    ? resultToken.ValueParameterKind
                    : SpellValueParameterKind.None;
            }

            if (consumer is TriggerTokenData triggerToken)
            {
                return triggerToken.ConsumesValueAsTriggerParameter
                    ? SpellValueParameterKind.TriggerParameter
                    : SpellValueParameterKind.None;
            }

            return SpellValueParameterKind.None;
        }

        private static string ResolveConsumerTokenTypeName(BaseTokenData consumer)
        {
            if (consumer is BehaviorTokenData behaviorToken)
            {
                return behaviorToken.BehaviorType.ToString();
            }

            if (consumer is ResultTokenData resultToken)
            {
                return resultToken.ResultType.ToString();
            }

            if (consumer is TriggerTokenData triggerToken)
            {
                return triggerToken.TriggerType.ToString();
            }

            return string.Empty;
        }

        private static string ResolveConsumerLabel(BaseTokenData consumer)
        {
            if (consumer is BehaviorTokenData behaviorToken)
            {
                return behaviorToken.BehaviorType switch
                {
                    AttackBehaviorType.Spread => "散射",
                    AttackBehaviorType.Bounce => "弹射",
                    AttackBehaviorType.Chain => "链接",
                    AttackBehaviorType.Pierce => "穿透",
                    AttackBehaviorType.Homing => "追踪",
                    AttackBehaviorType.Stasis => "停滞",
                    AttackBehaviorType.Rush => "加速",
                    AttackBehaviorType.Slow => "减速",
                    AttackBehaviorType.Snake => "蛇形",
                    AttackBehaviorType.Wander => "游移",
                    AttackBehaviorType.Split => "飞行分裂",
                    AttackBehaviorType.Spin => "环绕",
                    _ => consumer.GetResolvedDisplayText(),
                };
            }

            if (consumer is ResultTokenData resultToken)
            {
                return resultToken.ResultType switch
                {
                    AttackResultType.Explosion => "爆炸",
                    AttackResultType.StatusEffect => "控制",
                    AttackResultType.Split => "分裂",
                    AttackResultType.Healing => "治疗",
                    AttackResultType.Drain => "汲取",
                    AttackResultType.Shield => "护盾",
                    AttackResultType.Leave => "残留",
                    AttackResultType.Push => "排斥",
                    AttackResultType.Pull => "牵引",
                    AttackResultType.DirectDamage => "直击",
                    _ => consumer.GetResolvedDisplayText(),
                };
            }

            if (consumer is TriggerTokenData triggerToken)
            {
                return ResolveTriggerLabel(triggerToken.TriggerType);
            }

            return consumer != null ? consumer.GetResolvedDisplayText() : string.Empty;
        }

        private static string ResolveValueLabel(ValueTokenData valueToken)
        {
            if (valueToken == null)
            {
                return string.Empty;
            }

            string displayText = valueToken.GetResolvedDisplayText();
            if (!string.IsNullOrWhiteSpace(displayText))
            {
                return displayText;
            }

            return Mathf.Approximately(valueToken.NumericValue, Mathf.Round(valueToken.NumericValue))
                ? ToChineseNumber(Mathf.RoundToInt(valueToken.NumericValue))
                : valueToken.NumericValue.ToString("0.##");
        }

        private static SpellDescriptionValueBindingEntry FindValueBindingEntry(
            string tokenType,
            SpellValueParameterKind parameterKind,
            SpellDescriptionCatalogData catalog)
        {
            if (catalog?.valueBindings == null)
            {
                return null;
            }

            for (int i = 0; i < catalog.valueBindings.Count; i++)
            {
                SpellDescriptionValueBindingEntry entry = catalog.valueBindings[i];
                if (entry != null &&
                    entry.parameterKind == parameterKind &&
                    string.Equals(entry.tokenType, tokenType, StringComparison.OrdinalIgnoreCase))
                {
                    return entry;
                }
            }

            return null;
        }

        private static string BuildFallbackValueBindingTemplate(BaseTokenData consumer)
        {
            if (consumer is TriggerTokenData)
            {
                return "<value>{value}</value>归入<special>{consumer}</special>参数";
            }

            return consumer is BehaviorTokenData
                ? "<value>{value}</value>归入<behavior>{consumer}</behavior>"
                : "<value>{value}</value>归入<result>{consumer}</result>";
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
