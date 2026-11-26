using UnityEngine;

namespace Survivor
{
    // Bear hunter with a big rifle: fires a magazine (8 shots, 0.34 s apart), then reloads (2 s).
    [CreateAssetMenu(menuName = "Survivor/Companion/Gấu Súng Săn", fileName = "BearRifle")]
    public sealed class BearRifleData : CompanionData
    {
        [Header("Chỉ số theo cấp")]
        public float[] Damage = { 26, 33, 42, 54, 70 };
        [Tooltip("Shooting range, per level.")]
        public float[] Range = { 7f, 7.5f, 8f, 8.5f, 9f };
        [Tooltip("Enemies a bullet passes through, per level.")]
        public int[] Pierce = { 1, 1, 2, 2, 3 };

        [Header("Băng đạn")]
        [Tooltip("Shots in one magazine.")]
        [Range(1, 30)] public int MagazineSize = 8;
        [Tooltip("Seconds between two shots.")]
        [Min(0.02f)] public float ShotInterval = 0.34f;
        [Tooltip("Seconds to reload once the magazine is empty.")]
        [Min(0f)] public float ReloadTime = 2f;
        [Tooltip("With nothing to shoot for this long and some shots used, it reloads on its own. 0 = never.")]
        [Min(0f)] public float IdleReloadDelay = 1.5f;

        [Header("Đạn")]
        [Min(1f)] public float BulletSpeed = 22f;
        [Tooltip("Bullet size (hit radius, world units). The gunner's is 0.08.")]
        [Min(0.02f)] public float BulletRadius = 0.15f;
        [Tooltip("Random spread of each shot (degrees, ±).")]
        [Min(0f)] public float SpreadDegrees = 2f;
        [Min(0f)] public float Knockback = 4.5f;
        public Color TracerColor = new Color(1f, 0.75f, 0.35f, 0.95f);

        [Header("Súng")]
        [Tooltip("Height of the rifle sprite (world units).")]
        [Min(0.05f)] public float GunSize = 0.34f;
        [Tooltip("How far the rifle kicks back on each shot.")]
        [Min(0f)] public float Recoil = 0.12f;
        [Tooltip("Size of the muzzle flash.")]
        [Min(0f)] public float FlashSize = 0.42f;

        [Header("Thanh đạn / nạp đạn")]
        [Tooltip("Bar under it: shots left, then reload progress. Width 0 = hidden.")]
        [Min(0f)] public float BarWidth = 0.8f;
        [Min(0.02f)] public float BarHeight = 0.09f;
        public float BarOffsetY = -0.12f;
        [Min(0f)] public float BarOutline = 0.02f;
        public Color AmmoColor = new Color(1f, 0.8f, 0.3f);
        public Color ReloadColor = new Color(0.5f, 0.85f, 1f);
        public Color BarBackground = new Color(0.15f, 0.15f, 0.15f, 0.85f);

        public override CompanionKind Kind => CompanionKind.BearRifle;
        public override Companion Create() => new BearRifleCompanion(this);
    }
}
