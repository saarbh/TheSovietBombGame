using System;
using UnityEngine;

/// <summary>
/// Scene facade for the player. Owns the per-frame order of operations for its
/// sub-controllers and is the object interactables are handed when used.
/// </summary>
public class PlayerController : MonoBehaviour
{

    [SerializeField] private PlayerMovement movementSystem;
    [SerializeField] private CameraController cameraSystem;
    [SerializeField] private PlayerInteraction interactionSystem;
    [SerializeField] private PlayerAnimationController animationController;

    [Header("Time Skip")]
    [Tooltip("Countdown multiplier while the fast-forward button is held. Doors unlock on "
             + "elapsed time, so this is how the player skips the wait instead of standing still.")]
    [SerializeField] private float fastForwardTimeScale = 4f;

    /// <summary>One-way flag set by ControlRoomTrigger. Once departed, never unset.</summary>
    public bool IsControlRoomDeparted { get; private set; }

    public bool IsInputEnabled { get; private set; } = true;

    public PlayerInteraction InteractionSystem => interactionSystem;

    /// <summary>True while the player is holding fast-forward and the clock is running fast.</summary>
    public bool IsFastForwarding { get; private set; }

    /// <summary>
    /// Raised when fast-forward starts or stops. The HUD clock and the cigarette smoke
    /// both hang off this rather than polling the watch.
    /// </summary>
    public event Action<bool> OnFastForwardChanged;

    private void Awake()
    {
        if (movementSystem == null)
        {
            movementSystem = GetComponentInChildren<PlayerMovement>();
        }

        if (cameraSystem == null)
        {
            cameraSystem = GetComponentInChildren<CameraController>();
        }

        if (interactionSystem == null)
        {
            interactionSystem = GetComponentInChildren<PlayerInteraction>();
        }

        if (animationController == null)
        {
            animationController = GetComponentInChildren<PlayerAnimationController>();
        }
    }

    private void Start()
    {
        InitializePlayer();
    }

    private void Update()
    {
        if (!IsInputEnabled)
        {
            return;
        }

        // Gravity runs even with no move input, so it is not gated on input axes.
        movementSystem?.ApplyGravity();
        interactionSystem?.DetectInteractable();
    }

    public void InitializePlayer()
    {
        IsControlRoomDeparted = false;
        SetFastForwarding(false);
        SetInputEnabled(true);
    }

    public void SetInputEnabled(bool isEnabled)
    {
        IsInputEnabled = isEnabled;

        // Gamepad look is polled inside CameraController, so it must be gated here
        // too - otherwise the right stick could still turn the view while frozen.
        if (cameraSystem != null)
        {
            cameraSystem.LookEnabled = isEnabled;
        }

        if (isEnabled)
        {
            return;
        }

        // Drop queued motion and any highlighted target so nothing is left
        // half-applied while a modal (keypad, phone, pause) is open.
        movementSystem?.ResetMotion();
        interactionSystem?.ClearTarget();

        // The keypad deliberately does not pause the countdown, so a hold left
        // running here would keep burning the clock at 4x behind a modal.
        SetFastForwarding(false);
    }

    /// <summary>Feed the move axis. Hook to your input callback.</summary>
    public void OnMoveInput(Vector2 moveInput)
    {
        if (!IsInputEnabled)
        {
            return;
        }

        movementSystem?.ProcessMovement(moveInput);
    }

    /// <summary>Feed the look delta (mouse/stick). Hook to your input callback.</summary>
    public void OnLookInput(Vector2 lookInput)
    {
        if (!IsInputEnabled)
        {
            return;
        }

        cameraSystem?.ProcessMouseLook(lookInput);
    }

    public void OnInteractInput()
    {
        if (!IsInputEnabled)
        {
            return;
        }

        // Fire the "Taking Item" animation only when actually aimed at an interactable,
        // so pressing Interact at empty space doesn't play the pickup. CurrentTarget is a
        // pure C# interface reference, so != null is a plain identity check.
        if (interactionSystem != null && interactionSystem.CurrentTarget != null && animationController != null)
        {
            animationController.TriggerInteract();
        }

        interactionSystem?.ExecuteInteraction(this);
    }

    /// <summary>
    /// Hold-to-fast-forward. Called on both press and release, so a release is never
    /// swallowed: if input has since been disabled the hold is force-ended rather than
    /// early-returned, which would otherwise strand the clock at 4x with no way to stop it.
    /// </summary>
    public void OnFastForwardInput(bool isHeld)
    {
        SetFastForwarding(isHeld && IsInputEnabled);
    }

    private void SetFastForwarding(bool isFastForwarding)
    {
        if (IsFastForwarding == isFastForwarding)
        {
            return;
        }

        IsFastForwarding = isFastForwarding;

        // GameManager is a scene singleton and may legitimately be absent in the
        // single-room test scenes, where fast-forward is simply a no-op.
        if (GameManager.Instance != null && GameManager.Instance.WatchManager is not null)
        {
            GameManager.Instance.WatchManager.TimeScale = isFastForwarding ? fastForwardTimeScale : 1f;
        }

        OnFastForwardChanged?.Invoke(isFastForwarding);
    }

    /// <summary>Called by ControlRoomTrigger on exit. Idempotent.</summary>
    public void MarkControlRoomDeparted()
    {
        if (IsControlRoomDeparted)
        {
            return;
        }

        IsControlRoomDeparted = true;

        if (GameManager.Instance != null)
        {
            GameManager.Instance.MarkControlRoomDeparted();
        }
    }
}
