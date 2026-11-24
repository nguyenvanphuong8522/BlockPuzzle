using System.Collections.Generic;
using UnityEngine;

namespace Survivor
{
    // Companion #2: an otter with a sword. It dashes to the nearest enemy, slashes everything in a 120-degree arc
    // facing that enemy, then runs back to its place next to the hero.
    public sealed class SwordOtterCompanion : Companion
    {
        static readonly float[] Damage = { 22, 28, 36, 46, 60 };
        static readonly float[] Cooldown = { 2.2f, 2.0f, 1.8f, 1.5f, 1.3f };
        static readonly float[] SlashRadius = { 1.7f, 1.8f, 2.2f, 2.3f, 2.5f };
        static readonly int[] Slashes = { 1, 1, 1, 1, 2 };  // level 5 swings back the other way

        const float ArcDegrees = 120f;
        const float SlashTime = 0.22f;      // one swing: effect + slash pose
        const float SecondSlashDelay = 0.12f;
        const float Knockback = 4f;

        const float Radius = 0.4f;          // same size as the monkey
        const int IdleFrames = 4, DashFrame = 4, SlashFrame = 5, BlinkFrame = 6;
        const float IdleFps = 6f, BlinkEvery = 3.6f, BlinkTime = 0.13f;

        enum State { Home, Dash, Slash, Return }

        readonly List<int> found = new List<int>(8);
        State state = State.Home;
        float timer = 0.8f;       // cooldown while home, time left in dash / slash
        int targetIndex, targetId;
        Vector2 targetPos;
        Vector2 aim = Vector2.right;
        int slashesLeft;
        float slashAge = 99f;     // time since the last swing started (drives the effect)
        bool reverseSwing;
        Vector2 slashOrigin, slashAim = Vector2.right; // where the last swing happened (its effect stays there)
        bool facingLeft;

        public override string Name => "Rái Cá Kiếm Sĩ";
        public override string Description => "Lao tới kẻ địch gần nhất và chém một vùng hình quạt 120° phía trước.";
        public override Color Tint => new Color(0.55f, 0.85f, 1f);
        public override string NextLevelDescription => (Level + 1) switch
        {
            2 => "Sát thương +25%, chém nhanh hơn.",
            3 => "Vùng chém rộng hơn.",
            4 => "Sát thương cao hơn, hồi chiêu ngắn hơn.",
            _ => "Chém 2 nhát liên tiếp (qua lại)."
        };

        public override bool StaysHome => state == State.Home;
        public override float SightRadius(CompanionContext ctx) => ctx.Tuning.OtterSightRange;

        public override void Tick(float dt, CompanionContext ctx)
        {
            slashAge += dt;
            switch (state)
            {
                case State.Home: TickHome(dt, ctx); break;
                case State.Dash: TickDash(dt, ctx); break;
                case State.Slash: TickSlash(dt, ctx); break;
                case State.Return: TickReturn(dt, ctx); break;
            }
        }

        void TickHome(float dt, CompanionContext ctx)
        {
            timer -= dt;
            if (timer > 0f) return;

            // Nearest enemy to the otter itself.
            ctx.Enemies.FindNearest(Position, ctx.Tuning.OtterSightRange, 1, found);
            if (found.Count == 0) return;

            targetIndex = found[0];
            targetId = ctx.Enemies.Id[targetIndex];
            targetPos = ctx.Enemies.Position[targetIndex];
            state = State.Dash;
            timer = ctx.Tuning.OtterMaxDashTime;
            ctx.Effects.Smoke(Position + new Vector2(0f, -Radius), 0.35f, 0.25f); // kick-off dust
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
            var stopAt = At(SlashRadius) * 0.45f;
            var step = ctx.Tuning.OtterDashSpeed * dt;
            if (distance - stopAt <= step || (timer -= dt) <= 0f)
            {
                if (distance > stopAt) Position += aim * Mathf.Max(0f, distance - stopAt);
                slashesLeft = At(Slashes);
                reverseSwing = false;
                StartSwing(ctx);
                return;
            }
            Position += aim * step;
        }

        void StartSwing(CompanionContext ctx)
        {
            state = State.Slash;
            // Between two swings: a short gap. After the last one: it stays put for Otter Slash Stay Time
            // (PlayerTuning; 0 = runs back at once, the slash effect finishes on its own where it was swung).
            timer = slashesLeft > 1 ? SecondSlashDelay + SlashTime * 0.5f : ctx.Tuning.OtterSlashStayTime;
            slashAge = 0f;
            slashOrigin = Position;
            slashAim = aim;
            slashesLeft--;

            // Hit every enemy inside the 120-degree arc in front.
            var radius = At(SlashRadius);
            var minDot = Mathf.Cos(ArcDegrees * 0.5f * Mathf.Deg2Rad);
            var damage = At(Damage) * ctx.Player.Stats.Might;
            ctx.Enemies.QueryRadius(Position, radius, found);
            foreach (var e in found)
            {
                var offset = ctx.Enemies.Position[e] - Position;
                var d = offset.magnitude;
                // Enemies right on top of the otter always get hit; others must be inside the arc
                // (their body radius widens the angle a little).
                var inside = d < 0.35f + ctx.Enemies.Radius[e] ||
                             Vector2.Dot(offset / d, aim) >= minDot - ctx.Enemies.Radius[e] / Mathf.Max(d, 0.5f);
                if (!inside) continue;
                ctx.Enemies.Damage(e, damage, offset / Mathf.Max(d, 0.001f), Knockback);
                ctx.Effects.Burst(ctx.Enemies.Position[e], 0.3f, new Color(1f, 1f, 1f, 0.9f), 0.15f);
            }
            ctx.Audio.Play(Sfx.Slash, 0.7f, 0.12f, 0.05f);
        }

