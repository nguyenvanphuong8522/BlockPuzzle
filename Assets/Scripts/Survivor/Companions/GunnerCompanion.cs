using System.Collections.Generic;
using UnityEngine;

namespace Survivor
{
    // Companion #4: a little gunner with a small machine gun. Fires one bullet every 0.14 s at the nearest enemy,
    // non-stop while something is in range. Levels add damage, range, piercing and accuracy (the rate stays the same).
    public sealed class GunnerCompanion : Companion
    {
        public const float FireInterval = 0.14f;

        static readonly float[] Damage = { 3f, 4f, 5f, 7f, 9f };
        static readonly float[] Range = { 6f, 6.5f, 7f, 7.5f, 8f };
        static readonly int[] Pierce = { 0, 0, 1, 1, 2 };
        static readonly float[] SpreadDegrees = { 8f, 7f, 6f, 5f, 4f };

        const float BulletSpeed = 16f;
        const float BulletRadius = 0.08f;
        const float AimTurnSpeed = 18f;     // how fast the gun swings toward a new target
        const float GunSize = 0.24f;        // world height of the gun sprite
        const float GripOffset = 0.1f;      // the gun's center sits this far in front of the hands
        const float MuzzleOffset = 0.2f;    // muzzle tip, measured from the gun's center
        const float FlashTime = 0.05f;
        const float FlashSize = 0.24f;
        const float Recoil = 0.05f;

        const float Radius = 0.4f;
        const int IdleFrames = 4, BlinkFrame = 4;
        const float IdleFps = 6f, BlinkEvery = 3.3f, BlinkTime = 0.13f;

        static readonly Color TracerColor = new Color(1f, 0.9f, 0.5f, 0.85f);

        readonly List<int> found = new List<int>(1);
        float timer = 0.4f;
        Vector2 aim = Vector2.right;
        float kick;
        float flash;
        int flashFrame;

        public override string Name => "Xạ Thủ Nhí";
        public override string Description => "Cầm súng tiểu liên, bắn liên tục vào kẻ địch gần nhất (0,14 giây một viên).";
        public override Color Tint => new Color(0.45f, 0.65f, 1f);
        public override string NextLevelDescription => (Level + 1) switch
        {
            2 => "Sát thương cao hơn, bắn xa hơn.",
            3 => "Đạn xuyên qua 1 kẻ địch.",
            4 => "Sát thương cao hơn, bắn chụm hơn.",
            _ => "Đạn xuyên qua 2 kẻ địch, sát thương cao nhất."
        };

        bool AimsLeft => aim.x < 0f;
        Vector2 Hands => Position + new Vector2(AimsLeft ? -0.17f : 0.17f, 0f);
        Vector2 GunCenter => Hands + aim * (GripOffset - kick);
        Vector2 Muzzle => GunCenter + aim * MuzzleOffset;

        public override void Tick(float dt, CompanionContext ctx)
        {
            kick = Mathf.MoveTowards(kick, 0f, dt * 0.6f);
            flash -= dt;
            timer -= dt;

            ctx.Enemies.FindNearest(Position, At(Range), 1, found);
            if (found.Count == 0)
            {
                timer = Mathf.Max(timer, 0f); // ready to fire as soon as something comes close
                return;
            }

            // Swing the gun toward the target.
            var want = (ctx.Enemies.Position[found[0]] - Muzzle).normalized;
            var a = Mathf.Atan2(aim.y, aim.x);
            var b = Mathf.Atan2(want.y, want.x);
            a = Mathf.MoveTowardsAngle(a * Mathf.Rad2Deg, b * Mathf.Rad2Deg, AimTurnSpeed * 60f * dt) * Mathf.Deg2Rad;
            aim = new Vector2(Mathf.Cos(a), Mathf.Sin(a));

            // Fixed rate: catch up if a frame took longer than one interval.
            while (timer <= 0f)
            {
                timer += FireInterval;
                Fire(ctx);
            }
        }

        void Fire(CompanionContext ctx)
        {
            var spread = Random.Range(-At(SpreadDegrees), At(SpreadDegrees)) * Mathf.Deg2Rad;
            var c = Mathf.Cos(spread);
            var s = Mathf.Sin(spread);
            var dir = new Vector2(aim.x * c - aim.y * s, aim.x * s + aim.y * c);
            ctx.Projectiles.Spawn(Muzzle, dir, BulletSpeed, At(Damage) * ctx.Player.Stats.Might, At(Range) / BulletSpeed,
                BulletRadius, At(Pierce), TracerColor);

            kick = Recoil;
            flash = FlashTime;
            flashFrame++;
            ctx.Audio.Play(Sfx.Smg, 0.3f, 0.12f, 0.02f);
        }

        public override void Draw(InstancedRenderer renderer, CompanionContext ctx)
        {
            var feet = Position.y - Radius;
            renderer.Ellipse(RenderLayer.Shadows, new Vector2(Position.x, feet + 0.05f), new Vector2(Radius * 1.8f, Radius * 0.55f),
                new Color(0f, 0f, 0f, 0.22f));

            var anim = ctx.Sprites.Gunner;
            if (anim != null && anim.Frames.Length >= 5 && anim.Frames[0] != null)
            {
                renderer.Sprite(RenderLayer.World, PickFrame(anim), new Vector2(Position.x, Position.y + Radius * 0.4f),
                    Radius * 2.6f, Color.white, flipX: AimsLeft, sortY: feet);
            }
            else
            {
                renderer.Circle(RenderLayer.World, Position, Radius, new Color(0.35f, 0.5f, 0.8f), 0f, feet);
            }

            // Gun in the hands, always in front of the body.
            var angle = Mathf.Atan2(aim.y, aim.x);
            if (ctx.Sprites.Smg != null)
                renderer.SpriteRotated(RenderLayer.World, ctx.Sprites.Smg, GunCenter, GunSize, angle, Color.white,
                    flipY: AimsLeft, sortY: feet - 0.01f);
            else
                renderer.Streak(RenderLayer.World, GunCenter, 0.45f, 0.12f, angle, new Color(0.3f, 0.33f, 0.4f));

            if (flash > 0f)
            {
                var flashAnim = ctx.Sprites.MuzzleFlash;
                if (flashAnim != null && !flashAnim.IsEmpty)
                {
                    // The flash sprite is a cone starting at its left edge, twice as wide as tall.
                    var frame = flashAnim.Frames[flashFrame % flashAnim.Frames.Length];
                    renderer.SpriteRotated(RenderLayer.Effects, frame, Muzzle + aim * FlashSize, FlashSize, angle, Color.white);
                }
                else
                {
                    renderer.Circle(RenderLayer.Effects, Muzzle, 0.1f, new Color(1f, 0.85f, 0.4f));
                }
            }
        }

        Sprite PickFrame(SpriteAnim anim)
        {
            var frames = anim.Frames;
            var t = Time.time + Slot * 1.37f;
            if (t % BlinkEvery < BlinkTime) return frames[BlinkFrame];
            return frames[(int)((Time.time + Slot * 0.3f) * IdleFps) % IdleFrames];
        }
    }
}
