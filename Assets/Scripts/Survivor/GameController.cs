using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using VContainer.Unity;

namespace Survivor
{
    // Runs the game loop in a fixed, explicit order every frame (no per-enemy Update calls).
    public sealed class GameController : IStartable, ITickable, ILateTickable
    {
        enum State { Playing, Choosing, Paused, DebugPaused, Ended }

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
        readonly CompanionSystem companions;
        readonly BoomerangSystem boomerangs;
        readonly UpgradeService upgrades;
        readonly PlayerProgress progress;
        readonly GameStats stats;
        readonly HudView hud;
        readonly DebugPanel debugPanel;
        readonly PopupController popups;
        readonly DebugSettings debug;
        readonly InstancedRenderer renderer;
        readonly AudioService audio;
        readonly DecorationSystem decorations;
        readonly SpriteCatalog sprites;
        readonly CameraShake shake;
        readonly HapticsService haptics;

        State state;
        bool choosingForChest;
        int selectedCard;
        float menuOpenedAt;             // ignore stick / buttons briefly after a menu opens, so a held input does not pick by accident
        const float MenuInputDelay = 0.35f;
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
            EffectSystem effects, DamageNumberSystem damageNumbers, WeaponSystem weapons, CompanionSystem companions,
            BoomerangSystem boomerangs, UpgradeService upgrades,
            PlayerProgress progress, GameStats stats, HudView hud, DebugPanel debugPanel, PopupController popups, DebugSettings debug,
            InstancedRenderer renderer, AudioService audio, DecorationSystem decorations, SpriteCatalog sprites, CameraShake shake, HapticsService haptics)
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
            this.companions = companions;
            this.boomerangs = boomerangs;
            this.upgrades = upgrades;
            this.progress = progress;
            this.stats = stats;
            this.hud = hud;
            this.debugPanel = debugPanel;
            this.popups = popups;
            this.debug = debug;
            this.renderer = renderer;
            this.audio = audio;
            this.decorations = decorations;
            this.sprites = sprites;
            this.shake = shake;
            this.haptics = haptics;
        }

        public void Start()
        {
            Application.targetFrameRate = 60;
            Time.timeScale = debug.GameSpeed;
            camera = Camera.main;
            camera.orthographic = true;
            camera.orthographicSize = config.CameraSizeFor(camera.aspect);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.08f, 0.1f, 0.08f);

            renderer.SetGroundTexture(sprites.GroundTile, sprites.GroundTileSize);
            debugPanel.Build();
            hud.PauseClicked += Pause;
            debugPanel.Toggled += OnDebugToggled;
            popups.PreloadAsync().Forget(); // level-up / pause / result load in the background
            upgrades.GiveStartingWeapon();
            audio.PlayMusic();
        }

        public void Tick()
        {
            if (input.DebugTogglePressed) debugPanel.Toggle(); // OnDebugToggled pauses / resumes
            if (debug.ToolsAvailable && input.WasPressed(Key.F4)) gems.SpawnTestRing(20); // test the gem pickup animation

            switch (state)
            {
                case State.Choosing:
                    HandleChoiceInput();
                    return;
                case State.DebugPaused:
                    if (input.PausePressed) debugPanel.Toggle(); // Esc / Start also closes the debug panel
                    return;
                case State.Paused:
                    if (input.PausePressed) Resume();
                    else popups.Get<PausePopup>()?.HandleNavigation(input.NavigateVertical(), input.NavigateHorizontal(), input.SubmitPressed);
                    return;
                case State.Ended:
                    if (input.WasPressed(Key.R) || (MenuReady && input.SubmitPressed)) Restart();
                    return;
            }

            if (input.PausePressed)
            {
                Pause();
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
            companions.Tick(dt);
            boomerangs.Tick(dt);
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
            haptics.Update();
            var offset = shake.Tick(Time.unscaledDeltaTime);
            camera.transform.position = new Vector3(player.Position.x + offset.x, player.Position.y + offset.y, -10f);
            camera.orthographicSize = config.CameraSizeFor(camera.aspect); // follows window resizes
            renderer.SetView(camera);

            renderer.DrawGround(camera);
            decorations.Draw(renderer, camera);
            gems.Draw(renderer);
            pickups.Draw(renderer);
            enemies.Draw(renderer);
            projectiles.Draw(renderer);
            weapons.Draw(renderer);
            companions.Draw(renderer);
            boomerangs.Draw(renderer);
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
            hud.ReleaseJoystick();
            popups.Show<LevelUpPopup>(new LevelUpData
            {
                Title = forChest ? "RƯƠNG BÁU!" : "LÊN CẤP!", Options = options, OnPick = ChooseUpgrade
            });
            selectedCard = 0;
            menuOpenedAt = Time.unscaledTime;
        }

        // The game loop simply stops ticking while paused (music keeps playing, quieter settings apply live).
        public void Pause()
        {
            if (state != State.Playing) return;
            state = State.Paused;
            haptics.Stop();
            input.Joystick = Vector2.zero;
            hud.ReleaseJoystick();
            popups.Show<PausePopup>(new PauseData { OnResume = Resume, OnRestart = Restart });
        }

        // The game stops while the debug panel is open, so you can change settings calmly.
        // Opened on top of another menu (level-up, pause, result), it leaves that menu in charge.
        void OnDebugToggled(bool open)
        {
            if (open && state == State.Playing)
            {
                state = State.DebugPaused;
                input.Joystick = Vector2.zero;
                hud.ReleaseJoystick();
                haptics.Stop();
            }
            else if (!open && state == State.DebugPaused)
            {
                state = State.Playing;
            }
        }

        public void Resume()
        {
            if (state != State.Paused) return;
            popups.Dismiss<PausePopup>();
            state = State.Playing;
        }

        bool MenuReady => Time.unscaledTime - menuOpenedAt > MenuInputDelay;

        // Keys 1/2/3 pick directly; stick / d-pad / arrows move the highlight and Submit (gamepad A, Enter) confirms.
        void HandleChoiceInput()
        {
            var key = input.UpgradeKeyPressed();
            if (key >= 0 && key < options.Count)
            {
                ChooseUpgrade(key);
                return;
            }
            if (!MenuReady) return;

            // Cards are stacked vertically: down = next card, up = previous (left/right work too).
            var step = input.NavigateHorizontal();
            if (step == 0) step = -input.NavigateVertical();
            if (step != 0)
            {
                selectedCard = (selectedCard + step + options.Count) % options.Count;
                popups.Get<LevelUpPopup>()?.Highlight(selectedCard);
                audio.Play(Sfx.Click, 0.5f, 0f, 0f);
            }
            if (input.SubmitPressed) ChooseUpgrade(selectedCard);
        }

        public void ChooseUpgrade(int index)
        {
            if (state != State.Choosing || index < 0 || index >= options.Count) return;
            options[index].Apply();
            audio.Play(Sfx.Click, 1f, 0f, 0f);
            if (choosingForChest) progress.PendingChests--;
            else progress.PendingLevelUps--;
            popups.Dismiss<LevelUpPopup>();
            state = State.Playing; // anything else pending reopens the panel next frame
        }

        void End(bool survived)
        {
            state = State.Ended;
            haptics.Stop();
            menuOpenedAt = Time.unscaledTime;
            audio.StopMusic();
            audio.Play(survived ? Sfx.Victory : Sfx.GameOver, 1f, 0f, 0f);
            hud.ReleaseJoystick();
            popups.Show<ResultPopup>(new ResultData
            {
                Survived = survived, Seconds = stats.Elapsed, Kills = stats.Kills.Value, Level = progress.Level.Value,
                OnRestart = Restart
            });
        }

        void Restart() => SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);

        void UpdateDebugText()
        {
            fpsFrames++;
            fpsTimer += Time.unscaledDeltaTime;
            if (fpsTimer < 0.5f) return;
            var fps = fpsFrames / fpsTimer;
            hud.SetDebug(debug.ShowStats
                ? $"FPS {fps:0}  ·  Quái {enemies.Count}  ·  Logic {simulationMs:0.0}ms  ·  Vẽ {renderMs:0.0}ms  ·  Draw call {renderer.LastFrameDrawCalls}" +
                  $"\nTầm hút {player.Stats.MagnetRadius:0.00}"
                : "");
            fpsFrames = 0;
            fpsTimer = 0f;
        }
    }
}
