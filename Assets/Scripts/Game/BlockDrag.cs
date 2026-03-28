using DG.Tweening;
using Sirenix.OdinInspector;
using System.Collections.Generic;
using UnityEngine;

public class BlockDrag : MonoBehaviour
{

    private Vector3 _startPos;
    private Vector3 _startScale;

    private Vector3 _offset;

    private Camera _camera;

    private Vector3 _previousPosition;

    private Vector2Int _curCell;

    public Vector2Int[] Cells { get; set; }

    public GameObject PrefabBlock;

    public List<GameObject> BlockSpawned;
    public BlocksManager BlockManager;

    private void Awake()
    {
        _camera = Camera.main;
    }

    public void GrayBlocks()
    {
        foreach (var block in BlockSpawned)
        {
            block.GetComponent<SpriteRenderer>().color = Color.black;
        }
    }
    public void WhiteBlocks()
    {
        foreach (var block in BlockSpawned)
        {
            block.GetComponent<SpriteRenderer>().color = Color.white;
        }
    }
    [Button]
    public void RefreshBlocks()
    {
        foreach (var block in BlockSpawned)
        {
            Destroy(block);
        }
        BlockSpawned.Clear();
        int randomIndex = Random.Range(0, GameData.Instance.DataBlocks.Datas.Count);
        ListVector2Int newCells = GameData.Instance.DataBlocks.Datas[randomIndex];
        Cells = newCells.Array;
        for (int i = 0; i < Cells.Length; i++)
        {
            GameObject newCell = Instantiate(PrefabBlock, transform);
            newCell.transform.position = transform.position + new Vector3(Cells[i].x, -Cells[i].y, 0) * 0.5f;
            BlockSpawned.Add(newCell);
        }
        gameObject.SetActive(true);
    }

    void OnMouseDown()
    {
        //Debug.Log("down");

        _startPos = transform.position;
        _previousPosition = Input.mousePosition;
        _startScale = transform.localScale;
        transform.DOScale(_startScale * 2f, 0.15f);
        transform.DOMoveY(transform.position.y + 2f, 0.15f);
        Vector3 mousePosition = _camera.ScreenToWorldPoint(Input.mousePosition);

        _offset = transform.position - mousePosition;
        transform.position += new Vector3(0, 2, 0);
        int x = Mathf.RoundToInt(transform.position.x / 1);

        int y = Mathf.RoundToInt(transform.position.y / 1);

        Vector2Int cellPosition = new Vector2Int(x, y);
        _curCell = cellPosition;
    }

    void OnMouseDrag()
    {
        if (_previousPosition != Input.mousePosition)
        {
            Vector3 mousePosition = _camera.ScreenToWorldPoint(Input.mousePosition);
            Vector3 finalPosition = mousePosition + _offset;
            _previousPosition = Input.mousePosition;
            transform.position = mousePosition + _offset + new Vector3(0, 2, 0);

            int x = Mathf.RoundToInt(transform.position.x / 1);
            int y = Mathf.RoundToInt(transform.position.y / 1);

            Vector2Int cellPosition = new Vector2Int(x, y);
            if (cellPosition != _curCell)
            {
                _curCell = cellPosition;
                int r = Board.Instance.Col -1 - cellPosition.y;
                int c = cellPosition.x;
                Board.Instance.HideAllCellHover();
                Vector2Int posConverted = new Vector2Int(r, c);

                if (Board.Instance.CanPlace(posConverted, Cells))
                {
                    Board.Instance.Blur(posConverted, Cells);
                    //Debug.LogWarning($"x: {r}, y: {c}");
                }
            }

            //Debug.Log($"drag");
        }
    }

    void OnMouseUp()
    {
        //Debug.Log("up");
        int r = Board.Instance.Col - 1 - _curCell.y;
        int c = _curCell.x;
        transform.position = _startPos;
        transform.localScale = _startScale;

        Vector2Int posConverted = new Vector2Int(r, c);
        if (Board.Instance.CanPlace(posConverted, Cells))
        {
            Board.Instance.Place(posConverted, Cells);
            Board.Instance.CheckAndClear(posConverted, Cells);

            gameObject.SetActive(false);

            BlockManager.Count--;
            if (BlockManager.Count <= 0)
            {
                BlockManager.RefreshListBlocks();
            }
            BlockManager.CheckBlocksCantPlace();
        }
    }

}