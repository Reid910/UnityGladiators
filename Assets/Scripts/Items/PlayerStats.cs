using System;
using System.Collections.Generic;
using UnityEngine;

// Aggregates base stats + every equipped item's affixes. Recalculates
// whenever PlayerEquipment reports a change. Health.cs and PlayerCombat.cs
// read from this instead of using fixed hardcoded values directly.
public class PlayerStats : MonoBehaviour
{
    [SerializeField] private PlayerEquipment equipment;
    [SerializeField] private Health health;

    [Header("Base Stats (before equipment)")]
    [SerializeField] private int baseDamage = 10;

    private readonly Dictionary<StatType, float> statTotals = new Dictionary<StatType, float>();
    private int totalDamage;

    public int TotalDamage => totalDamage;

    private void Awake()
    {
        if (equipment == null)
        {
            equipment = GetComponent<PlayerEquipment>();
        }

        if (health == null)
        {
            health = GetComponent<Health>();
        }
    }

    private void OnEnable()
    {
        if (equipment != null)
        {
            equipment.ItemEquipped += OnItemEquipped;
        }

        Recalculate();
    }

    private void OnDisable()
    {
        if (equipment != null)
        {
            equipment.ItemEquipped -= OnItemEquipped;
        }
    }

    public float GetStat(StatType statType)
    {
        return statTotals.TryGetValue(statType, out float value) ? value : 0f;
    }

    private void OnItemEquipped(ItemSlot slot, EquippedItem item)
    {
        Recalculate();
    }

    private void Recalculate()
    {
        statTotals.Clear();
        totalDamage = baseDamage;

        if (equipment != null)
        {
            foreach (ItemSlot slot in (ItemSlot[])Enum.GetValues(typeof(ItemSlot)))
            {
                EquippedItem item = equipment.GetEquipped(slot);

                if (item == null)
                {
                    continue;
                }

                totalDamage += item.RolledDamage;

                foreach (RolledAffix affix in item.Affixes)
                {
                    if (affix.definition == null)
                    {
                        continue;
                    }

                    StatType statType = affix.definition.StatType;
                    statTotals.TryGetValue(statType, out float currentValue);
                    statTotals[statType] = currentValue + affix.rolledValue;
                }
            }
        }

        if (health != null)
        {
            health.SetMaxHealthBonus(Mathf.RoundToInt(GetStat(StatType.MaxHealth)));
        }
    }
}
