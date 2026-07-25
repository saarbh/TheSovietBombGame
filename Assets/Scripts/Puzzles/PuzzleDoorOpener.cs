using UnityEngine;

/// <summary>
/// Opens an existing <see cref="RoomDoor"/> when a room's puzzle is resolved.
///
/// Exists so a room can drive whatever door already fills its doorway instead of adding
/// a second leaf. Where a doorway is already occupied - for example by a combo-locked
/// door - dropping another panel in leaves two doors fighting for the same opening.
///
/// Use this when the door is authored by someone else; use <see cref="SlidingDoor"/> when
/// the room owns its own panel. Neither touches the other's script.
/// </summary>
public class PuzzleDoorOpener : MonoBehaviour
{
    [Header("Puzzle")]
    [Tooltip("GameObject carrying the room's puzzle. Read as IPuzzleResolution.")]
    [SerializeField] private GameObject puzzleObject;

    [Header("Door")]
    [Tooltip("The door leaf to swing open. Usually someone else's RoomDoor on the room's doorway.")]
    [SerializeField] private RoomDoor door;

    [Tooltip("On means a wrong answer leaves the door shut. Off matches the design: a filed answer ends the player's business in the room either way.")]
    [SerializeField] private bool requireCorrectAnswer;

    private IPuzzleResolution puzzle;

    private void Awake()
    {
        if (puzzleObject == null)
        {
            Debug.LogError($"[{nameof(PuzzleDoorOpener)}] No puzzle object assigned.", this);
            return;
        }

        // Unity cannot serialize an interface reference, so the field is a GameObject and
        // the interface is resolved here.
        puzzle = puzzleObject.GetComponent<IPuzzleResolution>();

        if (puzzle == null)
        {
            Debug.LogError($"[{nameof(PuzzleDoorOpener)}] '{puzzleObject.name}' has no IPuzzleResolution component.", this);
        }
    }

    private void OnEnable()
    {
        if (puzzle == null)
        {
            return;
        }

        puzzle.OnResolved += HandleResolved;

        // A room resolved before this component woke up must not leave the door shut.
        if (puzzle.IsResolved)
        {
            HandleResolved(puzzle.IsSolved);
        }
    }

    private void OnDisable()
    {
        if (puzzle != null)
        {
            puzzle.OnResolved -= HandleResolved;
        }
    }

    private void HandleResolved(bool wasCorrect)
    {
        if (requireCorrectAnswer && !wasCorrect)
        {
            Debug.Log($"[{nameof(PuzzleDoorOpener)}] Room resolved incorrectly and the door requires a correct answer - staying shut.", this);
            return;
        }

        if (door == null)
        {
            Debug.LogError($"[{nameof(PuzzleDoorOpener)}] No RoomDoor assigned; nothing to open.", this);
            return;
        }

        if (door.IsOpen)
        {
            return;
        }

        Debug.Log($"[{nameof(PuzzleDoorOpener)}] Room resolved (correct={wasCorrect}) - opening '{door.name}'.", this);
        door.OpenDoor();
    }
}
