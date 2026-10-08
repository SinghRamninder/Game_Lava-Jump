using UnityEngine;

// Gentle looping pulse (used on the "NEW HIGH SCORE" banner). Unscaled time, works while paused.
public class UIPulse : MonoBehaviour
{
    [SerializeField] private float amount = 0.06f;
    [SerializeField] private float speed = 5f;

    private Vector3 baseScale;

    private void Awake()
    {
        baseScale = transform.localScale;
    }

    private void OnEnable()
    {
        transform.localScale = baseScale * 1.4f;   // pops in big, then settles into the pulse
    }

    private void Update()
    {
        float pulse = 1f + Mathf.Sin(Time.unscaledTime * speed) * amount;
        transform.localScale = Vector3.Lerp(transform.localScale, baseScale * pulse, 1f - Mathf.Exp(-12f * Time.unscaledDeltaTime));
    }
}
