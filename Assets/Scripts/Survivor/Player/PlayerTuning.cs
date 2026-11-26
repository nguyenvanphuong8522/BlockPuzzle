using UnityEngine;

namespace Survivor
{
    // Companion types that can be picked in the Inspector. Add a line here and in UpgradeService.Create for a new one.
    public enum CompanionKind
    {
        [InspectorName("Khỉ Ném Chuối")] Monkey,
        [InspectorName("Rái Cá Kiếm Sĩ")] SwordOtter,
        [InspectorName("Rùa Pháp Sư")] TurtleMage,
        [InspectorName("Xạ Thủ Nhí")] Gunner,
        [InspectorName("Ếch Ná Thun")] SlingFrog,
        [InspectorName("Lợn Cầm Trùy")] PigMace,
        [InspectorName("Tề Thiên (hợp thể)")] Wukong,
        [InspectorName("Gấu Súng Săn")] BearRifle,
        [InspectorName("Cao Bồi Súng Máy (hợp thể)")] Cowboy,
        [InspectorName("Sư Thầy Gõ Mõ")] Monk,
        [InspectorName("Chí Phèo")] ChiPheo,
    }

    [System.Serializable]
    public struct StartingCompanion
    {
        public CompanionKind Kind;
        [Range(1, Companion.MaxLevel)] public int Level;
    }

    // The player's starting stats. Edit Assets/Resources/PlayerTuning.asset in the Inspector:
    // changes apply immediately, even in Play Mode (upgrades are applied on top of these values).
    [CreateAssetMenu(menuName = "Survivor/Player Tuning", fileName = "PlayerTuning")]
    public sealed class PlayerTuning : ScriptableObject
    {
        [Min(1f)] public float MaxHp = 100f;
        [Min(0f)] public float MoveSpeed = 4.5f;
        [Tooltip("Gems inside this radius start flying to the player. Turn on \"Hiện tầm hút\" in the debug panel (F3) to see it.")]
        [Min(0f)] public float MagnetRadius = 1.6f;
        [Tooltip("Damage multiplier")]
        [Min(0f)] public float Might = 1f;
        [Tooltip("Cooldown multiplier (lower = faster)")]
        [Min(0.05f)] public float Haste = 1f;
        [Tooltip("Size multiplier for area weapons")]
        [Min(0.1f)] public float Area = 1f;

        [Header("Camera")]
        [Tooltip("How much of the world is visible across the screen (world units). Bigger = camera further out, " +
                 "you see more. The height follows the screen shape. Enemies keep spawning just outside the view.")]
        [Min(4f)] public float CameraViewWidth = GameConfig.DefaultViewWidth;

        [Header("Thanh máu hero")]
        [Tooltip("Width of the HP bar under the hero (world units).")]
        [Min(0.1f)] public float HpBarWidth = 0.92f;
        [Tooltip("Thickness of the HP bar (world units).")]
        [Min(0.02f)] public float HpBarHeight = 0.18f;
        [Tooltip("Black outline around the bar (world units). 0 = none.")]
        [Min(0f)] public float HpBarOutline = 0.025f;
        public Color HpBarOutlineColor = Color.black;
        public Color HpBarBackground = new Color(0.15f, 0.15f, 0.15f, 0.85f);
        [Tooltip("Color when HP is above Hp Bar Mid Below.")]
        public Color HpBarHighColor = new Color(0.3f, 0.85f, 0.3f);
        [Tooltip("Color when HP is at or below this fraction (0.5 = 50%).")]
        [Range(0f, 1f)] public float HpBarMidBelow = 0.5f;
        public Color HpBarMidColor = new Color(1f, 0.6f, 0.15f);
        [Tooltip("Color when HP is at or below this fraction (0.25 = 25%).")]
        [Range(0f, 1f)] public float HpBarLowBelow = 0.25f;
        public Color HpBarLowColor = new Color(0.95f, 0.2f, 0.2f);

        [Header("Hiệu ứng hồi máu")]
        [Tooltip("How many plus signs float up each time the hero heals.")]
        [Range(0, 40)] public int HealFxCount = 12;
        [Tooltip("How long each plus sign lives (seconds).")]
        [Min(0.1f)] public float HealFxDuration = 0.9f;
        [Tooltip("How fast they rise (world units per second).")]
        public float HealFxRise = 1.6f;
        [Tooltip("Size of a plus sign (world units).")]
        [Min(0.02f)] public float HealFxSize = 0.3f;
        [Tooltip("How wide around the hero they appear (world units).")]
        [Min(0f)] public float HealFxSpread = 0.55f;
        [Tooltip("Tint (white = original green art).")]
        public Color HealFxTint = Color.white;

        [Header("Đệ tử (companions)")]
        [Tooltip("Distance from the hero to each companion. They share a circle: 1 = above the head, 2 = above + below, " +
                 "3 = every 120 degrees, and so on.")]
        [Min(0f)] public float CompanionDistance = 1.8f;
        [Tooltip("Squashes the circle vertically for a top-down look (1 = perfect circle).")]
        [Range(0.3f, 1f)] public float CompanionCircleSquash = 0.85f;
        [Tooltip("How fast companions slide to their new places when one joins (higher = snappier).")]
        [Min(0.1f)] public float CompanionRegroupSpeed = 8f;

