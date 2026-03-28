using Sirenix.Utilities;
using System.Collections.Generic;
using System.Linq;
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

        List<int> result = new List<int>();

        while (result.Count < 3)
        {
            int newIndex = Random.Range(0, GameData.Instance.ListOfSkin.Count);
            if(!result.Contains(newIndex))
            {
                result.Add(newIndex);
            }
        }
        for (int k = 0; k < ListOfBlockDrag.Count; k++)
        {
            ListOfBlockDrag[k].RefreshBlocks(result[k]);
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
