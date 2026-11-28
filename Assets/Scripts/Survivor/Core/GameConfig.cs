using UnityEngine;

namespace Survivor
{
    // All tuning numbers in one place.
    public sealed class GameConfig
    {
        public readonly float SurviveSeconds = 600f;
        // Portrait game: the camera keeps this much world WIDTH visible on every screen shape
        // (taller phones simply see more above and below). Tuned live in PlayerTuning (Camera View Width).
        public const float DefaultViewWidth = 13f;
        public float ViewWidth => tuning != null ? Mathf.Max(4f, tuning.CameraViewWidth) : DefaultViewWidth;

        readonly PlayerTuning tuning;
        readonly EnemyStats enemyStats;
        readonly EnemySettings enemySettings;

        public GameConfig() { } // editor tools: code defaults

        // In the game: every normal enemy type reads its numbers live from its own two assets (Resources/Enemies/<Id>/).
        [VContainer.Inject]
        public GameConfig(PlayerTuning tuning, EnemyStats enemyStats, EnemySettings enemySettings)
        {
            this.tuning = tuning;
            this.enemyStats = enemyStats;
            this.enemySettings = enemySettings;
            foreach (var def in Enemies)
                if (!def.IsBoss) def.LoadAssets();
        }

        // Orthographic size (half the visible height) for a screen aspect (width / height).
        public float CameraSizeFor(float aspect) => ViewWidth * 0.5f / Mathf.Max(0.1f, aspect);

        // Spawning
        public int MaxEnemies = 6000; // settable so benchmarks / debug tools can raise it
        // Spawn rate and horde interval: EnemySettings.asset; HP growth: EnemyStats.asset (code defaults when not linked).
        public float BaseSpawnPerSecond => enemySettings != null ? enemySettings.BaseSpawnPerSecond : 2f;
        public float SpawnPerSecondGrowth => enemySettings != null ? enemySettings.SpawnPerSecondGrowth : 0.08f; // per second survived
        public float MaxSpawnPerSecond => enemySettings != null ? enemySettings.MaxSpawnPerSecond : 80f;
        public float HordeInterval => enemySettings != null ? enemySettings.HordeInterval : 60f;
        public float EnemyHpGrowthPerMinute => enemyStats != null ? enemyStats.HpGrowthPerMinute : 0.35f;
        public readonly float DespawnDistance = 26f;          // enemies further than this are moved back near the player

        // Bosses: which one comes when = Resources/Bosses/BossSchedule.asset; each boss's stats / skills / arena =
        // Resources/Bosses/<Id>/<Id>_Stats.asset and _Settings.asset (see BossRoster).

        // Drops from normal enemies (chance per kill). Bosses always drop a chest.
        public readonly float HealAmount = 30f;
        public readonly int MaxGems = 1500;

        public readonly float PlayerRadius = 0.45f;
        public readonly float PlayerHurtCooldown = 0.25f;

        // Index = enemy type. SpriteCatalog.Enemies uses the same order (art files enemy0_..., enemy1_...).
        public readonly EnemyDefinition[] Enemies =
        {
            new EnemyDefinition("CuaCon", "Cua Con", hp: 8, speed: 1.7f, damage: 5, radius: 0.32f, xp: 1,
                new Color(1f, 0.42f, 0.29f), unlockTime: 0f, weight: 10f),
            new EnemyDefinition("CaBay", "Cá Bay", hp: 5, speed: 3.0f, damage: 4, radius: 0.27f, xp: 1,
                new Color(0.23f, 0.65f, 0.95f), unlockTime: 45f, weight: 6f),
            new EnemyDefinition("SuaDoc", "Sứa Độc", hp: 18, speed: 1.5f, damage: 7, radius: 0.33f, xp: 2,
                new Color(0.71f, 0.4f, 0.94f), unlockTime: 90f, weight: 6f),
            new EnemyDefinition("SaoBienGai", "Sao Biển Gai", hp: 30, speed: 2.2f, damage: 9, radius: 0.36f, xp: 2,
                new Color(1f, 0.64f, 0.23f), unlockTime: 150f, weight: 5f),
            new EnemyDefinition("BachTuoc", "Bạch Tuộc", hp: 60, speed: 1.6f, damage: 12, radius: 0.45f, xp: 4,
                new Color(1f, 0.5f, 0.71f), unlockTime: 210f, weight: 4f),
            new EnemyDefinition("OcMuonHon", "Ốc Mượn Hồn", hp: 150, speed: 1.2f, damage: 16, radius: 0.62f, xp: 8,
                new Color(0.95f, 0.82f, 0.64f), unlockTime: 270f, weight: 2f),
            new EnemyDefinition("VuaCua", "Vua Cua Khổng Lồ", hp: 2500, speed: 2.1f, damage: 25, radius: 1.45f, xp: 60,
                new Color(0.91f, 0.2f, 0.23f), unlockTime: float.MaxValue, weight: 0f, isBoss: true),
            // Added after the boss so the older types keep their numbers (drop settings, art file names).
            new EnemyDefinition("MucPhunMuc", "Mực Phun Mực", hp: 24, speed: 1.9f, damage: 6, radius: 0.36f, xp: 3,
                new Color(0.13f, 0.72f, 0.66f), unlockTime: 120f, weight: 3f, ranged: true),
            // Boss 2 (art enemy8_hammerhead). Bosses: stats / skills in Resources/Bosses/<Id>/, order in BossSchedule.asset.
            new EnemyDefinition("CaMapBua", "Cá Mập Búa", hp: 4000, speed: 2.4f, damage: 30, radius: 1.5f, xp: 90,
                new Color(0.36f, 0.49f, 0.6f), unlockTime: float.MaxValue, weight: 0f, isBoss: true),
            // Boss 3 (art enemy9_mantisshrimp).
            new EnemyDefinition("TomTit", "Tôm Tít Khổng Lồ", hp: 6000, speed: 2.6f, damage: 35, radius: 1.55f, xp: 120,
                new Color(0.18f, 0.66f, 0.43f), unlockTime: float.MaxValue, weight: 0f, isBoss: true),
        };

        // Index of the enemy type with this Id (-1 = none).
        public int TypeOf(string id) => System.Array.FindIndex(Enemies, e => e.Id == id);
    }
}
