using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerCombat : MonoBehaviour
{
    [Header("Attack")]
    [SerializeField] private Transform attackPoint;
    [SerializeField] private float attackRange = 1.5f;
    [SerializeField] private int attackDamage = 25;
    [SerializeField] private float attackCooldown = 0.6f;
    [SerializeField] private LayerMask enemyLayer;

    [Header("References")]
    [SerializeField] private Animator animator;
    [SerializeField] private Health health;

    private InputSystem_Actions inputSystemActions;
    private float nextAttackTime;

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
    }

    private void OnEnable()
    {
        inputSystemActions.Player.Enable();
        inputSystemActions.Player.Attack.performed += OnAttackPerformed;
    }

    private void OnDisable()
    {
        inputSystemActions.Player.Attack.performed -= OnAttackPerformed;
        inputSystemActions.Player.Disable();
    }

    private void OnAttackPerformed(InputAction.CallbackContext context)
    {
        TryAttack();
    }

    private void TryAttack()
    {
        if (health != null && health.IsDead)
        {
            return;
        }

        if (Time.time < nextAttackTime)
        {
            return;
        }

        Attack();

        nextAttackTime = Time.time + attackCooldown;
    }

    private void Attack()
    {
        if (animator != null)
        {
            animator.SetTrigger("Attack");
        }

        Collider[] hitEnemies = Physics.OverlapSphere(
            attackPoint.position,
            attackRange,
            enemyLayer
        );

        foreach (Collider enemyCollider in hitEnemies)
        {
            Health enemyHealth = enemyCollider.GetComponentInParent<Health>();

            if (enemyHealth != null)
            {
                enemyHealth.TakeDamage(attackDamage);
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