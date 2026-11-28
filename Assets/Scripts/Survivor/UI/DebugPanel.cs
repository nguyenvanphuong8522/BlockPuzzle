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
        readonly BossRoster bosses;
        readonly SwarmSystem swarm;
        readonly EliteSystem elites;
        readonly TimelineSystem timeline;
        readonly PlayerProgress progress;
        readonly GameStats stats;
        readonly UpgradeService upgrades;
        readonly PickupSystem pickups;
        readonly EnergyService energy;
        readonly ChapterService chapters;
        readonly AudioService audio;
        readonly GemSystem gems;
        readonly PlayerTuning tuning;
        readonly HapticsService haptics;
        readonly List<Action> refreshers = new List<Action>();

        // Gamepad / keyboard navigation: one list of rows per column, the selected row is highlighted.
        sealed class NavItem
        {
            public Image Background;
            public Outline Frame;
            public Color Normal;
            public Action Submit;        // A / Enter
            public Action<int> Step;     // left / right on a − / + row (null = left / right switches column)
        }
        readonly List<NavItem>[] columns = { new List<NavItem>(), new List<NavItem>() };
        int navColumn, navRow;

        GameObject panel;
        float columnX; // left edge of the column rows are added to

        public bool IsOpen => panel != null && panel.activeSelf;

        // Raised with true when the panel opens, false when it closes (keyboard, gamepad or the DEBUG button).
        public event Action<bool> Toggled;

        public DebugPanel(DebugSettings settings, Player player, EnemySystem enemies, EnemySpawner spawner,
            PlayerProgress progress, GameStats stats, UpgradeService upgrades, PickupSystem pickups, AudioService audio, GemSystem gems, PlayerTuning tuning, HapticsService haptics,
            SwarmSystem swarm, EliteSystem elites, TimelineSystem timeline, BossRoster bosses,
            EnergyService energy, ChapterService chapters)
        {
            this.settings = settings;
            this.player = player;
            this.enemies = enemies;
            this.spawner = spawner;
            this.bosses = bosses;
            this.swarm = swarm;
            this.elites = elites;
            this.timeline = timeline;
            this.progress = progress;
            this.stats = stats;
            this.upgrades = upgrades;
            this.pickups = pickups;
            this.energy = energy;
            this.chapters = chapters;
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
            rt.sizeDelta = new Vector2(840, 1160);
            panel.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.85f);

            // Left column: settings
            columnX = 0f;
            var y = -20f;
            Label(rt, "DEBUG (F3 / Back)  ·  CÀI ĐẶT", ref y);
            var help = CreateText(rt, "Tay cầm: lên/xuống chọn · A bấm · trái/phải đổi số hoặc sang cột · B đóng", 22,
                new Vector2(20, -1125), new Vector2(800, 30));
            help.color = new Color(0.75f, 0.75f, 0.8f);
            ToggleRow(rt, "Bất tử", () => settings.GodMode, v => settings.GodMode = v, ref y);
            ToggleRow(rt, "Số sát thương", () => settings.ShowDamageNumbers, v => settings.ShowDamageNumbers = v, ref y);
            ToggleRow(rt, "Hiện FPS / thông số", () => settings.ShowStats, v => settings.ShowStats = v, ref y);
            ToggleRow(rt, "Tắt tiếng", () => audio.Muted, v => audio.Muted = v, ref y);
            ToggleRow(rt, "Hiện tầm hút", () => settings.ShowPickupRange, v => settings.ShowPickupRange = v, ref y);
            ToggleRow(rt, "Hiện tầm nhìn đệ tử", () => settings.ShowCompanionRange, v => settings.ShowCompanionRange = v, ref y);
            ToggleRow(rt, "Hiện vùng chém (rái cá, lợn)", () => settings.ShowAttackArea, v => settings.ShowAttackArea = v, ref y);
            StepperRow(rt, () => $"Tầm hút gốc: {tuning.MagnetRadius:0.00}",
                step => tuning.MagnetRadius = Mathf.Max(0.25f, tuning.MagnetRadius + step * 0.25f), ref y);
            SpeedRow(rt, ref y);
            StepperRow(rt, () => $"Ngôn ngữ: {(Loc.CurrentLanguage != null ? Loc.CurrentLanguage.NativeName : "-")}",
                step => Loc.CycleLanguage(step), ref y);
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
            CompanionPickerRows(rt, ref y);
            ActionRow(rt, "Full vũ khí cấp tối đa", upgrades.GiveAllWeaponsMaxed, ref y);
            ActionRow(rt, "Thả 1000 quái", () => spawner.SpawnBurst(1000), ref y);
            foreach (var b in bosses.All) // one button per boss (only if no boss is alive)
            {
                var boss = b;
                ActionRow(rt, "Gọi boss: " + boss.Definition.Name, () => spawner.SpawnBoss(boss), ref y);
            }
            ActionRow(rt, "Gọi đàn quái bay", swarm.TriggerNow, ref y);
            ActionRow(rt, "Thả 5 Mực Phun Mực (bắn xa)", SpawnSquids, ref y);
            ActionRow(rt, "Gọi quái tinh anh", elites.SpawnNow, ref y);
            ActionRow(rt, "Chạy sự kiện timeline kế tiếp", () => UnityEngine.Debug.Log("[Debug] Timeline: " + timeline.TriggerNext()), ref y);
            ActionRow(rt, "Thử anim hút ngọc (F4)", () => gems.SpawnTestRing(20), ref y);
            ActionRow(rt, "Thả rương", () => pickups.Spawn(PickupKind.Chest, player.Position + new Vector2(1.5f, 0f)), ref y);
            ActionRow(rt, "Thả rương boss (xa 15m)", () => pickups.Spawn(PickupKind.BossChest, player.Position + new Vector2(6f, 14f)), ref y);
            ActionRow(rt, "Giết hết quái", enemies.KillAll, ref y);
            ActionRow(rt, "Năng lượng: hồi đầy (+60)", () => energy.Add(energy.Max), ref y);
            ActionRow(rt, "Mở khóa mọi chương", chapters.UnlockAll, ref y);
            ActionRow(rt, "Tua thời gian +60s", () => stats.Elapsed += 60f, ref y);

            panel.SetActive(false);
        }

        public void Toggle()
        {
            if (panel == null) return;
            panel.SetActive(!panel.activeSelf);
            Toggled?.Invoke(panel.activeSelf);
            Refresh();
            if (panel.activeSelf) Select(navColumn, navRow);
        }

        // Gamepad / keyboard while the panel is open (called by GameController every frame).
        // vertical: +1 up / -1 down; horizontal: -1 left / +1 right.
        public void HandleNavigation(int vertical, int horizontal, bool submit, bool cancel)
        {
            if (!IsOpen) return;
            if (cancel)
            {
                Toggle();
                return;
            }
            var items = columns[navColumn];
            if (items.Count == 0) return;
            var current = items[Mathf.Clamp(navRow, 0, items.Count - 1)];

            if (vertical != 0) Select(navColumn, navRow - vertical);
            if (horizontal != 0)
            {
                if (current.Step != null) current.Step(horizontal);   // − / + row: change the value
                else
                {
                    var other = navColumn == 0 ? 1 : 0;              // otherwise: jump to the other column
                    if (columns[other].Count > 0) Select(other, navRow);
                }
            }
            if (submit) current.Submit?.Invoke();
        }

        void Select(int column, int row)
        {
            var items = columns[column];
            if (items.Count == 0) return;
            navColumn = column;
            navRow = (row % items.Count + items.Count) % items.Count;   // wraps around
            for (var c = 0; c < columns.Length; c++)
                for (var r = 0; r < columns[c].Count; r++)
                {
                    var item = columns[c][r];
                    var on = c == navColumn && r == navRow;
                    item.Frame.enabled = on;
                    item.Background.color = on ? Color.Lerp(item.Normal, Color.white, 0.25f) : item.Normal;
                }
        }

        // Registers a row for gamepad navigation in the current column.
        void AddNav(GameObject row, Action submit, Action<int> step = null)
        {
            var image = row.GetComponent<Image>();
            var frame = row.AddComponent<Outline>();
            frame.effectColor = new Color(1f, 0.85f, 0.3f);
            frame.effectDistance = new Vector2(4f, -4f);
            frame.enabled = false;
            columns[columnX > 0f ? 1 : 0].Add(new NavItem { Background = image, Frame = frame, Normal = image.color, Submit = submit, Step = step });
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
            AddNav(button.gameObject, button.OnPressed);
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
            AddNav(button.gameObject, button.OnPressed);
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
            // Gamepad: left / right = − / +, A = +.
            AddNav(row.gameObject, () => { change(1); UpdateLabel(); }, step => { change(step); UpdateLabel(); });
            refreshers.Add(UpdateLabel);
            UpdateLabel();
            y -= 56f;
        }

        // Pick one companion type with − / + (gamepad left / right), then "add" recruits it (or +1 level if owned).
        int pickedCompanion;
        string companionResult = "";

        void CompanionPickerRows(RectTransform parent, ref float y)
        {
            var list = upgrades.AllCompanions;
            if (list.Count == 0) return;
            CompanionData Picked() => list[Mathf.Clamp(pickedCompanion, 0, list.Count - 1)];

            StepperRow(parent, () =>
            {
                var data = Picked();
                var level = upgrades.DebugOwnedLevel(data);
                var have = level > 0 ? $"<color=#7CFC7C>{level}/{data.MaxLevel}</color>" : "<color=#888888>chưa có</color>";
                if (companionResult.Length > 0) have += $" <color=#FFD54A>({companionResult})</color>";
                return $"<size=75%>{data.DisplayName} · {have}</size>";
            }, step =>
            {
                pickedCompanion = (pickedCompanion + step + list.Count) % list.Count;
                companionResult = "";
            }, ref y);

            ActionRow(parent, "Thêm / +1 cấp đệ tử trên", () =>
            {
                companionResult = upgrades.DebugGiveCompanion(Picked());
                UnityEngine.Debug.Log($"[Debug] {Picked().DisplayName}: {companionResult}");
                Refresh();
            }, ref y);
            ActionRow(parent, "Bỏ đệ tử mới nhất", () =>
            {
                upgrades.DebugRemoveLastCompanion();
                Refresh();
            }, ref y);
        }

        // Debug: 5 ranged enemies (ink squid) around the hero, just off his reach.
        void SpawnSquids()
        {
            var config = new GameConfig();
            var type = Array.FindIndex(config.Enemies, e => e.IsRanged);
            if (type < 0) return;
            for (var k = 0; k < 5; k++)
            {
                var a = k * Mathf.PI * 2f / 5f;
                enemies.Spawn(type, player.Position + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 7f, 1f);
            }
        }

        void ActionRow(RectTransform parent, string label, Action action, ref float y)
        {
            var button = CreateButton(parent, label, new Vector2(0, 1), new Vector2(columnX + 210, y - 22), new Vector2(380, 48),
                new Color(0.15f, 0.35f, 0.6f));
            button.OnPressed = action;
            AddNav(button.gameObject, action);
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
