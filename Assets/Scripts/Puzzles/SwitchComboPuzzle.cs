using System;
using TMPro;
using UnityEngine;

/// <summary>
/// A room solved by setting a row of <see cref="SelectorSwitch"/>es to one correct
/// combination and pulling a confirm lever. The player reads some evidence in the room,
/// dials the switches to match, and commits; the machine announces a result and prints the
/// room's card - right or wrong.
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

    private bool hasPlayerSetAnySwitch;

    /// <summary>Console-log prefix. Subclasses override so each room logs under its own name.</summary>
    protected virtual string LogTag => "SwitchCombo";

    /// <summary>True once the confirm lever has been pulled; the panel is sealed.</summary>
    public bool IsConfirmed { get; private set; }

    public bool CanConfirm => !IsConfirmed && (!requireAnyChange || hasPlayerSetAnySwitch);

    public string ConfirmBlockedReason
    {
        get
        {
            if (IsConfirmed)
            {
                return "Result already filed";
            }

            return CanConfirm ? null : "Set the switches first";
        }
    }

    public bool CanReset => !IsConfirmed;

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
    }

    /// <summary>
    /// Locks in the player's reading and prints the room's card. Always produces a result:
    /// a wrong panel still yields a code character, with a confidently wrong result attached.
    /// </summary>
    public PuzzleCard Confirm()
    {
        if (IsConfirmed)
        {
            return FiledCard ?? BuildCard(IsSolved);
        }

        if (!CanConfirm)
        {
            // Guarded rather than silently filed: sealing the room before the player has
            // touched anything leaves them a dead puzzle.
            Debug.LogWarning($"[{LogTag}] Confirm refused - {ConfirmBlockedReason}.", this);
            return BuildCard(false);
        }

        IsConfirmed = true;

        currentState = EncodeCurrentSelection();
        CheckSolve();

        LockSwitches();

        // Resolved regardless of correctness - the room is done with the player either way,
        // which is what the exit door keys off. This also files the card.
        MarkResolved(IsSolved);

        var card = FiledCard.Value;

        Debug.Log($"[{LogTag}] Confirm filed - panel {DescribeSelection()}, correct={card.WasCorrect}, "
                  + $"card \"{card.ToPrintedLine()}\". Room is now sealed.", this);

        ShowVerdict(card.WasCorrect);
        OnConfirmed?.Invoke(card);

        return card;
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
