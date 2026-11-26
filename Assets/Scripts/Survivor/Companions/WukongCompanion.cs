using System.Collections.Generic;
using UnityEngine;

namespace Survivor
{
    // Art: companion_wukong_0..8 (0-3 idle with the staff, 4 throw, 5 blink, 6 waiting for the staff,
    // 7 hair tuft at the lips, 8 blowing it away).
    // Fusion companion (banana monkey + sword otter at max level).
    // Skill 1: throws a big spinning staff at the nearest enemy; it flies out, turns around and comes back to his
    //          hand (BoomerangSystem), hitting everything on the way out and on the way back.
    // Skill 2: every Clone Interval seconds he waits for the staff, then summons (pluck a hair, blow it away: no
    //          throwing until all are out). One by one, at an uneven rhythm ("poof... poof-poof"), clones that look just like
    //          him jump out of a puff of smoke at his body and land all around him; they hunt enemies on their own (wander when none is in sight) and spin their staff.
    //          Every attack costs the clone health; at 0 it vanishes in a puff of smoke.
    // Every number and the card text: Assets/Resources/Upgrades/Companions/Wukong.asset.
    public sealed class WukongCompanion : Companion
    {
        const float ThrowPoseTime = 0.25f;
        const float Radius = 0.4f;
        const int IdleFrames = 4, ThrowFrame = 4, BlinkFrame = 5, WaitFrame = 6, PluckFrame = 7, BlowFrame = 8;
        const float IdleFps = 6f, WalkFps = 10f, BlinkEvery = 3.4f, BlinkTime = 0.13f;
        const float CloneDieTime = 0.25f;
        const float CloneSpacing = 0.7f;   // clones push each other apart closer than this

        sealed class Clone
        {
            public Vector2 Pos, From, Land, WanderTarget;
            public float WanderTimer;
            public float Fly;          // 0..1 while jumping out of his body to the landing spot, 1 = landed
            public float Appear;       // 0..1 landing bounce after the jump, 1 = ready
            public float Hp, Age, AttackTimer, Spin, SpinStart, Dying;
            public bool FacingLeft, Walking, DieAfterSpin;
        }

        readonly WukongData data;
        readonly List<int> targets = new List<int>(1);
        float summonAngle;   // direction of the last clone's jump (degrees); the next one turns ~137 from it
        readonly List<int> hits = new List<int>(64);
        readonly List<Clone> clones = new List<Clone>();
        float timer = 0.5f;
        float throwPose;
        bool facingLeft;
        bool staffOut;
        float cloneTimer;
        float cloneTimerFull;  // what cloneTimer started from (for the countdown bar)
        bool casting;
        float castTime;
        int toSummon, summoned;   // clones of the current summon: how many in all / already out
        float summonTimer;

        public WukongCompanion(WukongData data) : base(data)
        {
            this.data = data;
            cloneTimer = cloneTimerFull = data.CloneFirstDelay;
        }

        public override float SightRadius(CompanionContext ctx) => data.SightRange;

        int AliveClones
        {
            get
            {
                var n = 0;
                foreach (var c in clones)
                    if (c.Dying <= 0f) n++;
                return n;
            }
        }

