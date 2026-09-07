using TMPro;
using UnityEngine;

// Floating combat-feedback text: rises and fades out over its lifetime, then
// destroys itself. Spawned by Health.TakeDamage()/Execute() above whichever
// object (player or enemy) just took the hit — see Health.cs.
public class DamageNumber : MonoBehaviour
{
    [SerializeField] private TextMeshPro label;
    [SerializeField] private float lifetime = 0.8f;
    [SerializeField] private float riseSpeed = 1.5f;

    private float elapsed;
    private Color baseColor;

    private void Awake()
    {
        if (label == null)
        {
            label = GetComponent<TextMeshPro>();
        }
    }

    public void Initialize(int damageAmount, Color color)
    {
        baseColor = color;

        if (label != null)
        {
            label.text = damageAmount.ToString();
            label.color = baseColor;
        }
    }

    private void Update()
    {
        elapsed += Time.deltaTime;
        transform.position += Vector3.up * riseSpeed * Time.deltaTime;

        if (label != null)
        {
            Color faded = baseColor;
            faded.a = 1f - Mathf.Clamp01(elapsed / lifetime);
            label.color = faded;
        }

        if (elapsed >= lifetime)
        {
            Destroy(gameObject);
        }
    }
}
