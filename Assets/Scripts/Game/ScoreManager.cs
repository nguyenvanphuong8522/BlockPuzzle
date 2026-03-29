using UnityEngine;

public class ScoreManager : MonoBehaviour
{
    public UIInGame UiInGame;
    public int CurrentScore { get; private set; }
    public int HighScore { get; private set; }

    private void Awake()
    {
        LoadHighScore();
    }

    public void AddScore(int value)
    {
        CurrentScore += value;
        UiInGame.UpdateScore(CurrentScore.ToString());
        if (CurrentScore > HighScore)
        {
            HighScore = CurrentScore;
            SaveHighScore();
            UiInGame.UpdateHighScore(HighScore.ToString());
        }
    }

    public void AddClearScore(int clearedCells)
    {
        int score = clearedCells * 10;

        AddScore(score);
    }

    public void ResetScore()
    {
        CurrentScore = 0;
        UiInGame.UpdateScore(CurrentScore.ToString());
    }

    void SaveHighScore()
    {
        PlayerPrefs.SetInt("HIGH_SCORE", HighScore);
        PlayerPrefs.Save();
    }

    void LoadHighScore()
    {
        HighScore = PlayerPrefs.GetInt("HIGH_SCORE", 0);
        UiInGame.UpdateHighScore(HighScore.ToString());

    }
}
