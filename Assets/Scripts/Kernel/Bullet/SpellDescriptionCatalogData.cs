using System;
using System.Collections.Generic;
using UnityEngine;

namespace Kernel.Bullet
{
    [Serializable]
    public sealed class SpellDescriptionCatalogData
    {
        public SpellDescriptionHighlightColors colors = new();
        public List<string> emptySpellPrompts = new();
        public List<string> mainSentenceTemplates = new();
        public List<SpellDescriptionCoreEntry> coreLabels = new();
        public List<SpellDescriptionBehaviorEntry> behaviorPhrases = new();
        public List<SpellDescriptionResultEntry> resultPhrases = new();
        public List<SpellDescriptionSpecialEffectEntry> specialEffects = new();
        public List<SpellDescriptionValueBindingEntry> valueBindings = new();
        public string effectSeparator = "、";
        public List<string> specialSentenceTemplates = new();
        public List<string> manyItemSpecialSentenceTemplates = new();
        public string valueBindingSeparator = "、";
        public List<string> valueBindingSentenceTemplates = new();
        public string structureSeparator = "；";
        public List<string> structureSentenceTemplates = new();
        public List<string> spellBookTraitSentenceTemplates = new();

        public static bool TryDeserializeJson(string jsonText, out SpellDescriptionCatalogData catalog, out string errorMessage)
        {
            catalog = null;
            errorMessage = string.Empty;
            if (string.IsNullOrWhiteSpace(jsonText))
            {
                errorMessage = "Spell description catalog json is empty.";
                return false;
            }

            try
            {
                SpellDescriptionCatalogData raw = JsonUtility.FromJson<SpellDescriptionCatalogData>(jsonText);
                catalog = Sanitize(raw);
                return true;
            }
            catch (Exception exception)
            {
                errorMessage = exception.Message;
                return false;
            }
        }

        public static SpellDescriptionCatalogData Sanitize(SpellDescriptionCatalogData raw)
        {
            SpellDescriptionCatalogData sanitized = CreateDefault();
            if (raw == null)
            {
                return sanitized;
            }

            sanitized.colors = SpellDescriptionHighlightColors.Merge(sanitized.colors, raw.colors);
            ReplaceIfNotEmpty(sanitized.emptySpellPrompts, raw.emptySpellPrompts);
            ReplaceIfNotEmpty(sanitized.mainSentenceTemplates, raw.mainSentenceTemplates);
            MergeCoreEntries(sanitized.coreLabels, raw.coreLabels);
            MergeBehaviorEntries(sanitized.behaviorPhrases, raw.behaviorPhrases);
            MergeResultEntries(sanitized.resultPhrases, raw.resultPhrases);
            MergeSpecialEffectEntries(sanitized.specialEffects, raw.specialEffects);
            MergeValueBindingEntries(sanitized.valueBindings, raw.valueBindings);
            if (!string.IsNullOrEmpty(raw.effectSeparator))
            {
                sanitized.effectSeparator = raw.effectSeparator;
            }

            ReplaceIfNotEmpty(sanitized.specialSentenceTemplates, raw.specialSentenceTemplates);
            ReplaceIfNotEmpty(sanitized.manyItemSpecialSentenceTemplates, raw.manyItemSpecialSentenceTemplates);
            if (!string.IsNullOrEmpty(raw.valueBindingSeparator))
            {
                sanitized.valueBindingSeparator = raw.valueBindingSeparator;
            }

            ReplaceIfNotEmpty(sanitized.valueBindingSentenceTemplates, raw.valueBindingSentenceTemplates);
            if (!string.IsNullOrEmpty(raw.structureSeparator))
            {
                sanitized.structureSeparator = raw.structureSeparator;
            }

            ReplaceIfNotEmpty(sanitized.structureSentenceTemplates, raw.structureSentenceTemplates);
            ReplaceIfNotEmpty(sanitized.spellBookTraitSentenceTemplates, raw.spellBookTraitSentenceTemplates);
            return sanitized;
        }

