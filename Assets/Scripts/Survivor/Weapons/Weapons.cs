using System.Collections.Generic;
using UnityEngine;

namespace Survivor
{
    // Starting weapon: magic bolts aimed at the nearest enemies.
    public sealed class MagicBoltWeapon : Weapon
    {
        static readonly float[] Damage = { 10, 14, 14, 18, 24 };
        static readonly int[] Bolts = { 1, 1, 2, 2, 3 };
        static readonly int[] Pierce = { 0, 0, 0, 1, 1 };
        static readonly float[] Cooldown = { 0.9f, 0.9f, 0.85f, 0.75f, 0.65f };
        const float Range = 10f;

        readonly List<int> targets = new List<int>(8);
        float timer;

        public override string Name => "Pháo Hải Tặc";
        public override string Description => "Bắn đạn pháo vào kẻ địch gần nhất.";
        public override Color Tint => new Color(0.5f, 0.8f, 1f);
        public override string NextLevelDescription => Level switch
        {
            1 => "Sát thương +40%.",
            2 => "Bắn thêm 1 viên.",
            3 => "Đạn xuyên qua 1 kẻ địch, bắn nhanh hơn.",
            _ => "Bắn thêm 1 viên, sát thương +33%."
        };

        public override void Tick(float dt, WeaponContext ctx)
        {
            timer -= dt;
            if (timer > 0f) return;

            ctx.Enemies.FindNearest(ctx.Player.Position, Range, At(Bolts), targets);
            if (targets.Count == 0) return; // keep the shot ready until something is in range

            ctx.Audio.Play(Sfx.Shoot, 0.5f, 0.08f, 0.06f);
            timer = At(Cooldown) * ctx.Haste;
            for (var k = 0; k < At(Bolts); k++)
            {
                var target = targets[k % targets.Count];
                var dir = ctx.Enemies.Position[target] - ctx.Player.Position;
                ctx.Projectiles.Spawn(ctx.Player.Position, dir, 12f, At(Damage) * ctx.Might, 1.2f, 0.18f, At(Pierce), Tint);
            }
        }
    }

    // Blades circling the player, hurting everything they touch.
    public sealed class OrbitBladesWeapon : Weapon
    {
        static readonly int[] Blades = { 2, 2, 3, 4, 5 };
        static readonly float[] Damage = { 6, 9, 11, 13, 16 };
        static readonly float[] OrbitRadius = { 1.6f, 1.6f, 1.8f, 1.9f, 2.1f };
        static readonly float[] DegreesPerSecond = { 200, 220, 240, 260, 300 };
        const float BladeRadius = 0.28f;
        const float HitInterval = 0.4f; // the same enemy can be hit again after this

        readonly List<int> hits = new List<int>(32);
        readonly Dictionary<int, float> nextHitTime = new Dictionary<int, float>();
        readonly List<int> expired = new List<int>();
        float angle;
        float time;

        public override string Name => "Mỏ Neo Xoay";
        public override string Description => "Mỏ neo xoay quanh bạn, đập văng kẻ địch.";
        public override Color Tint => new Color(0.9f, 0.9f, 1f);
        public override string NextLevelDescription => Level switch
        {
            1 => "Sát thương +50%.",
            2 => "Thêm 1 mỏ neo, vòng xoay rộng hơn.",
            3 => "Thêm 1 mỏ neo, xoay nhanh hơn.",
            _ => "Thêm 1 mỏ neo, sát thương +23%."
        };

        public override void Tick(float dt, WeaponContext ctx)
        {
            time += dt;
            angle += At(DegreesPerSecond) * dt;

            var count = At(Blades);
            for (var b = 0; b < count; b++)
            {
                var pos = BladePosition(ctx, b, count);
                ctx.Enemies.QueryRadius(pos, BladeRadius * ctx.Area, hits);
                foreach (var e in hits)
                {
                    var id = ctx.Enemies.Id[e];
                    if (nextHitTime.TryGetValue(id, out var t) && t > time) continue;
                    nextHitTime[id] = time + HitInterval;
                    ctx.Enemies.Damage(e, At(Damage) * ctx.Might, (ctx.Enemies.Position[e] - ctx.Player.Position).normalized, 3f);
                }
            }

            // Forget enemies we have not touched for a while (also covers dead ones).
            if (nextHitTime.Count > 512)
            {
                expired.Clear();
                foreach (var pair in nextHitTime)
                    if (pair.Value < time) expired.Add(pair.Key);
                foreach (var id in expired) nextHitTime.Remove(id);
            }
        }

