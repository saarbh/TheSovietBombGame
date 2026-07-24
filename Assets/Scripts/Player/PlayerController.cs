using System;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Scene facade for the player. Owns the per-frame order of operations for its
/// sub-controllers and is the object interactables are handed when used.
/// </summary>
public class PlayerController : MonoBehaviour
{
    [SerializeField] private PlayerMovement movementSystem;
    [SerializeField] private PlayerInteraction interactionSystem;

    /// <summary>One-way flag set by ControlRoomTrigger. Once departed, never unset.</summary>
    public bool IsControlRoomDeparted { get; private set; }

    public bool IsInputEnabled { get; private set; } = true;

    public PlayerInteraction InteractionSystem => interactionSystem;

    private void Awake()
    {
        if (movementSystem == null)
        {
            movementSystem = GetComponentInChildren<PlayerMovement>();
        }

        if (interactionSystem == null)
        {
            interactionSystem = GetComponentInChildren<PlayerInteraction>();
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
        SetInputEnabled(true);
    }

    public void SetInputEnabled(bool isEnabled)
    {
        IsInputEnabled = isEnabled;

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

    public void OnInteractInput()
    {
        if (!IsInputEnabled)
        {
            return;
        }

        interactionSystem?.ExecuteInteraction(this);
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
