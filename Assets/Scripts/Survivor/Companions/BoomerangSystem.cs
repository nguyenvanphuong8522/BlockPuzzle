using System;
using System.Collections.Generic;
using UnityEngine;

namespace Survivor
{
    // How one thrown object looks and hits. Default (no style) = the monkey's banana.
    public struct BoomerangStyle
    {
        public Sprite Sprite;          // null = the banana
        public float Size;             // longest side of the drawn sprite (world units)
        public float HitRadius;        // enemies inside this circle are hit
        public float SpinSpeed;        // degrees per second
        public float Knockback;
        public float ReturnSpeed;      // top speed on the way back
        public int Ghosts;             // faded copies behind it (spin blur)
        public Color FallbackColor;    // drawn as a circle when there is no sprite

        public static BoomerangStyle Banana(SpriteCatalog sprites) => new BoomerangStyle
        {
            Sprite = sprites.Banana, Size = 0.55f, HitRadius = 0.35f, SpinSpeed = 900f, Knockback = 2f,
            ReturnSpeed = 16f, Ghosts = 0, FallbackColor = new Color(1f, 0.85f, 0.2f)
        };
    }

    // Thrown objects that fly out, slow down, then come back to whoever threw them (the monkey's bananas, the
    // monkey king's staff). They hurt every enemy they pass, once on the way out and once on the way back.
    public sealed class BoomerangSystem
    {
        const int HitMemory = 96;            // enemies remembered per pass (so each is hit once per pass)
        const float ReturnAcceleration = 30f;
        const float CatchDistance = 0.35f;

        readonly EnemySystem enemies;
        readonly SpriteCatalog sprites;
        readonly List<int> hits = new List<int>(64);

        int count;
        Vector2[] position = new Vector2[32];
        Vector2[] direction = new Vector2[32];
        float[] travelled = new float[32];
        float[] range = new float[32];
        float[] speed = new float[32];
        float[] damage = new float[32];
        float[] spin = new float[32];
        bool[] returning = new bool[32];
        Companion[] owner = new Companion[32];
        BoomerangStyle[] style = new BoomerangStyle[32];
        int[] hitIds = new int[32 * HitMemory];
        int[] hitCount = new int[32];

        public BoomerangSystem(EnemySystem enemies, SpriteCatalog sprites)
        {
            this.enemies = enemies;
            this.sprites = sprites;
        }

        public void Throw(Companion from, Vector2 dir, float distance, float launchSpeed, float damageAmount) =>
            Throw(from, dir, distance, launchSpeed, damageAmount, BoomerangStyle.Banana(sprites));

        public void Throw(Companion from, Vector2 dir, float distance, float launchSpeed, float damageAmount,
            BoomerangStyle look)
        {
            if (count == position.Length) Grow();
            var i = count++;
            position[i] = from.Position;
            direction[i] = dir.normalized;
            travelled[i] = 0f;
            range[i] = Mathf.Max(0.5f, distance);
            speed[i] = launchSpeed;
            damage[i] = damageAmount;
            spin[i] = UnityEngine.Random.Range(0f, 360f);
            returning[i] = false;
            owner[i] = from;
            style[i] = look;
            hitCount[i] = 0;
        }

        // How many of this companion's throws are still flying.
        public int CountOwnedBy(Companion who)
        {
            var n = 0;
            for (var i = 0; i < count; i++)
                if (owner[i] == who) n++;
            return n;
        }

        // A companion left (e.g. used up by a fusion): its throws vanish.
        public void RemoveOwnedBy(Companion who)
        {
            for (var i = count - 1; i >= 0; i--)
                if (owner[i] == who) RemoveAt(i);
        }

