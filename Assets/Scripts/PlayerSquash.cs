using UnityEngine;

// Squash & stretch for the player cube. Animates only the visual child (pivot at the cube's bottom),
// never the physics root, so colliders, trajectory and landing logic are unaffected.
[RequireComponent(typeof(PlayerMovement))]
public class PlayerSquash : MonoBehaviour
{
    [SerializeField] private Transform visual;
    [SerializeField] private Renderer bodyRenderer;

    [Header("Spring")]
    [SerializeField] private float stiffness = 260f;
    [SerializeField] private float damping = 11f;

    [Header("Charge")]
    [SerializeField] private float maxChargeSquash = 0.4f;
    [SerializeField] private float chargeShakeStart = 0.8f;
    [SerializeField] private float chargeShakeAmount = 0.03f;

    [Header("Jump / Land")]
    [SerializeField] private float jumpStretch = 0.45f;
    [SerializeField] private float landSquashVelocity = 6f;
    [SerializeField] private float airStretchPerSpeed = 0.012f;

    [Header("Eyes")]
    [SerializeField] private Vector2 blinkInterval = new Vector2(2f, 5f);
    [SerializeField] private float blinkDuration = 0.12f;
    [SerializeField] private float chargeSquint = 0.5f;

    private static readonly int BlinkId = Shader.PropertyToID("_Blink");

    private PlayerMovement movement;
    private Rigidbody rb;
    private MaterialPropertyBlock block;
    private Vector3 visualBasePosition;

    private float squash;          // > 0 stretch, < 0 squash
    private float squashVelocity;
    private float nextBlinkTime;
    private float blinkTimer = -1f;

    private void Awake()
    {
        movement = GetComponent<PlayerMovement>();
        rb = GetComponent<Rigidbody>();
        block = new MaterialPropertyBlock();
        visualBasePosition = visual.localPosition;
        ScheduleBlink();
    }

    private void OnEnable()
    {
        movement.Jumped += OnJumped;
        movement.Landed += OnLanded;
    }

    private void OnDisable()
    {
        movement.Jumped -= OnJumped;
        movement.Landed -= OnLanded;
    }

    private void OnJumped()
    {
        // Snap from the charged squash straight into a stretch, the spring settles it back
        squash = jumpStretch;
        squashVelocity = 0f;
    }

    private void OnLanded()
    {
        squashVelocity = -landSquashVelocity;
    }

    private void Update()
    {
        float dt = Mathf.Min(Time.deltaTime, 1f / 30f);
        float charge = movement.IsChargingJump ? movement.ChargeAmount : 0f;

        float target = 0f;
        if (movement.IsChargingJump)
        {
            // Ease out so the first bit of charging already reads clearly
            target = -maxChargeSquash * (1f - (1f - charge) * (1f - charge));
        }
        else if (movement.IsAirborne)
        {
            target = Mathf.Clamp(rb.linearVelocity.y * airStretchPerSpeed, -0.12f, 0.2f);
        }

        squashVelocity += (stiffness * (target - squash) - damping * squashVelocity) * dt;
        squash += squashVelocity * dt;
        squash = Mathf.Clamp(squash, -0.7f, 1f);

        // Volume-preserving scale around the bottom pivot
        float y = 1f + squash;
        float xz = 1f / Mathf.Sqrt(y);
        visual.localScale = new Vector3(xz, y, xz);

        // Tremble when the jump is almost fully charged
        Vector3 shake = Vector3.zero;
        if (charge > chargeShakeStart)
        {
            float strength = Mathf.InverseLerp(chargeShakeStart, 1f, charge) * chargeShakeAmount;
            shake = new Vector3(Random.Range(-1f, 1f), 0f, Random.Range(-1f, 1f)) * strength;
        }
        visual.localPosition = visualBasePosition + shake;

        UpdateEyes(charge);
    }

    private void UpdateEyes(float charge)
    {
        float blink = 0f;
        if (blinkTimer >= 0f)
        {
            blinkTimer += Time.deltaTime;
            float t = blinkTimer / blinkDuration;
            blink = 1f - Mathf.Abs(t * 2f - 1f);
            if (t >= 1f)
            {
                blinkTimer = -1f;
                ScheduleBlink();
            }
        }
        else if (Time.time >= nextBlinkTime)
        {
            blinkTimer = 0f;
        }

        // Determined squint while charging
        blink = Mathf.Max(blink, charge * chargeSquint);

        bodyRenderer.GetPropertyBlock(block);
        block.SetFloat(BlinkId, blink);
        bodyRenderer.SetPropertyBlock(block);
    }

    private void ScheduleBlink()
    {
        nextBlinkTime = Time.time + Random.Range(blinkInterval.x, blinkInterval.y);
    }
}
