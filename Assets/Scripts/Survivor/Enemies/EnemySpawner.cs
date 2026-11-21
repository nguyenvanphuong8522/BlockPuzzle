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
        float accumulator;
        float nextHorde;
        int nextBoss;
        int bossesSpawned;

        public EnemySpawner(GameConfig config, EnemySystem enemies, Player player, GameStats stats, AudioService audio)
        {
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
            var rate = Mathf.Min(config.MaxSpawnPerSecond, config.BaseSpawnPerSecond + t * config.SpawnPerSecondGrowth);
            accumulator += rate * dt;
            while (accumulator >= 1f)
            {
                accumulator -= 1f;
                SpawnOne(PickType(t), RandomRingPoint());
            }

            if (t >= nextHorde)
            {
                nextHorde += config.HordeInterval;
                SpawnHorde(t);
            }

            if (nextBoss < config.BossTimes.Length && t >= config.BossTimes[nextBoss])
            {
                nextBoss++;
                SpawnBoss();
            }
        }

        public void SpawnBoss()
        {
            var hpMultiplier = 1f + bossesSpawned * config.BossHpGrowthPerAppearance;
            bossesSpawned++;
            enemies.Spawn(config.BossType, RandomRingPoint(), hpMultiplier);
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
                if (config.Enemies[i].UnlockTime <= t && !config.Enemies[i].IsBoss) result = i;
            return result;
        }
    }
}
