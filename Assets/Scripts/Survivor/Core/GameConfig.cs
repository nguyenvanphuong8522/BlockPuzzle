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

        public GameConfig() { } // editor tools: default camera width

        [VContainer.Inject]
        public GameConfig(PlayerTuning tuning) => this.tuning = tuning;

        // Orthographic size (half the visible height) for a screen aspect (width / height).
        public float CameraSizeFor(float aspect) => ViewWidth * 0.5f / Mathf.Max(0.1f, aspect);

        // Spawning
        public int MaxEnemies = 6000; // settable so benchmarks / debug tools can raise it
        public readonly float BaseSpawnPerSecond = 2f;
        public readonly float SpawnPerSecondGrowth = 0.08f;   // extra spawns/second for every second survived
        public readonly float MaxSpawnPerSecond = 80f;
        public readonly float HordeInterval = 60f;
        public readonly float EnemyHpGrowthPerMinute = 0.35f;
        public readonly float DespawnDistance = 26f;          // enemies further than this are moved back near the player

        // Bosses appear at these times (seconds). Each one is stronger than the last.
        public readonly float[] BossTimes = { 180f, 360f, 540f };
        // Boss HP, its growth per appearance and its skills (dash, bullet burst) are in PlayerTuning ("Đấu boss").

        // Drops from normal enemies (chance per kill). Bosses always drop a chest.
        public readonly float HealAmount = 30f;
        public readonly int MaxGems = 1500;

        public readonly float PlayerRadius = 0.45f;
        public readonly float PlayerHurtCooldown = 0.25f;

        // Index = enemy type. SpriteCatalog.Enemies uses the same order (art files enemy0_..., enemy1_...).
        public readonly EnemyDefinition[] Enemies =
        {
            new EnemyDefinition("Cua Con", hp: 8, speed: 1.7f, damage: 5, radius: 0.32f, xp: 1,
                new Color(1f, 0.42f, 0.29f), unlockTime: 0f, weight: 10f),
            new EnemyDefinition("Cá Bay", hp: 5, speed: 3.0f, damage: 4, radius: 0.27f, xp: 1,
                new Color(0.23f, 0.65f, 0.95f), unlockTime: 45f, weight: 6f),
            new EnemyDefinition("Sứa Độc", hp: 18, speed: 1.5f, damage: 7, radius: 0.33f, xp: 2,
                new Color(0.71f, 0.4f, 0.94f), unlockTime: 90f, weight: 6f),
            new EnemyDefinition("Sao Biển Gai", hp: 30, speed: 2.2f, damage: 9, radius: 0.36f, xp: 2,
                new Color(1f, 0.64f, 0.23f), unlockTime: 150f, weight: 5f),
            new EnemyDefinition("Bạch Tuộc", hp: 60, speed: 1.6f, damage: 12, radius: 0.45f, xp: 4,
                new Color(1f, 0.5f, 0.71f), unlockTime: 210f, weight: 4f),
            new EnemyDefinition("Ốc Mượn Hồn", hp: 150, speed: 1.2f, damage: 16, radius: 0.62f, xp: 8,
                new Color(0.95f, 0.82f, 0.64f), unlockTime: 270f, weight: 2f),
            new EnemyDefinition("Vua Cua Khổng Lồ", hp: 2500, speed: 2.1f, damage: 25, radius: 1.3f, xp: 60,
                new Color(0.91f, 0.2f, 0.23f), unlockTime: float.MaxValue, weight: 0f, isBoss: true),
        };

        public int BossType => Enemies.Length - 1;
    }
}
