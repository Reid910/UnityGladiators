using UnityEngine;

// Shared color mapping so pickups, equipped-item UI, etc. all read rarity
// the same way at a glance without needing a tooltip.
public static class RarityColor
{
    public static Color Get(ItemRarity rarity)
    {
        switch (rarity)
        {
            case ItemRarity.Common:
                return Color.white;
            case ItemRarity.Rare:
                return new Color(0.35f, 0.55f, 1f);
            case ItemRarity.SuperRare:
                return new Color(1f, 0.65f, 0f);
            default:
                return Color.white;
        }
    }
}
