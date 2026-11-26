using System.Collections.Generic;
using UnityEngine;

namespace Survivor
{
    // Art: companion_cowboy_0..4 (0-3 idle, 4 blink) + weapon_gatling_0..2 (barrel spin frames).
    // Fusion companion (bear hunter + little gunner at max level): a cowboy with a huge six-barrel gatling gun.
    // When an enemy comes in range the barrels spin up, then it fires a non-stop stream of heavy bullets; with
    // nothing left to shoot the barrels slowly spin down.
    // Every number and the card text: Assets/Resources/Upgrades/Companions/Cowboy.asset.
    public sealed class CowboyCompanion : Companion
    {
        const float Radius = 0.4f;
        const float FlashTime = 0.04f;
        const int IdleFrames = 4, BlinkFrame = 4;
        const float IdleFps = 6f, BlinkEvery = 3.5f, BlinkTime = 0.13f;
        const float GripFraction = 0.25f;  // where the hands hold the gun, from its back end (0..1 of its length)

        readonly CowboyData data;
        readonly List<int> found = new List<int>(1);
        Vector2 aim = Vector2.right;
        float spin;        // 0 = barrels still, 1 = full speed
        float barrel;      // barrel animation clock
        float timer;
        float kick, flash;
        int flashFrame;

        public CowboyCompanion(CowboyData data) : base(data) => this.data = data;

        public override float SightRadius(CompanionContext ctx) => data.Range;

        bool AimsLeft => aim.x < 0f;
        float Body => Radius * data.BodyScale;
        float GunLength(CompanionContext ctx)
        {
            var frames = ctx.Sprites.Gatling?.Frames;
            var s = frames != null && frames.Length > 0 ? frames[0] : null;
            return s != null ? data.GunSize * s.rect.width / s.rect.height : data.GunSize * 2.3f;
        }
        Vector2 Hands => Position + new Vector2(AimsLeft ? -0.2f : 0.2f, -0.04f) * data.BodyScale;
        Vector2 GunCenter(CompanionContext ctx) =>
            Hands + aim * ((0.5f - GripFraction) * GunLength(ctx) - kick);
        Vector2 Muzzle(CompanionContext ctx) => GunCenter(ctx) + aim * (GunLength(ctx) * 0.47f);

        public override void Tick(float dt, CompanionContext ctx)
        {
            kick = Mathf.MoveTowards(kick, 0f, dt * 1.2f);
            flash -= dt;
            barrel += dt * data.BarrelSpinFps * spin;

            ctx.Enemies.FindNearest(Position, data.Range, 1, found);
            if (found.Count == 0)
            {
                spin = Mathf.Max(0f, spin - dt / data.SpinDownTime);
                timer = Mathf.Max(timer, 0f);
                return;
            }

            var want = (ctx.Enemies.Position[found[0]] - Position).normalized;
            var a = Mathf.Atan2(aim.y, aim.x) * Mathf.Rad2Deg;
            var b = Mathf.Atan2(want.y, want.x) * Mathf.Rad2Deg;
            a = Mathf.MoveTowardsAngle(a, b, data.AimTurnSpeed * dt) * Mathf.Deg2Rad;
            aim = new Vector2(Mathf.Cos(a), Mathf.Sin(a));

            // Spin up first, then fire at a fixed rate (catching up after a long frame).
            spin = data.SpinUpTime <= 0f ? 1f : Mathf.Min(1f, spin + dt / data.SpinUpTime);
            if (spin < 1f) return;
            timer -= dt;
            while (timer <= 0f)
            {
                timer += data.FireInterval;
                Fire(ctx);
            }
        }

        void Fire(CompanionContext ctx)
        {
            var spread = Random.Range(-data.SpreadDegrees, data.SpreadDegrees) * Mathf.Deg2Rad;
            float c = Mathf.Cos(spread), s = Mathf.Sin(spread);
            var dir = new Vector2(aim.x * c - aim.y * s, aim.x * s + aim.y * c);
            // Bullets leave from one of the three visible barrels.
            var side = new Vector2(-aim.y, aim.x) * (data.GunSize * 0.14f * ((flashFrame % 3) - 1));
            ctx.Projectiles.Spawn(Muzzle(ctx) + side, dir, data.BulletSpeed, data.Damage * ctx.Player.Stats.Might,
                data.Range / data.BulletSpeed, data.BulletRadius, data.Pierce, data.TracerColor, data.Knockback);
            kick = data.Recoil;
            flash = FlashTime;
            flashFrame++;
            ctx.Audio.PlayAtPitch(Sfx.Smg, data.SoundPitch * Random.Range(0.95f, 1.05f), 0.35f, 0.04f);
        }

        public override void Draw(InstancedRenderer renderer, CompanionContext ctx)
        {
            var r = Body;
            var feet = Position.y - r;
            renderer.Ellipse(RenderLayer.Shadows, new Vector2(Position.x, feet + 0.05f), new Vector2(r * 1.9f, r * 0.55f),
                new Color(0f, 0f, 0f, 0.22f));

            var anim = ctx.Sprites.Cowboy;
            if (anim != null && !anim.IsEmpty)
                renderer.Sprite(RenderLayer.World, PickFrame(anim), new Vector2(Position.x, Position.y + r * 0.4f),
                    r * 2.6f, Color.white, flipX: AimsLeft, sortY: feet);
            else
                renderer.Circle(RenderLayer.World, Position, r, new Color(0.65f, 0.45f, 0.3f), 0f, feet);

            // Huge gatling in front of him.
            var angle = Mathf.Atan2(aim.y, aim.x);
            var center = GunCenter(ctx);
            var gun = ctx.Sprites.Gatling;
            if (gun != null && !gun.IsEmpty)
                renderer.SpriteRotated(RenderLayer.World, gun.Frames[(int)barrel % gun.Frames.Length], center, data.GunSize,
                    angle, Color.white, flipY: AimsLeft, sortY: feet - 0.01f);
            else
                renderer.Streak(RenderLayer.World, center, GunLength(ctx), data.GunSize * 0.5f, angle, new Color(0.35f, 0.38f, 0.45f));

            if (flash > 0f)
            {
                var flashAnim = ctx.Sprites.MuzzleFlash;
                var muzzle = Muzzle(ctx);
                if (flashAnim != null && !flashAnim.IsEmpty)
                    renderer.SpriteRotated(RenderLayer.Effects, flashAnim.Frames[flashFrame % flashAnim.Frames.Length],
                        muzzle + aim * data.FlashSize, data.FlashSize, angle, Color.white);
                else
                    renderer.Circle(RenderLayer.Effects, muzzle, 0.18f, new Color(1f, 0.85f, 0.4f));
            }
        }

        Sprite PickFrame(SpriteAnim anim)
        {
            var frames = anim.Frames;
            if (frames.Length <= BlinkFrame) return frames[0];
            var t = Time.time + Slot * 1.37f;
            if (t % BlinkEvery < BlinkTime) return frames[BlinkFrame];
            return frames[(int)((Time.time + Slot * 0.3f) * IdleFps) % IdleFrames];
        }
    }
}
