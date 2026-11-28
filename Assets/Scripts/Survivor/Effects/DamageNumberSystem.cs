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
        readonly float[] scale = new float[MaxAlive];  // size, from the hit enemy type / boss / hero settings
        readonly Color[] color = new Color[MaxAlive];
        const byte KindNormal = 0, KindBig = 1, KindHurt = 2;

        // Shared numbers (fly speed, lifetime, hero damage...) come from PlayerTuning; size, height, spread and color
        // per enemy type (<Id>_Settings "Số sát thương") and per boss (<Id>_Settings). All live-tunable.
        readonly PlayerTuning tuning;
        readonly BossRoster roster;
        readonly GameConfig config;

        public DamageNumberSystem(DebugSettings debug, PlayerTuning tuning, BossRoster roster, GameConfig config)
        {
            this.debug = debug;
            this.tuning = tuning;
            this.roster = roster;
            this.config = config;
        }

        // Damage dealt to an enemy of `type` (index in GameConfig.Enemies; -1 = unknown: shared look).
        public void Spawn(Vector2 at, float amount, bool isBig = false, int type = -1)
        {
            if (!debug.ShowDamageNumbers) return;
            if (isBig)
            {
                var b = (type >= 0 ? roster.FindType(type) : null)?.Settings ?? roster.Settings;
                if (b != null)
                {
                    Add(at, amount, KindBig, b.NumberHeight, b.NumberSpread, b.NumberScale, b.NumberColor);
                    return;
                }
            }
            var s = type >= 0 && type < config.Enemies.Length && !config.Enemies[type].IsBoss ? config.Enemies[type].Settings : null;
            if (s != null)
                Add(at, amount, KindNormal, s.NumberHeight, s.NumberSpread, tuning.EnemyNumberScale * s.NumberScale,
                    s.OwnNumberColor ? s.NumberColor : tuning.EnemyNumberColor);
            else
                Add(at, amount, KindNormal, 0f, 1f, tuning.EnemyNumberScale, tuning.EnemyNumberColor);
        }

        // Damage the hero took. Always shown (even with enemy damage numbers turned off).
        public void SpawnHurt(Vector2 at, float amount) =>
            Add(at + new Vector2(0f, 0.3f), amount, KindHurt, 0f, 1f, tuning.HurtNumberScale, tuning.HurtNumberColor);

        void Add(Vector2 at, float amount, byte numberKind, float height, float spread, float size, Color tint)
        {
            if (count >= MaxAlive) return;
            var i = count++;
            // Appear somewhere in the fan above the hit and fly outward along the same direction.
            var half = tuning.NumberSpreadDegrees * 0.5f;
            var angle = (90f + UnityEngine.Random.Range(-half, half)) * Mathf.Deg2Rad;
            var dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            var dist = tuning.NumberSpawnRadius * spread * Mathf.Sqrt(UnityEngine.Random.value); // even spread over the area
            position[i] = at + new Vector2(0f, tuning.NumberStartLift + height) + dir * dist;
            velocity[i] = dir * tuning.NumberFlySpeed * (numberKind == KindHurt ? tuning.HurtNumberSpeedScale : 1f)
                          * UnityEngine.Random.Range(0.85f, 1.15f);
            value[i] = Mathf.Max(1, Mathf.RoundToInt(amount));
            age[i] = 0f;
            kind[i] = numberKind;
            scale[i] = size;
            color[i] = tint;
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
                scale[i] = scale[last];
                color[i] = color[last];
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
                var height = tuning.NumberBaseHeight * scale[i] * (t < 0.15f ? 1f + (0.15f - t) * 3f : 1f); // small pop-in
                var width = height * aspect * 0.8f; // letters overlap their outline a little
                var tint = color[i];
                tint.a *= t < 0.7f ? 1f : 1f - (t - 0.7f) / 0.3f;

                var startX = position[i].x - (n - 1) * width / 2f;
                for (var k = 0; k < n; k++)
                {
                    var digit = digits[n - 1 - k];
                    renderer.Draw(brush, DigitFont.Uv(digit),
                        new Vector2(startX + k * width, position[i].y), new Vector2(height * aspect, height), tint);
                }
            }
        }
    }
}
