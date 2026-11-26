using UnityEngine;

namespace Survivor
{
    [CreateAssetMenu(menuName = "Survivor/Companion/Xạ Thủ Nhí", fileName = "Gunner")]
    public sealed class GunnerData : CompanionData
    {
        [Header("Chỉ số theo cấp")]
        public float[] Damage = { 3f, 4f, 5f, 7f, 9f };
        [Tooltip("Shooting range, per level.")]
        public float[] Range = { 6f, 6.5f, 7f, 7.5f, 8f };
        [Tooltip("Enemies a bullet passes through, per level.")]
        public int[] Pierce = { 0, 0, 1, 1, 2 };
        [Tooltip("Random spread of each bullet (degrees, ±), per level.")]
        public float[] SpreadDegrees = { 8f, 7f, 6f, 5f, 4f };

        [Header("Khác")]
        [Tooltip("Seconds between two shots.")]
        [Min(0.02f)] public float FireInterval = 0.14f;
        [Min(1f)] public float BulletSpeed = 16f;
        public Color TracerColor = new Color(1f, 0.9f, 0.5f, 0.85f);

        public override CompanionKind Kind => CompanionKind.Gunner;
        public override Companion Create() => new GunnerCompanion(this);
    }
}
