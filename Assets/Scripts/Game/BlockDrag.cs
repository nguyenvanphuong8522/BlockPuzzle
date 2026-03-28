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

    private float _scaleMulti = 2;
    private float _durationScale = 0.15f;
    private float _cellSize = 1f;
    private float _offsetY = 2f;
    public Vector2Int[] Cells { get; set; }

    public GameObject PrefabBlock;

    public List<GameObject> BlockSpawned;
    public BlocksManager BlockManager;
    public Sprite SpriteBlocks;
    private void Awake()
    {
        _camera = Camera.main;
    }
    void OnMouseDown()
    {
        //Debug.Log("down");

        _startPos = transform.position;
        _previousPosition = Input.mousePosition;
        _startScale = transform.localScale;
        transform.DOScale(_startScale * _scaleMulti, _durationScale);
        transform.DOMoveY(transform.position.y + _offsetY, _durationScale);
        Vector3 mousePosition = _camera.ScreenToWorldPoint(Input.mousePosition);

        _offset = transform.position - mousePosition;

        Vector2Int cellPosition = WorldToGrid(transform.position);
        _curCell = cellPosition;
    }

    void OnMouseDrag()
    {
        if (_previousPosition != Input.mousePosition)
        {
            Vector3 mousePosition = _camera.ScreenToWorldPoint(Input.mousePosition);
            Vector3 finalPosition = mousePosition + _offset;
            _previousPosition = Input.mousePosition;
            transform.position = mousePosition + _offset + new Vector3(0, _offsetY, 0);

            Vector2Int cellPosition = WorldToGrid(transform.position);

            if (cellPosition != _curCell)
            {
                _curCell = cellPosition;
                int r = Board.Instance.Col - 1 - cellPosition.y;
                int c = cellPosition.x;
                Board.Instance.HideAllCellHover();
                Vector2Int posConverted = new Vector2Int(r, c);

                if (Board.Instance.CanPlace(posConverted, Cells))
                {
                    Board.Instance.Hover(SpriteBlocks, posConverted, Cells);
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
            Board.Instance.Place(SpriteBlocks,posConverted, Cells);
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

    private Vector2Int WorldToGrid(Vector3 worldPos)
    {
        int x = Mathf.RoundToInt(transform.position.x / _cellSize);
        int y = Mathf.RoundToInt(transform.position.y / _cellSize);

        Vector2Int cellPosition = new Vector2Int(x, y);
        return cellPosition;
    }
    public void GrayBlocks()
    {
        foreach (var block in BlockSpawned)
        {
            block.GetComponent<SpriteRenderer>().sprite = GameData.Instance.GrayBlock;
        }
    }
    public void WhiteBlocks()
    {
        foreach (var block in BlockSpawned)
        {
            block.GetComponent<SpriteRenderer>().sprite = SpriteBlocks;
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
        int indexSprite = Random.Range(0, GameData.Instance.ListOfSkin.Count);
        SpriteBlocks = GameData.Instance.ListOfSkin[indexSprite];
        int randomIndex = Random.Range(0, GameData.Instance.DataBlocks.Datas.Count);
        ListVector2Int newCells = GameData.Instance.DataBlocks.Datas[randomIndex];
        Cells = newCells.Array;
        for (int i = 0; i < Cells.Length; i++)
        {
            GameObject newCell = Instantiate(PrefabBlock, transform);
            newCell.transform.position = transform.position + new Vector3(Cells[i].x, -Cells[i].y, 0) * 0.5f;
            if(newCell.TryGetComponent(out SpriteRenderer spriteRenderer))
            {
                spriteRenderer.sprite = SpriteBlocks;
            }
            BlockSpawned.Add(newCell);
        }
        gameObject.SetActive(true);
    }

}