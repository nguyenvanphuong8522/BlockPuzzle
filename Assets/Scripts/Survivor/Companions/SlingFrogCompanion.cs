using System.Collections.Generic;
using UnityEngine;

namespace Survivor
{
    // Companion #5: a frog with a slingshot. It shoots a rubber pellet at the nearest enemy (A).
    //   - Enemies near A: the pellet bounces to the nearest one (B), then from B to the nearest new one (C). Max 2 bounces,
    //     each enemy hit once per shot; the chain stops early when nobody is close enough.
    //   - Nobody near A: A takes double damage and the pellet stops (a lone target gets punished).
    public sealed class SlingFrogCompanion : Companion
    {
        // Numbers and card text: Assets/Resources/Upgrades/Companions/SlingFrog.asset
        readonly SlingFrogData data;
        public SlingFrogCompanion(SlingFrogData data) : base(data) => this.data = data;
        float[] Damage => data.Damage;
        float[] Cooldown => data.Cooldown;

        const int MaxBouncesLimit = 6;           // most bounces the asset may ask for (sizes the hit list)
        int MaxBounces => Mathf.Clamp(data.MaxBounces, 0, MaxBouncesLimit); // A -> B -> C
        float LoneMultiplier => data.LoneMultiplier;
        float ShotSpeed => data.ShotSpeed;
        float BounceSpeed => data.BounceSpeed;
        float SightRange => data.SightRange;
        const float MaxFlightTime = 1.2f;     // a pellet that lost its target pops after this
        const float ShotPoseTime = 0.25f;

        float Radius => 0.4f * Data.BodyScale; // body size (CompanionData.BodyScale)
        const int IdleFrames = 4, ShotFrame = 4, BlinkFrame = 5;
        const float IdleFps = 6f, BlinkEvery = 3.5f, BlinkTime = 0.13f;

        sealed class Pellet
        {
            public Vector2 Position, TargetPos;
            public Vector2 Direction;               // last flight direction (for the trail)
            public int TargetIndex, TargetId;
            public int Bounce;                      // 0 = flying to A, 1 = to B, 2 = to C
            public readonly int[] HitIds = new int[MaxBouncesLimit + 1];
            public int HitCount;
            public float Flight;
            public float Squash;                    // little squash right after a bounce
        }

        readonly List<Pellet> pellets = new List<Pellet>();
        readonly Stack<Pellet> pool = new Stack<Pellet>();
        readonly List<int> found = new List<int>(32);
        float timer = 0.6f;
        float shotPose;
        bool facingLeft;

        public override float SightRadius(CompanionContext ctx) => SightRange;

        Vector2 SlingTip => Position + new Vector2(facingLeft ? -0.3f : 0.3f, 0.15f);

        public override void Tick(float dt, CompanionContext ctx)
        {
            shotPose -= dt;
            TickShoot(dt, ctx);
            for (var i = pellets.Count - 1; i >= 0; i--)
            {
                if (TickPellet(pellets[i], dt, ctx)) continue;
                pool.Push(pellets[i]);
                pellets.RemoveAt(i);
            }
        }

        void TickShoot(float dt, CompanionContext ctx)
        {
            timer -= dt;
            if (timer > 0f) return;
            ctx.Enemies.FindNearest(Position, SightRange, 1, found);
            if (found.Count == 0) return;

            timer = At(Cooldown) * ctx.Player.Stats.Haste;
            var e = found[0];
            facingLeft = ctx.Enemies.Position[e].x < Position.x;
            shotPose = ShotPoseTime;

            var p = pool.Count > 0 ? pool.Pop() : new Pellet();
            p.Position = SlingTip;
            p.TargetIndex = e;
            p.TargetId = ctx.Enemies.Id[e];
            p.TargetPos = ctx.Enemies.Position[e];
            p.Bounce = 0;
            p.HitCount = 0;
            p.Flight = 0f;
            p.Squash = 0f;
            p.Direction = (p.TargetPos - p.Position).normalized;
            pellets.Add(p);
            ctx.Audio.Play(Sfx.Sling, 0.5f, 0.1f, 0.04f);
        }

        // Returns false when the pellet is done.
        bool TickPellet(Pellet p, float dt, CompanionContext ctx)
        {
            p.Flight += dt;
            p.Squash = Mathf.Max(0f, p.Squash - dt * 6f);
            var enemies = ctx.Enemies;

            // Follow the target while it lives (indexes shift when enemies die, so check the id).
            var alive = p.TargetIndex < enemies.Count && enemies.Id[p.TargetIndex] == p.TargetId && enemies.IsAlive(p.TargetIndex);
            if (alive) p.TargetPos = enemies.Position[p.TargetIndex];

            var speed = p.Bounce == 0 ? ShotSpeed : BounceSpeed;
            var to = p.TargetPos - p.Position;
            var dist = to.magnitude;
            var reach = (alive ? enemies.Radius[p.TargetIndex] : 0f) + 0.1f;
            if (dist > reach + speed * dt)
            {
                p.Direction = to / dist;
                p.Position += p.Direction * speed * dt;
                if (p.Flight < MaxFlightTime) return true;
                Pop(p.Position, ctx);
                return false;
            }

            p.Position = p.TargetPos;
            if (!alive)
            {
                Pop(p.Position, ctx); // target died on the way: the pellet just pops
                return false;
            }
            return Hit(p, ctx);
        }

