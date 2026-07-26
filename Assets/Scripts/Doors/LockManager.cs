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
