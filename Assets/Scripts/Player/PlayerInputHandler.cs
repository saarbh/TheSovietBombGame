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

    [Header("Cursor")]
    [SerializeField] private bool lockCursorOnStart = true;

    [Tooltip("Click in the Game view to take the mouse back after Esc freed it. Without this the "
             + "cursor never re-locks, so OnLook keeps discarding input and the camera stays dead "
             + "until the scene reloads.")]
    [SerializeField] private bool recaptureCursorOnClick = true;

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

    /// <summary>
    /// Takes the mouse back when the player clicks into the game after Esc released it.
    /// Esc is the only way to reach the Editor UI mid-play, and both this handler's
    /// <see cref="OnCancel"/> and the Editor itself free the cursor - but nothing used to
    /// re-lock it, which left <see cref="OnLook"/> permanently ignoring look input.
    /// </summary>
    private void Update()
    {
        if (!recaptureCursorOnClick)
        {
            return;
        }

        // Already ours - nothing to take back.
        if (Cursor.lockState == CursorLockMode.Locked)
        {
            return;
        }

        // A paused menu owns the pointer; stealing it would make the UI unclickable.
        if (Time.timeScale == 0f)
        {
            return;
        }

        // A modal that freezes the player owns the pointer just as much, and the
        // keypad deliberately does not pause - the countdown has to keep running,
        // so the timeScale check above never fires for it. Without this, the first
        // click on a keypad button re-locks and hides the cursor, leaving the player
        // unable to click anything while their input is still disabled.
        if (playerController != null && !playerController.IsInputEnabled)
        {
            return;
        }

        var mouse = Mouse.current;

        if (mouse != null && mouse.leftButton.wasPressedThisFrame)
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
        if (!value.isPressed)
        {
            return;
        }

        // Interact is bound to left click as well as E. While the cursor is free the game
        // does not own the mouse, so the click that takes it back (see Update) must not also
        // press whatever the crosshair happens to be resting on. Mirrors the OnLook guard.
        if (Cursor.lockState != CursorLockMode.Locked)
        {
            return;
        }

        playerController.OnInteractInput();
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
