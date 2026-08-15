using UnityEngine;

// World object for a dropped item. Fortnite-style instant swap: walking over
// it immediately equips it and drops whatever was in that slot before, no
// pickup/equip menu step.
public class ItemPickup : MonoBehaviour
{
    private EquippedItem item;

    public EquippedItem Item => item;

    public void Initialize(EquippedItem rolledItem)
    {
        item = rolledItem;
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
    }
}
