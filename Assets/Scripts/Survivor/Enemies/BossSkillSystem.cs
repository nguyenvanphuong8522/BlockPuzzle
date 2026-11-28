using UnityEngine;

namespace Survivor
{
    // Boss skills (all numbers in Assets/Resources/BossSettings.asset). The boss walks toward the hero, and every
    // Boss Skill Interval seconds uses a skill, alternating:
    //   1. Lao (dash): stands still and locks a straight path toward the hero. A two-layer red warning is drawn on
    //      the ground: the light layer is the whole path, the dark layer runs from start to end during the cast.
    //      When it reaches the end the boss dashes along that exact line. Can combo (re-aims before each dash).
    //   2. Quẹt càng (claw swipe): stands still, a red rectangle toward the hero fills from the boss outward
    //      (same two-layer warning as the dash), then the claw sweeps across it and hurts the hero inside. Can combo.
    //   3. Đạn tỏa (bullet burst): winds up, then fires several waves: a full ring, or a fan aimed at the hero
    //      (Burst Angle).
    // Runs right after EnemySystem.Tick and before BossArenaSystem (so the arena wall still stops a dash).
    public sealed class BossSkillSystem
    {
        enum State { Walk, DashCast, Dash, BurstCast, Burst, SwipeCast, Swipe }

        const int MaxBullets = 1024;

        readonly EnemySystem enemies;
        readonly Player player;
        readonly BossRoster roster;
        BossSettings tuning => roster.Settings; // the boss fighting now (each boss has its own)
        BossStats stats => roster.Stats;
        readonly EffectSystem effects;
        readonly CameraShake shake;
        readonly AudioService audio;
        readonly BossArenaSystem arena;
        readonly SpriteCatalog sprites;

        int bossId = -1;
        State state;
        float timer;
        int nextSkill;            // 0 = dash, 1 = claw swipe, 2 = burst
        Vector2 dashDir = Vector2.right;
        float dashLeft;
        float dashLength;         // planned length (cut at the arena wall)
        bool dashHitsWall;
        float dashCast;           // length of the current warning (first dash / combo dash)
        int dashesLeft;
        int wavesLeft, waveIndex;

        // Claw swipe
        Vector2 swipeDir = Vector2.right;
        float swipeCast;          // length of the current warning (first swing / combo swing)
        int swipesLeft;
        float swipeSide = 1f;     // +1 / -1: which way the claw sweeps
        bool swipeHit;            // the hero was already hit by this swing

        // Boss bullets (structure of arrays, swap-remove).
        readonly Vector2[] bulletPos = new Vector2[MaxBullets];
        readonly Vector2[] bulletVel = new Vector2[MaxBullets];
        readonly float[] bulletLife = new float[MaxBullets];
        int bulletCount;

        public BossSkillSystem(EnemySystem enemies, Player player, BossRoster roster, EffectSystem effects,
            CameraShake shake, AudioService audio, BossArenaSystem arena, SpriteCatalog sprites)
        {
            this.enemies = enemies;
            this.player = player;
            this.roster = roster;
            this.effects = effects;
            this.shake = shake;
            this.audio = audio;
            this.arena = arena;
            this.sprites = sprites;
        }

