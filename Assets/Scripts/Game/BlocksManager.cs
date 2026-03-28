using System.Collections.Generic;
using UnityEngine;

public class BlocksManager : MonoBehaviour
{
    public List<BlockDrag> ListOfBlockDrag;

    public int Count { get; set; }

    private void Start()
    {
        Count = ListOfBlockDrag.Count;
        RefreshListBlocks();
    }
    public void RefreshListBlocks()
    {
        Count = ListOfBlockDrag.Count;

        foreach (var blockDrag in ListOfBlockDrag)
        {
            blockDrag.RefreshBlocks();
        }
    }

    public void CheckBlocksCantPlace()
    {
        int countLose = 0;

        foreach (var blockDrag in ListOfBlockDrag)
        {
            if(blockDrag.gameObject.activeSelf)
            {
                if (Board.Instance.CheckLose(blockDrag.Cells))
                {
                    countLose++;
                    blockDrag.GrayBlocks();
                }
                else
                {
                    blockDrag.WhiteBlocks();
                }
            }
        }
        if(Count > 0 && countLose >= Count)
        {
            PopUpReplay.Instance.Show();
            Debug.LogWarning("LOSE");
        }
    }
}