        public override void Tick(float dt, CompanionContext ctx)
        {
            throwPose -= dt;
            staffOut = ctx.Boomerangs.CountOwnedBy(this) > 0;
            TickClones(dt, ctx);

            // ---- Summon skill ----
            if (casting)
            {
                castTime += dt;
                if (castTime >= data.CloneCastTime)
                {
                    // Wind-up done: blow the clones out one by one.
                    summonTimer -= dt;
                    while (summonTimer <= 0f && summoned < toSummon)
                    {
                        SummonOne(ctx);
                        summoned++;
                        // Uneven rhythm: "poof... poof-poof... poof".
                        summonTimer += data.CloneSummonGap * Random.Range(1f - data.CloneGapJitter, 1f + data.CloneGapJitter);
                    }
                    if (summoned >= toSummon)
                    {
                        casting = false;
                        cloneTimer = cloneTimerFull = data.CloneInterval;
                    }
                }
                return; // no throwing until every clone of this summon is out
            }
            if (data.CloneSkill)
            {
                cloneTimer -= dt;
                if (cloneTimer <= 0f && AliveClones < data.CloneMaxAlive)
                {
                    if (staffOut) return; // ready: wait for the staff to come back, don't throw again
                    casting = true;
                    castTime = 0f;
                    toSummon = Mathf.Min(data.CloneCount, data.CloneMaxAlive - AliveClones);
                    summoned = 0;
                    summonTimer = 0f;
                    summonAngle = Random.Range(0f, 360f);
                    ctx.Effects.Ring(Position, 0.8f, Tint, 0.3f);
                    ctx.Audio.Play(Sfx.Cast, 0.6f, 0.05f, 0.05f);
                    return;
                }
            }

            // ---- Staff throw ----
            if (data.WaitForReturn && staffOut) return; // the cooldown starts once the staff is back in hand
            timer -= dt;
            if (timer > 0f) return;

            ctx.Enemies.FindNearest(Position, data.SightRange, 1, targets);
            if (targets.Count == 0) return;

            timer = At(data.Cooldown) * ctx.Player.Stats.Haste;
            var aim = (ctx.Enemies.Position[targets[0]] - Position).normalized;
            facingLeft = aim.x < 0f;
            throwPose = ThrowPoseTime;

            var length = At(data.StaffLength);
            var style = new BoomerangStyle
            {
                Sprite = ctx.Sprites.Staff, Size = length, HitRadius = length * 0.5f * data.HitRadiusScale,
                SpinSpeed = data.SpinSpeed, Knockback = data.Knockback, ReturnSpeed = data.ReturnSpeed,
                Ghosts = data.SpinGhosts, FallbackColor = new Color(0.85f, 0.2f, 0.15f)
            };
            var n = At(data.Staffs);
            var damage = At(data.Damage) * ctx.Player.Stats.Might;
            for (var k = 0; k < n; k++)
            {
                var offset = (k - (n - 1) / 2f) * data.SpreadDegrees * Mathf.Deg2Rad;
                float c = Mathf.Cos(offset), s = Mathf.Sin(offset);
                var dir = new Vector2(aim.x * c - aim.y * s, aim.x * s + aim.y * c);
                ctx.Boomerangs.Throw(this, dir, At(data.Range), data.ThrowSpeed, damage, style);
            }
            staffOut = true;
            ctx.Audio.PlayAtPitch(Sfx.Throw, 0.75f, 0.75f, 0.05f);
        }

        // ---------------- Clones ----------------

        // One clone of the summon: "poof", a puff of smoke at his body and the clone jumps out of it in an arc.
        // Each one turns ~137 degrees (+ a random bit) from the previous one and lands at a random distance,
        // so they spread all around him without forming an even circle.
        void SummonOne(CompanionContext ctx)
        {
            summonAngle += 137.5f + Random.Range(-data.CloneAngleJitter, data.CloneAngleJitter);
            var a = summonAngle * Mathf.Deg2Rad;
            var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
            var near = Mathf.Min(data.CloneLandDistance.x, data.CloneLandDistance.y);
            var far = Mathf.Max(data.CloneLandDistance.x, data.CloneLandDistance.y);
            var land = Position + dir * Random.Range(near, far);
            facingLeft = dir.x < 0f;
            clones.Add(new Clone
            {
                Pos = Position, From = Position, Land = land, WanderTarget = land,
                Hp = data.CloneHp, AttackTimer = Random.Range(0f, 0.2f), FacingLeft = dir.x < 0f
            });
            if (data.CloneLaunchSmokeScale > 0f) Poof(ctx, Position, data.CloneLaunchSmokeScale);
            ctx.Audio.PlayAtPitch(Sfx.Boing, data.ClonePoofPitch + 0.05f * summoned, 0.55f, 0.03f);
        }

