using System;
using System.Collections.Generic;
using UnityEngine;

// Tracks which EquippedItem is in each slot. Fortnite-style instant swap:
// equipping a new item just returns whatever was there before, no inventory.
public class PlayerEquipment : MonoBehaviour
{
    public event Action<ItemSlot, EquippedItem> ItemEquipped;

    private readonly Dictionary<ItemSlot, EquippedItem> equippedItems = new Dictionary<ItemSlot, EquippedItem>();

    public EquippedItem GetEquipped(ItemSlot slot)
    {
        return equippedItems.TryGetValue(slot, out EquippedItem item) ? item : null;
    }

    // Equips newItem into its slot. Returns whatever was previously equipped
    // there (null if the slot was empty), so the caller can drop it.
    public EquippedItem Equip(EquippedItem newItem)
    {
        if (newItem == null || newItem.Definition == null)
        {
            return null;
        }

        ItemSlot slot = newItem.Definition.Slot;
        EquippedItem previousItem = GetEquipped(slot);

        equippedItems[slot] = newItem;
        ItemEquipped?.Invoke(slot, newItem);

        return previousItem;
    }
}
