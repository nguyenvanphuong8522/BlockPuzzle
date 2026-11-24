using MessagePipe;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Survivor
{
    // Composition root: every system of the game is registered here.
    public class GameLifetimeScope : LifetimeScope
    {
        [Tooltip("HUD prefab (Assets/Prefabs/UI/Hud.prefab). Filled by Survivor > Build UI Prefabs.")]
        [SerializeField] HudView hudPrefab;

        protected override void Configure(IContainerBuilder builder)
        {
            var options = builder.RegisterMessagePipe();
            builder.RegisterMessageBroker<EnemyKilledEvent>(options);

            builder.Register<GameConfig>(Lifetime.Singleton);
            builder.Register<DebugSettings>(Lifetime.Singleton);
            builder.RegisterInstance(SpriteCatalog.LoadOrEmpty());
            builder.RegisterInstance(AudioCatalog.LoadOrEmpty());
            builder.RegisterInstance(GemPickupSettings.LoadOrDefault());
            builder.RegisterInstance(EnemyDropSettings.LoadOrDefault());
            builder.RegisterInstance(PlayerTuning.LoadOrDefault());
            builder.RegisterInstance(GunSettings.LoadOrDefault());
            builder.Register<AudioService>(Lifetime.Singleton);
            builder.Register<InputService>(Lifetime.Singleton);
            builder.Register<HapticsService>(Lifetime.Singleton);
            builder.Register<GameStats>(Lifetime.Singleton);
            builder.Register<PlayerProgress>(Lifetime.Singleton);
            builder.Register<Player>(Lifetime.Singleton);
            builder.Register<PlayerGun>(Lifetime.Singleton);
            builder.Register<CameraShake>(Lifetime.Singleton);

            builder.Register<EnemyDrawer>(Lifetime.Singleton);
            builder.Register<EnemyDeathSystem>(Lifetime.Singleton);
            builder.Register<EnemySystem>(Lifetime.Singleton);
            builder.Register<EnemySpawner>(Lifetime.Singleton);
            builder.Register<ProjectileSystem>(Lifetime.Singleton);
            builder.Register<GemSystem>(Lifetime.Singleton);
            builder.Register<PickupSystem>(Lifetime.Singleton);
            builder.RegisterEntryPoint<RewardDropSystem>().AsSelf(); // entry point = created at start, nobody needs to ask for it
            builder.Register<EffectSystem>(Lifetime.Singleton);
            builder.Register<DamageNumberSystem>(Lifetime.Singleton);
            builder.Register<DecorationSystem>(Lifetime.Singleton);

            builder.Register<WeaponContext>(Lifetime.Singleton);
            builder.Register<WeaponSystem>(Lifetime.Singleton);
            builder.Register<BoomerangSystem>(Lifetime.Singleton);
            builder.Register<CompanionContext>(Lifetime.Singleton);
            builder.Register<CompanionSystem>(Lifetime.Singleton);
            builder.Register<UpgradeService>(Lifetime.Singleton);

            builder.Register<InstancedRenderer>(Lifetime.Singleton);
            builder.RegisterComponentInNewPrefab(hudPrefab, Lifetime.Singleton);
            builder.RegisterInstance(PopupCatalog.LoadOrEmpty());
            builder.Register<PopupController>(Lifetime.Singleton);
            builder.Register<DebugPanel>(Lifetime.Singleton);

            builder.RegisterEntryPoint<GameController>().AsSelf();
            builder.RegisterEntryPoint<AutoScreenshot>();
            builder.RegisterEntryPoint<Benchmark>();
        }
    }
}
