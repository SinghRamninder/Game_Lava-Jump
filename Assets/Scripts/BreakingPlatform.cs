using UnityEngine;

// When the player lands: cracks spread from the landing point and the platform starts to shake,
// then it splits into chunks that sag apart, and finally the chunks fall and the collider is removed.
// Resets itself in OnEnable so it works with PlatformPool.
public class BreakingPlatform : MonoBehaviour
{
    [SerializeField] private float breakDelay = 3f;

    [Header("Visuals (built by Tools > Platforms > Build Breaking Platform)")]
    [SerializeField] private Transform visual;
    [SerializeField] private GameObject intact;
    [SerializeField] private GameObject fractured;
    [SerializeField] private Transform[] chunks;
    [SerializeField] private ParticleSystem crumbleParticles;
    [SerializeField] private ParticleSystem breakParticles;

    [Header("Cracking")]
    [SerializeField] private float crumbleTime = 0.5f;
    [SerializeField] private float maxShake = 0.04f;
    [SerializeField] private float maxShakeAngle = 1.2f;
    [SerializeField] private float shakeFrequency = 22f;
    [SerializeField] private float maxCrumbleRate = 30f;

    [Header("Breaking Apart")]
    [SerializeField] private float crumbleSpread = 0.08f;
    [SerializeField] private float crumbleSag = 0.05f;
    [SerializeField] private float crumbleTilt = 4f;
    [SerializeField] private Vector2 fallOutwardSpeed = new Vector2(0.8f, 2.2f);
    [SerializeField] private Vector2 fallUpSpeed = new Vector2(0f, 1.5f);
    [SerializeField] private float fallGravity = 22f;
    [SerializeField] private float fallSpin = 120f;
    [SerializeField] private float fallTime = 2.5f;

    private enum State { Idle, Cracking, Crumbling, Falling, Gone }

    private static readonly int ProgressId = Shader.PropertyToID("_CrackProgress");
    private static readonly int ImpactId = Shader.PropertyToID("_ImpactPoint");

    private State state;
    private float timer;
    private Collider platformCollider;
    private Renderer[] renderers;
    private MaterialPropertyBlock block;
    private Vector3 impactPoint;
    private Vector3 visualStartPosition;
    private Quaternion visualStartRotation;
    private Vector3[] chunkStartPositions;
    private Quaternion[] chunkStartRotations;
    private Vector3[] chunkOutward;
    private Vector3[] chunkTiltAxis;
    private Vector3[] chunkVelocity;
    private Vector3[] chunkSpin;
    private float shakeSeed;

    private void Awake()
    {
        platformCollider = GetComponent<Collider>();
        renderers = visual.GetComponentsInChildren<Renderer>(true);
        block = new MaterialPropertyBlock();
        visualStartPosition = visual.localPosition;
        visualStartRotation = visual.localRotation;

        int count = chunks.Length;
        chunkStartPositions = new Vector3[count];
        chunkStartRotations = new Quaternion[count];
        chunkOutward = new Vector3[count];
        chunkTiltAxis = new Vector3[count];
        chunkVelocity = new Vector3[count];
        chunkSpin = new Vector3[count];
        for (int i = 0; i < count; i++)
        {
            chunkStartPositions[i] = chunks[i].localPosition;
            chunkStartRotations[i] = chunks[i].localRotation;
            Vector3 flat = new Vector3(chunkStartPositions[i].x, 0f, chunkStartPositions[i].z);
            chunkOutward[i] = flat.sqrMagnitude > 1e-4f ? flat.normalized : Random.onUnitSphere;
            chunkTiltAxis[i] = Vector3.Cross(Vector3.up, chunkOutward[i]);
        }
    }

    private void OnEnable()
    {
        ResetPlatform();
    }

    private void ResetPlatform()
    {
        state = State.Idle;
        timer = 0f;
        shakeSeed = Random.value * 100f;
        platformCollider.enabled = true;
        intact.SetActive(true);
        fractured.SetActive(false);
        visual.localPosition = visualStartPosition;
        visual.localRotation = visualStartRotation;
        for (int i = 0; i < chunks.Length; i++)
        {
            chunks[i].localPosition = chunkStartPositions[i];
            chunks[i].localRotation = chunkStartRotations[i];
        }

        impactPoint = new Vector3(0f, 1f, 0f);
        SetCrackProgress(0f);
        SetCrumbleRate(0f);
        crumbleParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        breakParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (state != State.Idle || !collision.gameObject.CompareTag("Player"))
        {
            return;
        }

        state = State.Cracking;
        timer = 0f;
        // Mesh space == this transform's local space, so the cracks start right under the player
        impactPoint = transform.InverseTransformPoint(collision.GetContact(0).point);
        crumbleParticles.Play(true);
    }

