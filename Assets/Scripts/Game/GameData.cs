using System.Collections.Generic;
using UnityEngine;

public class GameData : MonoBehaviour
{
    public DataBlocks DataBlocks;

    public static GameData Instance;

    public List<Sprite> ListOfSkin;

    public Sprite GrayBlock;
    private void Awake()
    {
        Instance = this;
    }
}
