using System;
using TMPro;
using UnityEngine;

public class Health : MonoBehaviour
{
    public event Action<Health> Died;

    [Header("Health")]
    [SerializeField] private int maxHealth = 100;

    [Header("UI")]
    [SerializeField] private TextMeshPro healthText;

    [Header("Death")]
    [SerializeField] private bool destroyOnDeath = false;
    [SerializeField] private float destroyDelay = 2.5f;
    [SerializeField] private bool disableObjectOnDeath = false;

    [Header("References")]
    [SerializeField] private Animator animator;
    [SerializeField] private CharacterController characterController;
    [SerializeField] private Collider objectCollider;
    [Tooltip("Optional. Enabled on death so a corpse can still be hit for looting (see LootableCorpse) even though objectCollider gets disabled.")]
    [SerializeField] private Collider corpseHitbox;

    private int maxHealthBonus;

    public int CurrentHealth { get; private set; }
    public int MaxHealth => maxHealth + maxHealthBonus;
    public bool IsDead => CurrentHealth <= 0;

    // Called by PlayerStats when equipped gear's MaxHealth affix total changes.
    // Preserves the player's current health rather than clamping it down/up
    // arbitrarily: gaining max HP heals through by the delta, losing max HP
    // only clamps current health down if it would now exceed the new max.
    public void SetMaxHealthBonus(int bonus)
    {
        int previousMax = MaxHealth;
        maxHealthBonus = bonus;
        int delta = MaxHealth - previousMax;

        if (delta > 0)
        {
            CurrentHealth += delta;
        }
        else if (CurrentHealth > MaxHealth)
        {
            CurrentHealth = MaxHealth;
        }

        UpdateHealthText();
    }

    private void Awake()
    {
        CurrentHealth = maxHealth;

        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }

        if (characterController == null)
        {
            characterController = GetComponent<CharacterController>();
        }

        if (objectCollider == null)
        {
            objectCollider = GetComponent<Collider>();
        }

        UpdateHealthText();
    }

    public void TakeDamage(int damageAmount)
    {
        if (IsDead)
        {
            return;
        }

        CurrentHealth -= damageAmount;
        CurrentHealth = Mathf.Max(CurrentHealth, 0);

        UpdateHealthText();

        if (animator != null && !IsDead)
        {
            animator.SetTrigger("Hit");
        }

        if (IsDead)
        {
            Die();
        }
    }

    // Instant kill regardless of remaining health — used for finisher hits
    // landed while the target is broken (see Stagger.IsBroken).
    public void Execute()
    {
        if (IsDead)
        {
            return;
        }

        CurrentHealth = 0;
        UpdateHealthText();
        Die();
    }

    private void UpdateHealthText()
    {
        if (healthText == null)
        {
            return;
        }

        healthText.text = CurrentHealth + " / " + MaxHealth;
    }

    private void Die()
    {
        Died?.Invoke(this);

        if (animator != null)
        {
            animator.SetBool("IsDead", true);
            animator.SetTrigger("Death");
        }

        PlayerController playerController = GetComponent<PlayerController>();

        if (playerController != null)
        {
            playerController.enabled = false;
        }

        PlayerCombat playerCombat = GetComponent<PlayerCombat>();

        if (playerCombat != null)
        {
            playerCombat.enabled = false;
        }

        EnemyController enemyController = GetComponent<EnemyController>();

        if (enemyController != null)
        {
            enemyController.enabled = false;
        }

        if (characterController != null)
        {
            characterController.enabled = false;
        }

        if (objectCollider != null)
        {
            objectCollider.enabled = false;
        }

        if (corpseHitbox != null)
        {
            corpseHitbox.enabled = true;
        }

        if (destroyOnDeath)
        {
            Destroy(gameObject, destroyDelay);
        }

        if (disableObjectOnDeath)
        {
            StartCoroutine(DisableAfterDelay());
        }
    }

    private System.Collections.IEnumerator DisableAfterDelay()
    {
        yield return new WaitForSeconds(destroyDelay);
        gameObject.SetActive(false);
    }
}