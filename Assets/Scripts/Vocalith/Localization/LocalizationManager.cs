// LocalizationManager.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Vocalith.Logging;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceLocations;

namespace Vocalith.Localization
{
    /// <summary>
    /// 本地化管理器：提供字符串翻译表、JSON内嵌languageData覆盖、JSON外置补丁覆盖。
    /// </summary>
    public static class LocalizationManager
    {
        const string PlayerPrefsLanguageKey = "LanguageTag";

        static readonly Dictionary<string, string> _stringTable = new(StringComparer.Ordinal);

        // domain -> (id -> patch JObject)
        static readonly Dictionary<string, Dictionary<string, JObject>> _jsonPatchTable = new(StringComparer.Ordinal);

        /// <summary>
        /// 当前语言标签（例如 "zh-Hans-CN" / "en-US"）。
        /// </summary>
        public static string CurrentLanguageTag { get; private set; } = "en-US";

        /// <summary>
        /// 语言变更事件（切换语言、重载语言包后触发）。
        /// </summary>
        public static event Action OnLanguageChanged;

        /// <summary>
        /// 初始化本地化系统（加载字符串翻译表与JSON补丁）。
        /// </summary>
        /// <param name="languageTag">指定语言；为空则读取PlayerPrefs或系统语言推断。</param>
        /// <param name="stringTableLabel">字符串翻译表 Addressables 标签/组名（建议："Localization"）。</param>
        /// <param name="jsonPatchLabel">JSON补丁 Addressables 标签/组名（建议："LocalizationJson"）。</param>
        /// <param name="saveToPrefs">是否将语言写入PlayerPrefs。</param>
        /// <returns>异步任务。</returns>
        public static async Task InitializeAsync(
            string languageTag = null,
            string stringTableLabel = "Localization",
            string jsonPatchLabel = "LocalizationJson",
            bool saveToPrefs = true)
        {
            var saved = PlayerPrefs.GetString(PlayerPrefsLanguageKey, string.Empty);
            var final = !string.IsNullOrWhiteSpace(languageTag)
                ? languageTag
                : !string.IsNullOrWhiteSpace(saved)
                    ? saved
                    : GuessFromSystemLanguage();

            await SetLanguageAsync(final, stringTableLabel, jsonPatchLabel, saveToPrefs);
        }

        /// <summary>
        /// 切换语言并重载语言资源（字符串表 + JSON补丁）。
        /// </summary>
        /// <param name="languageTag">目标语言标签。</param>
        /// <param name="stringTableLabel">字符串翻译表 Addressables 标签/组名。</param>
        /// <param name="jsonPatchLabel">JSON补丁 Addressables 标签/组名。</param>
        /// <param name="saveToPrefs">是否保存到PlayerPrefs。</param>
        /// <returns>异步任务。</returns>
        public static async Task SetLanguageAsync(
            string languageTag,
            string stringTableLabel = "Localization",
            string jsonPatchLabel = "LocalizationJson",
            bool saveToPrefs = true)
        {
            CurrentLanguageTag = NormalizeLanguageTag(languageTag);

            _stringTable.Clear();
            _jsonPatchTable.Clear();

            await LoadStringTablesAsync(stringTableLabel);
            await LoadJsonPatchesAsync(jsonPatchLabel);

            if (saveToPrefs)
            {
                PlayerPrefs.SetString(PlayerPrefsLanguageKey, CurrentLanguageTag);
                PlayerPrefs.Save();
            }

            OnLanguageChanged?.Invoke();
        }

        /// <summary>
        /// 翻译一个key；缺失则返回原key。
        /// </summary>
        /// <param name="key">翻译键。</param>
        /// <returns>翻译后的字符串或原key。</returns>
        public static string Translate(string key)
        {
            if (string.IsNullOrEmpty(key))
                return string.Empty;

            // 允许 "$KEY" 的写法
            if (key.Length > 1 && key[0] == '$')
                key = key.Substring(1);

            return _stringTable.TryGetValue(key, out var v) ? v : key;
        }

