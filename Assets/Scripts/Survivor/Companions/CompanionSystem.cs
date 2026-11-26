using System;
using System.Collections.Generic;
using UnityEngine;

namespace Survivor
{
    // Holds up to 6 companions spread evenly on a circle around the player: 1 stands above the hero's head,
    // 2 = above and below, 3 = every 120 degrees, and so on. They are attached to the hero like children and
    // always follow. Distance is set in PlayerTuning (Assets/Resources/PlayerTuning.asset).
    public sealed class CompanionSystem
    {
        public const int MaxSlots = 6;

        readonly CompanionContext context;
        readonly PlayerTuning tuning;
        readonly List<Companion> companions = new List<Companion>();
        readonly List<Vector2> offsets = new List<Vector2>(); // current offset from the hero, eased toward the target

        public IReadOnlyList<Companion> Companions => companions;
        public bool HasFreeSlot => companions.Count < MaxSlots;

        readonly DebugSettings debug;

        public CompanionSystem(CompanionContext context, PlayerTuning tuning, DebugSettings debug)
        {
            this.debug = debug;
            this.context = context;
            this.tuning = tuning;
        }

        public Companion Find(Type type)
        {
            foreach (var c in companions)
                if (c.GetType() == type) return c;
            return null;
        }

        public void Add(Companion companion)
        {
            if (!HasFreeSlot) return;
            companion.Slot = companions.Count;
            companions.Add(companion);
            // The newcomer appears right at its place; the others slide over to make room.
            offsets.Add(TargetOffset(companion.Slot));
            companion.Home = context.Player.Position + offsets[companion.Slot];
            companion.Position = companion.Home;
            context.Effects.Ring(companion.Position, 0.9f, companion.Tint, 0.35f); // "summon" pop
        }

        // Fusion: `replacement` takes the place (slot) of `old`, which leaves.
        public void Replace(Companion old, Companion replacement)
        {
            var i = companions.IndexOf(old);
            if (i < 0)
            {
                Add(replacement);
                return;
            }
            context.Boomerangs.RemoveOwnedBy(old);
            replacement.Slot = i;
            companions[i] = replacement;
            replacement.Home = context.Player.Position + offsets[i];
            replacement.Position = replacement.Home;
            context.Effects.Ring(replacement.Position, 1.3f, replacement.Tint, 0.45f);
            context.Effects.Burst(replacement.Position, 1.0f, replacement.Tint, 0.35f);
        }

        // A companion leaves (used up by a fusion); the rest close the gap.
        public void Remove(Companion companion)
        {
            var i = companions.IndexOf(companion);
            if (i < 0) return;
            context.Boomerangs.RemoveOwnedBy(companion);
            context.Effects.Ring(companion.Position, 0.9f, companion.Tint, 0.35f);
            companions.RemoveAt(i);
            offsets.RemoveAt(i);
            for (var k = 0; k < companions.Count; k++) companions[k].Slot = k;
        }

        // Evenly spaced on the circle, starting straight above the hero.
        Vector2 TargetOffset(int slot)
        {
            var angle = (90f + slot * 360f / companions.Count) * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle) * tuning.CompanionCircleSquash) * tuning.CompanionDistance;
        }

        public void Tick(float dt)
        {
            var ease = 1f - Mathf.Exp(-tuning.CompanionRegroupSpeed * dt);
            for (var i = 0; i < companions.Count; i++)
            {
                var c = companions[i];
                offsets[i] = Vector2.Lerp(offsets[i], TargetOffset(c.Slot), ease);
                c.Home = context.Player.Position + offsets[i];
                if (c.StaysHome) c.Position = c.Home; // locked to the hero
                c.Tick(dt, context);
            }
        }

        public void Draw(InstancedRenderer renderer)
        {
            foreach (var c in companions) c.Draw(renderer, context);
            if (debug.ShowCompanionRange) DrawSightRanges(renderer);
            if (debug.ShowAttackArea)
                foreach (var c in companions) c.DrawAttackArea(renderer, context);
        }

        // Debug: one ring per companion in its own color, centered on the companion itself,
        // plus a small ring of the same color around it so you can tell which is which.
        void DrawSightRanges(InstancedRenderer renderer)
        {
            foreach (var c in companions)
            {
                var center = c.SightCenter(context);
                var r = c.SightRadius(context);
                var line = c.Tint;
                line.a = 0.9f;
                var fill = c.Tint;
                fill.a = 0.04f;
                renderer.Circle(RenderLayer.Shadows, center, r, fill);
                renderer.Ring(RenderLayer.Overlay, center, r, 0.05f, line);
                renderer.Ring(RenderLayer.Overlay, c.Position, 0.55f, 0.06f, line);
            }
        }
    }
}
