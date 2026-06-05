using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Vocalith.UI
{
    [DisallowMultipleComponent]
    public sealed class UIButtonPressScaleAutoInstaller : MonoBehaviour
    {
        private readonly List<Button> buttons = new();
        private Canvas rootCanvas;

        public void SetRoot(Canvas canvas)
        {
            rootCanvas = canvas;
            InstallFeedbackComponents();
        }

        private void Update()
        {
            InstallFeedbackComponents();
        }

        private void InstallFeedbackComponents()
        {
            if (rootCanvas == null)
            {
                return;
            }

            buttons.Clear();
            rootCanvas.GetComponentsInChildren(true, buttons);
            for (int i = 0; i < buttons.Count; i++)
            {
                Button button = buttons[i];
                if (button == null || button.GetComponent<UIButtonPressScaleFeedback>() != null)
                {
                    continue;
                }

                button.gameObject.AddComponent<UIButtonPressScaleFeedback>();
            }
        }
    }
}
