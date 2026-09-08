using System.Collections;
using System.Collections.Generic;
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
        [Tooltip("Telegraph delay before the hitbox becomes active — gives an opponent (or the player, when an enemy swings) a real window to react/dodge instead of an instant hit.")]
        public float windup;
        [Tooltip("How long after the windup the hitbox stays checkable. A target only needs to be in range at some point during this window, not at the exact instant the button was pressed.")]
        public float activeDuration;
        public float recoveryTime;
        public string animatorTrigger;
    }

    [Header("Light Combo")]
    // animatorTrigger reuses the existing "Attack" parameter (the only one the
    // current Animator Controllers actually have) rather than distinct
    // per-hit triggers — every light hit plays the same swing animation for
    // now. Give each hit its own trigger name here once real animations
    // exist and the Controllers have matching states/transitions.
    // windup+activeDuration+recoveryTime sums match the original single
    // recoveryTime values, so overall combo pacing is unchanged — this just
    // carves out an explicit telegraph + hit window instead of an instant hit.
    [SerializeField]
    private ComboHit[] lightComboHits =
    {
        new ComboHit { damage = 15, staggerAmount = 12f, hitstunDuration = 0.2f, windup = 0.08f, activeDuration = 0.08f, recoveryTime = 0.19f, animatorTrigger = "Attack" },
        new ComboHit { damage = 18, staggerAmount = 12f, hitstunDuration = 0.2f, windup = 0.08f, activeDuration = 0.08f, recoveryTime = 0.19f, animatorTrigger = "Attack" },
        new ComboHit { damage = 28, staggerAmount = 18f, hitstunDuration = 0.25f, windup = 0.12f, activeDuration = 0.1f, recoveryTime = 0.28f, animatorTrigger = "Attack" },
    };
    [SerializeField] private float comboWindow = 0.8f;

    [Header("Heavy Attack")]
    [SerializeField] private int heavyDamage = 40;
    [SerializeField] private float heavyStaggerAmount = 35f;
    [SerializeField] private float heavyHitstunDuration = 0.35f;
    [SerializeField] private float heavyWindup = 0.35f;
    [SerializeField] private float heavyActiveDuration = 0.15f;
    [SerializeField] private float heavyRecoveryTime = 0.4f;

    [Header("Attack")]
    [SerializeField] private Transform attackPoint;
    [SerializeField] private float attackRange = 1.5f;
    [SerializeField] private LayerMask enemyLayer;
    [Tooltip("Damage multiplier applied on a crit (see Crit Chance affix, StatType.CritChance).")]
    [SerializeField] private float critDamageMultiplier = 1.5f;
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
    private Coroutine attackCoroutine;

    private int comboStep;
    private float comboResetTime;
    private float nextAttackTime;
    private float nextAbilityTime;
    private float nextDashTime;

    public int ComboStep => comboStep;
    public float AbilityCooldownRemaining => Mathf.Max(0f, nextAbilityTime - Time.time);
    public float DashCooldownRemaining => Mathf.Max(0f, nextDashTime - Time.time);

    // Poise/hyperarmor window: true from the moment an attack starts (windup)
    // until its full recovery ends — the same window nextAttackTime already
    // gates. EnemyController checks this to skip applying Hitstun while true;
    // damage/Stagger still land normally, so this only stops a routine hit
    // from flinching the player out of a swing they've already committed to.
    public bool IsAttacking => Time.time < nextAttackTime;

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
        inputSystemActions.Player.Interact.performed += OnInteractPerformed;
    }

    private void OnDisable()
    {
        inputSystemActions.Player.Attack.performed -= OnAttackPerformed;
        inputSystemActions.Player.Heavy.performed -= OnHeavyPerformed;
        inputSystemActions.Player.Ability.performed -= OnAbilityPerformed;
        inputSystemActions.Player.Dash.performed -= OnDashPerformed;
        inputSystemActions.Player.Interact.performed -= OnInteractPerformed;
        inputSystemActions.Player.Disable();
    }

    private void OnAttackPerformed(InputAction.CallbackContext context) => TryLightAttack();

    private void OnHeavyPerformed(InputAction.CallbackContext context) => TryHeavyAttack();

    private void OnAbilityPerformed(InputAction.CallbackContext context) => TryUseAbility();

    private void OnDashPerformed(InputAction.CallbackContext context) => TryDash();

    // No inventory: swaps whatever's in the nearby ItemPickup's slot with
    // the player's currently equipped item there (see
    // PlayerEquipment.TrySwapWithNearby). No-ops if nothing's in range.
    private void OnInteractPerformed(InputAction.CallbackContext context) => equipment?.TrySwapWithNearby();

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
        comboStep++;

        BeginAttack(hit.damage, hit.staggerAmount, hit.hitstunDuration, hit.animatorTrigger, hit.windup, hit.activeDuration, hit.recoveryTime);

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
        BeginAttack(heavyDamage, heavyStaggerAmount, heavyHitstunDuration, "Attack", heavyWindup, heavyActiveDuration, heavyRecoveryTime);

        // Heavy attack interrupts and resets the light combo chain.
        comboStep = 0;
        comboResetTime = nextAttackTime;
    }

    // Attack Speed affix (Head-flavored, see TODO.md) shortens recovery time.
    private float ApplyAttackSpeed(float duration)
    {
        float attackSpeedMultiplier = 1f + (playerStats != null ? playerStats.GetStat(StatType.AttackSpeed) : 0f);
        return duration / Mathf.Max(0.1f, attackSpeedMultiplier);
    }

    private void BeginAttack(int damage, float staggerAmount, float hitstunDuration, string animatorTrigger, float windup, float activeDuration, float recoveryTime)
    {
        float scaledWindup = ApplyAttackSpeed(windup);
        float scaledActiveDuration = ApplyAttackSpeed(activeDuration);
        float scaledRecoveryTime = ApplyAttackSpeed(recoveryTime);

        nextAttackTime = Time.time + scaledWindup + scaledActiveDuration + scaledRecoveryTime;

        if (attackCoroutine != null)
        {
            StopCoroutine(attackCoroutine);
        }

        attackCoroutine = StartCoroutine(PerformAttack(damage, staggerAmount, hitstunDuration, animatorTrigger, scaledWindup, scaledActiveDuration));
    }

    private IEnumerator PerformAttack(int damage, float staggerAmount, float hitstunDuration, string animatorTrigger, float windup, float activeDuration)
    {
        if (animator != null && !string.IsNullOrEmpty(animatorTrigger))
        {
            animator.SetTrigger(animatorTrigger);
        }

        if (windup > 0f)
        {
            yield return new WaitForSeconds(windup);
        }

        // Getting broken (not just hitstunned, poise covers that) mid-windup
        // cancels the hit — a fully-interrupted swing shouldn't still land.
        if (IsIncapacitated)
        {
            yield break;
        }

        int totalDamage = RollDamage(damage);

        LootCorpses();

        HashSet<Health> hitTargets = new HashSet<Health>();
        float activeEndTime = Time.time + Mathf.Max(activeDuration, Time.deltaTime);

        do
        {
            CheckHit(totalDamage, staggerAmount, hitstunDuration, hitTargets);
            yield return null;
        }
        while (Time.time < activeEndTime);
    }

    // Crit Chance affix: one roll per swing, not per enemy hit, so every
    // enemy caught in a single swing (or a swing's whole active window)
    // shares the same crit result.
    private int RollDamage(int baseDamage)
    {
        int totalDamage = baseDamage + (playerStats != null ? playerStats.TotalDamage : 0);
        float critChance = playerStats != null ? playerStats.GetStat(StatType.CritChance) : 0f;

        if (Random.value < critChance)
        {
            totalDamage = Mathf.RoundToInt(totalDamage * critDamageMultiplier);
        }

        return totalDamage;
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
            // Dash's own movement is the telegraph, so this hits instantly
            // rather than going through the windup/active-window pipeline —
            // no "Dash" animator trigger exists yet either (see TryUseAbility).
            int totalDamage = RollDamage(dashDefinition.Damage);
            CheckHit(totalDamage, 0f, 0f, new HashSet<Health>());
            LootCorpses();
        }

        nextDashTime = Time.time + dashDefinition.Cooldown;
    }

    private void CheckHit(int totalDamage, float staggerAmount, float hitstunDuration, HashSet<Health> alreadyHit)
    {
        Collider[] hitEnemies = Physics.OverlapSphere(
            attackPoint.position,
            attackRange,
            enemyLayer
        );

        foreach (Collider enemyCollider in hitEnemies)
        {
            Health enemyHealth = enemyCollider.GetComponentInParent<Health>();

            if (enemyHealth == null || enemyHealth.IsDead || alreadyHit.Contains(enemyHealth))
            {
                continue;
            }

            alreadyHit.Add(enemyHealth);

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
