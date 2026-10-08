using UnityEngine;
using UnityEngine.EventSystems;

// Smooth grow on hover / shrink on press. Uses unscaled time so it works while the game is paused.
public class UIButtonFeel : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    [SerializeField] private float hoverScale = 1.06f;
    [SerializeField] private float pressScale = 0.95f;
    [SerializeField] private float speed = 18f;

    private float target = 1f;
    private bool hovered;

    private void OnDisable()
    {
        hovered = false;
        target = 1f;
        transform.localScale = Vector3.one;
    }

    private void Update()
    {
        float s = Mathf.Lerp(transform.localScale.x, target, 1f - Mathf.Exp(-speed * Time.unscaledDeltaTime));
        transform.localScale = new Vector3(s, s, 1f);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        hovered = true;
        target = hoverScale;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        hovered = false;
        target = 1f;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        target = pressScale;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        target = hovered ? hoverScale : 1f;
    }
}
