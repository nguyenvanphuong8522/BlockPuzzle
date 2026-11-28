using System.Collections.Generic;
using UnityEngine;

namespace Survivor
{
    // Companion #1: a monkey that throws bananas like boomerangs at the nearest enemy.
    public sealed class MonkeyCompanion : Companion
    {
        // Numbers and card text: Assets/Resources/Upgrades/Companions/Monkey.asset
        readonly MonkeyData data;
        public MonkeyCompanion(MonkeyData data) : base(data) => this.data = data;
        float[] Damage => data.Damage;
        int[] Bananas => data.Bananas;
        float[] Cooldown => data.Cooldown;
        float[] Range => data.Range;
        float ThrowSpeed => data.ThrowSpeed;
        float SpreadDegrees => data.SpreadDegrees;
        float SightRange => data.SightRange;
        const float ThrowPoseTime = 0.25f;    // arm stays up this long after a throw
        float Radius => 0.4f * Data.BodyScale; // drawn ~1.05 units tall at Body Scale 1; the hero is ~1.35
        const int IdleFrames = 4, ThrowFrame = 4, BlinkFrame = 5;
        const float IdleFps = 6f, BlinkEvery = 3.2f, BlinkTime = 0.13f;

        readonly List<int> targets = new List<int>(1);
        float timer = 0.5f;
        float throwPose;
        bool facingLeft;

        public override float SightRadius(CompanionContext ctx) => SightRange;

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
            var feet = Position.y - Radius;
            renderer.Ellipse(RenderLayer.Shadows, new Vector2(Position.x, feet + 0.05f), new Vector2(Radius * 1.8f, Radius * 0.55f),
                new Color(0f, 0f, 0f, 0.22f));

            var anim = ctx.Sprites.Monkey;
            if (anim != null && !anim.IsEmpty)
            {
                var frame = PickFrame(anim);
                renderer.Sprite(RenderLayer.World, frame, new Vector2(Position.x, Position.y + Radius * 0.4f),
                    Radius * 2.6f, Color.white, flipX: facingLeft, sortY: feet);
            }
            else
            {
                renderer.Circle(RenderLayer.World, Position, Radius, new Color(0.63f, 0.38f, 0.18f), 0f, feet);
            }
        }

        // Sprite frames: 0-3 idle loop, 4 = throw pose, 5 = blink.
        Sprite PickFrame(SpriteAnim anim)
        {
            var frames = anim.Frames;
            if (frames.Length < 6) return frames[throwPose > 0f && frames.Length > 1 ? frames.Length - 1 : 0]; // old 2-frame art
            if (throwPose > 0f) return frames[ThrowFrame];

            // Blink for a moment every few seconds; each slot blinks at its own time.
            var t = Time.time + Slot * 1.37f;
            if (t % BlinkEvery < BlinkTime) return frames[BlinkFrame];

            var i = (int)((Time.time + Slot * 0.3f) * IdleFps) % IdleFrames;
            return frames[i];
        }
    }
}
