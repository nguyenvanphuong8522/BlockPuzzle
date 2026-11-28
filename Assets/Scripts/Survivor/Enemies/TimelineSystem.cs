using System.Collections.Generic;
using UnityEngine;

namespace Survivor
{
    // Plays the scripted enemy timeline (Assets/Resources/SpawnTimeline.asset): at each event's second it warns
    // (text + an arrow on the screen edge it comes from), then spawns its formation: a circle closing in, a wall,
    // a dense group, or enemies scattered around. Runs on top of the normal trickle of enemies (EnemySpawner).
    // The game clock stops during boss fights, so nothing here happens while a boss is alive.
    public sealed class TimelineSystem
    {
        sealed class State
        {
            public float NextTime;
            public int Fired;
            public bool Done;
            public bool Warned;
            public Vector2 Side; // unit vector from the hero toward where they come from
        }

        // Enemies of one event that appear over a few seconds (Spawn Over Seconds).
        sealed class Stream
        {
            public TimelineEvent Event;
            public int Type;
            public float Hp;
            public readonly List<Vector2> Offsets = new List<Vector2>(); // from the hero at spawn time
            public Vector2 Velocity;
            public float Life;
            public int Next;
            public float Timer;
            public float Interval;
        }

        readonly SpawnTimeline timeline;
        readonly EnemySystem enemies;
        readonly EnemySpawner spawner;
        readonly Player player;
        readonly GameStats stats;
        readonly GameConfig config;
        readonly AudioService audio;
        readonly FloatingTextSystem floatingText;
        readonly List<State> states = new List<State>();
        readonly List<Stream> streams = new List<Stream>();

        // For the HUD: the next directional event that is warning right now.
        public bool WarningActive { get; private set; }
        public Vector2 WarningDirection { get; private set; }
        public float WarningProgress { get; private set; }

        public TimelineSystem(SpawnTimeline timeline, EnemySystem enemies, EnemySpawner spawner, Player player,
            GameStats stats, GameConfig config, AudioService audio, FloatingTextSystem floatingText)
        {
            this.timeline = timeline;
            this.enemies = enemies;
            this.spawner = spawner;
            this.player = player;
            this.stats = stats;
            this.config = config;
            this.audio = audio;
            this.floatingText = floatingText;
            foreach (var e in timeline.Events) states.Add(new State { NextTime = e.Time });
        }

        public void Tick(float dt)
        {
            spawner.AutoHordes = !(timeline.Enabled && timeline.ReplaceAutoHordes);
            WarningActive = false;
            if (!timeline.Enabled || player.IsDead || enemies.BossIndex >= 0) return;

            var t = stats.Elapsed;
            var soonest = float.MaxValue;
            for (var i = 0; i < timeline.Events.Count && i < states.Count; i++)
            {
                var e = timeline.Events[i];
                var s = states[i];
                if (s.Done) continue;

                if (!s.Warned && t >= s.NextTime - e.WarningTime)
                    Warn(e, s);

                if (t >= s.NextTime)
                {
                    Fire(e, s);
                    s.Fired++;
                    s.Warned = false;
                    if (e.RepeatEvery > 0f && (e.RepeatTimes == 0 || s.Fired <= e.RepeatTimes)) s.NextTime += e.RepeatEvery;
                    else s.Done = true;
                    continue;
                }

                // Arrow for the soonest wall / cluster being warned about.
                if (s.Warned && ShowsUI(e) && HasDirection(e) && s.NextTime < soonest)
                {
                    soonest = s.NextTime;
                    WarningActive = true;
                    WarningDirection = s.Side;
                    WarningProgress = e.WarningTime > 0f ? 1f - (s.NextTime - t) / e.WarningTime : 1f;
                }
            }

            TickStreams(dt);
        }

        bool ShowsUI(TimelineEvent e) => timeline.ShowWarnings && e.ShowWarningUI;

        static bool HasDirection(TimelineEvent e) =>
            e.Pattern == TimelinePattern.Wall || e.Pattern == TimelinePattern.Cluster;

        void Warn(TimelineEvent e, State s)
        {
            s.Warned = true;
            s.Side = PickSide(e.Side);
            if (ShowsUI(e) && !string.IsNullOrEmpty(e.Announce)) floatingText.Show(Loc.Text(e.Announce), e.AnnounceColor);
            if (e.WarningSound && e.WarningTime > 0f) audio.PlayAtPitch(Sfx.BossSpawn, 1.6f, 0.45f, 0.1f);
        }

        static Vector2 PickSide(TimelineSide side)
        {
            switch (side)
            {
                case TimelineSide.Top: return Vector2.up;
                case TimelineSide.Bottom: return Vector2.down;
                case TimelineSide.Left: return Vector2.left;
                case TimelineSide.Right: return Vector2.right;
                case TimelineSide.LeftOrRight: return Random.value < 0.5f ? Vector2.left : Vector2.right;
                case TimelineSide.TopOrBottom: return Random.value < 0.5f ? Vector2.up : Vector2.down;
                default:
                    var a = Random.Range(0f, Mathf.PI * 2f);
                    return new Vector2(Mathf.Cos(a), Mathf.Sin(a));
            }
        }

