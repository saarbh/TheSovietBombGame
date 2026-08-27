using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

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

        // Never recapture cursor in menu scenes
        var activeScene = SceneManager.GetActiveScene();
        if (activeScene.name.Equals("MainMenuScene", System.StringComparison.OrdinalIgnoreCase))
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
            Debug.Log($"[{nameof(PlayerInputHandler)}] Left mouse click in Update while cursor is unlocked (TimeScale: {Time.timeScale}, IsInputEnabled: {playerController?.IsInputEnabled}). Locking cursor.");
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
        Debug.Log($"[{nameof(PlayerInputHandler)}] OnInteract received (isPressed: {value.isPressed}, cursorLock: {Cursor.lockState}).");

        if (!value.isPressed)
        {
            return;
        }

        // Interact is bound to left click as well as E. While the cursor is free the game
        // does not own the mouse, so the click that takes it back (see Update) must not also
        // press whatever the crosshair happens to be resting on. Mirrors the OnLook guard.
        if (Cursor.lockState != CursorLockMode.Locked)
        {
            Debug.Log($"[{nameof(PlayerInputHandler)}] OnInteract discarded because cursor is not locked.");
            return;
        }

        playerController.OnInteractInput();
    }

    /// <summary>
    /// Hold-to-fast-forward, on the existing "Watch" action (Q / gamepad north). The action
    /// is a plain Button with no interactions, so PlayerInput sends this on both performed
    /// and canceled - the release must be forwarded, not filtered out like the tap actions above.
    /// </summary>
    public void OnWatch(InputValue value)
    {
        playerController.OnFastForwardInput(value.isPressed);
    }

    // Bound to the UI/Cancel action name as well; harmless if unused.
    public void OnCancel(InputValue value)
    {
        Debug.Log($"[{nameof(PlayerInputHandler)}] OnCancel received (isPressed: {value.isPressed}).");
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
