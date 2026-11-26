using UnityEngine;

namespace Survivor
{
    // Boss skills (all numbers in PlayerTuning, "Kỹ năng boss"). The boss walks toward the hero, and every
    // Boss Skill Interval seconds uses a skill, alternating:
    //   1. Lao (dash): stands still and locks a straight path toward the hero. A two-layer red warning is drawn on
    //      the ground: the light layer is the whole path, the dark layer runs from start to end during the cast.
    //      When it reaches the end the boss dashes along that exact line.
    //   2. Đạn tỏa (bullet burst): winds up, then fires a ring of bullets in 360 degrees, several waves.
    // Runs right after EnemySystem.Tick and before BossArenaSystem (so the arena wall still stops a dash).
    public sealed class BossSkillSystem
    {
        enum State { Walk, DashCast, Dash, BurstCast, Burst }

        const int MaxBullets = 1024;

        readonly EnemySystem enemies;
        readonly Player player;
        readonly PlayerTuning tuning;
        readonly EffectSystem effects;
        readonly CameraShake shake;
        readonly AudioService audio;

        int bossId = -1;
        State state;
        float timer;
        int nextSkill;            // 0 = dash, 1 = burst
        Vector2 dashDir = Vector2.right;
        float dashLeft;
        int wavesLeft, waveIndex;

        // Boss bullets (structure of arrays, swap-remove).
        readonly Vector2[] bulletPos = new Vector2[MaxBullets];
        readonly Vector2[] bulletVel = new Vector2[MaxBullets];
        readonly float[] bulletLife = new float[MaxBullets];
        int bulletCount;

        public BossSkillSystem(EnemySystem enemies, Player player, PlayerTuning tuning, EffectSystem effects,
            CameraShake shake, AudioService audio)
        {
            this.enemies = enemies;
            this.player = player;
            this.tuning = tuning;
            this.effects = effects;
            this.shake = shake;
            this.audio = audio;
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
                timer = tuning.BossFirstSkillDelay;
                nextSkill = 0;
            }

            var pos = enemies.Position[b];
            switch (state)
            {
                case State.Walk:
                    enemies.BossSpeedScale = 1f;
                    timer -= dt;
                    if (timer <= 0f) StartSkill(pos);
                    break;

                case State.DashCast:
                    enemies.BossSpeedScale = 0f;
                    timer += dt;
                    if (timer >= tuning.BossDashCastTime)
                    {
                        state = State.Dash;
                        dashLeft = tuning.BossDashDistance;
                        shake.Add(0.25f);
                        audio.PlayAtPitch(Sfx.Slash, 0.55f, 0.9f, 0.05f);
                    }
                    break;

                case State.Dash:
                {
                    enemies.BossSpeedScale = 0f;
                    var step = Mathf.Min(dashLeft, tuning.BossDashSpeed * dt);
                    var next = pos + dashDir * step;
                    enemies.Position[b] = next;
                    dashLeft -= step;
                    if (Random.value < 0.5f) effects.Smoke(pos - dashDir * enemies.Radius[b] * 0.6f, enemies.Radius[b] * 0.5f, 0.3f);
                    var reach = enemies.Radius[b] + player.Radius;
                    if ((player.Position - next).sqrMagnitude < reach * reach) player.TryHurt(tuning.BossDashDamage);
                    if (dashLeft <= 0f) EndSkill();
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
                        timer = tuning.BossBurstWaveDelay;
                    }
                    break;
            }
        }

        void StartSkill(Vector2 bossPos)
        {
            if (nextSkill == 0)
            {
                state = State.DashCast;
                timer = 0f;
                var toHero = player.Position - bossPos;
                dashDir = toHero.sqrMagnitude > 0.0001f ? toHero.normalized : Vector2.right;
            }
            else
            {
                state = State.BurstCast;
                timer = tuning.BossBurstCastTime;
                wavesLeft = tuning.BossBurstWaves;
                waveIndex = 0;
                audio.Play(Sfx.Cast, 0.7f, 0.05f, 0.1f);
            }
            nextSkill ^= 1;
        }

        void EndSkill()
        {
            state = State.Walk;
            timer = tuning.BossSkillInterval;
            enemies.BossSpeedScale = 1f;
        }

        // One ring of bullets; every other wave is turned half a step so the gaps change.
        void FireWave(int b)
        {
            var n = Mathf.Max(3, tuning.BossBurstBullets);
            var step = Mathf.PI * 2f / n;
            var offset = (waveIndex % 2 == 1 ? 0.5f : 0f) * step;
            var origin = enemies.Position[b];
            var start = enemies.Radius[b] * 0.8f;
            for (var k = 0; k < n && bulletCount < MaxBullets; k++)
            {
                var a = offset + k * step;
                var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                var i = bulletCount++;
                bulletPos[i] = origin + dir * start;
                bulletVel[i] = dir * tuning.BossBulletSpeed;
                bulletLife[i] = tuning.BossBulletLifetime;
            }
            waveIndex++;
            effects.Ring(origin, enemies.Radius[b] * 1.6f, tuning.BossBulletColor, 0.3f);
            shake.Add(0.15f);
            audio.PlayAtPitch(Sfx.Shoot, 0.6f, 0.7f, 0.05f);
        }

        void TickBullets(float dt)
        {
            var hero = player.Position;
            var reach = player.Radius + tuning.BossBulletRadius;
            var reachSq = reach * reach;
            for (var i = bulletCount - 1; i >= 0; i--)
            {
                bulletLife[i] -= dt;
                bulletPos[i] += bulletVel[i] * dt;
                var hit = !player.IsDead && (bulletPos[i] - hero).sqrMagnitude < reachSq;
                if (hit)
                {
                    player.TryHurt(tuning.BossBulletDamage);
                    effects.Burst(bulletPos[i], tuning.BossBulletRadius * 2f, tuning.BossBulletColor, 0.15f);
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
                else if (state == State.BurstCast)
                {
                    // Wind-up: a ring closing in on the boss.
                    var k = 1f - Mathf.Clamp01(timer / Mathf.Max(0.05f, tuning.BossBurstCastTime));
                    var c = tuning.BossBulletColor;
                    c.a = 0.4f + 0.5f * k;
                    renderer.Ring(RenderLayer.Effects, pos, radius * (2.4f - 1.0f * k), 0.1f, c);
                }
            }

            var color = tuning.BossBulletColor;
            var r = tuning.BossBulletRadius;
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

        // Two layers on the ground along the dash path: light = whole path, dark = progress of the cast.
        void DrawDashWarning(InstancedRenderer renderer, Vector2 from, float radius)
        {
            var width = radius * 2f * tuning.BossDashWidthScale;
            var length = tuning.BossDashDistance;
            var to = from + dashDir * length;
            var k = Mathf.Clamp01(timer / Mathf.Max(0.05f, tuning.BossDashCastTime));
            renderer.Line(RenderLayer.Shadows, from, to, width, tuning.BossDashWarnColor);
            if (k > 0.001f) renderer.Line(RenderLayer.Shadows, from, from + dashDir * (length * k), width, tuning.BossDashFillColor);
            // Thin bright edge at the end so you can see where it stops.
            var edge = tuning.BossDashFillColor;
            edge.a = Mathf.Min(1f, edge.a + 0.3f);
            var side = new Vector2(-dashDir.y, dashDir.x) * (width * 0.5f);
            renderer.Line(RenderLayer.Shadows, to - side, to + side, 0.08f, edge);
        }
    }
}