        // "Poof!": a puff of smoke (one big cloud + small puffs around it), `scale` × Clone Smoke Size.
        void Poof(CompanionContext ctx, Vector2 at, float scale)
        {
            var size = data.CloneSmokeSize * scale;
            var time = data.CloneSmokeTime;
            ctx.Effects.Smoke(at + new Vector2(0f, size * 0.25f), size, time, data.CloneSmokeColor);
            for (var k = 0; k < data.CloneSmokePuffs; k++)
            {
                var a = (k * 360f / Mathf.Max(1, data.CloneSmokePuffs) + Random.Range(-20f, 20f)) * Mathf.Deg2Rad;
                var off = new Vector2(Mathf.Cos(a), Mathf.Sin(a) * 0.6f) * size * 0.6f;
                ctx.Effects.Smoke(at + off + new Vector2(0f, size * 0.2f), size * Random.Range(0.45f, 0.65f), time * 0.8f,
                    data.CloneSmokeColor);
            }
            ctx.Effects.Ring(at, size * 0.9f, Color.white, time * 0.6f);
        }

        void TickClones(float dt, CompanionContext ctx)
        {
            for (var i = clones.Count - 1; i >= 0; i--)
            {
                var c = clones[i];

                if (c.Dying > 0f)
                {
                    c.Dying += dt;
                    if (c.Dying >= CloneDieTime) clones.RemoveAt(i);
                    continue;
                }

                // Jumping out of his body (arc drawn in DrawClone), not acting yet.
                if (c.Fly < 1f)
                {
                    c.Fly = Mathf.Min(1f, c.Fly + dt / Mathf.Max(0.05f, data.CloneFlyTime));
                    var e = 1f - (1f - c.Fly) * (1f - c.Fly);
                    c.Pos = Vector2.Lerp(c.From, c.Land, e);
                    if (c.Fly >= 1f && data.CloneLandSmokeScale > 0f) Poof(ctx, c.Pos, data.CloneLandSmokeScale);
                    continue;
                }
                // Landing bounce, not acting yet.
                if (c.Appear < 1f)
                {
                    c.Appear = Mathf.Min(1f, c.Appear + dt / Mathf.Max(0.01f, data.CloneAppearTime));
                    continue;
                }

                c.Age += dt;
                c.AttackTimer -= dt;
                c.Walking = false;
                if (c.Spin > 0f)
                {
                    c.Spin -= dt;
                    if (c.Spin <= 0f && c.DieAfterSpin) Kill(c, ctx);
                    continue; // stands still while spinning the staff
                }
                if (data.CloneLifetime > 0f && c.Age >= data.CloneLifetime)
                {
                    Kill(c, ctx);
                    continue;
                }

                ctx.Enemies.FindNearest(c.Pos, data.CloneSightRange, 1, targets);
                if (targets.Count > 0)
                {
                    var to = ctx.Enemies.Position[targets[0]] - c.Pos;
                    var dist = to.magnitude;
                    if (Mathf.Abs(to.x) > 0.05f) c.FacingLeft = to.x < 0f;
                    if (dist <= data.CloneAttackRange)
                    {
                        if (c.AttackTimer <= 0f) Attack(c, ctx);
                    }
                    else Walk(c, to / dist, dist - data.CloneAttackRange * 0.7f, dt);
                }
                else
                {
                    // Nothing in sight: wander, picking a new spot now and then.
                    c.WanderTimer -= dt;
                    if (c.WanderTimer <= 0f || (c.WanderTarget - c.Pos).sqrMagnitude < 0.04f)
                    {
                        c.WanderTarget = c.Pos + Random.insideUnitCircle * data.CloneWanderRadius;
                        c.WanderTimer = data.CloneWanderInterval * Random.Range(0.7f, 1.3f);
                    }
                    var to = c.WanderTarget - c.Pos;
                    var dist = to.magnitude;
                    if (dist > 0.05f)
                    {
                        if (Mathf.Abs(to.x) > 0.05f) c.FacingLeft = to.x < 0f;
                        Walk(c, to / dist, dist, dt, data.CloneWanderSpeedScale);
                    }
                }
            }

            // Keep them from standing on top of each other.
            for (var i = 0; i < clones.Count; i++)
            for (var j = i + 1; j < clones.Count; j++)
            {
                var a = clones[i];
                var b = clones[j];
                if (a.Appear < 1f || b.Appear < 1f || a.Dying > 0f || b.Dying > 0f) continue;
                var d = b.Pos - a.Pos;
                var m = d.magnitude;
                if (m >= CloneSpacing) continue;
                var push = (m < 0.001f ? Random.insideUnitCircle.normalized : d / m) * (CloneSpacing - m) * 0.5f;
                a.Pos -= push;
                b.Pos += push;
            }
        }

