using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

/// <summary>Directions used for the arrow-key door combo.</summary>
public enum ComboDirection
{
    Up,
    Down,
    Left,
    Right
}

/// <summary>
/// Arrow-key combination lock for a door. Interact (E) to engage the keypad, then
/// enter the combo with the arrow keys. A correct sequence opens the wired
/// <see cref="RoomDoor"/>; a wrong press resets the entry. Attach to an object with
/// a Collider so the player can aim at and interact with it.
/// </summary>
[RequireComponent(typeof(Collider))]
public class DoorComboLock : MonoBehaviour, IInteractable
{
    [Header("Combo")]
    [Tooltip("Helldivers-style arrow sequence that unlocks the door. A wrong press resets to the start.")]
    [SerializeField]
    private List<ComboDirection> combo = new List<ComboDirection>
    {
        ComboDirection.Up, ComboDirection.Up, ComboDirection.Down, ComboDirection.Down,
        ComboDirection.Left, ComboDirection.Right, ComboDirection.Left, ComboDirection.Right
    };

    [Header("Door")]
    [Tooltip("Door opened when the combo is correct. Optional - onUnlocked still fires without it.")]
    [SerializeField] private RoomDoor door;

    [Header("Behaviour")]
    [Tooltip("Freeze player movement/look while entering the combo so the arrow keys don't move the player.")]
    [SerializeField] private bool freezePlayerWhileEntering = true;

    [Header("Prompts")]
    [SerializeField] private string lockedPrompt = "[E] Use Keypad";
    [SerializeField] private string enteringPrompt = "Arrows: enter code   Esc: cancel";
    [SerializeField] private string unlockedPrompt = "";

    [Header("Events")]
    [SerializeField] private UnityEvent onUnlocked;
    [SerializeField] private UnityEvent onWrongCombo;
    [Tooltip("Fires on every valid direction press (for click/beep feedback).")]
    [SerializeField] private UnityEvent onDirectionEntered;

    /// <summary>Fires whenever the entered sequence changes (cleared, appended, or reset).</summary>
    public event Action<IReadOnlyList<ComboDirection>> SequenceChanged;

    public bool IsUnlocked { get; private set; }
    public bool IsEntering { get; private set; }

    private readonly List<ComboDirection> entered = new List<ComboDirection>();
    private PlayerController focusedPlayer;

    public void Interact(PlayerController player)
    {
        if (IsUnlocked)
        {
            return;
        }

        if (IsEntering)
        {
            ExitKeypad();   // second press cancels
            return;
        }

        EnterKeypad(player);
    }

    public string GetPrompt()
    {
        if (IsUnlocked)
        {
            return unlockedPrompt;
        }

        return IsEntering ? enteringPrompt : lockedPrompt;
    }

    private void Update()
    {
        if (!IsEntering || IsUnlocked)
        {
            return;
        }

        ComboDirection? dir = null;

        // Keyboard arrow keys (Esc cancels).
        var kb = Keyboard.current;
        if (kb != null)
        {
            if (kb.escapeKey.wasPressedThisFrame)
            {
                ExitKeypad();
                return;
            }

            if (kb.upArrowKey.wasPressedThisFrame) dir = ComboDirection.Up;
            else if (kb.downArrowKey.wasPressedThisFrame) dir = ComboDirection.Down;
            else if (kb.leftArrowKey.wasPressedThisFrame) dir = ComboDirection.Left;
            else if (kb.rightArrowKey.wasPressedThisFrame) dir = ComboDirection.Right;
        }

        // Gamepad D-pad (Circle / buttonEast cancels).
        var gp = Gamepad.current;
        if (dir == null && gp != null)
        {
            if (gp.buttonEast.wasPressedThisFrame)
            {
                ExitKeypad();
                return;
            }

            if (gp.dpad.up.wasPressedThisFrame) dir = ComboDirection.Up;
            else if (gp.dpad.down.wasPressedThisFrame) dir = ComboDirection.Down;
            else if (gp.dpad.left.wasPressedThisFrame) dir = ComboDirection.Left;
            else if (gp.dpad.right.wasPressedThisFrame) dir = ComboDirection.Right;
        }

        if (dir.HasValue)
        {
            RegisterDirection(dir.Value);
        }
    }

    private void EnterKeypad(PlayerController player)
    {
        IsEntering = true;
        focusedPlayer = player;
        entered.Clear();
        SequenceChanged?.Invoke(entered);

        if (freezePlayerWhileEntering && player != null)
        {
            player.SetInputEnabled(false);
        }
    }

    private void ExitKeypad()
    {
        IsEntering = false;
        entered.Clear();
        SequenceChanged?.Invoke(entered);

        if (freezePlayerWhileEntering && focusedPlayer != null)
        {
            focusedPlayer.SetInputEnabled(true);
        }

        focusedPlayer = null;
    }

    private void RegisterDirection(ComboDirection dir)
    {
        entered.Add(dir);
        onDirectionEntered?.Invoke();

        var index = entered.Count - 1;
        if (combo.Count == 0 || entered[index] != combo[index])
        {
            // Wrong press - drop the player out of the keypad (re-enabling movement)
            // so they have to interact again to retry.
            onWrongCombo?.Invoke();
            ExitKeypad();
            return;
        }

        SequenceChanged?.Invoke(entered);

        if (entered.Count == combo.Count)
        {
            Unlock();
        }
    }

    private void Unlock()
    {
        IsUnlocked = true;
        ExitKeypad();

        if (door != null)
        {
            door.OpenDoor();
        }

        onUnlocked?.Invoke();
    }
}
