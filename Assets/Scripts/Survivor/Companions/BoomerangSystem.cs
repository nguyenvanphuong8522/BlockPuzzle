using System;
using System.Collections.Generic;
using UnityEngine;

namespace Survivor
{
    // Thrown objects that fly out, slow down, then come back to whoever threw them (the monkey's bananas).
    // They hurt every enemy they pass, once on the way out and once on the way back.
    public sealed class BoomerangSystem
    {
        const int HitMemory = 24;            // enemies remembered per pass (so each is hit once per pass)
        const float ReturnAcceleration = 30f;
        const float MaxReturnSpeed = 16f;
        const float CatchDistance = 0.35f;
        const float SpinDegreesPerSecond = 900f;

        readonly EnemySystem enemies;
        readonly SpriteCatalog sprites;
        readonly List<int> hits = new List<int>(32);

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
        int[] hitIds = new int[32 * HitMemory];
        int[] hitCount = new int[32];

        public BoomerangSystem(EnemySystem enemies, SpriteCatalog sprites)
        {
            this.enemies = enemies;
            this.sprites = sprites;
        }

        public void Throw(Companion from, Vector2 dir, float distance, float launchSpeed, float damageAmount)
        {
            if (count == position.Length) Grow();
            var i = count++;
            position[i] = from.Position;
            direction[i] = dir.normalized;
            travelled[i] = 0f;
            range[i] = distance;
            speed[i] = launchSpeed;
            damage[i] = damageAmount;
            spin[i] = UnityEngine.Random.Range(0f, 360f);
            returning[i] = false;
            owner[i] = from;
            hitCount[i] = 0;
        }

        public void Tick(float dt)
        {
            for (var i = count - 1; i >= 0; i--)
            {
                spin[i] += SpinDegreesPerSecond * dt;

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
                    speed[i] = Mathf.Min(MaxReturnSpeed, speed[i] + ReturnAcceleration * dt);
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
            enemies.QueryRadius(position[i], 0.35f, hits);
            foreach (var e in hits)
            {
                var id = enemies.Id[e];
                if (AlreadyHit(i, id)) continue;
                var push = returning[i] ? (enemies.Position[e] - position[i]).normalized : direction[i];
                enemies.Damage(e, damage[i], push, 2f);
                if (hitCount[i] < HitMemory) hitIds[i * HitMemory + hitCount[i]++] = id;
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
            hitCount[i] = hitCount[last];
            Array.Copy(hitIds, last * HitMemory, hitIds, i * HitMemory, HitMemory);
        }

        public void Draw(InstancedRenderer renderer)
        {
            for (var i = 0; i < count; i++)
            {
                var angle = spin[i] * Mathf.Deg2Rad;
                if (sprites.Banana != null)
                    renderer.SpriteRotated(RenderLayer.Projectiles, sprites.Banana, position[i], 0.55f, angle, Color.white);
                else
                    renderer.Circle(RenderLayer.Projectiles, position[i], 0.2f, new Color(1f, 0.85f, 0.2f));
            }
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
            Array.Resize(ref hitIds, size * HitMemory);
            Array.Resize(ref hitCount, size);
        }
    }
}
