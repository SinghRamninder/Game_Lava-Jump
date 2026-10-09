using UnityEngine;

// Jump and land sounds, played from the player's position so they sit in the scene instead of
// right in the listener's ears. Only partly 3D: the player always stays on one side of the screen,
// so full 3D would push these sounds hard into one ear.
[RequireComponent(typeof(PlayerMovement))]
public class PlayerSounds : MonoBehaviour
{
    [SerializeField] private Vector2 jumpPitch = new Vector2(0.9f, 1.2f);
    [SerializeField] private float landVolume = 0.8f;

    [Header("3D")]
    [Tooltip("0 = flat 2D, 1 = fully positional")]
    [Range(0f, 1f)] [SerializeField] private float spatialBlend = 0.45f;
    [Tooltip("Higher spread = softer left/right panning")]
    [Range(0f, 360f)] [SerializeField] private float spread = 120f;
    [SerializeField] private float minDistance = 10f;
    [SerializeField] private float maxDistance = 100f;

    private PlayerMovement movement;
    private AudioSource source;
    private float lastCharge;

    private void Awake()
    {
        movement = GetComponent<PlayerMovement>();
        source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = spatialBlend;
        source.spread = spread;
        source.dopplerLevel = 0f;
        source.rolloffMode = AudioRolloffMode.Logarithmic;
        source.minDistance = minDistance;
        source.maxDistance = maxDistance;
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

    private void Update()
    {
        // ChargeAmount is reset inside Jump(), so remember it while charging
        if (movement.IsChargingJump)
        {
            lastCharge = movement.ChargeAmount;
        }
    }

    private void OnJumped()
    {
        Play(AudioManager.Instance != null ? AudioManager.Instance.JumpSound : null, 1f,
             Mathf.Lerp(jumpPitch.x, jumpPitch.y, lastCharge));
    }

    private void OnLanded()
    {
        Play(AudioManager.Instance != null ? AudioManager.Instance.LandSound : null, landVolume,
             Random.Range(0.94f, 1.06f));
    }

    private void Play(AudioClip clip, float volume, float pitch)
    {
        if (clip == null) return;
        source.pitch = pitch;
        source.PlayOneShot(clip, volume * AudioManager.Instance.SfxVolume);
    }
}
