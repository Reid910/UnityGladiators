using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerCombat : MonoBehaviour
{
    [System.Serializable]
    private struct ComboHit
    {
        public int damage;
        public float staggerAmount;
        public float hitstunDuration;
        public float recoveryTime;
        public string animatorTrigger;
    }

    [Header("Light Combo")]
    // animatorTrigger reuses the existing "Attack" parameter (the only one the
    // current Animator Controllers actually have) rather than distinct
    // per-hit triggers — every light hit plays the same swing animation for
    // now. Give each hit its own trigger name here once real animations
    // exist and the Controllers have matching states/transitions.
    [SerializeField]
    private ComboHit[] lightComboHits =
    {
        new ComboHit { damage = 15, staggerAmount = 12f, hitstunDuration = 0.2f, recoveryTime = 0.35f, animatorTrigger = "Attack" },
        new ComboHit { damage = 18, staggerAmount = 12f, hitstunDuration = 0.2f, recoveryTime = 0.35f, animatorTrigger = "Attack" },
        new ComboHit { damage = 28, staggerAmount = 18f, hitstunDuration = 0.25f, recoveryTime = 0.5f, animatorTrigger = "Attack" },
    };
    [SerializeField] private float comboWindow = 0.8f;

    [Header("Heavy Attack")]
    [SerializeField] private int heavyDamage = 40;
    [SerializeField] private float heavyStaggerAmount = 35f;
    [SerializeField] private float heavyHitstunDuration = 0.35f;
    [SerializeField] private float heavyRecoveryTime = 0.9f;

    [Header("Attack")]
    [SerializeField] private Transform attackPoint;
    [SerializeField] private float attackRange = 1.5f;
    [SerializeField] private LayerMask enemyLayer;
    [Tooltip("Separate from enemyLayer — dead enemies' corpse hitboxes (see Health.corpseHitbox) live here so attacks can loot them instead of dealing damage.")]
    [SerializeField] private LayerMask corpseLayer;

    [Header("References")]
    [SerializeField] private Animator animator;
    [SerializeField] private Health health;
    [SerializeField] private CharacterController characterController;
    [SerializeField] private Stagger stagger;
    [SerializeField] private Hitstun hitstun;
    [SerializeField] private PlayerStats playerStats;
    [SerializeField] private PlayerEquipment equipment;

    private InputSystem_Actions inputSystemActions;

    private int comboStep;
    private float comboResetTime;
    private float nextAttackTime;
    private float nextAbilityTime;
    private float nextDashTime;

    public int ComboStep => comboStep;
    public float AbilityCooldownRemaining => Mathf.Max(0f, nextAbilityTime - Time.time);
    public float DashCooldownRemaining => Mathf.Max(0f, nextDashTime - Time.time);

    private bool IsDead => health != null && health.IsDead;

    // The player can't act while stunned from a hit or broken from stagger —
    // mirrors the same restriction EnemyController applies to enemies.
    private bool IsIncapacitated =>
        IsDead ||
        (hitstun != null && hitstun.IsStunned) ||
        (stagger != null && stagger.IsBroken);

    private void Awake()
    {
        inputSystemActions = new InputSystem_Actions();

        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }

        if (health == null)
        {
            health = GetComponent<Health>();
        }

        if (characterController == null)
        {
            characterController = GetComponent<CharacterController>();
        }

        if (stagger == null)
        {
            stagger = GetComponent<Stagger>();
        }

        if (hitstun == null)
        {
            hitstun = GetComponent<Hitstun>();
        }

        if (playerStats == null)
        {
            playerStats = GetComponent<PlayerStats>();
        }

        if (equipment == null)
        {
            equipment = GetComponent<PlayerEquipment>();
        }
    }

    private void OnEnable()
    {
        inputSystemActions.Player.Enable();
        inputSystemActions.Player.Attack.performed += OnAttackPerformed;
        inputSystemActions.Player.Heavy.performed += OnHeavyPerformed;
        inputSystemActions.Player.Ability.performed += OnAbilityPerformed;
        inputSystemActions.Player.Dash.performed += OnDashPerformed;
    }

    private void OnDisable()
    {
        inputSystemActions.Player.Attack.performed -= OnAttackPerformed;
        inputSystemActions.Player.Heavy.performed -= OnHeavyPerformed;
        inputSystemActions.Player.Ability.performed -= OnAbilityPerformed;
        inputSystemActions.Player.Dash.performed -= OnDashPerformed;
        inputSystemActions.Player.Disable();
    }

    private void OnAttackPerformed(InputAction.CallbackContext context) => TryLightAttack();

    private void OnHeavyPerformed(InputAction.CallbackContext context) => TryHeavyAttack();

    private void OnAbilityPerformed(InputAction.CallbackContext context) => TryUseAbility();

    private void OnDashPerformed(InputAction.CallbackContext context) => TryDash();

    private void TryLightAttack()
    {
        if (IsIncapacitated || Time.time < nextAttackTime || lightComboHits.Length == 0)
        {
            return;
        }

        if (Time.time > comboResetTime)
        {
            comboStep = 0;
        }

        int hitIndex = comboStep % lightComboHits.Length;
        ComboHit hit = lightComboHits[hitIndex];

        DealDamage(hit.damage, hit.staggerAmount, hit.hitstunDuration, hit.animatorTrigger);

        comboStep++;
        nextAttackTime = Time.time + ApplyAttackSpeed(hit.recoveryTime);
        comboResetTime = nextAttackTime + comboWindow;
    }

    private void TryHeavyAttack()
    {
        if (IsIncapacitated || Time.time < nextAttackTime)
        {
            return;
        }

        // Reuses "Attack" too (see lightComboHits comment) — no distinct
        // heavy-swing animation exists yet.
        DealDamage(heavyDamage, heavyStaggerAmount, heavyHitstunDuration, "Attack");

        // Heavy attack interrupts and resets the light combo chain.
        comboStep = 0;
        nextAttackTime = Time.time + ApplyAttackSpeed(heavyRecoveryTime);
        comboResetTime = nextAttackTime;
    }

    // Attack Speed affix (Head-flavored, see TODO.md) shortens recovery time.
    private float ApplyAttackSpeed(float recoveryTime)
    {
        float attackSpeedMultiplier = 1f + (playerStats != null ? playerStats.GetStat(StatType.AttackSpeed) : 0f);
        return recoveryTime / Mathf.Max(0.1f, attackSpeedMultiplier);
    }

    private void TryUseAbility()
    {
        if (IsIncapacitated || Time.time < nextAbilityTime)
        {
            return;
        }

        // Ash-of-War style: the ability comes from whichever weapon is
        // equipped. No weapon (or a weapon with no AbilityDefinition) means
        // the ability button does nothing.
        AbilityDefinition abilityDefinition = equipment?.GetEquipped(ItemSlot.Weapon)?.Definition?.AbilityDefinition;

        if (abilityDefinition == null)
        {
            return;
        }

        float cooldownReduction = playerStats != null ? playerStats.GetStat(StatType.AbilityCooldownReduction) : 0f;
        float effectiveCooldown = Mathf.Max(0.1f, abilityDefinition.Cooldown * (1f - cooldownReduction));

        // No animator trigger fired yet — abilityDefinition.AnimatorTrigger
        // names a state ("AbilityCast" by default) that doesn't exist in the
        // current Animator Controllers. The ability still functions
        // (cooldown/effect), it just won't visibly animate until real states
        // are built for it.

        nextAbilityTime = Time.time + effectiveCooldown;
    }

    private void TryDash()
    {
        if (IsIncapacitated || Time.time < nextDashTime || characterController == null)
        {
            return;
        }

        // Risk of Rain shift-style: the dash comes from whichever boots are
        // equipped. No boots (or boots with no DashDefinition) means no dash.
        DashDefinition dashDefinition = equipment?.GetEquipped(ItemSlot.Boots)?.Definition?.DashDefinition;

        if (dashDefinition == null)
        {
            return;
        }

        characterController.Move(transform.forward * dashDefinition.Distance);

        if (dashDefinition.DealsDamage)
        {
            // No "Dash" animator trigger exists yet (see TryUseAbility), so
            // no animatorTrigger is passed here — damage/stagger/hitstun and
            // corpse looting still apply.
            DealDamage(dashDefinition.Damage, 0f, 0f, null);
        }

        nextDashTime = Time.time + dashDefinition.Cooldown;
    }

    private void DealDamage(int damage, float staggerAmount, float hitstunDuration, string animatorTrigger)
    {
        if (animator != null && !string.IsNullOrEmpty(animatorTrigger))
        {
            animator.SetTrigger(animatorTrigger);
        }

        // Combo/heavy/dash damage is a base move value; gear (base damage +
        // every equipped item's rolled damage, see PlayerStats) adds on top.
        int totalDamage = damage + (playerStats != null ? playerStats.TotalDamage : 0);

        Collider[] hitEnemies = Physics.OverlapSphere(
            attackPoint.position,
            attackRange,
            enemyLayer
        );

        foreach (Collider enemyCollider in hitEnemies)
        {
            Health enemyHealth = enemyCollider.GetComponentInParent<Health>();

            if (enemyHealth == null || enemyHealth.IsDead)
            {
                continue;
            }

            Stagger enemyStagger = enemyCollider.GetComponentInParent<Stagger>();

            // Landing any hit on an already-broken enemy is a finisher — instant kill.
            if (enemyStagger != null && enemyStagger.IsBroken)
            {
                enemyHealth.Execute();
                continue;
            }

            enemyHealth.TakeDamage(totalDamage);

            if (enemyStagger != null)
            {
                enemyStagger.AddStagger(staggerAmount);
            }

            Hitstun enemyHitstun = enemyCollider.GetComponentInParent<Hitstun>();

            if (enemyHitstun != null)
            {
                enemyHitstun.ApplyStun(hitstunDuration);
            }
        }

        LootCorpses();
    }

    private void LootCorpses()
    {
        Collider[] hitCorpses = Physics.OverlapSphere(
            attackPoint.position,
            attackRange,
            corpseLayer
        );

        foreach (Collider corpseCollider in hitCorpses)
        {
            LootableCorpse corpse = corpseCollider.GetComponentInParent<LootableCorpse>();

            if (corpse != null)
            {
                corpse.TryLoot();
            }
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (attackPoint == null)
        {
            return;
        }

        Gizmos.DrawWireSphere(attackPoint.position, attackRange);
    }
}
