using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Survivor
{
    public sealed class UpgradeOption
    {
        public string Title;
        public string Badge;       // "MỚI!" or "Cấp 3"
        public string Description;
        public Color Tint;
        public Sprite Icon;        // picture on the card (companion model / item icon), may be null
        public Action Apply;

        // Level stars under the picture: Stars filled out of MaxStars (0 = no stars). StarIsNew = the last filled
        // star is the one this pick gives (it blinks).
        public int Stars;
        public int MaxStars;
        public bool StarIsNew;
        public bool IsFusion;
        // "A (5 stars) + B (5 stars) = C" strip at the bottom of the card, null = none.
        public FusionRecipe Recipe;
    }

    public sealed class FusionRecipe
    {
        public Sprite[] Parts;     // ingredient pictures
        public int[] PartStars;    // their level (filled stars) counting this pick
        public int PartMaxStars;   // stars needed on each
        public Sprite Result;      // picture of the fusion
    }

    // Builds the choices shown on level-up (and when opening a chest).
    // Companions: one asset each in Assets/Resources/Upgrades/Companions (text, icon, every number).
    // Items: one asset each in Assets/Resources/Upgrades/Items. Every Nth level-up shows items (PlayerTuning).
    public sealed class UpgradeService
    {
        public const string CompanionFolder = "Upgrades/Companions";
        public const string ItemFolder = "Upgrades/Items";

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
        readonly PlayerTuning tuning;
        readonly SpriteCatalog sprites;
        readonly List<UpgradeOption> candidates = new List<UpgradeOption>();
        readonly CompanionData[] companionData;
        readonly ItemData[] itemData;
        readonly Dictionary<ItemData, int> itemPicks = new Dictionary<ItemData, int>();

        public IReadOnlyList<CompanionData> AllCompanions => companionData;
        public IReadOnlyList<ItemData> AllItems => itemData;

        public UpgradeService(WeaponSystem weapons, CompanionSystem companions, Player player, PlayerTuning tuning,
            SpriteCatalog sprites)
        {
            this.weapons = weapons;
            this.companions = companions;
            this.player = player;
            this.tuning = tuning;
            this.sprites = sprites;
            companionData = Resources.LoadAll<CompanionData>(CompanionFolder)
                .OrderBy(d => d.SortOrder).ThenBy(d => d.name).ToArray();
            itemData = Resources.LoadAll<ItemData>(ItemFolder)
                .OrderBy(d => d.SortOrder).ThenBy(d => d.name).ToArray();
            if (companionData.Length == 0) Debug.LogWarning($"No companion assets in Resources/{CompanionFolder}.");
            if (itemData.Length == 0) Debug.LogWarning($"No item assets in Resources/{ItemFolder}.");
        }

        // The hero's gun.
        public void GiveStartingWeapon() => weapons.Add(new MagicBoltWeapon());

        Companion Owned(CompanionData data)
        {
            foreach (var c in companions.Companions)
                if (c.Data == data) return c;
            return null;
        }

        CompanionData DataFor(CompanionKind kind)
        {
            foreach (var d in companionData)
                if (d.Kind == kind) return d;
            return null;
        }

        // Companions the hero starts every run with: the "Đệ tử khởi đầu" list in PlayerTuning.
        // A kind listed twice is recruited once (the higher level wins).
        public void GiveStartingCompanions()
        {
            var list = tuning.StartingCompanions;
            if (list == null) return;
            foreach (var entry in list)
            {
                var data = DataFor(entry.Kind);
                if (data == null) continue;
                var companion = Owned(data);
                if (companion == null)
                {
                    if (!companions.HasFreeSlot) break;
                    companion = data.Create();
                    companions.Add(companion);
                }
                while (companion.Level < entry.Level && !companion.IsMaxLevel) companion.LevelUp();
            }
        }

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
            foreach (var data in companionData)
            {
                if (data.IsFusion) continue; // fusions come from the level-up screen once their parts are maxed
                var companion = Owned(data);
                if (companion == null)
                {
                    if (!companions.HasFreeSlot) continue;
                    companion = data.Create();
                    companions.Add(companion);
                }
                while (!companion.IsMaxLevel) companion.LevelUp();
            }
        }

        // Debug: recruit one companion, or +1 level if the hero already has it. A fusion (Wukong, Cowboy...) is
        // added as a new one each time, like fusing again. Returns what happened, for the debug panel.
        public string DebugGiveCompanion(CompanionData data)
        {
            if (data == null) return "";
            if (!data.IsFusion)
            {
                var owned = Owned(data);
                if (owned != null)
                {
                    if (owned.IsMaxLevel) return "đã cấp tối đa";
                    owned.LevelUp();
                    return $"lên cấp {owned.Level}";
                }
            }
            if (!companions.HasFreeSlot) return $"hết chỗ (tối đa {CompanionSystem.MaxSlots})";
            companions.Add(data.Create());
            return "đã thêm";
        }

        // Debug: the most recently added companion leaves (frees a slot).
        public void DebugRemoveLastCompanion()
        {
            var list = companions.Companions;
            if (list.Count > 0) companions.Remove(list[list.Count - 1]);
        }

        // Level of the owned companion of this type, 0 if none.
        public int DebugOwnedLevel(CompanionData data)
        {
            var owned = data != null ? Owned(data) : null;
            return owned != null ? owned.Level : 0;
        }

        // ---- Level-up screen: companions most of the time, items every Nth level-up (PlayerTuning) ----

        // levelUpNumber = 1 for the first level-up of the run, 2 for the second...
        public bool IsItemLevelUp(int levelUpNumber)
        {
            var every = tuning.ItemChoiceEvery;
            if (AnyFusionReady()) return false; // a fusion is waiting: this screen is sure to offer it
            return every > 0 && levelUpNumber % every == 0 && HasItemToOffer();
        }

        public List<UpgradeOption> RollLevelUp(int levelUpNumber, int count) =>
            IsItemLevelUp(levelUpNumber) ? RollItems(count) : Roll(count);

        int Picks(ItemData item) => itemPicks.TryGetValue(item, out var n) ? n : 0;

        bool HasItemToOffer()
        {
            foreach (var item in itemData)
                if (item.Enabled && Picks(item) < item.MaxPicks) return true;
            return false;
        }

        // Items: permanent stat boosts, each up to its Max Picks.
        public List<UpgradeOption> RollItems(int count)
        {
            candidates.Clear();
            foreach (var item in itemData)
            {
                if (!item.Enabled || Picks(item) >= item.MaxPicks) continue;
                var next = Picks(item) + 1;
                var picked = item;
                candidates.Add(new UpgradeOption
                {
                    Title = item.LocalizedName, Badge = next == 1 ? Loc.Get("card.new") : "", Tint = item.Tint, Icon = item.Icon,
                    Description = item.FormatDescription(),
                    Stars = next, MaxStars = item.MaxPicks <= 10 ? item.MaxPicks : 0, StarIsNew = true,
                    Apply = () => { itemPicks[picked] = Picks(picked) + 1; picked.Apply(player); }
                });
            }
            Shuffle(candidates);
            var result = candidates.GetRange(0, Mathf.Min(count, candidates.Count));
            if (result.Count == 0) return Roll(count); // every item maxed: companions instead
            return result;
        }

        static void Shuffle(List<UpgradeOption> list)
        {
            for (var i = list.Count - 1; i > 0; i--)
            {
                var j = UnityEngine.Random.Range(0, i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        // Card picture: the asset's Icon, or the companion's own model (first idle frame).
        Sprite IconFor(CompanionData data)
        {
            if (data.Icon != null) return data.Icon;
            var anim = data.Kind switch
            {
                CompanionKind.Monkey => sprites.Monkey,
                CompanionKind.SwordOtter => sprites.Otter,
                CompanionKind.TurtleMage => sprites.Turtle,
                CompanionKind.Gunner => sprites.Gunner,
                CompanionKind.SlingFrog => sprites.Frog,
                CompanionKind.PigMace => sprites.Pig,
                CompanionKind.Wukong => sprites.Wukong,
                CompanionKind.BearRifle => sprites.Bear,
                CompanionKind.Cowboy => sprites.Cowboy,
                CompanionKind.Monk => sprites.Monk,
                CompanionKind.ChiPheo => sprites.Drunk,
                _ => null
            };
            return anim != null && anim.Frames != null && anim.Frames.Length > 0 ? anim.Frames[0] : null;
        }

        // ---- Fusion: a companion whose asset lists "Fusion Of" (e.g. monkey king = monkey + otter) ----

        // Every ingredient owned at max level and room for it. You may own the fusion already: rebuy and max the
        // ingredients again to fuse another one.
        bool FusionReady(CompanionData fusion)
        {
            if (!fusion.IsFusion || !fusion.Enabled) return false;
            foreach (var part in fusion.FusionOf)
            {
                var owned = part != null ? Owned(part) : null;
                if (owned == null || !owned.IsMaxLevel) return false;
            }
            return fusion.ConsumeIngredients || companions.HasFreeSlot;
        }

        public bool AnyFusionReady()
        {
            foreach (var d in companionData)
                if (FusionReady(d)) return true;
            return false;
        }

        // The first fusion that uses this companion.
        CompanionData FusionUsing(CompanionData data)
        {
            foreach (var d in companionData)
                if (d.IsFusion && d.Enabled && Array.IndexOf(d.FusionOf, data) >= 0) return d;
            return null;
        }

        // "A (5 stars) + B (5 stars) = fusion" strip: the recipe, so every part shows the level it must reach
        // (all stars full), not the level you have now. `picked` / `pickedLevel` are kept for callers, unused.
        FusionRecipe RecipeFor(CompanionData fusion, CompanionData picked, int pickedLevel)
        {
            var n = fusion.FusionOf.Length;
            var recipe = new FusionRecipe
            {
                Parts = new Sprite[n], PartStars = new int[n], PartMaxStars = Companion.MaxLevel, Result = IconFor(fusion)
            };
            for (var i = 0; i < n; i++)
            {
                var part = fusion.FusionOf[i];
                if (part == null) continue;
                recipe.Parts[i] = IconFor(part);
                recipe.PartStars[i] = part.MaxLevel;
            }
            return recipe;
        }

        UpgradeOption FusionOption(CompanionData fusion) => new UpgradeOption
        {
            Title = fusion.LocalizedName, Badge = Loc.Get("card.fusion"), Description = fusion.LocalizedDescription, Tint = fusion.Tint,
            Icon = IconFor(fusion), Stars = 1, MaxStars = fusion.MaxLevel, StarIsNew = true, IsFusion = true,
            Recipe = RecipeFor(fusion, null, 0),
            Apply = () => Fuse(fusion)
        };

        // The fusion takes the first ingredient's place; the other ingredients leave (unless Consume is off).
        void Fuse(CompanionData fusion)
        {
            var made = fusion.Create();
            if (!fusion.ConsumeIngredients)
            {
                companions.Add(made);
                return;
            }
            Companion first = null;
            foreach (var part in fusion.FusionOf)
            {
                var owned = part != null ? Owned(part) : null;
                if (owned == null) continue;
                if (first == null) first = owned;
                else companions.Remove(owned);
            }
            if (first != null) companions.Replace(first, made);
            else companions.Add(made);
        }

        // Companion choices (also used by chests). A ready fusion is always on the screen.
        public List<UpgradeOption> Roll(int count)
        {
            candidates.Clear();
            var result = new List<UpgradeOption>(count);
            foreach (var data in companionData)
                if (result.Count < count && FusionReady(data)) result.Add(FusionOption(data));

            foreach (var data in companionData)
            {
                var owned = Owned(data);
                if (owned == null && data.Enabled && !data.IsFusion && companions.HasFreeSlot)
                {
                    var d = data;
                    var fusion = FusionUsing(d);
                    candidates.Add(new UpgradeOption
                    {
                        Title = d.LocalizedName, Badge = Loc.Get("card.new"), Description = d.LocalizedDescription, Tint = d.Tint, Icon = IconFor(d),
                        Stars = 1, MaxStars = d.MaxLevel, StarIsNew = true,
                        Recipe = fusion != null ? RecipeFor(fusion, d, 1) : null,
                        Apply = () => companions.Add(d.Create())
                    });
                }
                else if (owned != null && !owned.IsMaxLevel)
                {
                    var fusion = FusionUsing(data);
                    candidates.Add(new UpgradeOption
                    {
                        Title = owned.Name, Badge = "", Description = owned.NextLevelDescription,
                        Tint = owned.Tint, Icon = IconFor(data), Apply = owned.LevelUp,
                        Stars = owned.Level + 1, MaxStars = data.MaxLevel, StarIsNew = true,
                        Recipe = fusion != null ? RecipeFor(fusion, data, owned.Level + 1) : null
                    });
                }
            }

            Shuffle(candidates);
            for (var i = 0; i < candidates.Count && result.Count < count; i++) result.Add(candidates[i]);

            // Nothing left to recruit or level up: offer healing instead.
            if (result.Count == 0)
            {
                result.Add(new UpgradeOption
                {
                    Title = Loc.Get("card.heal.title"), Badge = "", Description = Loc.Get("card.heal.desc"), Tint = new Color(1f, 0.4f, 0.4f), Icon = sprites.ItemHeart,
                    Apply = () => player.Heal(30f)
                });
            }
            return result;
        }
    }
}
