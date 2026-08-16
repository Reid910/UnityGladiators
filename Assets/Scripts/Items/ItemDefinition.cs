using UnityEngine;

[CreateAssetMenu(fileName = "NewItem", menuName = "UnityGladiators/Item")]
public class ItemDefinition : ScriptableObject
{
    [SerializeField] private string itemName;
    [SerializeField] private ItemSlot slot;
    [Tooltip("Base roll range at Common rarity. Rarity multiplies this at drop time (see ItemRoller).")]
    [SerializeField] private int minDamage;
    [SerializeField] private int maxDamage;
    [SerializeField] private AffixDefinition[] possibleAffixes;

    [Header("Weapon slot only")]
    [SerializeField] private AbilityDefinition abilityDefinition;

    [Header("Boots slot only")]
    [SerializeField] private DashDefinition dashDefinition;

    public string ItemName => itemName;
    public ItemSlot Slot => slot;
    public int MinDamage => minDamage;
    public int MaxDamage => maxDamage;
    public AffixDefinition[] PossibleAffixes => possibleAffixes;
    public AbilityDefinition AbilityDefinition => abilityDefinition;
    public DashDefinition DashDefinition => dashDefinition;
}
