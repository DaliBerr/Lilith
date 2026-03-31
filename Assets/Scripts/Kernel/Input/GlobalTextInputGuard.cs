using TMPro;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Kernel.UI
{
    public static class GlobalTextInputGuard
    {
        public static bool IsTextInputFocused => TryGetFocusedTextInput(out _, out _);

        public static bool ShouldBlockExternalInput => IsTextInputFocused;

        /// <summary>
        /// summary: 返回当前 EventSystem 是否正聚焦在可编辑文本输入框上。
        /// param: tmpInput 当前聚焦的 TMP 输入框
        /// param: legacyInput 当前聚焦的 legacy 输入框
        /// returns: 存在处于编辑态的输入框返回 true
        /// </summary>
        public static bool TryGetFocusedTextInput(out TMP_InputField tmpInput, out InputField legacyInput)
        {
            tmpInput = null;
            legacyInput = null;

            var eventSystem = EventSystem.current;
            if (eventSystem == null || eventSystem.currentSelectedGameObject == null)
            {
                return false;
            }

            var selected = eventSystem.currentSelectedGameObject;
            tmpInput = selected.GetComponent<TMP_InputField>() ?? selected.GetComponentInParent<TMP_InputField>();
            if (tmpInput != null && tmpInput.isActiveAndEnabled && tmpInput.isFocused)
            {
                return true;
            }

            legacyInput = selected.GetComponent<InputField>() ?? selected.GetComponentInParent<InputField>();
            return legacyInput != null && legacyInput.isActiveAndEnabled && legacyInput.isFocused;
        }
    }
}
