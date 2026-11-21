using System;
using System.IO;
using System.Text;
using UnityEngine;
using VContainer.Unity;

namespace Survivor
{
    // Performance test in a real build:  BlockPuzzle.exe -benchmark results.txt
    // Raises the enemy cap, fills the map to 2k / 5k / 10k / 20k enemies around an invincible player,
    // measures frame time with vsync and the frame cap off, writes the results and quits.
    // Does nothing without that argument.
    public sealed class Benchmark : ITickable
    {
        static readonly int[] Targets = { 2000, 5000, 10000, 20000 };
        const float SettleSeconds = 4f;   // let the crowd reach the player before measuring
        const int MeasureFrames = 180;

        readonly string path;
        readonly GameConfig config;
        readonly EnemySystem enemies;
        readonly EnemySpawner spawner;
        readonly UpgradeService upgrades;
        readonly GameController game;
        readonly DebugSettings debug;
        readonly InstancedRenderer renderer;
        readonly StringBuilder report = new StringBuilder();

        int step = -1;
        float settle;
        int frames;
        double frameMs, simMs, drawMs, worstMs;

        public Benchmark(GameConfig config, EnemySystem enemies, EnemySpawner spawner, UpgradeService upgrades,
            GameController game, DebugSettings debug, InstancedRenderer renderer)
        {
            this.config = config;
            this.enemies = enemies;
            this.spawner = spawner;
            this.upgrades = upgrades;
            this.game = game;
            this.debug = debug;
            this.renderer = renderer;
            var args = Environment.GetCommandLineArgs();
            var i = Array.IndexOf(args, "-benchmark");
            if (i >= 0 && i + 1 < args.Length) path = args[i + 1];
        }

        public void Tick()
        {
            if (path == null) return;
            if (game.IsChoosing) game.ChooseUpgrade(0);

            if (step < 0)
            {
                QualitySettings.vSyncCount = 0;
                Application.targetFrameRate = -1; // uncapped, so we see the real cost
                debug.SessionGodMode = true;
                config.MaxEnemies = 25000;
                upgrades.GiveAllWeaponsMaxed();
                report.AppendLine($"Survivor benchmark  {DateTime.Now:yyyy-MM-dd HH:mm}");
                report.AppendLine($"CPU: {SystemInfo.processorType} ({SystemInfo.processorCount} threads)");
                report.AppendLine($"GPU: {SystemInfo.graphicsDeviceName}   Screen: {Screen.width}x{Screen.height}");
                report.AppendLine();
                report.AppendLine("enemies | avg FPS | avg frame ms | worst frame ms | logic ms | draw-submit ms | visible shapes | draw calls");
                step = 0;
                return;
            }

            var target = Targets[step];
            spawner.SpawnBurst(target - enemies.Count); // keep topping up: weapons keep killing

            if (settle < SettleSeconds)
            {
                settle += Time.unscaledDeltaTime;
                return;
            }

            var dt = Time.unscaledDeltaTime * 1000.0;
            frameMs += dt;
            worstMs = Math.Max(worstMs, dt);
            simMs += game.SimulationMs;
            drawMs += game.RenderMs;
            frames++;
            if (frames < MeasureFrames) return;

            report.AppendLine($"{enemies.Count,7} | {1000.0 / (frameMs / frames),7:0} | {frameMs / frames,12:0.00} | " +
                              $"{worstMs,14:0.00} | {simMs / frames,8:0.00} | {drawMs / frames,14:0.00} | " +
                              $"{renderer.LastFrameInstances,14} | {renderer.LastFrameDrawCalls,10}");

            step++;
            settle = 0f;
            frames = 0;
            frameMs = simMs = drawMs = worstMs = 0;
            if (step < Targets.Length) return;

            File.WriteAllText(path, report.ToString());
            Application.Quit();
        }
    }
}
