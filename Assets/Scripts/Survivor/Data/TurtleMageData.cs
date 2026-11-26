using UnityEngine;

namespace Survivor
{
    [CreateAssetMenu(menuName = "Survivor/Companion/Rùa Pháp Sư", fileName = "TurtleMage")]
    public sealed class TurtleMageData : CompanionData
    {
        [Header("Chỉ số theo cấp")]
        [Tooltip("Damage of the orb itself, per level.")]
        public float[] OrbDamage = { 18, 22, 28, 34, 42 };
        [Tooltip("Damage of the explosion around it, per level.")]
        public float[] BlastDamage = { 10, 13, 17, 22, 28 };
        [Tooltip("Explosion radius, per level.")]
        public float[] BlastRadius = { 1.0f, 1.3f, 1.6f, 1.9f, 2.3f };
        [Tooltip("Seconds between casts, per level.")]
        public float[] Cooldown = { 2.0f, 1.9f, 1.7f, 1.5f, 1.3f };
        [Tooltip("Orbs per cast, per level.")]
        public int[] Orbs = { 1, 1, 1, 2, 2 };

        [Header("Khác")]
        [Min(0.5f)] public float OrbSpeed = 7.5f;
        [Tooltip("An orb that hits nothing explodes after this many seconds.")]
        [Min(0.1f)] public float OrbLifetime = 1.1f;
        [Tooltip("Angle between orbs when it casts several (degrees).")]
        [Min(0f)] public float SpreadDegrees = 14f;
        [Min(0.5f)] public float SightRange = 9f;

        public override CompanionKind Kind => CompanionKind.TurtleMage;
        public override Companion Create() => new TurtleMageCompanion(this);
    }
}
