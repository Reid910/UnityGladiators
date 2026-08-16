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
    [SerializeField]
    private ComboHit[] lightComboHits =
    {
        new ComboHit { damage = 15, staggerAmount = 12f, hitstunDuration = 0.2f, recoveryTime = 0.35f, animatorTrigger = "AttackCombo1" },
        new ComboHit { damage = 18, staggerAmount = 12f, hitstunDuration = 0.2f, recoveryTime = 0.35f, animatorTrigger = "AttackCombo2" },
        new ComboHit { damage = 28, staggerAmount = 18f, hitstunDuration = 0.25f, recoveryTime = 0.5f, animatorTrigger = "AttackCombo3" },
    };
    [SerializeField] private float comboWindow = 0.8f;

    [Header("Heavy Attack")]
    [SerializeField] private int heavyDamage = 40;
    [SerializeField] private float heavyStaggerAmount = 35f;
    [SerializeField] private float heavyHitstunDuration = 0.35f;
    [SerializeField] private float heavyRecoveryTime = 0.9f;

    [Header("Ability")]
    [SerializeField] private float abilityCooldown = 5f;

    [Header("Dash")]
    [SerializeField] private float dashDistance = 4f;
    [SerializeField] private float dashCooldown = 1.5f;

    [Header("Attack")]
    [SerializeField] private Transform attackPoint;
    [SerializeField] private float attackRange = 1.5f;
    [SerializeField] private LayerMask enemyLayer;

    [Header("References")]
    [SerializeField] private Animator animator;
    [SerializeField] private Health health;
    [SerializeField] private CharacterController characterController;
    [SerializeField] private Stagger stagger;
    [SerializeField] private Hitstun hitstun;

    private InputSystem_Actions inputSystemActions;

    private int comboStep;
    private float comboResetTime;
    private float nextAttackTime;
    private float nextAbilityTime;
    private float nextDashTime;

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
        nextAttackTime = Time.time + hit.recoveryTime;
        comboResetTime = nextAttackTime + comboWindow;
    }

    private void TryHeavyAttack()
    {
        if (IsIncapacitated || Time.time < nextAttackTime)
        {
            return;
        }

        DealDamage(heavyDamage, heavyStaggerAmount, heavyHitstunDuration, "AttackHeavy");

        // Heavy attack interrupts and resets the light combo chain.
        comboStep = 0;
        nextAttackTime = Time.time + heavyRecoveryTime;
        comboResetTime = nextAttackTime;
    }

    private void TryUseAbility()
    {
        if (IsIncapacitated || Time.time < nextAbilityTime)
        {
            return;
        }

        // Placeholder move. Once the item system (M2/M4) lands, this should
        // come from the equipped weapon's AbilityDefinition instead.
        if (animator != null)
        {
            animator.SetTrigger("AbilityCast");
        }

        nextAbilityTime = Time.time + abilityCooldown;
    }

    private void TryDash()
    {
        if (IsIncapacitated || Time.time < nextDashTime || characterController == null)
        {
            return;
        }

        // Placeholder: always available for now. Once boots gate this
        // (M2/M4), TryDash should return early when no boots are equipped.
        characterController.Move(transform.forward * dashDistance);

        if (animator != null)
        {
            animator.SetTrigger("Dash");
        }

        nextDashTime = Time.time + dashCooldown;
    }

    private void DealDamage(int damage, float staggerAmount, float hitstunDuration, string animatorTrigger)
    {
        if (animator != null && !string.IsNullOrEmpty(animatorTrigger))
        {
            animator.SetTrigger(animatorTrigger);
        }

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

            enemyHealth.TakeDamage(damage);

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

    private void OnDrawGizmosSelected()
    {
        if (attackPoint == null)
        {
            return;
        }

        Gizmos.DrawWireSphere(attackPoint.position, attackRange);
    }
}
