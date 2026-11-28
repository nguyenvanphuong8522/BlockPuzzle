using UnityEngine;

namespace Survivor
{
    // Ranged enemies (EnemyDefinition.IsRanged, e.g. the ink squid): they keep their distance (EnemySystem move job)
    // and shoot at the hero. Before each shot a ball swells at its mouth for Windup seconds (a visible warning),
    // then flies straight at where the hero was. A shot that touches the hero hurts him.
    // Numbers: each ranged type's own assets (<Id>_Settings "Bắn xa": timing / range / look, <Id>_Stats: Shot Damage);
    // "only shoot when on screen" is shared (EnemySettings).
    public sealed class EnemyBulletSystem
    {
        const int MaxBullets = 512;

        readonly EnemySystem enemies;
        readonly Player player;
        readonly GameConfig config;
        readonly EnemySettings tuning;
        readonly EffectSystem effects;
        readonly AudioService audio;
        readonly InstancedRenderer renderer;  // visible area (last frame), for "only shoot when on screen"

        readonly Vector2[] pos = new Vector2[MaxBullets];
        readonly Vector2[] vel = new Vector2[MaxBullets];
        readonly float[] life = new float[MaxBullets];
        readonly int[] kind = new int[MaxBullets]; // enemy type that shot it (its look / damage)
        int count;

        public EnemyBulletSystem(EnemySystem enemies, Player player, GameConfig config, EnemySettings tuning,
            EffectSystem effects, AudioService audio, InstancedRenderer renderer)
        {
            this.renderer = renderer;
            this.enemies = enemies;
            this.player = player;
            this.config = config;
            this.tuning = tuning;
            this.effects = effects;
            this.audio = audio;
        }

        EnemyDefinition TypeOf(int enemy) => config.Enemies[enemies.TypeOf(enemy)];

        // Where its shots come out: the type's Muzzle Offset (body radii), x mirrored to the side the art faces
        // (sprites face right and flip to face the hero; flyers face their flight).
        Vector2 Mouth(int i)
        {
            var p = enemies.Position[i];
            var r = enemies.Radius[i];
            var offset = TypeOf(i).Settings.MuzzleOffset;
            var facing = p.x > player.Position.x ? -1f : 1f;
            return p + new Vector2(offset.x * facing * r, offset.y * r);
        }

        bool CanShoot(int i, EnemyTypeSettings s)
        {
            var p = enemies.Position[i];
            var range = s.ShootRange;
            if ((p - player.Position).sqrMagnitude > range * range) return false;
            if (!tuning.RangedOnlyOnScreen) return true;
            var v = renderer.ViewRect;
            return p.x >= v.x && p.x <= v.z && p.y >= v.y && p.y <= v.w;
        }

        public void Tick(float dt)
        {
            // Shooters: count down; fire when the timer runs out (the windup is drawn in the last Windup seconds).
            if (!player.IsDead)
                for (var i = 0; i < enemies.Count; i++)
                {
                    if (!enemies.IsRanged(i) || !enemies.IsAlive(i)) continue;
                    var def = TypeOf(i);
                    var s = def.Settings;
                    var t = enemies.ShootTimer[i] - dt;
                    // Out of range: hold the shot just before the windup, so it only swells when it can shoot.
                    if (t <= s.Windup && !CanShoot(i, s)) t = Mathf.Max(t, s.Windup + 0.01f);
                    if (t <= 0f)
                    {
                        Fire(i, enemies.TypeOf(i), s);
                        t = s.ShotInterval * Random.Range(0.85f, 1.15f);
                    }
                    enemies.ShootTimer[i] = t;
                }

            // Shots
            for (var i = count - 1; i >= 0; i--)
            {
                var def = config.Enemies[kind[i]];
                var s = def.Settings;
                pos[i] += vel[i] * dt;
                life[i] -= dt;
                var reach = player.Radius + s.ShotRadius;
                if (!player.IsDead && (pos[i] - player.Position).sqrMagnitude <= reach * reach)
                {
                    player.TryHurt(def.Stats.ShotDamage);
                    effects.Burst(pos[i], s.ShotSize * 1.4f, s.ShotColor, 0.2f);
                    RemoveAt(i);
                    continue;
                }
                if (life[i] <= 0f)
                {
                    effects.Ring(pos[i], s.ShotSize * 0.8f, s.ShotColor, 0.2f);
                    RemoveAt(i);
                }
            }
        }

        void Fire(int i, int type, EnemyTypeSettings s)
        {
            if (count >= MaxBullets) return;
            var from = Mouth(i);
            var dir = player.Position - from;
            dir = dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector2.down;
            var k = count++;
            pos[k] = from;
            vel[k] = dir * s.ShotSpeed;
            life[k] = s.ShotLifetime;
            kind[k] = type;
            effects.Burst(from, s.ShotSize, s.ShotColor, 0.15f);
            audio.PlayAtPitch(Sfx.Sling, 0.6f, 0.4f, 0.08f);
        }

        void RemoveAt(int i)
        {
            var last = --count;
            pos[i] = pos[last];
            vel[i] = vel[last];
            life[i] = life[last];
            kind[i] = kind[last];
        }

        // Boss fight starts / run ends: remove every shot.
        public void Clear() => count = 0;

        public void Draw(InstancedRenderer renderer)
        {
            // Windup: a ball swelling at the mouth of every shooter about to fire.
            for (var i = 0; i < enemies.Count; i++)
            {
                if (!enemies.IsRanged(i) || !enemies.IsAlive(i)) continue;
                var s = TypeOf(i).Settings;
                if (s.Windup <= 0f) continue;
                var t = enemies.ShootTimer[i];
                if (t > s.Windup) continue;
                var k = 1f - Mathf.Clamp01(t / s.Windup);
                var wobble = 1f + 0.12f * Mathf.Sin(Time.time * 40f);
                DrawShot(renderer, s, Mouth(i), s.ShotSize * Mathf.Lerp(0.2f, 0.85f, k) * wobble, 1f, i);
            }

            for (var i = 0; i < count; i++)
            {
                var s = config.Enemies[kind[i]].Settings;
                var size = s.ShotSize;
                var fade = Mathf.Clamp01(life[i] / 0.25f);
                // A short glowing trail behind the shot.
                var back = -vel[i].normalized;
                DrawShot(renderer, s, pos[i] + back * size * 0.6f, size * 0.55f, 0.45f * fade, i);
                DrawShot(renderer, s, pos[i] + back * size * 1.05f, size * 0.3f, 0.25f * fade, i);
                DrawShot(renderer, s, pos[i], size, fade, i);
            }
        }

        // Glowing shot: soft pulsing glow, colored ball, bright center (the type's Shot Color / Glow / Core).
        static void DrawShot(InstancedRenderer renderer, EnemyTypeSettings s, Vector2 at, float size, float alpha, int seed)
        {
            var r = size * 0.5f;
            var pulse = 0.85f + 0.15f * Mathf.Sin(Time.time * 14f + seed);
            var glow = s.ShotGlowColor;
            glow.a *= alpha;
            renderer.Circle(RenderLayer.Effects, at, r * s.ShotGlowSize * pulse, glow);
            glow.a *= 0.8f;
            renderer.Circle(RenderLayer.Effects, at, r * (1f + (s.ShotGlowSize - 1f) * 0.5f) * pulse, glow);
            var body = s.ShotColor;
            body.a *= alpha;
            renderer.Circle(RenderLayer.Effects, at, r, body);
            var core = s.ShotCoreColor;
            core.a *= alpha;
            renderer.Circle(RenderLayer.Effects, at, r * 0.45f, core);
        }
    }
}