        void TickSlash(float dt, CompanionContext ctx)
        {
            timer -= dt;
            if (timer > 0f) return;
            if (slashesLeft > 0)
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
            var step = ctx.Tuning.OtterReturnSpeed * dt;
            if (distance <= step)
            {
                Position = Home;
                state = State.Home;
                timer = At(Cooldown);
                return;
            }
            Position += toHome / distance * step;
            facingLeft = toHome.x < 0f;
        }

        public override void Draw(InstancedRenderer renderer, CompanionContext ctx)
        {
            var feet = Position.y - Radius;
            renderer.Ellipse(RenderLayer.Shadows, new Vector2(Position.x, feet + 0.05f), new Vector2(Radius * 1.8f, Radius * 0.55f),
                new Color(0f, 0f, 0f, 0.22f));

            // Speed streak behind it while dashing.
            if (state == State.Dash)
            {
                var angle = Mathf.Atan2(aim.y, aim.x);
                renderer.Streak(RenderLayer.Effects, Position - aim * 0.6f, 1.2f, 0.35f, angle, new Color(1f, 1f, 1f, 0.35f));
            }

            var anim = ctx.Sprites.Otter;
            if (anim != null && anim.Frames.Length >= 7 && anim.Frames[0] != null)
            {
                renderer.Sprite(RenderLayer.World, PickFrame(anim), new Vector2(Position.x, Position.y + Radius * 0.4f),
                    Radius * 2.6f, Color.white, flipX: facingLeft, sortY: feet);
            }
            else
            {
                renderer.Circle(RenderLayer.World, Position, Radius, new Color(0.54f, 0.38f, 0.28f), 0f, feet);
            }

            DrawSlash(renderer, ctx);
        }

        Sprite PickFrame(SpriteAnim anim)
        {
            var frames = anim.Frames;
            if (state == State.Dash) return frames[DashFrame];
            if (state == State.Slash && slashAge < SlashTime) return frames[SlashFrame]; // then idle while it waits
            var t = Time.time + Slot * 1.37f;
            if (state == State.Home && t % BlinkEvery < BlinkTime) return frames[BlinkFrame];
            return frames[(int)((Time.time + Slot * 0.3f) * IdleFps) % IdleFrames];
        }

        // The arc: swing start -> full arc -> fading, pointing at the enemy. A reversed swing is mirrored.
        void DrawSlash(InstancedRenderer renderer, CompanionContext ctx)
        {
            if (slashAge >= SlashTime) return;
            var progress = slashAge / SlashTime;
            var radius = At(SlashRadius);
            var angle = Mathf.Atan2(slashAim.y, slashAim.x);
            var alpha = progress < 0.5f ? 1f : 1f - (progress - 0.5f) * 2f;
            var tint = new Color(Tint.r, Tint.g, Tint.b, alpha);

            var anim = ctx.Sprites.Slash;
            if (anim != null && anim.Frames.Length >= 3 && anim.Frames[0] != null)
            {
                var frame = anim.Frames[progress < 0.2f ? 0 : progress < 0.55f ? 1 : 2];
                // Sprite arc's outer edge is at 120/128 of its half-size.
                var height = radius * 2f * 128f / 120f * (0.9f + 0.1f * progress);
                renderer.SpriteRotated(RenderLayer.Effects, frame, slashOrigin, height, angle, tint, flipY: reverseSwing);
            }
            else
            {
                renderer.Ring(RenderLayer.Effects, slashOrigin, radius * (0.8f + 0.2f * progress), 0.12f, tint);
            }
        }

        // Debug (F3 "Hiện vùng chém rái cá"): the exact hit area used in StartSwing.
        //   - faint yellow fan: where a swing would land right now (current position + aim)
        //   - bright red fan: the last real swing, held for a moment so you can see it
        //   - small circle: enemies touching this always get hit, even behind the otter
        // The real test is a little wider for big enemies (their body radius widens the angle).
        const float DebugHoldTime = 0.6f;
        public override void DrawAttackArea(InstancedRenderer renderer, CompanionContext ctx)
        {
            DrawFan(renderer, Position, aim, new Color(1f, 0.9f, 0.2f, 0.45f));
            if (slashAge < DebugHoldTime)
            {
                var a = 1f - slashAge / DebugHoldTime;
                DrawFan(renderer, slashOrigin, slashAim, new Color(1f, 0.15f, 0.1f, 0.4f + 0.6f * a));
            }
        }

        void DrawFan(InstancedRenderer renderer, Vector2 origin, Vector2 dir, Color color)
        {
            const int Segments = 18;
            const float Width = 0.05f;
            var radius = At(SlashRadius);
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
