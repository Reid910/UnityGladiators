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
    private readonly List<ItemPickup> nearbyPickups = new List<ItemPickup>();
    private ItemPickup targetedPickup;

    public bool HasNearbyPickup => targetedPickup != null;

    // Only worth re-checking every frame while more than one pickup
    // overlaps — with 0 or 1 in range the target can't change without an
    // enter/exit event, which already triggers a refresh on its own.
    private void Update()
    {
        if (nearbyPickups.Count > 1)
        {
            RefreshTarget();
        }
    }

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

    // Called by ItemPickup while the player stands in its trigger. Doesn't
    // pick the target itself — just joins the candidate pool, then
    // RefreshTarget() picks whichever candidate is actually closest.
    public void RegisterNearby(ItemPickup pickup)
    {
        if (pickup == null || pickup.Item?.Definition == null || nearbyPickups.Contains(pickup))
        {
            return;
        }

        nearbyPickups.Add(pickup);
        RefreshTarget();
    }

    public void UnregisterNearby(ItemPickup pickup)
    {
        if (!nearbyPickups.Remove(pickup))
        {
            return;
        }

        RefreshTarget();
    }

    // Re-picks the closest pickup among everything currently in range.
    // Called whenever the candidate set changes (enter/exit) and, while
    // more than one pickup overlaps, every frame (see Update()) so the
    // target stays correct as the player moves between them.
    private void RefreshTarget()
    {
        ItemPickup closest = null;
        float closestSqrDistance = float.MaxValue;

        foreach (ItemPickup pickup in nearbyPickups)
        {
            if (pickup == null)
            {
                continue;
            }

            float sqrDistance = (pickup.transform.position - transform.position).sqrMagnitude;

            if (sqrDistance < closestSqrDistance)
            {
                closestSqrDistance = sqrDistance;
                closest = pickup;
            }
        }

        if (closest == targetedPickup)
        {
            return;
        }

        if (targetedPickup != null)
        {
            targetedPickup.SetTargeted(false, null);
        }

        targetedPickup = closest;

        if (targetedPickup != null)
        {
            targetedPickup.SetTargeted(true, GetEquipped(targetedPickup.Item.Definition.Slot));
        }
    }

    // Swaps whatever's currently in the targeted pickup's slot with the
    // player's equipped item there. Returns false if nothing's in range.
    public bool TrySwapWithNearby()
    {
        if (targetedPickup == null || targetedPickup.Item == null)
        {
            return false;
        }

        ItemPickup pickup = targetedPickup;
        EquippedItem previousItem = Equip(pickup.Item);

        if (previousItem == null)
        {
            nearbyPickups.Remove(pickup);
            targetedPickup = null;
            Destroy(pickup.gameObject);
            RefreshTarget();
        }
        else
        {
            // Same world object becomes the previously-equipped item instead
            // of spawning a new pickup — keeps this a straight swap in place.
            // Still the targeted pickup, so refresh its targeted feedback too.
            pickup.Initialize(previousItem);
            pickup.SetTargeted(true, GetEquipped(previousItem.Definition.Slot));
        }

        return true;
    }
}
