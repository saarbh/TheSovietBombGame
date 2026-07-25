using UnityEngine;

/// <summary>
/// Sub-controller: translates a 2D move axis into <see cref="CharacterController"/> motion
/// and owns vertical velocity. Input arrives as a parameter, so this class never touches
/// an input package.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 3.5f;

    [Tooltip("Downward acceleration in m/s^2. Positive value, applied as negative.")]
    [SerializeField] private float gravity = 9.81f;

    [Tooltip("Small downward force kept while grounded so the controller stays glued to slopes.")]
    [SerializeField] private float groundedStickVelocity = 2f;

    [SerializeField] private CharacterController characterController;

    private float verticalVelocity;
    private Vector2 currentMoveInput;

    public bool IsGrounded => characterController != null && characterController.isGrounded;

    /// <summary>Planar speed this frame, for footstep audio and head bob.</summary>
    public float CurrentPlanarSpeed { get; private set; }

    private void Awake()
    {
        if (characterController == null)
        {
            characterController = GetComponent<CharacterController>();
        }
    }

    /// <summary>
    /// Records the desired move axis. Call from the input callback; the actual
    /// translation happens in <see cref="ApplyGravity"/>'s frame step so movement
    /// and gravity resolve in a single <c>CharacterController.Move</c>.
    /// </summary>
    public void ProcessMovement(Vector2 moveInput)
    {
        currentMoveInput = Vector2.ClampMagnitude(moveInput, 1f);
    }

    /// <summary>
    /// Integrates gravity and moves the controller. Call once per frame from the
    /// owning <see cref="PlayerController"/>.
    /// </summary>
    public void ApplyGravity()
    {
        if (characterController == null || !characterController.enabled)
        {
            return;
        }

        if (characterController.isGrounded && verticalVelocity < 0f)
        {
            // Reset rather than accumulate, otherwise standing still builds up a
            // huge downward velocity that launches the player off the next ledge.
            verticalVelocity = -groundedStickVelocity;
        }
        else
        {
            verticalVelocity -= gravity * Time.deltaTime;
        }

        var planar = (transform.right * currentMoveInput.x) + (transform.forward * currentMoveInput.y);
        var velocity = (planar * moveSpeed) + (Vector3.up * verticalVelocity);

        characterController.Move(velocity * Time.deltaTime);
        CurrentPlanarSpeed = planar.magnitude * moveSpeed;
    }

    /// <summary>Zeroes queued input, e.g. when input is disabled mid-stride.</summary>
    public void ResetMotion()
    {
        currentMoveInput = Vector2.zero;
        CurrentPlanarSpeed = 0f;
    }

    private GameObject lastHitObject;

    private void OnControllerColliderHit(ControllerColliderHit hit)
    {
        if (hit.collider == null)
        {
            return;
        }

        // Ignore floor hits to avoid spamming the console
        if (hit.normal.y > 0.7f || hit.gameObject.name.StartsWith("Floor"))
        {
            return;
        }

        if (hit.gameObject != lastHitObject)
        {
            lastHitObject = hit.gameObject;
            var parentName = hit.transform.parent != null ? hit.transform.parent.name : "None";
            Debug.Log($"[PLAYER COLLISION DETECTED] Hit Object: '{hit.gameObject.name}' | Parent: '{parentName}' | Layer: '{LayerMask.LayerToName(hit.gameObject.layer)}' | Hit Point: {hit.point}", hit.gameObject);
        }
    }
}
