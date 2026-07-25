/// <summary>
/// The card a room prints when the player files an answer. Every resolved room emits one
/// even when the answer was wrong - a wrong run still yields a code character, but the
/// evidence attached to it is misleading, which is how a player can reach the finale with
/// a plausible but incorrect code.
///
/// A struct, and immutable: cards are small, copied freely into the inventory and the
/// views, and must never be edited after the room has filed them.
/// </summary>
public readonly struct PuzzleCard
{
    /// <summary>Where this card sits in the verification procedure. Decides code order.</summary>
    public readonly VerificationStage Stage;

    /// <summary>Printed noun for the stage, e.g. "CONFIRMATION".</summary>
    public readonly string StageLabel;

    /// <summary>Single character contributed to the final code.</summary>
    public readonly char CodeCharacter;

    /// <summary>What the card tells the player about the launch warning.</summary>
    public readonly string Evidence;

    /// <summary>True when the room judged the player's answer correct.</summary>
    public readonly bool WasCorrect;

    /// <summary>Id of the PuzzleConfig that produced this, for logging and duplicate reports.</summary>
    public readonly string SourcePuzzleId;

    public PuzzleCard(
        VerificationStage stage,
        string stageLabel,
        char codeCharacter,
        string evidence,
        bool wasCorrect,
        string sourcePuzzleId)
    {
        Stage = stage;
        StageLabel = string.IsNullOrEmpty(stageLabel) ? VerificationStages.DisplayLabel(stage) : stageLabel;
        CodeCharacter = codeCharacter;
        Evidence = evidence;
        WasCorrect = wasCorrect;
        SourcePuzzleId = sourcePuzzleId;
    }

    /// <summary>Printed form, e.g. "CONFIRMATION - 9".</summary>
    public string ToPrintedLine()
    {
        return $"{StageLabel} - {CodeCharacter}";
    }

    public override string ToString()
    {
        return $"{ToPrintedLine()} (correct={WasCorrect}, from={SourcePuzzleId})";
    }
}
