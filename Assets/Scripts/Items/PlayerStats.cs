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
    private int levelDamageBonus;

    public int TotalDamage => totalDamage;

    // Called by PlayerLevel — kept separate from gear's rolled damage so
    // levelling and itemization are independent power sources, per
    // docs/combat-redesign-plan.md's "Gear as build identity."
    public void SetLevelDamageBonus(int bonus)
    {
        levelDamageBonus = bonus;
        Recalculate();
    }

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
        totalDamage = baseDamage + levelDamageBonus;

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
            health.SetArmor(GetStat(StatType.Armor));

            // Chest passive effect (see docs/combat-redesign-plan.md) — the
            // other two effect types (Lifesteal, AutoDodge) are read
            // directly by PlayerCombat at the moment they trigger instead of
            // through this static recalculation, since they're reactive to
            // specific events (a hit landing, a cooldown), not a standing value.
            PassiveEffectDefinition chestEffect = equipment?.GetEquipped(ItemSlot.Chest)?.Definition?.PassiveEffectDefinition;
            float mitigation = chestEffect != null && chestEffect.EffectType == PassiveEffectType.DamageMitigation
                ? chestEffect.Value
                : 0f;
            health.SetDamageMitigation(mitigation);
        }
    }
}
