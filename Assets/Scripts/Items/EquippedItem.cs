using System.Collections.Generic;

// Runtime instance of a dropped/equipped item — the rolled result, distinct
// from ItemDefinition which is just the template. Built by ItemRoller.
public class EquippedItem
{
    public ItemDefinition Definition { get; }
    public ItemRarity Rarity { get; }
    public int RolledDamage { get; }
    public IReadOnlyList<RolledAffix> Affixes { get; }

    public EquippedItem(
        ItemDefinition definition,
        ItemRarity rarity,
        int rolledDamage,
        List<RolledAffix> affixes)
    {
        Definition = definition;
        Rarity = rarity;
        RolledDamage = rolledDamage;
        Affixes = affixes;
    }
}
