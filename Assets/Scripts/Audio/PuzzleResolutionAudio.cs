using UnityEngine;

/// <summary>
/// The room's verdict sting. Hangs off both outcomes a room can produce:
/// <see cref="IPuzzleResolution.OnResolved"/> for a filed answer, and
/// <see cref="IPuzzleResolution.OnAttemptRejected"/> for a wrong reading the room handed back.
///
/// Both routes play the failure clip, which matters because a room that refuses wrong answers
/// never resolves incorrectly - without the rejection hook, failing would be silent, and silence
/// reads as "the lever is broken" rather than "that was wrong".
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
        puzzle.OnAttemptRejected += HandleAttemptRejected;
    }

    private void OnDisable()
    {
        if (!hasPuzzle)
        {
            return;
        }

        puzzle.OnResolved -= HandleResolved;
        puzzle.OnAttemptRejected -= HandleAttemptRejected;
    }

    /// <summary>
    /// A wrong reading handed back. Plays the failure clip but never the card sound - nothing was
    /// filed, and a filing noise here would tell the player they had banked something.
    /// </summary>
    private void HandleAttemptRejected()
    {
        if (failedSfx == SfxId.None)
        {
            return;
        }

        var position = emitFrom == null ? transform.position : emitFrom.position;
        AudioManager.PlayAt(failedSfx, position, roomBank);
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
