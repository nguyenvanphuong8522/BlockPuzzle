using UnityEngine;

namespace Survivor
{
    // How ONE boss fights: Assets/Resources/Bosses/<Id>/<Id>_Settings.asset (live-tunable in Play mode): skill rules, timing,
    // sizes, colors, arena. Its stats (HP, speed, damage) are in <Id>_Stats.asset next to it. Order: BossSchedule.asset.
    // Each skill is only used when the hero is within its distance range (from the boss center):
    // e.g. bullets when he is close, dash when he is far, claw swipe when he is in reach.
    [CreateAssetMenu(menuName = "Survivor/Boss Settings", fileName = "BossSettings")]
    public sealed class BossSettings : ScriptableObject
    {
        [Header("Hiển thị")]
        [Tooltip("Picture on the boss bar / countdown / warning. Empty = first frame of its art.")]
        public Sprite Icon;

        [Header("Cảnh báo / phần thưởng / số sát thương")]
        [Tooltip("The \"boss coming\" warning shows this many seconds before a boss appears. 0 = no warning.")]
        [Min(0f)] public float WarningSeconds = 5f;
        [Tooltip("On = the boss experience drops as gems lined up along the arena fence (a fence of gems). " +
                 "Off = a wavy line of Trail Gems gems.")]
        public bool GemsOnFence = true;
        [Tooltip("Fence of gems: distance between two gems, and how far inside the wall line they lie (world units).")]
        [Min(0.2f)] public float FenceGemSpacing = 1f;
        [Min(0f)] public float FenceGemInset = 0.4f;
        [Tooltip("Gems On Fence off: the boss experience drops as this many gems in a line.")]
        [Min(1)] public int TrailGems = 30;
        [Tooltip("Color of the damage numbers when you hit the boss.")]
        public Color NumberColor = Color.white;
        [Tooltip("Size of the damage numbers on the boss (times Number Base Height in PlayerTuning).")]
        [Min(0.1f)] public float NumberScale = 2.3f;
        [Tooltip("Boss numbers start this much higher above its center (world units).")]
        public float NumberHeight = 0.6f;
        [Tooltip("How far around the hit boss numbers may appear, times Number Spawn Radius (PlayerTuning).")]
        [Min(0f)] public float NumberSpread = 2.5f;

        [Header("Chớp trắng khi trúng đòn")]
        [Tooltip("How long the boss turns white when hit (seconds). 0 = never.")]
        [Min(0f)] public float HitFlashTime = 0.06f;
        [Tooltip("After a flash, at least this long (seconds) of its normal look before it can flash again, so fast " +
                 "repeated hits blink instead of keeping it white.")]
        [Min(0f)] public float HitFlashGap = 0.14f;
        [Tooltip("How white the flash is (1 = fully white, 0.5 = half way).")]
        [Range(0f, 1f)] public float HitFlashStrength = 0.85f;

        [Header("Đấu trường (tường)")]
        [Tooltip("Width (left-right) of the arena that closes in around the hero (world units). Keep it a bit smaller " +
                 "than Camera View Width so both side walls are on screen.")]
        [Min(4f)] public float ArenaWidth = 11f;
        [Tooltip("Height (up-down) of the arena (world units).")]
        [Min(4f)] public float ArenaHeight = 15f;
        [Tooltip("How far the wall pushes the hero back in when touched.")]
        [Min(0f)] public float ArenaWallPush = 0.6f;
        [Min(0.02f)] public float ArenaWallThickness = 0.18f;
        public Color ArenaWallColor = new Color(1f, 0.25f, 0.2f, 0.9f);
        [Tooltip("Height of the barbed-wire fence art drawn on the wall (world units).")]
        [Min(0.05f)] public float ArenaFenceHeight = 0.7f;
        [Tooltip("Length of one fence segment (one post + wires).")]
        [Min(0.2f)] public float ArenaFenceSegment = 1.8f;
        [Tooltip("Tint of the fence art (white = original colors).")]
        public Color ArenaFenceTint = Color.white;

        [Header("Kỹ năng: chung")]
        [Tooltip("Seconds the boss walks between two skills. Then it uses the next skill whose distance fits " +
                 "(order: dash, claw swipe, bullets). If none fits it keeps walking and checks every frame.")]
        [Min(0.5f)] public float SkillInterval = 3.5f;
        [Tooltip("Seconds before the first skill after the boss appears.")]
        [Min(0f)] public float FirstSkillDelay = 2.5f;

        [Header("Kỹ năng: Lao tới (dash)")]
        public bool DashEnabled = true;
        [Tooltip("Only when the hero is at least this far from the boss (world units).")]
        [Min(0f)] public float DashMinDistance = 5f;
        [Tooltip("...and at most this far.")]
        [Min(0f)] public float DashMaxDistance = 99f;
        [Tooltip("Warning time: the dark red bar fills the path, then the boss dashes.")]
        [Min(0.1f)] public float DashCastTime = 1.1f;
        [Tooltip("Dash speed (world units per second).")]
        [Min(1f)] public float DashSpeed = 16f;
        [Tooltip("Dash length (world units). Aimed at where the hero is when the warning starts.")]
        [Min(1f)] public float DashDistance = 9f;
        [Tooltip("Stop the dash when the boss hits the arena wall (the warning path is cut at the wall too).")]
        public bool DashStopAtWall = true;
        [Tooltip("Width of the warning path, times the boss body width.")]
        [Min(0.2f)] public float DashWidthScale = 1f;
        [Tooltip("Dashes in a row (each one re-aims at the hero with a shorter warning).")]
        [Range(1, 5)] public int DashCombo = 1;
        [Tooltip("Warning time before each extra dash of the combo (seconds).")]
        [Min(0.05f)] public float DashComboCastTime = 0.45f;
        [Tooltip("Bottom layer of the warning (whole path).")]
        public Color DashWarnColor = new Color(1f, 0.25f, 0.2f, 0.22f);
        [Tooltip("Top layer that runs from start to end during the warning.")]
        public Color DashFillColor = new Color(0.9f, 0.05f, 0.05f, 0.5f);

