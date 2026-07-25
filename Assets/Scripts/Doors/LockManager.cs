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

#if UNITY_EDITOR
    private bool[] wasDigitPressedLastFrame = new bool[4];
    private bool[] wasNumpadPressedLastFrame = new bool[4];
#endif

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
        keypadPopup = FindObjectOfType<KeypadPopupUI>();
        playerController = FindObjectOfType<PlayerController>();
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

        bool d1 = keyboard.digit1Key.isPressed;
        bool n1 = keyboard.numpad1Key.isPressed;
        bool d2 = keyboard.digit2Key.isPressed;
        bool n2 = keyboard.numpad2Key.isPressed;
        bool d3 = keyboard.digit3Key.isPressed;
        bool n3 = keyboard.numpad3Key.isPressed;
        bool d4 = keyboard.digit4Key.isPressed;
        bool n4 = keyboard.numpad4Key.isPressed;

        if ((d1 && !wasDigitPressedLastFrame[0]) || (n1 && !wasNumpadPressedLastFrame[0]))
        {
            HandleDevHackKey(0, isShiftPressed);
        }
        else if ((d2 && !wasDigitPressedLastFrame[1]) || (n2 && !wasNumpadPressedLastFrame[1]))
        {
            HandleDevHackKey(1, isShiftPressed);
        }
        else if ((d3 && !wasDigitPressedLastFrame[2]) || (n3 && !wasNumpadPressedLastFrame[2]))
        {
            HandleDevHackKey(2, isShiftPressed);
        }
        else if ((d4 && !wasDigitPressedLastFrame[3]) || (n4 && !wasNumpadPressedLastFrame[3]))
        {
            HandleDevHackKey(3, isShiftPressed);
        }

        wasDigitPressedLastFrame[0] = d1;
        wasDigitPressedLastFrame[1] = d2;
        wasDigitPressedLastFrame[2] = d3;
        wasDigitPressedLastFrame[3] = d4;

        wasNumpadPressedLastFrame[0] = n1;
        wasNumpadPressedLastFrame[1] = n2;
        wasNumpadPressedLastFrame[2] = n3;
        wasNumpadPressedLastFrame[3] = n4;
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
