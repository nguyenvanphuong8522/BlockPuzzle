using TMPro;
using UnityEngine;

public class UIInGame : MonoBehaviour
{
    [SerializeField]
    private TextMeshProUGUI _txtHighScore;

    [SerializeField] 
    private TextMeshProUGUI _txtCurScore;

    public void UpdateScore(string value)
    {
        _txtCurScore.SetText(value);
    }
    public void UpdateHighScore(string value)
    {
        _txtHighScore.SetText(value);
    }

}
