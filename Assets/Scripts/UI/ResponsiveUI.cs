using UnityEngine;

public class ResponsiveUI : MonoBehaviour
{
    private bool isMobile = false;

    private void Awake()
    {
        isMobile = UnityEngine.Device.Application.isMobilePlatform;
    }

    private void Start()
    {
        foreach (Transform child in GetComponentsInChildren<Transform>(true))
        {
            if (child == transform) continue;

            if (child.CompareTag("MobileLayout"))
            {
                child.gameObject.SetActive(isMobile);
            }
            else if (child.CompareTag("DesktopLayout"))
            {
                child.gameObject.SetActive(!isMobile);
            }
        }
    }
}
