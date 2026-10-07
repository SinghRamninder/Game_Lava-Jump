using UnityEngine;

public class ChasingLava : MonoBehaviour
{
    [SerializeField] private GameObject player;
    [SerializeField] private GameObject lava;
    [SerializeField] private float chaseDistance = 30f;
    [SerializeField] private float spawnDistance = 30f;
    [SerializeField] private float noProgressTimeLimit = 1f;
    public float chaseSpeed = 5f;

    private float furtherstZPosition;
    private float noProgressTime = 0f;

    private void Start()
    {
        furtherstZPosition = player.transform.position.z;
    }

    private void Update()
    {
        if (player.transform.position.z > furtherstZPosition + 2f)
        {
            furtherstZPosition = player.transform.position.z;
            noProgressTime = 0f;
        }
        else
        {
            noProgressTime += Time.deltaTime;
        }

        if (noProgressTime > noProgressTimeLimit && !lava.activeSelf)
        {
            ActiveLava();
        }

        if (lava.activeSelf)
        {
            float distanceToPlayer = player.transform.position.z - lava.transform.position.z;

            if (distanceToPlayer >= 7f)
            {
                if (distanceToPlayer <= chaseDistance)
                {
                    lava.transform.position += Vector3.forward * chaseSpeed * Time.deltaTime;
                }
                else
                {
                    lava.SetActive(false);
                    noProgressTime = 0f;
                }
            }
        }
    }

    private void ActiveLava()
    {
        lava.transform.position = new Vector3(lava.transform.position.x, lava.transform.position.y, player.transform.position.z - spawnDistance);
        lava.SetActive(true);
    }
}