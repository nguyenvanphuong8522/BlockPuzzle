using Sirenix.OdinInspector;
using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "DataBlocks", menuName = "Data/DataBlocks", order = 1)]
public class DataBlocks : ScriptableObject
{
    [ShowInInspector]
    public List<ListVector2Int> Datas;
}

[Serializable]
public class ListVector2Int
{
    public Vector2Int[] Array;
}
