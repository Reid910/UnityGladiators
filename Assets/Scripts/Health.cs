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
    [Tooltip("Passive health regen per second, always active (same model as Stagger's constant decay) — not gated by time since the last hit. 0 disables it entirely — leave at 0 on Enemy (only the Player prefab should set this).")]
    [SerializeField] private float regenPerSecond = 0f;

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
    [Tooltip("SFX — assign clips once you have them (see AudioManager).")]
    [SerializeField] private AudioClip hitClip;
    [SerializeField] private AudioClip deathClip;

    private int maxHealthBonus;
    private int levelMaxHealthBonus;
    private float healthScaleMultiplier = 1f;
    private float armor;
    private float damageMitigation;
    private float regenRemainder;

    public int CurrentHealth { get; private set; }
    public int MaxHealth => Mathf.RoundToInt((maxHealth + maxHealthBonus + levelMaxHealthBonus) * healthScaleMultiplier);
    public bool IsDead => CurrentHealth <= 0;

    // Called once by WaveManager right after spawning an enemy, before
    // anything else touches its health — scales Max Health for the wave it
    // spawned in (see WaveManager.enemyHealthScalingPerWave), on top of
    // whatever gear/level bonuses apply. A fresh spawn has no existing
    // health to preserve through a delta like SetMaxHealthBonus/
    // SetLevelMaxHealthBonus do for an already-live player, so this just
    // sets CurrentHealth to the new (scaled) max outright.
    public void SetHealthScaleMultiplier(float multiplier)
    {
        healthScaleMultiplier = multiplier;
        CurrentHealth = MaxHealth;
    }

    // Called by PlayerStats when equipped gear's MaxHealth affix total changes.
    // Preserves the player's current health rather than clamping it down/up
    // arbitrarily: gaining max HP heals through by the delta, losing max HP
    // only clamps current health down if it would now exceed the new max.
    public void SetMaxHealthBonus(int bonus)
    {
        ApplyMaxHealthDelta(bonus - maxHealthBonus);
        maxHealthBonus = bonus;
    }

    // Called by PlayerLevel — separate from the gear-driven bonus above so
    // the two add together instead of overwriting each other. See
    // docs/combat-redesign-plan.md.
    public void SetLevelMaxHealthBonus(int bonus)
    {
        ApplyMaxHealthDelta(bonus - levelMaxHealthBonus);
        levelMaxHealthBonus = bonus;
    }

    private void ApplyMaxHealthDelta(int delta)
    {
        if (delta > 0)
        {
            CurrentHealth += delta;
        }
        else if (CurrentHealth > MaxHealth + delta)
        {
            CurrentHealth = MaxHealth + delta;
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

    // Called by PlayerStats from a Chest passive effect (DamageMitigation) —
    // a fraction (0.1 = 10%) reduction applied alongside Armor in TakeDamage.
    // See docs/combat-redesign-plan.md.
    public void SetDamageMitigation(float fraction)
    {
        damageMitigation = fraction;
    }

    // Called by a Head passive effect (Lifesteal, see PlayerCombat.CheckHit)
    // or anything else that wants to restore health outside of Regen.
    public void Heal(int amount)
    {
        if (IsDead || amount <= 0)
        {
            return;
        }

        CurrentHealth = Mathf.Min(MaxHealth, CurrentHealth + amount);
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

    private void Update()
    {
        if (IsDead || regenPerSecond <= 0f || CurrentHealth >= MaxHealth)
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

        // Armor reduces incoming damage by a flat amount, DamageMitigation
        // (a Chest passive effect, see docs/combat-redesign-plan.md) by a
        // fraction on top of that — never below 1, so neither can make the
        // target fully unkillable.
        float afterArmor = damageAmount - armor;
        int mitigatedDamage = Mathf.Max(1, Mathf.RoundToInt(afterArmor * (1f - damageMitigation)));

        CurrentHealth -= mitigatedDamage;
        CurrentHealth = Mathf.Max(CurrentHealth, 0);

        UpdateHealthText();
        SpawnDamageNumber(mitigatedDamage);

        if (animator != null && !IsDead)
        {
            animator.SetTrigger("Hit");
            AudioManager.PlaySfx(hitClip, 0.4f);
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

        // Show whatever health remained as the "damage" dealt. Deliberately
        // just an instant kill for now, no freeze/slow-mo — Time.timeScale
        // effects are gone project-wide (see PlayerCombat/AudioManager),
        // since they don't work in a multiplayer future. A real "flashy"
        // execute presentation (VFX/camera, not time manipulation) is
        // deferred until real assets exist for it.
        SpawnDamageNumber(CurrentHealth);

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

        LootableCorpse lootableCorpse = GetComponent<LootableCorpse>();

        if (lootableCorpse != null)
        {
            lootableCorpse.PrepareLoot();
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
        AudioManager.PlaySfx(deathClip);

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