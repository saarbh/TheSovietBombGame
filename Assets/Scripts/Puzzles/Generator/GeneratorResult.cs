/// <summary>
/// The card a room prints when the player pulls CONFIRM. Every confirmed room emits one
/// even when the answer was wrong - a wrong run still yields a code character, but the
/// evidence attached to it is misleading, which is how a player can reach the finale with
/// a plausible but incorrect code.
/// </summary>
public readonly struct GeneratorResult
{
    /// <summary>Verification stage this room satisfies, e.g. "CONFIRMATION".</summary>
    public readonly string StageLabel;

    /// <summary>Single character contributed to the final code.</summary>
    public readonly char CodeCharacter;

    /// <summary>What the card tells the player about the launch warning.</summary>
    public readonly string Evidence;

    public readonly bool WasCorrect;

    public GeneratorResult(string stageLabel, char codeCharacter, string evidence, bool wasCorrect)
    {
        StageLabel = stageLabel;
        CodeCharacter = codeCharacter;
        Evidence = evidence;
        WasCorrect = wasCorrect;
    }

    /// <summary>Printed form, e.g. "CONFIRMATION - 9".</summary>
    public string ToPrintedLine()
    {
        return $"{StageLabel} - {CodeCharacter}";
    }
}
