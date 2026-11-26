using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Survivor
{
    // One upgrade card inside LevelUpPopup.
    // Stars under the picture = the level it will have after this pick (the new star blinks).
    // Companions that are part of a fusion show the recipe at the bottom: A (5 stars) + B (5 stars) = fusion.
    // The star row and the recipe are made by the menu "Survivor/Setup Level-Up Card Stars"; move them freely.
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
        [Tooltip("Picture of the companion / item.")]
        [SerializeField] Image icon;
        [Tooltip("Round backdrop behind the picture (tinted per screen).")]
        [SerializeField] Image iconRing;
        [Tooltip("The card background (tinted per screen).")]
        [SerializeField] Image background;

        [Header("Sao cấp")]
        [SerializeField] Image[] stars = new Image[0];
        [SerializeField] Color starOn = new Color(1f, 0.82f, 0.2f);
        [SerializeField] Color starOff = new Color(0.1f, 0.1f, 0.14f, 0.55f);
        [Tooltip("The star this pick gives blinks between Star On and this color.")]
        [SerializeField] Color starNewBlink = Color.white;
        [SerializeField] float starBlinkSpeed = 6f;

        [Header("Công thức hợp thể")]
        [SerializeField] GameObject recipeRoot;
        [Tooltip("Ingredient pictures (2).")]
        [SerializeField] Image[] recipeParts = new Image[0];
        [Tooltip("Small stars under the ingredients: Recipe Stars Per Part for the first one, then the next...")]
        [SerializeField] Image[] recipeStars = new Image[0];
        [SerializeField] int recipeStarsPerPart = 5;
        [SerializeField] Image recipeResult;
        [Tooltip("Gap between the description and the recipe strip (pixels).")]
        [SerializeField] float recipeGap = 4f;
        [Tooltip("Gap under the description when there is no recipe (pixels).")]
        [SerializeField] float descriptionBottom = 10f;

        Image newStar;

        public void Bind(UpgradeOption option, int index, Action onPick)
        {
            stripe.color = option.Tint;
            if (keyHint != null) keyHint.text = $"[{index + 1}]";
            title.text = option.Title;
            title.color = option.Tint;
            badge.text = option.Badge;
            badge.gameObject.SetActive(!string.IsNullOrEmpty(option.Badge));
            description.text = option.Description;
            button.OnPressed = onPick;

            if (icon != null)
            {
                icon.sprite = option.Icon;
                icon.enabled = option.Icon != null;
                icon.preserveAspect = true;
            }
            if (iconRing != null) iconRing.enabled = option.Icon != null;

            BindStars(option);
            BindRecipe(option.Recipe);
        }

        void BindStars(UpgradeOption option)
        {
            newStar = null;
            for (var i = 0; i < stars.Length; i++)
            {
                var star = stars[i];
                if (star == null) continue;
                var shown = i < option.MaxStars;
                star.gameObject.SetActive(shown);
                if (!shown) continue;
                star.color = i < option.Stars ? starOn : starOff;
                star.transform.localScale = Vector3.one;
                if (option.StarIsNew && i == option.Stars - 1) newStar = star;
            }
        }

        void BindRecipe(FusionRecipe recipe)
        {
            var show = recipe != null && recipeRoot != null;
            if (recipeRoot != null) recipeRoot.SetActive(show);
            if (show)
            {
                for (var p = 0; p < recipeParts.Length; p++)
                {
                    var has = p < recipe.Parts.Length;
                    if (recipeParts[p] != null)
                    {
                        recipeParts[p].sprite = has ? recipe.Parts[p] : null;
                        recipeParts[p].enabled = has && recipe.Parts[p] != null;
                        recipeParts[p].preserveAspect = true;
                    }
                    for (var s = 0; s < recipeStarsPerPart; s++)
                    {
                        var k = p * recipeStarsPerPart + s;
                        if (k >= recipeStars.Length || recipeStars[k] == null) continue;
                        var shown = has && s < recipe.PartMaxStars;
                        recipeStars[k].gameObject.SetActive(shown);
                        if (shown) recipeStars[k].color = s < recipe.PartStars[p] ? starOn : starOff;
                    }
                }
                if (recipeResult != null)
                {
                    recipeResult.sprite = recipe.Result;
                    recipeResult.enabled = recipe.Result != null;
                    recipeResult.preserveAspect = true;
                }
            }

            // The description fills the space down to the recipe strip (or the card bottom).
            var rt = description.rectTransform;
            var bottom = descriptionBottom;
            if (show)
            {
                var r = (RectTransform)recipeRoot.transform;
                bottom = r.anchoredPosition.y + r.rect.height * (1f - r.pivot.y) + recipeGap;
            }
            if (rt.anchorMin.y == 0f && rt.anchorMax.y == 1f) rt.offsetMin = new Vector2(rt.offsetMin.x, bottom);
        }

        void Update()
        {
            if (newStar == null) return;
            var k = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * starBlinkSpeed);
            newStar.color = Color.Lerp(starOn, starNewBlink, k * 0.7f);
            newStar.transform.localScale = Vector3.one * (1f + 0.18f * k);
        }

        // Colors of the screen this card is on (companions / items / chest look different).
        public void ApplyTheme(Color card, Color ring, Color badgeColor)
        {
            if (background != null) background.color = card;
            if (iconRing != null) iconRing.color = ring;
            badge.color = badgeColor;
        }

        public void SetHighlighted(bool on)
        {
            highlight.enabled = on;
            transform.localScale = Vector3.one * (on ? highlightScale : 1f);
        }
    }
}
