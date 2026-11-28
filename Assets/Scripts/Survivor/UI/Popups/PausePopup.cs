using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace Survivor
{
    public sealed class PauseData
    {
        public Action OnResume;
        public Action OnRestart;
    }

    // Pause menu: music / sound-effect volume sliders, Resume, Restart.
    // Gamepad / arrows: up/down picks a row, left/right moves a slider, Submit presses a button.
    public sealed class PausePopup : BasePopup
    {
        [SerializeField] Slider musicSlider;
        [SerializeField] TextMeshProUGUI musicPercent;
        [SerializeField] Slider sfxSlider;
        [SerializeField] TextMeshProUGUI sfxPercent;
        [SerializeField] TapButton resumeButton;
        [SerializeField] TapButton restartButton;
        [Tooltip("Language row (bottom): pressing it or left / right switches language. Made by Survivor > Localization > Setup.")]
        [SerializeField] TapButton languageButton;
        [SerializeField] TextMeshProUGUI languageLabel;
        [Tooltip("Rows in navigation order: music, sfx, resume, restart, language. Each needs an Outline (the highlight).")]
        [SerializeField] Outline[] rows;
        [SerializeField] float sliderStep = 0.1f;

        AudioService audio;
        PauseData data;
        int selected;

        [Inject]
        public void Construct(AudioService audioService)
        {
            audio = audioService;
            musicSlider.onValueChanged.AddListener(v =>
            {
                audio.MusicVolume = v;
                musicPercent.text = Percent(v);
            });
            sfxSlider.onValueChanged.AddListener(v =>
            {
                audio.SfxVolume = v;
                sfxPercent.text = Percent(v);
                audio.Play(Sfx.Gem, 1f, 0f, 0.08f); // sample so you hear the new level
            });
            resumeButton.OnPressed = () => data?.OnResume?.Invoke();
            restartButton.OnPressed = () => data?.OnRestart?.Invoke();
            if (languageButton != null) languageButton.OnPressed = () => Loc.CycleLanguage();
            Loc.Changed += RefreshLanguage;
        }

        const int LanguageRow = 4;

        protected override void OnDestroy()
        {
            base.OnDestroy();
            Loc.Changed -= RefreshLanguage;
        }

        void RefreshLanguage()
        {
            var language = Loc.CurrentLanguage;
            if (languageLabel != null && language != null) languageLabel.text = Loc.Format("pause.language", language.NativeName);
        }

        protected override void OnShow(object d)
        {
            data = (PauseData)d;
            musicSlider.SetValueWithoutNotify(audio.MusicVolume);
            musicPercent.text = Percent(audio.MusicVolume);
            sfxSlider.SetValueWithoutNotify(audio.SfxVolume);
            sfxPercent.text = Percent(audio.SfxVolume);
            RefreshLanguage();
            Select(0);
        }

        protected override void OnHide() => audio.SaveSettings();

        public void HandleNavigation(int vertical, int horizontal, bool submit)
        {
            if (vertical != 0) Select((selected - vertical + rows.Length) % rows.Length); // up = previous row
            if (horizontal != 0)
            {
                if (selected == 0) musicSlider.value = Step(musicSlider.value, horizontal);
                else if (selected == 1) sfxSlider.value = Step(sfxSlider.value, horizontal);
                else if (selected == LanguageRow) Loc.CycleLanguage(horizontal);
            }
            if (submit)
            {
                if (selected == 2) data?.OnResume?.Invoke();
                else if (selected == 3) data?.OnRestart?.Invoke();
                else if (selected == LanguageRow) Loc.CycleLanguage();
            }
        }

        float Step(float value, int direction) =>
            Mathf.Clamp01(Mathf.Round((value + direction * sliderStep) / sliderStep) * sliderStep);

        void Select(int row)
        {
            selected = row;
            for (var i = 0; i < rows.Length; i++) rows[i].enabled = i == row;
        }

        static string Percent(float v) => $"{Mathf.RoundToInt(v * 100)}%";
    }
}
