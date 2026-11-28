using UnityEngine;

namespace Survivor
{
    // One asset per companion (Assets/Resources/Upgrades/Companions). Card text, icon, color and every number
    // of that companion. Edit it in the Inspector: changes apply immediately, even in Play Mode.
    // Arrays "per level" have 5 elements: element 0 = level 1 ... element 4 = level 5.
    public abstract class CompanionData : ScriptableObject
    {
        [Header("Thẻ chọn")]
        [Tooltip("Off = never offered on level-up / chest cards (a companion you already have keeps working).")]
        public bool Enabled = true;
        public string DisplayName = "Đệ tử";
        [Tooltip("Card text when it is offered for the first time (MỚI!).")]
        [TextArea(2, 4)] public string Description = "";
        [Tooltip("Card text for each upgrade: element 0 = going to level 2, 1 = level 3, 2 = level 4, 3 = level 5.")]
        [TextArea(1, 3)] public string[] LevelDescriptions = { "", "", "", "" };
        [Tooltip("Card title / stripe color, also its sight ring in the debug view.")]
        public Color Tint = Color.white;
        [Tooltip("Card picture. Empty = the companion's own model (first idle frame).")]
        public Sprite Icon;
        [Tooltip("Order on the list (lower first). Only matters for debug tools; the cards are shuffled.")]
        public int SortOrder;
        [Tooltip("Highest level (1..5). 1 = it never gets upgrade cards (e.g. a fusion).")]
        [Range(1, Companion.MaxLevel)] public int MaxLevel = Companion.MaxLevel;

        [Header("Hình")]
        [Tooltip("Drawn size of its body (1 = normal). Its shadow and hands follow; attack numbers and range do not change.")]
        [Min(0.3f)] public float BodyScale = 1f;

        [Header("Hợp thể (fusion)")]
        [Tooltip("Empty = a normal companion. Otherwise it is a fusion: never offered normally; once you own every " +
                 "companion listed here at max level, the next level-up (or chest) is sure to offer it.")]
        public CompanionData[] FusionOf = new CompanionData[0];
        [Tooltip("On = the companions above disappear when it is recruited (it takes the first one's place). " +
                 "Off = they stay (needs a free slot).")]
        public bool ConsumeIngredients = true;

        public bool IsFusion => FusionOf != null && FusionOf.Length > 0;

        public abstract CompanionKind Kind { get; }
        public abstract Companion Create();

        // Translated texts: Localization.csv "companion.<asset name>.name / .desc / .level2..5"; the fields above
        // when a key is missing.
        public string LocalizedName => Loc.Get($"companion.{name}.name", DisplayName);
        public string LocalizedDescription => Loc.Get($"companion.{name}.desc", Description);

        // Card text for an upgrade to `level` (2..5).
        public string LevelText(int level)
        {
            var i = level - 2;
            if (LevelDescriptions == null || LevelDescriptions.Length == 0) return LocalizedDescription;
            return Loc.Get($"companion.{name}.level{level}", LevelDescriptions[Mathf.Clamp(i, 0, LevelDescriptions.Length - 1)]);
        }
    }

    // Shared by the dash-and-swing companions (sword otter, club pig).
    public abstract class MeleeCompanionData : CompanionData
    {
        [Header("Chỉ số theo cấp")]
        public float[] Damage = { 22, 28, 36, 46, 60 };
        [Tooltip("Seconds between attacks, per level.")]
        public float[] Cooldown = { 2.2f, 2.0f, 1.8f, 1.5f, 1.3f };
        [Tooltip("Radius of the 120-degree arc it hits, per level.")]
        public float[] ArcRadius = { 1.7f, 1.8f, 2.2f, 2.3f, 2.5f };
        [Tooltip("Swings per attack, per level (2 = swings back the other way).")]
        public int[] Swings = { 1, 1, 1, 1, 2 };

        [Header("Di chuyển")]
        [Tooltip("Speed when dashing to the enemy (world units per second).")]
        [Min(0.5f)] public float DashSpeed = 20f;
        [Tooltip("Speed when running back next to the hero.")]
        [Min(0.5f)] public float ReturnSpeed = 13f;
        [Tooltip("Longest a dash may last (seconds). Raise this when you lower Dash Speed.")]
        [Min(0.05f)] public float MaxDashTime = 0.45f;
        [Tooltip("How long it stays where it hit before running back (seconds, after the last swing). 0 = right away.")]
        [Min(0f)] public float StayTime = 0.22f;
        [Tooltip("Time between the two swings when it swings twice (seconds).")]
        [Min(0f)] public float DoubleSwingGap = 0.23f;
        [Tooltip("Only enemies within this distance of it are chased (world units).")]
        [Min(0.5f)] public float SightRange = 7f;

        [Header("Hiệu ứng")]
        [Tooltip("How hard the hit pushes enemies back.")]
        [Min(0f)] public float Knockback = 4f;
        [Tooltip("Color of the 120-degree swing effect.")]
        public Color SwingColor = new Color(0.55f, 0.85f, 1f);
    }
}
