using UnityEngine;

/// <summary>
/// First-person look sub-controller. Yaw turns the player body; pitch tilts the
/// camera and is clamped. Lives on the camera GameObject; input arrives as a
/// parameter so this class stays independent of any input package.
/// </summary>
public class CameraController : MonoBehaviour
{
    [SerializeField] private float mouseSensitivity = 2f;

    [Tooltip("Min/max pitch in degrees (x = look down limit, y = look up limit).")]
    [SerializeField] private Vector2 pitchLimits = new Vector2(-80f, 80f);

    [Tooltip("Transform yawed left/right. Usually the Player root. Falls back to this transform's parent.")]
    [SerializeField] private Transform bodyTransform;

    [SerializeField] private Camera playerCamera;

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

        // Seed from the current pose so the first frame doesn't snap the view.
        yaw = bodyTransform.eulerAngles.y;
        pitch = NormalizePitch(transform.localEulerAngles.x);
    }

    /// <summary>
    /// Applies a mouse/stick look delta. Call once per frame with the raw delta;
    /// sensitivity is applied here.
    /// </summary>
    public void ProcessMouseLook(Vector2 lookInput)
    {
        yaw += lookInput.x * mouseSensitivity;
        pitch -= lookInput.y * mouseSensitivity;
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

        // Kept minimal until the watch view exists - just nudges the pitch down.
        pitch = Mathf.Clamp(45f, pitchLimits.x, pitchLimits.y);
        transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
    }

    private static float NormalizePitch(float rawEulerX)
    {
        // Euler X comes back as 0..360; fold the top half into negatives so clamp works.
        return rawEulerX > 180f ? rawEulerX - 360f : rawEulerX;
    }
}
