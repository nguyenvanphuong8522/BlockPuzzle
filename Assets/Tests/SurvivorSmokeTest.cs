using System.Collections;
using NUnit.Framework;
using Survivor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using VContainer;

public class SurvivorSmokeTest
{
    [UnityTest]
    public IEnumerator GameRunsAndScales()
    {
        SceneManager.LoadScene("Main");
        yield return null;
        yield return null;

        var c = Object.FindFirstObjectByType<GameLifetimeScope>().Container;
        var enemies = c.Resolve<EnemySystem>();
        var player = c.Resolve<Player>();
        var game = c.Resolve<GameController>();
        var progress = c.Resolve<PlayerProgress>();
        var stats = c.Resolve<GameStats>();
        var weapons = c.Resolve<WeaponSystem>();
        var gems = c.Resolve<GemSystem>();
        var debug = c.Resolve<DebugSettings>();
        var godModeBefore = debug.GodMode;

        // 1) Normal play for ~30 game seconds in god mode, auto-picking upgrades.
        debug.GodMode = true;
        Time.timeScale = 4f;
        var end = Time.realtimeSinceStartup + 8f;
        int picks = 0, maxEnemies = 0;
        while (Time.realtimeSinceStartup < end)
        {
            if (game.IsChoosing) { game.ChooseUpgrade(0); picks++; }
            maxEnemies = Mathf.Max(maxEnemies, enemies.Count);
            yield return null;
        }
        Time.timeScale = 1f;
        Debug.Log($"SMOKE play: t={stats.Elapsed:F0}s enemiesMax={maxEnemies} kills={stats.Kills.Value} level={progress.Level.Value} picks={picks} weapons={weapons.Weapons.Count} gems={gems.Count} hp={player.Hp.Value}");
        Assert.Greater(stats.Kills.Value, 0, "no kills");
        Assert.Greater(picks, 0, "never leveled up");
        Assert.AreEqual(player.Stats.MaxHp, player.Hp.Value, "god mode: HP must never drop");

        // 2) Boss: spawn, kill, chest drops, chest opens the reward screen.
        var spawner = c.Resolve<EnemySpawner>();
        var pickups = c.Resolve<PickupSystem>();
        spawner.SpawnBoss();
        yield return null;
        yield return null;
        Assert.GreaterOrEqual(enemies.BossIndex, 0, "boss not tracked");
        enemies.KillAll();
        yield return null;
        yield return null;
        Assert.Less(enemies.BossIndex, 0, "boss still alive");
        Assert.Greater(pickups.Count, 0, "boss dropped no chest");
        while (game.IsChoosing) { game.ChooseUpgrade(0); yield return null; }
        pickups.Spawn(PickupKind.Chest, player.Position); // drop one right on the player
        for (var i = 0; i < 5 && !game.IsChoosing; i++) yield return null;
        Assert.IsTrue(game.IsChoosing, "chest did not open the reward screen");
        game.ChooseUpgrade(0);

        // 3) Sprites: render through the atlas path with a runtime sprite.
        var catalog = c.Resolve<SpriteCatalog>();
        var tex = new Texture2D(8, 8);
        var sprite = Sprite.Create(tex, new Rect(0, 0, 8, 8), new Vector2(0.5f, 0.5f), 8);
        var savedPlayer = catalog.Player;
        var savedEnemies = catalog.Enemies;
        var savedGem = catalog.Gem;
        var anim = new SpriteAnim { Frames = new[] { sprite, sprite } };
        catalog.Player = anim;
        catalog.Gem = sprite;
        catalog.Enemies = new[] { anim, anim, anim, anim, anim, anim, anim };
        for (var i = 0; i < 10; i++) yield return null;
        catalog.Player = savedPlayer;
        catalog.Gem = savedGem;
        catalog.Enemies = savedEnemies;

        // 4) Stress: 5000 enemies.
        c.Resolve<UpgradeService>().GiveAllCompanionsMaxed();
        Assert.Greater(c.Resolve<CompanionSystem>().Companions.Count, 0, "no companion recruited");
        c.Resolve<UpgradeService>().GiveAllWeaponsMaxed();
        spawner.SpawnBurst(5000);
        for (var i = 0; i < 120; i++)
        {
            if (game.IsChoosing) game.ChooseUpgrade(0);
            yield return null;
        }
        var renderer = c.Resolve<InstancedRenderer>();
        Debug.Log($"SMOKE stress: enemies={enemies.Count} instances={renderer.LastFrameInstances} drawCalls={renderer.LastFrameDrawCalls}");
        Assert.Greater(enemies.Count, 3000);

        debug.GodMode = godModeBefore;
    }
}
