using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Registry of the level's door locks, and the Editor-only developer hack keys (1-4)
/// that toggle interactable state or force a door open.
///
/// This used to decide which locks were reachable from <see cref="WatchManager"/> elapsed
/// time. Doors are gated on their passcode alone now, so there is nothing left to evaluate
/// per minute - every keypad is live from the start.
/// </summary>
public class LockManager : MonoBehaviour
{
    [Header("Door Locks")]
    [Tooltip("Door locks in progression order (1-4). Order only affects the dev hack keys.")]
    [SerializeField] private DoorLockController[] locks = new DoorLockController[4];

    public IReadOnlyList<DoorLockController> Locks => locks;

    private KeypadPopupUI keypadPopup;
    private PlayerController playerController;

    private void OnEnable()
    {
        SubscribeToEvents();
    }

    private void OnDisable()
    {
        UnsubscribeFromEvents();
    }

    private void Start()
    {
        keypadPopup = FindFirstObjectByType<KeypadPopupUI>();
        playerController = FindFirstObjectByType<PlayerController>();
    }

    private void SubscribeToEvents()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnGameStarted += HandleGameStarted;
        }
    }

    private void UnsubscribeFromEvents()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnGameStarted -= HandleGameStarted;
        }
    }

    /// <summary>
    /// A restart rebuilds the run, so any lock a dev hack key had switched off is put
    /// back the way a fresh door starts: reachable, waiting on its passcode.
    /// </summary>
    private void HandleGameStarted()
    {
        if (locks == null)
        {
            return;
        }

        foreach (var lockController in locks)
        {
            if (lockController != null && !lockController.IsUnlocked)
            {
                lockController.SetInteractable(true);
            }
        }
    }

#if UNITY_EDITOR
    private void Update()
    {
        if ((keypadPopup != null && keypadPopup.IsOpen) || (playerController != null && !playerController.IsInputEnabled))
        {
            return;
        }

        var keyboard = Keyboard.current;

        if (keyboard == null)
        {
            return;
        }

        var isShiftPressed = keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed;

        if (keyboard.digit1Key.wasPressedThisFrame || keyboard.numpad1Key.wasPressedThisFrame)
        {
            HandleDevHackKey(0, isShiftPressed);
        }
        else if (keyboard.digit2Key.wasPressedThisFrame || keyboard.numpad2Key.wasPressedThisFrame)
        {
            HandleDevHackKey(1, isShiftPressed);
        }
        else if (keyboard.digit3Key.wasPressedThisFrame || keyboard.numpad3Key.wasPressedThisFrame)
        {
            HandleDevHackKey(2, isShiftPressed);
        }
        else if (keyboard.digit4Key.wasPressedThisFrame || keyboard.numpad4Key.wasPressedThisFrame)
        {
            HandleDevHackKey(3, isShiftPressed);
        }
    }

    private void HandleDevHackKey(int index, bool forceUnlock)
    {
        if (locks == null || index < 0 || index >= locks.Length)
        {
            return;
        }

        var lockController = locks[index];

        if (lockController == null)
        {
            Debug.LogWarning($"[DEV HACK] Door lock at index {index} is not assigned in LockManager.", this);
            return;
        }

        if (forceUnlock)
        {
            lockController.Unlock();
            Debug.Log($"[DEV HACK] Force unlocked Door Lock {index + 1} ('{lockController.Config?.RoomId}')", lockController);
        }
        else
        {
            var newInteractableState = !lockController.IsInteractable;
            lockController.SetInteractable(newInteractableState);
            Debug.Log($"[DEV HACK] Set Door Lock {index + 1} ('{lockController.Config?.RoomId}') interactable = {newInteractableState}", lockController);
        }
    }
#endif
}
