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

        public CompanionSystem(CompanionContext context, PlayerTuning tuning)
        {
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
        }
    }
}
