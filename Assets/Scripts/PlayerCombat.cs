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
        public float hitstunDuration;
        [Tooltip("Telegraph delay before the hitbox becomes active — gives an opponent (or the player, when an enemy swings) a real window to react/dodge instead of an instant hit.")]
        public float windup;
        [Tooltip("How long after the windup the hitbox stays checkable. A target only needs to be in range at some point during this window, not at the exact instant the button was pressed.")]
        public float activeDuration;
        public float recoveryTime;
        public string animatorTrigger;
    }

    [Header("Light Combo")]
    // animatorTrigger alternates PunchLeft/PunchRight (AttackComboLeft/Right
    // in the Animator Controllers, filler animations from the Blink pack
    // already in the project) so each hit visually reads as distinct instead
    // of every hit sharing the same swing. Heavy still reuses the existing
    // "Attack" state (already wired to MeleeAttack_OneHanded), which is a
    // deliberately bigger/different motion than either punch.
    // windup+activeDuration+recoveryTime sums match the original single
    // recoveryTime values, so overall combo pacing is unchanged — this just
    // carves out an explicit telegraph + hit window instead of an instant hit.
    // No stagger value per hit anymore — Stagger.AddStaggerFromDamage derives
    // it from damage dealt vs. the target's own max health.
    [SerializeField]
    private ComboHit[] lightComboHits =
    {
        new ComboHit { damage = 15, hitstunDuration = 0.2f, windup = 0.08f, activeDuration = 0.08f, recoveryTime = 0.19f, animatorTrigger = "AttackComboLeft" },
        new ComboHit { damage = 18, hitstunDuration = 0.2f, windup = 0.08f, activeDuration = 0.08f, recoveryTime = 0.19f, animatorTrigger = "AttackComboRight" },
        new ComboHit { damage = 28, hitstunDuration = 0.25f, windup = 0.12f, activeDuration = 0.1f, recoveryTime = 0.28f, animatorTrigger = "AttackComboLeft" },
    };
    [SerializeField] private float comboWindow = 0.8f;

    [Header("Heavy Attack")]
    [SerializeField] private int heavyDamage = 40;
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
    private float invulnerableUntilTime;

    public int ComboStep => comboStep;
    public float AbilityCooldownRemaining => Mathf.Max(0f, nextAbilityTime - Time.time);
    public float DashCooldownRemaining => Mathf.Max(0f, nextDashTime - Time.time);

    // Hyper armor window: true from the moment an attack starts (windup)
    // until its full recovery ends — the same window nextAttackTime already
    // gates. EnemyController checks this to skip applying Hitstun while true;
    // damage/Stagger still land normally, so this only stops a routine hit
    // from flinching the player out of a swing they've already committed to.
    public bool IsAttacking => Time.time < nextAttackTime;

    // True i-frames from dashing (see DashDefinition.InvulnerabilityDuration)
    // — unlike hyper armor, this blocks damage/stagger/finishers entirely,
    // not just hitstun. EnemyController checks this before resolving a hit
    // at all.
    public bool IsInvulnerable => Time.time < invulnerableUntilTime;

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

        BeginAttack(hit.damage, hit.hitstunDuration, hit.animatorTrigger, hit.windup, hit.activeDuration, hit.recoveryTime);

        comboResetTime = nextAttackTime + comboWindow;
    }

    private void TryHeavyAttack()
    {
        if (IsIncapacitated || Time.time < nextAttackTime)
        {
            return;
        }

        // Uses the pre-existing "Attack" state (MeleeAttack_OneHanded) — a
        // bigger, different motion from either combo punch, so heavy already
        // reads as distinct without needing a new state.
        BeginAttack(heavyDamage, heavyHitstunDuration, "Attack", heavyWindup, heavyActiveDuration, heavyRecoveryTime);

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

    private void BeginAttack(int damage, float hitstunDuration, string animatorTrigger, float windup, float activeDuration, float recoveryTime, float range = -1f)
    {
        float scaledWindup = ApplyAttackSpeed(windup);
        float scaledActiveDuration = ApplyAttackSpeed(activeDuration);
        float scaledRecoveryTime = ApplyAttackSpeed(recoveryTime);
        float hitRange = range > 0f ? range : attackRange;

        nextAttackTime = Time.time + scaledWindup + scaledActiveDuration + scaledRecoveryTime;

        if (attackCoroutine != null)
        {
            StopCoroutine(attackCoroutine);
        }

        attackCoroutine = StartCoroutine(PerformAttack(damage, hitstunDuration, animatorTrigger, scaledWindup, scaledActiveDuration, hitRange));
    }

    private IEnumerator PerformAttack(int damage, float hitstunDuration, string animatorTrigger, float windup, float activeDuration, float range)
    {
        if (animator != null && !string.IsNullOrEmpty(animatorTrigger))
        {
            animator.SetTrigger(animatorTrigger);
        }

        if (windup > 0f)
        {
            yield return new WaitForSeconds(windup);
        }

        // Getting broken (not just hitstunned — hyper armor covers that)
        // mid-windup cancels the hit — a fully-interrupted swing shouldn't
        // still land.
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
            CheckHit(totalDamage, hitstunDuration, hitTargets, range);
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
        // Deliberately not gated by IsIncapacitated/IsAttacking or nextAttackTime
        // the way light/heavy are — the ability has its own independent
        // cooldown (nextAbilityTime) and can be weaved between combo hits.
        // It's still blocked while dead/stunned/broken via IsIncapacitated below.
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
        nextAbilityTime = Time.time + effectiveCooldown;

        // An ability is a bigger, rarer hit than a normal swing — same
        // windup/active-window pipeline as combo/heavy, just with its own
        // damage/range from the weapon's AbilityDefinition. Doesn't touch
        // nextAttackTime/comboStep, so it doesn't interrupt or reset the
        // light combo chain. animatorTrigger ("AbilityCast" by default) now
        // maps to a real state (SpellCast, filler from the Blink pack) —
        // deliberately a different-looking motion from the punch/melee combo
        // states so an ability reads as clearly distinct from a normal attack.
        StartCoroutine(PerformAttack(
            abilityDefinition.Damage,
            abilityDefinition.HitstunDuration,
            abilityDefinition.AnimatorTrigger,
            ApplyAttackSpeed(abilityDefinition.Windup),
            ApplyAttackSpeed(abilityDefinition.ActiveDuration),
            abilityDefinition.Range));
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
        invulnerableUntilTime = Time.time + dashDefinition.InvulnerabilityDuration;

        if (dashDefinition.DealsDamage)
        {
            // Dash's own movement is the telegraph, so this hits instantly
            // rather than going through the windup/active-window pipeline —
            // no "Dash" animator trigger exists yet either (see TryUseAbility).
            int totalDamage = RollDamage(dashDefinition.Damage);
            CheckHit(totalDamage, 0f, new HashSet<Health>(), attackRange);
            LootCorpses();
        }

        nextDashTime = Time.time + dashDefinition.Cooldown;
    }

    private void CheckHit(int totalDamage, float hitstunDuration, HashSet<Health> alreadyHit, float range)
    {
        Collider[] hitEnemies = Physics.OverlapSphere(
            attackPoint.position,
            range,
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
                enemyStagger.AddStaggerFromDamage(totalDamage, enemyHealth.MaxHealth);
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
