using System.Collections.Generic;
using UnityEngine;

namespace Survivor
{
    // Movement/timing numbers of a dash-and-swing companion (each one reads its own section of PlayerTuning).
    public struct MeleeTiming
    {
        public float DashSpeed, ReturnSpeed, MaxDashTime, StayTime, DoubleSwingGap, SightRange;
    }

    // Shared logic for melee companions (sword otter, club pig): dash to the nearest enemy, hit everything in a
    // 120-degree arc facing it, stay a moment, then run back to the place next to the hero.
    // Subclasses give the numbers, the art and the swing effect color.
    public abstract class MeleeDashCompanion : Companion
    {
        protected const float ArcDegrees = 120f;
        protected const float SwingTime = 0.22f;  // one swing: effect + swing pose
        const int IdleFrames = 4, DashFrame = 4, SwingFrame = 5, BlinkFrame = 6;
        const float IdleFps = 6f, BlinkEvery = 3.6f, BlinkTime = 0.13f;

        enum State { Home, Dash, Swing, Return }

        readonly List<int> found = new List<int>(8);
        State state = State.Home;
        float timer = 0.8f;       // cooldown while home, time left in dash / swing
        int targetIndex, targetId;
        Vector2 targetPos;
        Vector2 aim = Vector2.right;
        int swingsLeft;
        float swingAge = 99f;     // time since the last swing started (drives the effect)
        bool reverseSwing;
        Vector2 swingOrigin, swingAim = Vector2.right; // where the last swing happened (its effect stays there)
        protected bool facingLeft;

        // ---- Numbers: the companion's asset (Assets/Resources/Upgrades/Companions) ----
        protected readonly MeleeCompanionData MData;
        protected MeleeDashCompanion(MeleeCompanionData data) : base(data) => MData = data;

        protected float[] DamageTable => MData.Damage;
        protected float[] CooldownTable => MData.Cooldown;
        protected float[] ArcRadiusTable => MData.ArcRadius;
        protected int[] SwingsTable => MData.Swings;
        protected MeleeTiming Timing(CompanionContext ctx) => new MeleeTiming
        {
            DashSpeed = MData.DashSpeed, ReturnSpeed = MData.ReturnSpeed, MaxDashTime = MData.MaxDashTime,
            StayTime = MData.StayTime, DoubleSwingGap = MData.DoubleSwingGap, SightRange = MData.SightRange,
        };
        protected float Knockback => MData.Knockback;
        protected Color SwingTint => MData.SwingColor;

        // ---- What a subclass provides ----
        protected abstract SpriteAnim Anim(CompanionContext ctx);   // 7 frames: 0-3 idle, 4 dash, 5 swing, 6 blink
        protected virtual float BodyRadius => 0.4f;
        protected virtual float SpriteHeightScale => 2.6f;
        protected virtual Color FallbackColor => new Color(0.54f, 0.38f, 0.28f);
        protected virtual void PlaySwingSound(CompanionContext ctx) => ctx.Audio.Play(Sfx.Slash, 0.7f, 0.12f, 0.05f);

        public override bool StaysHome => state == State.Home;
        public override float SightRadius(CompanionContext ctx) => Timing(ctx).SightRange;

        public override void Tick(float dt, CompanionContext ctx)
        {
            swingAge += dt;
            switch (state)
            {
                case State.Home: TickHome(dt, ctx); break;
                case State.Dash: TickDash(dt, ctx); break;
                case State.Swing: TickSwing(dt, ctx); break;
                case State.Return: TickReturn(dt, ctx); break;
            }
        }

        void TickHome(float dt, CompanionContext ctx)
        {
            timer -= dt;
            if (timer > 0f) return;

            // Nearest enemy to the companion itself.
            var timing = Timing(ctx);
            ctx.Enemies.FindNearest(Position, timing.SightRange, 1, found);
            if (found.Count == 0) return;

            targetIndex = found[0];
            targetId = ctx.Enemies.Id[targetIndex];
            targetPos = ctx.Enemies.Position[targetIndex];
            state = State.Dash;
            timer = timing.MaxDashTime;
            ctx.Effects.Smoke(Position + new Vector2(0f, -BodyRadius), 0.35f, 0.25f); // kick-off dust
        }

