using UnityEngine;

public class EnemyController : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float movementSpeed = 2.5f;
    [SerializeField] private float rotationSpeed = 10f;
    [SerializeField] private float stoppingDistance = 1.6f;
    [SerializeField] private float gravity = -20f;

    [Header("Combat")]
    [SerializeField] private int attackDamage = 10;
    [SerializeField] private float attackCooldown = 1.25f;

    [Header("References")]
    [SerializeField] private Transform target;
    [SerializeField] private Animator animator;

    private CharacterController characterController;
    private Vector3 verticalVelocity;
    private float nextAttackTime;

    private void Awake()
    {
        characterController = GetComponent<CharacterController>();

        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }

        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");

        if (playerObject != null)
        {
            target = playerObject.transform;
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

        Health enemyHealth = GetComponent<Health>();

        if (enemyHealth != null && enemyHealth.IsDead)
        {
            SetMoving(false);
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

        Health targetHealth = target.GetComponent<Health>();

        if (targetHealth != null)
        {
            targetHealth.TakeDamage(attackDamage);
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