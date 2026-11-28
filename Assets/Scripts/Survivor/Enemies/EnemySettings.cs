using UnityEngine;

namespace Survivor
{
    // Rules shared by every normal enemy type: Assets/Resources/EnemySettings.asset (live-tunable in Play mode) -
    // spawn rate, elite schedule / look / rewards, ranged movement, swarm, hit squash and knockback.
    // Each type has its own Assets/Resources/Enemies/<Id>/<Id>_Settings.asset (when / how often it appears, its
    // knockback, its shots) and <Id>_Stats.asset. The boss has BossStats / BossSettings.
    [CreateAssetMenu(menuName = "Survivor/Enemy Settings (shared)", fileName = "EnemySettings")]
    public sealed class EnemySettings : ScriptableObject
    {
        [Header("Xuất hiện")]
        [Tooltip("Enemies per second at the start of the run...")]
        [Min(0f)] public float BaseSpawnPerSecond = 2f;
        [Tooltip("...plus this many more per second for every second survived...")]
        [Min(0f)] public float SpawnPerSecondGrowth = 0.08f;
        [Tooltip("...up to this many per second.")]
        [Min(0f)] public float MaxSpawnPerSecond = 80f;
        [Tooltip("Seconds between two automatic hordes (a circle of enemies closing in). Off while the spawn timeline replaces them.")]
        [Min(5f)] public float HordeInterval = 60f;

        [Header("Quái tinh anh (elite)")]
        [Tooltip("Now and then a big, tough copy of a normal enemy comes in: tinted, with a glow and its own health bar. " +
                 "It drops a chest and a pile of gems.")]
        public bool EliteEnabled = true;
        [Tooltip("Seconds into the run before the first elite.")]
        [Min(0f)] public float EliteFirstTime = 75f;
        [Tooltip("Average seconds between two elites (± Elite Interval Jitter).")]
        [Min(3f)] public float EliteInterval = 50f;
        [Min(0f)] public float EliteIntervalJitter = 10f;
        [Tooltip("Elites per appearance at the start; +1 every Elite Extra Every minutes. 0 = never more.")]
        [Min(1)] public int EliteCount = 1;
        [Min(0f)] public float EliteExtraEveryMinutes = 4f;
        [Tooltip("How much it gets pushed back by hits (0 = not at all, 1 = like a normal enemy).")]
        [Range(0f, 1f)] public float EliteKnockbackScale = 0.25f;
        [Tooltip("Tint of the elite sprite.")]
        public Color EliteTint = new Color(1f, 0.78f, 0.55f);
        [Tooltip("Glow under its feet (pulses).")]
        public Color EliteGlow = new Color(1f, 0.2f, 0.05f, 0.65f);
        [Tooltip("Health bar over its head: width (× its size), height, colors.")]
        [Min(0f)] public float EliteBarWidth = 1.6f;
        [Min(0.02f)] public float EliteBarHeight = 0.12f;
        public Color EliteBarColor = new Color(1f, 0.3f, 0.25f);
        public Color EliteBarBackground = new Color(0.1f, 0.1f, 0.1f, 0.8f);
        [Tooltip("Text over the hero when an elite comes in (empty = none).")]
        public string EliteAnnounce = "announce.elite";
        public Color EliteAnnounceColor = new Color(1f, 0.55f, 0.25f);
        [Header("Quái tinh anh: phần thưởng")]
        [Tooltip("Chance (percent) to drop a chest.")]
        [Range(0f, 100f)] public float EliteChestChance = 0f;
        [Tooltip("Extra experience dropped as a line of gems toward the hero.")]
        [Min(0)] public int EliteBonusXp = 30;
        [Min(1)] public int EliteBonusGems = 10;
        [Tooltip("Chance (percent) to also drop a drumstick (heal).")]
        [Range(0f, 100f)] public float EliteHealChance = 0f;

