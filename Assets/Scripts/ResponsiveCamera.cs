using UnityEngine;

public class ResponsiveCamera : MonoBehaviour
{
    [SerializeField] private Transform player;
    [SerializeField] private Vector3[] impPoints;
    [SerializeField] private float maxFov = 52f;
    [SerializeField] private float marginDegrees = 2f;

    private Camera cam;
    private CameraMovement cameraMovement;

    private void Awake()
    {
        cam = GetComponent<Camera>();
        cameraMovement = GetComponent<CameraMovement>();
    }

    private void Start()
    {
        if (!UnityEngine.Device.Application.isMobilePlatform) return;

        cam.transform.position = new Vector3(cam.transform.position.x, cam.transform.position.y + 2.188f, cam.transform.position.z);

        float bestZ = cameraMovement.zOffset;
        float bestFov = float.MaxValue;
        for (float z = 10f; z <= 40f; z += 0.5f)
        {
            float fov = NeededFOV(z);
            if (fov < bestFov) 
            {
                bestFov = fov;
                bestZ = z;
            }
        }

        cam.fieldOfView = Mathf.Min(bestFov, maxFov);
        cameraMovement.zOffset = bestZ;
    }

    private float NeededFOV(float offset)
    {
        Vector3 camPos = new Vector3(cam.transform.position.x, cam.transform.position.y, player.position.z + offset);
        Quaternion toCamera = Quaternion.Inverse(cam.transform.rotation);

        float maxFov = 0f;
        foreach (Vector3 point in impPoints)
        {
            Vector3 local = toCamera * (player.position + point - camPos);

            maxFov = Mathf.Max(maxFov, Mathf.Abs(local.y / local.z), Mathf.Abs(local.x / local.z) / cam.aspect);
        }

        return 2f * Mathf.Atan(maxFov) * Mathf.Rad2Deg + marginDegrees;
    }
}