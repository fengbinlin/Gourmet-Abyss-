using UnityEngine;
using System.Collections.Generic;

public class DungeonObjectPool : MonoBehaviour
{
    private Dictionary<GameObject, Queue<GameObject>> poolDict = new Dictionary<GameObject, Queue<GameObject>>();
    public static DungeonObjectPool Instance;

    private void Awake()
    {
        Instance = this;
    }

    public void Prewarm(GameObject prefab, int count, Transform parent = null)
    {
        if (prefab == null) return;
        if (!poolDict.ContainsKey(prefab))
            poolDict[prefab] = new Queue<GameObject>();

        for (int i = 0; i < count; i++)
        {
            GameObject obj = Instantiate(prefab, Vector3.zero, Quaternion.identity, parent);
            obj.SetActive(false);
            poolDict[prefab].Enqueue(obj);
        }
    }

    public GameObject GetFromPool(GameObject prefab, Vector3 position, Quaternion rotation, Transform parent = null)
    {
        if (prefab == null) return null;
        // if (!poolDict.ContainsKey(prefab))
        //     poolDict[prefab] = new Queue<GameObject>();

        GameObject obj;
        if (poolDict[prefab].Count > 0)
        {
            obj = poolDict[prefab].Dequeue();
            obj.transform.SetParent(parent);
            obj.transform.position = position;
            obj.transform.rotation = rotation;
            //print("UP");
        }
        else
        {
            //print("NUP");
            obj = Instantiate(prefab, position, rotation, parent);
        }

        obj.SetActive(true);
        return obj;
    }

    public void ReturnToPool(GameObject obj, GameObject prefab)
    {
        if (obj == null || prefab == null) return;
        obj.SetActive(false);
        if (!poolDict.ContainsKey(prefab))
            poolDict[prefab] = new Queue<GameObject>();
        poolDict[prefab].Enqueue(obj);
    }
}