        public static SpellDescriptionCatalogData CreateDefault()
        {
            return new SpellDescriptionCatalogData
            {
                colors = SpellDescriptionHighlightColors.CreateDefault(),
                emptySpellPrompts = new List<string>
                {
                    "放入<core>核心</core>词，法术才会成形。",
                    "Spell Book 还缺少<core>核心</core>词。",
                    "先安放<core>核心</core>，咒文才有源头。",
                },
                mainSentenceTemplates = new List<string>
                {
                    "{core}咒沿符轨成形，{behavior}，命中时{result}。",
                    "{core}势被压入咒轨，{behavior}后{result}。",
                    "{core}在掌心收束，{behavior}，随后{result}。",
                },
                coreLabels = new List<SpellDescriptionCoreEntry>
                {
                    new() { coreType = "Fire", label = "火" },
                    new() { coreType = "Ice", label = "冰" },
                    new() { coreType = "Thunder", label = "雷" },
                    new() { coreType = "Edge", label = "刃" },
                    new() { coreType = "Light", label = "光" },
                    new() { coreType = "Shadow", label = "影" },
                    new() { coreType = "Toxin", label = "毒" },
                },
                behaviorPhrases = new List<SpellDescriptionBehaviorEntry>
                {
                    new()
                    {
                        behaviorType = "Straight",
                        phraseTemplates = new List<string>
                        {
                            "<behavior>沿直线贯出</behavior>",
                            "<behavior>笔直射出</behavior>",
                        },
                    },
                    new()
                    {
                        behaviorType = "Spread",
                        countUnit = "道",
                        phraseTemplates = new List<string>
                        {
                            "分作<value>{count}</value><behavior>散射</behavior>",
                            "铺开成<value>{count}</value><behavior>散射</behavior>弹线",
                        },
                    },
                    new()
                    {
                        behaviorType = "Bounce",
                        countUnit = "次",
                        phraseTemplates = new List<string>
                        {
                            "<behavior>弹射</behavior><value>{count}</value>",
                            "带着<value>{count}</value><behavior>弹射</behavior>余势",
                        },
                        fallbackPhraseTemplates = new List<string>
                        {
                            "<behavior>多次弹射</behavior>",
                        },
                    },
                    new()
                    {
                        behaviorType = "Chain",
                        phraseTemplates = new List<string>
                        {
                            "<behavior>链式牵引</behavior>",
                        },
                    },
                    new()
                    {
                        behaviorType = "Orbit",
                        phraseTemplates = new List<string>
                        {
                            "<behavior>环绕目标</behavior>",
                        },
                    },
                    new()
                    {
                        behaviorType = "Pierce",
                        countUnit = "次",
                        phraseTemplates = new List<string>
                        {
                            "<behavior>穿透</behavior><value>{count}</value>",
                            "带着<value>{count}</value><behavior>穿透</behavior>余势",
                        },
                        fallbackPhraseTemplates = new List<string>
                        {
                            "<behavior>连续穿透</behavior>",
                        },
                    },
                    new()
                    {
                        behaviorType = "Homing",
                        phraseTemplates = new List<string>
                        {
                            "<behavior>追踪目标</behavior>",
                            "带着<behavior>追踪</behavior>弧线逼近",
                        },
                    },
                },
                resultPhrases = new List<SpellDescriptionResultEntry>
                {
                    new()
                    {
                        resultType = "DirectDamage",
                        phraseTemplates = new List<string>
                        {
                            "造成<result>直击</result>",
                            "留下<result>直接伤害</result>",
                        },
                    },
                    new()
                    {
                        resultType = "Explosion",
                        phraseTemplates = new List<string>
                        {
                            "引发<result>爆炸</result>",
                            "炸开一圈<result>爆炸</result>余波",
                        },
                    },
                    new()
                    {
                        resultType = "StatusEffect",
                        phraseTemplates = new List<string>
                        {
                            "施加<result>控制</result>",
                            "压出<result>控制</result>印记",
                        },
                    },
                    new()
                    {
                        resultType = "SpawnChild",
                        phraseTemplates = new List<string>
                        {
                            "<result>召出子咒</result>",
                        },
                    },
                    new()
                    {
                        resultType = "Split",
                        phraseTemplates = new List<string>
                        {
                            "裂成<result>分裂</result>子弹",
                            "迸出<result>分裂</result>余弹",
                        },
                    },
                    new()
                    {
                        resultType = "Healing",
                        phraseTemplates = new List<string>
                        {
                            "化作<result>治疗</result>回流",
                            "返还<result>治疗</result>光屑",
                        },
                    },
                },
                specialEffects = new List<SpellDescriptionSpecialEffectEntry>
                {
                    new() { effect = "Burn", phrase = "<special>灼烧</special>" },
                    new() { effect = "Slow", phrase = "<special>减速</special>" },
                    new() { effect = "ThunderChain", phrase = "<special>雷链</special>" },
                    new() { effect = "ArmoredBonus", phrase = "<special>破甲</special>" },
                    new() { effect = "Split", phrase = "<special>分裂余弹</special>" },
                    new() { effect = "Control", phrase = "<special>压制</special>" },
                },
                valueBindings = new List<SpellDescriptionValueBindingEntry>
                {
                    new() { tokenType = "Spread", parameterKind = SpellValueParameterKind.Count, phrase = "<value>{value}</value>归入<behavior>{consumer}</behavior>数量" },
                    new() { tokenType = "Bounce", parameterKind = SpellValueParameterKind.Count, phrase = "<value>{value}</value>归入<behavior>{consumer}</behavior>次数" },
                    new() { tokenType = "Pierce", parameterKind = SpellValueParameterKind.Count, phrase = "<value>{value}</value>归入<behavior>{consumer}</behavior>次数" },
                    new() { tokenType = "Explosion", parameterKind = SpellValueParameterKind.Radius, phrase = "<value>{value}</value>定出<result>{consumer}</result>范围" },
                    new() { tokenType = "Explosion", parameterKind = SpellValueParameterKind.Duration, phrase = "<value>{value}</value>延后<result>{consumer}</result>爆发" },
                    new() { tokenType = "Split", parameterKind = SpellValueParameterKind.Count, phrase = "<value>{value}</value>归入<result>{consumer}</result>数量" },
                    new() { tokenType = "StatusEffect", parameterKind = SpellValueParameterKind.Count, phrase = "<value>{value}</value>归入<result>{consumer}</result>阈值" },
                    new() { tokenType = "StatusEffect", parameterKind = SpellValueParameterKind.Duration, phrase = "<value>{value}</value>拉长<result>{consumer}</result>持续" },
                    new() { tokenType = "Healing", parameterKind = SpellValueParameterKind.Radius, phrase = "<value>{value}</value>铺开<result>{consumer}</result>范围" },
                },
                effectSeparator = "、",
                specialSentenceTemplates = new List<string>
                {
                    "余波附带{effects}。",
                    "余辉短暂留下{effects}。",
                    "命中后还会留下{effects}。",
                },
                manyItemSpecialSentenceTemplates = new List<string>
                {
                    "余波附带{effects}。",
                    "多重词元让余波带上{effects}。",
                    "命中后还会留下{effects}。",
                },
                valueBindingSeparator = "、",
                valueBindingSentenceTemplates = new List<string>
                {
                    "数值词：{bindings}。",
                    "数值落点：{bindings}。",
                },
                structureSeparator = "；",
                structureSentenceTemplates = new List<string>
                {
                    "结构：{structures}。",
                    "构筑层级：{structures}。",
                },
                spellBookTraitSentenceTemplates = new List<string>
                {
                    "法术书：{spellBook}，{traits}。",
                    "执行器：{spellBook}，{traits}。",
                },
            };
        }

