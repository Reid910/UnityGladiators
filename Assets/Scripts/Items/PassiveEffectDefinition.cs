using UnityEngine;

// Granted by whichever Head/Chest/Pants item is equipped — no manually
// -triggered button, these are reactive procs (see docs/combat-redesign-plan.md,
// "Gear as build identity"). Value's meaning depends on Effect Type:
// Lifesteal/DamageMitigation = a fraction (0.1 = 10%), AutoDodge = the
// invulnerability duration granted each trigger.
[CreateAssetMenu(fileName = "NewPassiveEffect", menuName = "UnityGladiators/Passive Effect")]
public class PassiveEffectDefinition : ScriptableObject
{
    [SerializeField] private string effectName;
    [SerializeField] private PassiveEffectType effectType;
    [SerializeField] private float value;
    [Tooltip("AutoDodge only — how often the free invulnerability window triggers.")]
    [SerializeField] private float cooldown;

    public string EffectName => effectName;
    public PassiveEffectType EffectType => effectType;
    public float Value => value;
    public float Cooldown => cooldown;
}
