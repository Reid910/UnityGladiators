using System.Collections.Generic;
using UnityEngine;

// Rolls a dropped ItemDefinition into a concrete EquippedItem instance.
// Rarity is decided by the caller (loot table, see M3's per-enemy-tier gating)
// and passed in here — it isn't stored on ItemDefinition itself, since the
// same template can drop at any rarity.
public static class ItemRoller
{
    // Rarity widens/raises roll ranges on top of the extra affix at SuperRare.
    private static readonly Dictionary<ItemRarity, float> RarityRollMultiplier = new Dictionary<ItemRarity, float>
    {
        { ItemRarity.Common, 1f },
        { ItemRarity.Rare, 1.3f },
        { ItemRarity.SuperRare, 1.6f },
    };

    private static readonly Dictionary<ItemRarity, int> RarityAffixCount = new Dictionary<ItemRarity, int>
    {
        { ItemRarity.Common, 1 },
        { ItemRarity.Rare, 1 },
        { ItemRarity.SuperRare, 2 },
    };

    public static EquippedItem Roll(ItemDefinition definition, ItemRarity rarity)
    {
        float multiplier = RarityRollMultiplier[rarity];

        int rolledDamage = Mathf.RoundToInt(
            Random.Range(definition.MinDamage, definition.MaxDamage + 1) * multiplier
        );

        List<RolledAffix> rolledAffixes = RollAffixes(definition, rarity, multiplier);

        return new EquippedItem(definition, rarity, rolledDamage, rolledAffixes);
    }

    private static List<RolledAffix> RollAffixes(ItemDefinition definition, ItemRarity rarity, float multiplier)
    {
        List<RolledAffix> result = new List<RolledAffix>();
        List<AffixDefinition> eligibleAffixes = new List<AffixDefinition>();

        if (definition.PossibleAffixes != null)
        {
            foreach (AffixDefinition affix in definition.PossibleAffixes)
            {
                if (affix != null && affix.IsEligibleForSlot(definition.Slot))
                {
                    eligibleAffixes.Add(affix);
                }
            }
        }

        int affixCount = Mathf.Min(RarityAffixCount[rarity], eligibleAffixes.Count);

        for (int i = 0; i < affixCount; i++)
        {
            int randomIndex = Random.Range(0, eligibleAffixes.Count);
            AffixDefinition chosenAffix = eligibleAffixes[randomIndex];
            eligibleAffixes.RemoveAt(randomIndex);

            float rolledValue = Random.Range(chosenAffix.MinValue, chosenAffix.MaxValue) * multiplier;

            result.Add(new RolledAffix { definition = chosenAffix, rolledValue = rolledValue });
        }

        return result;
    }
}
