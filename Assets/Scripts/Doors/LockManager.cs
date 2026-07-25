using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Decides which door locks are currently interactable based on global <see cref="WatchManager"/> elapsed time.
/// Also provides Editor-only developer hack keys (1-4) to toggle interactable state or force unlock doors.
/// </summary>
public class LockManager : MonoBehaviour
{
    [Header("Door Locks")]
    [Tooltip("Door locks managed by time-gating in progression order (1-4).")]
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
        EvaluateLocks();
    }

    private void SubscribeToEvents()
    {
        if (locks != null)
        {
            foreach (var lockController in locks)
            {
                if (lockController != null)
                {
                    lockController.OnUnlocked += HandleLockUnlocked;
                }
            }
        }

        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnGameStarted += HandleGameStarted;

            if (GameManager.Instance.WatchManager != null)
            {
                GameManager.Instance.WatchManager.OnElapsedMinuteChanged += HandleElapsedMinuteChanged;
            }
        }
    }

    private void UnsubscribeFromEvents()
    {
        if (locks != null)
        {
            foreach (var lockController in locks)
            {
                if (lockController != null)
                {
                    lockController.OnUnlocked -= HandleLockUnlocked;
                }
            }
        }

        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnGameStarted -= HandleGameStarted;

            if (GameManager.Instance.WatchManager != null)
            {
                GameManager.Instance.WatchManager.OnElapsedMinuteChanged -= HandleElapsedMinuteChanged;
            }
        }
    }

    private void HandleGameStarted()
    {
        EvaluateLocks();
    }

    private void HandleElapsedMinuteChanged(int elapsedMinutes)
    {
        EvaluateLocks();
    }

    private void HandleLockUnlocked()
    {
        EvaluateLocks();
    }

    /// <summary>
    /// Evaluates each door lock and sets interactable = true if watch elapsed time has reached the lock's unlock time threshold.
    /// </summary>
    public void EvaluateLocks()
    {
        if (locks == null || locks.Length == 0)
        {
            return;
        }

        if (GameManager.Instance == null || GameManager.Instance.WatchManager == null)
        {
            return;
        }

        var elapsedMinutes = GameManager.Instance.WatchManager.ElapsedMinutes;

        foreach (var lockController in locks)
        {
            if (lockController == null || lockController.IsUnlocked)
            {
                continue;
            }

            var canInteract = lockController.CanUnlockAtCurrentTime(elapsedMinutes);
            lockController.SetInteractable(canInteract);
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