        int ResolveType(TimelineEnemy enemy)
        {
            if (enemy == TimelineEnemy.Auto)
            {
                var result = 0;
                for (var i = 0; i < config.Enemies.Length; i++)
                {
                    var d = config.Enemies[i];
                    if (d.UnlockTime <= stats.Elapsed && !d.IsBoss && !d.IsRanged) result = i;
                }
                return result;
            }
            var type = Mathf.Clamp((int)enemy, 0, config.Enemies.Length - 1);
            return config.Enemies[type].IsBoss ? 0 : type;
        }

        void Fire(TimelineEvent e, State s)
        {
            if (!s.Warned) s.Side = PickSide(e.Side);
            var stream = new Stream
            {
                Event = e,
                Type = ResolveType(e.Enemy),
                Hp = (1f + stats.Elapsed / 60f * config.EnemyHpGrowthPerMinute) * e.HpMultiplier,
            };
            var ring = enemies.SpawnRingRadius();
            var dist = e.Distance > 0f ? e.Distance : ring;
            var side = s.Side;
            var along = new Vector2(-side.y, side.x);

            switch (e.Pattern)
            {
                case TimelinePattern.Encircle:
                {
                    var start = Random.Range(0f, Mathf.PI * 2f);
                    for (var k = 0; k < e.Count; k++)
                    {
                        var a = start + k * Mathf.PI * 2f / e.Count;
                        stream.Offsets.Add(new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * dist);
                    }
                    break;
                }
                case TimelinePattern.Wall:
                {
                    for (var r = 0; r < e.Rows; r++)
                    for (var k = 0; k < e.Count; k++)
                    {
                        var x = (k - (e.Count - 1) * 0.5f) * e.Spacing + (r % 2) * e.Spacing * 0.5f; // rows staggered
                        stream.Offsets.Add(side * (dist + r * e.Spacing) + along * x);
                    }
                    stream.Life = (dist * 2f + e.Rows * e.Spacing + 2f) / e.MarchSpeed;
                    break;
                }
                case TimelinePattern.Cluster:
                {
                    var spread = e.Spacing * Mathf.Sqrt(e.Count) * 0.5f;
                    var center = side * (dist + spread);
                    for (var k = 0; k < e.Count; k++)
                        stream.Offsets.Add(center + Random.insideUnitCircle * spread);
                    stream.Life = (dist * 2f + spread * 2f + 2f) / e.MarchSpeed;
                    break;
                }
                default: // Scatter
                    for (var k = 0; k < e.Count; k++)
                    {
                        var a = Random.Range(0f, Mathf.PI * 2f);
                        stream.Offsets.Add(new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * dist);
                    }
                    break;
            }

            stream.Velocity = -side * e.MarchSpeed;
            stream.Interval = e.SpawnOverSeconds > 0f ? e.SpawnOverSeconds / Mathf.Max(1, stream.Offsets.Count) : 0f;
            if (stream.Interval <= 0f)
                while (stream.Next < stream.Offsets.Count) SpawnNext(stream);
            else
                streams.Add(stream);
            audio.PlayAtPitch(Sfx.Wave, 1.1f, 0.6f, 0.1f);
        }

        void TickStreams(float dt)
        {
            for (var i = streams.Count - 1; i >= 0; i--)
            {
                var s = streams[i];
                s.Timer -= dt;
                while (s.Timer <= 0f && s.Next < s.Offsets.Count)
                {
                    SpawnNext(s);
                    s.Timer += s.Interval;
                }
                if (s.Next >= s.Offsets.Count) streams.RemoveAt(i);
            }
        }

        void SpawnNext(Stream s)
        {
            var e = s.Event;
            var pos = player.Position + s.Offsets[s.Next++];
            var marches = e.March && (e.Pattern == TimelinePattern.Wall || e.Pattern == TimelinePattern.Cluster);
            if (e.Elite) enemies.SpawnElite(s.Type, pos, s.Hp);
            else if (marches) enemies.SpawnFlyer(s.Type, pos, s.Hp, s.Velocity * Random.Range(0.97f, 1.03f), s.Life);
            else enemies.Spawn(s.Type, pos, s.Hp);
        }

        // Debug: the next event warns now and starts after its warning. Pressing again takes the one after it
        // (events already about to start are skipped).
        public string TriggerNext()
        {
            var best = -1;
            for (var i = 0; i < states.Count && i < timeline.Events.Count; i++)
            {
                var st = states[i];
                if (st.Done || st.NextTime <= stats.Elapsed + timeline.Events[i].WarningTime + 0.01f) continue;
                if (best < 0 || st.NextTime < states[best].NextTime) best = i;
            }
            if (best < 0) return "hết sự kiện";
            var e = timeline.Events[best];
            var s = states[best];
            s.NextTime = stats.Elapsed + e.WarningTime;
            s.Warned = false;
            return e.Note;
        }
    }
}
