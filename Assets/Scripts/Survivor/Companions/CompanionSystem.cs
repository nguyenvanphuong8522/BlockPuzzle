using System;
using System.Collections.Generic;
using UnityEngine;

namespace Survivor
{
    // Holds up to 6 companions in slots around the player. They are attached to the hero like children:
    // each one stays exactly at its slot next to the player and always follows.
    public sealed class CompanionSystem
    {
        public const int MaxSlots = 6;
        const float FormationRadius = 1.25f;

        readonly CompanionContext context;
        readonly List<Companion> companions = new List<Companion>();

        public IReadOnlyList<Companion> Companions => companions;
        public bool HasFreeSlot => companions.Count < MaxSlots;

        public CompanionSystem(CompanionContext context) => this.context = context;

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
            companion.Position = SlotPosition(companion.Slot);
            companions.Add(companion);
            context.Effects.Ring(companion.Position, 0.9f, companion.Tint, 0.35f); // "summon" pop
        }

        // Slots on a ring around the player, first one bottom-right, then going around.
        Vector2 SlotPosition(int slot)
        {
            var angle = (-30f + slot * 360f / MaxSlots) * Mathf.Deg2Rad;
            return context.Player.Position + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle) * 0.8f) * FormationRadius;
        }

        public void Tick(float dt)
        {
            foreach (var c in companions)
            {
                c.Position = SlotPosition(c.Slot); // locked to the hero
                c.Tick(dt, context);
            }
        }

        public void Draw(InstancedRenderer renderer)
        {
            foreach (var c in companions) c.Draw(renderer, context);
        }
    }
}
