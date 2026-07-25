using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;

/// <summary>
/// A room solved by setting a row of <see cref="SelectorSwitch"/>es to one correct
/// combination and pulling a confirm lever. The player reads some evidence in the room,
/// dials the switches to match, and commits.
///
/// By default the room <b>refuses to file a wrong answer</b>: the machine announces what it
/// thinks it is looking at, the panel hands itself back, and the player tries again. Only a
/// correct combination seals the room, prints a card and resolves it. This is what stops a
/// player carrying a wrong reading forward into the finale - the pressure comes from the
/// seven-minute clock, not from a permanent mistake. Turn <c>rejectWrongAnswers</c> off for
/// the older behaviour, where a confirmed wrong answer files a misleading card and seals the
/// room.
///
/// This is the shared spine of the Identification and Radar rooms (and any future
/// dial-a-combination room): they differ only in switch labels, the correct indices, and
/// the flavour of the result text, all of which is authored data. A room that needs no
/// extra behaviour is just an empty subclass so it has its own component type in the scene;
/// override <see cref="LogTag"/> to keep its console output under its own name.
/// </summary>
public class SwitchComboPuzzle : BasePuzzle<int>, IConfirmablePuzzle
{
    [Header("Switches")]
    [Tooltip("The switches, in the order the answer is read. Usually three.")]
    [SerializeField] private SelectorSwitch[] criteriaSwitches = Array.Empty<SelectorSwitch>();

    [Header("Answer")]
    [Tooltip("Correct option index for each switch, in the same order as the switches above.")]
    [SerializeField] private int[] correctOptionIndices = Array.Empty<int>();

    [Header("Result")]
    [Tooltip("Optional. The machine's spoken result, shown on confirm.")]
    [SerializeField] private TMP_Text verdictDisplay;

    [SerializeField] private string correctVerdict = "CONFIRMED";

    [Tooltip("What the machine announces for a wrong panel. Chosen deterministically, so the "
             + "same wrong combination always reads the same.")]
    [TextArea]
    [SerializeField] private string[] incorrectVerdicts = Array.Empty<string>();

    [Header("Behaviour")]
    [Tooltip("Refuse confirm until the player has moved at least one switch, so an idle pull "
             + "cannot seal the room before they have read anything.")]
    [SerializeField] private bool requireAnyChange = true;

    [Header("Wrong answers")]
    [Tooltip("On: a wrong combination is REFUSED - the machine says what it thinks it sees, the "
             + "panel resets, and the player tries again. Nothing is filed and the room stays "
             + "open. Off: a wrong combination files a misleading card and seals the room.")]
    [SerializeField] private bool rejectWrongAnswers = true;

    [Tooltip("How long the refusal stays on the display before the panel hands itself back. "
             + "Long enough to read, short enough not to cost real clock time.")]
    [SerializeField] private float rejectHoldSeconds = 1.8f;

    [Tooltip("How the refusal reads. {0} is the wrong verdict picked from the list above. Empty "
             + "shows the bare verdict, which reads as an ANSWER rather than a refusal.")]
    [SerializeField] private string rejectedVerdictFormat = "{0}\n- READING REJECTED -";

    private bool hasPlayerSetAnySwitch;
    private bool isHandingBack;

    /// <summary>Console-log prefix. Subclasses override so each room logs under its own name.</summary>
    protected virtual string LogTag => "SwitchCombo";

    /// <summary>True once the confirm lever has been pulled; the panel is sealed.</summary>
    public bool IsConfirmed { get; private set; }

    public bool CanConfirm => !IsConfirmed && !isHandingBack && (!requireAnyChange || hasPlayerSetAnySwitch);

    public string ConfirmBlockedReason
    {
        get
        {
            if (IsConfirmed)
            {
                return "Result already filed";
            }

            if (isHandingBack)
            {
                return "Panel resetting";
            }

            return CanConfirm ? null : "Set the switches first";
        }
    }

    /// <summary>
    /// Refused while the panel is handing itself back, so a manual reset cannot land in the
    /// middle of the automatic one and have the player's fresh input wiped a moment later.
    /// </summary>
    public bool CanReset => !IsConfirmed && !isHandingBack;

    /// <summary>How many wrong combinations the player has had handed back. Tuning telemetry.</summary>
    public int RejectedAttempts { get; private set; }

    /// <summary>Raised when the room is confirmed, carrying the printed card.</summary>
    public event Action<PuzzleCard> OnConfirmed;

