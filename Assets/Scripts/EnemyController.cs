using System.Collections;
using UnityEngine;

public class EnemyController : MonoBehaviour
{
    [Header("Tier")]
    [Tooltip("Drives loot rarity (see LootableCorpse, which reads this) and is a label for tuning this prefab's own stats — it doesn't auto-scale anything itself. Fast/low-hp = T1, slow/high-damage = T2, ranged/tankier = T3 is the suggested split.")]
    [SerializeField] private EnemyTier tier = EnemyTier.T1;

    [Header("Movement")]
    [SerializeField] private float movementSpeed = 2.5f;
    [SerializeField] private float rotationSpeed = 10f;
    [SerializeField] private float stoppingDistance = 1.6f;
    [SerializeField] private float gravity = -20f;

    [Header("Combat")]
    [SerializeField] private int attackDamage = 10;
    [SerializeField] private float attackHitstunDuration = 0.2f;
    [Tooltip("Telegraph delay before the hit registers — gives the player a real window to see the tell and dodge/reposition before impact, instead of an instant hit the moment the enemy is in range.")]
    [SerializeField] private float attackWindup = 0.4f;
    [Tooltip("How long after the windup the hit stays checkable. The target must still be within Attack Range at some point during this window, not just when the windup started — dodging away during the windup avoids it.")]
    [SerializeField] private float attackActiveDuration = 0.15f;
    [Tooltip("Distance the target must be within when the active window checks — separate from Stopping Distance, which only decides when the enemy stops closing in to swing.")]
    [SerializeField] private float attackRange = 1.8f;
    [SerializeField] private float attackCooldown = 1.25f;

    [Header("References")]
    [SerializeField] private Transform target;
    [SerializeField] private Animator animator;

    private CharacterController characterController;
    private Health health;
    private Stagger stagger;
    private Hitstun hitstun;

    private Health targetHealth;
    private Stagger targetStagger;
    private Hitstun targetHitstun;
    private PlayerCombat targetCombat;

    private Vector3 verticalVelocity;
    private float nextAttackTime;
    private bool isAttacking;

    public EnemyTier Tier => tier;

    // Enemies can't move or attack while stunned from a hit or broken from
    // stagger — mirrors the same restriction PlayerCombat applies to the player.
    private bool IsIncapacitated =>
        (health != null && health.IsDead) ||
        (hitstun != null && hitstun.IsStunned) ||
        (stagger != null && stagger.IsBroken);

    private void Awake()
    {
        characterController = GetComponent<CharacterController>();
        health = GetComponent<Health>();
        stagger = GetComponent<Stagger>();
        hitstun = GetComponent<Hitstun>();

        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }

        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");

        if (playerObject != null)
        {
            target = playerObject.transform;
            targetHealth = playerObject.GetComponent<Health>();
            targetStagger = playerObject.GetComponent<Stagger>();
            targetHitstun = playerObject.GetComponent<Hitstun>();
            targetCombat = playerObject.GetComponent<PlayerCombat>();
        }
    }

    private void Update()
    {
        if (target == null)
        {
            SetMoving(false);
            ApplyGravity();
            return;
        }

        // isAttacking freezes the enemy in place for the whole windup/active
        // sequence — it's committed once it starts swinging, same as the
        // player isn't free to cancel their own combo mid-hit.
        if (IsIncapacitated || isAttacking)
        {
            SetMoving(false);
            ApplyGravity();
            return;
        }

        float distanceToTarget = Vector3.Distance(transform.position, target.position);

        if (distanceToTarget > stoppingDistance)
        {
            MoveTowardTarget();
            SetMoving(true);
        }
        else
        {
            SetMoving(false);
            AttackTarget();
        }

        ApplyGravity();
    }

    private void MoveTowardTarget()
    {
        Vector3 directionToTarget = target.position - transform.position;
        directionToTarget.y = 0f;

        if (directionToTarget.sqrMagnitude <= 0.01f)
        {
            return;
        }

        directionToTarget.Normalize();

        Quaternion targetRotation = Quaternion.LookRotation(directionToTarget);

        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            targetRotation,
            rotationSpeed * Time.deltaTime
        );

        characterController.Move(directionToTarget * movementSpeed * Time.deltaTime);
    }

    private void AttackTarget()
    {
        if (Time.time < nextAttackTime)
        {
            return;
        }

        nextAttackTime = Time.time + attackWindup + attackActiveDuration + attackCooldown;
        StartCoroutine(PerformAttack());
    }

    private IEnumerator PerformAttack()
    {
        isAttacking = true;

        if (animator != null)
        {
            animator.SetTrigger("Attack");
        }

        if (attackWindup > 0f)
        {
            yield return new WaitForSeconds(attackWindup);
        }

        // Getting broken mid-windup cancels the swing, same rule as the player's.
        if (!IsIncapacitated)
        {
            bool hasHit = false;
            float activeEndTime = Time.time + Mathf.Max(attackActiveDuration, Time.deltaTime);

            do
            {
                if (!hasHit && IsTargetInRange())
                {
                    ResolveHit();
                    hasHit = true;
                }

                yield return null;
            }
            while (Time.time < activeEndTime);
        }

        isAttacking = false;
    }

    private bool IsTargetInRange()
    {
        return target != null && Vector3.Distance(transform.position, target.position) <= attackRange;
    }

    private void ResolveHit()
    {
        if (targetHealth == null || targetHealth.IsDead)
        {
            return;
        }

        // True i-frames from a dash (DashDefinition.InvulnerabilityDuration):
        // a correctly-timed dodge takes nothing at all, not even a finisher
        // on an already-broken player — unlike hyper armor, which only
        // blocks hitstun and still lets damage/Stagger land.
        if (targetCombat != null && targetCombat.IsInvulnerable)
        {
            return;
        }

        // Attacking an already-broken target is a finisher — instant kill.
        if (targetStagger != null && targetStagger.IsBroken)
        {
            targetHealth.Execute();
            return;
        }

        targetHealth.TakeDamage(attackDamage);

        if (targetStagger != null)
        {
            targetStagger.AddStaggerFromDamage(attackDamage, targetHealth.MaxHealth);
        }

        // Hyper armor: a player mid-swing isn't flinched by a routine hit —
        // damage and Stagger still land normally, so reckless aggression can
        // still get them broken and finished, it just can't be
        // chain-interrupted by every graze. See PlayerCombat.IsAttacking.
        bool targetHasHyperArmor = targetCombat != null && targetCombat.IsAttacking;

        if (targetHitstun != null && !targetHasHyperArmor)
        {
            targetHitstun.ApplyStun(attackHitstunDuration);
        }
    }

    private void ApplyGravity()
    {
        if (characterController == null || !characterController.enabled)
        {
            return;
        }

        if (characterController.isGrounded && verticalVelocity.y < 0f)
        {
            verticalVelocity.y = -2f;
        }

        verticalVelocity.y += gravity * Time.deltaTime;
        characterController.Move(verticalVelocity * Time.deltaTime);
    }

    private void SetMoving(bool isMoving)
    {
        if (animator == null)
        {
            return;
        }

        animator.SetBool("IsMoving", isMoving);
    }
}