        [Header("Đệ tử khởi đầu")]
        [Tooltip("Companions the hero has when a run starts (up to 6). Empty = start alone. " +
                 "Takes effect on the next run (press Play again / restart).")]
        public StartingCompanion[] StartingCompanions = { new StartingCompanion { Kind = CompanionKind.Gunner, Level = 1 } };

        [Header("Kinh nghiệm lên cấp")]
        [Tooltip("Experience needed for each level-up. Element 0 = level 1 -> 2, element 1 = level 2 -> 3, and so on. " +
                 "Add more elements for more levels; after the last one each level needs Xp Growth After Table more.")]
        public int[] XpToLevelUp = DefaultXpTable();
        [Tooltip("Past the end of the table, every next level needs this much more experience than the previous one.")]
        [Min(0)] public int XpGrowthAfterTable = 11;

        [Header("Lên cấp: đệ tử / đồ")]
        [Tooltip("Every Nth level-up shows ITEMS instead of companions. 3 = level-ups 1,2 companions, 3 items, " +
                 "4,5 companions, 6 items... 0 = always companions. Chests always offer companions. " +
                 "The companions and items themselves: Assets/Resources/Upgrades (one asset each).")]
        [Min(0)] public int ItemChoiceEvery = 3;

        // Default table for the first 30 levels (the original formula, scaled by XpScale).
        // 0.7 = 30% less than the original curve: since every 3rd level-up is an item, companions still come often.
        const float XpScale = 0.7f;

        static int[] DefaultXpTable()
        {
            var table = new int[30];
            for (var level = 1; level <= table.Length; level++)
                table[level - 1] = Mathf.Max(3, Mathf.RoundToInt(XpScale * (5 + (level - 1) * 6 + Mathf.Max(0, level - 20) * 10)));
            return table;
        }

        // Experience needed to go from `level` to level + 1.
        public int XpRequired(int level)
        {
            var table = XpToLevelUp;
            if (table == null || table.Length == 0) return 5 + (level - 1) * 6;
            if (level <= table.Length) return Mathf.Max(1, table[level - 1]);
            return Mathf.Max(1, table[table.Length - 1] + (level - table.Length) * XpGrowthAfterTable);
        }

        [Header("Dấu hiệu hero")]
        [Tooltip("Size of the blue triangle over the hero's head (world units).")]
        [Min(0f)] public float MarkerSize = 0.3f;
        [Tooltip("Height of the triangle above the hero's center (the head top is at ~0.85).")]
        public float MarkerHeight = 1.02f;
        [Tooltip("Tint for the triangle. White = the original blue art.")]
        public Color MarkerTint = Color.white;

        [Header("Đấu boss")]
        [Tooltip("HP of the first boss.")]
        [Min(1f)] public float BossHp = 2500f;
        [Tooltip("Extra HP for each later boss: 0.6 = +60% per appearance (2nd boss = 1.6x, 3rd = 2.2x).")]
        [Min(0f)] public float BossHpGrowthPerAppearance = 0.6f;
        [Tooltip("The \"boss coming\" warning shows this many seconds before a boss appears. 0 = no warning.")]
        [Min(0f)] public float BossWarningSeconds = 5f;
        [Tooltip("Width (left-right) of the rectangular arena that closes in around the hero when a boss appears " +
                 "(world units). Keep it a bit smaller than Camera View Width so both side walls are on screen.")]
        [UnityEngine.Serialization.FormerlySerializedAs("BossArenaSize")]
        [Min(4f)] public float BossArenaWidth = 11f;
        [Tooltip("Height (up-down) of the boss arena (world units). Portrait screens show more height than width.")]
        [UnityEngine.Serialization.FormerlySerializedAs("BossArenaSize")]
        [Min(4f)] public float BossArenaHeight = 15f;
        [Tooltip("Damage when the hero touches the wall.")]
        [Min(0f)] public float BossArenaWallDamage = 8f;
        [Tooltip("How far the wall pushes the hero back in when touched.")]
        [Min(0f)] public float BossArenaWallPush = 0.6f;
        [Min(0.02f)] public float BossArenaWallThickness = 0.18f;
        public Color BossArenaWallColor = new Color(1f, 0.25f, 0.2f, 0.9f);
        [Tooltip("Height of the barbed-wire fence art drawn on the wall (world units).")]
        [Min(0.05f)] public float BossArenaFenceHeight = 0.7f;
        [Tooltip("Length of one fence segment (one post + wires). The sides are split into whole segments.")]
        [Min(0.2f)] public float BossArenaFenceSegment = 1.8f;
        [Tooltip("Tint of the fence art (white = original colors).")]
        public Color BossArenaFenceTint = Color.white;
        [Tooltip("The boss experience drops as this many gems in a line.")]
        [Min(1)] public int BossTrailGems = 30;

