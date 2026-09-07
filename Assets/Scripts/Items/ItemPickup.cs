using TMPro;
using UnityEngine;

// World object for a dropped item. No inventory: standing in range and
// pressing Interact (see PlayerCombat) swaps it with whatever's equipped in
// that slot — walking over it alone no longer auto-equips (see
// PlayerEquipment.TrySwapWithNearby for the actual swap).
public class ItemPickup : MonoBehaviour
{
    [Tooltip("Optional. Shows item name colored by rarity so drops read at a glance with no UI.")]
    [SerializeField] private TextMeshPro nameLabel;
    [Tooltip("Optional. Scaled up while this is the player's swap target — assign a child mesh transform here, NOT this object's own transform, so the trigger collider doesn't grow with it.")]
    [SerializeField] private Transform visualTransform;
    [Tooltip("Scale multiplier applied to Visual Transform while targeted.")]
    [SerializeField] private float targetedScaleMultiplier = 1.25f;

    private EquippedItem item;
    private EquippedItem targetComparisonItem;
    private Vector3 visualBaseScale = Vector3.one;
    private bool isTargeted;

    public EquippedItem Item => item;

    private void Awake()
    {
        if (visualTransform != null)
        {
            visualBaseScale = visualTransform.localScale;
        }
    }

    public void Initialize(EquippedItem rolledItem)
    {
        item = rolledItem;
        UpdateLabel();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (item == null)
        {
            return;
        }

        PlayerEquipment equipment = other.GetComponentInParent<PlayerEquipment>();
        equipment?.RegisterNearby(this);
    }

    private void OnTriggerExit(Collider other)
    {
        PlayerEquipment equipment = other.GetComponentInParent<PlayerEquipment>();
        equipment?.UnregisterNearby(this);
    }

    // Called by PlayerEquipment when this becomes (or stops being) the
    // pickup that pressing Interact would swap in. currentlyEquipped is
    // whatever's presently in this item's slot, shown as a "you'd give this
    // up" comparison so the swap isn't a guess.
    public void SetTargeted(bool targeted, EquippedItem currentlyEquipped)
    {
        isTargeted = targeted;
        targetComparisonItem = currentlyEquipped;

        if (visualTransform != null)
        {
            visualTransform.localScale = targeted ? visualBaseScale * targetedScaleMultiplier : visualBaseScale;
        }

        UpdateLabel();
    }

    private void UpdateLabel()
    {
        if (nameLabel == null || item?.Definition == null)
        {
            return;
        }

        if (isTargeted)
        {
            string replacedName = targetComparisonItem?.Definition != null
                ? targetComparisonItem.Definition.ItemName
                : "(empty)";
            nameLabel.text = "[E] " + item.Definition.ItemName + "\n<size=70%>swaps " + replacedName + "</size>";
        }
        else
        {
            nameLabel.text = item.Definition.ItemName;
        }

        nameLabel.color = RarityColor.Get(item.Rarity);
    }
}
