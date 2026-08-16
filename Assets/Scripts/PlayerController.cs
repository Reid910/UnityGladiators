using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float movementSpeed = 5f;
    [SerializeField] private float rotationSpeed = 12f;
    [SerializeField] private float gravity = -20f;

    [Header("Animation")]
    [SerializeField] private float animationBlendSpeed = 10f;

    [Header("References")]
    [SerializeField] private Transform cameraTransform;
    [SerializeField] private Animator animator;
    [SerializeField] private Health health;
    [SerializeField] private Stagger stagger;
    [SerializeField] private Hitstun hitstun;
    [SerializeField] private PlayerStats playerStats;

    private CharacterController characterController;
    private InputSystem_Actions inputSystemActions;

    private Vector2 movementInput;
    private Vector3 verticalVelocity;

    private float currentMoveX;
    private float currentMoveZ;

    // Movement is locked while stunned from a hit, broken from stagger, or
    // dead — mirrors the same restriction EnemyController applies to enemies.
    private bool IsIncapacitated =>
        (health != null && health.IsDead) ||
        (hitstun != null && hitstun.IsStunned) ||
        (stagger != null && stagger.IsBroken);

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

    private void Update()
    {
        movementInput = IsIncapacitated
            ? Vector2.zero
            : inputSystemActions.Player.Move.ReadValue<Vector2>();

        if (!IsIncapacitated)
        {
            HandleMovement();
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

            // Move Speed affix (Pants-flavored, see TODO.md) is a fractional
            // bonus on top of the base speed.
            float moveSpeedMultiplier = 1f + (playerStats != null ? playerStats.GetStat(StatType.MoveSpeed) : 0f);

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
    }
}