        [Header("Kỹ năng boss")]
        [Tooltip("Seconds the boss walks between two skills. Skills alternate: dash, bullet burst, dash...")]
        [Min(0.5f)] public float BossSkillInterval = 3.5f;
        [Tooltip("Seconds before the first skill after the boss appears.")]
        [Min(0f)] public float BossFirstSkillDelay = 2.5f;

        [Tooltip("Lao: warning time. The dark red bar fills the path from start to end in this many seconds, then the boss dashes.")]
        [Min(0.1f)] public float BossDashCastTime = 1.1f;
        [Tooltip("Lao: dash speed (world units per second).")]
        [Min(1f)] public float BossDashSpeed = 16f;
        [Tooltip("Lao: dash length (world units). The boss aims at where the hero is when the warning starts.")]
        [Min(1f)] public float BossDashDistance = 9f;
        [Tooltip("Lao: width of the warning path, times the boss body width.")]
        [Min(0.2f)] public float BossDashWidthScale = 1f;
        [Tooltip("Lao: bottom layer of the warning (whole path).")]
        public Color BossDashWarnColor = new Color(1f, 0.25f, 0.2f, 0.22f);
        [Tooltip("Lao: top layer that runs from start to end during the warning.")]
        public Color BossDashFillColor = new Color(0.9f, 0.05f, 0.05f, 0.5f);
        [Tooltip("Lao: damage when the dashing boss runs into the hero (its normal touch damage applies otherwise).")]
        [Min(0f)] public float BossDashDamage = 25f;

        [Tooltip("Đạn tỏa: how long the boss winds up before the first wave (seconds).")]
        [Min(0f)] public float BossBurstCastTime = 0.6f;
        [Tooltip("Đạn tỏa: number of waves.")]
        [Range(1, 10)] public int BossBurstWaves = 2;
        [Tooltip("Đạn tỏa: seconds between two waves.")]
        [Min(0.05f)] public float BossBurstWaveDelay = 1f;
        [Tooltip("Đạn tỏa: bullets per wave, spread evenly over 360 degrees (every other wave is turned half a step).")]
        [Range(3, 64)] public int BossBurstBullets = 16;
        [Min(0.5f)] public float BossBulletSpeed = 5f;
        [Min(0f)] public float BossBulletDamage = 10f;
        [Tooltip("Bullet size (world units, radius).")]
        [Min(0.05f)] public float BossBulletRadius = 0.22f;
        [Tooltip("How long a bullet flies before disappearing (seconds).")]
        [Min(0.2f)] public float BossBulletLifetime = 4f;
        public Color BossBulletColor = new Color(1f, 0.35f, 0.15f);

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
        [Tooltip("Health multiplier of swarm enemies (on top of the normal growth over time).")]
        [Min(0.01f)] public float SwarmHpMultiplier = 1f;

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

        [Header("Chữ bay trên đầu hero")]
        [Tooltip("Font size of the texts that float up over the hero (e.g. the pig's gift). In UI pixels on a 1080-wide screen.")]
        [Min(8f)] public float FloatTextSize = 60f;
        [Tooltip("How long a text stays (seconds).")]
        [Min(0.1f)] public float FloatTextDuration = 1.4f;
        [Tooltip("How far it floats up during that time (world units).")]
        public float FloatTextRise = 1.1f;
        [Tooltip("Where it appears, above the hero's center (world units).")]
        public float FloatTextHeight = 1.3f;
        [Tooltip("Space between texts shown at the same time, in text heights (1 = one line), so they don't overlap.")]
        [Min(0f)] public float FloatTextLineSpacing = 1.05f;
        [Tooltip("Black border around the letters (0 = none, 0.3 = thick). Too high eats into the letters: raise Float Text Thickness with it.")]
        [Range(0f, 1f)] public float FloatTextOutlineWidth = 0.3f;
        public Color FloatTextOutlineColor = new Color(0.08f, 0.08f, 0.08f, 1f);
        [Tooltip("Makes the letters fatter (positive) or thinner (negative). Helps when the outline is thick.")]
        [Range(-1f, 1f)] public float FloatTextThickness = 0.15f;

        [Header("Số sát thương")]
        [Tooltip("Height of one digit at scale 1 (world units). Every scale below multiplies this.")]
        [Min(0.05f)] public float NumberBaseHeight = 0.32f;
        [Tooltip("Color of the numbers when you hit a normal enemy.")]
        public Color EnemyNumberColor = Color.white;
        [Min(0.1f)] public float EnemyNumberScale = 1f;
        [Tooltip("Color of the numbers when you hit a boss.")]
        public Color BossNumberColor = new Color(1f, 0.85f, 0.3f);
        [Min(0.1f)] public float BossNumberScale = 1.6f;
        [Tooltip("Color of the number shown when the hero loses HP.")]
        public Color HurtNumberColor = new Color(1f, 0.22f, 0.2f);
        [Min(0.1f)] public float HurtNumberScale = 2f;

        public static PlayerTuning LoadOrDefault()
        {
            var tuning = Resources.Load<PlayerTuning>("PlayerTuning");
            return tuning != null ? tuning : CreateInstance<PlayerTuning>();
        }
    }
}
