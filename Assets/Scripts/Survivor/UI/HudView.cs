using System;
using System.Collections.Generic;
using R3;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Survivor
{
    // Builds the HUD from code: XP bar, level, timer, kills, debug info, joystick, level-up cards, result screen.
    public sealed class HudView : IDisposable
    {
        readonly InputService input;
        readonly PlayerProgress progress;
        readonly GameStats stats;
        readonly List<IDisposable> subscriptions = new List<IDisposable>();

        RectTransform root;
        Image xpFill;
        TextMeshProUGUI levelText;
        TextMeshProUGUI timeText;
        TextMeshProUGUI killText;
        TextMeshProUGUI debugText;
        VirtualJoystick joystick;
        GameObject levelUpPanel;
        GameObject bossBar;
        Image bossFill;
        TextMeshProUGUI bossName;
        int shownSeconds = -1;

        public HudView(InputService input, PlayerProgress progress, GameStats stats)
        {
            this.input = input;
            this.progress = progress;
            this.stats = stats;
        }

        public void Build()
        {
            if (EventSystem.current == null)
                new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

            var canvasGo = new GameObject("HUD", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            root = (RectTransform)canvasGo.transform;

            BuildJoystick(); // first, so everything else is drawn on top of its touch area

            // Dark strip behind the top row so white text stays readable on the bright beach.
            var strip = CreateImage(root, "TopStrip", UiSprites.Square, new Color(0f, 0f, 0f, 0.35f));
            Stretch(strip.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -70), new Vector2(0, 140));
            strip.raycastTarget = false;

            var xpBack = CreateImage(root, "XpBar", UiSprites.Square, new Color(0f, 0f, 0f, 0.6f));
            Stretch(xpBack.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -18), new Vector2(0, 36));
            xpFill = CreateImage(xpBack.rectTransform, "Fill", UiSprites.Square, new Color(0.35f, 0.7f, 1f));
            Stretch(xpFill.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            xpFill.type = Image.Type.Filled;
            xpFill.fillMethod = Image.FillMethod.Horizontal;
            levelText = CreateText(xpBack.rectTransform, "", 28, new Vector2(0, 0.5f), new Vector2(110, 0), new Vector2(200, 36));

            timeText = CreateText(root, "00:00", 56, new Vector2(0.5f, 1f), new Vector2(0, -85), new Vector2(300, 70));
            killText = CreateText(root, "", 34, new Vector2(1f, 1f), new Vector2(-170, -75), new Vector2(300, 50));
            debugText = CreateText(root, "", 24, new Vector2(0f, 1f), new Vector2(230, -75), new Vector2(420, 40));
            debugText.alignment = TextAlignmentOptions.Left;
            debugText.color = new Color(1f, 1f, 1f, 0.9f);

            BuildBossBar();

            subscriptions.Add(progress.Xp.Subscribe(_ => RefreshXp()));
            subscriptions.Add(progress.XpToNext.Subscribe(_ => RefreshXp()));
            subscriptions.Add(progress.Level.Subscribe(l => levelText.text = $"Cấp {l}"));
            subscriptions.Add(stats.Kills.Subscribe(k => killText.text = $"Hạ gục: {k}"));
        }

        void RefreshXp() => xpFill.fillAmount = progress.XpToNext.Value > 0 ? (float)progress.Xp.Value / progress.XpToNext.Value : 0f;

        public void SetTime(float seconds)
        {
            var s = Mathf.FloorToInt(seconds);
            if (s == shownSeconds) return; // avoid rebuilding the text every frame
            shownSeconds = s;
            timeText.text = $"{s / 60:00}:{s % 60:00}";
        }

        public void SetDebug(string text) => debugText.text = text;

        // Shared by level-ups and chests: shows the upgrade cards with a title.
        public void ShowLevelUp(string title, IReadOnlyList<UpgradeOption> options, Action<int> onPick)
        {
            joystick.Release();
            levelUpPanel = CreateOverlay("LevelUp", 0.7f).gameObject;
            var panel = (RectTransform)levelUpPanel.transform;
            CreateText(panel, title, 90, new Vector2(0.5f, 0.5f), new Vector2(0, 360), new Vector2(1000, 120))
                .color = new Color(1f, 0.85f, 0.3f);
            CreateText(panel, "Chọn 1 nâng cấp (phím 1 / 2 / 3)", 30, new Vector2(0.5f, 0.5f), new Vector2(0, 280),
                new Vector2(1000, 50));

            const float cardWidth = 440f, gap = 40f;
            var startX = -(options.Count - 1) * (cardWidth + gap) / 2f;
            for (var i = 0; i < options.Count; i++)
            {
                var option = options[i];
                var index = i;
                var card = CreateImage(panel, "Card" + i, UiSprites.Square, new Color(0.12f, 0.12f, 0.18f, 0.95f));
                Place(card.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(startX + i * (cardWidth + gap), -40),
                    new Vector2(cardWidth, 520));
                var stripe = CreateImage(card.rectTransform, "Stripe", UiSprites.Square, option.Tint);
                Stretch(stripe.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -6), new Vector2(0, 12));

                CreateText(card.rectTransform, $"[{i + 1}]", 28, new Vector2(0.5f, 1f), new Vector2(0, -50), new Vector2(400, 40))
                    .color = new Color(1f, 1f, 1f, 0.5f);
                CreateText(card.rectTransform, option.Title, 48, new Vector2(0.5f, 1f), new Vector2(0, -120), new Vector2(400, 70))
                    .color = option.Tint;
                CreateText(card.rectTransform, option.Badge, 32, new Vector2(0.5f, 1f), new Vector2(0, -180), new Vector2(400, 50))
                    .color = new Color(1f, 0.85f, 0.3f);
                var desc = CreateText(card.rectTransform, option.Description, 32, new Vector2(0.5f, 0.5f), new Vector2(0, -90),
                    new Vector2(380, 220));
                desc.textWrappingMode = TextWrappingModes.Normal;

                card.gameObject.AddComponent<TapButton>().OnPressed = () => onPick(index);
            }
        }

        // Shows the boss health bar while a boss is alive. ratio < 0 hides it.
        public void SetBoss(string name, float ratio)
        {
            var visible = ratio >= 0f;
            if (bossBar.activeSelf != visible) bossBar.SetActive(visible);
            if (!visible) return;
            bossFill.fillAmount = ratio;
            if (bossName.text != name) bossName.text = name;
        }

        void BuildBossBar()
        {
            var back = CreateImage(root, "BossBar", UiSprites.Square, new Color(0f, 0f, 0f, 0.7f));
            Place(back.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -175), new Vector2(900, 34));
            bossFill = CreateImage(back.rectTransform, "Fill", UiSprites.Square, new Color(0.8f, 0.1f, 0.2f));
            Stretch(bossFill.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(-8, -8));
            bossFill.type = Image.Type.Filled;
            bossFill.fillMethod = Image.FillMethod.Horizontal;
            bossName = CreateText(back.rectTransform, "", 26, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900, 34));
            bossName.color = Color.white;
            bossBar = back.gameObject;
            bossBar.SetActive(false);
        }

        public void HideLevelUp()
        {
            if (levelUpPanel != null) UnityEngine.Object.Destroy(levelUpPanel);
            levelUpPanel = null;
        }

        public void ShowResult(bool survived, float seconds, int kills, int level, Action onRestart)
        {
            joystick.Release();
            var panel = CreateOverlay("Result", 0.8f);
            CreateText(panel, survived ? "SỐNG SÓT!" : "BẠN ĐÃ GỤC NGÃ", 110, new Vector2(0.5f, 0.5f), new Vector2(0, 220),
                new Vector2(1400, 150)).color = survived ? new Color(1f, 0.85f, 0.3f) : new Color(0.9f, 0.4f, 0.4f);

            var s = Mathf.FloorToInt(seconds);
            CreateText(panel, $"Thời gian: {s / 60:00}:{s % 60:00}\nHạ gục: {kills}\nCấp: {level}", 44,
                new Vector2(0.5f, 0.5f), new Vector2(0, 30), new Vector2(800, 220));

            var button = CreateImage(panel, "Restart", UiSprites.Square, new Color(0.2f, 0.6f, 1f));
            Place(button.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, -200), new Vector2(460, 120));
            CreateText(button.rectTransform, "Chơi lại (R)", 52, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(460, 120));
            button.gameObject.AddComponent<TapButton>().OnPressed = onRestart;
        }

        void BuildJoystick()
        {
            var area = CreateImage(root, "JoystickArea", UiSprites.Square, new Color(0, 0, 0, 0)); // invisible, catches touches
            Stretch(area.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            var background = CreateImage(area.rectTransform, "Background", UiSprites.Circle, new Color(1, 1, 1, 0.15f));
            Place(background.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(260, 260));
            background.raycastTarget = false;
            var knob = CreateImage(background.rectTransform, "Knob", UiSprites.Circle, new Color(1, 1, 1, 0.5f));
            Place(knob.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(110, 110));
            knob.raycastTarget = false;

            joystick = area.gameObject.AddComponent<VirtualJoystick>();
            joystick.Background = background.rectTransform;
            joystick.Knob = knob.rectTransform;
            joystick.Input = input;
            background.gameObject.SetActive(false);
        }

        RectTransform CreateOverlay(string name, float alpha)
        {
            var overlay = CreateImage(root, name, UiSprites.Square, new Color(0f, 0f, 0f, alpha));
            Stretch(overlay.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            return overlay.rectTransform;
        }

        static Image CreateImage(RectTransform parent, string name, Sprite sprite, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            return image;
        }

        static TextMeshProUGUI CreateText(RectTransform parent, string text, float size, Vector2 anchor,
            Vector2 position, Vector2 boxSize)
        {
            var go = new GameObject("Text", typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            Place(rt, anchor, position, boxSize);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = size;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.raycastTarget = false;
            return tmp;
        }

        static void Place(RectTransform rt, Vector2 anchor, Vector2 position, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = anchor;
            rt.anchoredPosition = position;
            rt.sizeDelta = size;
        }

        static void Stretch(RectTransform rt, Vector2 anchorMin, Vector2 anchorMax, Vector2 position, Vector2 sizeDelta)
        {
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.anchoredPosition = position;
            rt.sizeDelta = sizeDelta;
        }

        public void Dispose()
        {
            foreach (var s in subscriptions) s.Dispose();
            subscriptions.Clear();
        }
    }
}
