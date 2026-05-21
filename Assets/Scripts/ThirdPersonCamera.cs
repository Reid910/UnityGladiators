using UnityEngine;

public class ThirdPersonCamera : MonoBehaviour
{
    [SerializeField] private Transform target;
    [SerializeField] private Vector3 offset = new Vector3(0f, 3f, -5f);
    [SerializeField] private float followSpeed = 10f;
    [SerializeField] private float mouseSensitivity = 2f;

    private float yaw;
    private float pitch = 20f;

    private void LateUpdate()
    {
        if (target == null)
        {
            return;
        }

        yaw += Input.GetAxis("Mouse X") * mouseSensitivity;
        pitch -= Input.GetAxis("Mouse Y") * mouseSensitivity;
        pitch = Mathf.Clamp(pitch, -20f, 60f);

        Quaternion cameraRotation = Quaternion.Euler(pitch, yaw, 0f);
        Vector3 desiredPosition = target.position + cameraRotation * offset;

        transform.position = Vector3.Lerp(transform.position, desiredPosition, followSpeed * Time.deltaTime);
        transform.LookAt(target.position + Vector3.up * 1.4f);
    }
}