        private static void ReplaceIfNotEmpty(List<string> target, List<string> source)
        {
            if (target == null || source == null || source.Count <= 0)
            {
                return;
            }

            target.Clear();
            for (int i = 0; i < source.Count; i++)
            {
                if (!string.IsNullOrEmpty(source[i]))
                {
                    target.Add(source[i]);
                }
            }
        }

        private static void MergeCoreEntries(List<SpellDescriptionCoreEntry> target, List<SpellDescriptionCoreEntry> source)
        {
            if (target == null || source == null)
            {
                return;
            }

            for (int i = 0; i < source.Count; i++)
            {
                SpellDescriptionCoreEntry entry = source[i];
                if (entry == null || string.IsNullOrWhiteSpace(entry.coreType) || string.IsNullOrEmpty(entry.label))
                {
                    continue;
                }

                RemoveCoreEntry(target, entry.coreType);
                target.Add(entry);
            }
        }

        private static void MergeBehaviorEntries(List<SpellDescriptionBehaviorEntry> target, List<SpellDescriptionBehaviorEntry> source)
        {
            if (target == null || source == null)
            {
                return;
            }

            for (int i = 0; i < source.Count; i++)
            {
                SpellDescriptionBehaviorEntry entry = source[i];
                if (entry == null || string.IsNullOrWhiteSpace(entry.behaviorType) || entry.PhraseCount <= 0)
                {
                    continue;
                }

                RemoveBehaviorEntry(target, entry.behaviorType);
                target.Add(entry);
            }
        }

