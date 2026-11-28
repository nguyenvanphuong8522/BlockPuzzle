using System.Collections.Generic;
using UnityEngine;

namespace Survivor
{
    // Art: companion_bear_0..5 (0-3 idle, 4 blink, 5 reloading) + weapon_rifle.
    // A bear hunter with a big rifle: fires a magazine of heavy piercing shots at the nearest enemy (8 shots,
    // 0.34 s apart), then has to reload (2 s): the rifle tips up and a bar under it fills.
    // Every number and the card text: Assets/Resources/Upgrades/Companions/BearRifle.asset.
    public sealed class BearRifleCompanion : Companion
    {
        float Radius => 0.4f * Data.BodyScale; // body size (CompanionData.BodyScale)
        const float FlashTime = 0.07f;
        const int IdleFrames = 4, BlinkFrame = 4, ReloadFrame = 5;
        const float IdleFps = 6f, BlinkEvery = 3.5f, BlinkTime = 0.13f;
        const float AimTurnSpeed = 720f;   // degrees per second
        const float GripFraction = 0.31f;  // where the paws hold the rifle, from its back end (0..1 of its length)

        readonly BearRifleData data;
        readonly List<int> found = new List<int>(1);
        Vector2 aim = Vector2.right;
        float timer = 0.3f;
        int ammo;
        bool reloading;
        float reloadTime;
        float idleTime;
        float kick, flash;
        int flashFrame;

        public BearRifleCompanion(BearRifleData data) : base(data)
        {
            this.data = data;
            ammo = data.MagazineSize;
        }

        public override float SightRadius(CompanionContext ctx) => At(data.Range);

        bool AimsLeft => aim.x < 0f;
        float GunLength(CompanionContext ctx)
        {
            var s = ctx.Sprites.Rifle;
            return s != null ? data.GunSize * s.rect.width / s.rect.height : data.GunSize * 4f;
        }
        Vector2 Hands => Position + new Vector2(AimsLeft ? -0.17f : 0.17f, -0.02f) * Data.BodyScale;
        Vector2 GunDir => reloading ? ReloadDir : aim;
        Vector2 ReloadDir
        {
            get
            {
                // Tipped up while reloading, with a little wiggle.
                var deg = (AimsLeft ? 125f : 55f) + Mathf.Sin(reloadTime * 18f) * 6f;
                return new Vector2(Mathf.Cos(deg * Mathf.Deg2Rad), Mathf.Sin(deg * Mathf.Deg2Rad));
            }
        }
        Vector2 GunCenter(CompanionContext ctx) => Hands + GunDir * ((0.5f - GripFraction) * GunLength(ctx) - kick);
        Vector2 Muzzle(CompanionContext ctx) => GunCenter(ctx) + GunDir * (GunLength(ctx) * 0.48f);

        public override void Tick(float dt, CompanionContext ctx)
        {
            kick = Mathf.MoveTowards(kick, 0f, dt * 0.8f);
            flash -= dt;

            if (reloading)
            {
                reloadTime += dt;
                if (reloadTime >= data.ReloadTime * ctx.Player.Stats.Haste)
                {
                    reloading = false;
                    ammo = data.MagazineSize;
                    timer = Mathf.Min(timer, 0.1f);
                    ctx.Audio.PlayAtPitch(Sfx.Click, 0.7f, 0.6f, 0.05f);   // "clack": loaded
                }
                return;
            }

            timer -= dt;
            ctx.Enemies.FindNearest(Position, At(data.Range), 1, found);
            if (found.Count == 0)
            {
                timer = Mathf.Max(timer, 0f);
                idleTime += dt;
                if (data.IdleReloadDelay > 0f && idleTime >= data.IdleReloadDelay && ammo < data.MagazineSize) StartReload(ctx);
                return;
            }
            idleTime = 0f;

            // Swing the rifle toward the target.
            var want = (ctx.Enemies.Position[found[0]] - Position).normalized;
            var a = Mathf.Atan2(aim.y, aim.x) * Mathf.Rad2Deg;
            var b = Mathf.Atan2(want.y, want.x) * Mathf.Rad2Deg;
            a = Mathf.MoveTowardsAngle(a, b, AimTurnSpeed * dt) * Mathf.Deg2Rad;
            aim = new Vector2(Mathf.Cos(a), Mathf.Sin(a));

            if (timer > 0f) return;
            timer = data.ShotInterval;
            Fire(ctx);
            if (ammo <= 0) StartReload(ctx);
        }

        void StartReload(CompanionContext ctx)
        {
            reloading = true;
            reloadTime = 0f;
            idleTime = 0f;
            ctx.Audio.PlayAtPitch(Sfx.Click, 0.5f, 0.5f, 0.05f);
        }

