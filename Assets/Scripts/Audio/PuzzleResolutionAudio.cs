using UnityEngine;

/// <summary>
/// The room's verdict sting. Hangs off <see cref="IPuzzleResolution.OnResolved"/>, which fires
/// on a filed answer whether or not it was right - so this plays in both cases, and the two
/// clips are what tells the player which happened.
///
/// Sits on the room root next to the puzzle. Serialize the puzzle's GameObject, exactly like
/// the confirm and reset levers do: a serialized field cannot hold an interface.
/// </summary>
public class PuzzleResolutionAudio : MonoBehaviour
{
    [Header("Puzzle")]
    [Tooltip("GameObject carrying the room's puzzle. Read as IPuzzleResolution. "
             + "Leave empty to find it on this object or a parent.")]
    [SerializeField] private GameObject puzzleObject;

    [Header("Sounds")]
    [SerializeField] private SfxId solvedSfx = SfxId.PuzzleSolved;

    [SerializeField] private SfxId failedSfx = SfxId.PuzzleFailed;

    [Tooltip("Optional. Filed-into-the-record sound, played after the verdict.")]
    [SerializeField] private SfxId cardFiledSfx = SfxId.None;

    [Header("Placement")]
    [Tooltip("Optional. Where the verdict comes from - usually the printer or the verdict "
             + "display rather than the room origin.")]
    [SerializeField] private Transform emitFrom;

    private IPuzzleResolution puzzle;

    // See ConfirmLever: an interface reference to a MonoBehaviour bypasses Unity's overridden
    // ==, so resolution is recorded once as a flag instead of null-checked repeatedly.
    private bool hasPuzzle;

    private SfxBank roomBank;

    private void Awake()
    {
        if (puzzleObject != null)
        {
            puzzle = puzzleObject.GetComponent<IPuzzleResolution>();
        }

        if (puzzle == null)
        {
            puzzle = GetComponentInParent<IPuzzleResolution>();
        }

        hasPuzzle = puzzle != null;
        roomBank = RoomAudioZone.BankFor(this);

        if (emitFrom == null)
        {
            emitFrom = transform;
        }
    }

    private void OnEnable()
    {
        if (!hasPuzzle)
        {
            Debug.LogError("[Audio] PuzzleResolutionAudio has no IPuzzleResolution; this room's "
                           + "verdict will be silent.", this);
            return;
        }

        puzzle.OnResolved += HandleResolved;
    }

    private void OnDisable()
    {
        if (!hasPuzzle)
        {
            return;
        }

        puzzle.OnResolved -= HandleResolved;
    }

    private void HandleResolved(bool wasCorrect)
    {
        var verdict = wasCorrect ? solvedSfx : failedSfx;
        var position = emitFrom == null ? transform.position : emitFrom.position;

        if (verdict != SfxId.None)
        {
            AudioManager.PlayAt(verdict, position, roomBank);
        }

        if (cardFiledSfx != SfxId.None)
        {
            AudioManager.PlayAt(cardFiledSfx, position, roomBank);
        }
    }
}
