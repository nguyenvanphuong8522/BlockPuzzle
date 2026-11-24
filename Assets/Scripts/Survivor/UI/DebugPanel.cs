using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Survivor
{
    // In-game debug tools (Editor + Development builds only). Open with F3 or the DEBUG button.
    public sealed class DebugPanel
    {
        readonly DebugSettings settings;
        readonly Player player;
        readonly EnemySystem enemies;
        readonly EnemySpawner spawner;
        readonly PlayerProgress progress;
        readonly GameStats stats;
        readonly UpgradeService upgrades;
        readonly PickupSystem pickups;
        readonly AudioService audio;
        readonly GemSystem gems;
        readonly PlayerTuning tuning;
        readonly HapticsService haptics;
        readonly List<Action> refreshers = new List<Action>();

        GameObject panel;
        float columnX; // left edge of the column rows are added to

        public bool IsOpen => panel != null && panel.activeSelf;

        // Raised with true when the panel opens, false when it closes (keyboard, gamepad or the DEBUG button).
        public event Action<bool> Toggled;

        public DebugPanel(DebugSettings settings, Player player, EnemySystem enemies, EnemySpawner spawner,
            PlayerProgress progress, GameStats stats, UpgradeService upgrades, PickupSystem pickups, AudioService audio, GemSystem gems, PlayerTuning tuning, HapticsService haptics)
        {
            this.settings = settings;
            this.player = player;
            this.enemies = enemies;
            this.spawner = spawner;
            this.progress = progress;
            this.stats = stats;
            this.upgrades = upgrades;
            this.pickups = pickups;
            this.audio = audio;
            this.gems = gems;
            this.tuning = tuning;
            this.haptics = haptics;
        }

        public void Build()
        {
            if (!settings.ToolsAvailable) return;

            // Own canvas, drawn above the HUD and the level-up screen.
            var canvasGo = new GameObject("DebugCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920); // portrait
            scaler.matchWidthOrHeight = 0f;                       // scale by width
            var root = (RectTransform)canvasGo.transform;

            var toggleButton = CreateButton(root, "DEBUG", new Vector2(0f, 0f), new Vector2(90, 40), new Vector2(150, 56),
                new Color(0.3f, 0.3f, 0.3f, 0.8f));
            toggleButton.OnPressed = Toggle;

            panel = new GameObject("DebugPanel", typeof(RectTransform), typeof(Image));
            var rt = (RectTransform)panel.transform;
            rt.SetParent(root, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 0f);
            rt.pivot = new Vector2(0f, 0f);
            rt.anchoredPosition = new Vector2(16, 70);
            rt.sizeDelta = new Vector2(840, 810);
            panel.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.85f);

            // Left column: settings
            columnX = 0f;
            var y = -20f;
            Label(rt, "DEBUG (F3 / Back)  ·  CÀI ĐẶT", ref y);
            ToggleRow(rt, "Bất tử", () => settings.GodMode, v => settings.GodMode = v, ref y);
            ToggleRow(rt, "Số sát thương", () => settings.ShowDamageNumbers, v => settings.ShowDamageNumbers = v, ref y);
            ToggleRow(rt, "Hiện FPS / thông số", () => settings.ShowStats, v => settings.ShowStats = v, ref y);
            ToggleRow(rt, "Tắt tiếng", () => audio.Muted, v => audio.Muted = v, ref y);
            ToggleRow(rt, "Hiện tầm hút", () => settings.ShowPickupRange, v => settings.ShowPickupRange = v, ref y);
            ToggleRow(rt, "Hiện tầm nhìn đệ tử", () => settings.ShowCompanionRange, v => settings.ShowCompanionRange = v, ref y);
            ToggleRow(rt, "Hiện vùng chém rái cá", () => settings.ShowAttackArea, v => settings.ShowAttackArea = v, ref y);
            StepperRow(rt, () => $"Tầm hút gốc: {tuning.MagnetRadius:0.00}",
                step => tuning.MagnetRadius = Mathf.Max(0.25f, tuning.MagnetRadius + step * 0.25f), ref y);
            SpeedRow(rt, ref y);
            ToggleRow(rt, "Rung (điện thoại / tay cầm)", () => haptics.Enabled, v => haptics.Enabled = v, ref y);
            StepperRow(rt, () => $"Độ rung: {Mathf.RoundToInt(haptics.Strength * 100)}%",
                step => { haptics.Strength += step * 0.1f; haptics.Save(); haptics.PlayHit(); }, ref y);
            StepperRow(rt, () => $"Thời gian rung: {haptics.DurationMs} ms",
                step => { haptics.DurationMs += step * 20; haptics.Save(); haptics.PlayHit(); }, ref y);
            ActionRow(rt, "Thử rung", haptics.PlayHit, ref y);

            // Right column: actions
            columnX = 420f;
            y = -20f;
            Label(rt, "HÀNH ĐỘNG", ref y);
            ActionRow(rt, "Hồi đầy máu", () => player.Heal(player.Stats.MaxHp), ref y);
            ActionRow(rt, "+1 cấp", () => progress.AddXp(progress.XpToNext.Value - progress.Xp.Value), ref y);
            ActionRow(rt, "Full đệ tử cấp tối đa", upgrades.GiveAllCompanionsMaxed, ref y);
            ActionRow(rt, "Full vũ khí cấp tối đa", upgrades.GiveAllWeaponsMaxed, ref y);
            ActionRow(rt, "Thả 1000 quái", () => spawner.SpawnBurst(1000), ref y);
            ActionRow(rt, "Gọi boss", spawner.SpawnBoss, ref y);
            ActionRow(rt, "Thử anim hút ngọc (F4)", () => gems.SpawnTestRing(20), ref y);
            ActionRow(rt, "Thả rương", () => pickups.Spawn(PickupKind.Chest, player.Position + new Vector2(1.5f, 0f)), ref y);
            ActionRow(rt, "Giết hết quái", enemies.KillAll, ref y);
            ActionRow(rt, "Tua thời gian +60s", () => stats.Elapsed += 60f, ref y);

            panel.SetActive(false);
        }

        public void Toggle()
        {
            if (panel == null) return;
            panel.SetActive(!panel.activeSelf);
            Toggled?.Invoke(panel.activeSelf);
            Refresh();
        }

        void Refresh()
        {
            foreach (var r in refreshers) r();
        }

        void Label(RectTransform parent, string text, ref float y)
        {
            var t = CreateText(parent, text, 30, new Vector2(columnX + 20, y), new Vector2(380, 44));
            t.color = new Color(1f, 0.85f, 0.3f);
            y -= 54f;
        }

        void ToggleRow(RectTransform parent, string label, Func<bool> get, Action<bool> set, ref float y)
        {
            var button = CreateButton(parent, "", new Vector2(0, 1), new Vector2(columnX + 210, y - 22), new Vector2(380, 48),
                new Color(0.2f, 0.2f, 0.25f));
            var text = button.GetComponentInChildren<TextMeshProUGUI>();
            void UpdateLabel()
            {
                var on = get();
                text.text = (on ? "<color=#7CFC7C>[BẬT]</color>  " : "<color=#888888>[TẮT]</color>  ") + label;
            }
            button.OnPressed = () =>
            {
                set(!get());
                UpdateLabel();
            };
            refreshers.Add(UpdateLabel);
            UpdateLabel();
            y -= 56f;
        }

        void SpeedRow(RectTransform parent, ref float y)
        {
            var button = CreateButton(parent, "", new Vector2(0, 1), new Vector2(columnX + 210, y - 22), new Vector2(380, 48),
                new Color(0.2f, 0.2f, 0.25f));
            var text = button.GetComponentInChildren<TextMeshProUGUI>();
            void UpdateLabel() => text.text = $"Tốc độ game: x{settings.GameSpeed:0}";
            button.OnPressed = () =>
            {
                settings.GameSpeed = settings.GameSpeed >= 4f ? 1f : settings.GameSpeed * 2f;
                Time.timeScale = settings.GameSpeed;
                UpdateLabel();
            };
            refreshers.Add(UpdateLabel);
            UpdateLabel();
            y -= 56f;
        }

        // A value with − / + buttons. step is -1 or +1.
        void StepperRow(RectTransform parent, Func<string> label, Action<int> change, ref float y)
        {
            var row = CreateButton(parent, "", new Vector2(0, 1), new Vector2(columnX + 210, y - 22), new Vector2(380, 48),
                new Color(0.2f, 0.2f, 0.25f));
            UnityEngine.Object.Destroy(row); // only the background; the two small buttons do the work
            var text = row.GetComponentInChildren<TextMeshProUGUI>();
            text.alignment = TextAlignmentOptions.Left;
            text.rectTransform.sizeDelta = new Vector2(360, 48);
            void UpdateLabel() => text.text = "  " + label();
            var rowRect = (RectTransform)row.transform;
            CreateButton(rowRect, "-", new Vector2(1f, 0.5f), new Vector2(-82, 0), new Vector2(52, 40), new Color(0.15f, 0.35f, 0.6f))
                .OnPressed = () => { change(-1); UpdateLabel(); };
            CreateButton(rowRect, "+", new Vector2(1f, 0.5f), new Vector2(-26, 0), new Vector2(52, 40), new Color(0.15f, 0.35f, 0.6f))
                .OnPressed = () => { change(1); UpdateLabel(); };
            refreshers.Add(UpdateLabel);
            UpdateLabel();
            y -= 56f;
        }

        void ActionRow(RectTransform parent, string label, Action action, ref float y)
        {
            CreateButton(parent, label, new Vector2(0, 1), new Vector2(columnX + 210, y - 22), new Vector2(380, 48),
                new Color(0.15f, 0.35f, 0.6f)).OnPressed = action;
            y -= 56f;
        }

        static TapButton CreateButton(RectTransform parent, string label, Vector2 anchor, Vector2 position, Vector2 size,
            Color color)
        {
            var go = new GameObject("Button", typeof(RectTransform), typeof(Image));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = anchor;
            rt.anchoredPosition = position;
            rt.sizeDelta = size;
            go.GetComponent<Image>().color = color;
            var text = CreateText(rt, label, 26, Vector2.zero, size);
            text.rectTransform.anchorMin = text.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            text.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            text.rectTransform.anchoredPosition = Vector2.zero;
            text.alignment = TextAlignmentOptions.Center;
            return go.AddComponent<TapButton>();
        }

        static TextMeshProUGUI CreateText(RectTransform parent, string text, float size, Vector2 position, Vector2 box)
        {
            var go = new GameObject("Text", typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = position;
            rt.sizeDelta = box;
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = size;
            tmp.alignment = TextAlignmentOptions.Left;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.raycastTarget = false;
            return tmp;
        }
    }
}
