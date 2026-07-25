using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Bridges Unity's <c>PlayerInput</c> component (New Input System, "Send Messages"
/// behavior) to <see cref="PlayerController"/>. PlayerInput calls the On&lt;Action&gt;
/// methods here by name; this relay converts them into the controller's input API.
///
/// Expects a PlayerInput on the same GameObject using the "Player" action map with
/// Move, Look, Interact, and Watch actions.
/// </summary>
[RequireComponent(typeof(PlayerController))]
public class PlayerInputHandler : MonoBehaviour
{
    [SerializeField] private PlayerController playerController;
    [SerializeField] private bool lockCursorOnStart = true;

    private void Awake()
    {
        if (playerController == null)
        {
            playerController = GetComponent<PlayerController>();
        }
    }

    private void Start()
    {
        if (lockCursorOnStart)
        {
            SetCursorLocked(true);
        }
    }

    // --- PlayerInput "Send Messages" callbacks (one per action in the Player map) ---

    public void OnMove(InputValue value)
    {
        playerController.OnMoveInput(value.Get<Vector2>());
    }

    public void OnLook(InputValue value)
    {
        // Ignore look while the cursor is free (e.g. after Esc) so the view doesn't drift.
        if (Cursor.lockState == CursorLockMode.Locked)
        {
            playerController.OnLookInput(value.Get<Vector2>());
        }
    }

    public void OnInteract(InputValue value)
    {
        if (value.isPressed)
        {
            playerController.OnInteractInput();
        }
    }

    public void OnWatch(InputValue value)
    {
        if (value.isPressed)
        {
            playerController.OnWatchInput();
        }
    }

    // Bound to the UI/Cancel action name as well; harmless if unused.
    public void OnCancel(InputValue value)
    {
        if (value.isPressed)
        {
            SetCursorLocked(false);
        }
    }

    private static void SetCursorLocked(bool locked)
    {
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }
}
