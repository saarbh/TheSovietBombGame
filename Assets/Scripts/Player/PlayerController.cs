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

    // TODO: WorldWatchView is not written yet (Module 2).
    // OnWatchInput raises OnWatchToggled so the watch view can subscribe once it exists.

    /// <summary>One-way flag set by ControlRoomTrigger. Once departed, never unset.</summary>
    public bool IsControlRoomDeparted { get; private set; }

    public bool IsInputEnabled { get; private set; } = true;

    public PlayerInteraction InteractionSystem => interactionSystem;

    /// <summary>Raised when the player raises/lowers the wrist watch.</summary>
    public event Action<bool> OnWatchToggled;

    private bool isWatchRaised;

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
        isWatchRaised = false;
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

    public void OnWatchInput()
    {
        if (!IsInputEnabled)
        {
            return;
        }

        isWatchRaised = !isWatchRaised;
        cameraSystem?.SetWatchViewPose(isWatchRaised);
        OnWatchToggled?.Invoke(isWatchRaised);
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
