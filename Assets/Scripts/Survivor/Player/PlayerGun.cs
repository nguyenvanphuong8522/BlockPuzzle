using UnityEngine;

namespace Survivor
{
    // The gun the player holds: turns toward the aim direction, kicks back on every shot,
    // and shows a muzzle flash. The weapon logic (MagicBoltWeapon) tells it where to aim and when it fired.
    public sealed class PlayerGun
    {
        readonly Player player;
        readonly GunSettings settings;
        readonly SpriteCatalog sprites;

        float angle;      // current aim, radians (0 = right)
        float recoil;     // 1 right after a shot, decays to 0
        float flashTimer;
        int flashFrame;

        public PlayerGun(Player player, GunSettings settings, SpriteCatalog sprites)
        {
            this.player = player;
            this.settings = settings;
            this.sprites = sprites;
        }

        public Vector2 AimDirection => new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));

        // Aiming left mirrors the gun so it stays upright.
        bool Flipped => Mathf.Cos(angle) < 0f;

        public void Tick(float dt, Vector2 desiredAim)
        {
            if (desiredAim.sqrMagnitude > 0.0001f)
            {
                var target = Mathf.Atan2(desiredAim.y, desiredAim.x);
                angle = Mathf.MoveTowardsAngle(angle * Mathf.Rad2Deg, target * Mathf.Rad2Deg,
                    settings.AimTurnSpeed * dt) * Mathf.Deg2Rad;
            }
            recoil *= Mathf.Exp(-settings.RecoilRecovery * dt);
            flashTimer -= dt;
        }

        // Called by the weapon on every shot.
        public void Kick()
        {
            recoil = 1f;
            flashTimer = settings.MuzzleFlashTime;
            flashFrame = Random.Range(0, 2);
        }

        // Where the gun sits, including the recoil kick back toward the player.
        Vector2 GunCenter()
        {
            var dir = AimDirection;
            var hold = settings.HoldDistance - settings.RecoilDistance * recoil;
            return player.Position + new Vector2(0f, settings.HoldHeight) + dir * hold;
        }

        // Barrel flips up while recoiling (counter-clockwise when aiming right, clockwise when aiming left).
        float VisualAngle() => angle + settings.RecoilAngle * Mathf.Deg2Rad * recoil * (Flipped ? -1f : 1f);

        // Muzzle position in world space: bullets and the flash start here.
        public Vector2 MuzzlePosition()
        {
            var a = VisualAngle();
            var local = settings.MuzzleOffset * settings.GunSize;
            if (Flipped) local.y = -local.y;
            var c = Mathf.Cos(a);
            var s = Mathf.Sin(a);
            return GunCenter() + new Vector2(local.x * c - local.y * s, local.x * s + local.y * c);
        }

        public void Draw(InstancedRenderer renderer)
        {
            if (player.IsDead) return;
            var a = VisualAngle();
            var center = GunCenter();
            // In front of the body (optionally behind it while aiming up, see GunSettings).
            var behind = settings.BehindPlayerWhenAimingUp && Mathf.Sin(angle) > 0.35f;
            var sortY = player.FeetY + (behind ? 0.01f : -0.01f);

            if (sprites.Gun != null)
                renderer.SpriteRotated(RenderLayer.World, sprites.Gun, center, settings.GunSize, a, Color.white, Flipped, sortY: sortY);
            else
                renderer.Streak(RenderLayer.Effects, center, settings.GunSize * 1.6f, settings.GunSize * 0.4f, a, new Color(0.45f, 0.5f, 0.58f));

            if (flashTimer > 0f)
            {
                var muzzle = MuzzlePosition();
                var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                var size = settings.MuzzleFlashSize;
                if (sprites.MuzzleFlash != null && !sprites.MuzzleFlash.IsEmpty)
                {
                    // The flash sprite starts at its left edge, so shift it forward by half its width.
                    var frame = sprites.MuzzleFlash.Frames[flashFrame % sprites.MuzzleFlash.Frames.Length];
                    renderer.SpriteRotated(RenderLayer.Effects, frame, muzzle + dir * size, size, a, Color.white);
                }
                else
                {
                    renderer.Circle(RenderLayer.Effects, muzzle, size * 0.4f, new Color(1f, 0.85f, 0.3f));
                }
            }
        }
    }
}
