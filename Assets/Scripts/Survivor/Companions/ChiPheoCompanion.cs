using System.Collections.Generic;
using UnityEngine;

namespace Survivor
{
    // Art: companion_drunk_0..5 (0-3 drunken wobble, 4 throw, 5 hiccup), projectile_firebottle, fx_fire_0..3.
    // A drunk village tough: lobs a green fire bottle in a high arc onto the nearest enemy (more bottles at higher
    // levels, each at another nearby enemy). It shatters where it lands and the ground burns for Fire Duration
    // seconds: every enemy inside the patch loses Fire Dps per second.
    // Every number and the card text: Assets/Resources/Upgrades/Companions/ChiPheo.asset.
    public sealed class ChiPheoCompanion : Companion
    {
        const float Radius = 0.4f;
        const float ThrowPoseTime = 0.3f;
        const int WobbleFrames = 4, ThrowFrame = 4, HicFrame = 5;
        const float WobbleFps = 4f, HicEvery = 4.3f, HicTime = 0.35f;

        sealed class Bottle
        {
            public Vector2 From, To;
            public float T, Duration, Spin;
            public float Dps, Radius, Life;
        }

        sealed class Patch
        {
            public Vector2 Center;
            public float Radius, Dps, Life, Age, Tick;
            public readonly Vector2[] Offsets = new Vector2[30];
            public readonly float[] Phase = new float[30];
        }

        readonly ChiPheoData data;
        readonly List<int> found = new List<int>(4);
        readonly List<int> hits = new List<int>(64);
        readonly List<Bottle> bottles = new List<Bottle>();
        readonly List<Patch> patches = new List<Patch>();
        readonly Stack<Patch> pool = new Stack<Patch>();
        float timer = 0.6f;
        float throwPose;
        bool facingLeft;

        public ChiPheoCompanion(ChiPheoData data) : base(data) => this.data = data;

        public override float SightRadius(CompanionContext ctx) => data.SightRange;

        public override void Tick(float dt, CompanionContext ctx)
        {
            throwPose -= dt;
            TickBottles(dt, ctx);
            TickPatches(dt, ctx);

            timer -= dt;
            if (timer > 0f) return;
            var n = Mathf.Max(1, At(data.Bottles));
            ctx.Enemies.FindNearest(Position, data.SightRange, n, found);
            if (found.Count == 0) return;

            timer = At(data.Cooldown) * ctx.Player.Stats.Haste;
            throwPose = ThrowPoseTime;
            facingLeft = ctx.Enemies.Position[found[0]].x < Position.x;
            for (var k = 0; k < n; k++)
            {
                var target = ctx.Enemies.Position[found[k % found.Count]];
                if (k >= found.Count) target += Random.insideUnitCircle * At(data.FireRadius); // fewer enemies: spread
                Throw(ctx, target);
            }
            ctx.Audio.PlayAtPitch(Sfx.Throw, 0.85f, 0.6f, 0.05f);
        }

        void Throw(CompanionContext ctx, Vector2 target)
        {
            var from = Position + new Vector2(facingLeft ? -0.3f : 0.3f, 0.35f);
            var dist = (target - from).magnitude;
            var lo = Mathf.Min(data.FlightTime.x, data.FlightTime.y);
            var hi = Mathf.Max(data.FlightTime.x, data.FlightTime.y);
            bottles.Add(new Bottle
            {
                From = from, To = target, T = 0f, Spin = Random.Range(0f, 360f),
                Duration = Mathf.Clamp(dist / data.ThrowSpeed, lo, hi),
                Dps = At(data.FireDps) * ctx.Player.Stats.Might, Radius = At(data.FireRadius), Life = At(data.FireDuration)
            });
        }

        void TickBottles(float dt, CompanionContext ctx)
        {
            for (var i = bottles.Count - 1; i >= 0; i--)
            {
                var b = bottles[i];
                b.T += dt / Mathf.Max(0.05f, b.Duration);
                b.Spin += data.BottleSpin * dt;
                if (b.T < 1f) continue;
                bottles.RemoveAt(i);
                Shatter(ctx, b);
            }
        }

        // The bottle breaks: a burst of fire, a burning patch, and (optionally) a first hit to everything in it.
        void Shatter(CompanionContext ctx, Bottle b)
        {
            var p = pool.Count > 0 ? pool.Pop() : new Patch();
            p.Center = b.To;
            p.Radius = b.Radius;
            p.Dps = b.Dps;
            p.Life = b.Life;
            p.Age = 0f;
            p.Tick = data.TickInterval;
            var n = Mathf.Clamp(data.Flames, 1, p.Offsets.Length);
            for (var k = 0; k < n; k++)
            {
                // Spread over the patch (sqrt for an even fill), flattened like the ground.
                var a = Random.Range(0f, Mathf.PI * 2f);
                var r = Mathf.Sqrt(Random.value) * p.Radius * 0.85f;
                p.Offsets[k] = new Vector2(Mathf.Cos(a) * r, Mathf.Sin(a) * r * 0.6f);
                p.Phase[k] = Random.Range(0f, 10f);
            }
            patches.Add(p);

            if (data.ImpactBurstSeconds > 0f) Burn(ctx, p, p.Dps * data.ImpactBurstSeconds);
            ctx.Effects.Burst(b.To, p.Radius * 1.1f, new Color(1f, 0.55f, 0.15f), 0.25f);
            ctx.Effects.Ring(b.To, p.Radius, new Color(0.45f, 0.9f, 0.45f), 0.2f);   // green glass splash
            ctx.Effects.Smoke(b.To + new Vector2(0f, 0.3f), p.Radius * 0.7f, 0.5f);
            ctx.Audio.PlayAtPitch(Sfx.Explode, 1.5f, 0.35f, 0.05f);
        }

