using System;
using UnityEngine;

/// <summary>
/// One outcome of a room: the character it contributes to the final code and the
/// evidence printed alongside it.
///
/// Every room has two of these - a correct and an incorrect one. The incorrect one is only
/// reachable for a room with <c>rejectWrongAnswers</c> off: rooms now refuse a wrong answer
/// and hand it back rather than filing it, so in normal play only the correct output prints.
/// Keep authoring both anyway - the wrong card is the opt-out, and a room that switches to it
/// with a blank incorrect output silently prints '?' into the final code.
///
/// The characters must differ, or a player who fails every room in a wrong-card build still
/// assembles the correct final code.
/// </summary>
[Serializable]
public class PuzzleCardOutput
{
    [Tooltip("Single character this outcome contributes to the final code, e.g. 9.")]
    [SerializeField] private string codeCharacter;

    [Tooltip("What the printed card tells the player about the launch warning.")]
    [TextArea]
    [SerializeField] private string evidence;

    /// <summary>First character of the configured string, or '?' when unset.</summary>
    public char CodeCharacter => string.IsNullOrEmpty(codeCharacter) ? '?' : codeCharacter[0];

    public string Evidence => evidence;

    /// <summary>False when nobody filled this outcome in yet.</summary>
    public bool IsAuthored => !string.IsNullOrEmpty(codeCharacter);
}

/// <summary>
/// Authoring data shared by every room puzzle: what it is called, whether a player who
/// walks out mid-solve finds it as they left it, and the two cards it can print.
/// </summary>
[CreateAssetMenu(fileName = "PuzzleConfig", menuName = "SovietBomb/Puzzle Config")]
public class PuzzleConfig : ScriptableObject
{
    [Header("Identity")]
    [SerializeField] private string puzzleId;
    [SerializeField] private string puzzleName;

    [Header("Behaviour")]
    [Tooltip("Keep partial progress when the player leaves the room. Off means the puzzle resets behind them.")]
    [SerializeField] private bool preserveProgressOnExit = true;

    [Header("Output card")]
    [Tooltip("Where this room sits in the verification procedure. Decides the card's slot in the final code.")]
    [SerializeField] private VerificationStage stage = VerificationStage.Detect;

    [Tooltip("Printed noun for the stage, e.g. CONFIRMATION. Leave empty to use the stage's default label.")]
    [SerializeField] private string stageLabel;

    [Tooltip("Printed when the player solves the room.")]
    [SerializeField] private PuzzleCardOutput correctOutput = new PuzzleCardOutput();

    [Tooltip("Printed when the player confirms a wrong answer. Must still read as plausible, and MUST use a different character.")]
    [SerializeField] private PuzzleCardOutput incorrectOutput = new PuzzleCardOutput();

    public string PuzzleId => puzzleId;
    public string PuzzleName => puzzleName;
    public bool PreserveProgressOnExit => preserveProgressOnExit;

    /// <summary>Procedural stage this room satisfies. Sorts the card into the final code.</summary>
    public VerificationStage Stage => stage;

    /// <summary>The stage's printed noun, falling back to the stage default when unauthored.</summary>
    public string StageLabel => string.IsNullOrEmpty(stageLabel)
        ? VerificationStages.DisplayLabel(stage)
        : stageLabel;

    public PuzzleCardOutput CorrectOutput => correctOutput;
    public PuzzleCardOutput IncorrectOutput => incorrectOutput;

    /// <summary>
    /// The finished card for the attempt a room just judged. Every room builds its card
    /// here so the correct/wrong split, the stage and the label are applied identically
    /// everywhere - a room that assembled its own card would be free to get it wrong.
    /// </summary>
    public PuzzleCard BuildCard(bool wasCorrect)
    {
        var output = OutputFor(wasCorrect);

        return new PuzzleCard(
            stage,
            StageLabel,
            output.CodeCharacter,
            output.Evidence,
            wasCorrect,
            string.IsNullOrEmpty(puzzleId) ? name : puzzleId);
    }

    /// <summary>
    /// The card a room should print for the attempt it just judged. Rooms should call
    /// this rather than picking the fields apart, so the correct/wrong split is applied
    /// the same way everywhere.
    /// </summary>
    public PuzzleCardOutput OutputFor(bool wasCorrect)
    {
        return wasCorrect ? correctOutput : incorrectOutput;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        // Catches the failure at authoring time rather than in the finale, where a
        // wrong-but-identical character silently makes every playthrough's code correct.
        if (correctOutput.IsAuthored && incorrectOutput.IsAuthored
            && correctOutput.CodeCharacter == incorrectOutput.CodeCharacter)
        {
            Debug.LogWarning(
                $"[PuzzleConfig] '{name}' uses '{correctOutput.CodeCharacter}' for both the correct and incorrect "
                + "outcome. A failed room must emit a different character, or the assembled final code is always right.",
                this);
        }

        // The stage enum arrived after stageLabel did, so any asset authored before it
        // silently defaults to Detect while still displaying its old noun. That mismatch
        // would put the card in the wrong slot of the final code with nothing to show for it.
        if (!string.IsNullOrEmpty(stageLabel)
            && !string.Equals(stageLabel, VerificationStages.DisplayLabel(stage), StringComparison.OrdinalIgnoreCase))
        {
            Debug.LogWarning(
                $"[PuzzleConfig] '{name}' is labelled '{stageLabel}' but its stage is {stage} "
                + $"({VerificationStages.DisplayLabel(stage)}). Check the Stage field - the label is only "
                + "cosmetic, the stage is what orders the final code.",
                this);
        }

        if (!incorrectOutput.IsAuthored)
        {
            // Only bites a room with rejectWrongAnswers off, since a refusing room never files
            // this output at all - but that toggle is one Inspector click away, so an unauthored
            // wrong card is still a '?' waiting to appear in the final code.
            Debug.LogWarning(
                $"[PuzzleConfig] '{name}' has no incorrect-outcome character. Harmless while the room "
                + "refuses wrong answers, but it would print '?' if the room is switched to filing them.",
                this);
        }
    }
#endif
}
