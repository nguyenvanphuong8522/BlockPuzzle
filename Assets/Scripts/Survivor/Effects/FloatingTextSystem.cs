using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Survivor
{
    // Short texts that pop up over the hero's head and float up (e.g. "+5% ATK" from the pig's gift).
    // They follow the hero while rising. Uses a small screen-space canvas below the HUD; settings in PlayerTuning
    // ("Chữ bay trên đầu hero").
    public sealed class FloatingTextSystem
    {
        const int MaxTexts = 8;

        sealed class Item
        {
            public TextMeshProUGUI Text;
            public float Age = 99f;
            public int Line;     // stacks several texts shown at the same time
            public Color Color;
        }

        readonly Player player;
        readonly PlayerTuning tuning;
        readonly List<Item> items = new List<Item>(MaxTexts);
        RectTransform root;

        public FloatingTextSystem(Player player, PlayerTuning tuning)
        {
            this.player = player;
            this.tuning = tuning;
        }

        public void Show(string text, Color color)
        {
            if (root == null) Build();

            // Reuse the oldest one if all are busy; stack on the next free line so texts don't overlap.
            Item item = null;
            var line = 0;
            foreach (var it in items)
            {
                if (it.Age >= tuning.FloatTextDuration) { if (item == null) item = it; }
                else line = Mathf.Max(line, it.Line + 1);
            }
            if (item == null)
            {
                item = items[0];
                foreach (var it in items) if (it.Age > item.Age) item = it;
            }
            if (line > 3) line = 0;

            item.Age = 0f;
            item.Line = line;
            item.Color = color;
            item.Text.text = text;
            item.Text.gameObject.SetActive(true);
        }

        public void Tick(float dt)
        {
            foreach (var it in items)
            {
                if (it.Age >= tuning.FloatTextDuration) continue;
                it.Age += dt;
                if (it.Age >= tuning.FloatTextDuration) it.Text.gameObject.SetActive(false);
            }
        }

        // Every frame after the camera moved: place the texts over the hero's head.
        public void Draw(Camera camera)
        {
            if (root == null || camera == null) return;
            var duration = Mathf.Max(0.1f, tuning.FloatTextDuration);
            foreach (var it in items)
            {
                if (it.Age >= duration) continue;
                var k = it.Age / duration;
                var rise = tuning.FloatTextRise * (1f - (1f - k) * (1f - k));            // ease-out
                var world = player.Position + new Vector2(0f, tuning.FloatTextHeight + rise);
                // Stacked texts are spaced in screen space (one text height per line), whatever the camera zoom.
                var lineStep = tuning.FloatTextSize * tuning.FloatTextLineSpacing * root.lossyScale.y;
                it.Text.rectTransform.position = camera.WorldToScreenPoint(world) + new Vector3(0f, it.Line * lineStep, 0f);
                var pop = it.Age < 0.12f ? Mathf.Lerp(0.5f, 1.15f, it.Age / 0.12f) :
                          it.Age < 0.22f ? Mathf.Lerp(1.15f, 1f, (it.Age - 0.12f) / 0.1f) : 1f;
                it.Text.rectTransform.localScale = Vector3.one * pop;
                var c = it.Color;
                c.a *= k < 0.7f ? 1f : 1f - (k - 0.7f) / 0.3f;                           // fade at the end
                it.Text.color = c;
                it.Text.fontSize = tuning.FloatTextSize;
            }
            ApplyStyle();
        }

        // Outline / thickness come from PlayerTuning and can be changed while playing. A change makes a fresh
        // material, so the fallback-font glyphs (Vietnamese letters with marks) get the new outline too.
        Material material;
        float appliedOutline = -1f, appliedDilate = -1f;
        Color appliedOutlineColor;

        void ApplyStyle()
        {
            var w = Mathf.Clamp01(tuning.FloatTextOutlineWidth);
            var d = Mathf.Clamp(tuning.FloatTextThickness, -1f, 1f);
            var oc = tuning.FloatTextOutlineColor;
            if (material != null && w == appliedOutline && d == appliedDilate && oc == appliedOutlineColor) return;
            appliedOutline = w;
            appliedDilate = d;
            appliedOutlineColor = oc;

            var font = items[0].Text.font;
            if (font == null || font.material == null) return;
            var old = material;
            // Resources/FloatingTextOutline.mat has OUTLINE_ON turned on, so builds keep the outline shader variant.
            var template = Resources.Load<Material>("FloatingTextOutline");
            material = new Material(template != null ? template : font.material) { name = "FloatingText (outline)" };
            material.SetTexture(ShaderUtilities.ID_MainTex, font.material.GetTexture(ShaderUtilities.ID_MainTex));
            if (w > 0f) material.EnableKeyword(ShaderUtilities.Keyword_Outline);
            else material.DisableKeyword(ShaderUtilities.Keyword_Outline);
            material.SetFloat(ShaderUtilities.ID_OutlineWidth, w);
            material.SetColor(ShaderUtilities.ID_OutlineColor, oc);
            material.SetFloat(ShaderUtilities.ID_FaceDilate, d);
            foreach (var it in items)
            {
                it.Text.fontSharedMaterial = material;
                it.Text.UpdateMeshPadding();
                it.Text.SetAllDirty();
            }
            if (old != null) Object.Destroy(old);
        }

        void Build()
        {
            var go = new GameObject("FloatingTextCanvas", typeof(Canvas), typeof(CanvasScaler));
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = -5; // under the HUD and popups
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 0f;
            root = (RectTransform)go.transform;

            for (var i = 0; i < MaxTexts; i++)
            {
                var t = new GameObject("FloatText", typeof(RectTransform));
                var rt = (RectTransform)t.transform;
                rt.SetParent(root, false);
                rt.sizeDelta = new Vector2(700, 120);
                var tmp = t.AddComponent<TextMeshProUGUI>();
                tmp.alignment = TextAlignmentOptions.Center;
                tmp.textWrappingMode = TextWrappingModes.NoWrap;
                tmp.fontStyle = FontStyles.Bold;
                tmp.raycastTarget = false;
                t.SetActive(false);
                items.Add(new Item { Text = tmp });
            }
            ApplyStyle();
        }
    }
}
