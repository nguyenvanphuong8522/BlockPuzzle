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
        enum State { Home, Playing, Choosing, Paused, DebugPaused, Ended }

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
        readonly BloodSystem blood;
        readonly ChapterService chapters;   // the chapter being played (picked on the Home screen)
        readonly EnergyService energy;
        readonly FloatingTextSystem floatingText;
        readonly BossSkillSystem bossSkills;
        readonly SwarmSystem swarm;
        readonly EnemyBulletSystem enemyBullets;
        readonly EliteSystem elites;
        readonly TimelineSystem timeline;
        readonly BossRoster bossRoster;
        readonly PlayerTuning tuning;
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
        readonly BossArenaSystem arena;

        State state;
        bool choosingForChest;
        int selectedCard;
        float menuOpenedAt;             // ignore stick / buttons briefly after a menu opens, so a held input does not pick by accident
        const float MenuInputDelay = 0.35f;
        List<UpgradeOption> options;
        Camera camera;
        public const int TargetFps = 60;
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
            InstancedRenderer renderer, AudioService audio, DecorationSystem decorations, SpriteCatalog sprites, CameraShake shake, HapticsService haptics,
            BossArenaSystem arena, FloatingTextSystem floatingText, BossSkillSystem bossSkills, SwarmSystem swarm,
            EnemyBulletSystem enemyBullets, EliteSystem elites, TimelineSystem timeline, BossRoster bossRoster, PlayerTuning tuning, BloodSystem blood,
            ChapterService chapters, EnergyService energy)
        {
            this.arena = arena;
            this.floatingText = floatingText;
            this.bossSkills = bossSkills;
            this.swarm = swarm;
            this.enemyBullets = enemyBullets;
            this.elites = elites;
            this.timeline = timeline;
            this.bossRoster = bossRoster;
            this.tuning = tuning;
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
            this.blood = blood;
            this.chapters = chapters;
            this.energy = energy;
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
            // Lock to 60 FPS everywhere. VSync must be off: while it is on, Unity ignores targetFrameRate and follows the
            // monitor (75/120/144 Hz...), so the frame rate jumps above 60 and back down.
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = TargetFps;
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
            popups.PreloadAsync().Forget(); // home / level-up / pause / result load in the background
            audio.PlayMusic();

            // "Restart" reloads the scene straight into a run (energy already paid); otherwise the Home screen first.
            if (GameSession.AutoStart)
            {
                GameSession.AutoStart = false;
                BeginRun();
            }
            else EnterHome();
        }

        // Home screen: nothing runs; Play (energy paid, chapter picked) starts the run.
        void EnterHome()
        {
            state = State.Home;
            hud.gameObject.SetActive(false);
            popups.Show<HomePopup>(new HomeData { OnPlay = BeginRun });
        }

        void BeginRun()
        {
            if (popups.IsOpen<HomePopup>()) popups.Dismiss<HomePopup>();
            hud.gameObject.SetActive(true);
            upgrades.GiveStartingWeapon();
            upgrades.GiveStartingCompanions(); // "Đệ tử khởi đầu" in PlayerTuning
            state = State.Playing;
        }

        // Back to the Home screen (the run is thrown away): reload the scene.
        void GoHome()
        {
            GameSession.AutoStart = false;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        // Pause menu "Quit": asks first.
        void AskQuit()
        {
            popups.Show<ConfirmPopup>(new ConfirmData { Message = Loc.Get("confirm.quit"), OnYes = GoHome });
            menuOpenedAt = Time.unscaledTime;
        }

        public void Tick()
        {
            if (input.DebugTogglePressed) debugPanel.Toggle(); // OnDebugToggled pauses / resumes
            if (debug.ToolsAvailable && input.WasPressed(Key.F4)) gems.SpawnTestRing(20); // test the gem pickup animation

            switch (state)
            {
                case State.Home:
                    return; // the Home screen is driven by touch / mouse
                case State.Choosing:
                    HandleChoiceInput();
                    return;
                case State.DebugPaused:
                    if (input.PausePressed) debugPanel.Toggle(); // Esc / Start also closes the debug panel
                    else debugPanel.HandleNavigation(input.NavigateVertical(), input.NavigateHorizontal(), input.SubmitPressed,
                        input.CancelPressed); // gamepad / keyboard: move, press, change values, B closes
                    return;
                case State.Paused:
                    if (popups.IsOpen<ConfirmPopup>()) // "Quit to Home?" on top: Submit = yes, Esc / B = no
                    {
                        if (MenuReady) popups.Get<ConfirmPopup>().HandleInput(input.SubmitPressed, input.PausePressed || input.CancelPressed);
                    }
                    else if (input.PausePressed) Resume();
                    else popups.Get<PausePopup>()?.HandleNavigation(input.NavigateVertical(), input.NavigateHorizontal(), input.SubmitPressed);
                    return;
                case State.Ended:
                    if (input.WasPressed(Key.R) || (MenuReady && input.SubmitPressed)) GoHome();
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
            if (enemies.BossIndex < 0) stats.Elapsed += dt; // the clock stops during a boss fight

            player.Tick(input.Move, dt);
            spawner.Tick(dt);
            swarm.Tick(dt);          // swarm event: warning, then a flock flying across the hero
            elites.Tick(dt);         // elite enemies: big tough copies now and then
            timeline.Tick(dt);       // scripted moments (SpawnTimeline.asset): circles, walls, groups at set seconds
            enemies.Tick(dt);
            bossSkills.Tick(dt);     // boss dash / bullet burst (before the arena, so the wall stops a dash)
            enemyBullets.Tick(dt);   // ranged enemies (ink squid): windup, shots, blobs hitting the hero
            arena.Tick(dt);          // boss arena: wall, keeps hero and boss inside
            weapons.Tick(dt);
            companions.Tick(dt);
            boomerangs.Tick(dt);
            projectiles.Tick(dt);
            enemies.RemoveDead(); // publishes EnemyKilledEvent -> gems / drops spawn, kill counter goes up
            gems.Tick(dt);
            pickups.Tick(dt);
            effects.Tick(dt);
            blood.Tick(dt);
            damageNumbers.Tick(dt);
            floatingText.Tick(dt);
            simulationMs = (Time.realtimeSinceStartup - start) * 1000f;

            hud.SetTime(stats.Elapsed);

            if (player.IsDead) End(false);
            else if (stats.Elapsed >= chapters.Current.SurviveSeconds && !BossPending()) End(true);
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

            renderer.SetGroundLook(tuning.GroundTint, tuning.GroundSaturation);
            renderer.DrawGround(camera);
            decorations.Draw(renderer, camera);
            gems.Draw(renderer);
            pickups.Draw(renderer);
            enemies.Draw(renderer);
            arena.Draw(renderer);
            bossSkills.Draw(renderer);
            enemyBullets.Draw(renderer);
            elites.Draw(renderer);
            projectiles.Draw(renderer);
            weapons.Draw(renderer);
            companions.Draw(renderer);
            boomerangs.Draw(renderer);
            player.Draw(renderer);
            blood.Draw(renderer);
            effects.Draw(renderer);
            damageNumbers.Draw(renderer);
            renderer.Flush();
            floatingText.Draw(camera);
            renderMs = (Time.realtimeSinceStartup - start) * 1000f;

            bossRoster.SetAlive(enemies.BossIndex >= 0 ? enemies.TypeOf(enemies.BossIndex) : -1);
            hud.SetBoss(enemies.BossName, enemies.BossIndex >= 0 ? enemies.BossHpRatio : -1f);
            // Countdown to the next boss under the XP bar (+ warning banner), hidden during the fight.
            var nextBoss = spawner.NextBossTime;
            hud.SetBossCountdown(enemies.BossIndex >= 0 || nextBoss < 0f ? -1f : nextBoss - stats.Elapsed);
            var swarmWarn = swarm.WarningActive;
            if (timeline.WarningActive && !swarmWarn)
                hud.SetSwarmWarning(true, timeline.WarningDirection, timeline.WarningProgress);
            else
                hud.SetSwarmWarning(swarmWarn, swarm.WarningDirection, swarm.WarningProgress);
            UpdateDebugText();
        }

        // Level-ups and chests use the same card screen.
        void OpenChoice(bool forChest)
        {
            state = State.Choosing;
            choosingForChest = forChest;
            // Level-ups alternate between companions and items (every Nth one, PlayerTuning "Lên cấp: đệ tử / đồ").
            var levelUpNumber = progress.LevelUpsChosen + 1;
            var items = !forChest && upgrades.IsItemLevelUp(levelUpNumber);
            options = forChest ? upgrades.Roll(3) : upgrades.RollLevelUp(levelUpNumber, 3);
            if (!forChest) audio.Play(Sfx.LevelUp, 1f, 0f);
            hud.ReleaseJoystick();
            popups.Show<LevelUpPopup>(new LevelUpData
            {
                Title = Loc.Get(forChest ? "levelup.title_chest" : items ? "levelup.title_items" : "levelup.title_companions"),
                Kind = forChest ? ChoiceKind.Chest : items ? ChoiceKind.Item : ChoiceKind.Companion,
                Options = options, OnPick = ChooseUpgrade
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
            popups.Show<PausePopup>(new PauseData { OnResume = Resume, OnRestart = Restart, OnQuit = AskQuit });
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
            else
            {
                progress.PendingLevelUps--;
                progress.LevelUpsChosen++;
            }
            popups.Dismiss<LevelUpPopup>();
            state = State.Playing; // anything else pending reopens the panel next frame
        }

        // A boss is alive, or one is due by now (e.g. scheduled right at the end time): the run is only won once it
        // is beaten, so a boss scheduled at Survive Seconds becomes the final boss instead of ending the run at once.
        bool BossPending()
        {
            if (enemies.BossIndex >= 0) return true;
            var next = bossRoster.NextTime;
            return next >= 0f && next <= stats.Elapsed;
        }

        void End(bool survived)
        {
            state = State.Ended;
            chapters.RecordResult(survived, stats.Elapsed); // best time; survived = cleared (opens the next chapter)
            haptics.Stop();
            menuOpenedAt = Time.unscaledTime;
            audio.StopMusic();
            audio.Play(survived ? Sfx.Victory : Sfx.GameOver, 1f, 0f, 0f);
            hud.ReleaseJoystick();
            popups.Show<ResultPopup>(new ResultData
            {
                Survived = survived, Seconds = stats.Elapsed, Kills = stats.Kills.Value, Level = progress.Level.Value,
                OnRestart = GoHome
            });
        }

        // Pause menu "Restart": a new run of the same chapter (pays its energy again; not enough = Home screen).
        void Restart()
        {
            if (!energy.TryConsume(chapters.CostOf(chapters.SelectedIndex)))
            {
                GoHome();
                return;
            }
            GameSession.AutoStart = true;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        void UpdateDebugText()
        {
            fpsFrames++;
            fpsTimer += Time.unscaledDeltaTime;
            if (fpsTimer < 0.5f) return;
            var fps = fpsFrames / fpsTimer;
            // Release builds: only FPS (time and kills have their own HUD labels).
            // Editor and Development builds: the full stats line, toggled in the debug panel.
            if (!Debug.isDebugBuild)
                hud.SetDebug($"FPS {fps:0}");
            else
                hud.SetDebug(debug.ShowStats
                    ? $"FPS {fps:0}  ·  Quái {enemies.Count}  ·  Logic {simulationMs:0.0}ms  ·  Vẽ {renderMs:0.0}ms  ·  Draw call {renderer.LastFrameDrawCalls}" +
                      $"\nTầm hút {player.Stats.MagnetRadius:0.00}"
                    : "");
            fpsFrames = 0;
            fpsTimer = 0f;
        }
    }
}