        [Header("Kỹ năng: Quẹt càng (claw swipe)")]
        public bool SwipeEnabled = true;
        [Tooltip("Only when the hero is at least this far from the boss (world units).")]
        [Min(0f)] public float SwipeMinDistance = 0f;
        [Tooltip("...and at most this far. Around Swipe Length so the hero is inside the rectangle.")]
        [Min(0f)] public float SwipeMaxDistance = 5f;
        [Tooltip("How long the red warning shows before the swing (seconds).")]
        [Min(0.1f)] public float SwipeCastTime = 0.9f;
        [Tooltip("During this first part of the cast (seconds) the warning still turns toward the hero, then it locks.")]
        [Min(0f)] public float SwipeAimTime = 0.25f;
        [Tooltip("Length of the rectangle, from the boss toward the hero (world units).")]
        [Min(0.5f)] public float SwipeLength = 5f;
        [Tooltip("Width of the rectangle, across (world units). The claw sweeps across this width.")]
        [Min(0.5f)] public float SwipeWidth = 4f;
        [Tooltip("The rectangle starts this far in front of the boss center (world units, 0 = at the center).")]
        public float SwipeOffset = 0f;
        [Tooltip("How long the swing takes to sweep across the area (seconds).")]
        [Min(0.02f)] public float SwipeSwingTime = 0.18f;
        [Tooltip("Swings in a row (each one re-aims, the claw swings back the other way).")]
        [Range(1, 5)] public int SwipeCombo = 2;
        [Tooltip("Warning time before each extra swing of the combo (seconds).")]
        [Min(0.05f)] public float SwipeComboCastTime = 0.5f;
        [Tooltip("Light layer = the whole area.")]
        public Color SwipeWarnColor = new Color(1f, 0.25f, 0.2f, 0.22f);
        [Tooltip("Dark layer that fills the area from the boss outward during the cast.")]
        public Color SwipeFillColor = new Color(0.9f, 0.05f, 0.05f, 0.5f);
        [Tooltip("Color of the slash streak when it swings.")]
        public Color SwipeSlashColor = new Color(1f, 0.92f, 0.85f, 0.9f);
        [Tooltip("Draw the big claw (king crab art) swinging with the slash. Off = only the slash streak (e.g. a shark tail sweep).")]
        public bool ShowClaw = true;
        [Tooltip("Size of the big claw that swings across the area (world units, length of the claw art).")]
        [Min(0.2f)] public float ClawSize = 2.6f;
        [Tooltip("Where along the rectangle's length the claw swings (0 = at the boss, 1 = far end).")]
        [Range(0f, 1f)] public float ClawAlong = 0.6f;
        [Tooltip("During the cast the claw is raised beside the boss and shakes; this is how far out (world units).")]
        [Min(0f)] public float ClawRaiseDistance = 1.4f;
        [Tooltip("Tint of the claw (white = original art, matches the boss).")]
        public Color ClawTint = Color.white;

        [Header("Kỹ năng: Đạn tỏa (bullet burst)")]
        public bool BurstEnabled = true;
        [Tooltip("Only when the hero is at least this far from the boss (world units).")]
        [Min(0f)] public float BurstMinDistance = 0f;
        [Tooltip("...and at most this far.")]
        [Min(0f)] public float BurstMaxDistance = 6f;
        [Tooltip("How long the boss winds up before the first wave (seconds).")]
        [Min(0f)] public float BurstCastTime = 0.6f;
        [Tooltip("Number of waves.")]
        [Range(1, 10)] public int BurstWaves = 2;
        [Tooltip("Seconds between two waves.")]
        [Min(0.05f)] public float BurstWaveDelay = 1f;
        [Tooltip("Bullets per wave, spread evenly over the Burst Angle.")]
        [Range(3, 64)] public int BurstBullets = 16;
        [Tooltip("Spread of each wave in degrees. 360 = full ring (every other wave turned half a step). Less = a fan " +
                 "centered on the hero, re-aimed at each wave (e.g. 90).")]
        [Range(10f, 360f)] public float BurstAngle = 360f;
        [Min(0.5f)] public float BulletSpeed = 5f;
        [Tooltip("Bullet size (world units, radius).")]
        [Min(0.05f)] public float BulletRadius = 0.22f;
        [Tooltip("How long a bullet flies before disappearing (seconds).")]
        [Min(0.2f)] public float BulletLifetime = 4f;
        public Color BulletColor = new Color(1f, 0.35f, 0.15f);

        public static BossSettings LoadOrDefault()
        {
            var settings = Resources.Load<BossSettings>("BossSettings");
            return settings != null ? settings : CreateInstance<BossSettings>();
        }
    }
}
