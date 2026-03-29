using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

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
        /// <returns>应用本地化后的JObject；若缺少id则仍返回原root。</returns>
        public static JObject ParseAndLocalize(string jsonText, string domain, string idField = "id")
        {
            var root = JObject.Parse(jsonText);

            var id = root.Value<string>(idField);
            if (!string.IsNullOrEmpty(id))
            {
                LocalizationManager.TryApplyExternalJsonPatch(domain, id, root);
            }

            // 兼容同文件 languageData（你不想要也可以删掉这行）
            LocalizationManager.TryApplyLanguageData(root);

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
        /// <returns>反序列化后的对象。</returns>
        public static T DeserializeLocalized<T>(string jsonText, string domain, JsonSerializerSettings settings, string idField = "id")
        {
            var root = ParseAndLocalize(jsonText, domain, idField);
            return root.ToObject<T>(JsonSerializer.Create(settings));
        }
    }
    }