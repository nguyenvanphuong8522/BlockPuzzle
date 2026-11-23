using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Survivor
{
    // One upgrade card inside LevelUpPopup.
    public sealed class UpgradeCardView : MonoBehaviour
    {
        [SerializeField] Image stripe;
        [SerializeField] TextMeshProUGUI keyHint;
        [SerializeField] TextMeshProUGUI title;
        [SerializeField] TextMeshProUGUI badge;
        [SerializeField] TextMeshProUGUI description;
        [SerializeField] Outline highlight;
        [SerializeField] TapButton button;
        [SerializeField] float highlightScale = 1.06f;

        public void Bind(UpgradeOption option, int index, Action onPick)
        {
            stripe.color = option.Tint;
            keyHint.text = $"[{index + 1}]";
            title.text = option.Title;
            title.color = option.Tint;
            badge.text = option.Badge;
            description.text = option.Description;
            button.OnPressed = onPick;
        }

        public void SetHighlighted(bool on)
        {
            highlight.enabled = on;
            transform.localScale = Vector3.one * (on ? highlightScale : 1f);
        }
    }
}
