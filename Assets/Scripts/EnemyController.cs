using System.Collections;
using UnityEngine;

public class EnemyController : MonoBehaviour
{
    [Header("Tier")]
    [Tooltip("Drives loot rarity (see LootableCorpse, which reads this), tints Visual Renderer by tier on spawn (see EnemyTierColor — a placeholder until real per-tier prefabs/models exist), and is a label for tuning this prefab's own stats — it doesn't auto-scale stats itself. Fast/low-hp = T1, slow/high-damage = T2, ranged/tankier = T3 is the suggested split.")]
    [SerializeField] private EnemyTier tier = EnemyTier.T1;
    [Tooltip("Optional. Tinted by Tier on spawn (see EnemyTierColor). Falls back to the first Renderer found in children if left empty.")]
    [SerializeField] private Renderer visualRenderer;

    [Header("Movement")]
    [SerializeField] private float movementSpeed = 2.5f;
    [SerializeField] private float rotationSpeed = 10f;
    [SerializeField] private float stoppingDistance = 1.6f;
    [SerializeField] private float gravity = -20f;
    [Tooltip("Distance band between this and Stopping Distance where the enemy Shuffles (moves side to side while facing the player, like circling for an opening) instead of closing straight in. Above this distance the enemy Sprints straight toward the player. See docs/combat-redesign-plan.md.")]
    [SerializeField] private float shuffleDistance = 3.5f;
    [Tooltip("Lateral movement speed while Shuffling — separate from Movement Speed so the shuffle can read as more tentative/searching than a full Sprint.")]
    [SerializeField] private float shuffleSpeed = 1.5f;
    [Tooltip("How often the shuffle direction flips (left/right), in seconds.")]
    [SerializeField] private float shuffleFlipInterval = 0.8f;

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
    [Tooltip("SFX — assign once you have a clip (see AudioManager).")]
    [SerializeField] private AudioClip attackSwingClip;

    [Header("Telegraph")]
    [Tooltip("Filler visual telegraph until real wind-up animations exist — flickers this color during the attack windup so an incoming hit is readable, not just mechanically fair (the windup timing already existed, it just wasn't visible). See docs/combat-redesign-plan.md.")]
    [SerializeField] private Color telegraphFlickerColor = new Color(1f, 0.15f, 0.1f);
    [SerializeField] private float telegraphFlickerInterval = 0.08f;

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
    private MaterialPropertyBlock propertyBlock;
    private float shuffleDirection = 1f;
    private float nextShuffleFlipTime;

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

        if (visualRenderer == null)
        {
            visualRenderer = GetComponentInChildren<Renderer>();
        }

        TintByTier();

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

    // Cheap placeholder tier tell until real per-tier prefabs/models exist —
    // see EnemyTierColor.
    private void TintByTier()
    {
        SetTint(EnemyTierColor.Get(tier));
    }

    private void SetTint(Color tint)
    {
        if (visualRenderer == null)
        {
            return;
        }

        propertyBlock ??= new MaterialPropertyBlock();
        visualRenderer.GetPropertyBlock(propertyBlock);
        propertyBlock.SetColor("_BaseColor", tint);
        propertyBlock.SetColor("_Color", tint);
        visualRenderer.SetPropertyBlock(propertyBlock);
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

        // Three-phase approach instead of one constant chase speed: Sprint
        // while far, Shuffle (circle, looking for an opening) once close but
        // not yet in range, Attack once in range. See docs/combat-redesign-plan.md.
        if (distanceToTarget > shuffleDistance)
        {
            MoveTowardTarget();
            SetMoving(true);
        }
        else if (distanceToTarget > stoppingDistance)
        {
            ShuffleAroundTarget();
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
        RotateToFaceDirection(directionToTarget);

        characterController.Move(directionToTarget * movementSpeed * Time.deltaTime);
    }

    // Moves side to side while still facing the player, like real sword
    // -fighting circling/feinting while looking for an opening — reads as
    // "about to commit to something" more than a slower straight approach
    // would. See docs/combat-redesign-plan.md.
    private void ShuffleAroundTarget()
    {
        Vector3 directionToTarget = target.position - transform.position;
        directionToTarget.y = 0f;

        if (directionToTarget.sqrMagnitude <= 0.01f)
        {
            return;
        }

        directionToTarget.Normalize();
        RotateToFaceDirection(directionToTarget);

        if (Time.time >= nextShuffleFlipTime)
        {
            shuffleDirection *= -1f;
            nextShuffleFlipTime = Time.time + shuffleFlipInterval;
        }

        Vector3 lateralDirection = Vector3.Cross(Vector3.up, directionToTarget).normalized;
        characterController.Move(lateralDirection * shuffleDirection * shuffleSpeed * Time.deltaTime);
    }

    private void RotateToFaceDirection(Vector3 direction)
    {
        Quaternion targetRotation = Quaternion.LookRotation(direction);

        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            targetRotation,
            rotationSpeed * Time.deltaTime
        );
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

        AudioManager.PlaySfx(attackSwingClip);

        if (attackWindup > 0f)
        {
            yield return StartCoroutine(FlickerTelegraph(attackWindup));
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

    // Filler telegraph until real wind-up animations exist — flickers
    // between the tier tint and a warning color for the whole windup, then
    // guarantees the tier tint is restored before the active hit window
    // starts. See docs/combat-redesign-plan.md.
    private IEnumerator FlickerTelegraph(float duration)
    {
        float elapsed = 0f;
        bool flickerOn = false;
        Color tierTint = EnemyTierColor.Get(tier);

        while (elapsed < duration)
        {
            if (IsIncapacitated)
            {
                break;
            }

            flickerOn = !flickerOn;
            SetTint(flickerOn ? telegraphFlickerColor : tierTint);

            float step = Mathf.Min(telegraphFlickerInterval, duration - elapsed);
            yield return new WaitForSeconds(step);
            elapsed += step;
        }

        SetTint(tierTint);
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

        // Deflect (no cost, fills the Ultimate meter) or Block (absorbs the
        // hit but costs the player Stagger instead) — see
        // PlayerCombat.TryDefendAgainst and docs/combat-redesign-plan.md. A
        // successfully defended hit skips damage/stagger/hitstun entirely.
        if (targetCombat != null && targetCombat.TryDefendAgainst(attackDamage))
        {
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
