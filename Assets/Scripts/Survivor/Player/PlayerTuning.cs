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
        [Tooltip("Color of the number shown when the hero loses HP.")]
        public Color HurtNumberColor = new Color(1f, 0.22f, 0.2f);
        [Min(0.1f)] public float HurtNumberScale = 2f;
        [Tooltip("How long a number stays on screen (seconds).")]
        [Min(0.1f)] public float NumberLifetime = 0.6f;
        [Tooltip("Numbers appear at a random point within this distance of the hit (world units), in the fan above it. 0 = always on the hit point.")]
        [Min(0f)] public float NumberSpawnRadius = 0.35f;
        [Tooltip("Width of the fan above the hit that numbers appear in and fly out along (degrees). 180 = the whole upper half, 0 = straight up.")]
        [Range(0f, 360f)] public float NumberSpreadDegrees = 180f;
        [Tooltip("Start speed of a number flying away from the hit (world units / second).")]
        [Min(0f)] public float NumberFlySpeed = 2.2f;
        [Tooltip("How fast the number slows down (higher = stops sooner). 0 = constant speed.")]
        [Min(0f)] public float NumberFlyDrag = 3f;
        [Tooltip("Lift added to every number at the start (world units up), so it starts above the enemy's center.")]
        public float NumberStartLift = 0.3f;
        [Tooltip("Numbers for damage the hero takes fly at this fraction of the speed above.")]
        [Min(0f)] public float HurtNumberSpeedScale = 0.6f;

        public static PlayerTuning LoadOrDefault()
        {
            var tuning = Resources.Load<PlayerTuning>("PlayerTuning");
            return tuning != null ? tuning : CreateInstance<PlayerTuning>();
        }
    }
}
