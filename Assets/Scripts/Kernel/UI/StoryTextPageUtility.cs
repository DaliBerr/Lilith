using TMPro;
using UnityEngine;

namespace Kernel.UI
{
    internal static class StoryTextPageUtility
    {
        private const int DefaultProbeLimit = 2000;

        public static bool DoesTextFitPage(TMP_Text textComponent, string text)
        {
            if (textComponent == null)
            {
                return true;
            }

            Vector2 pageSize = ResolveTextRectSize(textComponent);
            if (pageSize.x <= 0f || pageSize.y <= 0f)
            {
                return true;
            }

            Vector2 preferredSize = textComponent.GetPreferredValues(text ?? string.Empty, pageSize.x, float.PositiveInfinity);
            return preferredSize.y <= pageSize.y + 0.5f;
        }

        public static int EstimateTextElementCapacity(TMP_Text textComponent, int fallbackCapacity)
        {
            int fallback = Mathf.Max(1, fallbackCapacity);
            if (textComponent == null)
            {
                return fallback;
            }

            // 这里只提供动态分页的估算起点与无测量场景的 fallback，
            // 真正的分页是否溢出仍应以 DoesTextFitPage 的实际排版结果为准。
            int low = 1;
            int high = Mathf.Max(fallback, DefaultProbeLimit);
            int best = 1;

            while (low <= high)
            {
                int middle = (low + high) / 2;
                if (DoesTextFitPage(textComponent, new string('测', middle)))
                {
                    best = middle;
                    low = middle + 1;
                }
                else
                {
                    high = middle - 1;
                }
            }

            return Mathf.Max(1, best);
        }

        private static Vector2 ResolveTextRectSize(TMP_Text textComponent)
        {
            Canvas.ForceUpdateCanvases();
            RectTransform rectTransform = textComponent.rectTransform;
            return rectTransform != null ? rectTransform.rect.size : Vector2.zero;
        }
    }
}
