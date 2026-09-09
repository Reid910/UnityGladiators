using UnityEngine;
using UnityEngine.InputSystem;

public class ThirdPersonCamera : MonoBehaviour
{
    [SerializeField] private Transform target;
    [SerializeField] private Vector3 offset = new Vector3(0f, 3f, -5f);
    [SerializeField] private float followSpeed = 10f;
    [Tooltip("Raw pointer delta (pixels/frame) is a very different scale than the old Input Manager's smoothed Mouse X/Y axis — start low and tune from here.")]
    [SerializeField] private float mouseSensitivity = 0.12f;

    private InputSystem_Actions inputSystemActions;

    private float yaw;
    private float pitch = 20f;

    private void Awake()
    {
        inputSystemActions = new InputSystem_Actions();
    }

    private void OnEnable()
    {
        inputSystemActions.Player.Enable();
        LockCursor();
    }

    private void OnDisable()
    {
        inputSystemActions.Player.Disable();
        UnlockCursor();
    }

    private void LateUpdate()
    {
        if (target == null)
        {
            return;
        }

        bool unlockHeld = Keyboard.current != null &&
            (Keyboard.current.leftAltKey.isPressed || Keyboard.current.rightAltKey.isPressed);

        if (unlockHeld)
        {
            UnlockCursor();
        }
        else
        {
            LockCursor();

            Vector2 lookInput = inputSystemActions.Player.Look.ReadValue<Vector2>();
            yaw += lookInput.x * mouseSensitivity;
            pitch -= lookInput.y * mouseSensitivity;
            pitch = Mathf.Clamp(pitch, -20f, 60f);
        }

        Quaternion cameraRotation = Quaternion.Euler(pitch, yaw, 0f);
        Vector3 desiredPosition = target.position + cameraRotation * offset;

        transform.position = Vector3.Lerp(transform.position, desiredPosition, followSpeed * Time.deltaTime);
        transform.LookAt(target.position + Vector3.up * 1.4f);
    }

    // Holding Alt frees the cursor (e.g. to click off the game window) without
    // needing a dedicated UI/pause state yet.
    private static void LockCursor()
    {
        if (Cursor.lockState != CursorLockMode.Locked)
        {
            Cursor.lockState = CursorLockMode.Locked;
        }

        if (Cursor.visible)
        {
            Cursor.visible = false;
        }
    }

    private static void UnlockCursor()
    {
        if (Cursor.lockState != CursorLockMode.None)
        {
            Cursor.lockState = CursorLockMode.None;
        }

        if (!Cursor.visible)
        {
            Cursor.visible = true;
        }
    }
}
