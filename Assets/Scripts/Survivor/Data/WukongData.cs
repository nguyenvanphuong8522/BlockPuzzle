using UnityEngine;

namespace Survivor
{
    // Fusion of the banana monkey and the sword otter (both at max level): throws a big spinning staff that
    // flies out and comes back like a banana. "Fusion Of" (above) lists the two companions it is made from.
    [CreateAssetMenu(menuName = "Survivor/Companion/Tề Thiên (hợp thể)", fileName = "Wukong")]
    public sealed class WukongData : CompanionData
    {
        [Header("Chỉ số theo cấp")]
        public float[] Damage = { 45, 55, 68, 82, 100 };
        [Tooltip("Staffs per throw, per level.")]
        public int[] Staffs = { 1, 1, 2, 2, 3 };
        [Tooltip("Seconds between throws, per level (counted from the moment every staff is back in hand " +
                 "when Wait For Return is on).")]
        public float[] Cooldown = { 1.4f, 1.3f, 1.2f, 1.1f, 1.0f };
        [Tooltip("How far a staff flies before coming back, per level (world units).")]
        public float[] Range = { 6f, 6.5f, 7f, 7.5f, 8f };
        [Tooltip("Length of the staff, per level (world units). The banana is 0.55.")]
        public float[] StaffLength = { 2.4f, 2.6f, 2.8f, 3.0f, 3.4f };

        [Header("Gậy")]
        [Tooltip("Hit radius around the spinning staff = Staff Length / 2 × this. 1 = the whole circle it spins through.")]
        [Range(0.2f, 1.5f)] public float HitRadiusScale = 0.9f;
        [Tooltip("Spin speed (degrees per second).")]
        [Min(0f)] public float SpinSpeed = 1080f;
        [Tooltip("Launch speed (world units per second).")]
        [Min(1f)] public float ThrowSpeed = 13f;
        [Tooltip("Top speed on the way back.")]
        [Min(1f)] public float ReturnSpeed = 15f;
        [Tooltip("Faded copies drawn behind the staff while it spins (spin blur). 0 = none.")]
        [Range(0, 4)] public int SpinGhosts = 2;
        [Tooltip("How hard each hit pushes enemies back.")]
        [Min(0f)] public float Knockback = 4f;
        [Tooltip("Angle between staffs when it throws several (degrees).")]
        [Min(0f)] public float SpreadDegrees = 25f;
        [Tooltip("On = it does not throw again until every staff has come back.")]
        public bool WaitForReturn = true;

        [Header("Skill phân thân")]
        [Tooltip("Off = never summons clones.")]
        public bool CloneSkill = true;
        [Tooltip("Seconds between summons (counted from the end of the previous summon).")]
        [Min(1f)] public float CloneInterval = 12f;
        [Tooltip("Seconds after he joins before the first summon.")]
        [Min(0f)] public float CloneFirstDelay = 3f;
        [Tooltip("Wind-up before the first clone (pluck a hair). Then he blows the clones out one by one. " +
                 "He does not throw the staff until every clone of the summon is out.")]
        [Min(0.1f)] public float CloneCastTime = 1.0f;
        [Tooltip("Clones per summon.")]
        [Range(1, 16)] public int CloneCount = 8;
        [Tooltip("Most clones alive at once (a summon stops at this number).")]
        [Range(1, 32)] public int CloneMaxAlive = 16;
        [Tooltip("Average seconds between two clones of the same summon (they come out one by one).")]
        [Min(0f)] public float CloneSummonGap = 0.18f;
        [Tooltip("How uneven the timing is: each gap is Clone Summon Gap × random(1 - this, 1 + this). 0 = a steady rhythm.")]
        [Range(0f, 1f)] public float CloneGapJitter = 0.6f;
        [Tooltip("Closest / farthest a clone lands from him (world units); each one picks a random distance in between.")]
        public Vector2 CloneLandDistance = new Vector2(1.4f, 3.2f);
        [Tooltip("Each clone flies off ~137° from the previous one (spread all around, never an even circle), " +
                 "plus a random turn of up to this many degrees.")]
        [Range(0f, 90f)] public float CloneAngleJitter = 30f;
        [Tooltip("Flight time from his body to the landing spot (seconds).")]
        [Min(0.05f)] public float CloneFlyTime = 0.38f;
        [Tooltip("Height of the jump arc while flying (world units).")]
        [Min(0f)] public float CloneFlyHeight = 0.9f;
        [Tooltip("Size when it leaves his body (1 = full size); it grows while flying.")]
        [Range(0.1f, 1f)] public float CloneFlyStartScale = 0.45f;
        [Tooltip("Faded copies trailing behind a flying clone. 0 = none.")]
        [Range(0, 4)] public int CloneFlyTrail = 2;
        [Tooltip("Size of the smoke puff at his body each time a clone flies out (world units).")]
        [Min(0.1f)] public float CloneSmokeSize = 0.9f;
        [Tooltip("Size of the puff at his body when a clone jumps out, compared to Clone Smoke Size.")]
        [Range(0f, 2f)] public float CloneLaunchSmokeScale = 0.55f;
        [Tooltip("Size of the little dust puff where it lands, compared to Clone Smoke Size. 0 = none.")]
        [Range(0f, 2f)] public float CloneLandSmokeScale = 0.5f;
        [Tooltip("How long the smoke puff lasts (seconds).")]
        [Min(0.05f)] public float CloneSmokeTime = 0.45f;
        [Tooltip("Small extra puffs around the big one (a fuller cloud).")]
        [Range(0, 8)] public int CloneSmokePuffs = 4;
        [Tooltip("Color of the smoke (alpha = how thick it is).")]
        public Color CloneSmokeColor = new Color(1f, 1f, 1f, 0.9f);
        [Tooltip("Landing: a little bounce before it starts hunting (seconds).")]
        [Min(0.01f)] public float CloneAppearTime = 0.22f;
        [Tooltip("Pitch of the \"poof\" sound (each clone of a summon a bit higher).")]
        [Range(0.3f, 3f)] public float ClonePoofPitch = 1.4f;

