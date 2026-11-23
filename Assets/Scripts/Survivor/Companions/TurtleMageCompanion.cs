using System.Collections.Generic;
using UnityEngine;

namespace Survivor
{
    // Companion #3: a turtle mage. It shoots a magic orb at the nearest enemy. The orb damages the first enemy it
    // touches, then explodes, hurting everything in a circle that grows with the level. An orb that hits nothing
    // flies for a while and explodes on its own.
    public sealed class TurtleMageCompanion : Companion
    {
        static readonly float[] OrbDamage = { 18, 22, 28, 34, 42 };
        static readonly float[] BlastDamage = { 10, 13, 17, 22, 28 };
        static readonly float[] BlastRadius = { 1.0f, 1.3f, 1.6f, 1.9f, 2.3f };
        static readonly float[] Cooldown = { 2.0f, 1.9f, 1.7f, 1.5f, 1.3f };
        static readonly int[] Orbs = { 1, 1, 1, 2, 2 };

        const float OrbSpeed = 7.5f;
        const float OrbLifetime = 1.1f;     // missed orbs explode after this
        const float OrbRadius = 0.22f;
        const float OrbSize = 0.55f;
        const float SpreadDegrees = 14f;
        const float SightRange = 9f;
        const float CastPoseTime = 0.3f;
        const float BlastTime = 0.4f;

        const float Radius = 0.4f;
        const int IdleFrames = 4, CastFrame = 4, BlinkFrame = 5;
        const float IdleFps = 6f, BlinkEvery = 3.4f, BlinkTime = 0.13f;

        struct Orb
        {
            public Vector2 Position, Velocity;
            public float Age;
        }

        struct Blast
        {
            public Vector2 Position;
            public float Radius, Age;
        }

        readonly List<Orb> orbs = new List<Orb>();
        readonly List<Blast> blasts = new List<Blast>();
        readonly List<int> found = new List<int>(16);
        float timer = 1.0f;
        float castPose;
        bool facingLeft;

        public override string Name => "Rùa Pháp Sư";
        public override string Description => "Bắn cầu phép: trúng kẻ địch thì gây sát thương rồi nổ tung cả vùng xung quanh.";
        public override Color Tint => new Color(0.85f, 0.55f, 1f);
        public override string NextLevelDescription => (Level + 1) switch
        {
            2 => "Sát thương cao hơn, vùng nổ to hơn.",
            3 => "Vùng nổ to hơn, bắn nhanh hơn.",
            4 => "Bắn 2 quả cầu mỗi lần.",
            _ => "Vùng nổ cực lớn, sát thương cao nhất."
        };

        // Where the orb leaves: the gem on top of the staff.
        Vector2 StaffTip => Position + new Vector2(facingLeft ? -0.36f : 0.36f, 0.3f);

        public override void Tick(float dt, CompanionContext ctx)
        {
            castPose -= dt;
            TickCast(dt, ctx);
            TickOrbs(dt, ctx);
            for (var i = blasts.Count - 1; i >= 0; i--)
            {
                var b = blasts[i];
                b.Age += dt;
                if (b.Age >= BlastTime) blasts.RemoveAt(i);
                else blasts[i] = b;
            }
        }

        void TickCast(float dt, CompanionContext ctx)
        {
            timer -= dt;
            if (timer > 0f) return;

            ctx.Enemies.FindNearest(Position, SightRange, 1, found);
            if (found.Count == 0) return;

            timer = At(Cooldown) * ctx.Player.Stats.Haste;
            var aim = (ctx.Enemies.Position[found[0]] - Position).normalized;
            facingLeft = aim.x < 0f;
            castPose = CastPoseTime;

            var n = At(Orbs);
            for (var k = 0; k < n; k++)
            {
                var offset = (k - (n - 1) / 2f) * SpreadDegrees * Mathf.Deg2Rad;
                var c = Mathf.Cos(offset);
                var s = Mathf.Sin(offset);
                var dir = new Vector2(aim.x * c - aim.y * s, aim.x * s + aim.y * c);
                orbs.Add(new Orb { Position = StaffTip, Velocity = dir * OrbSpeed });
            }
            ctx.Effects.Burst(StaffTip, 0.35f, new Color(0.9f, 0.6f, 1f, 0.8f), 0.15f);
            ctx.Audio.Play(Sfx.Cast, 0.5f, 0.1f, 0.05f);
        }

        void TickOrbs(float dt, CompanionContext ctx)
        {
            for (var i = orbs.Count - 1; i >= 0; i--)
            {
                var o = orbs[i];
                o.Age += dt;
                o.Position += o.Velocity * dt;

                ctx.Enemies.QueryRadius(o.Position, OrbRadius, found);
                if (found.Count > 0)
                {
                    // Direct hit on the first enemy touched, then the explosion right there.
                    var hit = found[0];
                    ctx.Enemies.Damage(hit, At(OrbDamage) * ctx.Player.Stats.Might, o.Velocity.normalized, 1.5f);
                    Explode(o.Position, ctx);
                    orbs.RemoveAt(i);
                }
                else if (o.Age >= OrbLifetime)
                {
                    Explode(o.Position, ctx); // missed: blows up where it is
                    orbs.RemoveAt(i);
                }
                else
                {
                    orbs[i] = o;
                }
            }
        }

