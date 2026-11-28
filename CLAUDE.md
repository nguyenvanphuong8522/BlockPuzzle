# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project

"Pal Rush Survivors": an original portrait (1080x1920) survivors-like made in Unity 6000.3.18f1. The folder and
solution are still named BlockPuzzle; ignore that name. All game code is in `Assets/Scripts/Survivor`, in the
`Survivor` namespace and assembly (`Survivor.asmdef`). The main scene is `Assets/Scenes/Main.unity`.

The user writes in Vietnamese: reply in Vietnamese. Code, comments and tooltips stay in English. Text the
player sees goes through localization (see below).

## Working rules

- The user often has the Editor in Play mode. Before compiling, running scripts or entering Play, check
  `Application.isPlaying`. Never stop or change their Play session. If it is playing, write the code and say
  it will compile when they leave Play.
- Changes made in Play mode to ScriptableObject assets persist. Restore any value you change for a test,
  using `try/finally`.
- Commit only when asked.

## Driving the Editor (Unity CLI)

Use the `unity` CLI against the running Editor. These calls need the sandbox disabled.

```
unity --no-banner command --project-path "<project>" --caller plugin --skill unity-cli --timeout 120 "script eval" --code '<C#>'
unity ... "editor.playmode start" | "editor.playmode stop"
unity ... "capture game_view" --output-path <png>
unity ... "console get" --level error
```

**Compile:**
1. Run `UnityEditor.AssetDatabase.Refresh()`.
2. Poll `EditorApplication.isCompiling` until it is false.
3. Check `EditorUtility.scriptCompilationFailed`. The console keeps old `error CS` lines, so do not trust it alone.
4. After a domain reload the CLI briefly reports "No Pipeline instance": wait, then retry.

**Running systems from a script:** resolve them from the container:

```csharp
var c = UnityEngine.Object.FindFirstObjectByType<Survivor.GameLifetimeScope>().Container;
var x = VContainer.IObjectResolverExtensions.Resolve<Survivor.EnemySystem>(c);
```

`GameController` is not a MonoBehaviour, so resolve it as well.

**Play-mode testing:**
- Play starts on the Home screen. To get into a run, call the private `HomePopup.Play` by reflection (costs energy;
  `EnergyService.Refill()` afterwards) or set `GameSession.AutoStart = true` before entering Play.
- A level-up pauses the game. Call `GameController.ChooseUpgrade(0)` to continue.
- When the Editor is not focused, the game can stay stuck on frame 1. Check the numbers (for example
  `GameStats.Elapsed`) instead of trusting a screenshot.
- To zoom in, temporarily change `PlayerTuning.CameraViewWidth`; the normal value is 18.

## Commands

- **Tests:** PlayMode tests in `Assets/Tests` (`SurvivorSmokeTests.asmdef`). `SurvivorSmokeTest.GameRunsAndScales`
  is a smoke test of the whole game; `SurvivorProfileTest` measures system timings. Run them in Test Runner.
- **Builds:** the `Build/...` menus in `Assets/Scripts/Editor/BuildScript.cs`.
  - Android (CI): `-executeMethod BuildScript.BuildAndroid`, used by `codemagic.yaml` on every push to master.
  - Windows and WebGL.
- **WebGL:**
  - Test locally: `powershell -ExecutionPolicy Bypass -File Tools/serve-webgl.ps1`, then http://localhost:8080
    (also the `webgl` entry in `.claude/launch.json`).
  - Publish: `Tools/publish-itch.ps1` (uses butler).
