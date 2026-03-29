
namespace Vocalith.Localization
{
    public static class StringLocalizationExtensions
    {
        /// <summary>
        /// 翻译当前字符串（把字符串当作 key）。
        /// </summary>
        /// <param name="key">翻译键。</param>
        /// <returns>翻译结果；缺失则返回原字符串。</returns>
        public static string Translate(this string key)
        {
            return LocalizationManager.Translate(key);
        }

        /// <summary>
        /// 翻译并格式化（string.Format）。
        /// </summary>
        /// <param name="key">翻译键。</param>
        /// <param name="args">格式化参数。</param>
        /// <returns>翻译并格式化后的字符串。</returns>
        public static string Translate(this string key, params object[] args)
        {
            return LocalizationManager.TranslateFormat(key, args);
        }
    }
}
