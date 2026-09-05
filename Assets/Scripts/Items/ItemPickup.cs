using TMPro;
using UnityEngine;

// World object for a dropped item. Fortnite-style instant swap: walking over
// it immediately equips it and drops whatever was in that slot before, no
// pickup/equip menu step.
public class ItemPickup : MonoBehaviour
{
    [Tooltip("Optional. Shows item name colored by rarity so drops read at a glance with no UI.")]
    [SerializeField] private TextMeshPro nameLabel;
    [Tooltip("Optional. Tinted by rarity so drops read at a glance even without the name label.")]
    [SerializeField] private Renderer visualRenderer;

    private EquippedItem item;
    private MaterialPropertyBlock propertyBlock;

    public EquippedItem Item => item;

    private void Awake()
    {
        if (visualRenderer == null)
        {
            visualRenderer = GetComponentInChildren<Renderer>();
        }
    }

    public void Initialize(EquippedItem rolledItem)
    {
        item = rolledItem;
        UpdateLabel();
        UpdateColor();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (item == null)
        {
            return;
        }

        PlayerEquipment equipment = other.GetComponentInParent<PlayerEquipment>();

        if (equipment == null)
        {
            return;
        }

        EquippedItem previousItem = equipment.Equip(item);

        if (previousItem == null)
        {
            Destroy(gameObject);
            return;
        }

        // Become the previously equipped item instead of spawning a new
        // pickup object — keeps this the same world object, just swapped.
        item = previousItem;
        UpdateLabel();
        UpdateColor();
    }

    private void UpdateLabel()
    {
        if (nameLabel == null || item?.Definition == null)
        {
            return;
        }

        nameLabel.text = item.Definition.ItemName;
        nameLabel.color = RarityColor.Get(item.Rarity);
    }

    private void UpdateColor()
    {
        if (visualRenderer == null || item == null)
        {
            return;
        }

        propertyBlock ??= new MaterialPropertyBlock();
        visualRenderer.GetPropertyBlock(propertyBlock);
        propertyBlock.SetColor("_BaseColor", RarityColor.Get(item.Rarity));
        propertyBlock.SetColor("_Color", RarityColor.Get(item.Rarity));
        visualRenderer.SetPropertyBlock(propertyBlock);
    }
}
