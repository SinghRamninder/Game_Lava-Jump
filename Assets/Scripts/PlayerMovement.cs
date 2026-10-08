using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    [Header("Jump")]
    [SerializeField] private float maxJumpPower = 25f;
    [SerializeField] private float minJumpPower = 5f;
    [SerializeField] private float chargingSpeed = 2f;

    [Header("Gravity")]
    [SerializeField] private float gravityMultiplier = 3f;

    [Header("Trajectory")]
    [SerializeField] private LineRenderer lineRenderer;
    [SerializeField] private int linePoints = 30;
    [SerializeField] private float timeBetweenPoints = 0.1f;
    [SerializeField] private GameObject landingAreaObject;
    [SerializeField] private Transform lineRendStartPos;

    private float jumpPower;
    private Vector3 jumpDirection = new Vector3(0f, 1f, 1f).normalized;
    private Rigidbody rb;
    private bool isChargingJump = false;
    private bool isAirborne = false;

    public bool IsChargingJump => isChargingJump;
    public bool IsAirborne => isAirborne;
    public float ChargeAmount => Mathf.InverseLerp(minJumpPower, maxJumpPower, jumpPower);
    public event System.Action Jumped;
    public event System.Action Landed;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        landingAreaObject.SetActive(false);
    }

    private void Start()
    {
        lineRenderer.enabled = false;
        jumpPower = minJumpPower;
    }
        
    private void Update()
    {
        if (!isAirborne && Input.GetKeyDown(KeyCode.Space))
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
            landingAreaObject.SetActive(false);
        }

        if (isChargingJump && Input.GetKeyDown(KeyCode.E))
        {
            isChargingJump = false;
            lineRenderer.enabled = false;
            jumpPower = minJumpPower;
            landingAreaObject.SetActive(false);
        }
    }

    private void FixedUpdate()
    {
        if (isAirborne)
        {
            rb.AddForce(
                Physics.gravity * (gravityMultiplier - 1f),
                ForceMode.Acceleration
            );
        }
    }

    private void Jump()
    {
        jumpDirection = new Vector3(0f, 1f, 1f).normalized;
        rb.linearVelocity = jumpDirection * jumpPower;
        jumpPower = minJumpPower;
        isChargingJump = false;
        isAirborne = true;
        lineRenderer.enabled = false;
        Jumped?.Invoke();
    }

    private void DrawTrajectory()
    {
        Vector3 startPosition = lineRendStartPos.position;
        Vector3 startVelocity = jumpDirection * jumpPower;

        Vector3 gravity = Physics.gravity * gravityMultiplier;

        Vector3 previousPoint = lineRendStartPos.position;
        lineRenderer.positionCount = linePoints;

        for (int i = 0; i < linePoints; i++)
        {
            float time = i * timeBetweenPoints;

            Vector3 point =
                startPosition
                + startVelocity * time
                + 0.5f * gravity * time * time;

            Vector3 disBetweenPoints = point - previousPoint;

            if (Physics.Raycast(previousPoint, disBetweenPoints.normalized, out RaycastHit hit, disBetweenPoints.magnitude))
            {
                lineRenderer.positionCount = i + 1;
                lineRenderer.SetPosition(i, hit.point);
                landingAreaObject.transform.position = new Vector3(hit.point.x, hit.point.y + 0.05f, hit.point.z);
                landingAreaObject.transform.rotation = Quaternion.LookRotation(-hit.normal);
                landingAreaObject.SetActive(true);
                break;
            }

            landingAreaObject.SetActive(false);

            lineRenderer.SetPosition(i, point);
            previousPoint = point;
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("Platform"))
        {
            rb.linearVelocity = Vector3.zero;
            bool wasAirborne = isAirborne;
            isAirborne = false;
            if (wasAirborne)
            {
                Landed?.Invoke();
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Lava"))
        {
            rb.linearVelocity = Vector3.zero;
            isAirborne = false;
            isChargingJump = false;
            lineRenderer.enabled = false;
            landingAreaObject.SetActive(false);
            GameUI.PlayerDied();
        }
    }
}
