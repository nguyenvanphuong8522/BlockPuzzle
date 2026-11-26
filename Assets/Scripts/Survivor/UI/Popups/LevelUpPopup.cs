using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Survivor
{
    public enum ChoiceKind { Companion, Item, Chest }

    public sealed class LevelUpData
    {
        public string Title;
        public ChoiceKind Kind;
        public IReadOnlyList<UpgradeOption> Options;
        public Action<int> OnPick;
    }

    // Level-up and chest reward screen: up to 3 upgrade cards. Each kind of screen (companions, items, chest)
    // has its own colors and hint so you can tell them apart at a glance: edit "Themes" on the prefab.
    public sealed class LevelUpPopup : BasePopup
    {
        [Serializable]
        public struct Theme
        {
            public Color TitleColor;
            [Tooltip("Full-screen darkening behind the cards.")]
            public Color DimColor;
            public Color CardColor;
            [Tooltip("Round backdrop behind each card picture.")]
            public Color IconRingColor;
            public Color BadgeColor;
            [TextArea] public string Hint;
        }

        [SerializeField] TextMeshProUGUI title;
        [SerializeField] UpgradeCardView[] cards;
        [Tooltip("Offset between cards. (0, -340) stacks them vertically (portrait); (480, 0) puts them side by side.")]
        [SerializeField] Vector2 cardStep = new Vector2(0f, -340f);
        [Tooltip("Position of the middle of the card group.")]
        [SerializeField] Vector2 cardsCenter = new Vector2(0f, -80f);
        [SerializeField] Image dim;
        [SerializeField] TextMeshProUGUI hint;

        [Header("Themes")]
        [SerializeField] Theme companionTheme = new Theme
        {
            TitleColor = new Color(0.45f, 0.9f, 1f),
            DimColor = new Color(0.02f, 0.08f, 0.18f, 0.82f),
            CardColor = new Color(0.1f, 0.17f, 0.32f, 0.97f),
            IconRingColor = new Color(0.35f, 0.7f, 1f),
            BadgeColor = new Color(0.55f, 1f, 0.75f),
            Hint = "Chiêu mộ đệ tử mới hoặc nâng cấp đệ tử đang có"
        };
        [SerializeField] Theme itemTheme = new Theme
        {
            TitleColor = new Color(1f, 0.78f, 0.25f),
            DimColor = new Color(0.16f, 0.07f, 0.0f, 0.84f),
            CardColor = new Color(0.33f, 0.2f, 0.1f, 0.97f),
            IconRingColor = new Color(1f, 0.75f, 0.3f),
            BadgeColor = new Color(1f, 0.9f, 0.45f),
            Hint = "Chọn 1 món đồ: tăng chỉ số vĩnh viễn cho hero"
        };
        [SerializeField] Theme chestTheme = new Theme
        {
            TitleColor = new Color(1f, 0.85f, 0.3f),
            DimColor = new Color(0.12f, 0.02f, 0.16f, 0.82f),
            CardColor = new Color(0.25f, 0.12f, 0.33f, 0.97f),
            IconRingColor = new Color(0.85f, 0.55f, 1f),
            BadgeColor = new Color(1f, 0.85f, 0.3f),
            Hint = "Quà từ rương báu: chọn 1 đệ tử"
        };

        protected override void OnShow(object data)
        {
            var d = (LevelUpData)data;
            var theme = d.Kind == ChoiceKind.Item ? itemTheme : d.Kind == ChoiceKind.Chest ? chestTheme : companionTheme;
            title.text = d.Title;
            title.color = theme.TitleColor;
            if (dim != null) dim.color = theme.DimColor;
            if (hint != null && !string.IsNullOrEmpty(theme.Hint)) hint.text = theme.Hint;

            var count = Mathf.Min(d.Options.Count, cards.Length);
            var start = cardsCenter - cardStep * ((count - 1) / 2f);
            for (var i = 0; i < cards.Length; i++)
            {
                var visible = i < count;
                cards[i].gameObject.SetActive(visible);
                if (!visible) continue;
                var index = i;
                cards[i].Bind(d.Options[i], i, () => d.OnPick(index));
                cards[i].ApplyTheme(theme.CardColor, theme.IconRingColor, theme.BadgeColor);
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
