using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    [SerializeField] private float maxJumpPower = 50f;
    [SerializeField] private float minJumpPower = 5f;
    [SerializeField] private float chargingSpeed = 2f;
    [SerializeField] private LineRenderer lineRenderer;
    [SerializeField] private int linePoints = 30;
    [SerializeField] private float timeBetweenPoints = 0.1f;
    private float jumpPower;
    private Vector3 jumpDirection = new Vector3(0f, 1f, 1f).normalized;
    private Rigidbody rb;
    private bool isChargingJump = false;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    private void Start()
    {
        lineRenderer.enabled = false;
        jumpPower = minJumpPower;
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            isChargingJump = true;
            lineRenderer.enabled = true;
        }

        if (isChargingJump && Input.GetKey(KeyCode.Space))
        {
            jumpPower += chargingSpeed * Time.deltaTime;
            jumpPower = Mathf.Clamp(jumpPower, minJumpPower, maxJumpPower);
            DrawTrajectory();
        }

        if (isChargingJump && Input.GetKeyUp(KeyCode.Space))
        {
            Jump();
        }

        if (isChargingJump && Input.GetKeyDown(KeyCode.E))
        {
            isChargingJump = false;
            lineRenderer.enabled = false;
            jumpPower = minJumpPower;
        }
    }

    private void Jump()
    {
        jumpDirection = new Vector3(0f, 1f, 1f).normalized;
        rb.linearVelocity = jumpDirection * jumpPower;
        jumpPower = minJumpPower;
        isChargingJump = false;
        lineRenderer.enabled = false;
    }

    private void DrawTrajectory()
    {
        Vector3 startPosition = transform.position;
        Vector3 startVelocity = jumpDirection * jumpPower;

        Vector3 previousPoint = transform.position;
        lineRenderer.positionCount = linePoints;

        for (int i = 0; i < linePoints; i++)
        {
            float time = i * timeBetweenPoints;

            Vector3 point =
                startPosition
                + startVelocity * time
                + 0.5f * Physics.gravity * time * time;

            Vector3 disBetweenPoints = point - previousPoint;

            if (Physics.Raycast(previousPoint, disBetweenPoints.normalized, out RaycastHit hit, disBetweenPoints.magnitude))
            {
                lineRenderer.positionCount = i + 1;
                lineRenderer.SetPosition(i, hit.point);
                break;
            }

            lineRenderer.SetPosition(i, point);
            previousPoint = point;
        }
    }
}