        public void Tick(float dt)
        {
            TickBullets(dt);

            var b = enemies.BossIndex;
            if (b < 0)
            {
                bossId = -1;
                enemies.BossSpeedScale = 1f;
                return;
            }
            if (enemies.Id[b] != bossId)
            {
                // A new boss: start walking, first skill after a short delay, dash first.
                bossId = enemies.Id[b];
                state = State.Walk;
                timer = tuning.FirstSkillDelay;
                nextSkill = 0;
            }

            var pos = enemies.Position[b];
            switch (state)
            {
                case State.Walk:
                    enemies.BossSpeedScale = 1f;
                    timer -= dt;
                    if (timer <= 0f)
                    {
                        timer = 0f;
                        TryStartSkill(pos); // waits (keeps walking) until the hero is at a distance some skill fits
                    }
                    break;

                case State.DashCast:
                    enemies.BossSpeedScale = 0f;
                    timer += dt;
                    if (timer >= dashCast)
                    {
                        state = State.Dash;
                        dashLeft = dashLength;
                        shake.Add(0.25f);
                        audio.PlayAtPitch(Sfx.Slash, 0.55f, 0.9f, 0.05f);
                    }
                    break;

                case State.Dash:
                {
                    enemies.BossSpeedScale = 0f;
                    var step = Mathf.Min(dashLeft, tuning.DashSpeed * dt);
                    var next = pos + dashDir * step;
                    enemies.Position[b] = next;
                    dashLeft -= step;
                    if (Random.value < 0.5f) effects.Smoke(pos - dashDir * enemies.Radius[b] * 0.6f, enemies.Radius[b] * 0.5f, 0.3f);
                    var reach = enemies.Radius[b] + player.Radius;
                    if ((player.Position - next).sqrMagnitude < reach * reach) player.TryHurt(stats.DashDamage);
                    if (dashLeft <= 0f)
                    {
                        if (dashHitsWall) HitWall(next, enemies.Radius[b]);
                        dashesLeft--;
                        if (dashesLeft <= 0) EndSkill();
                        else
                        {
                            // Next dash of the combo: re-aim at the hero, shorter warning.
                            state = State.DashCast;
                            timer = 0f;
                            dashCast = tuning.DashComboCastTime;
                            AimDash(next);
                        }
                    }
                    break;
                }

                case State.SwipeCast:
                    enemies.BossSpeedScale = 0f;
                    timer += dt;
                    if (timer <= tuning.SwipeAimTime) AimSwipe(pos); // still turning toward the hero, then locked
                    if (timer >= swipeCast)
                    {
                        state = State.Swipe;
                        timer = 0f;
                        swipeHit = false;
                        shake.Add(0.2f);
                        audio.PlayAtPitch(Sfx.Slash, 0.7f, 0.9f, 0.08f);
                    }
                    break;

                case State.Swipe:
                {
                    enemies.BossSpeedScale = 0f;
                    timer += dt;
                    var p = Mathf.Clamp01(timer / tuning.SwipeSwingTime);
                    if (!swipeHit && SwipeReachesHero(pos, p))
                    {
                        swipeHit = true;
                        player.TryHurt(stats.SwipeDamage);
                        shake.Add(0.2f);
                    }
                    if (p >= 1f)
                    {
                        swipesLeft--;
                        if (swipesLeft <= 0) EndSkill();
                        else
                        {
                            // Next swing of the combo: the claw comes back the other way.
                            state = State.SwipeCast;
                            timer = 0f;
                            swipeCast = tuning.SwipeComboCastTime;
                            swipeSide = -swipeSide;
                            AimSwipe(pos);
                        }
                    }
                    break;
                }

                case State.BurstCast:
                case State.Burst:
                    enemies.BossSpeedScale = 0f;
                    timer -= dt;
                    if (timer > 0f) break;
                    FireWave(b);
                    wavesLeft--;
                    if (wavesLeft <= 0) EndSkill();
                    else
                    {
                        state = State.Burst;
                        timer = tuning.BurstWaveDelay;
                    }
                    break;
            }
        }

        // Skills in turn (dash, claw swipe, bullets), starting from the one after the last used; the first one that is
        // turned on and whose distance range holds the hero is used. None fits: nothing happens (checked again next frame).
        void TryStartSkill(Vector2 bossPos)
        {
            var dist = Vector2.Distance(player.Position, bossPos);
            for (var k = 0; k < 3; k++)
            {
                var skill = (nextSkill + k) % 3;
                if (!CanUse(skill, dist, bossPos)) continue;
                StartSkill(skill, bossPos);
                nextSkill = (skill + 1) % 3;
                return;
            }
        }

        bool CanUse(int skill, float dist, Vector2 bossPos)
        {
            switch (skill)
            {
                case 0:
                    if (!tuning.DashEnabled || dist < tuning.DashMinDistance || dist > tuning.DashMaxDistance) return false;
                    // Pressed against the wall in that direction: no room to dash.
                    return !(tuning.DashStopAtWall && arena.Active &&
                             DistanceToWall(bossPos, (player.Position - bossPos).normalized, enemies.Radius[enemies.BossIndex]) < 1.5f);
                case 1: return tuning.SwipeEnabled && dist >= tuning.SwipeMinDistance && dist <= tuning.SwipeMaxDistance;
                default: return tuning.BurstEnabled && dist >= tuning.BurstMinDistance && dist <= tuning.BurstMaxDistance;
            }
        }