        Vector2 BladePosition(WeaponContext ctx, int index, int count)
        {
            var a = (angle + index * 360f / count) * Mathf.Deg2Rad;
            return ctx.Player.Position + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * At(OrbitRadius) * ctx.Area;
        }

        public override void Draw(InstancedRenderer renderer, WeaponContext ctx)
        {
            var count = At(Blades);
            for (var b = 0; b < count; b++)
            {
                var pos = BladePosition(ctx, b, count);
                if (ctx.Sprites.OrbitBlade != null)
                    renderer.Sprite(RenderLayer.Weapons, ctx.Sprites.OrbitBlade, pos, BladeRadius * 2.6f * ctx.Area, Color.white);
                else
                    renderer.Circle(RenderLayer.Weapons, pos, BladeRadius * ctx.Area, Tint);
            }
        }
    }

    // Periodic fire wave around the player that knocks enemies back.
    public sealed class NovaPulseWeapon : Weapon
    {
        static readonly float[] Cooldown = { 3f, 2.8f, 2.5f, 2.2f, 1.9f };
        static readonly float[] Radius = { 2.2f, 2.5f, 2.8f, 3.1f, 3.5f };
        static readonly float[] Damage = { 12, 16, 20, 26, 34 };

        readonly List<int> hits = new List<int>(256);
        float timer = 1f;

        public override string Name => "Sóng Thần";
        public override string Description => "Định kỳ tạo sóng nước đẩy lùi kẻ địch xung quanh.";
        public override Color Tint => new Color(0.3f, 0.8f, 1f);
        public override string NextLevelDescription => "Phạm vi rộng hơn, sát thương cao hơn, hồi nhanh hơn.";

        public override void Tick(float dt, WeaponContext ctx)
        {
            timer -= dt;
            if (timer > 0f) return;
            timer = At(Cooldown) * ctx.Haste;

            var radius = At(Radius) * ctx.Area;
            var center = ctx.Player.Position;
            ctx.Enemies.QueryRadius(center, radius, hits);
            foreach (var e in hits)
                ctx.Enemies.Damage(e, At(Damage) * ctx.Might, (ctx.Enemies.Position[e] - center).normalized, 9f);
            ctx.Effects.Burst(center, radius, Tint, 0.3f);
            ctx.Audio.Play(Sfx.Wave, 0.8f);
        }
    }

    // Lightning strikes random enemies on screen, damaging a small area.
    public sealed class LightningWeapon : Weapon
    {
        static readonly float[] Cooldown = { 2f, 1.8f, 1.6f, 1.4f, 1.2f };
        static readonly int[] Strikes = { 1, 2, 2, 3, 4 };
        static readonly float[] Damage = { 20, 26, 32, 40, 50 };
        const float SplashRadius = 0.8f;
        const float Range = 9f;

        readonly List<int> targets = new List<int>(8);
        readonly List<int> hits = new List<int>(32);
        float timer = 0.5f;

        public override string Name => "Sét Biển";
        public override string Description => "Sét đánh ngẫu nhiên vào kẻ địch quanh bạn.";
        public override Color Tint => new Color(1f, 0.95f, 0.4f);
        public override string NextLevelDescription => "Thêm tia sét hoặc sát thương cao hơn, hồi nhanh hơn.";

        public override void Tick(float dt, WeaponContext ctx)
        {
            timer -= dt;
            if (timer > 0f) return;

            ctx.Enemies.PickRandom(ctx.Player.Position, Range, At(Strikes), targets);
            if (targets.Count == 0) return;
            ctx.Audio.Play(Sfx.Lightning, 0.7f, 0.1f);
            timer = At(Cooldown) * ctx.Haste;

            foreach (var t in targets)
            {
                var point = ctx.Enemies.Position[t];
                ctx.Enemies.QueryRadius(point, SplashRadius * ctx.Area, hits);
                foreach (var e in hits) ctx.Enemies.Damage(e, At(Damage) * ctx.Might);
                ctx.Effects.Bolt(point, Tint);
            }
        }
    }
}
