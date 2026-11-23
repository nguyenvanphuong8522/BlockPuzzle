using System.Collections.Generic;
using UnityEngine;

namespace Survivor
{
    // Companion #1: a monkey that throws bananas like boomerangs at the nearest enemy.
    public sealed class MonkeyCompanion : Companion
    {
        static readonly float[] Damage = { 12, 16, 20, 26, 34 };
        static readonly int[] Bananas = { 1, 1, 2, 2, 3 };
        static readonly float[] Cooldown = { 2.0f, 1.8f, 1.6f, 1.4f, 1.2f };
        static readonly float[] Range = { 5f, 5.5f, 6f, 6.5f, 7f };
        const float ThrowSpeed = 14f;
        const float SpreadDegrees = 18f;      // angle between bananas when it throws several
        const float SightRange = 9f;
        const float ThrowPoseTime = 0.25f;    // arm stays up this long after a throw

        readonly List<int> targets = new List<int>(1);
        float timer = 0.5f;
        float throwPose;
        bool facingLeft;

        public override string Name => "Khỉ Ném Chuối";
        public override string Description => "Ném chuối như boomerang: bay ra rồi quay về, gây sát thương cả đi lẫn về.";
        public override Color Tint => new Color(1f, 0.85f, 0.25f);
        public override string NextLevelDescription => (Level + 1) switch
        {
            2 => "Sát thương +33%, ném nhanh hơn.",
            3 => "Ném 2 quả chuối mỗi lần.",
            4 => "Sát thương cao hơn, chuối bay xa hơn.",
            _ => "Ném 3 quả chuối mỗi lần."
        };

        public override void Tick(float dt, CompanionContext ctx)
        {
            throwPose -= dt;
            timer -= dt;
            if (timer > 0f) return;

            ctx.Enemies.FindNearest(Position, SightRange, 1, targets);
            if (targets.Count == 0) return; // hold the throw until something is close

            timer = At(Cooldown) * ctx.Player.Stats.Haste;
            var aim = (ctx.Enemies.Position[targets[0]] - Position).normalized;
            facingLeft = aim.x < 0f;
            throwPose = ThrowPoseTime;

            var n = At(Bananas);
            for (var k = 0; k < n; k++)
            {
                var offset = (k - (n - 1) / 2f) * SpreadDegrees * Mathf.Deg2Rad;
                var c = Mathf.Cos(offset);
                var s = Mathf.Sin(offset);
                var dir = new Vector2(aim.x * c - aim.y * s, aim.x * s + aim.y * c);
                ctx.Boomerangs.Throw(this, dir, At(Range), ThrowSpeed, At(Damage) * ctx.Player.Stats.Might);
            }
            ctx.Audio.Play(Sfx.Throw, 0.6f, 0.1f, 0.05f);
        }

        public override void Draw(InstancedRenderer renderer, CompanionContext ctx)
        {
            const float radius = 0.32f;
            var feet = Position.y - radius;
            renderer.Ellipse(RenderLayer.Shadows, new Vector2(Position.x, feet + 0.05f), new Vector2(radius * 1.8f, radius * 0.55f),
                new Color(0f, 0f, 0f, 0.22f));

            var anim = ctx.Sprites.Monkey;
            if (anim != null && !anim.IsEmpty)
            {
                // Frame 0 = idle (with a small bob), frame 1 = arm up right after a throw.
                var frame = anim.Frames[throwPose > 0f && anim.Frames.Length > 1 ? 1 : 0];
                var bob = throwPose > 0f ? 0f : Mathf.Abs(Mathf.Sin(Time.time * 5f + Slot)) * 0.05f;
                renderer.Sprite(RenderLayer.World, frame, new Vector2(Position.x, Position.y + radius * 0.4f + bob),
                    radius * 2.6f, Color.white, flipX: facingLeft, sortY: feet);
            }
            else
            {
                renderer.Circle(RenderLayer.World, Position, radius, new Color(0.63f, 0.38f, 0.18f), 0f, feet);
            }
        }
    }
}
