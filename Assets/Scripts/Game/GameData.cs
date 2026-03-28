using UnityEngine;

public class GameData : MonoBehaviour
{
    public DataBlocks DataBlocks;

    public static GameData Instance;

    private void Awake()
    {
        Instance = this;
    }
}
