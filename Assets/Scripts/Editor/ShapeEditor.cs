using UnityEditor;
using UnityEngine;

public class ShapeEditor : EditorWindow
{
    bool[,] grid = new bool[5, 5];

    int size = 5;

    ShapeData currentShape;

    [MenuItem("Tools/Shape Editor")]
    static void Open()
    {
        GetWindow<ShapeEditor>();
    }

    void OnGUI()
    {
        GUILayout.Space(10);

        currentShape = (ShapeData)EditorGUILayout.ObjectField(currentShape, typeof(ShapeData), false);

        GUILayout.Space(10);

        DrawGrid();

        GUILayout.Space(10);
        GUIStyle buttonStyle = new GUIStyle(GUI.skin.button);
        buttonStyle.fontSize = 16;
        buttonStyle.fixedHeight = 40;
        if (GUILayout.Button("Clear", buttonStyle))
        {
            grid = new bool[size, size];
        }

        if (GUILayout.Button("Save", buttonStyle))
        {
            SaveShape();
        }
    }
    void DrawShape(Vector2Int[] cells)
    {
        grid = new bool[size, size];

        foreach (var cell in cells)
        {
            int x = cell.x;
            int y = cell.y;

            // nếu grid editor bị đảo Y thì phải flip:
            int drawY = size - 1 - y;

            if (x >= 0 && x < size &&
               drawY >= 0 && drawY < size)
            {
                grid[x, drawY] = true;
            }
        }
    }

    void DrawGrid()
    {
        for (int y = size - 1; y >= 0; y--)
        {
            GUILayout.BeginHorizontal();

            for (int x = 0; x < size; x++)
            {
                Color old = GUI.backgroundColor;

                GUI.backgroundColor = grid[x, y] ? Color.white : Color.gray;

                if (GUILayout.Button("", GUILayout.Width(40), GUILayout.Height(40)))
                {
                    grid[x, y] = !grid[x, y];
                }

                GUI.backgroundColor = old;
            }

            GUILayout.EndHorizontal();
        }
    }

    void SaveShape()
    {
        if (currentShape == null)
            return;

        currentShape.cells.Clear();

        for (int x = 0; x < size; x++)
        {
            for (int y = 0; y < size; y++)
            {
                if (grid[x, y])
                {
                    currentShape.cells.Add(new Vector2Int(x - 2, y - 2));
                }
            }
        }

        EditorUtility.SetDirty(currentShape);

        AssetDatabase.SaveAssets();
    }
}