using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerMovement : MonoBehaviour
{
    [Header("Input")]
    [SerializeField] private InputAction chargeJumpAction = CreateButton("ChargeJump", "<Keyboard>/space", "<Mouse>/leftButton");
    [SerializeField] private InputAction cancelJumpAction = CreateButton("CancelJump", "<Keyboard>/e", "<Mouse>/rightButton");

    [Header("Jump")]
    [SerializeField] private float maxJumpPower = 25f;
    [SerializeField] private float minJumpPower = 5f;
    public float chargingSpeed = 2f;

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
    private bool waitForChargeRelease = false;

    public bool IsChargingJump => isChargingJump;
    public bool UsingMouse { get; private set; }
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

    private void OnEnable()
    {
        chargeJumpAction.Enable();
        cancelJumpAction.Enable();
    }

    private void OnDisable()
    {
        chargeJumpAction.Disable();
        cancelJumpAction.Disable();
    }

    private static InputAction CreateButton(string name, params string[] bindings)
    {
        var action = new InputAction(name, InputActionType.Button);
        foreach (string binding in bindings)
        {
            action.AddBinding(binding);
        }
        return action;
    }

    private void Update()
    {
        TrackInputDevice();

        bool chargeHeld = chargeJumpAction.IsPressed();
        if (!chargeHeld)
        {
            waitForChargeRelease = false;
        }

        bool wantsToCharge = chargeJumpAction.WasPressedThisFrame() || (chargeHeld && !waitForChargeRelease);
        if (!isAirborne && !isChargingJump && wantsToCharge)
        {
            isChargingJump = true;
            lineRenderer.enabled = true;
        }

        if (isChargingJump && chargeHeld)
        {
            jumpPower += chargingSpeed * Time.deltaTime;
            jumpPower = Mathf.Clamp(jumpPower, minJumpPower, maxJumpPower);
            DrawTrajectory();
        }

        if (isChargingJump && chargeJumpAction.WasReleasedThisFrame())
        {
            Jump();
            landingAreaObject.SetActive(false);
        }

        if (isChargingJump && cancelJumpAction.WasPressedThisFrame())
        {
            waitForChargeRelease = true;
            isChargingJump = false;
            lineRenderer.enabled = false;
            jumpPower = minJumpPower;
            landingAreaObject.SetActive(false);
        }
    }

    private void TrackInputDevice()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard.anyKey.wasPressedThisFrame)
        {
            UsingMouse = false;
        }

        Mouse mouse = Mouse.current;
        if (mouse != null && (mouse.leftButton.wasPressedThisFrame || mouse.rightButton.wasPressedThisFrame || mouse.middleButton.wasPressedThisFrame))
        {
            UsingMouse = true;
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
            Land();
        }
    }

    private void OnCollisionStay(Collision collision)
    {
        if (!isAirborne || rb.linearVelocity.y > 0.01f || !collision.gameObject.CompareTag("Platform"))
        {
            return;
        }

        for (int i = 0; i < collision.contactCount; i++)
        {
            if (Mathf.Abs(collision.GetContact(i).normal.y) > 0.7f)
            {
                Land();
                return;
            }
        }
    }

    private void Land()
    {
        rb.linearVelocity = Vector3.zero;
        bool wasAirborne = isAirborne;
        isAirborne = false;
        if (wasAirborne)
        {
            Landed?.Invoke();
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
