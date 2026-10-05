using UnityEngine;
using System.Collections.Generic;

public class WordGenerator : MonoBehaviour
{
    [SerializeField] private GameObject player;
    [SerializeField] private PlatformPool normalPlatform;
    [SerializeField] private PlatformPool movingPlatform;
    [SerializeField] private PlatformPool breakingPlatform;
    [SerializeField] private float farthestGeneratedDistance = 30f;
    [SerializeField] private float minPlatformGap = 4f;
    [SerializeField] private float maxPlatformGap = 8f;

    private Dictionary<GameObject, int> activePlatforms = new Dictionary<GameObject, int>();

    private float lastGeneratedPlatformZ = 0f;

    private void Update()
    {
        if (player.transform.position.z + farthestGeneratedDistance > lastGeneratedPlatformZ)
        {
            CreatePlatform();
        }

        RemovePlatform();
    }


    private void CreatePlatform()
    {
        int platformType = Random.Range(1, 4);
        float platformGap = Random.Range(minPlatformGap, maxPlatformGap);

        GameObject platform;
        switch (platformType)
        {
            case 1:
                platform = normalPlatform.GetPlatform();
                break;
            case 2:
                platform = movingPlatform.GetPlatform();
                break;
            case 3:
                platform = breakingPlatform.GetPlatform();
                break;
            default:
                platform = normalPlatform.GetPlatform();
                break;
        }
        activePlatforms.Add(platform, platformType);

        platform.transform.position = new Vector3(3.94f, 0.58f, lastGeneratedPlatformZ + platformGap);
        lastGeneratedPlatformZ = platform.transform.position.z;
    }

    private void CreateLavaChunk()
    {

    }

    private void RemovePlatform()
    {
        List<GameObject> platformsToRemove = new List<GameObject>();

        foreach (var currentPlatform in activePlatforms)
        {
            if (currentPlatform.Key.transform.position.z - player.transform.position.z < -30f)
            {
                switch (currentPlatform.Value)
                {
                    case 1:
                        normalPlatform.ReturnPlatform(currentPlatform.Key);
                        break;
                    case 2:
                        movingPlatform.ReturnPlatform(currentPlatform.Key);
                        break;
                    case 3:
                        breakingPlatform.ReturnPlatform(currentPlatform.Key);
                        break;
                    default:
                        normalPlatform.ReturnPlatform(currentPlatform.Key);
                        break;
                }

                platformsToRemove.Add(currentPlatform.Key);
            }
        }

        foreach (GameObject platform in platformsToRemove)
        {
            activePlatforms.Remove(platform);
        }
    }

    private void RemoveLavaChunk()
    {

    }
}
