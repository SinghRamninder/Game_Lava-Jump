using UnityEngine;
using System.Collections.Generic;

public class WordGenerator : MonoBehaviour
{
    [SerializeField] private GameObject player;
    [SerializeField] private PlatformPool normalPlatform;
    [SerializeField] private PlatformPool movingPlatform;
    [SerializeField] private PlatformPool breakingPlatform;
    [SerializeField] private PlatformPool lavaPlane;
    [SerializeField] private float platformSpawnDistance = 30f;
    [SerializeField] private float platformDestroyDistance = 40f;
    [SerializeField] private float minPlatformGap = 4f;
    [SerializeField] private float maxPlatformGap = 8f;
    [SerializeField] private float platformHeightVariation = 1f;
    [SerializeField] private float lavaPlaneSpawnDistance = 18f;
    [SerializeField] private float lavaPlaneDestroyDistance = 30f;
    [SerializeField] private GameObject lastLavaPlane;
    [SerializeField] private ChasingLava chasingLava;

    private Dictionary<GameObject, int> activePlatforms = new Dictionary<GameObject, int>();

    private List<GameObject> activeLavaPlanes = new List<GameObject>();

    private Dictionary<GameObject, float> originalPlatformYScales = new Dictionary<GameObject, float>();
    private Dictionary<GameObject, float> originalMovingPlatformMaxHeights = new Dictionary<GameObject, float>();
    private Dictionary<GameObject, float> originalBreakingPlatformBreakDelays = new Dictionary<GameObject, float>();

    private float lastGeneratedPlatformZ = 0f;
    private float difficulty = 0f;
    private float originalChasingLavaSpeed;
    private float originalChasingLavaNoProgressTimeLimit;
    private float originalPlayerChargingSpeed;
    private PlayerMovement playerMovement;

    private void Start()
    {
        activeLavaPlanes.Add(lastLavaPlane);

        originalChasingLavaSpeed = chasingLava.chaseSpeed;
        originalChasingLavaNoProgressTimeLimit = chasingLava.noProgressTimeLimit;
        playerMovement = player.GetComponent<PlayerMovement>();
        originalPlayerChargingSpeed = playerMovement.chargingSpeed;
    }

    private void Update()
    {
        if (player.transform.position.z + platformSpawnDistance > lastGeneratedPlatformZ)
        {
            CreatePlatform();
        }

        Renderer lastLavaRenderer = lastLavaPlane.GetComponent<Renderer>();

        float lastLavaPlaneLength = lastLavaRenderer.bounds.size.z;

        float lastLavaPlaneEnd =
            lastLavaPlane.transform.position.z + lastLavaPlaneLength / 2f;

        if (player.transform.position.z + lavaPlaneSpawnDistance > lastLavaPlaneEnd)
        {
            CreateLavaChunk();
        }

        RemovePlatform();
        RemoveLavaChunk();
    }


