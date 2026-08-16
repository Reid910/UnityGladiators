using UnityEngine;

[CreateAssetMenu(fileName = "NewAffix", menuName = "UnityGladiators/Affix")]
public class AffixDefinition : ScriptableObject
{
    [SerializeField] private StatType statType;
    [SerializeField] private float minValue;
    [SerializeField] private float maxValue;
    [Tooltip("Which slots can roll this affix. Empty = eligible on any slot.")]
    [SerializeField] private ItemSlot[] eligibleSlots;

    public StatType StatType => statType;
    public float MinValue => minValue;
    public float MaxValue => maxValue;
    public ItemSlot[] EligibleSlots => eligibleSlots;

    public bool IsEligibleForSlot(ItemSlot slot)
    {
        if (eligibleSlots == null || eligibleSlots.Length == 0)
        {
            return true;
        }

        foreach (ItemSlot eligibleSlot in eligibleSlots)
        {
            if (eligibleSlot == slot)
            {
                return true;
            }
        }

        return false;
    }
}
