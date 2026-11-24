using System;
using UnityEngine;

namespace Survivor
{
    // Floating damage numbers. Capped, because area weapons can hit hundreds of enemies in one frame.
    public sealed class DamageNumberSystem
    {
        const int MaxAlive = 400;
        const float Lifetime = 0.6f;

        readonly DebugSettings debug;

        int count;
        readonly Vector2[] position = new Vector2[MaxAlive];
        readonly int[] value = new int[MaxAlive];
        readonly float[] age = new float[MaxAlive];
        readonly byte[] kind = new byte[MaxAlive]; // KindNormal, KindBig (boss), KindHurt (the hero lost HP)
        const byte KindNormal = 0, KindBig = 1, KindHurt = 2;

        // Hero damage color and size come from PlayerTuning (Assets/Resources/PlayerTuning.asset), live-tunable.
        readonly PlayerTuning tuning;

        public DamageNumberSystem(DebugSettings debug, PlayerTuning tuning)
        {
            this.debug = debug;
            this.tuning = tuning;
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
            position[i] = at + new Vector2(UnityEngine.Random.Range(-0.2f, 0.2f), 0.3f);
            value[i] = Mathf.Max(1, Mathf.RoundToInt(amount));
            age[i] = 0f;
            kind[i] = numberKind;
        }

        public void Tick(float dt)
        {
            for (var i = count - 1; i >= 0; i--)
            {
                age[i] += dt;
                position[i] += new Vector2(0f, (kind[i] == KindHurt ? 0.8f : 1.2f) * dt);
                if (age[i] < Lifetime) continue;
                var last = --count;
                position[i] = position[last];
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

                var t = age[i] / Lifetime;
                var scale = kind[i] == KindHurt ? tuning.HurtNumberScale : kind[i] == KindBig ? tuning.BossNumberScale : tuning.EnemyNumberScale;
                var height = tuning.NumberBaseHeight * scale * (t < 0.15f ? 1f + (0.15f - t) * 3f : 1f); // small pop-in
                var width = height * aspect * 0.8f; // letters overlap their outline a little
                var color = kind[i] == KindHurt ? tuning.HurtNumberColor : kind[i] == KindBig ? tuning.BossNumberColor : tuning.EnemyNumberColor;
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
