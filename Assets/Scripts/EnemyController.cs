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
    [SerializeField] private float attackStaggerAmount = 15f;
    [SerializeField] private float attackHitstunDuration = 0.2f;
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

    private Vector3 verticalVelocity;
    private float nextAttackTime;

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

        if (IsIncapacitated)
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

        if (animator != null)
        {
            animator.SetTrigger("Attack");
        }

        if (targetHealth != null && !targetHealth.IsDead)
        {
            // Attacking an already-broken target is a finisher — instant kill.
            if (targetStagger != null && targetStagger.IsBroken)
            {
                targetHealth.Execute();
            }
            else
            {
                targetHealth.TakeDamage(attackDamage);

                if (targetStagger != null)
                {
                    targetStagger.AddStagger(attackStaggerAmount);
                }

                if (targetHitstun != null)
                {
                    targetHitstun.ApplyStun(attackHitstunDuration);
                }
            }
        }

        nextAttackTime = Time.time + attackCooldown;
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
