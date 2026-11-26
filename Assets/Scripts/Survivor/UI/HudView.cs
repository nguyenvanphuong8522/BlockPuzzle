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
        [Tooltip("Hide the clock during a boss fight (it is stopped then) and show it again once the boss is dead.")]
        [SerializeField] bool hideTimeDuringBoss = true;
        [SerializeField] TextMeshProUGUI killText;
        [SerializeField] TextMeshProUGUI debugText;
        [SerializeField] TapButton pauseButton;

        [Header("Boss")]
        [SerializeField] GameObject bossBar;
        [SerializeField] Image bossFill;
        [SerializeField] TextMeshProUGUI bossName;
        [Tooltip("Boss picture on the boss health bar (optional).")]
        [SerializeField] Image bossBarIcon;
        [Tooltip("Hidden during a boss fight (the boss health bar takes its place).")]
        [SerializeField] GameObject xpBar;
        [Tooltip("Boss icon + time left before the next boss, under the XP bar.")]
        [SerializeField] GameObject bossCountdown;
        [SerializeField] Image bossCountdownIcon;
        [SerializeField] TextMeshProUGUI bossCountdownText;
        [Tooltip("\"Boss coming\" banner, shown the last PlayerTuning > Boss Warning Seconds before a boss.")]
        [SerializeField] CanvasGroup bossWarning;
        [SerializeField] TextMeshProUGUI bossWarningText;
        [Tooltip("Boss icons on the warning banner (optional).")]
        [SerializeField] Image[] bossWarningIcons = new Image[0];
        [Tooltip("Banner blink speed.")]
        [SerializeField] float warningBlinkSpeed = 7f;
        [Tooltip("Picture of the boss. Empty = first frame of the boss enemy art.")]
        [SerializeField] Sprite bossIcon;

        [Header("Cảnh báo đàn bay")]
        [Tooltip("Warning shown on the screen edge a swarm comes from (moved and turned by the code).")]
        [SerializeField] RectTransform swarmWarning;
        [Tooltip("Arrow inside it, turned to point at the swarm.")]
        [SerializeField] RectTransform swarmArrow;
        [Tooltip("Distance from the screen edges (pixels of the 1080x1920 layout): x = sides, y = bottom, z = top.")]
        [SerializeField] Vector3 swarmEdgeMargin = new Vector3(110f, 160f, 330f);
        [Tooltip("Blink speed of the warning (it blinks faster as the swarm gets close).")]
        [SerializeField] float swarmBlinkSpeed = 8f;

        [Header("Controls")]
        [SerializeField] VirtualJoystick joystick;

        [Header("Texts")]
        [SerializeField] string levelFormat = "Cấp {0}";
        [SerializeField] string killFormat = "Hạ gục: {0}";
        [Tooltip("{0} = seconds left before the boss appears.")]
        [SerializeField] string bossWarningFormat = "Boss xuất hiện sau {0} giây!";

        readonly List<IDisposable> subscriptions = new List<IDisposable>();
        PlayerProgress progress;
        int shownSeconds = -1;
        int shownBossSeconds = -1;
        float warningShownAt = -1f;
        PlayerTuning tuning;

        public event Action PauseClicked;

        [Inject]
        public void Construct(InputService input, PlayerProgress playerProgress, GameStats stats, PlayerTuning playerTuning,
            SpriteCatalog sprites, GameConfig config)
        {
            tuning = playerTuning;
            if (bossIcon == null && config.BossType < sprites.Enemies.Length && sprites.Enemies[config.BossType] is { IsEmpty: false } art)
                bossIcon = art.Frames[0];
            foreach (var image in new[] { bossBarIcon, bossCountdownIcon }) SetIcon(image);
            foreach (var image in bossWarningIcons) SetIcon(image);
            if (bossCountdown != null) bossCountdown.SetActive(false);
            if (bossWarning != null) bossWarning.gameObject.SetActive(false);
            if (swarmWarning != null) swarmWarning.gameObject.SetActive(false);
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
            if (xpBar != null && xpBar.activeSelf == visible) xpBar.SetActive(!visible); // boss bar replaces the XP bar
            var showTime = !(visible && hideTimeDuringBoss);
            if (timeText.gameObject.activeSelf != showTime) timeText.gameObject.SetActive(showTime);
            if (!visible) return;
            bossFill.fillAmount = ratio;
            if (bossName.text != name) bossName.text = name;
        }

        void SetIcon(Image image)
        {
            if (image == null) return;
            image.sprite = bossIcon;
            image.enabled = bossIcon != null;
            image.preserveAspect = true;
        }

        // Seconds left before the next boss; < 0 = hide (boss fight, or no boss left).
        // Shows the icon + mm:ss under the XP bar, and the warning banner in the last Boss Warning Seconds.
        // Swarm warning: `direction` = where the swarm comes from, seen from the hero (screen and world axes match).
        // Placed on the screen edge in that direction, arrow pointing at it; blinks faster as `progress` nears 1.
        public void SetSwarmWarning(bool active, Vector2 direction, float progress)
        {
            if (swarmWarning == null) return;
            if (swarmWarning.gameObject.activeSelf != active) swarmWarning.gameObject.SetActive(active);
            if (!active || direction.sqrMagnitude < 0.0001f) return;

            var dir = direction.normalized;
            var area = ((RectTransform)swarmWarning.parent).rect;
            // Ray from the screen center to the inset edge of the screen.
            var halfX = area.width * 0.5f - swarmEdgeMargin.x;
            var top = area.height * 0.5f - swarmEdgeMargin.z;
            var bottom = area.height * 0.5f - swarmEdgeMargin.y;
            var halfY = dir.y >= 0f ? top : bottom;
            var tx = Mathf.Abs(dir.x) > 0.0001f ? halfX / Mathf.Abs(dir.x) : float.MaxValue;
            var ty = Mathf.Abs(dir.y) > 0.0001f ? halfY / Mathf.Abs(dir.y) : float.MaxValue;
            var t = Mathf.Min(tx, ty);
            swarmWarning.anchorMin = swarmWarning.anchorMax = new Vector2(0.5f, 0.5f);
            swarmWarning.anchoredPosition = area.center + dir * t;
            if (swarmArrow != null)
                swarmArrow.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);

            var speed = swarmBlinkSpeed * (1f + progress * 1.5f);
            var k = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * speed);
            swarmWarning.localScale = Vector3.one * (0.92f + 0.16f * k);
            var group = swarmWarning.GetComponent<CanvasGroup>();
            if (group != null) group.alpha = 0.6f + 0.4f * k;
        }

        public void SetBossCountdown(float secondsLeft)
        {
            var visible = secondsLeft >= 0f;
            if (bossCountdown != null)
            {
                if (bossCountdown.activeSelf != visible) bossCountdown.SetActive(visible);
                if (visible)
                {
                    var s = Mathf.CeilToInt(secondsLeft);
                    if (s != shownBossSeconds)
                    {
                        shownBossSeconds = s;
                        bossCountdownText.text = $"{s / 60:00}:{s % 60:00}";
                    }
                }
            }

            if (bossWarning == null) return;
            var warn = visible && tuning != null && secondsLeft <= tuning.BossWarningSeconds;
            if (bossWarning.gameObject.activeSelf != warn)
            {
                bossWarning.gameObject.SetActive(warn);
                warningShownAt = Time.unscaledTime;
            }
            if (!warn) return;

            if (bossWarningText != null)
                bossWarningText.text = string.Format(bossWarningFormat, Mathf.CeilToInt(secondsLeft));
            // Pops in, then blinks.
            var age = Time.unscaledTime - warningShownAt;
            var pop = age < 0.25f ? Mathf.Lerp(1.6f, 1f, age / 0.25f) : 1f;
            bossWarning.transform.localScale = new Vector3(1f, pop, 1f);
            bossWarning.alpha = 0.55f + 0.45f * (0.5f + 0.5f * Mathf.Cos(age * warningBlinkSpeed));
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
