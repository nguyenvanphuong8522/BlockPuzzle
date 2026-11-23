using System;
using TMPro;
using UnityEngine;

namespace Survivor
{
    public sealed class ResultData
    {
        public bool Survived;
        public float Seconds;
        public int Kills;
        public int Level;
        public Action OnRestart;
    }

    // End-of-run screen.
    public sealed class ResultPopup : BasePopup
    {
        [SerializeField] TextMeshProUGUI title;
        [SerializeField] TextMeshProUGUI stats;
        [SerializeField] TapButton restartButton;
        [SerializeField] string survivedText = "SỐNG SÓT!";
        [SerializeField] string defeatedText = "BẠN ĐÃ GỤC NGÃ";
        [SerializeField] Color survivedColor = new Color(1f, 0.85f, 0.3f);
        [SerializeField] Color defeatedColor = new Color(0.9f, 0.4f, 0.4f);

        protected override void OnShow(object data)
        {
            var d = (ResultData)data;
            title.text = d.Survived ? survivedText : defeatedText;
            title.color = d.Survived ? survivedColor : defeatedColor;
            var s = Mathf.FloorToInt(d.Seconds);
            stats.text = $"Thời gian: {s / 60:00}:{s % 60:00}\nHạ gục: {d.Kills}\nCấp: {d.Level}";
            restartButton.OnPressed = d.OnRestart;
        }
    }
}