        void TickDash(float dt, CompanionContext ctx)
        {
            // Follow the target while it lives (indexes move when enemies die, so check the id).
            if (targetIndex < ctx.Enemies.Count && ctx.Enemies.Id[targetIndex] == targetId && ctx.Enemies.IsAlive(targetIndex))
                targetPos = ctx.Enemies.Position[targetIndex];

            var toTarget = targetPos - Position;
            var distance = toTarget.magnitude;
            if (distance > 0.001f)
            {
                aim = toTarget / distance;
                facingLeft = aim.x < 0f;
            }

            // Stop a bit before the enemy so the arc covers it and the ones around it.
            var stopAt = At(ArcRadiusTable) * 0.45f;
            var step = Timing(ctx).DashSpeed * dt;
            if (distance - stopAt <= step || (timer -= dt) <= 0f)
            {
                if (distance > stopAt) Position += aim * Mathf.Max(0f, distance - stopAt);
                swingsLeft = At(SwingsTable);
                reverseSwing = false;
                StartSwing(ctx);
                return;
            }
            Position += aim * step;
        }

        void StartSwing(CompanionContext ctx)
        {
            state = State.Swing;
            // Between two swings: the double-swing gap. After the last one: it stays put for the stay time
            // (0 = runs back at once, the swing effect finishes on its own where it was swung).
            var timing = Timing(ctx);
            timer = swingsLeft > 1 ? timing.DoubleSwingGap : timing.StayTime;
            swingAge = 0f;
            swingOrigin = Position;
            swingAim = aim;
            swingsLeft--;

            // Hit every enemy inside the 120-degree arc in front.
            var radius = At(ArcRadiusTable);
            var minDot = Mathf.Cos(ArcDegrees * 0.5f * Mathf.Deg2Rad);
            var damage = At(DamageTable) * ctx.Player.Stats.Might;
            ctx.Enemies.QueryRadius(Position, radius, found);
            foreach (var e in found)
            {
                var offset = ctx.Enemies.Position[e] - Position;
                var d = offset.magnitude;
                // Enemies right on top of it always get hit; others must be inside the arc
                // (their body radius widens the angle a little).
                var inside = d < 0.35f + ctx.Enemies.Radius[e] ||
                             Vector2.Dot(offset / d, aim) >= minDot - ctx.Enemies.Radius[e] / Mathf.Max(d, 0.5f);
                if (!inside) continue;
                ctx.Enemies.Damage(e, damage, offset / Mathf.Max(d, 0.001f), Knockback);
                ctx.Effects.Burst(ctx.Enemies.Position[e], 0.3f, new Color(1f, 1f, 1f, 0.9f), 0.15f);
            }
            PlaySwingSound(ctx);
        }

        void TickSwing(float dt, CompanionContext ctx)
        {
            timer -= dt;
            if (timer > 0f) return;
            if (swingsLeft > 0)
            {
                reverseSwing = !reverseSwing;
                StartSwing(ctx);
                return;
            }
            state = State.Return;
        }

        void TickReturn(float dt, CompanionContext ctx)
        {
            var toHome = Home - Position;
            var distance = toHome.magnitude;
            var step = Timing(ctx).ReturnSpeed * dt;
            if (distance <= step)
            {
                Position = Home;
                state = State.Home;
                timer = At(CooldownTable);
                return;
            }
            Position += toHome / distance * step;
            facingLeft = toHome.x < 0f;
        }

        public override void Draw(InstancedRenderer renderer, CompanionContext ctx)
        {
            var r = BodyRadius * Data.BodyScale;
            var feet = Position.y - r;
            renderer.Ellipse(RenderLayer.Shadows, new Vector2(Position.x, feet + 0.05f), new Vector2(r * 1.8f, r * 0.55f),
                new Color(0f, 0f, 0f, 0.22f));

            // Speed streak behind it while dashing.
            if (state == State.Dash)
            {
                var angle = Mathf.Atan2(aim.y, aim.x);
                renderer.Streak(RenderLayer.Effects, Position - aim * 0.6f, 1.2f, 0.35f, angle, new Color(1f, 1f, 1f, 0.35f));
            }

            var anim = Anim(ctx);
            if (anim != null && anim.Frames.Length >= 7 && anim.Frames[0] != null)
            {
                renderer.Sprite(RenderLayer.World, PickFrame(anim), new Vector2(Position.x, Position.y + r * 0.4f),
                    r * SpriteHeightScale, Color.white, flipX: facingLeft, sortY: feet);
            }
            else
            {
                renderer.Circle(RenderLayer.World, Position, r, FallbackColor, 0f, feet);
            }

            DrawSwing(renderer, ctx);
        }

