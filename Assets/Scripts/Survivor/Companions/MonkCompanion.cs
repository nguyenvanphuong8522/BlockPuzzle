using System.Collections.Generic;
using UnityEngine;

namespace Survivor
{
    // Art: companion_monk_0..2 (0 chanting, 1 mouth closed, 2 striking the wooden fish).
    // A monk sitting cross-legged next to the hero, tapping a wooden fish drum and chanting. Every tap
    // (Pulse Interval, 1 s) sends a golden ring out from the hero; it grows to Max Radius and fades. Enemies the
    // ring's edge sweeps over take damage and are pushed outward, once per ring.
    // Every number and the card text: Assets/Resources/Upgrades/Companions/Monk.asset.
    public sealed class MonkCompanion : Companion
    {
        const float Radius = 0.4f;
        const float HitPoseTime = 0.14f;
        const float ChantFps = 3f;
        // Rings are drawn under the hero, companions and enemies (World layer), above the ground and the gems.
        const RenderLayer RingLayer = RenderLayer.Pickups;

        sealed class Wave
        {
            public Vector2 Center;
            public float Radius, PrevRadius, MaxRadius, Damage;
            public readonly HashSet<int> Hit = new HashSet<int>();
        }

        readonly MonkData data;
        readonly List<Wave> waves = new List<Wave>();
        readonly Stack<Wave> pool = new Stack<Wave>();
        readonly List<int> hits = new List<int>(128);
        readonly List<int> near = new List<int>(1);
        float timer = 0.3f;
        float hitPose;

        public MonkCompanion(MonkData data) : base(data) => this.data = data;

        public override float SightRadius(CompanionContext ctx) => At(data.MaxRadius);
        public override Vector2 SightCenter(CompanionContext ctx) => ctx.Player.Position;

        public override void Tick(float dt, CompanionContext ctx)
        {
            hitPose -= dt;
            TickWaves(dt, ctx);

            timer -= dt;
            if (timer > 0f) return;
            if (data.OnlyWhenEnemies)
            {
                ctx.Enemies.FindNearest(ctx.Player.Position, At(data.MaxRadius), 1, near);
                if (near.Count == 0)
                {
                    timer = 0f; // tap as soon as something comes close
                    return;
                }
            }
            timer = data.PulseInterval * ctx.Player.Stats.Haste;
            Tap(ctx);
        }

        // "Tok!": one tap of the wooden fish = one ring.
        void Tap(CompanionContext ctx)
        {
            var w = pool.Count > 0 ? pool.Pop() : new Wave();
            w.Hit.Clear();
            w.Center = ctx.Player.Position;
            w.Radius = w.PrevRadius = data.StartRadius;
            w.MaxRadius = At(data.MaxRadius);
            w.Damage = At(data.Damage) * ctx.Player.Stats.Might;
            waves.Add(w);
            hitPose = HitPoseTime;
            ctx.Audio.PlayAtPitch(Sfx.Click, data.TapPitch, data.TapVolume, 0.05f);
        }

        void TickWaves(float dt, CompanionContext ctx)
        {
            for (var i = waves.Count - 1; i >= 0; i--)
            {
                var w = waves[i];
                if (data.FollowHero) w.Center = ctx.Player.Position;
                w.PrevRadius = w.Radius;
                w.Radius += data.ExpandSpeed * dt;

                // Hit what the edge swept over this frame (between the old and the new radius, +- Hit Width).
                var outer = w.Radius + data.HitWidth;
                var inner = Mathf.Max(0f, w.PrevRadius - data.HitWidth);
                ctx.Enemies.QueryRadius(w.Center, outer, hits);
                foreach (var e in hits)
                {
                    var off = ctx.Enemies.Position[e] - w.Center;
                    var d = off.magnitude;
                    if (d < inner) continue;
                    if (!w.Hit.Add(ctx.Enemies.Id[e])) continue;
                    ctx.Enemies.Damage(e, w.Damage, d > 0.001f ? off / d : Vector2.up, data.Knockback);
                }

                if (w.Radius >= w.MaxRadius)
                {
                    waves.RemoveAt(i);
                    pool.Push(w);
                }
            }
        }

        public override void Draw(InstancedRenderer renderer, CompanionContext ctx)
        {
            var r = Radius * data.BodyScale;
            var feet = Position.y - r;
            renderer.Ellipse(RenderLayer.Shadows, new Vector2(Position.x, feet + 0.06f), new Vector2(r * 2.3f, r * 0.6f),
                new Color(0f, 0f, 0f, 0.22f));

            var anim = ctx.Sprites.Monk;
            if (anim != null && !anim.IsEmpty)
                renderer.Sprite(RenderLayer.World, PickFrame(anim), new Vector2(Position.x, Position.y + r * 0.4f), r * 2.7f,
                    Color.white, flipX: Position.x > ctx.Player.Position.x, sortY: feet);
            else
                renderer.Circle(RenderLayer.World, Position, r, new Color(0.95f, 0.65f, 0.2f), 0f, feet);

            DrawWaves(renderer);
        }

        void DrawWaves(InstancedRenderer renderer)
        {
            foreach (var w in waves)
            {
                var k = Mathf.Clamp01((w.Radius - data.StartRadius) / Mathf.Max(0.01f, w.MaxRadius - data.StartRadius));
                var fade = 1f - Mathf.Clamp01((k - (1f - data.FadePart)) / data.FadePart);
                if (data.GlowColor.a > 0f && data.GlowThickness > 0f)
                {
                    var glow = data.GlowColor;
                    glow.a *= fade;
                    renderer.Ring(RingLayer, w.Center, w.Radius + data.GlowThickness * 0.5f, data.GlowThickness / Mathf.Max(0.01f, w.Radius + data.GlowThickness * 0.5f), glow);
                }
                var edge = data.RingColor;
                edge.a *= fade;
                renderer.Ring(RingLayer, w.Center, w.Radius + data.RingThickness * 0.5f, data.RingThickness / Mathf.Max(0.01f, w.Radius + data.RingThickness * 0.5f), edge);
            }
        }

        // Faces the hero. Striking pose right after a tap, otherwise chanting (mouth opening and closing).
        Sprite PickFrame(SpriteAnim anim)
        {
            var frames = anim.Frames;
            if (hitPose > 0f && frames.Length > 2) return frames[2];
            if (frames.Length < 2) return frames[0];
            return frames[(int)((Time.time + Slot * 0.4f) * ChantFps) % 2];
        }
    }
}
