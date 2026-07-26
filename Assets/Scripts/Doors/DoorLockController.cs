using System;
using UnityEngine;

/// <summary>
/// Controls interactive door locks attached to doors. Implements <see cref="IInteractable"/>.
///
/// The passcode is the only gate: keypads are interactable from the start of the run and
/// the clock has no say in whether a door will open. The countdown still runs, but it now
/// only bounds the whole run rather than staging which room is reachable when.
/// </summary>
public class DoorLockController : MonoBehaviour, IInteractable
{
    [Header("Configuration")]
    [Tooltip("Room configuration carrying this room's passcode.")]
    [SerializeField] private RoomConfig config;

    [Header("Door Link")]
    [Tooltip("The door unlocked by this controller.")]
    [SerializeField] private RoomDoor door;

    [Header("Prompts")]
    [Tooltip("Only ever shown if something explicitly calls SetInteractable(false) - "
             + "doors are no longer gated on the clock, so a keypad is reachable from the start.")]
    [SerializeField] private string lockedPrompt = "[E] Keypad Locked";
    [SerializeField] private string interactPrompt = "[E] Enter Passcode";
    [SerializeField] private string unlockedPrompt = "";

    private bool isInteractable;
    private bool isUnlocked;

    public RoomConfig Config => config;
    public RoomDoor Door => door;
    public bool IsInteractable => isInteractable;
    public bool IsUnlocked => isUnlocked;

    /// <summary>Fired when any lock is interacted with by a player.</summary>
    public static event Action<DoorLockController, PlayerController> OnAnyLockInteracted;

    /// <summary>Fired when this lock is successfully unlocked.</summary>
    public event Action OnUnlocked;

    /// <summary>Fired when an incorrect code is submitted.</summary>
    public event Action OnCodeAttemptFailed;

    private void Awake()
    {
        isUnlocked = false;

        // Passcode is the only gate. Doors used to open this at minute N via LockManager;
        // now every keypad is live from the first frame and the code is the whole puzzle.
        isInteractable = true;

        if (config == null)
        {
            Debug.LogWarning($"[{nameof(DoorLockController)}] '{name}' has no RoomConfig assigned.", this);
        }

        if (door == null)
        {
            door = GetComponentInParent<RoomDoor>();
        }
    }

    public string GetPrompt()
    {
        if (IsUnlocked)
        {
            return unlockedPrompt;
        }

        return IsInteractable ? interactPrompt : lockedPrompt;
    }

    /// <summary>
    /// Sets whether this lock is currently interactable.
    /// </summary>
    public void SetInteractable(bool interactable)
    {
        if (isUnlocked)
        {
            return;
        }

        isInteractable = interactable;
    }

    public void Interact(PlayerController player)
    {
        if (!enabled || IsUnlocked || !isInteractable)
        {
            return;
        }

        OnAnyLockInteracted?.Invoke(this, player);
    }

    public bool ValidateCode(string enteredCode)
    {
        if (IsUnlocked)
        {
            return true;
        }

        if (config != null && config.Matches(enteredCode))
        {
            Unlock();
            return true;
        }

        OnCodeAttemptFailed?.Invoke();
        return false;
    }

    public void Unlock()
    {
        if (isUnlocked)
        {
            return;
        }

        isUnlocked = true;
        isInteractable = true;

        if (door != null)
        {
            door.OpenDoor();
        }

        OnUnlocked?.Invoke();
        Debug.Log($"[{nameof(DoorLockController)}] Door Lock '{config?.RoomId}' UNLOCKED!", this);
    }
}
