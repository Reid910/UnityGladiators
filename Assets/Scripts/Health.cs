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

    [Header("Regen")]
    [Tooltip("Passive health regen per second. 0 disables it entirely — leave at 0 on Enemy (only the Player prefab should set this).")]
    [SerializeField] private float regenPerSecond = 0f;
    [Tooltip("Seconds since the last hit taken before regen starts — an out-of-combat window rather than healing through an ongoing beating. 0 means regen is always active.")]
    [SerializeField] private float regenDelayAfterHit = 3f;

    [Header("References")]
    [SerializeField] private Animator animator;
    [SerializeField] private CharacterController characterController;
    [SerializeField] private Collider objectCollider;
    [Tooltip("Optional. Enabled on death so a corpse can still be hit for looting (see LootableCorpse) even though objectCollider gets disabled.")]
    [SerializeField] private Collider corpseHitbox;

    [Header("Combat Feedback")]
    [Tooltip("Optional. Spawned above this object on every hit that lands (see DamageNumber.cs). Leave unset to skip.")]
    [SerializeField] private DamageNumber damageNumberPrefab;
    [SerializeField] private Vector3 damageNumberSpawnOffset = new Vector3(0f, 2f, 0f);
    [SerializeField] private Color damageNumberColor = Color.white;
    [Tooltip("Brief global freeze-frame applied on every hit that lands (see HitStop.cs). 0 disables it.")]
    [SerializeField] private float hitStopDuration = 0.05f;

    private int maxHealthBonus;
    private float armor;
    private float lastHitTime = float.NegativeInfinity;
    private float regenRemainder;

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

    // Called by PlayerStats when equipped gear's Armor affix total changes.
    // Armor affix values are flat points (see GameUI.FormatAffix), so it's a
    // flat reduction here too, not a percentage.
    public void SetArmor(float armorValue)
    {
        armor = armorValue;
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

    private void Update()
    {
        if (IsDead || regenPerSecond <= 0f || CurrentHealth >= MaxHealth)
        {
            return;
        }

        if (Time.time - lastHitTime < regenDelayAfterHit)
        {
            return;
        }

        // Accumulate fractional regen in a remainder rather than rounding
        // every frame, so slow regen rates (e.g. 2/sec) don't get rounded
        // away to zero at high framerate or drift high at low framerate.
        regenRemainder += regenPerSecond * Time.deltaTime;
        int wholeRegen = Mathf.FloorToInt(regenRemainder);

        if (wholeRegen <= 0)
        {
            return;
        }

        regenRemainder -= wholeRegen;
        CurrentHealth = Mathf.Min(MaxHealth, CurrentHealth + wholeRegen);
        UpdateHealthText();
    }

    public void TakeDamage(int damageAmount)
    {
        if (IsDead)
        {
            return;
        }

        lastHitTime = Time.time;

        // Armor reduces incoming damage by a flat amount but never below 1,
        // so a heavily-armored player can't become fully unkillable.
        int mitigatedDamage = Mathf.Max(1, damageAmount - Mathf.RoundToInt(armor));

        CurrentHealth -= mitigatedDamage;
        CurrentHealth = Mathf.Max(CurrentHealth, 0);

        UpdateHealthText();
        SpawnDamageNumber(mitigatedDamage);
        HitStop.Trigger(hitStopDuration);

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

        // Show whatever health remained as the "damage" dealt, and hold the
        // freeze a beat longer than a normal hit — a finisher should read as
        // more impactful than a regular combo tick.
        SpawnDamageNumber(CurrentHealth);
        HitStop.Trigger(hitStopDuration * 2f);

        CurrentHealth = 0;
        UpdateHealthText();
        Die();
    }

    // Called by WaveManager once the wave this enemy died in fully clears —
    // corpses aren't lootable before then, so the player can't farm loot off
    // a body while more enemies from the same wave are still incoming.
    public void EnableCorpseHitbox()
    {
        if (corpseHitbox != null)
        {
            corpseHitbox.enabled = true;
        }
    }

    private void SpawnDamageNumber(int amount)
    {
        if (damageNumberPrefab == null || amount <= 0)
        {
            return;
        }

        DamageNumber instance = Instantiate(damageNumberPrefab, transform.position + damageNumberSpawnOffset, Quaternion.identity);
        instance.Initialize(amount, damageNumberColor);
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

        // Stops driving the "Broken" animator bool after death — otherwise a
        // corpse that died while staggered keeps ticking Stagger.Update(),
        // which re-fires the Any State -> StunnedLoop transition right after
        // Death plays and, once the broken window ends, transitions back out
        // to idle, leaving the corpse standing instead of in its death pose.
        // The Animator Controllers also guard this directly (Broken's Any
        // State transition now requires IsDead == false), so this is a
        // second layer, not the only fix.
        Stagger stagger = GetComponent<Stagger>();

        if (stagger != null)
        {
            stagger.enabled = false;
        }

        if (characterController != null)
        {
            characterController.enabled = false;
        }

        if (objectCollider != null)
        {
            objectCollider.enabled = false;
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