        private static void MergeResultEntries(List<SpellDescriptionResultEntry> target, List<SpellDescriptionResultEntry> source)
        {
            if (target == null || source == null)
            {
                return;
            }

            for (int i = 0; i < source.Count; i++)
            {
                SpellDescriptionResultEntry entry = source[i];
                if (entry == null || string.IsNullOrWhiteSpace(entry.resultType) || entry.PhraseCount <= 0)
                {
                    continue;
                }

                RemoveResultEntry(target, entry.resultType);
                target.Add(entry);
            }
        }

        private static void MergeSpecialEffectEntries(List<SpellDescriptionSpecialEffectEntry> target, List<SpellDescriptionSpecialEffectEntry> source)
        {
            if (target == null || source == null)
            {
                return;
            }

            for (int i = 0; i < source.Count; i++)
            {
                SpellDescriptionSpecialEffectEntry entry = source[i];
                if (entry == null || string.IsNullOrWhiteSpace(entry.effect) || string.IsNullOrEmpty(entry.phrase))
                {
                    continue;
                }

                RemoveSpecialEffectEntry(target, entry.effect);
                target.Add(entry);
            }
        }

        private static void MergeValueBindingEntries(List<SpellDescriptionValueBindingEntry> target, List<SpellDescriptionValueBindingEntry> source)
        {
            if (target == null || source == null)
            {
                return;
            }

            for (int i = 0; i < source.Count; i++)
            {
                SpellDescriptionValueBindingEntry entry = source[i];
                if (entry == null ||
                    string.IsNullOrWhiteSpace(entry.tokenType) ||
                    entry.parameterKind == SpellValueParameterKind.None ||
                    string.IsNullOrEmpty(entry.phrase))
                {
                    continue;
                }

                RemoveValueBindingEntry(target, entry.tokenType, entry.parameterKind);
                target.Add(entry);
            }
        }

        private static void RemoveCoreEntry(List<SpellDescriptionCoreEntry> entries, string coreType)
        {
            for (int i = entries.Count - 1; i >= 0; i--)
            {
                if (string.Equals(entries[i]?.coreType, coreType, StringComparison.OrdinalIgnoreCase))
                {
                    entries.RemoveAt(i);
                }
            }
        }

