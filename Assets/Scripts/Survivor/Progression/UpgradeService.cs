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

    // Builds the 3 random choices shown on level-up: new weapons, weapon levels and passive bonuses.
    public sealed class UpgradeService
    {
        sealed class Passive
        {
            public string Name;
            public string Description;
            public Color Tint;
            public Action<Player> Apply;
            public int Level;
            public const int MaxLevel = 5;
        }

        static readonly Func<Weapon>[] WeaponFactories =
        {
            () => new MagicBoltWeapon(),
            () => new OrbitBladesWeapon(),
            () => new NovaPulseWeapon(),
            () => new LightningWeapon(),
        };

        readonly WeaponSystem weapons;
        readonly Player player;
        readonly List<UpgradeOption> candidates = new List<UpgradeOption>();
        readonly Passive[] passives =
        {
            new Passive { Name = "Giày Gió", Description = "Tốc độ chạy +10%.", Tint = new Color(0.5f, 1f, 0.8f),
                Apply = p => p.Stats.MoveSpeedMultiplier *= 1.1f },
            new Passive { Name = "Tim Thép", Description = "Máu tối đa +20 và hồi 20 máu.", Tint = new Color(1f, 0.4f, 0.4f),
                Apply = p => p.IncreaseMaxHp(20f) },
            new Passive { Name = "Nam Châm", Description = "Tầm hút kinh nghiệm +30%.", Tint = new Color(0.4f, 0.7f, 1f),
                Apply = p => p.Stats.MagnetMultiplier *= 1.3f },
            new Passive { Name = "Sức Mạnh", Description = "Sát thương +10%.", Tint = new Color(1f, 0.6f, 0.2f),
                Apply = p => p.Stats.BonusMight += 0.1f },
            new Passive { Name = "Đồng Hồ Cát", Description = "Hồi chiêu nhanh hơn 8%.", Tint = new Color(0.9f, 0.85f, 0.4f),
                Apply = p => p.Stats.HasteMultiplier *= 0.92f },
            new Passive { Name = "Bùa Rộng", Description = "Phạm vi vũ khí +10%.", Tint = new Color(0.8f, 0.5f, 1f),
                Apply = p => p.Stats.BonusArea += 0.1f },
        };

        public UpgradeService(WeaponSystem weapons, Player player)
        {
            this.weapons = weapons;
            this.player = player;
        }

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

        public List<UpgradeOption> Roll(int count)
        {
            candidates.Clear();

            foreach (var factory in WeaponFactories)
            {
                var sample = factory();
                var owned = weapons.Find(sample.GetType());
                if (owned == null && weapons.Weapons.Count < WeaponSystem.MaxWeapons)
                {
                    candidates.Add(new UpgradeOption
                    {
                        Title = sample.Name, Badge = "MỚI!", Description = sample.Description, Tint = sample.Tint,
                        Apply = () => weapons.Add(sample)
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

            foreach (var passive in passives)
            {
                if (passive.Level >= Passive.MaxLevel) continue;
                var p = passive;
                candidates.Add(new UpgradeOption
                {
                    Title = p.Name, Badge = p.Level == 0 ? "MỚI!" : $"Cấp {p.Level + 1}", Description = p.Description,
                    Tint = p.Tint,
                    Apply = () =>
                    {
                        p.Level++;
                        p.Apply(player);
                    }
                });
            }

            // Shuffle and keep the first `count`.
            for (var i = candidates.Count - 1; i > 0; i--)
            {
                var j = UnityEngine.Random.Range(0, i + 1);
                (candidates[i], candidates[j]) = (candidates[j], candidates[i]);
            }
            var result = candidates.GetRange(0, Mathf.Min(count, candidates.Count));

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
