using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace Survivor
{
    public sealed class HomeData
    {
        public Action OnPlay;    // energy is already paid and the chapter picked
    }

    // First screen: energy (top left), settings (top right), chapter card in the middle (arrows / swipe to change,
    // locked until the previous chapter is survived), Play at the bottom (costs energy).
    public sealed class HomePopup : BasePopup
    {
        [Header("Năng lượng")]
        [SerializeField] TextMeshProUGUI energyText;
        [SerializeField] TextMeshProUGUI energyTimer;
        [SerializeField] TapButton settingsButton;

        [Header("Chương")]
        [SerializeField] Image card;
        [SerializeField] TextMeshProUGUI chapterNumber;
        [SerializeField] TextMeshProUGUI chapterName;
        [SerializeField] Image preview;
        [SerializeField] TextMeshProUGUI chapterInfo;
        [SerializeField] TextMeshProUGUI pageText;
        [SerializeField] GameObject lockOverlay;
        [SerializeField] TapButton previousButton;
        [SerializeField] TapButton nextButton;
        [SerializeField] SwipeArea swipe;

        [Header("Chơi")]
        [SerializeField] TapButton playButton;
        [SerializeField] Image playBackground;
        [SerializeField] TextMeshProUGUI playCost;
        [SerializeField] TextMeshProUGUI message;
        [SerializeField] Color playColor = new Color(1f, 0.72f, 0.15f);
        [SerializeField] Color playDisabledColor = new Color(0.45f, 0.45f, 0.5f);
        [SerializeField] float messageTime = 2f;

        EnergyService energy;
        ChapterService chapters;
        AudioService audio;
        HomeData data;
        int index;
        float refreshTimer, messageTimer;

        [Inject]
        public void Construct(EnergyService energyService, ChapterService chapterService, AudioService audioService)
        {
            energy = energyService;
            chapters = chapterService;
            audio = audioService;
            settingsButton.OnPressed = () =>
            {
                audio.Play(Sfx.Click, 1f, 0f, 0f);
                Controller.Show<SettingsPopup>();
            };
            previousButton.OnPressed = () => Step(-1);
            nextButton.OnPressed = () => Step(1);
            if (swipe != null) swipe.Swiped = Step;
            playButton.OnPressed = Play;
            Loc.Changed += Refresh;
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            Loc.Changed -= Refresh;
        }

        protected override void OnShow(object d)
        {
            data = d as HomeData;
            index = chapters.SelectedIndex;
            message.text = "";
            Refresh();
        }

        void Update()
        {
            if (!IsShown) return;
            refreshTimer -= Time.unscaledDeltaTime;
            if (refreshTimer <= 0f)
            {
                refreshTimer = 0.25f;
                RefreshEnergy();
            }
            if (messageTimer > 0f)
            {
                messageTimer -= Time.unscaledDeltaTime;
                var c = message.color;
                c.a = Mathf.Clamp01(messageTimer / 0.4f);
                message.color = c;
            }
        }

        void Step(int direction)
        {
            var next = Mathf.Clamp(index + direction, 0, Mathf.Max(0, chapters.Count - 1));
            if (next == index) return;
            index = next;
            audio.Play(Sfx.Click, 0.6f, 0f, 0f);
            Refresh();
        }

        void Play()
        {
            if (!chapters.IsUnlocked(index))
            {
                ShowMessage(Loc.Get("home.locked"));
                return;
            }
            if (!energy.TryConsume(chapters.CostOf(index)))
            {
                ShowMessage(Loc.Get("home.no_energy"));
                return;
            }
            audio.Play(Sfx.Click, 1f, 0f, 0f);
            chapters.Select(index);
            data?.OnPlay?.Invoke();
        }

        void ShowMessage(string text)
        {
            message.text = text;
            messageTimer = messageTime;
            audio.Play(Sfx.Click, 0.5f, 0f, 0f);
        }

        void Refresh()
        {
            if (chapters == null) return;
            var c = chapters.Get(index);
            var open = chapters.IsUnlocked(index);
            card.color = c.CardColor;
            chapterNumber.text = Loc.Format("home.chapter", index + 1);
            chapterName.text = c.DisplayName;
            preview.sprite = c.Preview;
            preview.enabled = c.Preview != null;
            preview.color = open ? Color.white : new Color(0.15f, 0.15f, 0.2f, 1f);
            preview.preserveAspect = true;
            lockOverlay.SetActive(!open);
            var best = chapters.BestSeconds(index);
            chapterInfo.text = !open ? Loc.Get("home.locked")
                : chapters.IsCleared(index) ? Loc.Get("home.cleared") + "\n" + Loc.Format("home.best", Clock(best))
                : best > 0f ? Loc.Format("home.best", Clock(best)) : Loc.Get("home.best_none");
            pageText.text = $"{index + 1} / {Mathf.Max(1, chapters.Count)}";
            previousButton.gameObject.SetActive(index > 0);
            nextButton.gameObject.SetActive(index < chapters.Count - 1);
            playCost.text = "-" + chapters.CostOf(index);
            RefreshEnergy();
        }

        void RefreshEnergy()
        {
            var now = energy.Current;
            energyText.text = $"{now}/{energy.Max}";
            energyTimer.text = now >= energy.Max ? Loc.Get("home.energy_full") : Loc.Format("home.energy_next", Clock(energy.SecondsToNext));
            var canPlay = chapters.IsUnlocked(index) && now >= chapters.CostOf(index);
            playBackground.color = canPlay ? playColor : playDisabledColor;
        }

        static string Clock(float seconds)
        {
            var s = Mathf.CeilToInt(seconds);
            return $"{s / 60:00}:{s % 60:00}";
        }
    }
}
