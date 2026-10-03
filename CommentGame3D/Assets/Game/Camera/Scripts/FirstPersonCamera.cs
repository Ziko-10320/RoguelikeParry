using UnityEngine;
using UnityEngine.InputSystem;

public class FirstPersonCamera : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private Transform target;                                  // the bean
    [SerializeField] private Vector3 eyeOffset = new Vector3(0f, 0.7f, 0.15f);  // local to the bean

    [Header("Input")]
    [SerializeField] private InputActionReference lookAction;

    [Header("Look")]
    [SerializeField] private float mouseSensitivity = 0.12f;
    [SerializeField] private float minPitch = -85f;
    [SerializeField] private float maxPitch = 85f;
    [SerializeField] private bool lockCursor = true;

    private float yaw;
    private float pitch;

    [Header("Wall Run Tilt")]
    [SerializeField] private float wallRunTilt = 12f;
    [SerializeField] private float tiltSpeed = 8f;

    public PlayerMovement player;
    private float currentTilt;

    private void OnEnable() => lookAction.action.Enable();
    private void OnDisable() => lookAction.action.Disable();

    private void Start()
    {
        if (target == null)
        {
            Debug.LogWarning("FirstPersonCamera: no target assigned.");
            enabled = false;
            return;
        }

        yaw = target.eulerAngles.y;

        if (lockCursor)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        player = target.GetComponentInParent<PlayerMovement>();
        if (player == null) player = target.GetComponentInChildren<PlayerMovement>();
        if (player == null) player = FindFirstObjectByType<PlayerMovement>();
        if (player == null) Debug.LogWarning("FirstPersonCamera: PlayerMovement not found, no wall tilt.");
    }

    private void Update()
    {
        // Esc frees the mouse, click locks it again
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        else if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame && lockCursor)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    private void LateUpdate()
    {
        if (target == null) return;

        if (Cursor.lockState == CursorLockMode.Locked)
        {
            Vector2 look = lookAction.action.ReadValue<Vector2>();
            yaw += look.x * mouseSensitivity;
            pitch -= look.y * mouseSensitivity;
            pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
        }

        // Camera sits at the bean's eyes, rotated by yaw/pitch
        Quaternion yawRot = Quaternion.Euler(0f, yaw, 0f);
        Vector3 eyePos = target.position + yawRot * eyeOffset;

        // lean away from the wall
        float targetTilt = player != null ? player.WallRunSide * wallRunTilt : 0f;   // left = -1 -> negative Z
        currentTilt = Mathf.Lerp(currentTilt, targetTilt, 1f - Mathf.Exp(-tiltSpeed * Time.deltaTime));

        transform.SetPositionAndRotation(eyePos, Quaternion.Euler(pitch, yaw, currentTilt));
    }
}