- **Placeholder art and sound:** `powershell -ExecutionPolicy Bypass -File Tools/AssetGen/generate.ps1`.
  - This compiles `Tools/AssetGen/ArtGen.cs` / `SfxGen.cs` with `csc` (C# 5, System.Drawing) into
    `Assets/Art/Sprites` and `Assets/Audio`.
  - Then run `ArtSetup.Run()` (menu "Survivor/Setup Art & Audio") to fill `SpriteCatalog`.
  - Enemy art files are named `enemy{index}_<name>_<frame>`, where index is the position in `GameConfig.Enemies`.
- **Editor setup menus (`Survivor/...`):**
  - `UiPrefabBuilder`: builds the UI prefabs once; existing ones are not overwritten.
  - `HudBossSetup`, `LevelUpCardSetup`, `UpgradeDataSetup`: create the upgrade assets.
  - `LocalizationSetup`.

## Architecture

**Composition and loop**
- `GameLifetimeScope` (VContainer) registers every system as a singleton, plus the ScriptableObject configs
  (`RegisterInstance(X.LoadOrDefault())`). Messages use MessagePipe (`EnemyKilledEvent`).
- A new system is a plain class registered there and ticked or drawn from `GameController`.
- `GameController` runs the whole frame in a fixed order: input, player, spawner and event systems, enemies,
  boss skills and arena, weapons and companions, then draw.
- No per-object `Update`. `GameStats.Elapsed` stops while a boss is alive.

**Enemies (`Enemies/EnemySystem.cs`)**
- Data is stored as arrays: NativeArrays plus Burst jobs for movement, grid separation and drawing.
- Removal is swap-remove. Any new per-enemy array must be added to `Spawn`, `RemoveAt`, `Grow` and `Dispose`.
- Special enemies are flags on that same data, not separate objects: ranged keep-distance (`keep`),
  flyers (`fly`), elites (`elite`). The boss is one entry, found by `BossIndex`.

**Rendering (`Rendering/InstancedRenderer.cs`)**
- Everything in the world is drawn with GPU instancing: no SpriteRenderers.
- Calls are made each frame: `Sprite`, `SpriteRotated`, `Circle`, `Ring` (thickness is a fraction of the
  radius), `Line`, `Rect`, `Bar`.
- Layers are drawn in the `RenderLayer` order. Shapes come from `Resources/Shaders/InstancedShape.shader`.
- UI is uGUI + TextMeshPro, as prefabs in `Assets/Prefabs/UI`. Popups load through Addressables via `PopupCatalog`.

**Config assets in `Assets/Resources`, editable during Play.** Each one has a `LoadOrDefault()` that falls back
to code defaults.
- **Stats versus behaviour.** Numbers used for balancing are kept apart from rules and visuals:
  - Bosses: each boss (an `IsBoss` entry in `GameConfig.Enemies`) has `Resources/Bosses/<Id>/<Id>_Stats.asset`
    (`BossStats`: HP, speed, damage) and `<Id>_Settings.asset` (`BossSettings`: skill rules by distance, timing,
    arena, visuals).
  - `Resources/Bosses/BossSchedule.asset` sets which boss comes at which second.
  - `BossRoster` serves the current boss's assets: the living boss, or else the next one. Boss systems read
    `roster.Stats` / `roster.Settings`, never a single global asset.
  - Each normal enemy type: `Resources/Enemies/<Id>/<Id>_Stats.asset` and `<Id>_Settings.asset`.
    `EnemyDefinition` reads them live after `GameConfig` calls `LoadAssets()`.
  - Rules shared by all types: `EnemyStats` / `EnemySettings` (spawn rate, elite schedule, swarm, knockback).
  - `GameConfig.Enemies` keeps only identity: Id, name, color, xp, IsBoss, IsRanged.
- **Other config assets:**
  - `PlayerTuning`: the hero, camera, companions, level-up, numbers.
  - `SpawnTimeline`: scripted events at set seconds.
  - `EnemyDropSettings`, `GemPickupSettings`, `GunSettings`, `SpriteCatalog`, `AudioCatalog`.

**Home screen, chapters, energy (`Meta/`)**
- The game opens on `HomePopup` (`GameController` state `Home`; nothing ticks, the HUD is hidden). Play pays energy,
  picks the chapter and calls `BeginRun`. Quit (pause menu, after `ConfirmPopup`) and the result button reload the
  scene back to Home; pause Restart pays energy again and reloads with `GameSession.AutoStart` (straight into a run).
  Tests set `GameSession.AutoStart = true` before loading the scene.
- `HomeSettings.asset`: max energy (60), cost per run (5), refill time, chapter list. `EnergyService` and
  `ChapterService` save to PlayerPrefs; chapter N opens when N-1 has been survived.
- `ChapterData` (`Resources/Chapters/`): name key, preview, survive seconds, its own `BossSchedule`, and difficulty
  scales applied in `EnemySystem.Spawn` (HP), `Player.TryHurt` (damage taken) and `EnemySpawner` (spawn rate).
  `BossRoster` reads the played chapter's schedule.
- The language is only changed in `SettingsPopup` (Home); the pause menu has Quit instead.
- Prefabs: "Survivor/Build Home UI" (`HomeUiBuilder`, reuses the `UiPrefabBuilder` helpers).

**Companions and upgrades**
- One `CompanionData` ScriptableObject per companion, in `Resources/Upgrades/Companions`. Each has a
  `Companion` subclass in `Companions/`.
- `CompanionKind` (enum in `PlayerTuning.cs`) and `SpriteCatalog` must get an entry for each new one.
  `UpgradeDataSetup` creates the asset.
- Fusions (for example Wukong = max Monkey + max Otter) use `CompanionData.FusionOf`, `MaxLevel` and
  `ConsumeIngredients`, and are handled in `UpgradeService`.
- Items are `ItemData` assets.

**Localization (`Localization/`)**
- `Loc.Get(key)` / `Loc.Format(key, args)` read `Resources/Localization/Localization.csv`. Columns:
  Key, Description, English, Vietnamese, Japanese.
- Languages are set in `Resources/LocalizationSettings.asset`. Each language has a list of font rules. The first
  rule whose From Font / From Material matches a text's original font and material wins, and the text switches
  to To Font / To Material.
- Replacement fonts and materials are Addressables (`LocFonts`). They load when the language is chosen and the
  previous language's are released once the new ones are in.
- Every string the player sees must be a CSV key:
  - Fixed labels in prefabs use the `LocalizedText` component.
  - Text set from code uses `Loc` and refreshes on `Loc.Changed`. The `TMP_Text` gets a `LocalizedFont`.
  - Text fields in assets hold keys and are read with `Loc.Text()`.
- Companion and item text: keys are derived from the asset name (`companion.<asset>.name/.desc/.level2..5`,
  `item.<asset>.name/.desc`).
- After adding UI labels, run "Survivor/Localization/Setup". The debug panel, tooltips and headers are not
  localized.
- The team edits the table in a Google Sheet. "Survivor/Localization/Download from Google Sheet" overwrites the
  CSV from `LocalizationSettings.GoogleSheetUrl`. This is one way only and Editor only.

## Pitfalls

- When editing files from the shell, use the Edit tool or `sed`. Do not use perl `s|...|...|` with `\|\|` in the
  pattern; it has corrupted files before.
- On the instanced shader, a white flash on a half-transparent sprite shows the whole quad as a rectangle.
- `ArtGen.cs` must stay C# 5 (old `csc`).