    private void Update()
    {
        if (state == State.Idle || state == State.Gone)
        {
            return;
        }

        timer += Time.deltaTime;
        float crackDuration = Mathf.Max(0.01f, breakDelay - crumbleTime);

        if (state == State.Cracking || state == State.Crumbling)
        {
            float progress = Mathf.Clamp01(timer / crackDuration);
            SetCrackProgress(progress);
            SetCrumbleRate(maxCrumbleRate * progress * progress);

            float shake = state == State.Crumbling ? 1.25f : Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.2f, 1f, progress));
            Shake(shake);

            if (state == State.Cracking && timer >= crackDuration)
            {
                StartCrumble();
            }

            if (state == State.Crumbling)
            {
                float t = Mathf.Clamp01((timer - crackDuration) / Mathf.Max(crumbleTime, 0.01f));
                float eased = 1f - (1f - t) * (1f - t);
                for (int i = 0; i < chunks.Length; i++)
                {
                    chunks[i].localPosition = chunkStartPositions[i] + chunkOutward[i] * (crumbleSpread * eased) + Vector3.down * (crumbleSag * t * t);
                    chunks[i].localRotation = Quaternion.AngleAxis(crumbleTilt * eased, chunkTiltAxis[i]) * chunkStartRotations[i];
                }

                if (timer >= breakDelay)
                {
                    StartFall();
                }
            }
        }
        else if (state == State.Falling)
        {
            float dt = Time.deltaTime;
            for (int i = 0; i < chunks.Length; i++)
            {
                chunkVelocity[i] += Vector3.down * (fallGravity * dt);
                chunks[i].position += chunkVelocity[i] * dt;
                chunks[i].Rotate(chunkSpin[i] * dt, Space.World);
            }

            if (timer >= fallTime)
            {
                fractured.SetActive(false);
                state = State.Gone;
            }
        }
    }

    private void StartCrumble()
    {
        state = State.Crumbling;
        intact.SetActive(false);
        fractured.SetActive(true);
        crumbleParticles.Emit(12);
    }

    private void StartFall()
    {
        state = State.Falling;
        timer = 0f;
        platformCollider.enabled = false;
        visual.localPosition = visualStartPosition;
        visual.localRotation = visualStartRotation;
        SetCrumbleRate(0f);
        crumbleParticles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        breakParticles.Play(true);

        for (int i = 0; i < chunks.Length; i++)
        {
            Vector3 outward = transform.TransformDirection(chunkOutward[i]).normalized;
            chunkVelocity[i] = outward * Random.Range(fallOutwardSpeed.x, fallOutwardSpeed.y)
                             + Vector3.up * Random.Range(fallUpSpeed.x, fallUpSpeed.y);
            chunkSpin[i] = Random.insideUnitSphere * fallSpin;
        }
    }

    private void Shake(float strength)
    {
        float t = Time.time * shakeFrequency;
        float x = Mathf.PerlinNoise(t, shakeSeed) * 2f - 1f;
        float z = Mathf.PerlinNoise(shakeSeed, t) * 2f - 1f;
        float r = Mathf.PerlinNoise(t, shakeSeed + 7f) * 2f - 1f;
        visual.localPosition = visualStartPosition + new Vector3(x, 0f, z) * (maxShake * strength);
        visual.localRotation = visualStartRotation * Quaternion.Euler(r * maxShakeAngle * strength, 0f, -x * maxShakeAngle * strength);
    }

    private void SetCrackProgress(float progress)
    {
        foreach (Renderer r in renderers)
        {
            r.GetPropertyBlock(block);
            block.SetFloat(ProgressId, progress);
            block.SetVector(ImpactId, impactPoint);
            r.SetPropertyBlock(block);
        }
    }

    private void SetCrumbleRate(float rate)
    {
        var emission = crumbleParticles.emission;
        emission.rateOverTime = rate;
    }
}