        private static void RemoveBehaviorEntry(List<SpellDescriptionBehaviorEntry> entries, string behaviorType)
        {
            for (int i = entries.Count - 1; i >= 0; i--)
            {
                if (string.Equals(entries[i]?.behaviorType, behaviorType, StringComparison.OrdinalIgnoreCase))
                {
                    entries.RemoveAt(i);
                }
            }
        }

        private static void RemoveResultEntry(List<SpellDescriptionResultEntry> entries, string resultType)
        {
            for (int i = entries.Count - 1; i >= 0; i--)
            {
                if (string.Equals(entries[i]?.resultType, resultType, StringComparison.OrdinalIgnoreCase))
                {
                    entries.RemoveAt(i);
                }
            }
        }

        private static void RemoveSpecialEffectEntry(List<SpellDescriptionSpecialEffectEntry> entries, string effect)
        {
            for (int i = entries.Count - 1; i >= 0; i--)
            {
                if (string.Equals(entries[i]?.effect, effect, StringComparison.OrdinalIgnoreCase))
                {
                    entries.RemoveAt(i);
                }
            }
        }

        private static void RemoveValueBindingEntry(List<SpellDescriptionValueBindingEntry> entries, string tokenType, SpellValueParameterKind parameterKind)
        {
            for (int i = entries.Count - 1; i >= 0; i--)
            {
                SpellDescriptionValueBindingEntry entry = entries[i];
                if (entry != null &&
                    entry.parameterKind == parameterKind &&
                    string.Equals(entry.tokenType, tokenType, StringComparison.OrdinalIgnoreCase))
                {
                    entries.RemoveAt(i);
                }
            }
        }
    }

    [Serializable]
    public sealed class SpellDescriptionHighlightColors
    {
        public string core = "#FF7A3D";
        public string behavior = "#66C7FF";
        public string result = "#FF5C7A";
        public string value = "#FFD166";
        public string special = "#B98CFF";

        public static SpellDescriptionHighlightColors CreateDefault()
        {
            return new SpellDescriptionHighlightColors();
        }

        public static SpellDescriptionHighlightColors Merge(SpellDescriptionHighlightColors fallback, SpellDescriptionHighlightColors overrideColors)
        {
            SpellDescriptionHighlightColors merged = fallback ?? CreateDefault();
            if (overrideColors == null)
            {
                return merged;
            }

            if (!string.IsNullOrWhiteSpace(overrideColors.core))
            {
                merged.core = overrideColors.core;
            }

            if (!string.IsNullOrWhiteSpace(overrideColors.behavior))
            {
                merged.behavior = overrideColors.behavior;
            }

            if (!string.IsNullOrWhiteSpace(overrideColors.result))
            {
                merged.result = overrideColors.result;
            }

            if (!string.IsNullOrWhiteSpace(overrideColors.value))
            {
                merged.value = overrideColors.value;
            }

            if (!string.IsNullOrWhiteSpace(overrideColors.special))
            {
                merged.special = overrideColors.special;
            }

            return merged;
        }
    }

    [Serializable]
    public sealed class SpellDescriptionCoreEntry
    {
        public string coreType;
        public string label;
    }

    [Serializable]
    public sealed class SpellDescriptionBehaviorEntry
    {
        public string behaviorType;
        public string countUnit;
        public List<string> phraseTemplates = new();
        public List<string> fallbackPhraseTemplates = new();

        public int PhraseCount => phraseTemplates != null ? phraseTemplates.Count : 0;
    }

    [Serializable]
    public sealed class SpellDescriptionResultEntry
    {
        public string resultType;
        public List<string> phraseTemplates = new();

        public int PhraseCount => phraseTemplates != null ? phraseTemplates.Count : 0;
    }

    [Serializable]
    public sealed class SpellDescriptionSpecialEffectEntry
    {
        public string effect;
        public string phrase;
    }

    [Serializable]
    public sealed class SpellDescriptionValueBindingEntry
    {
        public string tokenType;
        public SpellValueParameterKind parameterKind;
        public string phrase;
    }
}
