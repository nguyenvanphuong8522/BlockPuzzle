using System.Collections.Generic;
using UnityEngine;

public class Board : Singleton<Board>
{
    public ScoreManager ScoreManager;

    public GameObject CellPrefab;
    public int Row;
    public int Col;

    public CellRender[,] Cells;

    public int[,] Data;

    public override void Awake()
    {
        base.Awake();
        Application.targetFrameRate = 60;
    }
    private void Start()
    {
        Data = new int[Row, Col];
        Cells = new CellRender[Row, Col];
        InitCell();
        HideAllCell();
    }
    public void ResetData()
    {
        Data = new int[Row, Col];
        HideAllCell();
        ScoreManager.ResetScore();
    }
    bool IsRowFull(int y)
    {
        for (int x = 0; x < Row; x++)
        {
            if (Data[x, y] == 0)
                return false;
        }

        return true;
    }
    bool IsColumnFull(int x)
    {
        for (int y = 0; y < Col; y++)
        {
            if (Data[x, y] == 0)
                return false;
        }

        return true;
    }
    public HashSet<Vector2Int> GetCellsSameRowCol(Vector2Int pos, List<Vector2Int> placedCells)
    {
        HashSet<int> rows = new HashSet<int>();
        HashSet<int> cols = new HashSet<int>();
        HashSet<Vector2Int> cellsToClear = new HashSet<Vector2Int>();

        foreach (var c in placedCells)
        {
            int x = pos.x + c.y;
            int y = pos.y + c.x;
            rows.Add(y);

            cols.Add(x);
        }
        foreach (int y in rows)
        {
            if (IsRowFull(y))
            {
                for (int x = 0; x < Row; x++)
                {
                    cellsToClear.Add(new Vector2Int(x, y));
                }
            }
        }

        foreach (int x in cols)
        {
            if (IsColumnFull(x))
            {
                for (int y = 0; y < Col; y++)
                {
                    cellsToClear.Add(new Vector2Int(x, y));
                }
            }
        }
        return cellsToClear;
    }
    public void CheckAndClear(Vector2Int pos, List<Vector2Int> placedCells)
    {
        HashSet<Vector2Int> cellsToClear = GetCellsSameRowCol(pos, placedCells);

        foreach (var posd in cellsToClear)
        {
            HideCell(posd.x, posd.y);
        }
        ScoreManager.AddClearScore(cellsToClear.Count);
    }
    public void Place(Sprite sprite,Vector2Int pos, List<Vector2Int> cells)
    {
        foreach (var c in cells)
        {
            int x = pos.x + c.y;
            int y = pos.y + c.x;

            Data[x, y] = 1;
            Cells[x, y].SpritePlaced = sprite;
            ShowCellNormal(x, y);
        }
    }
    public void Hover(Sprite sprite, Vector2Int pos, List<Vector2Int> cells)
    {
        foreach (var c in cells)
        {
            int x = pos.x + c.y;
            int y = pos.y + c.x;

            Data[x, y] = 2;
            Cells[x, y].UpdateSprite(sprite);
            Cells[x, y].ShowBlur();
        }
        CheckHighLight(sprite,pos, cells);
    }

    private void CheckHighLight(Sprite sprite,Vector2Int pos, List<Vector2Int> cells)
    {
        HashSet<Vector2Int> cellsToClear = GetCellsSameRowCol(pos, cells);

        foreach (var posd in cellsToClear)
        {
            ShowCellHighLight(sprite,posd.x, posd.y);
        }
    }
    public bool CanPlace(Vector2Int pos, List<Vector2Int> cells)
    {
        foreach (var c in cells)
        {
            int x = pos.x + c.y;
            int y = pos.y + c.x;

            if (x < 0 || y < 0 || x >= Row || y >= Col)
                return false;

            if (Data[x, y] == 1)
                return false;
        }

        return true;
    }

    private void InitCell()
    {
        for (int i = 0; i < Row; i++)
        {
            for (int j = 0; j < Col; j++)
            {
                GameObject newCell = Instantiate(CellPrefab, transform);
                newCell.name = $"Cell{i}{j}";
                int x = j;
                int y = Row - 1 - i;
                Vector3 putPosition = new Vector3(x, y, 0);
                newCell.transform.position = putPosition;


                if (newCell.TryGetComponent(out CellRender cellRender))
                {
                    Cells[i, j] = newCell.GetComponent<CellRender>();
                }
            }
        }
    }

    private void HideAllCell()
    {
        foreach (CellRender cell in Cells)
        {
            cell.Hide();
            cell.HideEffectShine();
        }
    }

    public void HideAllCellHover()
    {
        for (int i = 0; i < Row; i++)
        {
            for (int j = 0; j < Col; j++)
            {
                if (Data[i, j] == 2)
                {
                    Data[i, j] = 0;
                    HideCell(i, j);
                }
                if (Data[i, j] == 1)
                {
                    SetSpritePlaced(i, j);
                    ShowCellNormal(i, j);
                    HideEffectShine(i, j);
                }
            }
        }
    }
    public void HideCell(int x, int y)
    {
        Data[x, y] = 0;
        Cells[x, y].Hide();
        Cells[x, y].HideEffectShine();
    }

    public void ShowCellNormal(int x, int y)
    {
        Cells[x, y].ShowNormal();
    }

    public void HideEffectShine(int x, int y)
    {
        Cells[x, y].HideEffectShine();
    }

    public void SetSpritePlaced(int x, int y)
    {
        Cells[x, y].UpdateSprite(Cells[x, y].SpritePlaced);
    }

    public void ShowCellHighLight(Sprite sprite,int x, int y)
    {
        Cells[x, y].UpdateSprite(sprite);
        Cells[x, y].ShowNormal();
        Cells[x, y].ShowEffectShine();
    }

    public bool CheckLose(List<Vector2Int> cells)
    {
        for (int i = 0; i < Row; i++)
        {
            for (int j = 0; j < Col; j++)
            {
                if (Data[i, j] == 0)
                {
                    if(CanPlace(new Vector2Int(i, j), cells))
                    {
                        return false;
                    }
                }
            }
        }
        return true;
    }
}