        void StartSkill(int skill, Vector2 bossPos)
        {
            if (skill == 0)
            {
                state = State.DashCast;
                timer = 0f;
                dashCast = tuning.DashCastTime;
                dashesLeft = Mathf.Max(1, tuning.DashCombo);
                AimDash(bossPos);
            }
            else if (skill == 1)
            {
                state = State.SwipeCast;
                timer = 0f;
                swipeCast = tuning.SwipeCastTime;
                swipesLeft = Mathf.Max(1, tuning.SwipeCombo);
                swipeSide = Random.value < 0.5f ? 1f : -1f;
                AimSwipe(bossPos);
                audio.PlayAtPitch(Sfx.Cast, 0.8f, 0.6f, 0.1f);
            }
            else
            {
                state = State.BurstCast;
                timer = tuning.BurstCastTime;
                wavesLeft = tuning.BurstWaves;
                waveIndex = 0;
                audio.Play(Sfx.Cast, 0.7f, 0.05f, 0.1f);
            }
        }

        // How far the boss (its body, radius r) can go from `from` along `dir` before touching the arena wall.
        float DistanceToWall(Vector2 from, Vector2 dir, float r)
        {
            var c = arena.Center;
            var half = arena.Half;
            var lx = Mathf.Max(0f, half.x - r);
            var ly = Mathf.Max(0f, half.y - r);
            var t = float.MaxValue;
            if (dir.x > 0.0001f) t = Mathf.Min(t, (c.x + lx - from.x) / dir.x);
            else if (dir.x < -0.0001f) t = Mathf.Min(t, (c.x - lx - from.x) / dir.x);
            if (dir.y > 0.0001f) t = Mathf.Min(t, (c.y + ly - from.y) / dir.y);
            else if (dir.y < -0.0001f) t = Mathf.Min(t, (c.y - ly - from.y) / dir.y);
            return Mathf.Max(0f, t);
        }

        // The dashing boss slams into the wall: dust, a shake and a thud.
        void HitWall(Vector2 at, float r)
        {
            var front = at + dashDir * r;
            effects.Smoke(front, r * 0.9f, 0.45f);
            effects.Burst(front, r * 1.2f, tuning.DashFillColor, 0.25f);
            shake.Add(0.45f);
            audio.PlayAtPitch(Sfx.BossSpawn, 0.7f, 0.6f, 0.05f);
        }

        // Locks a straight path toward the hero, cut at the arena wall (the boss stops there, the warning shows where).
        void AimDash(Vector2 bossPos)
        {
            var toHero = player.Position - bossPos;
            dashDir = toHero.sqrMagnitude > 0.0001f ? toHero.normalized : Vector2.right;
            dashLength = tuning.DashDistance;
            dashHitsWall = false;
            if (tuning.DashStopAtWall && arena.Active)
            {
                var toWall = DistanceToWall(bossPos, dashDir, enemies.Radius[enemies.BossIndex]);
                if (toWall < dashLength)
                {
                    dashLength = Mathf.Max(0f, toWall);
                    dashHitsWall = true;
                }
            }
        }

        void AimSwipe(Vector2 bossPos)
        {
            var toHero = player.Position - bossPos;
            if (toHero.sqrMagnitude > 0.0001f) swipeDir = toHero.normalized;
        }

        // Rectangle in front of the boss: from Offset to Offset + Length along swipeDir, Width across.
        Vector2 SwipeAcross => new Vector2(-swipeDir.y, swipeDir.x);

        // Has the claw (swept across the width from one side to the other, progress p 0..1) passed over the hero?
        bool SwipeReachesHero(Vector2 bossPos, float p)
        {
            if (player.IsDead) return false;
            var toHero = player.Position - bossPos;
            var r = player.Radius;
            var along = Vector2.Dot(toHero, swipeDir);
            var near = tuning.SwipeOffset;
            if (along < near - r || along > near + tuning.SwipeLength + r) return false;
            var half = tuning.SwipeWidth * 0.5f;
            var across = Vector2.Dot(toHero, SwipeAcross) * swipeSide; // along the sweep direction
            if (Mathf.Abs(across) > half + r) return false;
            var edge = -half + 2f * half * p; // the claw sweeps from -half to +half
            return across <= edge + r;
        }

