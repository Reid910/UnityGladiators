public enum ItemSlot
{
    Head,
    Chest,
    Pants,
    Boots,
    Weapon,
    // Added for the combat identity redesign's gear-as-build-identity model
    // (see docs/combat-redesign-plan.md): Weapon/Boots/Gloves are the three
    // active, button-pressed slots (Ability/Dash/Deflect), Head/Chest/Pants
    // are passive, Stat Shard carries the old numeric-affix system now that
    // armor pieces carry fixed effects instead.
    Gloves,
    StatShard,
}
