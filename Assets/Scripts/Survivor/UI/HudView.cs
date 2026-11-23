using System;
using System.Collections.Generic;
using R3;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using VContainer;

namespace Survivor
{
    // In-game HUD prefab (Assets/Prefabs/UI/Hud.prefab): XP bar, level, timer, kills, debug line, boss bar,
    // pause button and the floating joystick. Created by VContainer from GameLifetimeScope's Hud Prefab.
    public sealed class HudView : MonoBehaviour
    {
        [Header("Top bar")]
        [SerializeField] Image xpFill;
        [SerializeField] TextMeshProUGUI levelText;
        [SerializeField] TextMeshProUGUI timeText;
        [SerializeField] TextMeshProUGUI killText;
        [SerializeField] TextMeshProUGUI debugText;
        [SerializeField] TapButton pauseButton;

        [Header("Boss")]
        [SerializeField] GameObject bossBar;
        [SerializeField] Image bossFill;
        [SerializeField] TextMeshProUGUI bossName;

        [Header("Controls")]
        [SerializeField] VirtualJoystick joystick;

        [Header("Texts")]
        [SerializeField] string levelFormat = "Cấp {0}";
        [SerializeField] string killFormat = "Hạ gục: {0}";

        readonly List<IDisposable> subscriptions = new List<IDisposable>();
        PlayerProgress progress;
        int shownSeconds = -1;

        public event Action PauseClicked;

        [Inject]
        public void Construct(InputService input, PlayerProgress playerProgress, GameStats stats)
        {
            progress = playerProgress;
            if (EventSystem.current == null)
                new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

            joystick.Input = input;
            pauseButton.OnPressed = () => PauseClicked?.Invoke();
            bossBar.SetActive(false);

            subscriptions.Add(progress.Xp.Subscribe(_ => RefreshXp()));
            subscriptions.Add(progress.XpToNext.Subscribe(_ => RefreshXp()));
            subscriptions.Add(progress.Level.Subscribe(l => levelText.text = string.Format(levelFormat, l)));
            subscriptions.Add(stats.Kills.Subscribe(k => killText.text = string.Format(killFormat, k)));
        }

        void RefreshXp() =>
            xpFill.fillAmount = progress.XpToNext.Value > 0 ? (float)progress.Xp.Value / progress.XpToNext.Value : 0f;

        public void SetTime(float seconds)
        {
            var s = Mathf.FloorToInt(seconds);
            if (s == shownSeconds) return; // avoid rebuilding the text every frame
            shownSeconds = s;
            timeText.text = $"{s / 60:00}:{s % 60:00}";
        }

        public void SetDebug(string text) => debugText.text = text;

        // Shows the boss health bar while a boss is alive. ratio < 0 hides it.
        public void SetBoss(string name, float ratio)
        {
            var visible = ratio >= 0f;
            if (bossBar.activeSelf != visible) bossBar.SetActive(visible);
            if (!visible) return;
            bossFill.fillAmount = ratio;
            if (bossName.text != name) bossName.text = name;
        }

        // Called when a menu opens, so a finger held on the joystick does not keep the player walking.
        public void ReleaseJoystick() => joystick.Release();

        void OnDestroy()
        {
            foreach (var s in subscriptions) s.Dispose();
            subscriptions.Clear();
        }
    }
}
