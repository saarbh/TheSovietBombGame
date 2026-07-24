using System;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Throwaway first-person rig for the generator room test scene.
///
/// This exists only because the canonical player controller is not wired to input yet
/// (PlayerController.OnMoveInput has no caller, and CameraController is still a TODO).
/// It deliberately lives outside Assets/Scripts/Player so it cannot collide with that
/// work, and it raycasts for the shared <see cref="IInteractable"/> so every puzzle prop
/// built against it keeps working once the real controller lands.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class GeneratorRoomRig : MonoBehaviour
{
    [Header("Input")]
    [Tooltip("Assets/InputSystem_Actions.inputactions - do not author a second asset.")]
    [SerializeField] private InputActionAsset inputActions;

    [SerializeField] private string actionMapName = "Player";

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 3.5f;
    [SerializeField] private float gravity = 9.81f;
    [SerializeField] private float groundedStickVelocity = 2f;

    [Header("Look")]
    [SerializeField] private Transform cameraPivot;
    [SerializeField] private float lookSensitivity = 0.12f;
    [SerializeField] private Vector2 pitchLimits = new Vector2(-80f, 80f);
    [SerializeField] private bool lockCursor = true;

    [Header("Interaction")]
    [SerializeField] private float interactDistance = 3f;

    [Tooltip("Set this to the interactable layer. Leaving it as Everything makes the ray stop on walls.")]
    [SerializeField] private LayerMask interactableMask = ~0;

    [Header("Debug")]
    [Tooltip("Logs each time the targeted interactable changes. Useful while wiring the room.")]
    [SerializeField] private bool logTargetChanges;

    private CharacterController characterController;
    private PlayerController playerController;

    private InputActionMap playerMap;
    private InputAction moveAction;
    private InputAction lookAction;
    private InputAction interactAction;

    private IInteractable currentTarget;
    private float verticalVelocity;
    private float pitch;

    /// <summary>The interactable under the crosshair, or null.</summary>
    public IInteractable CurrentTarget => currentTarget;

    /// <summary>Fires only when the target actually changes, including to null.</summary>
    public event Action<IInteractable> OnTargetChanged;

    private void Awake()
    {
        characterController = GetComponent<CharacterController>();
        playerController = GetComponent<PlayerController>();

        if (cameraPivot == null && Camera.main != null)
        {
            cameraPivot = Camera.main.transform;
        }

        ResolveActions();
    }

    private void OnEnable()
    {
        playerMap?.Enable();

        if (lockCursor)
        {
            SetCursorLocked(true);
        }
    }

    private void OnDisable()
    {
        playerMap?.Disable();

        if (lockCursor)
        {
            SetCursorLocked(false);
        }
    }

    private void Update()
    {
        // A modal or pause owns input while the game is stopped.
        if (Time.timeScale == 0f)
        {
            return;
        }

        ProcessLook();
        ProcessMovement();
        DetectInteractable();

        if (interactAction != null && interactAction.WasPressedThisFrame())
        {
            currentTarget?.Interact(playerController);
        }
    }

    private void ResolveActions()
    {
        if (inputActions == null)
        {
            Debug.LogError($"[{nameof(GeneratorRoomRig)}] No InputActionAsset assigned - assign Assets/InputSystem_Actions.inputactions.", this);
            return;
        }

        playerMap = inputActions.FindActionMap(actionMapName, throwIfNotFound: false);

        if (playerMap == null)
        {
            Debug.LogError($"[{nameof(GeneratorRoomRig)}] Action map '{actionMapName}' not found on {inputActions.name}.", this);
            return;
        }

        moveAction = playerMap.FindAction("Move", throwIfNotFound: false);
        lookAction = playerMap.FindAction("Look", throwIfNotFound: false);
        interactAction = playerMap.FindAction("Interact", throwIfNotFound: false);
    }

    private void ProcessLook()
    {
        if (lookAction == null || cameraPivot == null)
        {
            return;
        }

        var lookInput = lookAction.ReadValue<Vector2>() * lookSensitivity;

        // Yaw turns the whole body so movement follows the camera; pitch is camera-only.
        transform.Rotate(Vector3.up, lookInput.x, Space.Self);

        pitch = Mathf.Clamp(pitch - lookInput.y, pitchLimits.x, pitchLimits.y);
        cameraPivot.localRotation = Quaternion.Euler(pitch, 0f, 0f);
    }

    private void ProcessMovement()
    {
        if (!characterController.enabled)
        {
            return;
        }

        if (characterController.isGrounded && verticalVelocity < 0f)
        {
            // Reset rather than accumulate, or standing still builds a huge downward
            // velocity that launches the player off the next ledge.
            verticalVelocity = -groundedStickVelocity;
        }
        else
        {
            verticalVelocity -= gravity * Time.deltaTime;
        }

        var moveInput = moveAction != null ? moveAction.ReadValue<Vector2>() : Vector2.zero;
        moveInput = Vector2.ClampMagnitude(moveInput, 1f);

        var planar = (transform.right * moveInput.x) + (transform.forward * moveInput.y);
        var velocity = (planar * moveSpeed) + (Vector3.up * verticalVelocity);

        characterController.Move(velocity * Time.deltaTime);
    }

    private void DetectInteractable()
    {
        var found = Raycast();

        if (ReferenceEquals(found, currentTarget))
        {
            return;
        }

        currentTarget = found;
        OnTargetChanged?.Invoke(currentTarget);

        if (logTargetChanges)
        {
            Debug.Log($"[{nameof(GeneratorRoomRig)}] Target: {(currentTarget == null ? "none" : currentTarget.GetPrompt())}");
        }
    }

    private IInteractable Raycast()
    {
        if (cameraPivot == null)
        {
            return null;
        }

        if (!Physics.Raycast(
                cameraPivot.position,
                cameraPivot.forward,
                out var hit,
                interactDistance,
                interactableMask,
                QueryTriggerInteraction.Collide))
        {
            return null;
        }

        // GetComponentInParent so a collider on a child mesh still resolves to the
        // interactable on the prop root.
        return hit.collider.GetComponentInParent<IInteractable>();
    }

    private static void SetCursorLocked(bool isLocked)
    {
        Cursor.lockState = isLocked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !isLocked;
    }

    private void OnDrawGizmosSelected()
    {
        var origin = cameraPivot != null ? cameraPivot : transform;
        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(origin.position, origin.position + (origin.forward * interactDistance));
    }
}
