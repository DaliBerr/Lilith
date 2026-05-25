using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Kernel.UI
{
    /// <summary>
    /// 启动菜单按钮 hover 反馈，只负责背景显隐和 TMP 文本颜色。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class StartUpButtonHoverFeedback : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        private Image backgroundImage;
        private TMP_Text label;
        private Color defaultTextColor;
        private Color hoverTextColor;

        public void Configure(Image background, TMP_Text text, Color defaultText, Color hoverText)
        {
            backgroundImage = background;
            label = text;
            defaultTextColor = defaultText;
            hoverTextColor = hoverText;
            ApplyDefaultState();
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            ApplyHoverState();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            ApplyDefaultState();
        }

        public void ApplyDefaultState()
        {
            if (backgroundImage != null)
            {
                Color color = Color.white;
                color.a = 0f;
                backgroundImage.color = color;
            }

            if (label != null)
            {
                label.color = defaultTextColor;
            }
        }

        private void ApplyHoverState()
        {
            if (backgroundImage != null)
            {
                backgroundImage.color = Color.white;
            }

            if (label != null)
            {
                label.color = hoverTextColor;
            }
        }
    }
}