        /// <summary>
        /// 翻译并进行string.Format格式化；缺失则以key作为格式源。
        /// </summary>
        /// <param name="key">翻译键。</param>
        /// <param name="args">格式化参数。</param>
        /// <returns>翻译并格式化后的字符串。</returns>
        public static string TranslateFormat(string key, params object[] args)
        {
            var fmt = Translate(key);
            try { return string.Format(fmt, args); }
            catch { return fmt; }
        }

        /// <summary>
        /// 注册/覆盖一个翻译项（运行时动态写入）。
        /// </summary>
        /// <param name="key">翻译键。</param>
        /// <param name="value">翻译值。</param>
        /// <param name="overwrite">是否允许覆盖已有值。</param>
        /// <returns>是否写入成功。</returns>
        public static bool RegisterString(string key, string value, bool overwrite = true)
        {
            if (string.IsNullOrEmpty(key)) return false;
            if (!overwrite && _stringTable.ContainsKey(key)) return false;
            _stringTable[key] = value ?? string.Empty;
            return true;
        }

        /// <summary>
        /// 将JSON根对象里的languageData按当前语言合并覆盖到根对象上（用于“定义文件内嵌翻译”）。
        /// </summary>
        /// <param name="root">JSON根对象。</param>
        /// <returns>是否应用了languageData覆盖。</returns>
        public static bool TryApplyLanguageData(JObject root)
        {
            if (root == null) return false;
            if (root["languageData"] is not JObject langDataObj) return false;

            var chosen = PickBestLanguageKey(langDataObj, CurrentLanguageTag);
            if (string.IsNullOrEmpty(chosen)) return false;

            if (langDataObj[chosen] is not JObject patchObj) return false;

            root.Merge(patchObj, new JsonMergeSettings
            {
                MergeArrayHandling = MergeArrayHandling.Replace,
                MergeNullValueHandling = MergeNullValueHandling.Ignore
            });

            // 合并完移除，避免影响后续反序列化/校验
            root.Remove("languageData");
            return true;
        }

        /// <summary>
        /// 将外置JSON补丁（domain+id）合并覆盖到root上（用于“定义与翻译分离”）。
        /// </summary>
        /// <param name="domain">域名，例如 "BuildingDef"。</param>
        /// <param name="id">定义ID，例如 "generator_small"。</param>
        /// <param name="root">目标JSON根对象。</param>
        /// <returns>是否成功应用补丁。</returns>
        public static bool TryApplyExternalJsonPatch(string domain, string id, JObject root)
        {
            if (root == null) return false;
            if (string.IsNullOrEmpty(domain) || string.IsNullOrEmpty(id)) return false;

            if (!_jsonPatchTable.TryGetValue(domain, out var byId)) return false;
            if (!byId.TryGetValue(id, out var patch) || patch == null) return false;

            root.Merge(patch, new JsonMergeSettings
            {
                MergeArrayHandling = MergeArrayHandling.Replace,
                MergeNullValueHandling = MergeNullValueHandling.Ignore
            });

            return true;
        }

        /// <summary>
        /// 加载字符串翻译表（Addressables TextAsset）。
        /// 支持两种JSON：
        /// 1) { "language":"xx", "entries": { "k":"v" } }
        /// 2) { "k":"v", ... }（视为entries）
        /// </summary>
        /// <param name="labelOrGroup">Addressables标签/组名。</param>
        /// <returns>异步任务。</returns>
        static async Task LoadStringTablesAsync(string labelOrGroup)
        {
            var assets = await LoadTextAssetsByLabelAsync(labelOrGroup);
            if (assets == null || assets.Count == 0) return;

            foreach (var ta in assets.OrderBy(a => a.name, StringComparer.Ordinal))
            {
                if (!ta) continue;
                TryMergeStringPack(ta.name, ta.text);
            }
        }

