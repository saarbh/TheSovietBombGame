using System;
using UnityEngine;

/// <summary>
/// Controls initial room progression when leaving the control room (see <see cref="ControlRoomTrigger"/>).
/// Doors require manual passcode entry and time-gating rather than auto-unlocking on puzzle completion.
/// </summary>
public class RoomProgressionController : MonoBehaviour
{
    [Serializable]
    private class Stage
    {
        [Tooltip("Door associated with this stage.")]
        public RoomDoor door;

        [Tooltip("Puzzle inside this stage's room.")]
        public GameObject puzzleObject;
    }

    [Tooltip("Rooms in progression order. Stage 0's door opens on leaving the control room.")]
    [SerializeField] private Stage[] stages = Array.Empty<Stage>();

    private int openedCount;
    private bool begun;

    /// <summary>How many doors have been opened so far.</summary>
    public int OpenedCount => openedCount;

    /// <summary>
    /// Opens the first room's door. Called once, by the control-room trigger. Idempotent, so
    /// a player who steps back and forth over the trigger cannot re-fire it.
    /// </summary>
    public void Begin()
    {
        if (begun)
        {
            return;
        }

        begun = true;

        Debug.Log("[RoomProgression] Player left the control room - opening the first room.", this);
        OpenNextDoor();
    }

    /// <summary>
    /// Opens the next not-yet-opened door in the sequence. Skips stages left without a door
    /// but still consumes their slot, so an empty placeholder room does not stall the chain.
    /// </summary>
    private void OpenNextDoor()
    {
        while (openedCount < stages.Length)
        {
            var door = stages[openedCount].door;
            openedCount++;

            if (door != null)
            {
                door.OpenDoor();
                Debug.Log($"[RoomProgression] Opened '{door.name}' ({openedCount}/{stages.Length}).", this);
                return;
            }

            Debug.LogWarning($"[RoomProgression] Stage {openedCount - 1} has no door; skipping it.", this);
        }

        Debug.Log("[RoomProgression] No further doors to open - the last room is unlocked.", this);
    }
}
