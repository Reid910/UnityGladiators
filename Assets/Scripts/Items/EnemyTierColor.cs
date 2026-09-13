using UnityEngine;

// Cheap placeholder for telling enemy tiers apart before real per-tier
// prefabs/models exist — see EnemyController, which tints itself by Tier on
// spawn. Same shared-mapping pattern as RarityColor.
public static class EnemyTierColor
{
    public static Color Get(EnemyTier tier)
    {
        switch (tier)
        {
            case EnemyTier.T1:
                return Color.white;
            case EnemyTier.T2:
                return new Color(1f, 0.5f, 0.1f);
            case EnemyTier.T3:
                return new Color(0.8f, 0.1f, 0.1f);
            default:
                return Color.white;
        }
    }
}