        public void Tick(float dt)
        {
            for (var i = count - 1; i >= 0; i--)
            {
                spin[i] += style[i].SpinSpeed * dt;

                if (!returning[i])
                {
                    // Way out: fast at first, slowing to a stop at full range (ease-out), then turn around.
                    var left = 1f - travelled[i] / range[i];
                    var step = (speed[i] * left + 1.5f) * dt;
                    position[i] += direction[i] * step;
                    travelled[i] += step;
                    if (travelled[i] >= range[i])
                    {
                        returning[i] = true;
                        speed[i] = 2f;
                        hitCount[i] = 0; // the way back can hit the same enemies again
                    }
                }
                else
                {
                    // Way back: accelerate toward the thrower's current position, so it is caught even if it moved.
                    var toOwner = owner[i].Position - position[i];
                    var dist = toOwner.magnitude;
                    speed[i] = Mathf.Min(Mathf.Max(1f, style[i].ReturnSpeed), speed[i] + ReturnAcceleration * dt);
                    var step = speed[i] * dt;
                    if (dist <= Mathf.Max(CatchDistance, step))
                    {
                        RemoveAt(i);
                        continue;
                    }
                    position[i] += toOwner / dist * step;
                }

                HitEnemies(i);
            }
        }

        void HitEnemies(int i)
        {
            enemies.QueryRadius(position[i], style[i].HitRadius, hits);
            foreach (var e in hits)
            {
                var id = enemies.Id[e];
                if (AlreadyHit(i, id)) continue;
                if (hitCount[i] >= HitMemory) break; // memory full: the rest wait for the next pass
                var push = returning[i] ? (enemies.Position[e] - position[i]).normalized : direction[i];
                enemies.Damage(e, damage[i], push, style[i].Knockback);
                hitIds[i * HitMemory + hitCount[i]++] = id;
            }
        }

        bool AlreadyHit(int i, int id)
        {
            for (var k = 0; k < hitCount[i]; k++)
                if (hitIds[i * HitMemory + k] == id) return true;
            return false;
        }

        void RemoveAt(int i)
        {
            var last = --count;
            if (i == last)
            {
                owner[i] = null;
                return;
            }
            position[i] = position[last];
            direction[i] = direction[last];
            travelled[i] = travelled[last];
            range[i] = range[last];
            speed[i] = speed[last];
            damage[i] = damage[last];
            spin[i] = spin[last];
            returning[i] = returning[last];
            owner[i] = owner[last];
            owner[last] = null;
            style[i] = style[last];
            hitCount[i] = hitCount[last];
            Array.Copy(hitIds, last * HitMemory, hitIds, i * HitMemory, HitMemory);
        }

        public void Draw(InstancedRenderer renderer)
        {
            for (var i = 0; i < count; i++)
            {
                var s = style[i];
                var sprite = s.Sprite;
                if (sprite == null)
                {
                    renderer.Circle(RenderLayer.Projectiles, position[i], Mathf.Max(0.1f, s.HitRadius * 0.6f), s.FallbackColor);
                    continue;
                }
                // Size = longest side, so a long thin staff and a square banana use the same setting.
                var rect = sprite.rect;
                var height = s.Size * rect.height / Mathf.Max(rect.width, rect.height);
                // Spin blur: faded copies a little behind in the rotation.
                for (var g = s.Ghosts; g >= 1; g--)
                {
                    var back = Mathf.Sign(s.SpinSpeed) * g * 22f;
                    renderer.SpriteRotated(RenderLayer.Projectiles, sprite, position[i], height,
                        (spin[i] - back) * Mathf.Deg2Rad, new Color(1f, 1f, 1f, 0.38f / g));
                }
                renderer.SpriteRotated(RenderLayer.Projectiles, sprite, position[i], height, spin[i] * Mathf.Deg2Rad, Color.white);
            }
        }

        // Debug: the hit circle of each throw owned by `who`.
        public void DrawHitAreas(InstancedRenderer renderer, Companion who, Color color)
        {
            for (var i = 0; i < count; i++)
                if (owner[i] == who)
                    renderer.Ring(RenderLayer.Overlay, position[i], style[i].HitRadius, 0.04f, color);
        }

        void Grow()
        {
            var size = position.Length * 2;
            Array.Resize(ref position, size);
            Array.Resize(ref direction, size);
            Array.Resize(ref travelled, size);
            Array.Resize(ref range, size);
            Array.Resize(ref speed, size);
            Array.Resize(ref damage, size);
            Array.Resize(ref spin, size);
            Array.Resize(ref returning, size);
            Array.Resize(ref owner, size);
            Array.Resize(ref style, size);
            Array.Resize(ref hitIds, size * HitMemory);
            Array.Resize(ref hitCount, size);
        }
    }
}
