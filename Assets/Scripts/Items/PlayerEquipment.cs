using System;
using System.Collections.Generic;
using UnityEngine;

// Tracks which EquippedItem is in each slot. No inventory: swapping a new
// item in just returns whatever was there before, which the caller drops.
// Swap-in is button-triggered (see TrySwapWithNearby, called from
// PlayerCombat on the Interact input) rather than instant-on-touch — see
// ItemPickup for the trigger-enter/exit range tracking that feeds this.
public class PlayerEquipment : MonoBehaviour
{
    public event Action<ItemSlot, EquippedItem> ItemEquipped;

    private readonly Dictionary<ItemSlot, EquippedItem> equippedItems = new Dictionary<ItemSlot, EquippedItem>();
    private ItemPickup nearbyPickup;

    public bool HasNearbyPickup => nearbyPickup != null;

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

    // Called by ItemPickup while the player stands in its trigger — the most
    // recently entered pickup wins if more than one overlaps at once, and
    // the one it displaces has its targeted feedback cleared so only one
    // pickup ever shows as the swap target at a time.
    public void RegisterNearby(ItemPickup pickup)
    {
        if (pickup == null || pickup.Item?.Definition == null)
        {
            return;
        }

        if (nearbyPickup != null && nearbyPickup != pickup)
        {
            nearbyPickup.SetTargeted(false, null);
        }

        nearbyPickup = pickup;
        pickup.SetTargeted(true, GetEquipped(pickup.Item.Definition.Slot));
    }

    // Only clears if it's still the one that registered — an exit event from
    // a pickup that already lost the "nearby" slot to another one shouldn't
    // clear the newer reference.
    public void UnregisterNearby(ItemPickup pickup)
    {
        if (nearbyPickup != pickup)
        {
            return;
        }

        nearbyPickup = null;
        pickup.SetTargeted(false, null);
    }

    // Swaps whatever's currently in the nearby pickup's slot with the
    // player's equipped item there. Returns false if nothing's in range.
    public bool TrySwapWithNearby()
    {
        if (nearbyPickup == null || nearbyPickup.Item == null)
        {
            return false;
        }

        ItemPickup pickup = nearbyPickup;
        EquippedItem previousItem = Equip(pickup.Item);

        if (previousItem == null)
        {
            nearbyPickup = null;
            Destroy(pickup.gameObject);
        }
        else
        {
            // Same world object becomes the previously-equipped item instead
            // of spawning a new pickup — keeps this a straight swap in place.
            // Still the nearby target, so refresh its targeted feedback too.
            pickup.Initialize(previousItem);
            pickup.SetTargeted(true, GetEquipped(previousItem.Definition.Slot));
        }

        return true;
    }
}