        void Fire(CompanionContext ctx)
        {
            ammo--;
            var spread = Random.Range(-data.SpreadDegrees, data.SpreadDegrees) * Mathf.Deg2Rad;
            float c = Mathf.Cos(spread), s = Mathf.Sin(spread);
            var dir = new Vector2(aim.x * c - aim.y * s, aim.x * s + aim.y * c);
            var range = At(data.Range);
            ctx.Projectiles.Spawn(Muzzle(ctx), dir, data.BulletSpeed, At(data.Damage) * ctx.Player.Stats.Might,
                range / data.BulletSpeed, data.BulletRadius, At(data.Pierce), data.TracerColor, data.Knockback);
            kick = data.Recoil;
            flash = FlashTime;
            flashFrame++;
            ctx.Audio.PlayAtPitch(Sfx.Shoot, 0.55f, 0.75f, 0.03f);
        }

        public override void Draw(InstancedRenderer renderer, CompanionContext ctx)
        {
            var feet = Position.y - Radius;
            renderer.Ellipse(RenderLayer.Shadows, new Vector2(Position.x, feet + 0.05f), new Vector2(Radius * 1.9f, Radius * 0.55f),
                new Color(0f, 0f, 0f, 0.22f));

            var anim = ctx.Sprites.Bear;
            if (anim != null && !anim.IsEmpty)
                renderer.Sprite(RenderLayer.World, PickFrame(anim), new Vector2(Position.x, Position.y + Radius * 0.4f),
                    Radius * 2.7f, Color.white, flipX: AimsLeft, sortY: feet);
            else
                renderer.Circle(RenderLayer.World, Position, Radius, new Color(0.6f, 0.42f, 0.26f), 0f, feet);

            // Rifle in the paws, in front of the body.
            var dir = GunDir;
            var angle = Mathf.Atan2(dir.y, dir.x);
            var center = GunCenter(ctx);
            if (ctx.Sprites.Rifle != null)
                renderer.SpriteRotated(RenderLayer.World, ctx.Sprites.Rifle, center, data.GunSize, angle, Color.white,
                    flipY: AimsLeft, sortY: feet - 0.01f);
            else
                renderer.Streak(RenderLayer.World, center, GunLength(ctx), data.GunSize * 0.4f, angle, new Color(0.35f, 0.3f, 0.28f));

            if (flash > 0f)
            {
                var flashAnim = ctx.Sprites.MuzzleFlash;
                var muzzle = Muzzle(ctx);
                if (flashAnim != null && !flashAnim.IsEmpty)
                    renderer.SpriteRotated(RenderLayer.Effects, flashAnim.Frames[flashFrame % flashAnim.Frames.Length],
                        muzzle + dir * data.FlashSize, data.FlashSize, angle, Color.white);
                else
                    renderer.Circle(RenderLayer.Effects, muzzle, 0.15f, new Color(1f, 0.85f, 0.4f));
            }

            DrawAmmoBar(renderer, feet);
        }

        // Shots left (one segment per shot), or reload progress while reloading.
        void DrawAmmoBar(InstancedRenderer renderer, float feet)
        {
            if (data.BarWidth <= 0f) return;
            var center = new Vector2(Position.x, feet + data.BarOffsetY);
            var size = new Vector2(data.BarWidth, data.BarHeight);
            if (data.BarOutline > 0f)
                renderer.Rect(RenderLayer.Overlay, center, size + new Vector2(data.BarOutline * 2f, data.BarOutline * 2f), Color.black);
            if (reloading)
            {
                var k = Mathf.Clamp01(reloadTime / Mathf.Max(0.01f, data.ReloadTime));
                renderer.Bar(RenderLayer.Overlay, center, size, k, data.BarBackground, data.ReloadColor);
                return;
            }
            renderer.Rect(RenderLayer.Overlay, center, size, data.BarBackground);
            var n = Mathf.Max(1, data.MagazineSize);
            const float gap = 0.015f;
            var w = (size.x - gap * (n - 1)) / n;
            for (var i = 0; i < ammo && i < n; i++)
            {
                var x = center.x - size.x * 0.5f + w * 0.5f + i * (w + gap);
                renderer.Rect(RenderLayer.Overlay, new Vector2(x, center.y), new Vector2(w, size.y), data.AmmoColor);
            }
        }

        Sprite PickFrame(SpriteAnim anim)
        {
            var frames = anim.Frames;
            if (reloading && frames.Length > ReloadFrame) return frames[ReloadFrame];
            if (frames.Length <= BlinkFrame) return frames[0];
            var t = Time.time + Slot * 1.37f;
            if (t % BlinkEvery < BlinkTime) return frames[BlinkFrame];
            return frames[(int)((Time.time + Slot * 0.3f) * IdleFps) % IdleFrames];
        }
    }
}
