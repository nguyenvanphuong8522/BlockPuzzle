using UnityEngine;

namespace Survivor
{
    [CreateAssetMenu(menuName = "Survivor/Companion/Khỉ Ném Chuối", fileName = "Monkey")]
    public sealed class MonkeyData : CompanionData
    {
        [Header("Chỉ số theo cấp")]
        public float[] Damage = { 12, 16, 20, 26, 34 };
        [Tooltip("Bananas per throw, per level.")]
        public int[] Bananas = { 1, 1, 2, 2, 3 };
        [Tooltip("Seconds between throws, per level.")]
        public float[] Cooldown = { 2.0f, 1.8f, 1.6f, 1.4f, 1.2f };
        [Tooltip("How far a banana flies before coming back, per level.")]
        public float[] Range = { 5f, 5.5f, 6f, 6.5f, 7f };

        [Header("Khác")]
        [Min(1f)] public float ThrowSpeed = 14f;
        [Tooltip("Angle between bananas when it throws several (degrees).")]
        [Min(0f)] public float SpreadDegrees = 18f;
        [Tooltip("Only enemies within this distance are targeted.")]
        [Min(0.5f)] public float SightRange = 9f;

        public override CompanionKind Kind => CompanionKind.Monkey;
        public override Companion Create() => new MonkeyCompanion(this);
    }
}