        void Walk(Clone c, Vector2 dir, float maxStep, float dt, float speedScale = 1f)
        {
            c.Pos += dir * Mathf.Min(data.CloneMoveSpeed * speedScale * dt, Mathf.Max(0f, maxStep));
            c.Walking = true;
        }

        // Spin the staff once around itself: hits everything in Clone Attack Radius and costs health.
        void Attack(Clone c, CompanionContext ctx)
        {
            c.AttackTimer = data.CloneAttackInterval;
            c.Spin = data.CloneSpinTime;
            c.SpinStart = Random.Range(0f, 360f);
            var damage = At(data.CloneDamage) * ctx.Player.Stats.Might;
            ctx.Enemies.QueryRadius(c.Pos, data.CloneAttackRadius, hits);
            foreach (var e in hits)
            {
                var push = ctx.Enemies.Position[e] - c.Pos;
                ctx.Enemies.Damage(e, damage, push.sqrMagnitude > 0.0001f ? push.normalized : Vector2.up, data.CloneKnockback);
            }
            ctx.Audio.PlayAtPitch(Sfx.Slash, 1.25f, 0.35f, 0.06f);
            c.Hp -= data.CloneAttackHpCost;
            if (c.Hp <= 0f) c.DieAfterSpin = true; // finish this spin, then vanish
        }

        void Kill(Clone c, CompanionContext ctx)
        {
            c.Dying = 0.0001f;
            ctx.Effects.Smoke(c.Pos, 0.7f, 0.4f);
            ctx.Effects.Ring(c.Pos, 0.5f, new Color(1f, 0.9f, 0.6f), 0.2f);
        }

        // ---------------- Drawing ----------------

        // Debug "Hiện vùng chém": throw range, hit circle of each flying staff, attack circle of each clone.
        public override void DrawAttackArea(InstancedRenderer renderer, CompanionContext ctx)
        {
            renderer.Ring(RenderLayer.Overlay, Position, At(data.Range), 0.04f, new Color(1f, 0.8f, 0.3f, 0.5f));
            ctx.Boomerangs.DrawHitAreas(renderer, this, new Color(1f, 0.35f, 0.25f, 0.8f));
            foreach (var c in clones)
                if (c.Dying <= 0f && c.Appear >= 1f)
                    renderer.Ring(RenderLayer.Overlay, c.Pos, data.CloneAttackRadius, 0.03f, new Color(1f, 0.6f, 0.2f, 0.7f));
        }