    private void OnEnable()
    {
        foreach (var criteriaSwitch in criteriaSwitches)
        {
            if (criteriaSwitch == null)
            {
                continue;
            }

            criteriaSwitch.OnSelectionChanged += HandleSelectionChanged;
        }
    }

    private void OnDisable()
    {
        foreach (var criteriaSwitch in criteriaSwitches)
        {
            if (criteriaSwitch == null)
            {
                continue;
            }

            criteriaSwitch.OnSelectionChanged -= HandleSelectionChanged;
        }
    }

    public override void InitializePuzzle()
    {
        base.InitializePuzzle();

        // Only the target is set here. The current selection is read at confirm time, never
        // in Awake - the switches set their own starting position in their Awake, and the
        // relative order of two Awakes is undefined.
        targetState = EncodeIndices(correctOptionIndices);
        hasPlayerSetAnySwitch = false;
        isHandingBack = false;
    }

    /// <summary>
    /// Judges the player's reading. A correct panel seals the room and prints its card; a wrong
    /// one is refused and handed back (see <c>rejectWrongAnswers</c>), returning null so the
    /// lever knows there is nothing to print.
    /// </summary>
    public PuzzleCard? Confirm()
    {
        if (IsConfirmed)
        {
            return FiledCard;
        }

        if (!CanConfirm)
        {
            // Guarded rather than silently filed: sealing the room before the player has
            // touched anything leaves them a dead puzzle.
            Debug.LogWarning($"[{LogTag}] Confirm refused - {ConfirmBlockedReason}.", this);
            return null;
        }

        // Read and judged before anything is committed, so a wrong panel can still be handed
        // back. CheckSolve only ever latches on a match, so calling it on a wrong attempt
        // costs nothing and leaves the room unsolved.
        currentState = EncodeCurrentSelection();
        var isCorrect = CheckSolve();

        if (!isCorrect && rejectWrongAnswers)
        {
            RefuseAttempt();
            return null;
        }

        IsConfirmed = true;

        LockSwitches();

        // Files the card and resolves the room, which is what the exit door keys off. With
        // rejectWrongAnswers on this is only ever reached by a correct panel, so resolution
        // now means "solved" - a room that also files wrong answers is the opt-out.
        MarkResolved(IsSolved);

        var card = FiledCard.Value;

        Debug.Log($"[{LogTag}] Confirm filed - panel {DescribeSelection()}, correct={card.WasCorrect}, "
                  + $"card \"{card.ToPrintedLine()}\". Room is now sealed.", this);

        ShowVerdict(card.WasCorrect);
        OnConfirmed?.Invoke(card);

        return card;
    }

    /// <summary>
    /// Hands a wrong reading back instead of filing it. Nothing is committed: no card, no
    /// resolution, no seal - the machine states what it thinks it is looking at, holds that on
    /// the display long enough to be read, and then returns the panel to its starting position.
    ///
    /// The switches lock for the duration so input during the hold cannot be silently wiped by
    /// the reset that follows.
    /// </summary>
    private void RefuseAttempt()
    {
        RejectedAttempts++;
        isHandingBack = true;

        LockSwitches();
        ShowRefusal();

        Debug.Log($"[{LogTag}] Attempt REFUSED - panel {DescribeSelection()} does not match the "
                  + $"evidence (refusal {RejectedAttempts}). Nothing filed; the panel resets in "
                  + $"{rejectHoldSeconds:0.0}s.", this);

        RaiseAttemptRejected();
        HandBackAttemptAsync(this.GetCancellationTokenOnDestroy()).Forget();
    }

    private async UniTaskVoid HandBackAttemptAsync(CancellationToken token)
    {
        try
        {
            // ignoreTimeScale: a panel frozen mid-refusal would be unrecoverable if anything
            // pauses the game, and the room has to stay usable while the clock runs.
            await UniTask.Delay(
                TimeSpan.FromSeconds(Mathf.Max(0f, rejectHoldSeconds)),
                DelayType.UnscaledDeltaTime,
                cancellationToken: token);
        }
        catch (OperationCanceledException)
        {
            // Room destroyed mid-hold. There is nothing left to hand back to.
            return;
        }

        // Cleared before the reset so ResetAttempt's own guards see a settled panel.
        isHandingBack = false;
        ResetAttempt();
    }

