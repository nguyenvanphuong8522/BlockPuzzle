using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(menuName = "Data/Shape")]
public class ShapeData : ScriptableObject
{
    public List<Vector2Int> cells = new List<Vector2Int>();
}