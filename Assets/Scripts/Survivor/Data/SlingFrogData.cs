using UnityEngine;

namespace Survivor
{
    [CreateAssetMenu(menuName = "Survivor/Companion/Ếch Ná Thun", fileName = "SlingFrog")]
    public sealed class SlingFrogData : CompanionData
    {
        [Header("Chỉ số theo cấp")]
        public float[] Damage = { 10f, 13f, 17f, 22f, 28f };
        [Tooltip("Seconds between shots, per level.")]
        public float[] Cooldown = { 1.3f, 1.2f, 1.1f, 1.0f, 0.85f };
        [Tooltip("After a hit the pellet jumps to the nearest enemy within this distance, per level. " +
                 "Nobody that close around the first target = double damage.")]
        public float[] BounceRadius = { 2.5f, 2.7f, 3.0f, 3.2f, 3.5f };

        [Header("Khác")]
        [Tooltip("How many times a pellet bounces (2 = A -> B -> C).")]
        [Range(0, 6)] public int MaxBounces = 2;
        [Tooltip("Damage multiplier when the target stands alone.")]
        [Min(1f)] public float LoneMultiplier = 2f;
        [Min(1f)] public float ShotSpeed = 13f;
        [Min(1f)] public float BounceSpeed = 11f;
        [Min(0.5f)] public float SightRange = 8.5f;

        [Header("Viên đạn")]
        [Tooltip("Size of the rubber pellet (world units).")]
        [Min(0.05f)] public float PelletSize = 0.34f;
        public Color PelletTrailColor = new Color(1f, 0.15f, 0.1f, 0.8f);
        [Tooltip("Trail length (world units). 0 = no trail.")]
        [Min(0f)] public float PelletTrailLength = 0.8f;
        [Min(0f)] public float PelletTrailWidth = 0.2f;

        public override CompanionKind Kind => CompanionKind.SlingFrog;
        public override Companion Create() => new SlingFrogCompanion(this);
    }
}
