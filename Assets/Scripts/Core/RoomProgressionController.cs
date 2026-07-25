using System;
using UnityEngine;

/// <summary>
/// Opens the rooms one at a time as the player advances. The first door opens when the
/// player leaves the control room (see <see cref="ControlRoomTrigger"/>); after that, each
/// room's own puzzle resolving opens the NEXT room's door.
///
/// This owns every door it lists, so a room's door is NOT opened by solving the room behind
/// it - you would need the door open to get in and solve it. A door opens because the
/// PREVIOUS step finished: the trigger for the first, the previous room's puzzle for the rest.
///
/// It is the scene's progression wiring, so it lives on a plain scene object rather than the
/// GameManager prefab: the doors and puzzles it references are scene objects.
/// </summary>
public class RoomProgressionController : MonoBehaviour
{
    [Serializable]
    private class Stage
    {
        [Tooltip("Door opened when the previous stage finishes (the control-room trigger for the first stage, "
                 + "the previous stage's puzzle for the rest).")]
        public RoomDoor door;

        [Tooltip("Puzzle inside this stage's room. Resolving it - right or wrong - opens the NEXT stage's door. "
                 + "Leave empty for a room that has no gating puzzle yet; the chain then stops there.")]
        public GameObject puzzleObject;
    }

    [Tooltip("Rooms in unlock order. Stage 0's door opens on leaving the control room; each later door "
             + "opens when the preceding stage's puzzle resolves.")]
    [SerializeField] private Stage[] stages = Array.Empty<Stage>();

    private IPuzzleResolution[] resolutions;
    private int openedCount;
    private bool begun;

    /// <summary>How many doors have been opened so far.</summary>
    public int OpenedCount => openedCount;

    private void Awake()
    {
        // A serialized field cannot hold an interface, so each stage carries the puzzle's
        // GameObject and the resolution view is resolved here, once.
        resolutions = new IPuzzleResolution[stages.Length];

        for (var i = 0; i < stages.Length; i++)
        {
            if (stages[i].puzzleObject != null)
            {
                resolutions[i] = stages[i].puzzleObject.GetComponent<IPuzzleResolution>();

                if (resolutions[i] == null)
                {
                    Debug.LogError(
                        $"[RoomProgression] Stage {i} puzzle '{stages[i].puzzleObject.name}' has no "
                        + "IPuzzleResolution component; that room can never advance the chain.", this);
                }
            }
        }
    }

    private void OnEnable()
    {
        if (resolutions == null)
        {
            return;
        }

        for (var i = 0; i < resolutions.Length; i++)
        {
            if (resolutions[i] != null)
            {
                resolutions[i].OnResolved += HandleResolved;
            }
        }
    }

    private void OnDisable()
    {
        if (resolutions == null)
        {
            return;
        }

        for (var i = 0; i < resolutions.Length; i++)
        {
            if (resolutions[i] != null)
            {
                resolutions[i].OnResolved -= HandleResolved;
            }
        }
    }

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

    private void HandleResolved(bool wasCorrect)
    {
        // Advance on any filed answer, right or wrong: the whole game lets a player carry a
        // wrong card forward, and sealing them in a room they answered would be a dead end
        // with the clock running. This matches PuzzleDoorOpener's requireCorrect=false default.
        Debug.Log($"[RoomProgression] A room resolved (correct={wasCorrect}) - opening the next door.", this);
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
