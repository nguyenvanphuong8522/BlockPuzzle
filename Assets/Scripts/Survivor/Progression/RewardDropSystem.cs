using System;
using MessagePipe;
using UnityEngine;
using VContainer.Unity;

namespace Survivor
{
    // Drops rewards when an enemy dies, using the per-type lists in EnemyDropSettings.
    // Every reward line is rolled on its own (so a death can drop a gem AND a magnet).
    public sealed class RewardDropSystem : IInitializable, IDisposable
    {
        readonly EnemyDropSettings settings;
        readonly GemSystem gems;
        readonly PickupSystem pickups;
        readonly Player player;
        readonly EnemySettings tuning;
        readonly BossRoster roster;
        readonly BossArenaSystem arena;   // still up when the boss dies (it ends on its next Tick)
        BossSettings boss;                // settings of the boss that just died (fence of gems, trail)
        readonly IDisposable subscription;

        public RewardDropSystem(EnemyDropSettings settings, GemSystem gems, PickupSystem pickups, Player player,
            EnemySettings tuning, ISubscriber<EnemyKilledEvent> killed, BossRoster roster, BossArenaSystem arena)
        {
            this.arena = arena;
            this.player = player;
            this.tuning = tuning;
            this.roster = roster;
            this.settings = settings;
            this.gems = gems;
            this.pickups = pickups;
            subscription = killed.Subscribe(OnEnemyKilled);
        }

        void OnEnemyKilled(EnemyKilledEvent e)
        {
            boss = (e.IsBoss ? roster.FindType(e.Type)?.Settings : null) ?? roster.Settings;
            foreach (var reward in settings.RewardsFor(e.Type))
            {
                if (reward == null || UnityEngine.Random.value * 100f >= reward.ChancePercent) continue;
                Give(reward, e.Position, e.IsBoss);
            }
            if (e.IsElite) GiveEliteRewards(e.Position);
        }

        // Elite bonus (EnemySettings "Quái tinh anh: phần thưởng"): chest, a line of gems, maybe a drumstick.
        void GiveEliteRewards(Vector2 at)
        {
            if (UnityEngine.Random.value * 100f < tuning.EliteChestChance) SpawnPickups(PickupKind.Chest, at, 1);
            if (tuning.EliteBonusXp > 0)
                gems.SpawnTrail(at, player.Position, tuning.EliteBonusXp, Mathf.Max(1, tuning.EliteBonusGems));
            if (UnityEngine.Random.value * 100f < tuning.EliteHealChance)
                SpawnPickups(PickupKind.Heal, at + UnityEngine.Random.insideUnitCircle.normalized * 0.8f, 1);
        }

        void Give(EnemyDropSettings.Reward reward, Vector2 at, bool fromBoss)
        {
            switch (reward.Type)
            {
                case RewardType.Gem:
                    if (!fromBoss) gems.Spawn(at, reward.Amount); // one gem holding all the experience
                    else if (boss.GemsOnFence && arena.UpOrJustEnded) // a fence of gems along the arena wall
                        gems.SpawnRectangle(arena.Center, new Vector2(boss.ArenaWidth, boss.ArenaHeight) - Vector2.one * (2f * boss.FenceGemInset),
                            reward.Amount, boss.FenceGemSpacing);
                    else gems.SpawnTrail(at, player.Position, reward.Amount, boss.TrailGems); // a line of gems
                    break;
                case RewardType.Heal:
                    SpawnPickups(PickupKind.Heal, at, reward.Amount);
                    break;
                case RewardType.Magnet:
                    SpawnPickups(PickupKind.Magnet, at, reward.Amount);
                    break;
                case RewardType.Chest:
                    SpawnPickups(fromBoss ? PickupKind.BossChest : PickupKind.Chest, at, reward.Amount);
                    break;
            }
        }

        // Several of the same pickup are spread around the spot so they don't stack.
        void SpawnPickups(PickupKind kind, Vector2 at, int amount)
        {
            for (var k = 0; k < amount; k++)
                pickups.Spawn(kind, k == 0 ? at : at + UnityEngine.Random.insideUnitCircle * 0.6f);
        }

        public void Initialize() { } // only here so VContainer creates it as an entry point

        public void Dispose() => subscription.Dispose();
    }
}