        void EndSkill()
        {
            state = State.Walk;
            timer = tuning.SkillInterval;
            enemies.BossSpeedScale = 1f;
        }

        // One wave of bullets. 360 degrees: a full ring, every other wave turned half a step so the gaps change.
        // Less: a fan of Burst Angle degrees centered on the hero (aimed at each wave), bullets edge to edge.
        void FireWave(int b)
        {
            var n = Mathf.Max(3, tuning.BurstBullets);
            var origin = enemies.Position[b];
            float step, offset;
            if (tuning.BurstAngle >= 359.9f)
            {
                step = Mathf.PI * 2f / n;
                offset = (waveIndex % 2 == 1 ? 0.5f : 0f) * step;
            }
            else
            {
                var arc = tuning.BurstAngle * Mathf.Deg2Rad;
                var toHero = player.Position - origin;
                var aim = toHero.sqrMagnitude > 0.0001f ? Mathf.Atan2(toHero.y, toHero.x) : 0f;
                step = arc / (n - 1);
                offset = aim - arc * 0.5f;
            }
            var start = enemies.Radius[b] * 0.8f;
            for (var k = 0; k < n && bulletCount < MaxBullets; k++)
            {
                var a = offset + k * step;
                var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                var i = bulletCount++;
                bulletPos[i] = origin + dir * start;
                bulletVel[i] = dir * tuning.BulletSpeed;
                bulletLife[i] = tuning.BulletLifetime;
            }
            waveIndex++;
            effects.Ring(origin, enemies.Radius[b] * 1.6f, tuning.BulletColor, 0.3f);
            shake.Add(0.15f);
            audio.PlayAtPitch(Sfx.Shoot, 0.6f, 0.7f, 0.05f);
        }

        void TickBullets(float dt)
        {
            var hero = player.Position;
            var reach = player.Radius + tuning.BulletRadius;
            var reachSq = reach * reach;
            for (var i = bulletCount - 1; i >= 0; i--)
            {
                bulletLife[i] -= dt;
                bulletPos[i] += bulletVel[i] * dt;
                var hit = !player.IsDead && (bulletPos[i] - hero).sqrMagnitude < reachSq;
                if (hit)
                {
                    player.TryHurt(stats.BulletDamage);
                    effects.Burst(bulletPos[i], tuning.BulletRadius * 2f, tuning.BulletColor, 0.15f);
                }
                if (!hit && bulletLife[i] > 0f) continue;
                var last = --bulletCount;
                bulletPos[i] = bulletPos[last];
                bulletVel[i] = bulletVel[last];
                bulletLife[i] = bulletLife[last];
            }
        }

        public void Draw(InstancedRenderer renderer)
        {
            var b = enemies.BossIndex;
            if (b >= 0 && b < enemies.Count)
            {
                var pos = enemies.Position[b];
                var radius = enemies.Radius[b];
                if (state == State.DashCast) DrawDashWarning(renderer, pos, radius);
                else if (state == State.SwipeCast || state == State.Swipe) DrawSwipe(renderer, pos);
                else if (state == State.BurstCast)
                {
                    // Wind-up: a ring closing in on the boss.
                    var k = 1f - Mathf.Clamp01(timer / Mathf.Max(0.05f, tuning.BurstCastTime));
                    var c = tuning.BulletColor;
                    c.a = 0.4f + 0.5f * k;
                    renderer.Ring(RenderLayer.Effects, pos, radius * (2.4f - 1.0f * k), 0.1f, c);
                }
            }

            var color = tuning.BulletColor;
            var r = tuning.BulletRadius;
            var glow = color;
            glow.a = 0.35f;
            for (var i = 0; i < bulletCount; i++)
            {
                var p = bulletPos[i];
                if (!renderer.IsVisible(p, r * 2f)) continue;
                renderer.Circle(RenderLayer.Projectiles, p, r * 1.7f, glow);
                renderer.Circle(RenderLayer.Projectiles, p, r, color);
                renderer.Circle(RenderLayer.Projectiles, p, r * 0.45f, new Color(1f, 0.95f, 0.8f));
            }
        }

