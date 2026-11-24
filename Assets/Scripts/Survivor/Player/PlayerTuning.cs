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

        [Header("Dấu hiệu hero")]
        [Tooltip("Size of the blue triangle over the hero's head (world units).")]
        [Min(0f)] public float MarkerSize = 0.3f;
        [Tooltip("Height of the triangle above the hero's center (the head top is at ~0.85).")]
        public float MarkerHeight = 1.02f;
        [Tooltip("Tint for the triangle. White = the original blue art.")]
        public Color MarkerTint = Color.white;

        [Header("Rái Cá Kiếm Sĩ")]
        [Tooltip("Speed when dashing to the enemy (world units per second).")]
        [Min(0.5f)] public float OtterDashSpeed = 20f;
        [Tooltip("Speed when running back to its place next to the hero (world units per second).")]
        [Min(0.5f)] public float OtterReturnSpeed = 13f;
        [Tooltip("Longest a dash may last (seconds). If it hasn't reached the enemy by then it slashes where it is. " +
                 "Raise this when you lower Otter Dash Speed.")]
        [Min(0.05f)] public float OtterMaxDashTime = 0.45f;
        [Tooltip("How long the otter stays where it slashed before running back (seconds, counted from the last swing). " +
                 "0 = runs back right after the swing (the slash effect still plays out where it was swung).")]
        [Min(0f)] public float OtterSlashStayTime = 0.22f;
        [Tooltip("Only enemies within this distance of the otter are chased (world units).")]
        [Min(0.5f)] public float OtterSightRange = 7f;

        [Header("Đạn Ếch Ná Thun")]
        [Tooltip("Size of the rubber pellet (world units).")]
        [Min(0.05f)] public float PelletSize = 0.34f;
        [Tooltip("Color of the trail behind the pellet.")]
        public Color PelletTrailColor = new Color(1f, 0.15f, 0.1f, 0.8f);
        [Tooltip("Trail length (world units). 0 = no trail.")]
        [Min(0f)] public float PelletTrailLength = 0.8f;
        [Tooltip("Trail thickness (world units).")]
        [Min(0f)] public float PelletTrailWidth = 0.2f;
        [Tooltip("Bounce radius per level (element 0 = level 1 ... element 4 = level 5): after hitting an enemy, the pellet " +
                 "jumps to the nearest enemy within this distance. Nobody that close around the first target = double damage.")]
        public float[] FrogBounceRadius = { 2.5f, 2.7f, 3.0f, 3.2f, 3.5f };

        [Header("Đấu boss")]
        [Tooltip("HP of the first boss.")]
        [Min(1f)] public float BossHp = 2500f;
        [Tooltip("Extra HP for each later boss: 0.6 = +60% per appearance (2nd boss = 1.6x, 3rd = 2.2x).")]
        [Min(0f)] public float BossHpGrowthPerAppearance = 0.6f;
        [Tooltip("Side of the square arena that closes in around the hero when a boss appears (world units). " +
                 "Keep it a bit smaller than Camera View Width so the whole wall is on screen.")]
        [Min(4f)] public float BossArenaSize = 11f;
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

        [Header("Đẩy lùi quái (knockback)")]
        [Tooltip("Only when the hit does NOT kill. Multiplies every weapon's push (0 = no knockback).")]
        [Min(0f)] public float KnockbackStrength = 2.5f;
        [Tooltip("How fast the push dies out. Lower = enemies slide back further.")]
        [Min(0.5f)] public float KnockbackDecay = 7f;
        [Tooltip("Cap on the push speed, so fast-firing weapons can't launch enemies across the screen.")]
        [Min(0f)] public float KnockbackMaxSpeed = 9f;
        [Tooltip("While pushed faster than this, the enemy stops walking toward the hero (0 = it never stops).")]
        [Min(0f)] public float KnockbackStunSpeed = 2f;

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
