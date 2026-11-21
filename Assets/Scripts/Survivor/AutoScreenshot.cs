using System;
using UnityEngine;
using VContainer.Unity;

namespace Survivor
{
    // Automated visual check: run the player with  -autoshot <file.png>  and it spawns a crowd and a boss,
    // takes a screenshot after a few seconds, then quits. Does nothing without that argument.
    // Add -pause to capture the pause menu instead.
    public sealed class AutoScreenshot : ITickable
    {
        readonly string path;
        readonly EnemySpawner spawner;
        readonly UpgradeService upgrades;
        readonly GameController game;
        readonly DebugSettings debug;
        float time;
        bool prepared;
        bool captured;

        public AutoScreenshot(EnemySpawner spawner, UpgradeService upgrades, GameController game, DebugSettings debug)
        {
            this.spawner = spawner;
            this.upgrades = upgrades;
            this.game = game;
            this.debug = debug;
            var args = Environment.GetCommandLineArgs();
            var i = Array.IndexOf(args, "-autoshot");
            if (i >= 0 && i + 1 < args.Length) path = args[i + 1];
        }

        public void Tick()
        {
            if (path == null) return;
            time += Time.unscaledDeltaTime;
            if (game.IsChoosing) game.ChooseUpgrade(0);

            if (!prepared && time > 1f)
            {
                prepared = true;
                debug.SessionGodMode = true;
                upgrades.GiveAllWeaponsMaxed();
                spawner.SpawnBurst(600);
                spawner.SpawnBoss();
            }
            if (!captured && time > 9f)
            {
                if (Array.IndexOf(Environment.GetCommandLineArgs(), "-pause") >= 0) game.Pause(); // screenshot the pause menu
                captured = true;
                ScreenCapture.CaptureScreenshot(path);
            }
            if (time > 11f) Application.Quit();
        }
    }
}
