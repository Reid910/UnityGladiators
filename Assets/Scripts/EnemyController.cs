using UnityEngine;

public class EnemyController : MonoBehaviour
{
    [SerializeField] private Transform target;
    [SerializeField] private float movementSpeed = 3f;
    [SerializeField] private float stoppingDistance = 1.5f;
    [SerializeField] private int damage = 10;
    [SerializeField] private float attackCooldown = 1.2f;

    private CharacterController characterController;
    private float nextAttackTime;

    private void Awake()
    {
        characterController = GetComponent<CharacterController>();

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
            return;
        }

        float distanceToTarget = Vector3.Distance(transform.position, target.position);

        if (distanceToTarget > stoppingDistance)
        {
            MoveTowardTarget();
        }
        else
        {
            AttackTarget();
        }
    }

    private void MoveTowardTarget()
    {
        Vector3 direction = target.position - transform.position;
        direction.y = 0f;
        direction.Normalize();

        transform.rotation = Quaternion.LookRotation(direction);
        characterController.Move(direction * movementSpeed * Time.deltaTime);
    }

    private void AttackTarget()
    {
        if (Time.time < nextAttackTime)
        {
            return;
        }

        Health targetHealth = target.GetComponent<Health>();

        if (targetHealth != null)
        {
            targetHealth.TakeDamage(damage);
        }

        nextAttackTime = Time.time + attackCooldown;
    }
}