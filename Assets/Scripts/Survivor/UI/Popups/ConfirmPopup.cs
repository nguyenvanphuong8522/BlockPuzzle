using System;
using TMPro;
using UnityEngine;

namespace Survivor
{
    public sealed class ConfirmData
    {
        public string Message;   // already translated
        public Action OnYes;
        public Action OnNo;
    }

    // Yes / No question (e.g. "Quit to Home?"). Closes itself, then runs the answer.
    public sealed class ConfirmPopup : BasePopup
    {
        [SerializeField] TextMeshProUGUI message;
        [SerializeField] TapButton yesButton;
        [SerializeField] TapButton noButton;

        ConfirmData data;

        protected override void OnShow(object d)
        {
            data = (ConfirmData)d;
            message.text = data.Message;
            yesButton.OnPressed = () => Answer(data.OnYes);
            noButton.OnPressed = () => Answer(data.OnNo);
        }

        void Answer(Action action)
        {
            Close();
            action?.Invoke();
        }

        // Keyboard / gamepad: Submit = yes, Cancel = no.
        public void HandleInput(bool submit, bool cancel)
        {
            if (!IsShown) return;
            if (submit) Answer(data?.OnYes);
            else if (cancel) Answer(data?.OnNo);
        }
    }
}