    /// <summary>Returns every switch to its starting position so the player can re-read the room.</summary>
    public void ResetAttempt()
    {
        if (IsConfirmed)
        {
            return;
        }

        foreach (var criteriaSwitch in criteriaSwitches)
        {
            if (criteriaSwitch == null)
            {
                continue;
            }

            criteriaSwitch.ResetToStart();
        }

        hasPlayerSetAnySwitch = false;
        ClearVerdict();

        Debug.Log($"[{LogTag}] RESET - switches returned to their starting positions.", this);
    }

    public override void ResetPuzzle()
    {
        IsConfirmed = false;

        // A full reset is a fresh room, so the refusal count goes with it. Any hold still in
        // flight is harmless: it clears the same flag and resets an already-reset panel.
        isHandingBack = false;
        RejectedAttempts = 0;

        base.ResetPuzzle();

        ResetAttempt();
    }

    private void HandleSelectionChanged(SelectorSwitch changed)
    {
        hasPlayerSetAnySwitch = true;
    }

    private void LockSwitches()
    {
        foreach (var criteriaSwitch in criteriaSwitches)
        {
            if (criteriaSwitch == null)
            {
                continue;
            }

            criteriaSwitch.IsLocked = true;
        }
    }

    private int EncodeCurrentSelection()
    {
        var encoded = 0;
        var place = 1;

        for (var i = 0; i < criteriaSwitches.Length; i++)
        {
            var index = criteriaSwitches[i] == null ? 0 : criteriaSwitches[i].CurrentIndex;
            encoded += index * place;
            place *= ENCODING_BASE;
        }

        return encoded;
    }

    /// <summary>
    /// Packs one index per switch into a single int so <see cref="BasePuzzle{T}"/> can compare
    /// the whole panel with its default equality comparer. Base 16 leaves room for any switch
    /// this game will ever have without overflowing across places.
    /// </summary>
    private static int EncodeIndices(int[] indices)
    {
        var encoded = 0;
        var place = 1;

        for (var i = 0; i < indices.Length; i++)
        {
            encoded += indices[i] * place;
            place *= ENCODING_BASE;
        }

        return encoded;
    }

    private const int ENCODING_BASE = 16;

    private void ShowVerdict(bool wasCorrect)
    {
        if (verdictDisplay == null)
        {
            return;
        }

        verdictDisplay.text = wasCorrect ? correctVerdict : PickIncorrectVerdict();
    }

    /// <summary>
    /// The refusal message. Wraps the wrong verdict in <c>rejectedVerdictFormat</c> so the
    /// display reads as the machine turning the reading down rather than as its final answer -
    /// a bare "WEATHER BALLOON" looks like a result the player just earned.
    /// </summary>
    private void ShowRefusal()
    {
        if (verdictDisplay == null)
        {
            return;
        }

        var verdict = PickIncorrectVerdict();

        verdictDisplay.text = string.IsNullOrEmpty(rejectedVerdictFormat)
            ? verdict
            : string.Format(rejectedVerdictFormat, verdict);
    }

    private void ClearVerdict()
    {
        if (verdictDisplay != null)
        {
            verdictDisplay.text = string.Empty;
        }
    }

    /// <summary>
    /// Keyed off the panel itself rather than chosen at random, so a player who retries the
    /// same wrong combination is not told a different story each time.
    /// </summary>
    private string PickIncorrectVerdict()
    {
        if (incorrectVerdicts.Length == 0)
        {
            return "UNKNOWN";
        }

        var key = Mathf.Abs(currentState) % incorrectVerdicts.Length;
        return incorrectVerdicts[key];
    }

    private string DescribeSelection()
    {
        var description = string.Empty;

        for (var i = 0; i < criteriaSwitches.Length; i++)
        {
            if (criteriaSwitches[i] == null)
            {
                continue;
            }

            description += $"[{criteriaSwitches[i].SwitchLabel}={criteriaSwitches[i].CurrentOption}]";
        }

        return description;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        // The two arrays are read in lockstep; a mismatch means the answer silently checks
        // against the wrong switch, which is invisible until someone fails a correct panel.
        if (criteriaSwitches.Length != correctOptionIndices.Length)
        {
            Debug.LogWarning(
                $"[{GetType().Name}] '{name}' has {criteriaSwitches.Length} switches but "
                + $"{correctOptionIndices.Length} correct indices. They must line up one-to-one.",
                this);
        }
    }
#endif
}
