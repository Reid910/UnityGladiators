using UnityEngine;

// Attach to enemies. Loot doesn't auto-drop on death — the player has to
// attack the corpse (see PlayerCombat's corpse-layer check in DealDamage) to
// pop the item out. Corpses/pickups themselves are cleaned up by WaveManager
// on wave transitions, not by a timer.
[RequireComponent(typeof(EnemyController))]
public class LootableCorpse : MonoBehaviour
{
    [Range(0f, 1f)]
    [SerializeField] private float dropChance = 0.5f;
    [Tooltip("Chance a T3 corpse's drop rolls SuperRare instead of Rare.")]
    [Range(0f, 1f)]
    [SerializeField] private float t3SuperRareChance = 0.3f;
    [SerializeField] private ItemDefinition[] possibleItems;
    [SerializeField] private GameObject itemPickupPrefab;

    private EnemyController enemyController;
    private bool looted;

    private void Awake()
    {
        enemyController = GetComponent<EnemyController>();
    }

    // Returns true if this attack actually popped loot (used for feedback hooks later).
    public bool TryLoot()
    {
        if (looted)
        {
            return false;
        }

        looted = true;

        if (possibleItems == null || possibleItems.Length == 0 || Random.value > dropChance)
        {
            return false;
        }

        EnemyTier tier = enemyController != null ? enemyController.Tier : EnemyTier.T1;
        ItemDefinition chosenDefinition = possibleItems[Random.Range(0, possibleItems.Length)];
        ItemRarity rarity = RollRarity(tier, t3SuperRareChance);
        EquippedItem rolledItem = ItemRoller.Roll(chosenDefinition, rarity);

        SpawnPickup(rolledItem);
        return true;
    }

    // T1 -> Common only, T2 -> Rare only, T3 -> Rare/SuperRare overlap — the
    // one deliberate spot where the toughest enemies can drop the best gear.
    private static ItemRarity RollRarity(EnemyTier tier, float t3SuperRareChance)
    {
        switch (tier)
        {
            case EnemyTier.T1:
                return ItemRarity.Common;
            case EnemyTier.T2:
                return ItemRarity.Rare;
            case EnemyTier.T3:
                return Random.value < t3SuperRareChance ? ItemRarity.SuperRare : ItemRarity.Rare;
            default:
                return ItemRarity.Common;
        }
    }

    private void SpawnPickup(EquippedItem rolledItem)
    {
        if (itemPickupPrefab == null)
        {
            Debug.LogWarning("LootableCorpse has no itemPickupPrefab assigned.", this);
            return;
        }

        GameObject pickupObject = Instantiate(itemPickupPrefab, transform.position, Quaternion.identity);
        ItemPickup itemPickup = pickupObject.GetComponent<ItemPickup>();

        if (itemPickup != null)
        {
            itemPickup.Initialize(rolledItem);
        }

        WaveManager waveManager = FindFirstObjectByType<WaveManager>();

        if (waveManager != null)
        {
            waveManager.RegisterPickup(pickupObject);
        }
    }
}
