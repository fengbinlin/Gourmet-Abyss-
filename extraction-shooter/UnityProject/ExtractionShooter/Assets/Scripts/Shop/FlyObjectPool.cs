using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FlyObjectPool : MonoBehaviour
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
    
    public static FlyObjectPool Instance { get; private set; }
    
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }
    
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
