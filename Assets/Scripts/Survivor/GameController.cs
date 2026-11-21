using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using VContainer.Unity;

namespace Survivor
{
    // Runs the game loop in a fixed, explicit order every frame (no per-enemy Update calls).
    public sealed class GameController : IStartable, ITickable, ILateTickable
    {
        enum State { Playing, Choosing, Ended }

        readonly GameConfig config;
        readonly InputService input;
        readonly Player player;
        readonly EnemySystem enemies;
        readonly EnemySpawner spawner;
        readonly ProjectileSystem projectiles;
        readonly GemSystem gems;
        readonly PickupSystem pickups;
        readonly EffectSystem effects;
        readonly DamageNumberSystem damageNumbers;
        readonly WeaponSystem weapons;
        readonly UpgradeService upgrades;
        readonly PlayerProgress progress;
        readonly GameStats stats;
        readonly HudView hud;
        readonly DebugPanel debugPanel;
        readonly DebugSettings debug;
        readonly InstancedRenderer renderer;
        readonly AudioService audio;
        readonly DecorationSystem decorations;
        readonly SpriteCatalog sprites;

        State state;
        bool choosingForChest;
        List<UpgradeOption> options;
        Camera camera;
        float fpsTimer;
        int fpsFrames;
        float simulationMs;
        float renderMs;

        public float SimulationMs => simulationMs;
        public float RenderMs => renderMs;

        public bool IsChoosing => state == State.Choosing;
        public bool IsEnded => state == State.Ended;

        public GameController(GameConfig config, InputService input, Player player, EnemySystem enemies,
            EnemySpawner spawner, ProjectileSystem projectiles, GemSystem gems, PickupSystem pickups,
            EffectSystem effects, DamageNumberSystem damageNumbers, WeaponSystem weapons, UpgradeService upgrades,
            PlayerProgress progress, GameStats stats, HudView hud, DebugPanel debugPanel, DebugSettings debug,
            InstancedRenderer renderer, AudioService audio, DecorationSystem decorations, SpriteCatalog sprites)
        {
            this.config = config;
            this.input = input;
            this.player = player;
            this.enemies = enemies;
            this.spawner = spawner;
            this.projectiles = projectiles;
            this.gems = gems;
            this.pickups = pickups;
            this.effects = effects;
            this.damageNumbers = damageNumbers;
            this.weapons = weapons;
            this.upgrades = upgrades;
            this.progress = progress;
            this.stats = stats;
            this.hud = hud;
            this.debugPanel = debugPanel;
            this.debug = debug;
            this.renderer = renderer;
            this.audio = audio;
            this.decorations = decorations;
            this.sprites = sprites;
        }

        public void Start()
        {
            Application.targetFrameRate = 60;
            Time.timeScale = debug.GameSpeed;
            camera = Camera.main;
            camera.orthographic = true;
            camera.orthographicSize = config.CameraSize;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.08f, 0.1f, 0.08f);

            renderer.SetGroundTexture(sprites.GroundTile, sprites.GroundTileSize);
            hud.Build();
            debugPanel.Build();
            upgrades.GiveStartingWeapon();
            audio.PlayMusic();
        }

        public void Tick()
        {
            if (input.WasPressed(Key.F3)) debugPanel.Toggle();

            switch (state)
            {
                case State.Choosing:
                    var key = input.UpgradeKeyPressed();
                    if (key >= 0 && key < options.Count) ChooseUpgrade(key);
                    return;
                case State.Ended:
                    if (input.WasPressed(Key.R)) Restart();
                    return;
            }

            var start = Time.realtimeSinceStartup;
            // Cap the step after a hitch (scaled, so the x2 / x4 debug speed still works).
            var dt = Mathf.Min(Time.deltaTime, Mathf.Max(1f, Time.timeScale) / 20f);
            stats.Elapsed += dt;

            player.Tick(input.Move, dt);
            spawner.Tick(dt);
            enemies.Tick(dt);
            weapons.Tick(dt);
            projectiles.Tick(dt);
            enemies.RemoveDead(); // publishes EnemyKilledEvent -> gems / drops spawn, kill counter goes up
            gems.Tick(dt);
            pickups.Tick(dt);
            effects.Tick(dt);
            damageNumbers.Tick(dt);
            simulationMs = (Time.realtimeSinceStartup - start) * 1000f;

            hud.SetTime(stats.Elapsed);

            if (player.IsDead) End(false);
            else if (stats.Elapsed >= config.SurviveSeconds) End(true);
            else if (progress.PendingChests > 0) OpenChoice(forChest: true);
            else if (progress.PendingLevelUps > 0) OpenChoice(forChest: false);
        }

        public void LateTick()
        {
            var start = Time.realtimeSinceStartup;
            camera.transform.position = new Vector3(player.Position.x, player.Position.y, -10f);
            renderer.SetView(camera);

            renderer.DrawGround(camera);
            decorations.Draw(renderer, camera);
            gems.Draw(renderer);
            pickups.Draw(renderer);
            enemies.Draw(renderer);
            projectiles.Draw(renderer);
            weapons.Draw(renderer);
            player.Draw(renderer);
            effects.Draw(renderer);
            damageNumbers.Draw(renderer);
            renderer.Flush();
            renderMs = (Time.realtimeSinceStartup - start) * 1000f;

            hud.SetBoss(enemies.BossName, enemies.BossIndex >= 0 ? enemies.BossHpRatio : -1f);
            UpdateDebugText();
        }

        // Level-ups and chests use the same card screen.
        void OpenChoice(bool forChest)
        {
            state = State.Choosing;
            choosingForChest = forChest;
            options = upgrades.Roll(3);
            if (!forChest) audio.Play(Sfx.LevelUp, 1f, 0f);
            hud.ShowLevelUp(forChest ? "RƯƠNG BÁU!" : "LÊN CẤP!", options, ChooseUpgrade);
        }

        public void ChooseUpgrade(int index)
        {
            if (state != State.Choosing || index < 0 || index >= options.Count) return;
            options[index].Apply();
            audio.Play(Sfx.Click, 1f, 0f, 0f);
            if (choosingForChest) progress.PendingChests--;
            else progress.PendingLevelUps--;
            hud.HideLevelUp();
            state = State.Playing; // anything else pending reopens the panel next frame
        }

        void End(bool survived)
        {
            state = State.Ended;
            audio.StopMusic();
            audio.Play(survived ? Sfx.Victory : Sfx.GameOver, 1f, 0f, 0f);
            hud.ShowResult(survived, stats.Elapsed, stats.Kills.Value, progress.Level.Value, Restart);
        }

        void Restart() => SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);

        void UpdateDebugText()
        {
            fpsFrames++;
            fpsTimer += Time.unscaledDeltaTime;
            if (fpsTimer < 0.5f) return;
            var fps = fpsFrames / fpsTimer;
            hud.SetDebug(debug.ShowStats
                ? $"FPS {fps:0}  ·  Quái {enemies.Count}  ·  Logic {simulationMs:0.0}ms  ·  Vẽ {renderMs:0.0}ms  ·  Draw call {renderer.LastFrameDrawCalls}"
                : "");
            fpsFrames = 0;
            fpsTimer = 0f;
        }
    }
}