        [Header("Quái bắn xa (chung; khoảng cách, nhịp bắn, đạn: ở Settings của từng loại)")]
        [Tooltip("Comfort zone around Keep Distance: inside it, it stops walking in/out and circles the hero.")]
        [Min(0.05f)] public float RangedDistanceSlack = 0.8f;
        [Tooltip("Speed while circling around the hero, compared to its walking speed (0 = stands still).")]
        [Range(0f, 1f)] public float RangedStrafeSpeed = 0.45f;
        [Tooltip("Speed while backing off, compared to its walking speed.")]
        [Range(0f, 2f)] public float RangedRetreatSpeed = 0.8f;
        [Tooltip("Only shoot when it is on the screen (fair: you can see who is shooting).")]
        public bool RangedOnlyOnScreen = true;

        [Header("Đàn quái bay ngang")]
        [Tooltip("A flock of enemies flies in a straight line from one side, right across the hero, to the other side. " +
                 "A warning arrow shows on that edge of the screen first.")]
        public bool SwarmEnabled = true;
        [Tooltip("Seconds into the run before the first swarm.")]
        [Min(0f)] public float SwarmFirstTime = 40f;
        [Tooltip("Average seconds between two swarms (± Swarm Interval Jitter).")]
        [Min(3f)] public float SwarmInterval = 35f;
        [Min(0f)] public float SwarmIntervalJitter = 8f;
        [Tooltip("How long the warning shows before the swarm comes in.")]
        [Min(0f)] public float SwarmWarningTime = 2f;
        [Tooltip("Show the warning arrow on the screen edge before the swarm comes. Off = it just arrives (still after Swarm Warning Time).")]
        public bool SwarmShowWarning = true;
        [Tooltip("Enemy type of the swarm (GameConfig order): 0 crab, 1 flying fish, 2 jellyfish, 3 starfish, 4 octopus, 5 hermit.")]
        [Min(0)] public int SwarmEnemyType = 1;
        [Tooltip("Enemies in one swarm at the start of the run...")]
        [Min(1)] public int SwarmCount = 22;
        [Tooltip("...plus this many more for every minute survived.")]
        [Min(0f)] public float SwarmCountPerMinute = 4f;
        [Tooltip("Flight speed (world units per second).")]
        [Min(0.5f)] public float SwarmSpeed = 7f;
        [Tooltip("Width of the swarm across its path (world units).")]
        [Min(0f)] public float SwarmWidth = 5f;
        [Tooltip("Length of the swarm along its path (world units).")]
        [Min(0f)] public float SwarmDepth = 4f;
        [Tooltip("The swarm's path passes this far from the hero at most (random). 0 = straight through the hero.")]
        [Min(0f)] public float SwarmAimOffset = 1f;

        [Header("Co rúm khi trúng đòn")]
        [Tooltip("How long an enemy (or boss) squashes when hit and survives (seconds). The killing hit never squashes.")]
        [Min(0f)] public float HitSquashTime = 0.14f;
        [Tooltip("How much it squashes: 0.12 = 12% shorter (and a bit wider) at the peak. 0 = off.")]
        [Range(0f, 0.5f)] public float HitSquashAmount = 0.12f;

        [Header("Đẩy lùi quái (knockback)")]
        [Tooltip("Only when the hit does NOT kill. Multiplies every weapon's push (0 = no knockback).")]
        [Min(0f)] public float KnockbackStrength = 2.5f;
        [Tooltip("How fast the push dies out. Lower = enemies slide back further.")]
        [Min(0.5f)] public float KnockbackDecay = 7f;
        [Tooltip("Cap on the push speed, so fast-firing weapons can't launch enemies across the screen.")]
        [Min(0f)] public float KnockbackMaxSpeed = 9f;
        [Tooltip("While pushed faster than this, the enemy stops walking toward the hero (0 = it never stops).")]
        [Min(0f)] public float KnockbackStunSpeed = 2f;

        public static EnemySettings LoadOrDefault()
        {
            var settings = Resources.Load<EnemySettings>("EnemySettings");
            return settings != null ? settings : CreateInstance<EnemySettings>();
        }
    }
}