        public override void Draw(InstancedRenderer renderer, CompanionContext ctx)
        {
            var r = Radius * data.BodyScale;
            var feet = Position.y - r;
            renderer.Ellipse(RenderLayer.Shadows, new Vector2(Position.x, feet + 0.05f), new Vector2(r * 1.8f, r * 0.55f),
                new Color(0f, 0f, 0f, 0.22f));

            var anim = ctx.Sprites.Wukong;
            if (anim != null && !anim.IsEmpty)
            {
                var frame = PickFrame(anim);
                var center = new Vector2(Position.x, Position.y + r * 0.4f);
                renderer.Sprite(RenderLayer.World, frame, center, r * 2.6f, Color.white, flipX: facingLeft, sortY: feet);
                DrawPlumes(renderer, ctx, anim, frame, center, r * 2.6f, facingLeft, 1f, feet, Slot * 1.3f,
                    throwPose > 0f ? throwPose / ThrowPoseTime : 0f);
            }
            else
                renderer.Circle(RenderLayer.World, Position, r, new Color(0.8f, 0.55f, 0.25f), 0f, feet);

            if (casting) DrawCastGlow(renderer, r);
            DrawSummonBar(renderer, r);
            foreach (var c in clones) DrawClone(renderer, ctx, anim, c);
        }

        // Head bob of each art frame in pixels of the 128-px image (0-3 breathing, 4 throw leans up).
        static readonly float[] FrameBob = { 0f, 1.5f, 3f, 1.5f, -3f };

        // Two pheasant plumes growing from the crown, swaying. Each turns around its base; the base follows the
        // head (frame bob) and the facing. `swing` (0..1) adds an extra kick (throw / staff spin).
        void DrawPlumes(InstancedRenderer renderer, CompanionContext ctx, SpriteAnim anim, Sprite frame, Vector2 spriteCenter,
            float height, bool flip, float alpha, float feet, float phase, float swing)
        {
            var plume = ctx.Sprites.Plume;
            if (!data.ShowPlumes || plume == null) return;
            var index = System.Array.IndexOf(anim.Frames, frame);
            var bob = index >= 0 && index < FrameBob.Length ? FrameBob[index] : 0f;
            var baseY = spriteCenter.y + (data.PlumeBase.y - bob / 128f) * height;
            var rect = plume.rect;
            var length = data.PlumeLength * height / (Radius * 2.6f * data.BodyScale); // grows/shrinks with the body
            var quadHeight = length * rect.height / rect.width * data.PlumeThickness;
            var tint = data.PlumeTint;
            tint.a *= alpha;
            var t = Time.time * data.PlumeSwaySpeed + phase;
            for (var p = 0; p < 2; p++)
            {
                var front = p == 0;
                var side = (front ? 1f : -1f) * data.PlumeBase.x * height;
                var basePos = new Vector2(spriteCenter.x + (flip ? -side : side), baseY);
                // Angle facing right; the swing pushes both plumes back (toward bigger angles).
                var deg = (front ? data.FrontPlumeAngle : data.BackPlumeAngle)
                          + Mathf.Sin(t + (front ? 0f : 0.9f)) * data.PlumeSwayDegrees
                          + swing * data.PlumeThrowSwing;
                if (flip) deg = 180f - deg;
                var rad = deg * Mathf.Deg2Rad;
                var dir = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
                // Behind the head: drawn as if a bit further back than his feet.
                renderer.SpriteRotated(RenderLayer.World, plume, basePos + dir * (length * 0.5f), quadHeight, rad, tint,
                    flipY: flip, sortY: feet + 0.002f + p * 0.001f);
            }
        }

        // Golden rings closing in while he summons.
        void DrawCastGlow(InstancedRenderer renderer, float r)
        {
            var k = Mathf.Clamp01(castTime / Mathf.Max(0.05f, data.CloneCastTime));
            var center = Position + new Vector2(0f, r * 0.3f);
            var pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 22f);
            renderer.Ring(RenderLayer.Effects, center, r * (1.5f + 0.4f * k), 0.1f, new Color(1f, 0.85f, 0.3f, 0.45f + 0.4f * pulse));
            renderer.Ring(RenderLayer.Effects, center, r * (3.2f - 1.8f * k), 0.07f, new Color(1f, 1f, 1f, 0.7f * k));
        }