    private void CreatePlatform()
    {
        float currentDifficulty = Mathf.Clamp01(difficulty);

        float normalChance = Mathf.Lerp(80f, 35f, currentDifficulty);
        float movingChance = Mathf.Lerp(15f, 30f, currentDifficulty);
        float breakingChance = Mathf.Lerp(5f, 35f, currentDifficulty);

        float randomValue = Random.Range(0f, 100f);

        int platformType;

        if (randomValue < normalChance)
        {
            platformType = 1;
        }
        else if (randomValue < normalChance + movingChance)
        {
            platformType = 2;
        }
        else
        {
            platformType = 3;
        }

        maxPlatformGap = Mathf.Lerp(7f, 11.5f, currentDifficulty);

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

        float maxHeightVariation = Mathf.Lerp(platformHeightVariation, platformHeightVariation * 2f, currentDifficulty);
        float heightVariation = Random.Range(-platformHeightVariation, maxHeightVariation);

        if (platformType == 1 || platformType == 3)
        {
            if (!originalPlatformYScales.ContainsKey(platform))
            {
                originalPlatformYScales.Add(platform, platform.transform.localScale.y);
            }

            Vector3 scale = platform.transform.localScale;
            scale.y = originalPlatformYScales[platform] + heightVariation / GetLocalTop(platform);
            platform.transform.localScale = scale;
        }
        else if (platformType == 2)
        {
            MovingPlatform movingPlatformScript = platform.GetComponent<MovingPlatform>();

            if (!originalMovingPlatformMaxHeights.ContainsKey(platform))
            {
                originalMovingPlatformMaxHeights.Add(platform, movingPlatformScript.maxHeight);
            }

            movingPlatformScript.maxHeight = originalMovingPlatformMaxHeights[platform] + heightVariation;
        }

        platform.transform.position = new Vector3(player.transform.position.x, platform.transform.position.y, lastGeneratedPlatformZ + platformGap);

        lastGeneratedPlatformZ = platform.transform.position.z;

        BreakingPlatform breakingPlatformScript = platform.GetComponent<BreakingPlatform>();
        if (breakingPlatformScript != null)
        {
            if (!originalBreakingPlatformBreakDelays.ContainsKey(platform))
            {
                originalBreakingPlatformBreakDelays.Add(platform, breakingPlatformScript.breakDelay);
            }

            float originalBreakDelay = originalBreakingPlatformBreakDelays[platform];

            breakingPlatformScript.breakDelay = Mathf.Lerp(originalBreakDelay, originalBreakDelay - 1f, currentDifficulty);
        }

        chasingLava.chaseSpeed = Mathf.Lerp(originalChasingLavaSpeed, originalChasingLavaSpeed * 2f, currentDifficulty);
        chasingLava.noProgressTimeLimit = Mathf.Lerp(originalChasingLavaNoProgressTimeLimit, originalChasingLavaNoProgressTimeLimit - 0.7f, currentDifficulty);

        playerMovement.chargingSpeed = Mathf.Lerp(originalPlayerChargingSpeed, originalPlayerChargingSpeed + 4f, currentDifficulty);

        difficulty = Mathf.Clamp01(difficulty + 0.02f);
    }

    private float GetLocalTop(GameObject platform)
    {
        MeshCollider meshCollider = platform.GetComponent<MeshCollider>();
        if (meshCollider != null && meshCollider.sharedMesh != null)
        {
            return Mathf.Max(meshCollider.sharedMesh.bounds.max.y, 0.01f);
        }

        MeshFilter meshFilter = platform.GetComponentInChildren<MeshFilter>();
        return meshFilter != null ? Mathf.Max(meshFilter.sharedMesh.bounds.max.y, 0.01f) : 1f;
    }

    private void CreateLavaChunk()
    {
        Renderer renderer = lastLavaPlane.GetComponent<Renderer>();

        float planeLength = renderer.bounds.size.z;

        Vector3 spawnPosition =
            lastLavaPlane.transform.position +
            lastLavaPlane.transform.forward * planeLength;

        GameObject spawnPlane = lavaPlane.GetPlatform();

        spawnPlane.transform.position = spawnPosition;

        activeLavaPlanes.Add(spawnPlane);

        lastLavaPlane = spawnPlane;
    }

    private void RemovePlatform()
    {
        List<GameObject> platformsToRemove = new List<GameObject>();

        foreach (var currentPlatform in activePlatforms)
        {
            if (currentPlatform.Key.transform.position.z - player.transform.position.z < -platformDestroyDistance)
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
        List<GameObject> lavaPlanesToRemove = new List<GameObject>();

        foreach (GameObject currentLavaPlane in activeLavaPlanes)
        {
            Renderer renderer = currentLavaPlane.GetComponent<Renderer>();

            float planeLength = renderer.bounds.size.z;

            float planeEnd =
                currentLavaPlane.transform.position.z + planeLength / 2f;

            if (planeEnd < player.transform.position.z - lavaPlaneDestroyDistance)
            {
                lavaPlanesToRemove.Add(currentLavaPlane);
            }
        }

        foreach (GameObject currentLavaPlane in lavaPlanesToRemove)
        {
            lavaPlane.ReturnPlatform(currentLavaPlane);

            activeLavaPlanes.Remove(currentLavaPlane);
        }
    }
}