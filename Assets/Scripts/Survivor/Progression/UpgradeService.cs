using System;
using System.Collections.Generic;
using UnityEngine;

namespace Survivor
{
    public sealed class UpgradeOption
    {
        public string Title;
        public string Badge;       // "MỚI!" or "Cấp 3"
        public string Description;
        public Color Tint;
        public Action Apply;
    }

    // Builds the choices shown on level-up (and when opening a chest). Every choice is a companion:
    // recruit a new one into a free slot (up to 6), or level up one you already have.
    public sealed class UpgradeService
    {
        // Add new companion types here (one line each); they appear in level-up choices automatically.
        static readonly Func<Companion>[] CompanionFactories =
        {
            () => new MonkeyCompanion(),
            () => new SwordOtterCompanion(),
            () => new TurtleMageCompanion(),
        };

        // Still used by debug tools; the player's own gun is the only weapon in normal play.
        static readonly Func<Weapon>[] WeaponFactories =
        {
            () => new MagicBoltWeapon(),
            () => new OrbitBladesWeapon(),
            () => new NovaPulseWeapon(),
            () => new LightningWeapon(),
        };

        readonly WeaponSystem weapons;
        readonly CompanionSystem companions;
        readonly Player player;
        readonly List<UpgradeOption> candidates = new List<UpgradeOption>();

        public UpgradeService(WeaponSystem weapons, CompanionSystem companions, Player player)
        {
            this.weapons = weapons;
            this.companions = companions;
            this.player = player;
        }

        // The hero's gun.
        public void GiveStartingWeapon() => weapons.Add(new MagicBoltWeapon());

        // Debug: every weapon at max level.
        public void GiveAllWeaponsMaxed()
        {
            foreach (var factory in WeaponFactories)
            {
                var sample = factory();
                var weapon = weapons.Find(sample.GetType());
                if (weapon == null)
                {
                    weapon = sample;
                    weapons.Add(weapon);
                }
                while (!weapon.IsMaxLevel) weapon.LevelUp();
            }
        }

        // Debug: recruit every companion type and max it.
        public void GiveAllCompanionsMaxed()
        {
            foreach (var factory in CompanionFactories)
            {
                var sample = factory();
                var companion = companions.Find(sample.GetType());
                if (companion == null)
                {
                    if (!companions.HasFreeSlot) continue;
                    companion = sample;
                    companions.Add(companion);
                }
                while (!companion.IsMaxLevel) companion.LevelUp();
            }
        }

        public List<UpgradeOption> Roll(int count)
        {
            candidates.Clear();

            foreach (var factory in CompanionFactories)
            {
                var sample = factory();
                var owned = companions.Find(sample.GetType());
                if (owned == null && companions.HasFreeSlot)
                {
                    candidates.Add(new UpgradeOption
                    {
                        Title = sample.Name, Badge = "MỚI!", Description = sample.Description, Tint = sample.Tint,
                        Apply = () => companions.Add(sample)
                    });
                }
                else if (owned != null && !owned.IsMaxLevel)
                {
                    candidates.Add(new UpgradeOption
                    {
                        Title = owned.Name, Badge = $"Cấp {owned.Level + 1}", Description = owned.NextLevelDescription,
                        Tint = owned.Tint, Apply = owned.LevelUp
                    });
                }
            }

            // Shuffle and keep the first `count`.
            for (var i = candidates.Count - 1; i > 0; i--)
            {
                var j = UnityEngine.Random.Range(0, i + 1);
                (candidates[i], candidates[j]) = (candidates[j], candidates[i]);
            }
            var result = candidates.GetRange(0, Mathf.Min(count, candidates.Count));

            // Nothing left to recruit or level up: offer healing instead.
            if (result.Count == 0)
            {
                result.Add(new UpgradeOption
                {
                    Title = "Hồi Phục", Badge = "", Description = "Hồi 30 máu.", Tint = new Color(1f, 0.4f, 0.4f),
                    Apply = () => player.Heal(30f)
                });
            }
            return result;
        }
    }
}