        // Faded copies of a flying clone a little behind it on its arc (no white flash: on a see-through sprite
        // that would show its whole square).
        void DrawFlyTrail(InstancedRenderer renderer, SpriteAnim anim, Clone c, float r)
        {
            if (anim == null || anim.IsEmpty || anim.Frames.Length <= ThrowFrame) return;
            for (var g = data.CloneFlyTrail; g >= 1; g--)
            {
                var t = Mathf.Max(0f, c.Fly - g * 0.1f);
                var e = 1f - (1f - t) * (1f - t);
                var pos = Vector2.Lerp(c.From, c.Land, e);
                var scale = Mathf.Lerp(data.CloneFlyStartScale, 1f, e);
                var lift = Mathf.Sin(t * Mathf.PI) * data.CloneFlyHeight;
                var col = data.CloneTint;
                col.a *= 0.35f / g;
                renderer.Sprite(RenderLayer.Effects, anim.Frames[ThrowFrame], new Vector2(pos.x, pos.y + r * 0.4f * scale + lift),
                    r * 2.6f * scale, col, flipX: c.FacingLeft);
            }
        }

        void DrawClone(InstancedRenderer renderer, CompanionContext ctx, SpriteAnim anim, Clone c)
        {
            var r = Radius * data.BodyScale * data.CloneScale;
            var scale = 1f;
            var alpha = 1f;
            var flash = 0f;
            var lift = 0f;
            if (c.Fly < 1f)
            {
                // Jumping out of his body: grows from small, flying in an arc, with faded copies trailing behind.
                var t = c.Fly;
                lift = Mathf.Sin(t * Mathf.PI) * data.CloneFlyHeight;
                scale = Mathf.Lerp(data.CloneFlyStartScale, 1f, 1f - (1f - t) * (1f - t));
                DrawFlyTrail(renderer, anim, c, r);
            }
            else if (c.Appear < 1f)
            {
                // Landing bounce: squashes a little, then pops back up.
                scale = 1f + 0.16f * Mathf.Sin(c.Appear * Mathf.PI) * (c.Appear < 0.5f ? -1f : 1f);
            }
            else if (c.Dying > 0f)
            {
                var t = Mathf.Clamp01(c.Dying / CloneDieTime);
                scale = 1f + 0.3f * t;
                alpha = 1f - t;
            }

            var feet = c.Pos.y - r;
            if (c.Dying <= 0f)
                renderer.Ellipse(RenderLayer.Shadows, new Vector2(c.Pos.x, feet + 0.05f), new Vector2(r * 1.8f, r * 0.55f) * Mathf.Min(1f, scale),
                    new Color(0f, 0f, 0f, 0.2f));

            var tint = data.CloneTint;
            tint.a *= alpha;
            var center = new Vector2(c.Pos.x, c.Pos.y + r * 0.4f * scale + lift);
            if (anim != null && !anim.IsEmpty)
            {
                var frame = CloneFrame(anim, c);
                renderer.Sprite(RenderLayer.World, frame, center, r * 2.6f * scale, tint, flipX: c.FacingLeft,
                    flash: flash, sortY: feet);
                DrawPlumes(renderer, ctx, anim, frame, center, r * 2.6f * scale, c.FacingLeft, alpha, feet, c.Land.x * 0.7f,
                    c.Spin > 0f ? 1f : 0f);
            }
            else
                renderer.Circle(RenderLayer.World, c.Pos, r * scale, new Color(0.8f, 0.55f, 0.25f, alpha), 0f, feet);

            // Spinning staff around it while attacking.
            if (c.Spin > 0f && ctx.Sprites.Staff != null)
            {
                var staff = ctx.Sprites.Staff;
                var k = 1f - c.Spin / Mathf.Max(0.05f, data.CloneSpinTime);
                var angle = c.SpinStart + k * 360f * (c.FacingLeft ? 1f : -1f);
                var rect = staff.rect;
                var h = data.CloneStaffLength * rect.height / Mathf.Max(rect.width, rect.height);
                var at = c.Pos + new Vector2(0f, r * 0.3f);
                for (var g = 2; g >= 1; g--)
                    renderer.SpriteRotated(RenderLayer.Projectiles, staff, at, h,
                        (angle - g * 25f * (c.FacingLeft ? 1f : -1f)) * Mathf.Deg2Rad, new Color(1f, 1f, 1f, 0.35f / g));
                renderer.SpriteRotated(RenderLayer.Projectiles, staff, at, h, angle * Mathf.Deg2Rad, Color.white);
            }

            // Health bar.
            if (data.CloneHpBar && c.Appear >= 1f && c.Dying <= 0f)
                renderer.Bar(RenderLayer.Overlay, new Vector2(c.Pos.x, feet - 0.1f), new Vector2(r * 1.6f, 0.08f),
                    Mathf.Clamp01(c.Hp / Mathf.Max(1f, data.CloneHp)), data.CloneHpBarBackground, data.CloneHpBarColor);
        }

