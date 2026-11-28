using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace Survivor
{
    // Settings from the Home screen: music / sound-effect volume and the language (only changed here, not in a run).
    public sealed class SettingsPopup : BasePopup
    {
        [SerializeField] Slider musicSlider;
        [SerializeField] TextMeshProUGUI musicPercent;
        [SerializeField] Slider sfxSlider;
        [SerializeField] TextMeshProUGUI sfxPercent;
        [Tooltip("Language row: tap = next language; the arrows step back / forward.")]
        [SerializeField] TapButton languageButton;
        [SerializeField] TextMeshProUGUI languageLabel;
        [SerializeField] TapButton languagePrevious;
        [SerializeField] TapButton languageNext;
        [SerializeField] TapButton closeButton;

        AudioService audio;

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
            languageButton.OnPressed = () => Loc.CycleLanguage();
            if (languagePrevious != null) languagePrevious.OnPressed = () => Loc.CycleLanguage(-1);
            if (languageNext != null) languageNext.OnPressed = () => Loc.CycleLanguage();
            closeButton.OnPressed = Close;
            Loc.Changed += RefreshLanguage;
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            Loc.Changed -= RefreshLanguage;
        }

        protected override void OnShow(object data)
        {
            musicSlider.SetValueWithoutNotify(audio.MusicVolume);
            musicPercent.text = Percent(audio.MusicVolume);
            sfxSlider.SetValueWithoutNotify(audio.SfxVolume);
            sfxPercent.text = Percent(audio.SfxVolume);
            RefreshLanguage();
        }

        protected override void OnHide() => audio.SaveSettings();

        void RefreshLanguage()
        {
            var language = Loc.CurrentLanguage;
            if (languageLabel != null && language != null) languageLabel.text = language.NativeName;
        }

        static string Percent(float v) => $"{Mathf.RoundToInt(v * 100)}%";
    }
}
