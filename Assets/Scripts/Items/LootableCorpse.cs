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

    [Tooltip("Optional. Tinted once the corpse becomes lootable — rarity color if it holds an item, a dim grey if it's empty — so looting doesn't require guessing. Not lootable yet = untinted.")]
    [SerializeField] private Renderer visualRenderer;

    private static readonly Color EmptyLootTint = new Color(0.25f, 0.25f, 0.25f);

    private EnemyController enemyController;
    private MaterialPropertyBlock propertyBlock;
    private bool looted;
    private bool lootPrepared;
    private bool hasDrop;
    private ItemDefinition preparedDefinition;
    private ItemRarity preparedRarity;

    private void Awake()
    {
        enemyController = GetComponent<EnemyController>();

        if (visualRenderer == null)
        {
            visualRenderer = GetComponentInChildren<Renderer>();
        }
    }

    // Called by Health.EnableCorpseHitbox() once the wave this enemy died in
    // fully clears. Rolls the drop outcome once here (rather than lazily in
    // TryLoot()) so the tint shown to the player matches exactly what
    // TryLoot() will actually produce — no re-rolling on attack.
    public void PrepareLoot()
    {
        if (lootPrepared)
        {
            return;
        }

        lootPrepared = true;

        if (possibleItems != null && possibleItems.Length > 0 && Random.value <= dropChance)
        {
            hasDrop = true;
            preparedDefinition = possibleItems[Random.Range(0, possibleItems.Length)];
            EnemyTier tier = enemyController != null ? enemyController.Tier : EnemyTier.T1;
            preparedRarity = RollRarity(tier, t3SuperRareChance);
        }

        UpdateTint();
    }

    // Returns true if this attack actually popped loot (used for feedback hooks later).
    public bool TryLoot()
    {
        if (looted || !lootPrepared)
        {
            return false;
        }

        looted = true;

        if (!hasDrop)
        {
            return false;
        }

        EquippedItem rolledItem = ItemRoller.Roll(preparedDefinition, preparedRarity);
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

    private void UpdateTint()
    {
        if (visualRenderer == null)
        {
            return;
        }

        Color tint = hasDrop ? RarityColor.Get(preparedRarity) : EmptyLootTint;

        propertyBlock ??= new MaterialPropertyBlock();
        visualRenderer.GetPropertyBlock(propertyBlock);
        propertyBlock.SetColor("_BaseColor", tint);
        propertyBlock.SetColor("_Color", tint);
        visualRenderer.SetPropertyBlock(propertyBlock);
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

        // Only one WaveManager ever exists in the scene, so deterministic
        // ordering (FindFirstObjectByType's whole reason to exist) doesn't
        // matter here — FindAnyObjectByType is the faster, non-deprecated
        // choice for a single-instance lookup like this.
        WaveManager waveManager = FindAnyObjectByType<WaveManager>();

        if (waveManager != null)
        {
            waveManager.RegisterPickup(pickupObject);
        }
    }
}
