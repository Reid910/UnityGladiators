using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerCombat : MonoBehaviour
{
    [System.Serializable]
    private struct ComboHit
    {
        public int damage;
        public float hitstunDuration;
        [Tooltip("Telegraph delay before the hitbox becomes active — gives an opponent (or the player, when an enemy swings) a real window to react/dodge instead of an instant hit.")]
        public float windup;
        [Tooltip("How long after the windup the hitbox stays checkable. A target only needs to be in range at some point during this window, not at the exact instant the button was pressed.")]
        public float activeDuration;
        public float recoveryTime;
        public string animatorTrigger;
    }

    [Header("Light Combo")]
    // animatorTrigger names (AttackCombo1/2/3) are generic on purpose — they're
    // a permanent interface, while the clips they currently point to (the
    // Blink pack's MeleeAttack_OneHanded, PunchLeft, PunchRight) are disposable
    // filler that just needs to look like 3 distinct hits. Heavy gets its own
    // dedicated AttackHeavy state/clip (MeleeAttack_TwoHanded) so it stays
    // visually distinct from combo hit 1.
    // windup+activeDuration+recoveryTime sums match the original single
    // recoveryTime values, so overall combo pacing is unchanged — this just
    // carves out an explicit telegraph + hit window instead of an instant hit.
    // No stagger value per hit anymore — Stagger.AddStaggerFromDamage derives
    // it from damage dealt vs. the target's own max health.
    [SerializeField]
    private ComboHit[] lightComboHits =
    {
        new ComboHit { damage = 15, hitstunDuration = 0.2f, windup = 0.08f, activeDuration = 0.08f, recoveryTime = 0.19f, animatorTrigger = "AttackCombo1" },
        new ComboHit { damage = 18, hitstunDuration = 0.2f, windup = 0.08f, activeDuration = 0.08f, recoveryTime = 0.19f, animatorTrigger = "AttackCombo2" },
        new ComboHit { damage = 28, hitstunDuration = 0.25f, windup = 0.12f, activeDuration = 0.1f, recoveryTime = 0.28f, animatorTrigger = "AttackCombo3" },
    };
    [SerializeField] private float comboWindow = 0.8f;

    [Header("Heavy Attack")]
    [SerializeField] private int heavyDamage = 40;
    [SerializeField] private float heavyHitstunDuration = 0.35f;
    [SerializeField] private float heavyWindup = 0.35f;
    [SerializeField] private float heavyActiveDuration = 0.15f;
    [SerializeField] private float heavyRecoveryTime = 0.4f;
    [Tooltip("Heavy and Ultimate both exist primarily to build stagger, not health damage, just at very different scales (see docs/combat-redesign-plan.md) — this multiplies the stagger contribution only, on top of Stagger's own damageToStaggerMultiplier, not the actual HP damage dealt.")]
    [SerializeField] private float heavyStaggerMultiplier = 2f;

    [Header("Ultimate")]
    [Tooltip("Huge stagger bonus + good damage, AoE, rare/meter-gated — the 'reset the fight' payoff move. Reuses the AttackHeavy animator state for now (no distinct animation exists yet).")]
    [SerializeField] private int ultimateDamage = 60;
    [SerializeField] private float ultimateHitstunDuration = 0.4f;
    [SerializeField] private float ultimateWindup = 0.4f;
    [SerializeField] private float ultimateActiveDuration = 0.2f;
    [SerializeField] private float ultimateRecoveryTime = 0.6f;
    [Tooltip("AoE radius — bigger than the normal Attack Range since Heavy/Ultimate are both meant to hit a surrounding cluster, not one target.")]
    [SerializeField] private float ultimateRange = 3.5f;
    [SerializeField] private float ultimateStaggerMultiplier = 5f;
    [SerializeField] private float ultimateMeterMax = 100f;
    [Tooltip("Meter gained per source, all placeholders — see docs/combat-redesign-plan.md.")]
    [SerializeField] private float ultimateMeterPerHit = 5f;
    [SerializeField] private float ultimateMeterPerDeflect = 20f;
    [SerializeField] private float ultimateMeterPerFinisher = 15f;

    [Header("Deflect / Block")]
    [Tooltip("Same input handles both, repurposing the unused stock 'Jump' action (bound to Space) — see docs/combat-redesign-plan.md's 'no jump' decision. A fresh press within the equipped Gloves item's Deflect Window of an incoming hit becomes a perfect Deflect (no cost, fills the Ultimate meter fast, requires a Gloves item with a DeflectDefinition); just holding the button Blocks HP damage but costs the player Stagger instead, no gear required — this is how Sekiro's actual posture-on-block works.")]
    [SerializeField] private float blockStaggerCostMultiplier = 0.75f;
    [Tooltip("Optional. A successful Deflect had no feedback at all otherwise (no clip assigned yet, no visual cue), making it indistinguishable from a plain Block in playtesting. Falls back to the first Renderer found in children if left empty.")]
    [SerializeField] private Renderer visualRenderer;
    [Tooltip("Cheap placeholder tell for a successful Deflect until real VFX exists — briefly tints Visual Renderer this color, same MaterialPropertyBlock technique EnemyController already uses for its telegraph flicker.")]
    [SerializeField] private Color deflectFlashColor = new Color(1f, 0.85f, 0.2f);
    [SerializeField] private float deflectFlashDuration = 0.15f;
    [Tooltip("A very brief freeze-frame (see HitStop.cs) sells a perfect-timing parry the same way a bigger one sells a finisher — much shorter since this should happen often, not read as a big event.")]
    [SerializeField] private float deflectHitStopDuration = 0.04f;

    [Header("Attack")]
    [SerializeField] private Transform attackPoint;
    [SerializeField] private float attackRange = 1.5f;
    [SerializeField] private LayerMask enemyLayer;
    [Tooltip("Damage multiplier applied on a crit (see Crit Chance affix, StatType.CritChance).")]
    [SerializeField] private float critDamageMultiplier = 1.5f;
    [Tooltip("Separate from enemyLayer — dead enemies' corpse hitboxes (see Health.corpseHitbox) live here so attacks can loot them instead of dealing damage.")]
    [SerializeField] private LayerMask corpseLayer;

    [Header("Slide")]
    [Tooltip("Dash while sprinting triggers a Slide instead of the normal instant-burst dash — same distance/cooldown/i-frames from the equipped DashDefinition, but covered over this duration instead of one instant Move(). See docs/combat-redesign-plan.md.")]
    [SerializeField] private float slideDuration = 0.3f;
    [Tooltip("Speed multiplier applied briefly right after a Slide ends — the actual 'chain moves while still fast' ingredient, not the slide itself.")]
    [SerializeField] private float slideMomentumMultiplier = 1.4f;
    [SerializeField] private float slideMomentumDuration = 0.25f;

    [Header("Movement-state attack variants")]
    [Tooltip("Window after a Dash/Slide ends where the next Light attack becomes a dodge-out attack. No distinct animation exists yet (see docs/combat-redesign-plan.md) — a forward lunge burst is the placeholder mechanical effect that makes the variant real and testable already.")]
    [SerializeField] private float dodgeOutWindow = 0.25f;
    [SerializeField] private float attackLungeDistance = 1.2f;
    [Tooltip("How long the lunge burst takes to cover Attack Lunge Distance. A single-frame CharacterController.Move() covering the full distance instantly read as a teleport rather than a lunge — spreading it over this short window instead.")]
    [SerializeField] private float attackLungeDuration = 0.12f;

    [Header("SFX (assign clips once you have them — see AudioManager)")]
    [SerializeField] private AudioClip lightAttackClip;
    [SerializeField] private AudioClip heavyAttackClip;
    [SerializeField] private AudioClip ultimateClip;
    [SerializeField] private AudioClip hitImpactClip;
    [SerializeField] private AudioClip abilityCastClip;
    [SerializeField] private AudioClip dashClip;
    [SerializeField] private AudioClip slideClip;
    [SerializeField] private AudioClip deflectClip;
    [SerializeField] private AudioClip blockClip;

    [Header("References")]
    [SerializeField] private Animator animator;
    [SerializeField] private Health health;
    [SerializeField] private CharacterController characterController;
    [SerializeField] private Stagger stagger;
    [SerializeField] private Hitstun hitstun;
    [SerializeField] private PlayerStats playerStats;
    [SerializeField] private PlayerEquipment equipment;
    [SerializeField] private PlayerController playerController;

    private InputSystem_Actions inputSystemActions;
    private Coroutine attackCoroutine;
    private Coroutine attackLungeCoroutine;
    private Coroutine deflectFlashCoroutine;
    private MaterialPropertyBlock propertyBlock;

    private int comboStep;
    private float comboResetTime;
    private float nextAttackTime;
    private float hyperArmorUntilTime;
    private float nextAbilityTime;
    private float nextDashTime;
    private float invulnerableUntilTime;
    private float dashEndedTime = float.NegativeInfinity;
    private float lastDeflectPressTime = float.NegativeInfinity;
    private bool isBlockHeld;
    private float ultimateMeter;
    private float nextAutoDodgeTime;

    public int ComboStep => comboStep;
    public float AttackCooldownRemaining => Mathf.Max(0f, nextAttackTime - Time.time);
    public float AttackCooldownDuration { get; private set; }
    public float AbilityCooldownRemaining => Mathf.Max(0f, nextAbilityTime - Time.time);
    public float DashCooldownRemaining => Mathf.Max(0f, nextDashTime - Time.time);
    public float UltimateMeter => ultimateMeter;
    public float UltimateMeterMax => ultimateMeterMax;
    public bool IsUltimateReady => ultimateMeter >= ultimateMeterMax;

    // Hyper armor window: true from the moment a Heavy attack starts (windup)
    // until its full recovery ends. Light no longer grants this (see
    // docs/combat-redesign-plan.md — cut so basic combat has a real safe
    // window to poke in, Heavy stays a deliberate bigger-commitment trade).
    // EnemyController checks this to skip applying Hitstun while true;
    // damage/Stagger still land normally, so this only stops a routine hit
    // from flinching the player out of a swing they've already committed to.
    public bool IsAttacking => Time.time < hyperArmorUntilTime;

    // True i-frames from dashing (see DashDefinition.InvulnerabilityDuration)
    // — unlike hyper armor, this blocks damage/stagger/finishers entirely,
    // not just hitstun. EnemyController checks this before resolving a hit
    // at all.
    public bool IsInvulnerable => Time.time < invulnerableUntilTime;

    private bool IsDead => health != null && health.IsDead;

    // The player can't act while stunned from a hit or broken from stagger —
    // mirrors the same restriction EnemyController applies to enemies.
    private bool IsIncapacitated =>
        IsDead ||
        (hitstun != null && hitstun.IsStunned) ||
        (stagger != null && stagger.IsBroken);

    private void Awake()
    {
        inputSystemActions = new InputSystem_Actions();

        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }

        if (visualRenderer == null)
        {
            visualRenderer = GetComponentInChildren<Renderer>();
        }

        if (health == null)
        {
            health = GetComponent<Health>();
        }

        if (characterController == null)
        {
            characterController = GetComponent<CharacterController>();
        }

        if (stagger == null)
        {
            stagger = GetComponent<Stagger>();
        }

        if (hitstun == null)
        {
            hitstun = GetComponent<Hitstun>();
        }

        if (playerStats == null)
        {
            playerStats = GetComponent<PlayerStats>();
        }

        if (equipment == null)
        {
            equipment = GetComponent<PlayerEquipment>();
        }

        if (playerController == null)
        {
            playerController = GetComponent<PlayerController>();
        }
    }

    private void OnEnable()
    {
        inputSystemActions.Player.Enable();
        inputSystemActions.Player.Attack.performed += OnAttackPerformed;
        inputSystemActions.Player.Heavy.performed += OnHeavyPerformed;
        inputSystemActions.Player.Ability.performed += OnAbilityPerformed;
        inputSystemActions.Player.Dash.performed += OnDashPerformed;
        inputSystemActions.Player.Interact.performed += OnInteractPerformed;
        // Jump/Crouch are unused stock actions repurposed for Deflect/Block
        // and Ultimate (see the Deflect/Block and Ultimate field headers) —
        // avoids touching the Input Actions asset or its generated wrapper.
        inputSystemActions.Player.Jump.started += OnDeflectStarted;
        inputSystemActions.Player.Jump.canceled += OnDeflectCanceled;
        inputSystemActions.Player.Crouch.performed += OnUltimatePerformed;
    }

    private void OnDisable()
    {
        inputSystemActions.Player.Attack.performed -= OnAttackPerformed;
        inputSystemActions.Player.Heavy.performed -= OnHeavyPerformed;
        inputSystemActions.Player.Ability.performed -= OnAbilityPerformed;
        inputSystemActions.Player.Dash.performed -= OnDashPerformed;
        inputSystemActions.Player.Interact.performed -= OnInteractPerformed;
        inputSystemActions.Player.Jump.started -= OnDeflectStarted;
        inputSystemActions.Player.Jump.canceled -= OnDeflectCanceled;
        inputSystemActions.Player.Crouch.performed -= OnUltimatePerformed;
        inputSystemActions.Player.Disable();
    }

    private void OnAttackPerformed(InputAction.CallbackContext context) => TryLightAttack();

    private void OnHeavyPerformed(InputAction.CallbackContext context) => TryHeavyAttack();

    private void OnAbilityPerformed(InputAction.CallbackContext context) => TryUseAbility();

    private void OnDashPerformed(InputAction.CallbackContext context) => TryDash();

    private void OnUltimatePerformed(InputAction.CallbackContext context) => TryUltimateAttack();

    private void OnDeflectStarted(InputAction.CallbackContext context)
    {
        lastDeflectPressTime = Time.time;
        isBlockHeld = true;

        if (animator != null)
        {
            animator.SetBool("IsBlocking", true);
        }
    }

    private void OnDeflectCanceled(InputAction.CallbackContext context)
    {
        isBlockHeld = false;

        if (animator != null)
        {
            animator.SetBool("IsBlocking", false);
        }
    }

    // No inventory: swaps whatever's in the nearby ItemPickup's slot with
    // the player's currently equipped item there (see
    // PlayerEquipment.TrySwapWithNearby). No-ops if nothing's in range.
    private void OnInteractPerformed(InputAction.CallbackContext context) => equipment?.TrySwapWithNearby();

    private void TryLightAttack()
    {
        if (IsIncapacitated || Time.time < nextAttackTime || lightComboHits.Length == 0)
        {
            return;
        }

        if (Time.time > comboResetTime)
        {
            comboStep = 0;
        }

        int hitIndex = comboStep % lightComboHits.Length;
        ComboHit hit = lightComboHits[hitIndex];
        comboStep++;

        // Movement-state attack variants (see docs/combat-redesign-plan.md,
        // modeled on Elden Ring's running/roll attacks): dodge-out takes
        // priority since a Slide is also technically "sprinting" right as it
        // ends. No distinct animation exists for either yet — a forward
        // lunge burst is the placeholder mechanical effect.
        bool isDodgeOutAttack = Time.time - dashEndedTime <= dodgeOutWindow;
        bool isSprintAttack = !isDodgeOutAttack && playerController != null && playerController.IsSprinting;

        BeginAttack(hit.damage, hit.hitstunDuration, hit.animatorTrigger, hit.windup, hit.activeDuration, hit.recoveryTime, isLightAttack: true);
        AudioManager.PlaySfx(lightAttackClip);

        if (isDodgeOutAttack || isSprintAttack)
        {
            Vector3 lungeDirection = playerController != null && playerController.MovementDirection.sqrMagnitude > 0.01f
                ? playerController.MovementDirection
                : transform.forward;

            if (attackLungeCoroutine != null)
            {
                StopCoroutine(attackLungeCoroutine);
            }

            attackLungeCoroutine = StartCoroutine(PerformAttackLunge(lungeDirection, attackLungeDistance));
        }

        comboResetTime = nextAttackTime + comboWindow;
    }

    // Covers Attack Lunge Distance over Attack Lunge Duration instead of one
    // instant CharacterController.Move() call, which read as a teleport
    // rather than a lunge — same incremental-Move pattern as PerformSlide.
    private IEnumerator PerformAttackLunge(Vector3 direction, float distance)
    {
        float elapsed = 0f;
        float speed = distance / Mathf.Max(0.01f, attackLungeDuration);

        while (elapsed < attackLungeDuration)
        {
            float step = Mathf.Min(Time.deltaTime, attackLungeDuration - elapsed);
            characterController.Move(direction * speed * step);
            elapsed += step;
            yield return null;
        }
    }

    private void TryHeavyAttack()
    {
        if (IsIncapacitated || Time.time < nextAttackTime)
        {
            return;
        }

        // Dedicated AttackHeavy state (MeleeAttack_TwoHanded) — a bigger,
        // different motion from any combo hit.
        BeginAttack(heavyDamage, heavyHitstunDuration, "AttackHeavy", heavyWindup, heavyActiveDuration, heavyRecoveryTime, grantsHyperArmor: true, staggerMultiplier: heavyStaggerMultiplier);
        AudioManager.PlaySfx(heavyAttackClip);

        // Heavy attack interrupts and resets the light combo chain.
        comboStep = 0;
        comboResetTime = nextAttackTime;
    }

    private void TryUltimateAttack()
    {
        if (IsIncapacitated || Time.time < nextAttackTime || !IsUltimateReady)
        {
            return;
        }

        ultimateMeter = 0f;

        // Reuses the AttackHeavy state — no distinct Ultimate animation
        // exists yet (see docs/combat-redesign-plan.md).
        BeginAttack(ultimateDamage, ultimateHitstunDuration, "AttackHeavy", ultimateWindup, ultimateActiveDuration, ultimateRecoveryTime, range: ultimateRange, grantsHyperArmor: true, staggerMultiplier: ultimateStaggerMultiplier);
        AudioManager.PlaySfx(ultimateClip);

        // Same interrupt-and-reset behavior as Heavy.
        comboStep = 0;
        comboResetTime = nextAttackTime;
    }

    // Called by CheckHit whenever a hit lands, and by a successful Deflect
    // — the three sources the design doc lists. See docs/combat-redesign-plan.md.
    private void AddUltimateMeter(float amount)
    {
        ultimateMeter = Mathf.Min(ultimateMeterMax, ultimateMeter + amount);
    }

    // Head passive effect (see docs/combat-redesign-plan.md) — heals the
    // player for a fraction of damage just dealt. Read directly from the
    // equipped item rather than through PlayerStats' static recalculation
    // since this only matters at the moment a hit actually lands.
    private void ApplyLifesteal(int damageDealt)
    {
        PassiveEffectDefinition headEffect = equipment?.GetEquipped(ItemSlot.Head)?.Definition?.PassiveEffectDefinition;

        if (headEffect == null || headEffect.EffectType != PassiveEffectType.Lifesteal || health == null)
        {
            return;
        }

        health.Heal(Mathf.RoundToInt(damageDealt * headEffect.Value));
    }

    // Pants passive effect (see docs/combat-redesign-plan.md) — a free
    // automatic invulnerability window on a cooldown, independent of Boots'
    // Dash. Polled every frame rather than event-driven since it's a
    // standing cooldown, not a reaction to something happening.
    private void Update()
    {
        if (IsIncapacitated || Time.time < nextAutoDodgeTime)
        {
            return;
        }

        PassiveEffectDefinition pantsEffect = equipment?.GetEquipped(ItemSlot.Pants)?.Definition?.PassiveEffectDefinition;

        if (pantsEffect == null || pantsEffect.EffectType != PassiveEffectType.AutoDodge)
        {
            return;
        }

        nextAutoDodgeTime = Time.time + pantsEffect.Cooldown;
        invulnerableUntilTime = Mathf.Max(invulnerableUntilTime, Time.time + pantsEffect.Value);
    }

    // Called by whatever resolves a hit against the player (see
    // EnemyController.ResolveHit()) before applying damage/stagger/hitstun —
    // returns true if the hit was fully absorbed (Deflect or Block), meaning
    // the caller should skip its normal resolution entirely. Perfect Deflect
    // requires a Gloves item with a DeflectDefinition; Block is universal
    // regardless of gear (see docs/combat-redesign-plan.md — this is what
    // guarantees baseline defense even without a good Gloves item).
    public bool TryDefendAgainst(int incomingDamage)
    {
        if (IsIncapacitated)
        {
            return false;
        }

        DeflectDefinition deflectDefinition = equipment?.GetEquipped(ItemSlot.Gloves)?.Definition?.DeflectDefinition;
        bool isPerfectDeflect = deflectDefinition != null && Time.time - lastDeflectPressTime <= deflectDefinition.DeflectWindow;

        if (isPerfectDeflect)
        {
            AddUltimateMeter(ultimateMeterPerDeflect);
            AudioManager.PlaySfx(deflectClip);
            HitStop.Trigger(deflectHitStopDuration);
            FlashDeflect();
            return true;
        }

        if (isBlockHeld)
        {
            // Same posture-on-block model Sekiro actually uses: holding
            // block absorbs the hit but costs the player Stagger instead of
            // the enemy taking none — only a fresh, well-timed press avoids
            // that cost entirely (the branch above).
            if (stagger != null && health != null)
            {
                int blockStaggerDamage = Mathf.RoundToInt(incomingDamage * blockStaggerCostMultiplier);
                stagger.AddStaggerFromDamage(blockStaggerDamage, health.MaxHealth);
            }

            AudioManager.PlaySfx(blockClip);
            return true;
        }

        return false;
    }

    // Cheap placeholder tell for a successful Deflect until real VFX exists —
    // a perfect Deflect otherwise had zero feedback (no clip assigned yet,
    // no visual cue), making it indistinguishable from a plain Block in
    // playtesting. Same MaterialPropertyBlock technique EnemyController
    // already uses for its telegraph flicker.
    private void FlashDeflect()
    {
        if (visualRenderer == null)
        {
            return;
        }

        if (deflectFlashCoroutine != null)
        {
            StopCoroutine(deflectFlashCoroutine);
        }

        deflectFlashCoroutine = StartCoroutine(DeflectFlashRoutine());
    }

    private IEnumerator DeflectFlashRoutine()
    {
        propertyBlock ??= new MaterialPropertyBlock();
        visualRenderer.GetPropertyBlock(propertyBlock);
        propertyBlock.SetColor("_BaseColor", deflectFlashColor);
        propertyBlock.SetColor("_Color", deflectFlashColor);
        visualRenderer.SetPropertyBlock(propertyBlock);

        yield return new WaitForSeconds(deflectFlashDuration);

        // Clears the override rather than restoring a hardcoded "neutral"
        // color — unlike EnemyController's tier tint, the player's actual
        // base appearance isn't something this script should need to know.
        visualRenderer.SetPropertyBlock(null);
        deflectFlashCoroutine = null;
    }

    // Attack Speed affix (Head-flavored, see TODO.md) shortens recovery time.
    private float ApplyAttackSpeed(float duration)
    {
        float attackSpeedMultiplier = 1f + (playerStats != null ? playerStats.GetStat(StatType.AttackSpeed) : 0f);
        return duration / Mathf.Max(0.1f, attackSpeedMultiplier);
    }

    private void BeginAttack(int damage, float hitstunDuration, string animatorTrigger, float windup, float activeDuration, float recoveryTime, float range = -1f, bool grantsHyperArmor = false, bool isLightAttack = false, float staggerMultiplier = 1f)
    {
        float scaledWindup = ApplyAttackSpeed(windup);
        float scaledActiveDuration = ApplyAttackSpeed(activeDuration);
        float scaledRecoveryTime = ApplyAttackSpeed(recoveryTime);
        float hitRange = range > 0f ? range : attackRange;

        AttackCooldownDuration = scaledWindup + scaledActiveDuration + scaledRecoveryTime;
        nextAttackTime = Time.time + AttackCooldownDuration;

        if (grantsHyperArmor)
        {
            hyperArmorUntilTime = nextAttackTime;
        }

        if (attackCoroutine != null)
        {
            StopCoroutine(attackCoroutine);
        }

        attackCoroutine = StartCoroutine(PerformAttack(damage, hitstunDuration, animatorTrigger, scaledWindup, scaledActiveDuration, hitRange, isLightAttack, staggerMultiplier));
    }

    private IEnumerator PerformAttack(int damage, float hitstunDuration, string animatorTrigger, float windup, float activeDuration, float range, bool isLightAttack = false, float staggerMultiplier = 1f)
    {
        if (animator != null && !string.IsNullOrEmpty(animatorTrigger))
        {
            animator.SetTrigger(animatorTrigger);
        }

        if (windup > 0f)
        {
            yield return new WaitForSeconds(windup);
        }

        // Getting broken (not just hitstunned — hyper armor covers that)
        // mid-windup cancels the hit — a fully-interrupted swing shouldn't
        // still land.
        if (IsIncapacitated)
        {
            yield break;
        }

        int totalDamage = RollDamage(damage);

        LootCorpses();

        HashSet<Health> hitTargets = new HashSet<Health>();
        float activeEndTime = Time.time + Mathf.Max(activeDuration, Time.deltaTime);

        do
        {
            CheckHit(totalDamage, hitstunDuration, hitTargets, range, isLightAttack, staggerMultiplier);
            yield return null;
        }
        while (Time.time < activeEndTime);
    }

    // Crit Chance affix: one roll per swing, not per enemy hit, so every
    // enemy caught in a single swing (or a swing's whole active window)
    // shares the same crit result.
    private int RollDamage(int baseDamage)
    {
        int totalDamage = baseDamage + (playerStats != null ? playerStats.TotalDamage : 0);
        float critChance = playerStats != null ? playerStats.GetStat(StatType.CritChance) : 0f;

        if (Random.value < critChance)
        {
            totalDamage = Mathf.RoundToInt(totalDamage * critDamageMultiplier);
        }

        return totalDamage;
    }

    private void TryUseAbility()
    {
        // Now a real committed action like everything else (see
        // docs/combat-redesign-plan.md) — shares the same nextAttackTime
        // lock as Light/Heavy/Ultimate/Dash, so it can't be cast mid-swing
        // or mid-slide, and casting it locks other actions out for its own
        // duration in turn. Previously deliberately exempt ("weave between
        // combo hits"); reversed per playtest feedback. Still has its own
        // independent cooldown (nextAbilityTime) on top of that lock.
        if (IsIncapacitated || Time.time < nextAttackTime || Time.time < nextAbilityTime)
        {
            return;
        }

        // Ash-of-War style: the ability comes from whichever weapon is
        // equipped. No weapon (or a weapon with no AbilityDefinition) means
        // the ability button does nothing.
        AbilityDefinition abilityDefinition = equipment?.GetEquipped(ItemSlot.Weapon)?.Definition?.AbilityDefinition;

        if (abilityDefinition == null)
        {
            return;
        }

        float cooldownReduction = playerStats != null ? playerStats.GetStat(StatType.AbilityCooldownReduction) : 0f;
        float effectiveCooldown = Mathf.Max(0.1f, abilityDefinition.Cooldown * (1f - cooldownReduction));
        nextAbilityTime = Time.time + effectiveCooldown;
        AudioManager.PlaySfx(abilityCastClip);

        // A bigger, rarer hit than a normal swing, with its own damage/
        // range/recovery from the weapon's AbilityDefinition. Interrupts
        // and resets the light combo chain, same as Heavy/Ultimate — it's
        // a real committed action now, not something weaved in between.
        // animatorTrigger ("AbilityCast" by default) maps to a real state
        // (SpellCast, filler from the Blink pack) — deliberately a
        // different-looking motion from the punch/melee combo states so an
        // ability reads as clearly distinct from a normal attack.
        BeginAttack(
            abilityDefinition.Damage,
            abilityDefinition.HitstunDuration,
            abilityDefinition.AnimatorTrigger,
            abilityDefinition.Windup,
            abilityDefinition.ActiveDuration,
            abilityDefinition.RecoveryTime,
            range: abilityDefinition.Range);

        comboStep = 0;
        comboResetTime = nextAttackTime;
    }

    private void TryDash()
    {
        if (IsIncapacitated || Time.time < nextDashTime || characterController == null)
        {
            return;
        }

        // Risk of Rain shift-style: the dash comes from whichever boots are
        // equipped. No boots (or boots with no DashDefinition) means no dash.
        DashDefinition dashDefinition = equipment?.GetEquipped(ItemSlot.Boots)?.Definition?.DashDefinition;

        if (dashDefinition == null)
        {
            return;
        }

        // Respects movement input direction instead of always firing in the
        // current facing (see docs/combat-redesign-plan.md) — transform.forward
        // lags behind input during quick turns since PlayerController smooths
        // rotation, but a dash should go where you're pressing right now.
        // Falls back to facing direction when standing still.
        Vector3 dashDirection = playerController != null && playerController.MovementDirection.sqrMagnitude > 0.01f
            ? playerController.MovementDirection
            : transform.forward;

        invulnerableUntilTime = Time.time + dashDefinition.InvulnerabilityDuration;

        // Dash while sprinting becomes a Slide instead of the normal
        // instant-burst dash — same input, state-conditional result, no new
        // button (see docs/combat-redesign-plan.md).
        if (playerController != null && playerController.IsSprinting)
        {
            // Locked into the slide for its full duration except the last
            // Dodge Out Window seconds — attacking or re-dashing mid-slide
            // isn't allowed, matching a Dark Souls-style "committed to your
            // action" feel. dashEndedTime opens the dodge-out attack window
            // starting at the tail, not at the slide's full completion, so
            // the very first input once the lock lifts already qualifies.
            float slideLockDuration = Mathf.Max(0f, slideDuration - dodgeOutWindow);
            float tailStartTime = Time.time + slideLockDuration;

            nextAttackTime = Mathf.Max(nextAttackTime, tailStartTime);
            nextDashTime = Mathf.Max(nextDashTime, tailStartTime);
            dashEndedTime = tailStartTime;

            StartCoroutine(PerformSlide(dashDirection, dashDefinition.Distance));
            AudioManager.PlaySfx(slideClip);
        }
        else
        {
            characterController.Move(dashDirection * dashDefinition.Distance);
            dashEndedTime = Time.time;
            AudioManager.PlaySfx(dashClip);
        }

        if (dashDefinition.DealsDamage)
        {
            // Dash's own movement is the telegraph, so this hits instantly
            // rather than going through the windup/active-window pipeline —
            // no "Dash" animator trigger exists yet either (see TryUseAbility).
            int totalDamage = RollDamage(dashDefinition.Damage);
            CheckHit(totalDamage, 0f, new HashSet<Health>(), attackRange);
            LootCorpses();
        }

        // Mathf.Max so the slide's lock (set above) isn't shortened by a
        // Dash cooldown that happens to be quicker than the slide itself.
        nextDashTime = Mathf.Max(nextDashTime, Time.time + dashDefinition.Cooldown);
    }

    // Covers the same total distance as a normal dash, but over time
    // instead of one instant Move() — the actual "slide" — then applies a
    // brief momentum boost so chaining into the next action (another dash,
    // a dodge-out attack) feels fast rather than snapping back to normal
    // speed immediately. See docs/combat-redesign-plan.md.
    private IEnumerator PerformSlide(Vector3 direction, float distance)
    {
        // RollForward (Blink pack) stands in for a dedicated slide clip —
        // no literal "slide" animation exists in the pack, but a forward
        // roll reads as the same kind of low, fast, forward-traveling move.
        if (animator != null)
        {
            animator.SetTrigger("Slide");
        }

        float elapsed = 0f;
        float speed = distance / Mathf.Max(0.01f, slideDuration);

        while (elapsed < slideDuration)
        {
            float step = Mathf.Min(Time.deltaTime, slideDuration - elapsed);
            characterController.Move(direction * speed * step);
            elapsed += step;
            yield return null;
        }

        // dashEndedTime/nextAttackTime/nextDashTime were already set upfront
        // in TryDash() to open the tail/dodge-out window before the slide
        // physically finishes — nothing to set here.
        playerController?.ApplyMomentumBoost(slideMomentumMultiplier, slideMomentumDuration);
    }

    private void CheckHit(int totalDamage, float hitstunDuration, HashSet<Health> alreadyHit, float range, bool isLightAttack = false, float staggerMultiplier = 1f)
    {
        Collider[] hitEnemies = Physics.OverlapSphere(
            attackPoint.position,
            range,
            enemyLayer
        );

        foreach (Collider enemyCollider in hitEnemies)
        {
            Health enemyHealth = enemyCollider.GetComponentInParent<Health>();

            if (enemyHealth == null || enemyHealth.IsDead || alreadyHit.Contains(enemyHealth))
            {
                continue;
            }

            alreadyHit.Add(enemyHealth);

            Stagger enemyStagger = enemyCollider.GetComponentInParent<Stagger>();

            // Landing any hit on an already-broken enemy is a finisher —
            // instant kill. Only Light attack gets the bonus finisher meter
            // (see docs/combat-redesign-plan.md — Heavy/Ultimate/Ability/Dash
            // just kill outright, no separate reward beyond the baseline
            // per-hit meter gain below).
            if (enemyStagger != null && enemyStagger.IsBroken)
            {
                enemyHealth.Execute();
                AddUltimateMeter(isLightAttack ? ultimateMeterPerFinisher : ultimateMeterPerHit);
                continue;
            }

            enemyHealth.TakeDamage(totalDamage);
            AddUltimateMeter(ultimateMeterPerHit);
            ApplyLifesteal(totalDamage);
            AudioManager.PlaySfx(hitImpactClip);

            if (enemyStagger != null)
            {
                int staggerDamage = staggerMultiplier != 1f
                    ? Mathf.RoundToInt(totalDamage * staggerMultiplier)
                    : totalDamage;

                enemyStagger.AddStaggerFromDamage(staggerDamage, enemyHealth.MaxHealth);
            }

            Hitstun enemyHitstun = enemyCollider.GetComponentInParent<Hitstun>();

            if (enemyHitstun != null)
            {
                enemyHitstun.ApplyStun(hitstunDuration);
            }
        }
    }

    private void LootCorpses()
    {
        Collider[] hitCorpses = Physics.OverlapSphere(
            attackPoint.position,
            attackRange,
            corpseLayer
        );

        foreach (Collider corpseCollider in hitCorpses)
        {
            LootableCorpse corpse = corpseCollider.GetComponentInParent<LootableCorpse>();

            if (corpse != null)
            {
                corpse.TryLoot();
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
