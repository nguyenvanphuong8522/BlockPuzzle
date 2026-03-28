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
        foreach(var blockDrag in ListOfBlockDrag)
        {
            blockDrag.RefreshBlocks();
        }
    }
}