        /// <summary>
        /// 加载JSON外置补丁（Addressables TextAsset）。
        /// 建议格式：
        /// { "language":"en-US", "domain":"BuildingDef", "patches": { "id": { ... } } }
        /// </summary>
        /// <param name="labelOrGroup">Addressables标签/组名。</param>
        /// <returns>异步任务。</returns>
        static async Task LoadJsonPatchesAsync(string labelOrGroup)
        {
            var assets = await LoadTextAssetsByLabelAsync(labelOrGroup);
            if (assets == null || assets.Count == 0) return;

            foreach (var ta in assets.OrderBy(a => a.name, StringComparer.Ordinal))
            {
                if (!ta) continue;
                TryMergeJsonPatchPack(ta.name, ta.text);
            }
        }

        /// <summary>
        /// 统一的 Addressables TextAsset 批量加载。
        /// </summary>
        /// <param name="labelOrGroup">Addressables标签/组名。</param>
        /// <returns>TextAsset列表；失败返回空列表。</returns>
        static async Task<IList<TextAsset>> LoadTextAssetsByLabelAsync(string labelOrGroup)
        {
            AsyncOperationHandle<IList<IResourceLocation>> locHandle =
                Addressables.LoadResourceLocationsAsync(labelOrGroup, typeof(TextAsset));

            IList<IResourceLocation> locations = null;
            try { locations = await locHandle.Task; }
            catch (Exception ex)
            {
                GameDebug.LogError($"[Localization] 查询 Addressables 失败（{labelOrGroup}）：\n{ex}");
                if (locHandle.IsValid()) Addressables.Release(locHandle);
                return Array.Empty<TextAsset>();
            }

            if (locations == null || locations.Count == 0)
            {
                GameDebug.LogWarning($"[Localization] 未找到任何 TextAsset（{labelOrGroup}）。");
                if (locHandle.IsValid()) Addressables.Release(locHandle);
                return Array.Empty<TextAsset>();
            }

            AsyncOperationHandle<IList<TextAsset>> loadHandle =
                Addressables.LoadAssetsAsync<TextAsset>(locations, null, true);

            IList<TextAsset> assets = null;
            try { assets = await loadHandle.Task; }
            catch (Exception ex)
            {
                GameDebug.LogError($"[Localization] 批量加载 TextAsset 失败（{labelOrGroup}）：\n{ex}");
                if (loadHandle.IsValid()) Addressables.Release(loadHandle);
                if (locHandle.IsValid()) Addressables.Release(locHandle);
                return Array.Empty<TextAsset>();
            }

            if (loadHandle.IsValid()) Addressables.Release(loadHandle);
            if (locHandle.IsValid()) Addressables.Release(locHandle);

            return assets ?? Array.Empty<TextAsset>();
        }

