using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

[Serializable]
public class Pool
{
    public string name;
    public GameObject prefab;
    public int count;
    public List<GameObject> actives;
    public Queue<GameObject> deactives;

    public Pool(string name, GameObject prefab, int count)
    {
        this.name = name;
        this.prefab = prefab;
        this.count = count;
    }

    public void InitaPool(Transform container, Dictionary<int, int> dicPairHash)
    {
        actives = new List<GameObject>();
        deactives = new Queue<GameObject>();
        for (int i = 0; i < count; i++)
        {
            spawnAClone(container, dicPairHash);
        }
    }

    void spawnAClone(Transform container, Dictionary<int, int> dicPairHash)
    {
        var clone = Object.Instantiate(prefab, container);
        clone.transform.localScale = Vector3.one;
        clone.name += actives.Count + deactives.Count;
        deactives.Enqueue(clone);
        dicPairHash.Add(clone.GetHashCode(), GetHashCode());
    }

    public GameObject Get(Transform container, Dictionary<int, int> dicPairHash)
    {
        if (deactives.Count == 0)
        {
            spawnAClone(container, dicPairHash);
        }
        var clone = deactives.Dequeue();
        clone.SetActive(true);
        actives.Add(clone);
        return clone;
    }

    public void Return(GameObject go)
    {
        go.SetActive(false);
        actives.Remove(go);
        deactives.Enqueue(go);
    }
}

public class ObjectPool : Singleton<ObjectPool>
{
    public Dictionary<int, int> dicPairHash = new Dictionary<int, int>();
    public List<Pool> pools = new List<Pool>();

    public Pool Block;

    public override void Awake()
    {
        AddToPools();
        InitPools();
        base.Awake();
    }

    private void InitPools()
    {
        foreach (var pool in pools)
        {
            pool.InitaPool(transform, dicPairHash);
        }
    }
    private void AddToPools()
    {
            pools.Add(Block);
    }
    public void ReturnAllPool()
    {
        foreach (var p in pools)
        {
            while (p.actives.Count > 0)
            {
                p.Return(p.actives[p.actives.Count - 1]);
            }
        }
    }
    #region TryAddPoolViaScript
    //public Pool TryAddPoolByScript(Pool p)
    //{
    //    var existedPool = pools.Find(x => x.prefab == p.prefab);
    //    if (existedPool != null)
    //    {
    //        Debug.LogWarning($"existed pool: {p.prefab.name}", p.prefab.transform);
    //        return existedPool;
    //    }
    //    pools.Add(p);
    //    p.InitaPool(transform, dicPairHash);
    //    return p;
    //}

#if UNITY_EDITOR
    private void Update()
    {
        //foreach (var p in pools)
        //{
        //    var activeCount = p.actives.Count;
        //    var deactiveCount = p.deactives.Count;
        //    p.name = $"total: {activeCount + deactiveCount} | active: {activeCount} | deactive: {deactiveCount}";
        //}
    }
#endif
    #endregion

    public GameObject Get(Pool p)
    {
        return p.Get(transform, dicPairHash);
    }
    public GameObject Get(Pool p, Vector3 pos, float scale = 1)
    {
        GameObject clone = p.Get(transform, dicPairHash);
        clone.transform.position = pos;
        clone.transform.localScale = Vector3.one * scale;
        return clone;
    }

    public void Return(GameObject clone)
    {
        clone.SetActive(false);
        var hash = clone.GetHashCode();
        if (dicPairHash.ContainsKey(hash))
        {
            var p = getPool(dicPairHash[hash]);
            p.Return(clone);
        }
    }
    Pool getPool(int hash)
    {
        foreach (var pool in pools)
        {
            if (pool.GetHashCode() == hash)
                return pool;
        }

        return null;
    }
}