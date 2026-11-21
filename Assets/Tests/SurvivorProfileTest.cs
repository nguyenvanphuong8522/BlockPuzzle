using System.Collections;
using System.Diagnostics;
using NUnit.Framework;
using Survivor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using VContainer;
using Debug = UnityEngine.Debug;

// Benchmark: CPU cost per frame of each system at 5k / 10k / 20k enemies crowding the player.
// GPU time is not measured here (tests run without a graphics device).
public class SurvivorProfileTest
{
    [UnityTest]
    public IEnumerator ProfileSystems()
    {
        SceneManager.LoadScene("Main");
        yield return null;
        yield return null;
        var c = Object.FindFirstObjectByType<GameLifetimeScope>().Container;
        var config = c.Resolve<GameConfig>();
        var enemies = c.Resolve<EnemySystem>();
        var weapons = c.Resolve<WeaponSystem>();
        var projectiles = c.Resolve<ProjectileSystem>();
        var renderer = c.Resolve<InstancedRenderer>();
        var spawner = c.Resolve<EnemySpawner>();
        var debug = c.Resolve<DebugSettings>();
        var godBefore = debug.GodMode;
        debug.GodMode = true;
        config.MaxEnemies = 25000;
        c.Resolve<UpgradeService>().GiveAllWeaponsMaxed();

        foreach (var target in new[] { 5000, 10000, 20000 })
        {
            spawner.SpawnBurst(target - enemies.Count);
            // Let the crowd close in on the player.
            for (var i = 0; i < 240; i++)
            {
                spawner.SpawnBurst(target - enemies.Count);
                yield return null;
            }

            var sw = new Stopwatch();
            double tEnemy = 0, tWeapons = 0, tProj = 0, tDraw = 0;
            const int N = 30;
            for (var i = 0; i < N; i++)
            {
                spawner.SpawnBurst(target - enemies.Count);
                sw.Restart(); enemies.Tick(1f / 60f); tEnemy += sw.Elapsed.TotalMilliseconds;
                sw.Restart(); weapons.Tick(1f / 60f); tWeapons += sw.Elapsed.TotalMilliseconds;
                sw.Restart(); projectiles.Tick(1f / 60f); tProj += sw.Elapsed.TotalMilliseconds;
                enemies.RemoveDead();
                sw.Restart(); enemies.Draw(renderer); renderer.Flush(); tDraw += sw.Elapsed.TotalMilliseconds;
                yield return null;
            }
            Debug.Log($"PROFILE enemies={enemies.Count} enemyTick={tEnemy / N:F2}ms weapons={tWeapons / N:F2}ms " +
                      $"projectiles={tProj / N:F2}ms draw(cull+submit)={tDraw / N:F2}ms visibleInstances={renderer.LastFrameInstances}");
        }

        debug.GodMode = godBefore;
        Assert.Pass();
    }
}
