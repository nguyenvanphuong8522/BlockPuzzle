using System;
using System.Collections.Generic;
using UnityEngine;

namespace Survivor
{
    // Projectiles stored in arrays like enemies. Each remembers the last few enemies it hit,
    // so a piercing shot never damages the same enemy twice.
    public sealed class ProjectileSystem
    {
        const int HitSlots = 4;

        readonly EnemySystem enemies;
        readonly SpriteCatalog sprites;
        readonly GunSettings gun;
        readonly List<int> hits = new List<int>(32);

        int count;
        Vector2[] position = new Vector2[128];
        Vector2[] velocity = new Vector2[128];
        float[] damage = new float[128];
        float[] life = new float[128];
        float[] radius = new float[128];
        int[] pierce = new int[128];
        Color[] color = new Color[128];
        int[] hitIds = new int[128 * HitSlots];

        public int Count => count;

        public ProjectileSystem(EnemySystem enemies, SpriteCatalog sprites, GunSettings gun)
        {
            this.enemies = enemies;
            this.sprites = sprites;
            this.gun = gun;
        }

        public void Spawn(Vector2 from, Vector2 direction, float speed, float damageAmount, float lifetime,
            float hitRadius, int pierceCount, Color tint)
        {
            if (count == position.Length) Grow();
            var i = count++;
            position[i] = from;
            velocity[i] = direction.normalized * speed;
            damage[i] = damageAmount;
            life[i] = lifetime;
            radius[i] = hitRadius;
            pierce[i] = pierceCount;
            color[i] = tint;
            for (var s = 0; s < HitSlots; s++) hitIds[i * HitSlots + s] = 0;
        }

        public void Tick(float dt)
        {
            for (var i = count - 1; i >= 0; i--)
            {
                position[i] += velocity[i] * dt;
                life[i] -= dt;
                if (life[i] <= 0f)
                {
                    RemoveAt(i);
                    continue;
                }

                enemies.QueryRadius(position[i], radius[i], hits);
                foreach (var e in hits)
                {
                    if (AlreadyHit(i, enemies.Id[e])) continue;
                    enemies.Damage(e, damage[i], velocity[i].normalized, 2f);
                    RememberHit(i, enemies.Id[e]);
                    if (--pierce[i] < 0) break;
                }
                if (pierce[i] < 0) RemoveAt(i);
            }
        }

        bool AlreadyHit(int i, int enemyId)
        {
            for (var s = 0; s < HitSlots; s++)
                if (hitIds[i * HitSlots + s] == enemyId) return true;
            return false;
        }

        void RememberHit(int i, int enemyId)
        {
            for (var s = HitSlots - 1; s > 0; s--) hitIds[i * HitSlots + s] = hitIds[i * HitSlots + s - 1];
            hitIds[i * HitSlots] = enemyId;
        }

        void RemoveAt(int i)
        {
            var last = --count;
            if (i == last) return;
            position[i] = position[last];
            velocity[i] = velocity[last];
            damage[i] = damage[last];
            life[i] = life[last];
            radius[i] = radius[last];
            pierce[i] = pierce[last];
            color[i] = color[last];
            for (var s = 0; s < HitSlots; s++) hitIds[i * HitSlots + s] = hitIds[last * HitSlots + s];
        }

        public void Draw(InstancedRenderer renderer)
        {
            // Bullet = glowing tracer streak trailing behind + the ball itself.
            for (var i = 0; i < count; i++)
            {
                var v = velocity[i];
                var speed = v.magnitude;
                var dir = speed > 0.001f ? v / speed : Vector2.right;
                var angle = Mathf.Atan2(dir.y, dir.x);
                var length = Mathf.Max(radius[i] * 2f, gun.TracerLength);
                renderer.Streak(RenderLayer.Projectiles, position[i] - dir * (length * 0.4f), length, gun.TracerWidth, angle, color[i]);
                if (sprites.MagicBolt != null)
                    renderer.Sprite(RenderLayer.Projectiles, sprites.MagicBolt, position[i], radius[i] * 2.4f, Color.white);
                else
                    renderer.Circle(RenderLayer.Projectiles, position[i], radius[i], new Color(0.25f, 0.25f, 0.3f));
            }
        }

        void Grow()
        {
            var size = position.Length * 2;
            Array.Resize(ref position, size);
            Array.Resize(ref velocity, size);
            Array.Resize(ref damage, size);
            Array.Resize(ref life, size);
            Array.Resize(ref radius, size);
            Array.Resize(ref pierce, size);
            Array.Resize(ref color, size);
            Array.Resize(ref hitIds, size * HitSlots);
        }
    }
}
