using System;
using UnityEngine;

namespace Survivor
{
    // Floating damage numbers. Capped, because area weapons can hit hundreds of enemies in one frame.
    public sealed class DamageNumberSystem
    {
        const int MaxAlive = 400;

        readonly DebugSettings debug;

        int count;
        readonly Vector2[] position = new Vector2[MaxAlive];
        readonly Vector2[] velocity = new Vector2[MaxAlive];
        readonly int[] value = new int[MaxAlive];
        readonly float[] age = new float[MaxAlive];
        readonly byte[] kind = new byte[MaxAlive]; // KindNormal, KindBig (boss), KindHurt (the hero lost HP)
        const byte KindNormal = 0, KindBig = 1, KindHurt = 2;

        // Hero damage color and size come from PlayerTuning (Assets/Resources/PlayerTuning.asset), live-tunable.
        readonly PlayerTuning tuning;
        readonly BossSettings boss;

        public DamageNumberSystem(DebugSettings debug, PlayerTuning tuning, BossSettings boss)
        {
            this.debug = debug;
            this.tuning = tuning;
            this.boss = boss;
        }

        public void Spawn(Vector2 at, float amount, bool isBig = false)
        {
            if (!debug.ShowDamageNumbers) return;
            Add(at, amount, isBig ? KindBig : KindNormal);
        }

        // Damage the hero took. Always shown (even with enemy damage numbers turned off).
        public void SpawnHurt(Vector2 at, float amount) => Add(at + new Vector2(0f, 0.3f), amount, KindHurt);

        void Add(Vector2 at, float amount, byte numberKind)
        {
            if (count >= MaxAlive) return;
            var i = count++;
            // Appear somewhere in the fan above the hit and fly outward along the same direction.
            var half = tuning.NumberSpreadDegrees * 0.5f;
            var angle = (90f + UnityEngine.Random.Range(-half, half)) * Mathf.Deg2Rad;
            var dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            var dist = tuning.NumberSpawnRadius * Mathf.Sqrt(UnityEngine.Random.value); // even spread over the area
            position[i] = at + new Vector2(0f, tuning.NumberStartLift) + dir * dist;
            velocity[i] = dir * tuning.NumberFlySpeed * (numberKind == KindHurt ? tuning.HurtNumberSpeedScale : 1f)
                          * UnityEngine.Random.Range(0.85f, 1.15f);
            value[i] = Mathf.Max(1, Mathf.RoundToInt(amount));
            age[i] = 0f;
            kind[i] = numberKind;
        }

        public void Tick(float dt)
        {
            var lifetime = tuning.NumberLifetime;
            var slow = tuning.NumberFlyDrag > 0f ? Mathf.Exp(-tuning.NumberFlyDrag * dt) : 1f;
            for (var i = count - 1; i >= 0; i--)
            {
                age[i] += dt;
                position[i] += velocity[i] * dt;
                velocity[i] *= slow;
                if (age[i] < lifetime) continue;
                var last = --count;
                position[i] = position[last];
                velocity[i] = velocity[last];
                value[i] = value[last];
                age[i] = age[last];
                kind[i] = kind[last];
            }
        }

        public void Draw(InstancedRenderer renderer)
        {
            Span<int> digits = stackalloc int[10];
            // Looked up once per frame, not once per digit (texture size is a call into the engine).
            var brush = renderer.Brush(RenderLayer.Text, DigitFont.Texture);
            var aspect = DigitFont.Aspect;
            for (var i = 0; i < count; i++)
            {
                var n = 0;
                var v = value[i];
                do
                {
                    digits[n++] = v % 10;
                    v /= 10;
                } while (v > 0 && n < digits.Length);

                var t = age[i] / Mathf.Max(0.01f, tuning.NumberLifetime);
                var scale = kind[i] == KindHurt ? tuning.HurtNumberScale : kind[i] == KindBig ? boss.NumberScale : tuning.EnemyNumberScale;
                var height = tuning.NumberBaseHeight * scale * (t < 0.15f ? 1f + (0.15f - t) * 3f : 1f); // small pop-in
                var width = height * aspect * 0.8f; // letters overlap their outline a little
                var color = kind[i] == KindHurt ? tuning.HurtNumberColor : kind[i] == KindBig ? boss.NumberColor : tuning.EnemyNumberColor;
                color.a = t < 0.7f ? 1f : 1f - (t - 0.7f) / 0.3f;

                var startX = position[i].x - (n - 1) * width / 2f;
                for (var k = 0; k < n; k++)
                {
                    var digit = digits[n - 1 - k];
                    renderer.Draw(brush, DigitFont.Uv(digit),
                        new Vector2(startX + k * width, position[i].y), new Vector2(height * aspect, height), color);
                }
            }
        }
    }
}