        // Countdown bar under his feet: fills up until the next summon; blinks while waiting for the staff / summoning.
        void DrawSummonBar(InstancedRenderer renderer, float r)
        {
            if (!data.CloneSkill || data.BarWidth <= 0f) return;
            var center = new Vector2(Position.x, Position.y - r + data.BarOffsetY);
            var size = new Vector2(data.BarWidth, data.BarHeight);
            if (data.BarOutline > 0f)
                renderer.Rect(RenderLayer.Overlay, center, size + new Vector2(data.BarOutline * 2f, data.BarOutline * 2f), Color.black);

            float fill;
            var color = data.BarColor;
            if (casting)
            {
                // Drains while he summons (wind-up + every clone coming out).
                var total = data.CloneCastTime + data.CloneSummonGap * Mathf.Max(0, toSummon - 1);
                fill = 1f - Mathf.Clamp01(castTime / Mathf.Max(0.05f, total));
                color = data.BarReadyColor;
            }
            else
            {
                fill = 1f - Mathf.Clamp01(cloneTimer / Mathf.Max(0.05f, cloneTimerFull));
                if (cloneTimer <= 0f)
                    color = Color.Lerp(data.BarColor, data.BarReadyColor, 0.5f + 0.5f * Mathf.Sin(Time.time * 14f));
                else if (cloneTimer < 1f)
                    color = Color.Lerp(color, Color.white, 0.35f + 0.35f * Mathf.Sin(Time.time * 18f));
            }
            renderer.Bar(RenderLayer.Overlay, center, size, fill, data.BarBackground, color);
        }

        Sprite CloneFrame(SpriteAnim anim, Clone c)
        {
            var frames = anim.Frames;
            if (frames.Length <= WaitFrame) return frames[0];
            if (c.Fly < 1f || c.Appear < 1f) return frames[ThrowFrame];   // arm up: jumping out / landing
            if (c.Spin > 0f) return frames[WaitFrame];      // hand out, the staff spins around it
            var fps = c.Walking ? WalkFps : IdleFps;
            return frames[(int)((Time.time + c.Land.x * 0.37f) * fps) % IdleFrames];
        }

        Sprite PickFrame(SpriteAnim anim)
        {
            var frames = anim.Frames;
            if (frames.Length <= WaitFrame) return frames[0];
            if (casting && frames.Length > BlowFrame)
                return frames[castTime < data.CloneCastTime * 0.55f ? PluckFrame : BlowFrame];
            if (throwPose > 0f) return frames[ThrowFrame];
            if (staffOut) return frames[WaitFrame];
            var t = Time.time + Slot * 1.37f;
            if (t % BlinkEvery < BlinkTime) return frames[BlinkFrame];
            return frames[(int)((Time.time + Slot * 0.3f) * IdleFps) % IdleFrames];
        }
    }
}