        /// <summary>
        /// 合并一个字符串翻译包到_stringTable。
        /// </summary>
        /// <param name="assetName">资源名（用于日志）。</param>
        /// <param name="jsonText">JSON文本。</param>
        /// <returns>是否合并成功。</returns>
        static bool TryMergeStringPack(string assetName, string jsonText)
        {
            if (string.IsNullOrWhiteSpace(jsonText)) return false;

            try
            {
                var token = JToken.Parse(jsonText);
                if (token is not JObject obj) return false;

                // 格式1：标准pack
                if (obj["entries"] is JObject entriesObj)
                {
                    var lang = NormalizeLanguageTag(obj.Value<string>("language"));
                    if (!string.IsNullOrEmpty(lang) && !IsLanguageMatch(lang, CurrentLanguageTag))
                        return false;

                    foreach (var p in entriesObj.Properties())
                        _stringTable[p.Name] = p.Value?.ToString() ?? string.Empty;

                    return true;
                }

                // 格式2：直接视为entries（只收string值）
                foreach (var p in obj.Properties())
                {
                    if (p.Value.Type == JTokenType.String)
                        _stringTable[p.Name] = p.Value.ToString();
                }

                return true;
            }
            catch (Exception ex)
            {
                GameDebug.LogWarning($"[Localization] 解析字符串包失败（{assetName}）：{ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 合并一个JSON补丁包到_jsonPatchTable。
        /// </summary>
        /// <param name="assetName">资源名（用于日志）。</param>
        /// <param name="jsonText">JSON文本。</param>
        /// <returns>是否合并成功。</returns>
        static bool TryMergeJsonPatchPack(string assetName, string jsonText)
        {
            if (string.IsNullOrWhiteSpace(jsonText)) return false;

            try
            {
                var root = JToken.Parse(jsonText) as JObject;
                if (root == null) return false;

                var lang = NormalizeLanguageTag(root.Value<string>("language"));
                if (!string.IsNullOrEmpty(lang) && !IsLanguageMatch(lang, CurrentLanguageTag))
                    return false;

                var domain = root.Value<string>("domain") ?? "Default";
                if (root["patches"] is not JObject patchesObj) return false;

                if (!_jsonPatchTable.TryGetValue(domain, out var byId))
                {
                    byId = new Dictionary<string, JObject>(StringComparer.Ordinal);
                    _jsonPatchTable[domain] = byId;
                }

                foreach (var prop in patchesObj.Properties())
                {
                    if (prop.Value is JObject patchObj)
                        byId[prop.Name] = patchObj;
                }

                return true;
            }
            catch (Exception ex)
            {
                GameDebug.LogWarning($"[Localization] 解析JSON补丁失败（{assetName}）：{ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 从系统语言粗略推断语言标签。
        /// </summary>
        /// <param name="none">无。</param>
        /// <returns>语言标签字符串。</returns>
        static string GuessFromSystemLanguage()
        {
            return Application.systemLanguage switch
            {
                SystemLanguage.ChineseSimplified => "zh-Hans-CN",
                SystemLanguage.ChineseTraditional => "zh-Hant",
                SystemLanguage.Japanese => "ja-JP",
                SystemLanguage.Korean => "ko-KR",
                SystemLanguage.French => "fr-FR",
                SystemLanguage.German => "de-DE",
                SystemLanguage.Spanish => "es-ES",
                _ => "en-US"
            };
        }

        /// <summary>
        /// 规范化语言标签（空->空，'_'->'-'，Trim）。
        /// </summary>
        /// <param name="tag">输入标签。</param>
        /// <returns>规范化标签。</returns>
        static string NormalizeLanguageTag(string tag)
        {
            if (string.IsNullOrWhiteSpace(tag)) return string.Empty;
            return tag.Trim().Replace('_', '-');
        }

        /// <summary>
        /// languageData中选择最匹配语言键：精确->逐级降级->default。
        /// </summary>
        /// <param name="languageData">languageData对象。</param>
        /// <param name="currentTag">当前语言标签。</param>
        /// <returns>匹配到的key；失败返回null。</returns>
        static string PickBestLanguageKey(JObject languageData, string currentTag)
        {
            if (languageData == null) return null;

            var tag = NormalizeLanguageTag(currentTag);
            if (string.IsNullOrEmpty(tag)) tag = "en-US";

            // 1) 精确
            if (languageData.Property(tag) != null) return tag;

            // 2) 降级：zh-Hans-CN -> zh-Hans -> zh
            var parts = tag.Split('-', StringSplitOptions.RemoveEmptyEntries);
            for (int i = parts.Length - 1; i >= 1; i--)
            {
                var candidate = string.Join("-", parts.Take(i));
                if (languageData.Property(candidate) != null) return candidate;
            }

            // 3) default
            if (languageData.Property("default") != null) return "default";

            return null;
        }

        /// <summary>
        /// 判断语言是否匹配：允许包语言为更短前缀（例如 pack="zh" 匹配 target="zh-Hans-CN"）。
        /// </summary>
        /// <param name="packLang">包语言。</param>
        /// <param name="targetLang">目标语言。</param>
        /// <returns>是否匹配。</returns>
        static bool IsLanguageMatch(string packLang, string targetLang)
        {
            packLang = NormalizeLanguageTag(packLang);
            targetLang = NormalizeLanguageTag(targetLang);

            if (string.IsNullOrEmpty(packLang)) return true; // 未声明语言视为通用
            if (packLang == targetLang) return true;
            if (targetLang.StartsWith(packLang + "-", StringComparison.Ordinal)) return true;
            return false;
        }
    }
}
