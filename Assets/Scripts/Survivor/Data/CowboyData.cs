using UnityEngine;

namespace Survivor
{
    // Fusion of the bear hunter and the little gunner (both at max level): a cowboy with a huge six-barrel
    // gatling gun. The barrels spin up, then it fires non-stop while an enemy is in range. One level only.
    [CreateAssetMenu(menuName = "Survivor/Companion/Cao Bồi Súng Máy (hợp thể)", fileName = "Cowboy")]
    public sealed class CowboyData : CompanionData
    {
        [Header("Súng máy")]
        [Tooltip("Damage of one bullet.")]
        [Min(0f)] public float Damage = 18f;
        [Tooltip("Seconds between two bullets once the barrels are at full speed.")]
        [Min(0.01f)] public float FireInterval = 0.05f;
        [Min(0.5f)] public float Range = 9f;
        [Tooltip("Enemies a bullet passes through.")]
        [Range(0, 3)] public int Pierce = 2;
        [Tooltip("Random spread of each bullet (degrees, ±).")]
        [Min(0f)] public float SpreadDegrees = 6f;
        [Tooltip("Time for the barrels to reach full speed before the first bullet.")]
        [Min(0f)] public float SpinUpTime = 0.45f;
        [Tooltip("Time the barrels keep spinning after the last enemy is gone (it fires right away if one comes back).")]
        [Min(0.01f)] public float SpinDownTime = 1.2f;
        [Tooltip("Barrel spin animation speed at full speed (frames per second).")]
        [Min(1f)] public float BarrelSpinFps = 36f;
        [Tooltip("How fast the gun turns toward a new target (degrees per second).")]
        [Min(10f)] public float AimTurnSpeed = 540f;

        [Header("Đạn")]
        [Min(1f)] public float BulletSpeed = 22f;
        [Min(0.02f)] public float BulletRadius = 0.12f;
        [Min(0f)] public float Knockback = 2.5f;
        public Color TracerColor = new Color(1f, 0.85f, 0.4f, 0.9f);

        [Header("Hình")]
        [Tooltip("Height of the gatling sprite (world units). The little gunner's gun is 0.24.")]
        [Min(0.05f)] public float GunSize = 0.62f;
        [Tooltip("How far the gun shakes back on each shot.")]
        [Min(0f)] public float Recoil = 0.05f;
        [Min(0f)] public float FlashSize = 0.5f;
        [Tooltip("Pitch of the gun sound (lower = heavier).")]
        [Range(0.3f, 2f)] public float SoundPitch = 0.75f;

        public override CompanionKind Kind => CompanionKind.Cowboy;
        public override Companion Create() => new CowboyCompanion(this);
    }
}
