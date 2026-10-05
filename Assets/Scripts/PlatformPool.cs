using System.Collections.Generic;
using UnityEngine;

public class PlatformPool : MonoBehaviour
{
    [SerializeField] private GameObject platformPrefab;
    [SerializeField] private int poolSize = 10;
    
    private Queue<GameObject> platformPool = new Queue<GameObject>();

    private void Start()
    {
        for (int i = 0; i < poolSize; i++)
        {
            GameObject platform = Instantiate(platformPrefab);
            platform.SetActive(false);
            platformPool.Enqueue(platform);
        }
    }

    public GameObject GetPlatform()
    {
        GameObject platform = platformPool.Dequeue();
        platform.SetActive(true);
        return platform;
    }

    public void ReturnPlatform(GameObject platform)
    {
        platform.SetActive(false);
        platformPool.Enqueue(platform);
    }
}
