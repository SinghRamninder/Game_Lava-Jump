using UnityEngine;

public class BreakingPlatform : MonoBehaviour
{
    [SerializeField] private float breakDelay = 3f;
    private bool startBreaking = false;
    private float breakTime = 0f;

    private void Update()
    {
        if (startBreaking)
        {
            if (Time.time >= breakTime)
            {
                gameObject.AddComponent<Rigidbody>();
                startBreaking = false;
            }
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("Player") && !startBreaking)
        {
            startBreaking = true;
            breakTime = Time.time + breakDelay;
        }
    }
}
