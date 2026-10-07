using UnityEngine;

// Plays a small dust + spark burst where the player lands on a platform.
// One instance of the effect is reused for every landing.
public class PlayerLandingEffect : MonoBehaviour
{
    [SerializeField] private ParticleSystem landingEffectPrefab;
    [SerializeField] private float minImpactSpeed = 2f;
    [SerializeField] private float surfaceOffset = 0.03f;

    private ParticleSystem landingEffect;

    private void Awake()
    {
        landingEffect = Instantiate(landingEffectPrefab);
        landingEffect.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!collision.gameObject.CompareTag("Platform"))
        {
            return;
        }

        if (collision.relativeVelocity.magnitude < minImpactSpeed)
        {
            return;
        }

        // Only landings on top of a platform, not bumps into its side
        ContactPoint contact = collision.GetContact(0);
        if (Mathf.Abs(contact.normal.y) < 0.5f)
        {
            return;
        }

        Vector3 position = new Vector3(transform.position.x, contact.point.y + surfaceOffset, transform.position.z);
        landingEffect.transform.position = position;
        landingEffect.Clear(true);
        landingEffect.Play(true);
    }
}
