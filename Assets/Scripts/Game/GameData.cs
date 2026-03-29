using System.Collections.Generic;
using UnityEngine;

public class GameData : MonoBehaviour
{
    public List<ShapeData> ShapeDatas;

    public static GameData Instance;

    public List<Sprite> ListOfSkin;

    public Sprite GrayBlock;
    private void Awake()
    {
        Instance = this;
    }
}