        // Claw swipe. Cast: two layers on the ground like the dash (light = whole area, dark = fills from the boss
        // outward). Swing: a bright slash sweeps across the area, leaving a fading trail.
        void DrawSwipe(InstancedRenderer renderer, Vector2 center)
        {
            var length = tuning.SwipeLength;
            var width = tuning.SwipeWidth;
            var near = center + swipeDir * tuning.SwipeOffset;
            var far = near + swipeDir * length;

            if (state == State.SwipeCast)
            {
                var k = Mathf.Clamp01(timer / Mathf.Max(0.05f, swipeCast));
                renderer.Line(RenderLayer.Shadows, near, far, width, tuning.SwipeWarnColor);
                if (k > 0.001f) renderer.Line(RenderLayer.Shadows, near, near + swipeDir * (length * k), width, tuning.SwipeFillColor);
                DrawClaw(renderer, center, near);
                return;
            }

            // Swing: the claw edge sweeps across the width; the part already swept stays as a fading trail.
            var p = Mathf.Clamp01(timer / tuning.SwipeSwingTime);
            var across = SwipeAcross * swipeSide;
            var half = width * 0.5f;
            var edge = -half + width * p;
            var swept = edge + half;
            var trail = tuning.SwipeFillColor;
            trail.a *= 1f - 0.5f * p;
            if (swept > 0.01f)
            {
                var mid = across * ((-half + edge) * 0.5f);
                renderer.Line(RenderLayer.Shadows, near + mid, far + mid, swept, trail);
            }
            var slash = Mathf.Min(width, 0.45f);
            var slashMid = across * (edge - slash * 0.5f);
            renderer.Line(RenderLayer.Effects, near + slashMid - swipeDir * 0.1f, far + slashMid + swipeDir * 0.1f, slash,
                tuning.SwipeSlashColor);
            DrawClaw(renderer, center, near);
        }

        // The big claw: raised on the side it will swing from during the warning (pulled back further and shaking
        // as the warning fills), then swung across the rectangle with the slash, snapping shut halfway.
        void DrawClaw(InstancedRenderer renderer, Vector2 center, Vector2 near)
        {
            if (!tuning.ShowClaw) return;
            var art = sprites.BossClaw;
            if (art == null || art.IsEmpty) return;
            var across = SwipeAcross * swipeSide;    // direction the claw sweeps toward
            Vector2 at, point;
            int frame;
            if (state == State.SwipeCast)
            {
                var k = Mathf.Clamp01(timer / Mathf.Max(0.05f, swipeCast));
                at = center - across * tuning.ClawRaiseDistance + swipeDir * Mathf.Lerp(0.5f, 0.1f, k);
                at += Random.insideUnitCircle * (0.07f * k);                  // trembles before the swing
                point = (swipeDir * Mathf.Lerp(1f, 0.25f, k) - across * Mathf.Lerp(0.3f, 1f, k)).normalized; // wound back
                frame = 0;
            }
            else
            {
                var p = Mathf.Clamp01(timer / tuning.SwipeSwingTime);
                var half = tuning.SwipeWidth * 0.5f;
                at = near + swipeDir * (tuning.SwipeLength * tuning.ClawAlong) + across * (-half + tuning.SwipeWidth * p);
                point = (swipeDir + across * 0.6f).normalized;              // leaning into the swing
                frame = p < 0.5f ? 0 : 1;
            }
            var sprite = art.Frames[Mathf.Min(frame, art.Frames.Length - 1)];
            renderer.SpriteRotated(RenderLayer.Overlay, sprite, at, tuning.ClawSize, Mathf.Atan2(point.y, point.x),
                tuning.ClawTint, swipeSide < 0f);
        }

        // Two layers on the ground along the dash path: light = whole path, dark = progress of the cast.
        void DrawDashWarning(InstancedRenderer renderer, Vector2 from, float radius)
        {
            var width = radius * 2f * tuning.DashWidthScale;
            var length = dashLength;
            var to = from + dashDir * length;
            var k = Mathf.Clamp01(timer / Mathf.Max(0.05f, dashCast));
            renderer.Line(RenderLayer.Shadows, from, to, width, tuning.DashWarnColor);
            if (k > 0.001f) renderer.Line(RenderLayer.Shadows, from, from + dashDir * (length * k), width, tuning.DashFillColor);
        }
    }
}
