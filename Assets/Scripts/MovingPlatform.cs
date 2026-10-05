using System;
using UnityEngine;

public class MovingPlatform : MonoBehaviour
{
    public float maxHeight = 0.7f;
    public float minHeight = -2f;
    [SerializeField] private float speed = 1f;
    [SerializeField] private float delay = 2f;
    private float timer = 0f;
    private bool stopMovement = false;
    private float targetY;

    private void Start()
    {
        transform.position = new Vector3(transform.position.x, maxHeight, transform.position.z);
        targetY = minHeight;
    }

    private void Update()
    {
        if (!stopMovement)
        {
            float newY = Mathf.MoveTowards(transform.position.y, targetY, speed * Time.deltaTime);
            transform.position = new Vector3(transform.position.x, newY, transform.position.z);

            if (Mathf.Approximately(newY, targetY))
            {
                stopMovement = true;
                timer = Time.time + delay;
            }
        }
        else
        {
            if (Time.time >= timer)
            {
                stopMovement = false;
                targetY = targetY == maxHeight ? minHeight : maxHeight;
            }
        }
    }
}
