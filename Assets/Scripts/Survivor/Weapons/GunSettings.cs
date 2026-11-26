using UnityEngine;

namespace Survivor
{
    public enum GunAimMode
    {
        [InspectorName("Theo hướng di chuyển")] MoveDirection,
        [InspectorName("Tự ngắm kẻ địch gần nhất")] NearestEnemy,
    }

    // Feel of the player's gun (the starting weapon). Edit Assets/Resources/GunSettings.asset in the
    // Inspector while playing: every value applies immediately.
    [CreateAssetMenu(menuName = "Survivor/Gun Settings", fileName = "GunSettings")]
    public sealed class GunSettings : ScriptableObject
    {
        [Header("Cầm súng")]
        [Tooltip("Theo hướng di chuyển = the gun points where the hero walks (standing still keeps the last direction) and fires " +
                 "whenever it is ready. Tự ngắm = it turns to the nearest enemy and only fires when one is in range.")]
        public GunAimMode AimMode = GunAimMode.MoveDirection;
        [Tooltip("Gun sprite height in world units.")]
        [Min(0.05f)] public float GunSize = 0.55f;
        [Tooltip("Distance from the player's center to the gun.")]
        public float HoldDistance = 0.55f;
        [Tooltip("Gun position relative to the player's center (y = hand height).")]
        public float HoldHeight = -0.05f;
        [Tooltip("How fast the gun turns toward the target (degrees per second).")]
        [Min(1f)] public float AimTurnSpeed = 900f;
        [Tooltip("Off = the gun is always drawn in front of the player. On = it goes behind the player while aiming up.")]
        public bool BehindPlayerWhenAimingUp = false;
        [Tooltip("Muzzle point in the gun sprite, in units of the gun height (x along the barrel, y up).")]
        public Vector2 MuzzleOffset = new Vector2(0.95f, 0.14f);

        [Header("Đạn")]
        [Min(1f)] public float BulletSpeed = 22f;
        [Tooltip("Random angle added to each shot (degrees, ±).")]
        [Min(0f)] public float SpreadDegrees = 4f;
        [Tooltip("Delay between bullets when a level fires several (seconds).")]
        [Min(0f)] public float BurstInterval = 0.08f;
        public float TracerLength = 0.9f;
        public float TracerWidth = 0.14f;
        public Color TracerColor = new Color(1f, 0.92f, 0.55f, 0.6f);

        [Header("Giật súng (recoil)")]
        [Tooltip("How far the gun kicks back toward the player.")]
        public float RecoilDistance = 0.18f;
        [Tooltip("How much the barrel flips up (degrees).")]
        public float RecoilAngle = 22f;
        [Tooltip("How fast the gun settles back (higher = snappier).")]
        [Min(0.1f)] public float RecoilRecovery = 14f;

        [Header("Lửa đầu nòng & khói")]
        [Min(0f)] public float MuzzleFlashTime = 0.06f;
        [Min(0f)] public float MuzzleFlashSize = 0.45f;
        [Min(0f)] public float SmokeSize = 0.35f;

        [Header("Rung màn hình")]
        [Tooltip("Camera shake per shot (world units). 0 = off.")]
        [Min(0f)] public float ScreenShake = 0.06f;

        public static GunSettings LoadOrDefault()
        {
            var settings = Resources.Load<GunSettings>("GunSettings");
            return settings != null ? settings : CreateInstance<GunSettings>();
        }
    }
}
