using System;
using UnityEngine;

/// <summary>
/// Controls interactive door locks attached to doors. Implements <see cref="IInteractable"/>.
/// </summary>
public class DoorLockController : MonoBehaviour, IInteractable
{
    [Header("Configuration")]
    [Tooltip("Room configuration carrying expected unlock time and correct passcode.")]
    [SerializeField] private RoomConfig config;

    [Header("Door Link")]
    [Tooltip("The door unlocked by this controller.")]
    [SerializeField] private RoomDoor door;

    [Header("Prompts")]
    [SerializeField] private string lockedPrompt = "[E] Keypad Locked (Time-Gated)";
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

    public bool CanUnlockAtCurrentTime(float elapsedMinutes)
    {
        if (config == null)
        {
            return true;
        }

        return elapsedMinutes >= config.ActualUnlockTimeMinutes;
    }

    public void Interact(PlayerController player)
    {
        if (!enabled || IsUnlocked)
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