        void Explode(Vector2 at, CompanionContext ctx)
        {
            var radius = At(BlastRadius) * ctx.Player.Stats.Area;
            var damage = At(BlastDamage) * ctx.Player.Stats.Might;
            ctx.Enemies.QueryRadius(at, radius, found);
            foreach (var e in found)
            {
                var push = ctx.Enemies.Position[e] - at;
                ctx.Enemies.Damage(e, damage, push.sqrMagnitude > 0.0001f ? push.normalized : Vector2.up, 3f);
            }
            blasts.Add(new Blast { Position = at, Radius = radius });
            ctx.Effects.Ring(at, radius, new Color(0.9f, 0.65f, 1f, 0.9f), 0.3f);
            ctx.Shake.Add(0.04f + radius * 0.02f);
            ctx.Audio.Play(Sfx.Explode, 0.55f, 0.12f, 0.06f);
        }

        public override void Draw(InstancedRenderer renderer, CompanionContext ctx)
        {
            var feet = Position.y - Radius;
            renderer.Ellipse(RenderLayer.Shadows, new Vector2(Position.x, feet + 0.05f), new Vector2(Radius * 1.9f, Radius * 0.55f),
                new Color(0f, 0f, 0f, 0.22f));

            var anim = ctx.Sprites.Turtle;
            if (anim != null && anim.Frames.Length >= 6 && anim.Frames[0] != null)
            {
                renderer.Sprite(RenderLayer.World, PickFrame(anim), new Vector2(Position.x, Position.y + Radius * 0.4f),
                    Radius * 2.6f, Color.white, flipX: facingLeft, sortY: feet);
            }
            else
            {
                renderer.Circle(RenderLayer.World, Position, Radius, new Color(0.3f, 0.65f, 0.4f), 0f, feet);
            }

            DrawOrbs(renderer, ctx);
            DrawBlasts(renderer, ctx);
        }

        Sprite PickFrame(SpriteAnim anim)
        {
            var frames = anim.Frames;
            if (castPose > 0f) return frames[CastFrame];
            var t = Time.time + Slot * 1.37f;
            if (t % BlinkEvery < BlinkTime) return frames[BlinkFrame];
            return frames[(int)((Time.time + Slot * 0.3f) * IdleFps) % IdleFrames];
        }

        void DrawOrbs(InstancedRenderer renderer, CompanionContext ctx)
        {
            var anim = ctx.Sprites.MagicOrb;
            var hasArt = anim != null && !anim.IsEmpty;
            foreach (var o in orbs)
            {
                // Soft trail behind the orb.
                var angle = Mathf.Atan2(o.Velocity.y, o.Velocity.x);
                renderer.Streak(RenderLayer.Projectiles, o.Position - o.Velocity.normalized * 0.3f, 0.7f, 0.28f, angle,
                    new Color(0.85f, 0.55f, 1f, 0.35f));
                if (hasArt) renderer.Sprite(RenderLayer.Projectiles, anim.Frame(o.Age + Time.time), o.Position, OrbSize, Color.white);
                else renderer.Circle(RenderLayer.Projectiles, o.Position, OrbRadius, new Color(0.85f, 0.55f, 1f));
            }
        }

        void DrawBlasts(InstancedRenderer renderer, CompanionContext ctx)
        {
            var anim = ctx.Sprites.Blast;
            var hasArt = anim != null && anim.Frames.Length >= 4 && anim.Frames[0] != null;
            foreach (var b in blasts)
            {
                var t = b.Age / BlastTime;
                if (hasArt)
                {
                    // flash -> burst -> puffs -> fading ring; grows quickly to full size.
                    var frame = anim.Frames[Mathf.Min(3, (int)(t * 4f))];
                    var grow = Mathf.Lerp(0.6f, 1f, Mathf.Min(1f, t * 3f));
                    var alpha = t < 0.6f ? 1f : 1f - (t - 0.6f) / 0.4f;
                    var height = b.Radius * 2f * 128f / 116f * grow; // the burst's star tips reach 116/128 of the image
                    renderer.Sprite(RenderLayer.Effects, frame, b.Position, height, new Color(1f, 1f, 1f, alpha));
                }
                else
                {
                    renderer.Circle(RenderLayer.Effects, b.Position, b.Radius * Mathf.Min(1f, t * 3f), new Color(0.9f, 0.6f, 1f, 0.5f * (1f - t)));
                }
            }
        }
    }
}
