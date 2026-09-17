using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Screen-space nameplates track world positions; no enemy prefab or AI changes.
public class EnemyStaggerHUD : MonoBehaviour
{
    public RectTransform plateTemplate;
    public Camera worldCamera;
    public Transform player;
    public ArenaMenuController menus;
    [SerializeField] private float visibleDistance = 28f;
    private float nextScan;

    private class Plate
    {
        public EnemyController enemy;
        public Stagger stagger;
        public Health health;
        public Collider collider;
        public RectTransform root;
        public Image fill;
        public TextMeshProUGUI label;
        public Image healthFill;
        public TextMeshProUGUI healthLabel;
    }
    private readonly List<Plate> plates = new List<Plate>();
    private readonly List<TextMeshPro> replacedHealthLabels = new List<TextMeshPro>();

    private void LateUpdate()
    {
        if (worldCamera == null || plateTemplate == null) return;
        if (Time.unscaledTime >= nextScan)
        {
            nextScan = Time.unscaledTime + .4f;
            replacedHealthLabels.RemoveAll(label => label == null);
            foreach (var enemy in FindObjectsByType<EnemyController>(FindObjectsSortMode.None))
            {
                if (plates.Exists(p => p.enemy == enemy)) continue;
                var stagger = enemy.GetComponent<Stagger>();
                var health = enemy.GetComponent<Health>();
                if (stagger == null || health == null || health.IsDead) continue;
                var root = Instantiate(plateTemplate, transform);
                root.name = "Stagger - " + enemy.name;
                var healthTrack = root.Find("Health track/Fill");
                if (healthTrack != null)
                {
                    // Preserve the old world text object; suppress only the numeric
                    // health readout now represented by this combined nameplate.
                    foreach (var text in enemy.GetComponentsInChildren<TextMeshPro>(true))
                    {
                        if (!text.enabled || text.text != health.CurrentHealth + " / " + health.MaxHealth) continue;
                        replacedHealthLabels.Add(text);
                        text.enabled = false;
                    }
                }
                plates.Add(new Plate { enemy = enemy, stagger = stagger, health = health,
                    collider = enemy.GetComponent<Collider>(), root = root,
                    healthFill = healthTrack != null ? healthTrack.GetComponent<Image>() : null,
                    healthLabel = root.Find("Health value")?.GetComponent<TextMeshProUGUI>(),
                    fill = root.Find("Track/Fill").GetComponent<Image>(), label = root.Find("Status").GetComponent<TextMeshProUGUI>() });
            }
        }
        for (int i = plates.Count - 1; i >= 0; i--)
        {
            var plate = plates[i];
            if (plate.enemy == null || plate.health == null || plate.health.IsDead)
            {
                Destroy(plate.root.gameObject);
                plates.RemoveAt(i);
                continue;
            }
            Vector3 head = plate.collider != null
                ? new Vector3(plate.collider.bounds.center.x, plate.collider.bounds.max.y + .3f, plate.collider.bounds.center.z)
                : plate.enemy.transform.position + Vector3.up * 2.4f;
            Vector3 viewport = worldCamera.WorldToViewportPoint(head);
            bool visible = plate.enemy.gameObject.activeInHierarchy && (menus == null || !menus.IsSuspended) &&
                viewport.z > 0f && viewport.x > 0f && viewport.x < 1f && viewport.y > 0f && viewport.y < 1f &&
                (player == null || Vector3.Distance(player.position, head) < visibleDistance);
            if (visible && Physics.Linecast(worldCamera.transform.position, head, out RaycastHit hit, ~0, QueryTriggerInteraction.Ignore))
                visible = hit.transform == plate.enemy.transform || hit.transform.IsChildOf(plate.enemy.transform);
            plate.root.gameObject.SetActive(visible);
            if (!visible) continue;
            RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)transform,
                worldCamera.WorldToScreenPoint(head), null, out Vector2 position);
            plate.root.anchoredPosition = position;
            bool broken = plate.stagger.IsBroken;
            float amount = broken ? 1f : Mathf.Clamp01(plate.stagger.CurrentStagger / Mathf.Max(1f, plate.stagger.MaxStagger));
            plate.fill.rectTransform.localScale = new Vector3(amount, 1f, 1f);
            plate.fill.color = broken ? new Color(1f,.3f,.12f) : new Color(.9f,.69f,.3f);
            plate.label.text = broken ? "BROKEN - FINISHER" : "STAGGER  " + Mathf.RoundToInt(plate.stagger.CurrentStagger) + " / " + Mathf.RoundToInt(plate.stagger.MaxStagger);
            plate.label.color = broken ? new Color(1f,.78f,.42f) : new Color(.92f,.87f,.75f);
            if (plate.healthFill != null)
                plate.healthFill.rectTransform.localScale = new Vector3(Mathf.Clamp01((float)plate.health.CurrentHealth / Mathf.Max(1,plate.health.MaxHealth)),1f,1f);
            if (plate.healthLabel != null)
                plate.healthLabel.text = plate.health.CurrentHealth + " / " + plate.health.MaxHealth;
        }
    }

    private void OnDisable()
    {
        foreach (var text in replacedHealthLabels) if (text != null) text.enabled = true;
        replacedHealthLabels.Clear();
        foreach (var plate in plates) if (plate.root != null) Destroy(plate.root.gameObject);
        plates.Clear();
        nextScan = 0f;
    }
}
