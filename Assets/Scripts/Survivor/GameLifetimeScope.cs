using MessagePipe;
using VContainer;
using VContainer.Unity;

namespace Survivor
{
    // Composition root: every system of the game is registered here.
    public class GameLifetimeScope : LifetimeScope
    {
        protected override void Configure(IContainerBuilder builder)
        {
            var options = builder.RegisterMessagePipe();
            builder.RegisterMessageBroker<EnemyKilledEvent>(options);

            builder.Register<GameConfig>(Lifetime.Singleton);
            builder.Register<DebugSettings>(Lifetime.Singleton);
            builder.RegisterInstance(SpriteCatalog.LoadOrEmpty());
            builder.RegisterInstance(AudioCatalog.LoadOrEmpty());
            builder.RegisterInstance(GemPickupSettings.LoadOrDefault());
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
            builder.Register<EnemySystem>(Lifetime.Singleton);
            builder.Register<EnemySpawner>(Lifetime.Singleton);
            builder.Register<ProjectileSystem>(Lifetime.Singleton);
            builder.Register<GemSystem>(Lifetime.Singleton);
            builder.Register<PickupSystem>(Lifetime.Singleton);
            builder.Register<EffectSystem>(Lifetime.Singleton);
            builder.Register<DamageNumberSystem>(Lifetime.Singleton);
            builder.Register<DecorationSystem>(Lifetime.Singleton);

            builder.Register<WeaponContext>(Lifetime.Singleton);
            builder.Register<WeaponSystem>(Lifetime.Singleton);
            builder.Register<UpgradeService>(Lifetime.Singleton);

            builder.Register<InstancedRenderer>(Lifetime.Singleton);
            builder.Register<HudView>(Lifetime.Singleton);
            builder.Register<DebugPanel>(Lifetime.Singleton);
            builder.Register<PauseView>(Lifetime.Singleton);

            builder.RegisterEntryPoint<GameController>().AsSelf();
            builder.RegisterEntryPoint<AutoScreenshot>();
            builder.RegisterEntryPoint<Benchmark>();
        }
    }
}
