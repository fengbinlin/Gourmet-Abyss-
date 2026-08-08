using System.Collections;
using System.Collections.Generic;
using Game.Core;
using UnityEngine;

// 同 ShopManager：挂在子物体上，原来的 DontDestroyOnLoad 从未生效。
public class FlyObjectPool : MonoSingleton<FlyObjectPool>
{
     [System.Serializable]
    public class Pool
    {
        public GameObject prefab;
        public int initialSize = 10;
    }
    
    public Pool projectilePool;
    private Queue<GameObject> availableObjects = new Queue<GameObject>();
    private List<GameObject> allObjects = new List<GameObject>();
    
    private void Start()
    {
        // 预创建对象
        for (int i = 0; i < projectilePool.initialSize; i++)
        {
            CreateNewObject();
        }
    }
    
    private void CreateNewObject()
    {
        GameObject obj = Instantiate(projectilePool.prefab, transform);
        obj.SetActive(false);
        availableObjects.Enqueue(obj);
        allObjects.Add(obj);
    }
    
    public GameObject GetObject(Vector3 position)
    {
        if (availableObjects.Count == 0)
        {
            CreateNewObject();
        }
        
        GameObject obj = availableObjects.Dequeue();
        obj.transform.position = position;
        obj.SetActive(true);
        return obj;
    }
    
    public void ReturnObject(GameObject obj)
    {
        obj.SetActive(false);
        availableObjects.Enqueue(obj);
    }
}