        void TickPatches(float dt, CompanionContext ctx)
        {
            for (var i = patches.Count - 1; i >= 0; i--)
            {
                var p = patches[i];
                p.Age += dt;
                p.Tick -= dt;
                while (p.Tick <= 0f && p.Age <= p.Life)
                {
                    p.Tick += data.TickInterval;
                    Burn(ctx, p, p.Dps * data.TickInterval);
                }
                if (p.Age < p.Life) continue;
                patches.RemoveAt(i);
                pool.Push(p);
            }
        }

        void Burn(CompanionContext ctx, Patch p, float damage)
        {
            ctx.Enemies.QueryRadius(p.Center, p.Radius, hits);
            foreach (var e in hits) ctx.Enemies.Damage(e, damage);
        }

        // ---------------- Drawing ----------------

        public override void DrawAttackArea(InstancedRenderer renderer, CompanionContext ctx)
        {
            foreach (var p in patches)
                renderer.Ring(RenderLayer.Overlay, p.Center, p.Radius, 0.04f, new Color(1f, 0.4f, 0.1f, 0.8f));
        }

        public override void Draw(InstancedRenderer renderer, CompanionContext ctx)
        {
            DrawPatches(renderer, ctx);

            var r = Radius * data.BodyScale;
            var feet = Position.y - r;
            renderer.Ellipse(RenderLayer.Shadows, new Vector2(Position.x, feet + 0.05f), new Vector2(r * 1.8f, r * 0.55f),
                new Color(0f, 0f, 0f, 0.22f));
            var anim = ctx.Sprites.Drunk;
            if (anim != null && !anim.IsEmpty)
                renderer.Sprite(RenderLayer.World, PickFrame(anim), new Vector2(Position.x, Position.y + r * 0.4f), r * 2.6f,
                    Color.white, flipX: facingLeft, sortY: feet);
            else
                renderer.Circle(RenderLayer.World, Position, r, new Color(0.55f, 0.36f, 0.22f), 0f, feet);

            DrawBottles(renderer, ctx);
        }

        void DrawBottles(InstancedRenderer renderer, CompanionContext ctx)
        {
            var sprite = ctx.Sprites.FireBottle;
            foreach (var b in bottles)
            {
                var t = Mathf.Clamp01(b.T);
                var ground = Vector2.Lerp(b.From, b.To, t);
                var height = Mathf.Sin(t * Mathf.PI) * data.ArcHeight;
                // Shadow on the ground grows as it comes down.
                renderer.Ellipse(RenderLayer.Shadows, ground, new Vector2(0.35f, 0.12f) * (0.6f + 0.4f * t), new Color(0f, 0f, 0f, 0.25f));
                var at = ground + new Vector2(0f, height);
                if (sprite != null)
                    renderer.SpriteRotated(RenderLayer.Projectiles, sprite, at, data.BottleSize, b.Spin * Mathf.Deg2Rad, Color.white);
                else
                    renderer.Circle(RenderLayer.Projectiles, at, 0.12f, new Color(0.3f, 0.8f, 0.35f));
            }
        }

        void DrawPatches(InstancedRenderer renderer, CompanionContext ctx)
        {
            var fire = ctx.Sprites.Fire;
            foreach (var p in patches)
            {
                // Grows in fast, fades out over the last Fade Time.
                var grow = Mathf.Clamp01(p.Age / 0.15f);
                var fade = Mathf.Clamp01((p.Life - p.Age) / data.FadeTime);
                var a = grow * fade;
                var size = new Vector2(p.Radius * 2f, p.Radius * 1.2f) * (0.7f + 0.3f * grow);
                var scorch = data.Scorch;
                scorch.a *= a;
                renderer.Ellipse(RenderLayer.Shadows, p.Center, size, scorch);
                var glow = data.GroundGlow;
                glow.a *= a * (0.85f + 0.15f * Mathf.Sin(Time.time * 9f + p.Center.x));
                renderer.Ellipse(RenderLayer.Shadows, p.Center, size * 0.92f, glow);

                if (fire == null || fire.IsEmpty) continue;
                var n = Mathf.Clamp(data.Flames, 1, p.Offsets.Length);
                var flameH = data.FlameSize * Mathf.Lerp(0.8f, 1.25f, Mathf.InverseLerp(1f, 2f, p.Radius));
                for (var k = 0; k < n; k++)
                {
                    var off = p.Offsets[k];
                    var ph = p.Phase[k];
                    var frame = fire.Frames[(int)((Time.time + ph) * fire.Fps) % fire.Frames.Length];
                    var h = flameH * (0.75f + 0.25f * Mathf.Sin(Time.time * 7f + ph)) * grow * Mathf.Lerp(0.3f, 1f, fade);
                    var basePos = p.Center + off;
                    var tint = data.FlameTint;
                    tint.a *= a;
                    // Base at the bottom of the sprite: lift its center by half its height. Sorted with the world.
                    renderer.Sprite(RenderLayer.World, frame, basePos + new Vector2(0f, h * 0.5f), h, tint, sortY: basePos.y);
                }
            }
        }

        // Drunken wobble; arm up right after a throw; a hiccup now and then.
        Sprite PickFrame(SpriteAnim anim)
        {
            var frames = anim.Frames;
            if (frames.Length <= ThrowFrame) return frames[0];
            if (throwPose > 0f) return frames[ThrowFrame];
            var t = Time.time + Slot * 1.7f;
            if (frames.Length > HicFrame && t % HicEvery < HicTime) return frames[HicFrame];
            return frames[(int)((Time.time + Slot * 0.3f) * WobbleFps) % WobbleFrames];
        }
    }
}
