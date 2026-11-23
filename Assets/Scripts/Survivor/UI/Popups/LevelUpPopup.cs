using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace Survivor
{
    public sealed class LevelUpData
    {
        public string Title;
        public IReadOnlyList<UpgradeOption> Options;
        public Action<int> OnPick;
    }

    // Level-up and chest reward screen: up to 3 upgrade cards.
    public sealed class LevelUpPopup : BasePopup
    {
        [SerializeField] TextMeshProUGUI title;
        [SerializeField] UpgradeCardView[] cards;
        [Tooltip("Offset between cards. (0, -340) stacks them vertically (portrait); (480, 0) puts them side by side.")]
        [SerializeField] Vector2 cardStep = new Vector2(0f, -340f);
        [Tooltip("Position of the middle of the card group.")]
        [SerializeField] Vector2 cardsCenter = new Vector2(0f, -80f);

        protected override void OnShow(object data)
        {
            var d = (LevelUpData)data;
            title.text = d.Title;
            var count = Mathf.Min(d.Options.Count, cards.Length);
            var start = cardsCenter - cardStep * ((count - 1) / 2f);
            for (var i = 0; i < cards.Length; i++)
            {
                var visible = i < count;
                cards[i].gameObject.SetActive(visible);
                if (!visible) continue;
                var index = i;
                cards[i].Bind(d.Options[i], i, () => d.OnPick(index));
                ((RectTransform)cards[i].transform).anchoredPosition = start + cardStep * i;
            }
            Highlight(0);
        }

        // Gamepad / arrow-key selection.
        public void Highlight(int index)
        {
            for (var i = 0; i < cards.Length; i++) cards[i].SetHighlighted(i == index);
        }
    }
}