        // The pellet reached its target. Returns true if it bounces on to another enemy.
        bool Hit(Pellet p, CompanionContext ctx)
        {
            var enemies = ctx.Enemies;
            var target = p.TargetIndex;
            p.HitIds[p.HitCount++] = p.TargetId;

            // Next target: the nearest enemy around this one that this pellet hasn't hit yet.
            var next = -1;
            if (p.Bounce < MaxBounces)
            {
                var best = float.MaxValue;
                enemies.QueryRadius(p.Position, BounceRadiusFor(ctx), found);
                foreach (var e in found)
                {
                    if (e == target || AlreadyHit(p, enemies.Id[e])) continue;
                    var d = (enemies.Position[e] - p.Position).sqrMagnitude;
                    if (d < best)
                    {
                        best = d;
                        next = e;
                    }
                }
            }

            // First target standing alone: double damage.
            var lone = p.Bounce == 0 && next < 0;
            var damage = At(Damage) * ctx.Player.Stats.Might * (lone ? LoneMultiplier : 1f);
            var push = (p.Position - Position).normalized; // away from the frog
            enemies.Damage(target, damage, push, lone ? 2.5f : 1.5f);
            ctx.Effects.Ring(p.Position, lone ? 0.6f : 0.35f, lone ? new Color(1f, 0.55f, 0.2f) : new Color(1f, 0.8f, 0.5f), 0.2f);

            if (next < 0) return false;

            p.Bounce++;
            p.TargetIndex = next;
            p.TargetId = enemies.Id[next];
            p.TargetPos = enemies.Position[next];
            p.Flight = 0f;
            p.Squash = 1f;
            ctx.Audio.Play(Sfx.Boing, 0.4f, 0.15f, 0.04f);
            return true;
        }

        // Bounce radius for the current level (SlingFrog.asset).
        float BounceRadiusFor(CompanionContext ctx)
        {
            var table = data.BounceRadius;
            return table != null && table.Length > 0 ? At(table) : 2.5f;
        }

        static bool AlreadyHit(Pellet p, int id)
        {
            for (var k = 0; k < p.HitCount; k++)
                if (p.HitIds[k] == id) return true;
            return false;
        }

        static void Pop(Vector2 at, CompanionContext ctx) => ctx.Effects.Burst(at, 0.25f, new Color(1f, 0.6f, 0.4f, 0.8f), 0.12f);

        public override void Draw(InstancedRenderer renderer, CompanionContext ctx)
        {
            var feet = Position.y - Radius;
            renderer.Ellipse(RenderLayer.Shadows, new Vector2(Position.x, feet + 0.05f), new Vector2(Radius * 1.8f, Radius * 0.55f),
                new Color(0f, 0f, 0f, 0.22f));

            var anim = ctx.Sprites.Frog;
            if (anim != null && anim.Frames.Length >= 6 && anim.Frames[0] != null)
                renderer.Sprite(RenderLayer.World, PickFrame(anim), new Vector2(Position.x, Position.y + Radius * 0.4f),
                    Radius * 2.6f, Color.white, flipX: facingLeft, sortY: feet);
            else
                renderer.Circle(RenderLayer.World, Position, Radius, new Color(0.45f, 0.8f, 0.35f), 0f, feet);

            var tuning = data; // pellet size / trail live in SlingFrog.asset
            foreach (var p in pellets)
            {
                // Red trail behind the pellet, so it is easy to follow.
                if (tuning.PelletTrailLength > 0f && tuning.PelletTrailWidth > 0f)
                {
                    var angle = Mathf.Atan2(p.Direction.y, p.Direction.x);
                    renderer.Streak(RenderLayer.Projectiles, p.Position - p.Direction * (tuning.PelletTrailLength * 0.5f),
                        tuning.PelletTrailLength, tuning.PelletTrailWidth, angle, tuning.PelletTrailColor);
                }
                var size = tuning.PelletSize * (1f + p.Squash * 0.4f);
                if (ctx.Sprites.Pellet != null)
                    renderer.Sprite(RenderLayer.Projectiles, ctx.Sprites.Pellet, p.Position, size, Color.white);
                else
                    renderer.Circle(RenderLayer.Projectiles, p.Position, size * 0.5f, new Color(1f, 0.42f, 0.23f));
            }
        }

        Sprite PickFrame(SpriteAnim anim)
        {
            var frames = anim.Frames;
            if (shotPose > 0f) return frames[ShotFrame];
            var t = Time.time + Slot * 1.37f;
            if (t % BlinkEvery < BlinkTime) return frames[BlinkFrame];
            return frames[(int)((Time.time + Slot * 0.3f) * IdleFps) % IdleFrames];
        }
    }
}
