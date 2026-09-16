using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

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
    [Tooltip("How long the enemy circles/feints (Shuffle) before committing to close the distance and attack — a 'sizing you up' beat, not an indefinite circle. Without a commit, an enemy that stays just outside Stopping Distance would shuffle forever and never actually attack unless the player closed the gap themselves. See docs/combat-redesign-plan.md.")]
    [SerializeField] private float shuffleDecisionTime = 1.2f;
    [Tooltip("Optional. When present (and a NavMesh is baked — see SETUP.md), the Sprint phase paths around obstacles/other enemies instead of walking straight at the player. Shuffle/Attack still move via CharacterController directly, per docs/combat-redesign-plan.md — pathfinding is the only job of the NavMesh switch. Falls back to straight-line movement if left empty or off-mesh, so nothing breaks before the Editor-side setup is done.")]
    [SerializeField] private NavMeshAgent navMeshAgent;
    [Tooltip("Distance under which enemies gently push apart from each other. NavMesh's own agent-avoidance only steers around other enemies during the Sprint phase (see above) — Shuffle and the final close-in otherwise had zero awareness of nearby enemies, so several enemies converging on the player would physically jam into each other right around Stopping/Attack Range. This is a lightweight separation nudge blended into whatever movement an enemy is already doing (Sprint/closing-in/Shuffle), not a full pathfinding pass.")]
    [SerializeField] private float enemySeparationRadius = 1.4f;
    [Tooltip("How strongly the separation push above competes with the enemy's actual movement goal (closing distance / shuffling) — higher values prioritize not overlapping other enemies over beelining at the player.")]
    [SerializeField] private float enemySeparationStrength = 1.5f;

    // All enabled EnemyController instances — used only for the cheap local
    // separation pass above. A dead enemy has EnemyController disabled by
    // Health.Die() (see OnDisable), so corpses correctly stop pushing others
    // away. Wave sizes are small (see WaveManager), so an O(n) scan per
    // enemy per frame is fine — no spatial partitioning needed.
    private static readonly List<EnemyController> activeEnemies = new List<EnemyController>();

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

    [Header("Perilous Attacks")]
    [Tooltip("Perilous attacks (Downslam/Side swing) don't appear until this wave — gives the player time to learn basic combat and gear up first, same pattern as EnemyTier T2/T3 gating in WaveManager. Set once at spawn via SetSpawnWave(), not looked up continuously. See docs/combat-redesign-plan.md.")]
    [SerializeField] private int perilousUnlockWave = 3;
    [Tooltip("Chance to use a Perilous attack instead of a normal swing when eligible (unlock wave reached, off its own cooldown) — the rest of the time it's a normal attack. Placeholder, expect to retune.")]
    [SerializeField] private float perilousAttackChance = 0.35f;
    [Tooltip("Shared cooldown covering BOTH Perilous moves together, not tracked per-type — keeps them as rare, high-impact spikes rather than a constant threat. Placeholder, expect to retune.")]
    [SerializeField] private float perilousCooldown = 10f;
    [Tooltip("Both Perilous moves are unblockable/undeflectable — the only counter is being outside the affected area when it lands, not a per-type counter-move.")]
    [SerializeField] private Color perilousFlickerColor = new Color(1f, 0.55f, 0f);
    [Tooltip("Overhead impact, AoE around the enemy's own position. Get outside this radius before it lands.")]
    [SerializeField] private float downslamRadius = 3f;
    [SerializeField] private int downslamDamage = 25;
    [SerializeField] private float downslamWindup = 0.9f;
    [Tooltip("Wide horizontal arc in front of the enemy. Get clear of the arc's reach or its angle before it lands.")]
    [SerializeField] private float sideSwingRange = 2.5f;
    [Tooltip("Full width of the arc in degrees, centered on the enemy's facing direction.")]
    [SerializeField] private float sideSwingArcDegrees = 160f;
    [SerializeField] private int sideSwingDamage = 20;
    [SerializeField] private float sideSwingWindup = 0.7f;

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
    private float shuffleStartTime = -1f;
    private bool isClosingIn;
    private int spawnWave;
    private float nextPerilousTime;

    public EnemyTier Tier => tier;

    // Set once by WaveManager.SpawnEnemy() at spawn time — see
    // perilousUnlockWave. Not a continuous lookup since the wave number
    // this enemy spawned in never changes after the fact.
    public void SetSpawnWave(int wave)
    {
        spawnWave = wave;
    }

    // Enemies can't move or attack while stunned from a hit or broken from
    // stagger — mirrors the same restriction PlayerCombat applies to the player.
    private bool IsIncapacitated =>
        (health != null && health.IsDead) ||
        (hitstun != null && hitstun.IsStunned) ||
        (stagger != null && stagger.IsBroken);

    private void OnEnable()
    {
        activeEnemies.Add(this);
    }

    private void OnDisable()
    {
        activeEnemies.Remove(this);
    }

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

        if (navMeshAgent == null)
        {
            navMeshAgent = GetComponent<NavMeshAgent>();
        }

        if (navMeshAgent != null)
        {
            // CharacterController stays the sole authority on actual
            // position/rotation — the agent only computes a pathfinding
            // -aware desired direction (see MoveTowardTarget), avoiding the
            // classic "two systems both moving the same Transform" fight.
            navMeshAgent.updatePosition = false;
            navMeshAgent.updateRotation = false;
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
        // Keeps the agent's internal steering aware of where the
        // CharacterController actually put us, since updatePosition is off
        // and the agent never moves the Transform itself.
        if (navMeshAgent != null)
        {
            navMeshAgent.nextPosition = transform.position;
        }

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
            isClosingIn = false;
            shuffleStartTime = -1f;
            MoveTowardTarget();
            SetMoving(true);
        }
        else if (distanceToTarget > stoppingDistance)
        {
            // Shuffle purely circles laterally and never closes the gap on
            // its own — without a decision to commit, an enemy that stays
            // just outside Stopping Distance would shuffle forever unless
            // the player happened to close in themselves. After sizing the
            // player up for shuffleDecisionTime, commit to closing straight
            // in instead.
            if (!isClosingIn)
            {
                if (shuffleStartTime < 0f)
                {
                    shuffleStartTime = Time.time;
                }

                if (Time.time - shuffleStartTime >= shuffleDecisionTime)
                {
                    isClosingIn = true;
                }
            }

            if (isClosingIn)
            {
                MoveTowardTarget();
            }
            else
            {
                ShuffleAroundTarget();
            }

            SetMoving(true);
        }
        else
        {
            isClosingIn = false;
            shuffleStartTime = -1f;
            SetMoving(false);
            AttackTarget();
        }

        ApplyGravity();
    }

    private void MoveTowardTarget()
    {
        Vector3 directionToTarget;

        if (navMeshAgent != null && navMeshAgent.isOnNavMesh)
        {
            // The agent only supplies a pathfinding-aware direction to walk
            // in (routing around obstacles/other enemies) — CharacterController
            // still does the actual moving, same as the straight-line path below.
            navMeshAgent.SetDestination(target.position);
            directionToTarget = navMeshAgent.desiredVelocity;
            directionToTarget.y = 0f;

            if (directionToTarget.sqrMagnitude <= 0.01f)
            {
                // No path yet, or already at the last corridor point but
                // still far from the target (e.g. NavMesh not baked right
                // up to the target) — fall back rather than standing still.
                directionToTarget = target.position - transform.position;
                directionToTarget.y = 0f;
            }
        }
        else
        {
            directionToTarget = target.position - transform.position;
            directionToTarget.y = 0f;
        }

        if (directionToTarget.sqrMagnitude <= 0.01f)
        {
            return;
        }

        directionToTarget.Normalize();
        RotateToFaceDirection(directionToTarget);

        // Facing stays purely toward the target (readable, doesn't jitter),
        // but the actual movement blends in a push away from nearby
        // enemies — otherwise several enemies converging on the same point
        // just physically jam into each other once close, regardless of
        // how well NavMesh routed the approach.
        Vector3 moveDirection = (directionToTarget + GetSeparationVector() * enemySeparationStrength).normalized;

        characterController.Move(moveDirection * movementSpeed * Time.deltaTime);
    }

    // Sums a push-away vector from every other active enemy within
    // Enemy Separation Radius, stronger the closer they are. Zero when no
    // one's close enough to matter. See enemySeparationRadius' tooltip.
    private Vector3 GetSeparationVector()
    {
        Vector3 separation = Vector3.zero;

        foreach (EnemyController other in activeEnemies)
        {
            if (other == this || other == null)
            {
                continue;
            }

            Vector3 offset = transform.position - other.transform.position;
            offset.y = 0f;
            float distance = offset.magnitude;

            if (distance > 0.01f && distance < enemySeparationRadius)
            {
                separation += offset.normalized * (1f - distance / enemySeparationRadius);
            }
        }

        return separation;
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
        Vector3 moveDirection = (lateralDirection * shuffleDirection + GetSeparationVector() * enemySeparationStrength).normalized;
        characterController.Move(moveDirection * shuffleSpeed * Time.deltaTime);
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

        bool perilousEligible = spawnWave >= perilousUnlockWave && Time.time >= nextPerilousTime;

        if (perilousEligible && Random.value < perilousAttackChance)
        {
            nextPerilousTime = Time.time + perilousCooldown;

            if (Random.value < 0.5f)
            {
                nextAttackTime = Time.time + downslamWindup + attackCooldown;
                StartCoroutine(PerformDownslam());
            }
            else
            {
                nextAttackTime = Time.time + sideSwingWindup + attackCooldown;
                StartCoroutine(PerformSideSwing());
            }

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
            yield return StartCoroutine(FlickerTelegraph(attackWindup, telegraphFlickerColor));
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

    // Both Perilous moves are a single instant check at the moment the
    // windup ends ("get out of the area before it lands"), not a sustained
    // active window like the normal attack above — there's no "still in
    // range a moment later" grace period to model here.
    private IEnumerator PerformDownslam()
    {
        isAttacking = true;

        if (animator != null)
        {
            animator.SetTrigger("Attack");
        }

        AudioManager.PlaySfx(attackSwingClip);

        yield return StartCoroutine(FlickerTelegraph(downslamWindup, perilousFlickerColor));

        // Getting broken mid-windup cancels the swing, same rule as every other attack.
        if (!IsIncapacitated && IsTargetInDownslamRadius())
        {
            ResolveHit(downslamDamage, attackHitstunDuration, canBeDefended: false);
        }

        isAttacking = false;
    }

    private IEnumerator PerformSideSwing()
    {
        isAttacking = true;

        if (animator != null)
        {
            animator.SetTrigger("Attack");
        }

        AudioManager.PlaySfx(attackSwingClip);

        yield return StartCoroutine(FlickerTelegraph(sideSwingWindup, perilousFlickerColor));

        if (!IsIncapacitated && IsTargetInSideSwingArc())
        {
            ResolveHit(sideSwingDamage, attackHitstunDuration, canBeDefended: false);
        }

        isAttacking = false;
    }

    // Filler telegraph until real wind-up animations exist — flickers
    // between the tier tint and the given warning color for the whole
    // windup (a distinct color for Perilous vs a normal swing, so the read
    // is teachable, not a guess — see docs/combat-redesign-plan.md), then
    // guarantees the tier tint is restored before the active hit window
    // starts.
    private IEnumerator FlickerTelegraph(float duration, Color flickerColor)
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
            SetTint(flickerOn ? flickerColor : tierTint);

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

    private bool IsTargetInDownslamRadius()
    {
        return target != null && Vector3.Distance(transform.position, target.position) <= downslamRadius;
    }

    // Wide arc in front of the enemy — range AND angle both have to be
    // satisfied, unlike Downslam's simple radius-around-self.
    private bool IsTargetInSideSwingArc()
    {
        if (target == null)
        {
            return false;
        }

        Vector3 toTarget = target.position - transform.position;
        toTarget.y = 0f;

        if (toTarget.magnitude > sideSwingRange)
        {
            return false;
        }

        float angle = Vector3.Angle(transform.forward, toTarget);
        return angle <= sideSwingArcDegrees * 0.5f;
    }

    private void ResolveHit()
    {
        ResolveHit(attackDamage, attackHitstunDuration, canBeDefended: true);
    }

    // canBeDefended is false for Perilous attacks (Downslam/Side swing) —
    // both are unblockable/undeflectable by design, the only counter is
    // being outside the affected area when it lands, not a Deflect/Block
    // timing. See docs/combat-redesign-plan.md.
    private void ResolveHit(int damage, float hitstunDuration, bool canBeDefended)
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
        if (canBeDefended && targetCombat != null && targetCombat.TryDefendAgainst(damage))
        {
            return;
        }

        targetHealth.TakeDamage(damage);

        if (targetStagger != null)
        {
            targetStagger.AddStaggerFromDamage(damage, targetHealth.MaxHealth);
        }

        // Hyper armor: a player mid-swing isn't flinched by a routine hit —
        // damage and Stagger still land normally, so reckless aggression can
        // still get them broken and finished, it just can't be
        // chain-interrupted by every graze. See PlayerCombat.IsAttacking.
        bool targetHasHyperArmor = targetCombat != null && targetCombat.IsAttacking;

        if (targetHitstun != null && !targetHasHyperArmor)
        {
            targetHitstun.ApplyStun(hitstunDuration);
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
