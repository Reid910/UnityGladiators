using UnityEngine;

// Passive baseline power that scales with character level, fully decoupled
// from gear (see docs/combat-redesign-plan.md's "Gear as build identity") —
// frees gear from being the only source of raw power. Kills-only XP, resets
// every run (no persistence — consistent with TODO.md's "meta-progression
// between runs" being explicitly out of scope). Grants only the two
// universal power stats (MaxHealth, flat damage) deliberately — everything
// else stays purely Stat-Shard-driven so shards keep a clear job.
public class PlayerLevel : MonoBehaviour
{
    [SerializeField] private Health health;
    [SerializeField] private PlayerStats playerStats;

    [Header("XP (placeholders, all untuned)")]
    [SerializeField] private int xpPerKill = 10;
    [Tooltip("XP required to go from level N to N+1 = this * N — a simple linear curve, not tuned.")]
    [SerializeField] private int baseXpToLevel = 50;

    [Header("Per-level bonuses (placeholders, all untuned)")]
    [SerializeField] private int maxHealthPerLevel = 10;
    [SerializeField] private int damagePerLevel = 2;

    private int currentXp;

    public int Level { get; private set; } = 1;
    public int CurrentXp => currentXp;
    public int XpToNextLevel => baseXpToLevel * Level;

    private void Awake()
    {
        if (health == null)
        {
            health = GetComponent<Health>();
        }

        if (playerStats == null)
        {
            playerStats = GetComponent<PlayerStats>();
        }
    }

    private void OnEnable()
    {
        ApplyLevelBonuses();
    }

    // Called by WaveManager.OnEnemyDied — see docs/combat-redesign-plan.md.
    public void AddKillXp()
    {
        currentXp += xpPerKill;

        while (currentXp >= XpToNextLevel)
        {
            currentXp -= XpToNextLevel;
            Level++;
        }

        ApplyLevelBonuses();
    }

    private void ApplyLevelBonuses()
    {
        int bonusLevels = Level - 1;

        if (health != null)
        {
            health.SetLevelMaxHealthBonus(bonusLevels * maxHealthPerLevel);
        }

        if (playerStats != null)
        {
            playerStats.SetLevelDamageBonus(bonusLevels * damagePerLevel);
        }
    }
}
