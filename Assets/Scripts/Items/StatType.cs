// The shared affix pool. Slot theming (Chest = survivability, Head = accuracy/
// detection, Pants = mobility) is expressed through which slots each
// AffixDefinition is eligible for, not through separate enums per slot.
public enum StatType
{
    AttackSpeed,
    CritChance,
    AbilityCooldownReduction,
    MoveSpeed,
    MaxHealth,
    Armor,
}
