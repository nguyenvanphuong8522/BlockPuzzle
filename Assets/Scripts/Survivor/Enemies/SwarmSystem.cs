using UnityEngine;

namespace Survivor
{
    // Swarm event: every Swarm Interval seconds a flock of enemies flies in a straight line from one side, right
    // across the hero, to the other side, and leaves the map. Swarm Warning Time seconds before, the HUD shows a
    // warning arrow on the edge of the screen it comes from. Numbers: PlayerTuning "Đàn quái bay ngang".
    // No swarm during a boss fight (it waits until the boss is dead).
    public sealed class SwarmSystem
    {
        readonly EnemySystem enemies;
        readonly Player player;
        readonly GameStats stats;
        readonly GameConfig config;
        readonly PlayerTuning tuning;
        readonly AudioService audio;

        float nextTime;
        bool warning;
        float warningLeft;
        Vector2 flyDir;   // direction the swarm flies
        Vector2 aimOffset;

        // For the HUD: where the coming swarm is (unit vector from the hero toward it) and how soon (0..1, 1 = now).
        public bool WarningActive => warning;
        public Vector2 WarningDirection => -flyDir;
        public float WarningProgress => tuning.SwarmWarningTime > 0f ? 1f - warningLeft / tuning.SwarmWarningTime : 1f;

        public SwarmSystem(EnemySystem enemies, Player player, GameStats stats, GameConfig config, PlayerTuning tuning,
            AudioService audio)
        {
            this.enemies = enemies;
            this.player = player;
            this.stats = stats;
            this.config = config;
            this.tuning = tuning;
            this.audio = audio;
            nextTime = tuning.SwarmFirstTime;
        }

        public void Tick(float dt)
        {
            if (!tuning.SwarmEnabled) return;
            if (enemies.BossIndex >= 0) // boss fight: no swarm; the warning (if any) is cancelled
            {
                warning = false;
                return;
            }

            if (warning)
            {
                warningLeft -= dt;
                if (warningLeft <= 0f)
                {
                    warning = false;
                    Launch();
                }
                return;
            }

            if (stats.Elapsed < nextTime) return;
            nextTime = stats.Elapsed + Mathf.Max(3f, tuning.SwarmInterval + Random.Range(-1f, 1f) * tuning.SwarmIntervalJitter);
            Warn();
        }

        // Pick the direction now, so the arrow points at where it will really come from.
        void Warn()
        {
            var a = Random.Range(0f, Mathf.PI * 2f);
            flyDir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
            aimOffset = new Vector2(-flyDir.y, flyDir.x) * Random.Range(-1f, 1f) * tuning.SwarmAimOffset;
            warningLeft = tuning.SwarmWarningTime;
            warning = true;
            audio.PlayAtPitch(Sfx.BossSpawn, 1.6f, 0.45f, 0.1f);
            if (warningLeft <= 0f)
            {
                warning = false;
                Launch();
            }
        }

        void Launch()
        {
            var type = Mathf.Clamp(tuning.SwarmEnemyType, 0, config.Enemies.Length - 1);
            if (config.Enemies[type].IsBoss) type = 0;
            var minutes = stats.Elapsed / 60f;
            var count = tuning.SwarmCount + Mathf.RoundToInt(minutes * tuning.SwarmCountPerMinute);
            var hp = (1f + minutes * config.EnemyHpGrowthPerMinute) * tuning.SwarmHpMultiplier;

            // Start just outside the screen, behind the hero along the flight path; fly twice that far, then leave.
            var startDistance = enemies.SpawnRingRadius() + 1f;
            var start = player.Position + aimOffset - flyDir * startDistance;
            var across = new Vector2(-flyDir.y, flyDir.x);
            var life = (startDistance * 2f + tuning.SwarmDepth + 2f) / tuning.SwarmSpeed;
            for (var i = 0; i < count; i++)
            {
                var pos = start + across * (Random.Range(-0.5f, 0.5f) * tuning.SwarmWidth)
                                - flyDir * Random.Range(0f, tuning.SwarmDepth);
                var speed = tuning.SwarmSpeed * Random.Range(0.93f, 1.07f);
                enemies.SpawnFlyer(type, pos, hp, flyDir * speed, life);
            }
            audio.PlayAtPitch(Sfx.Wave, 1.2f, 0.6f, 0.1f);
        }

        // Debug: warn and send a swarm right now.
        public void TriggerNow()
        {
            nextTime = stats.Elapsed + tuning.SwarmInterval;
            Warn();
        }
    }
}
