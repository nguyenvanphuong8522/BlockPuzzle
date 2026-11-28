using System.Collections.Generic;
using UnityEngine;

namespace Survivor
{
    // Elite enemies: every Elite Interval seconds a big, tough copy of one of the unlocked enemy types comes in from
    // off screen (EnemySystem.SpawnElite: bigger, more health and damage, tinted, barely pushed back). It has a pulsing
    // glow under its feet and its own health bar; killing it drops a chest and a pile of gems (RewardDropSystem).
    // Numbers: EnemySettings "Quái tinh anh". No elites during a boss fight.
    public sealed class EliteSystem
    {
        readonly EnemySystem enemies;
        readonly Player player;
        readonly GameStats stats;
        readonly GameConfig config;
        readonly EnemySettings tuning;
        readonly AudioService audio;
        readonly FloatingTextSystem floatingText;
        readonly List<int> types = new List<int>(8);
        float nextTime;

        public EliteSystem(EnemySystem enemies, Player player, GameStats stats, GameConfig config, EnemySettings tuning,
            AudioService audio, FloatingTextSystem floatingText)
        {
            this.enemies = enemies;
            this.player = player;
            this.stats = stats;
            this.config = config;
            this.tuning = tuning;
            this.audio = audio;
            this.floatingText = floatingText;
            nextTime = tuning.EliteFirstTime;
        }

        public void Tick(float dt)
        {
            if (!tuning.EliteEnabled || enemies.BossIndex >= 0) return;
            if (stats.Elapsed < nextTime) return;
            nextTime = stats.Elapsed + Mathf.Max(3f, tuning.EliteInterval + Random.Range(-1f, 1f) * tuning.EliteIntervalJitter);
            SpawnNow();
        }

        // Debug: spawn the elites right now.
        public void SpawnNow()
        {
            var minutes = stats.Elapsed / 60f;
            var extra = tuning.EliteExtraEveryMinutes > 0f ? Mathf.FloorToInt(minutes / tuning.EliteExtraEveryMinutes) : 0;
            var n = tuning.EliteCount + extra;
            var hp = 1f + minutes * config.EnemyHpGrowthPerMinute;
            var spawned = 0;
            for (var k = 0; k < n; k++)
            {
                var type = PickType();
                var a = Random.Range(0f, Mathf.PI * 2f);
                var at = player.Position + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * enemies.SpawnRingRadius();
                if (enemies.SpawnElite(type, at, hp) >= 0) spawned++;
            }
            if (spawned == 0) return;
            if (!string.IsNullOrEmpty(tuning.EliteAnnounce)) floatingText.Show(Loc.Text(tuning.EliteAnnounce), tuning.EliteAnnounceColor);
            audio.PlayAtPitch(Sfx.BossSpawn, 1.25f, 0.6f, 0.1f);
        }

        // One of the types unlocked so far (newer ones more often). Never the boss.
        int PickType()
        {
            types.Clear();
            var t = stats.Elapsed;
            for (var i = 0; i < config.Enemies.Length; i++)
            {
                var def = config.Enemies[i];
                if (def.IsBoss || def.UnlockTime > t || !def.Settings.CanBeElite) continue;
                types.Add(i);
            }
            if (types.Count == 0) return 0;
            // Bias toward the newest: pick from the last half of the unlocked list half of the time.
            var from = Random.value < 0.5f ? types.Count / 2 : 0;
            return types[Random.Range(from, types.Count)];
        }

        // Glow under every elite (under the enemies) and a health bar over its head.
        public void Draw(InstancedRenderer renderer)
        {
            var pulse = 0.75f + 0.25f * Mathf.Sin(Time.time * 6f);
            for (var i = 0; i < enemies.Count; i++)
            {
                if (!enemies.IsElite(i) || !enemies.IsAlive(i)) continue;
                var p = enemies.Position[i];
                var r = enemies.Radius[i];
                var glow = tuning.EliteGlow;
                glow.a *= pulse;
                var feet = new Vector2(p.x, p.y - r * 0.85f);
                renderer.Ellipse(RenderLayer.Shadows, feet, new Vector2(r * 2.8f, r * 1f) * (0.95f + 0.1f * pulse), glow);
                renderer.Ring(RenderLayer.Shadows, feet, r * 1.4f, 0.12f, new Color(glow.r, glow.g, glow.b, Mathf.Min(1f, glow.a * 1.4f)));

                if (tuning.EliteBarWidth <= 0f) continue;
                var barCenter = new Vector2(p.x, p.y + r * 1.45f);
                var size = new Vector2(r * tuning.EliteBarWidth, tuning.EliteBarHeight);
                renderer.Rect(RenderLayer.Overlay, barCenter, size + new Vector2(0.04f, 0.04f), Color.black);
                renderer.Bar(RenderLayer.Overlay, barCenter, size, Mathf.Clamp01(enemies.Hp[i] / Mathf.Max(1f, enemies.MaxHp(i))),
                    tuning.EliteBarBackground, tuning.EliteBarColor);
            }
        }
    }
}