        [Header("Thanh hồi chiêu triệu hồi")]
        [Tooltip("Countdown bar under him (world units). Width 0 = hidden.")]
        [Min(0f)] public float BarWidth = 0.8f;
        [Min(0.02f)] public float BarHeight = 0.09f;
        [Tooltip("Height of the bar relative to his feet (negative = below the feet).")]
        public float BarOffsetY = -0.12f;
        [Min(0f)] public float BarOutline = 0.02f;
        public Color BarColor = new Color(1f, 0.55f, 0.2f);
        public Color BarBackground = new Color(0.15f, 0.15f, 0.15f, 0.85f);
        [Tooltip("Bar color while he is waiting for the staff / summoning (blinks).")]
        public Color BarReadyColor = new Color(1f, 0.95f, 0.6f);

        [Header("Phân thân")]
        [Tooltip("Health of each clone. Every attack costs it Clone Attack Hp Cost; at 0 it vanishes in a puff.")]
        [Min(1f)] public float CloneHp = 100f;
        [Tooltip("Health a clone loses per attack (100 / 20 = 5 attacks).")]
        [Min(0f)] public float CloneAttackHpCost = 20f;
        [Tooltip("Damage of one clone attack, per level.")]
        public float[] CloneDamage = { 18, 22, 27, 33, 40 };
        [Tooltip("Seconds between a clone's attacks.")]
        [Min(0.05f)] public float CloneAttackInterval = 0.6f;
        [Tooltip("A clone attacks when an enemy is this close.")]
        [Min(0.1f)] public float CloneAttackRange = 0.9f;
        [Tooltip("Radius of a clone's spinning-staff hit (everything inside is hit).")]
        [Min(0.1f)] public float CloneAttackRadius = 1.2f;
        [Tooltip("Length of the staff a clone spins (world units).")]
        [Min(0.1f)] public float CloneStaffLength = 1.6f;
        [Tooltip("How long one spin of the staff lasts (seconds).")]
        [Min(0.05f)] public float CloneSpinTime = 0.3f;
        [Min(0f)] public float CloneKnockback = 3f;
        [Tooltip("Walking speed (world units per second).")]
        [Min(0.1f)] public float CloneMoveSpeed = 5.5f;
        [Tooltip("Clones look for enemies within this distance of themselves.")]
        [Min(0.5f)] public float CloneSightRange = 7f;
        [Tooltip("No enemy in sight: it wanders, picking a new spot within this distance of itself.")]
        [Min(0.3f)] public float CloneWanderRadius = 3f;
        [Tooltip("Seconds before a wandering clone picks a new spot.")]
        [Min(0.1f)] public float CloneWanderInterval = 1.6f;
        [Tooltip("Walking speed while wandering, compared to Clone Move Speed.")]
        [Range(0.1f, 1f)] public float CloneWanderSpeedScale = 0.6f;
        [Tooltip("A clone vanishes after this many seconds even with health left. 0 = only when out of health.")]
        [Min(0f)] public float CloneLifetime = 20f;
        [Tooltip("Drawn size compared to the real one (1 = identical).")]
        [Min(0.2f)] public float CloneScale = 1f;
        [Tooltip("Tint of the clones (white = identical).")]
        public Color CloneTint = Color.white;
        [Tooltip("Health bar under each clone.")]
        public bool CloneHpBar = true;
        public Color CloneHpBarColor = new Color(1f, 0.8f, 0.25f);
        public Color CloneHpBarBackground = new Color(0f, 0f, 0f, 0.6f);

        [Header("Lông trĩ trên đầu")]
        public bool ShowPlumes = true;
        [Tooltip("Length of each plume (world units).")]
        [Min(0.05f)] public float PlumeLength = 0.8f;
        [Tooltip("Thickness of the plumes (1 = as drawn).")]
        [Min(0.1f)] public float PlumeThickness = 1.4f;
        [Tooltip("Direction of the front plume (degrees: 90 = straight up, smaller = leaning forward).")]
        [Range(0f, 180f)] public float FrontPlumeAngle = 72f;
        [Tooltip("Direction of the back plume (degrees: 90 = straight up, bigger = leaning back).")]
        [Range(0f, 180f)] public float BackPlumeAngle = 112f;
        [Tooltip("Where the plumes grow from, as a fraction of his drawn height from the sprite center: " +
                 "x = sideways (front / back plume), y = up.")]
        public Vector2 PlumeBase = new Vector2(0.156f, 0.242f);
        [Tooltip("How far they sway each way (degrees).")]
        [Range(0f, 45f)] public float PlumeSwayDegrees = 9f;
        [Tooltip("Sway speed (swings per second × 2π).")]
        [Min(0f)] public float PlumeSwaySpeed = 3f;
        [Tooltip("Extra swing when he throws the staff (degrees).")]
        [Range(0f, 45f)] public float PlumeThrowSwing = 14f;
        public Color PlumeTint = Color.white;

        [Header("Khác")]
        [Tooltip("Only enemies within this distance are targeted.")]
        [Min(0.5f)] public float SightRange = 9f;

        public override CompanionKind Kind => CompanionKind.Wukong;
        public override Companion Create() => new WukongCompanion(this);
    }
}
