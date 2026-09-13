using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Image))]
public class HUDFlash : MonoBehaviour
{
    [SerializeField] private float duration = .65f;
    private Image image;
    private Color baseColor;
    private float remaining;

    private void Awake()
    {
        image = GetComponent<Image>();
        baseColor = image.color;
    }

    public void SetBaseColor(Color color)
    {
        baseColor = color;
        if (remaining <= 0f) image.color = color;
    }

    public void Trigger()
    {
        remaining = duration;
        image.color = Color.white;
    }

    private void Update()
    {
        if (remaining <= 0f) return;
        remaining = Mathf.Max(0f, remaining - Time.unscaledDeltaTime);
        image.color = Color.Lerp(baseColor, Color.white, remaining / Mathf.Max(.01f, duration));
    }

    private void OnDisable()
    {
        remaining = 0f;
        if (image != null) image.color = baseColor;
    }
}
