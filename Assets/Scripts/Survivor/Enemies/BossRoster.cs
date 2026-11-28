using System.Collections.Generic;
using UnityEngine;

namespace Survivor
{
    // Every boss of the game with its own two assets, and which one is "current":
    //   Assets/Resources/Bosses/<Id>/<Id>_Stats.asset    - HP, speed, damage (BossStats)
    //   Assets/Resources/Bosses/<Id>/<Id>_Settings.asset - skills, arena, look (BossSettings)
    // Current = the boss alive now, or else the next one in the schedule (for the HUD countdown / warning).
    // Systems read Roster.Stats / Roster.Settings, so each boss fight uses that boss's numbers.
    public sealed class BossRoster
    {
        public sealed class Boss
        {
            public int Type;               // index in GameConfig.Enemies
            public EnemyDefinition Definition;
            public BossStats Stats;
            public BossSettings Settings;
        }

        readonly List<Boss> bosses = new List<Boss>();
        readonly BossSchedule defaultSchedule;
        readonly ChapterService chapters;
        // The played chapter's boss order (Chapter > Bosses), else Resources/Bosses/BossSchedule.asset. Read live, so the
        // chapter picked on the Home screen applies.
        BossSchedule schedule => chapters.Current.Bosses != null ? chapters.Current.Bosses : defaultSchedule;
        int next;          // index of the next schedule entry
        Boss alive;

        public IReadOnlyList<Boss> All => bosses;
        public BossSchedule Schedule => schedule;

        public BossRoster(GameConfig config, ChapterService chapters)
        {
            this.chapters = chapters;
            defaultSchedule = BossSchedule.LoadOrDefault();
            for (var i = 0; i < config.Enemies.Length; i++)
            {
                var def = config.Enemies[i];
                if (!def.IsBoss) continue;
                bosses.Add(new Boss { Type = i, Definition = def, Stats = LoadStats(def.Id), Settings = LoadSettings(def.Id) });
            }
            if (bosses.Count == 0) Debug.LogError("BossRoster: GameConfig has no boss.");
        }

        public static string StatsPath(string id) => $"Bosses/{id}/{id}_Stats";
        public static string SettingsPath(string id) => $"Bosses/{id}/{id}_Settings";

        static BossStats LoadStats(string id)
        {
            var asset = Resources.Load<BossStats>(StatsPath(id));
            if (asset == null) Debug.LogWarning($"BossRoster: no {StatsPath(id)}.asset, using code defaults.");
            return asset != null ? asset : ScriptableObject.CreateInstance<BossStats>();
        }

        static BossSettings LoadSettings(string id)
        {
            var asset = Resources.Load<BossSettings>(SettingsPath(id));
            if (asset == null) Debug.LogWarning($"BossRoster: no {SettingsPath(id)}.asset, using code defaults.");
            return asset != null ? asset : ScriptableObject.CreateInstance<BossSettings>();
        }

        public Boss Find(string id)
        {
            foreach (var b in bosses)
                if (b.Definition.Id == id) return b;
            return null;
        }

        public Boss FindType(int type)
        {
            foreach (var b in bosses)
                if (b.Type == type) return b;
            return null;
        }

        // ---- Schedule ----

        // Next scheduled boss (null when none is left).
        public Boss Upcoming
        {
            get
            {
                while (next < schedule.Bosses.Count)
                {
                    var b = Find(schedule.Bosses[next].BossId);
                    if (b != null) return b;
                    Debug.LogWarning($"BossSchedule: unknown boss Id \"{schedule.Bosses[next].BossId}\" skipped.");
                    next++;
                }
                return null;
            }
        }

        // Play time of the next boss, -1 = none left.
        public float NextTime => Upcoming != null ? schedule.Bosses[next].Time : -1f;

        // The spawner takes the next boss when its time comes.
        public Boss TakeNext()
        {
            var b = Upcoming;
            if (b != null) next++;
            return b;
        }

        // ---- Current ----

        // GameController calls it every frame with the living boss's enemy type (-1 = none).
        public void SetAlive(int type) => alive = type >= 0 ? FindType(type) : null;

        public Boss Current => alive ?? Upcoming ?? (bosses.Count > 0 ? bosses[0] : null);
        public BossStats Stats => Current?.Stats;
        public BossSettings Settings => Current?.Settings;
    }
}
