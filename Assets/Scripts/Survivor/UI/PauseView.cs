using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Survivor
{
    // Pause button (top-right) and pause menu: music / sound-effect volume sliders, Resume, Restart.
    // Mouse/touch use the controls directly; with a gamepad or arrow keys:
    // up/down picks a row, left/right moves a slider, Submit (A / Enter) presses a button.
    public sealed class PauseView
    {
        const float SliderStep = 0.1f;

        readonly AudioService audio;
        readonly List<Image> rows = new List<Image>();
        readonly List<Action<int>> rowAdjust = new List<Action<int>>(); // left/right on a slider row
        readonly List<Action> rowSubmit = new List<Action>();           // Submit on a button row

        GameObject menu;
        Slider musicSlider;
        Slider sfxSlider;
        int selectedRow;

        public bool IsOpen => menu != null && menu.activeSelf;

        public PauseView(AudioService audio) => this.audio = audio;

        public void Build(Action onPauseButton, Action onResume, Action onRestart)
        {
            // Own canvas: above the HUD, below the debug panel.
            var canvasGo = new GameObject("PauseCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 50;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            var root = (RectTransform)canvasGo.transform;

            // Pause button, top-right corner.
            var button = Image(root, "PauseButton", UiSprites.Circle, new Color(0f, 0f, 0f, 0.55f));
            Place(button.rectTransform, new Vector2(1f, 1f), new Vector2(-70, -80), new Vector2(84, 84));
            // "II" icon
            var bar1 = Image(button.rectTransform, "Bar1", UiSprites.Square, Color.white);
            Place(bar1.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(-11, 0), new Vector2(12, 36));
            bar1.raycastTarget = false;
            var bar2 = Image(button.rectTransform, "Bar2", UiSprites.Square, Color.white);
            Place(bar2.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(11, 0), new Vector2(12, 36));
            bar2.raycastTarget = false;
            button.gameObject.AddComponent<TapButton>().OnPressed = onPauseButton;

            // Menu
            var overlay = Image(root, "PauseMenu", UiSprites.Square, new Color(0f, 0f, 0f, 0.7f));
            overlay.rectTransform.anchorMin = Vector2.zero;
            overlay.rectTransform.anchorMax = Vector2.one;
            overlay.rectTransform.sizeDelta = Vector2.zero;
            menu = overlay.gameObject;

            var panel = Image(overlay.rectTransform, "Panel", UiSprites.Square, new Color(0.1f, 0.11f, 0.16f, 0.97f));
            Place(panel.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(760, 640));
            var p = panel.rectTransform;

            Text(p, "TẠM DỪNG", 72, new Vector2(0, 240), new Vector2(700, 100)).color = new Color(1f, 0.85f, 0.3f);

            musicSlider = SliderRow(p, "Nhạc nền", new Vector2(0, 110), audio.MusicVolume, v => audio.MusicVolume = v);
            sfxSlider = SliderRow(p, "Âm thanh hiệu ứng", new Vector2(0, -10), audio.SfxVolume, v =>
            {
                audio.SfxVolume = v;
                audio.Play(Sfx.Gem, 1f, 0f, 0.08f); // sample so you hear the new level
            });
            ButtonRow(p, "Tiếp tục", new Vector2(0, -150), new Color(0.2f, 0.6f, 1f), onResume);
            ButtonRow(p, "Chơi lại", new Vector2(0, -250), new Color(0.45f, 0.45f, 0.5f), onRestart);

            menu.SetActive(false);
        }

        public void Show()
        {
            musicSlider.SetValueWithoutNotify(audio.MusicVolume);
            sfxSlider.SetValueWithoutNotify(audio.SfxVolume);
            menu.SetActive(true);
            Select(0);
        }

        public void Hide()
        {
            menu.SetActive(false);
            audio.SaveSettings();
        }

        // Gamepad / keyboard navigation while the menu is open.
        public void HandleNavigation(int vertical, int horizontal, bool submit)
        {
            if (vertical != 0) Select((selectedRow - vertical + rows.Count) % rows.Count); // up = previous row
            if (horizontal != 0) rowAdjust[selectedRow]?.Invoke(horizontal);
            if (submit) rowSubmit[selectedRow]?.Invoke();
        }

        void Select(int row)
        {
            selectedRow = row;
            for (var i = 0; i < rows.Count; i++)
                rows[i].GetComponent<Outline>().enabled = i == row;
        }

        Slider SliderRow(RectTransform parent, string label, Vector2 position, float value, Action<float> onChanged)
        {
            var row = Row(parent, position);
            Text(row.rectTransform, label, 34, new Vector2(0, 26), new Vector2(640, 46));

            // Slider built from code: background, fill and handle.
            var go = new GameObject("Slider", typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(row.rectTransform, false);
            Place(rt, new Vector2(0.5f, 0.5f), new Vector2(-30, -24), new Vector2(540, 28));
            var background = Image(rt, "Background", UiSprites.Square, new Color(1f, 1f, 1f, 0.15f));
            Stretch(background.rectTransform);
            var fillArea = new GameObject("Fill Area", typeof(RectTransform)).GetComponent<RectTransform>();
            fillArea.SetParent(rt, false);
            Stretch(fillArea);
            var fill = Image(fillArea, "Fill", UiSprites.Square, new Color(0.35f, 0.75f, 1f));
            Stretch(fill.rectTransform);
            var handleArea = new GameObject("Handle Area", typeof(RectTransform)).GetComponent<RectTransform>();
            handleArea.SetParent(rt, false);
            Stretch(handleArea);
            var handle = Image(handleArea, "Handle", UiSprites.Circle, Color.white);
            handle.rectTransform.sizeDelta = new Vector2(44, 44);

            var percent = Text(row.rectTransform, "", 30, new Vector2(285, -24), new Vector2(110, 40));

            var slider = go.AddComponent<Slider>();
            slider.fillRect = fill.rectTransform;
            slider.handleRect = handle.rectTransform;
            slider.targetGraphic = handle;
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.SetValueWithoutNotify(value);
            percent.text = $"{Mathf.RoundToInt(value * 100)}%";
            slider.onValueChanged.AddListener(v =>
            {
                percent.text = $"{Mathf.RoundToInt(v * 100)}%";
                onChanged(v);
            });

            rowAdjust.Add(step => slider.value = Mathf.Clamp01(Mathf.Round((slider.value + step * SliderStep) * 10f) / 10f));
            rowSubmit.Add(null);
            return slider;
        }

        void ButtonRow(RectTransform parent, string label, Vector2 position, Color color, Action onClick)
        {
            var row = Row(parent, position);
            row.color = color;
            row.rectTransform.sizeDelta = new Vector2(420, 80);
            Text(row.rectTransform, label, 40, Vector2.zero, new Vector2(420, 80));
            row.gameObject.AddComponent<TapButton>().OnPressed = onClick;
            rowAdjust.Add(null);
            rowSubmit.Add(onClick);
        }

        Image Row(RectTransform parent, Vector2 position)
        {
            var row = Image(parent, "Row", UiSprites.Square, new Color(1f, 1f, 1f, 0.04f));
            Place(row.rectTransform, new Vector2(0.5f, 0.5f), position, new Vector2(680, 110));
            var outline = row.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(1f, 0.85f, 0.3f);
            outline.effectDistance = new Vector2(4f, -4f);
            outline.enabled = false;
            rows.Add(row);
            return row;
        }

        static Image Image(RectTransform parent, string name, Sprite sprite, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            return image;
        }

        static TextMeshProUGUI Text(RectTransform parent, string text, float size, Vector2 position, Vector2 box)
        {
            var go = new GameObject("Text", typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            Place(rt, new Vector2(0.5f, 0.5f), position, box);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = size;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.raycastTarget = false;
            return tmp;
        }

        static void Place(RectTransform rt, Vector2 anchor, Vector2 position, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = anchor;
            rt.anchoredPosition = position;
            rt.sizeDelta = size;
        }

        static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.sizeDelta = Vector2.zero;
            rt.anchoredPosition = Vector2.zero;
        }
    }
}
