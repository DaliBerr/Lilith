using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Linq;

namespace Vocalith.Localization
{
    public static class LocalizedJsonUtility
    {
        /// <summary>
        /// 解析JSON并应用本地化：先外置补丁(domain+id)，再同文件languageData。
        /// </summary>
        /// <param name="jsonText">原始JSON文本。</param>
        /// <param name="domain">域名，如 "BuildingDef"。</param>
        /// <param name="idField">id字段名，默认 "id"。</param>
        /// <param name="rootPatchId">根对象无 id 时可显式指定的补丁 id。</param>
        /// <returns>应用本地化后的JObject；若缺少id则仍返回原root。</returns>
        public static JObject ParseAndLocalize(string jsonText, string domain, string idField = "id", string rootPatchId = null)
        {
            var root = JObject.Parse(jsonText);
            ApplyObjectLocalization(root, domain, idField, rootPatchId);

            List<JObject> descendants = root.Descendants().OfType<JObject>().ToList();
            foreach (JObject child in descendants)
            {
                ApplyObjectLocalization(child, domain, idField, null);
            }

            return root;
        }

        /// <summary>
        /// 将JSON文本按指定类型反序列化（会先应用本地化）。
        /// </summary>
        /// <typeparam name="T">目标类型。</typeparam>
        /// <param name="jsonText">原始JSON文本。</param>
        /// <param name="domain">域名。</param>
        /// <param name="settings">Json设置。</param>
        /// <param name="idField">id字段名。</param>
        /// <param name="rootPatchId">根对象无 id 时可显式指定的补丁 id。</param>
        /// <returns>反序列化后的对象。</returns>
        public static T DeserializeLocalized<T>(
            string jsonText,
            string domain,
            JsonSerializerSettings settings,
            string idField = "id",
            string rootPatchId = null)
        {
            var root = ParseAndLocalize(jsonText, domain, idField, rootPatchId);
            return root.ToObject<T>(JsonSerializer.Create(settings));
        }

        static void ApplyObjectLocalization(JObject target, string domain, string idField, string fallbackId)
        {
            if (target == null)
                return;

            string id = target.Value<string>(idField);
            if (string.IsNullOrWhiteSpace(id))
            {
                id = fallbackId;
            }

            if (!string.IsNullOrWhiteSpace(id))
            {
                LocalizationManager.TryApplyExternalJsonPatch(domain, id, target);
            }

            LocalizationManager.TryApplyLanguageData(target);
        }
    }
}
