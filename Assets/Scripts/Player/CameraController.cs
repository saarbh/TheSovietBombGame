using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// First-person look sub-controller. Yaw turns the player body; pitch tilts the
/// camera and is clamped. Mouse look arrives as a per-frame delta and is applied
/// directly; gamepad look is polled here each frame and scaled by delta time,
/// because the stick reports a position (a turn velocity), not a delta.
/// </summary>
public class CameraController : MonoBehaviour
{
    [Header("Mouse")]
    [SerializeField] private float mouseSensitivity = 2f;

    [Header("Gamepad")]
    [Tooltip("Right-stick look speed in degrees per second.")]
    [SerializeField] private float gamepadLookSpeed = 220f;

    [Tooltip("Right-stick magnitude below this is ignored (dead zone).")]
    [SerializeField, Range(0f, 0.9f)] private float stickDeadzone = 0.15f;

    [Header("Limits")]
    [Tooltip("Min/max pitch in degrees (x = look down limit, y = look up limit).")]
    [SerializeField] private Vector2 pitchLimits = new Vector2(-80f, 80f);

    [Tooltip("Transform yawed left/right. Usually the Player root. Falls back to this transform's parent.")]
    [SerializeField] private Transform bodyTransform;

    [SerializeField] private Camera playerCamera;

    /// <summary>When false, gamepad look polling is skipped (e.g. while a modal is open).</summary>
    public bool LookEnabled { get; set; } = true;

    private float yaw;
    private float pitch;

    private void Awake()
    {
        if (playerCamera == null)
        {
            playerCamera = GetComponent<Camera>();
        }

        if (bodyTransform == null)
        {
            bodyTransform = transform.parent != null ? transform.parent : transform;
        }

        yaw = bodyTransform.eulerAngles.y;
        pitch = NormalizePitch(transform.localEulerAngles.x);
    }

    private void Update()
    {
        if (!LookEnabled)
        {
            return;
        }

        var gamepad = Gamepad.current;
        if (gamepad == null)
        {
            return;
        }

        var stick = gamepad.rightStick.ReadValue();
        var magnitude = stick.magnitude;
        if (magnitude < stickDeadzone)
        {
            return;
        }

        // Rescale so motion ramps from zero at the dead-zone edge (no snap), then
        // square it for finer control near centre. This is a velocity, so it is
        // multiplied by delta time.
        var direction = stick / magnitude;
        var scaled = (magnitude - stickDeadzone) / (1f - stickDeadzone);
        var look = direction * (scaled * scaled);
        var step = gamepadLookSpeed * Time.deltaTime;
        ApplyLook(look.x * step, look.y * step);
    }

    /// <summary>
    /// Mouse/pointer look. The input is already a per-frame delta, so it is applied
    /// directly (no delta-time scaling).
    /// </summary>
    public void ProcessMouseLook(Vector2 lookInput)
    {
        ApplyLook(lookInput.x * mouseSensitivity, lookInput.y * mouseSensitivity);
    }

    private void ApplyLook(float deltaYaw, float deltaPitch)
    {
        yaw += deltaYaw;
        pitch -= deltaPitch;
        pitch = Mathf.Clamp(pitch, pitchLimits.x, pitchLimits.y);

        bodyTransform.rotation = Quaternion.Euler(0f, yaw, 0f);
        transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
    }

    /// <summary>
    /// Placeholder for the wrist-watch pose (Module 2). Tilts the view down toward
    /// the wrist when raised; returns to neutral otherwise.
    /// </summary>
    public void SetWatchViewPose(bool isWatching)
    {
        if (!isWatching)
        {
            return;
        }

        pitch = Mathf.Clamp(45f, pitchLimits.x, pitchLimits.y);
        transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
    }

    private static float NormalizePitch(float rawEulerX)
    {
        // Euler X comes back as 0..360; fold the top half into negatives so clamp works.
        return rawEulerX > 180f ? rawEulerX - 360f : rawEulerX;
    }
}
