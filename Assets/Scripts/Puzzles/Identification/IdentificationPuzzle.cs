using System;
using TMPro;
using UnityEngine;

/// <summary>
/// The Identification Room. A telemetry sheet reports the detected object's readings; a
/// wall chart gives the bands those readings fall into. The player sets one switch per
/// criterion and pulls CLASSIFY, and the machine announces what it thinks the object is.
///
/// The joke is that the machine is confidently specific and frequently wrong: the object is
/// moving far too slowly to be a missile, and only a correctly-read panel gets that out of it.
///
/// The switch meanings live entirely in the authored options, so this same component also
/// supports the doc's simpler MISSILE / AIRCRAFT / OTHER phrasing - that is a change to the
/// SelectorSwitch option lists and <see cref="correctOptionIndices"/>, not to this code.
/// </summary>
public class IdentificationPuzzle : BasePuzzle<int>, IConfirmablePuzzle
{
    [Header("Switches")]
    [Tooltip("The criteria switches, in the order the answer is read. Usually SPEED, ALTITUDE, HEAT.")]
    [SerializeField] private SelectorSwitch[] criteriaSwitches = Array.Empty<SelectorSwitch>();

    [Header("Answer")]
    [Tooltip("Correct option index for each switch, in the same order as the switches above.")]
    [SerializeField] private int[] correctOptionIndices = Array.Empty<int>();

    [Header("Verdict")]
    [Tooltip("Optional. The machine's spoken classification, shown on pulling CLASSIFY.")]
    [SerializeField] private TMP_Text verdictDisplay;

    [SerializeField] private string correctVerdict = "PROBABLE METEOROLOGICAL OBJECT";

    [Tooltip("What the machine announces for a wrong panel. Chosen deterministically, so the "
             + "same wrong combination always reads the same.")]
    [TextArea]
    [SerializeField]
    private string[] incorrectVerdicts =
    {
        "EXTREMELY AMBITIOUS GOOSE",
        "AMERICAN MOON",
        "OBJECT INSUFFICIENTLY PATRIOTIC",
        "UNKNOWN. RECOMMEND ESCALATING.",
    };

    [Header("Behaviour")]
    [Tooltip("Refuse CLASSIFY until the player has moved at least one switch, so an idle pull "
             + "cannot seal the room before they have read anything.")]
    [SerializeField] private bool requireAnyChange = true;

    private bool hasPlayerSetAnySwitch;

    /// <summary>True once CLASSIFY has been pulled; the panel is sealed.</summary>
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

            return CanConfirm ? null : "Set the classification switches first";
        }
    }

    public bool CanReset => !IsConfirmed;

    /// <summary>Raised when CLASSIFY is pulled, carrying the printed card.</summary>
    public event Action<PuzzleCard> OnClassified;

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

        // Only the target is set here. The current selection is read at CLASSIFY time, never
        // in Awake - the switches set their own starting position in their Awake, and the
        // relative order of two Awakes is undefined.
        targetState = EncodeIndices(correctOptionIndices);
        hasPlayerSetAnySwitch = false;
    }

    /// <summary>
    /// Locks in the player's reading of the telemetry and prints the room's card. Always
    /// produces a result: a wrong panel still yields a code character, with a confidently
    /// wrong classification attached.
    /// </summary>
    public PuzzleCard Confirm()
    {
        if (IsConfirmed)
        {
            return FiledCard ?? BuildCard(IsSolved);
        }

        if (!CanConfirm)
        {
            // Guarded rather than silently filed, exactly as the generators are: sealing the
            // room before the player has touched anything leaves them a dead puzzle.
            Debug.LogWarning($"[Identification] CLASSIFY refused - {ConfirmBlockedReason}.", this);
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

        Debug.Log($"[Identification] CLASSIFY filed - panel {DescribeSelection()}, correct={card.WasCorrect}, "
                  + $"card \"{card.ToPrintedLine()}\". Room is now sealed.", this);

        ShowVerdict(card.WasCorrect);
        OnClassified?.Invoke(card);

        return card;
    }

    /// <summary>Returns every switch to its starting position so the player can re-read the sheet.</summary>
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

        Debug.Log("[Identification] RESET - switches returned to their starting positions.", this);
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
            return "UNKNOWN OBJECT";
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
                $"[IdentificationPuzzle] '{name}' has {criteriaSwitches.Length} switches but "
                + $"{correctOptionIndices.Length} correct indices. They must line up one-to-one.",
                this);
        }
    }
#endif
}
