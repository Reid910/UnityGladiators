using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float movementSpeed = 5f;
    [SerializeField] private float rotationSpeed = 12f;
    [SerializeField] private float gravity = -20f;
    [Tooltip("Speed multiplier while holding Sprint. Unlimited — no stamina meter (see docs/combat-redesign-plan.md); Dash's own cooldown already covers 'can't spam mobility forever'.")]
    [SerializeField] private float sprintSpeedMultiplier = 1.6f;

    [Header("Animation")]
    [SerializeField] private float animationBlendSpeed = 10f;

    [Header("References")]
    [SerializeField] private Transform cameraTransform;
    [SerializeField] private Animator animator;
    [SerializeField] private Health health;
    [SerializeField] private Stagger stagger;
    [SerializeField] private Hitstun hitstun;
    [SerializeField] private PlayerStats playerStats;
    [SerializeField] private PlayerCombat playerCombat;

    private CharacterController characterController;
    private InputSystem_Actions inputSystemActions;

    private Vector2 movementInput;
    private Vector3 verticalVelocity;

    private float currentMoveX;
    private float currentMoveZ;

    // Camera-relative world-space movement direction from raw input, zero
    // when not moving. Exposed so PlayerCombat's dash can fire in the
    // direction the player is actually pressing instead of always
    // transform.forward (see docs/combat-redesign-plan.md) — transform.forward
    // lags behind input during quick turns since rotation is smoothed
    // (rotationSpeed), but a dash should go where you're pressing right now.
    public Vector3 MovementDirection { get; private set; }

    // True while Sprint is held and the player is actually moving — exposed
    // for PlayerCombat's dash-while-sprinting-triggers-a-Slide and
    // sprint-attack behaviors (see docs/combat-redesign-plan.md).
    public bool IsSprinting { get; private set; }

    private float momentumMultiplier = 1f;
    private float momentumUntilTime;

    // Sprint's Any-State animator transition (no exit time, no interruption
    // source) fires the instant IsSprinting reads true, which hijacked the
    // Slide clip mid-play since holding Sprint is exactly what triggers a
    // Slide. A fixed suppression timer isn't reliable here — it has to
    // outlast whatever the Slide clip's actual length is, which can drift
    // out of sync with the gameplay slideDuration tuning value. Checking
    // the Animator's real current/in-progress state instead ties the mask
    // exactly to how long the clip is actually playing, with no duration
    // number to keep in sync. See docs/combat-redesign-plan.md.
    private static readonly int SlideStateHash = Animator.StringToHash("Slide");

    private bool IsPlayingSlideAnimation =>
        animator != null &&
        (animator.GetCurrentAnimatorStateInfo(0).shortNameHash == SlideStateHash ||
         animator.GetNextAnimatorStateInfo(0).shortNameHash == SlideStateHash);

    // Called by PlayerCombat when a Slide ends — a brief residual speed
    // boost is the actual ingredient that makes chaining moves (slide into
    // another dash, into an attack) feel fast instead of the slide just
    // being an animation. See docs/combat-redesign-plan.md.
    public void ApplyMomentumBoost(float multiplier, float duration)
    {
        momentumMultiplier = multiplier;
        momentumUntilTime = Time.time + duration;
    }

    // Movement is locked while stunned from a hit, broken from stagger, or
    // dead — mirrors the same restriction EnemyController applies to
    // enemies. Also locked for any committed action (see
    // PlayerCombat.IsActionLocked — every attack roots the player in place
    // now, not just Finishers) and while holding Block. A dodge-out/sprint
    // attack's initial lunge still moves the player during this window
    // since it drives CharacterController.Move() directly from PlayerCombat,
    // bypassing this script entirely — only manual WASD input is locked.
    private bool IsIncapacitated =>
        (health != null && health.IsDead) ||
        (hitstun != null && hitstun.IsStunned) ||
        (stagger != null && stagger.IsBroken) ||
        (playerCombat != null && playerCombat.IsActionLocked) ||
        (playerCombat != null && playerCombat.IsBlocking);

    private void Awake()
    {
        characterController = GetComponent<CharacterController>();

        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }

        if (health == null)
        {
            health = GetComponent<Health>();
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

        if (playerCombat == null)
        {
            playerCombat = GetComponent<PlayerCombat>();
        }

        if (Camera.main != null)
        {
            cameraTransform = Camera.main.transform;
        }

        inputSystemActions = new InputSystem_Actions();
    }

    private void OnEnable()
    {
        inputSystemActions.Player.Enable();
    }

    private void OnDisable()
    {
        inputSystemActions.Player.Disable();
    }

    // See PlayerCombat.SetGameplayInputEnabled — same reasoning, called by
    // ArenaMenuController to suspend/restore input without disabling this
    // whole component.
    public void SetGameplayInputEnabled(bool isEnabled)
    {
        if (isEnabled)
        {
            inputSystemActions.Player.Enable();
        }
        else
        {
            inputSystemActions.Player.Disable();
        }
    }

    private void Update()
    {
        movementInput = IsIncapacitated
            ? Vector2.zero
            : inputSystemActions.Player.Move.ReadValue<Vector2>();

        if (!IsIncapacitated)
        {
            HandleMovement();
        }
        else
        {
            MovementDirection = Vector3.zero;
            IsSprinting = false;
        }

        ApplyGravity();
        UpdateAnimation();
    }

    private void HandleMovement()
    {
        Vector3 inputDirection = new Vector3(
            movementInput.x,
            0f,
            movementInput.y
        );

        if (inputDirection.sqrMagnitude > 0.01f)
        {
            Vector3 cameraForward = cameraTransform.forward;
            Vector3 cameraRight = cameraTransform.right;

            cameraForward.y = 0f;
            cameraRight.y = 0f;

            cameraForward.Normalize();
            cameraRight.Normalize();

            Vector3 movementDirection =
                cameraForward * inputDirection.z +
                cameraRight * inputDirection.x;

            // Normalize movement so diagonal movement is not faster.
            movementDirection.Normalize();
            MovementDirection = movementDirection;
            IsSprinting = inputSystemActions.Player.Sprint.IsPressed();

            // Move Speed affix (Pants-flavored, see TODO.md) is a fractional
            // bonus on top of the base speed.
            float moveSpeedMultiplier = 1f + (playerStats != null ? playerStats.GetStat(StatType.MoveSpeed) : 0f);

            if (IsSprinting)
            {
                moveSpeedMultiplier *= sprintSpeedMultiplier;
            }

            if (Time.time < momentumUntilTime)
            {
                moveSpeedMultiplier *= momentumMultiplier;
            }

            characterController.Move(
                movementDirection * movementSpeed * moveSpeedMultiplier * Time.deltaTime
            );

            Quaternion targetRotation = Quaternion.LookRotation(movementDirection);

            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                targetRotation,
                rotationSpeed * Time.deltaTime
            );
        }
        else
        {
            MovementDirection = Vector3.zero;
            IsSprinting = false;
        }
    }

    private void ApplyGravity()
    {
        if (characterController.isGrounded && verticalVelocity.y < 0f)
        {
            verticalVelocity.y = -2f;
        }

        verticalVelocity.y += gravity * Time.deltaTime;

        characterController.Move(verticalVelocity * Time.deltaTime);
    }

    private void UpdateAnimation()
    {
        if (animator == null)
        {
            return;
        }

        // Do NOT normalize these. This allows W + D to become MoveX = 1 and MoveZ = 1.
        float targetMoveX = movementInput.x;
        float targetMoveZ = movementInput.y;

        currentMoveX = Mathf.Lerp(
            currentMoveX,
            targetMoveX,
            animationBlendSpeed * Time.deltaTime
        );

        currentMoveZ = Mathf.Lerp(
            currentMoveZ,
            targetMoveZ,
            animationBlendSpeed * Time.deltaTime
        );

        animator.SetFloat("MoveX", currentMoveX);
        animator.SetFloat("MoveZ", currentMoveZ);
        animator.SetFloat("Speed", movementInput.magnitude);
        animator.SetBool("IsMoving", movementInput.sqrMagnitude > 0.01f);

        // Forward-only Sprint clip is a clean fit here, not a directional
        // compromise — the character already always rotates to face
        // MovementDirection above regardless of sprint state, so there's
        // never actually a "strafing while sprinting" case to represent.
        bool showSprintAnimation = IsSprinting && !IsPlayingSlideAnimation;
        animator.SetBool("IsSprinting", showSprintAnimation);
    }
}