        // A subclass can force a pose (e.g. the pig raising its club while casting). null = normal animation.
        protected virtual Sprite OverrideFrame(SpriteAnim anim) => null;

        // For a subclass that pauses the attack logic (skips base.Tick): keeps the last swing effect playing out.
        protected void AdvanceSwingEffect(float dt) => swingAge += dt;

        protected const int RaiseFrame = DashFrame; // weapon held up

        Sprite PickFrame(SpriteAnim anim)
        {
            var forced = OverrideFrame(anim);
            if (forced != null) return forced;
            var frames = anim.Frames;
            if (state == State.Dash) return frames[DashFrame];
            if (state == State.Swing && swingAge < SwingTime) return frames[SwingFrame]; // then idle while it waits
            var t = Time.time + Slot * 1.37f;
            if (state == State.Home && t % BlinkEvery < BlinkTime) return frames[BlinkFrame];
            return frames[(int)((Time.time + Slot * 0.3f) * IdleFps) % IdleFrames];
        }

        // The arc: swing start -> full arc -> fading, pointing at the enemy. A reversed swing is mirrored.
        void DrawSwing(InstancedRenderer renderer, CompanionContext ctx)
        {
            if (swingAge >= SwingTime) return;
            var progress = swingAge / SwingTime;
            var radius = At(ArcRadiusTable);
            var angle = Mathf.Atan2(swingAim.y, swingAim.x);
            var alpha = progress < 0.5f ? 1f : 1f - (progress - 0.5f) * 2f;
            var c = SwingTint;
            var tint = new Color(c.r, c.g, c.b, alpha);

            var anim = ctx.Sprites.Slash;
            if (anim != null && anim.Frames.Length >= 3 && anim.Frames[0] != null)
            {
                var frame = anim.Frames[progress < 0.2f ? 0 : progress < 0.55f ? 1 : 2];
                // Sprite arc's outer edge is at 120/128 of its half-size.
                var height = radius * 2f * 128f / 120f * (0.9f + 0.1f * progress);
                renderer.SpriteRotated(RenderLayer.Effects, frame, swingOrigin, height, angle, tint, flipY: reverseSwing);
            }
            else
            {
                renderer.Ring(RenderLayer.Effects, swingOrigin, radius * (0.8f + 0.2f * progress), 0.12f, tint);
            }
        }

        // Debug (F3 "Hiện vùng chém"): the exact hit area used in StartSwing.
        //   - faint yellow fan: where a swing would land right now (current position + aim)
        //   - bright red fan: the last real swing, held for a moment so you can see it
        //   - small circle: enemies touching this always get hit, even behind it
        // The real test is a little wider for big enemies (their body radius widens the angle).
        const float DebugHoldTime = 0.6f;
        public override void DrawAttackArea(InstancedRenderer renderer, CompanionContext ctx)
        {
            DrawFan(renderer, Position, aim, new Color(1f, 0.9f, 0.2f, 0.45f));
            if (swingAge < DebugHoldTime)
            {
                var a = 1f - swingAge / DebugHoldTime;
                DrawFan(renderer, swingOrigin, swingAim, new Color(1f, 0.15f, 0.1f, 0.4f + 0.6f * a));
            }
        }

        void DrawFan(InstancedRenderer renderer, Vector2 origin, Vector2 dir, Color color)
        {
            const int Segments = 18;
            const float Width = 0.05f;
            var radius = At(ArcRadiusTable);
            var half = ArcDegrees * 0.5f * Mathf.Deg2Rad;
            var baseAngle = Mathf.Atan2(dir.y, dir.x);
            Vector2 Point(float angle) => origin + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;

            var start = Point(baseAngle - half);
            var end = Point(baseAngle + half);
            renderer.Line(RenderLayer.Overlay, origin, start, Width, color);
            renderer.Line(RenderLayer.Overlay, origin, end, Width, color);
            var prev = start;
            for (var i = 1; i <= Segments; i++)
            {
                var p = Point(baseAngle - half + 2f * half * i / Segments);
                renderer.Line(RenderLayer.Overlay, prev, p, Width, color);
                prev = p;
            }
            renderer.Line(RenderLayer.Overlay, origin, origin + dir * radius, Width * 0.6f, color); // center line
            renderer.Ring(RenderLayer.Overlay, origin, 0.35f, 0.15f, color);                         // always-hit core
        }
    }
}
