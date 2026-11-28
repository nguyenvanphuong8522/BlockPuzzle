using UnityEngine;

namespace Survivor
{
    // Spawns enemies on a ring just outside the screen. Rate and enemy HP grow with time.
    // Also brings in hordes every minute and bosses at fixed times.
    public sealed class EnemySpawner
    {
        readonly GameConfig config;
        readonly EnemySystem enemies;
        readonly Player player;
        readonly GameStats stats;
        readonly AudioService audio;
        readonly PlayerTuning tuning;
        readonly BossRoster bosses;
        readonly ChapterService chapters; // chapter difficulty: spawn rate x Spawn Rate Scale
        float accumulator;
        float nextHorde;
        int bossesSpawned;

        // True during a boss fight: no normal enemies or hordes (boss times still count).
        public bool Paused { get; set; }

        // Off while the scripted timeline (SpawnTimeline "Replace Auto Hordes") takes over.
        public bool AutoHordes { get; set; } = true;

        // Elapsed time at which the next boss appears, -1 = no boss left.
        public float NextBossTime => bosses.NextTime;

        public EnemySpawner(GameConfig config, EnemySystem enemies, Player player, GameStats stats, AudioService audio,
            PlayerTuning tuning, BossRoster bosses, ChapterService chapters)
        {
            this.tuning = tuning;
            this.bosses = bosses;
            this.chapters = chapters;
            this.config = config;
            this.enemies = enemies;
            this.player = player;
            this.stats = stats;
            this.audio = audio;
            nextHorde = config.HordeInterval;
        }

        public void Tick(float dt)
        {
            var t = stats.Elapsed;
            if (Paused)
            {
                accumulator = 0f;
                if (t >= nextHorde) nextHorde = t + config.HordeInterval; // no horde right after the fight
                return;
            }
            var rate = Mathf.Min(config.MaxSpawnPerSecond, config.BaseSpawnPerSecond + t * config.SpawnPerSecondGrowth)
                       * chapters.Current.SpawnRateScale;
            accumulator += rate * dt;
            while (accumulator >= 1f)
            {
                accumulator -= 1f;
                SpawnOne(PickType(t), RandomRingPoint());
            }

            if (t >= nextHorde)
            {
                nextHorde += config.HordeInterval;
                if (AutoHordes) SpawnHorde(t);
            }

            var bossTime = bosses.NextTime;
            if (bossTime >= 0f && t >= bossTime) SpawnBoss(bosses.TakeNext());
        }

        // Debug: the next boss of the schedule (without using up its turn).
        public void SpawnBoss() => SpawnBoss(bosses.Upcoming ?? bosses.Current);

        public void SpawnBoss(BossRoster.Boss b)
        {
            if (b == null || enemies.BossIndex >= 0) return; // one boss at a time
            // HP, speed and touch damage come from that boss's Stats asset; HP grows with every boss fight.
            var s = b.Stats;
            var hpMultiplier = s.Hp / Mathf.Max(1f, b.Definition.Hp) * (1f + bossesSpawned * s.HpGrowthPerAppearance);
            bossesSpawned++;
            var before = enemies.Count;
            enemies.Spawn(b.Type, RandomRingPoint(), hpMultiplier);
            if (enemies.Count > before) enemies.SetSpeedAndDamage(enemies.Count - 1, s.MoveSpeed, s.ContactDamage);
            audio.Play(Sfx.BossSpawn);
        }

        // Debug / stress test helper.
        public void SpawnBurst(int count)
        {
            for (var i = 0; i < count; i++) SpawnOne(PickType(stats.Elapsed), RandomRingPoint());
        }

        // A full circle of enemies closing in from every direction.
        void SpawnHorde(float t)
        {
            var count = 30 + Mathf.RoundToInt(t / 6f);
            var radius = enemies.SpawnRingRadius();
            var typeIndex = NewestUnlocked(t);
            for (var i = 0; i < count; i++)
            {
                var angle = i * Mathf.PI * 2f / count;
                SpawnOne(typeIndex, player.Position + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius);
            }
        }

        void SpawnOne(int typeIndex, Vector2 position)
        {
            var hpMultiplier = 1f + stats.Elapsed / 60f * config.EnemyHpGrowthPerMinute;
            enemies.Spawn(typeIndex, position, hpMultiplier);
        }

        Vector2 RandomRingPoint()
        {
            var angle = Random.Range(0f, Mathf.PI * 2f);
            return player.Position + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * enemies.SpawnRingRadius();
        }

        int PickType(float t)
        {
            var total = 0f;
            for (var i = 0; i < config.Enemies.Length; i++)
                if (config.Enemies[i].UnlockTime <= t) total += config.Enemies[i].Weight;

            var roll = Random.Range(0f, total);
            for (var i = 0; i < config.Enemies.Length; i++)
            {
                var def = config.Enemies[i];
                if (def.UnlockTime > t || def.Weight <= 0f) continue;
                roll -= def.Weight;
                if (roll <= 0f) return i;
            }
            return 0;
        }

        int NewestUnlocked(float t)
        {
            var result = 0;
            for (var i = 0; i < config.Enemies.Length; i++)
                if (config.Enemies[i].UnlockTime <= t && !config.Enemies[i].IsBoss && !config.Enemies[i].IsRanged) result = i; // hordes: melee only
            return result;
        }
    }
}
