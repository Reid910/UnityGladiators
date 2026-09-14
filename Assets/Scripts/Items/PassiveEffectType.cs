// One concrete effect per Head/Chest/Pants theme, as a starting point —
// see docs/combat-redesign-plan.md's Gear as build identity section, which
// lists more flavor options (crit/bleed/poison for Head, heal/tank for
// Chest) as future content once this pattern is proven out. Adding a new
// type here is the same shape of work as adding a new StatType affix.
public enum PassiveEffectType
{
    // Head: heals the player for a fraction of damage dealt.
    Lifesteal,
    // Chest: reduces incoming damage by a fraction, applied in Health.TakeDamage
    // alongside Armor.
    DamageMitigation,
    // Pants: grants a brief automatic invulnerability window on a cooldown,
    // modifying how reliably Boots' Dash-granted defense is backed up — see
    // PlayerCombat's Pants Auto-Dodge header.
    AutoDodge,
}
