using UnityEngine;

/// <summary>
/// Authoring data shared by every room puzzle: what it is called, and whether a
/// player who walks out mid-solve finds it as they left it.
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
    [Tooltip("Verification stage this room satisfies, e.g. CONFIRMATION. Printed on the room's output card.")]
    [SerializeField] private string stageLabel;

    [Tooltip("Single character this room contributes to the final code.")]
    [SerializeField] private string codeCharacter;

    [Tooltip("What a correct solve tells the player about the launch warning.")]
    [TextArea]
    [SerializeField] private string correctEvidence;

    [Tooltip("Shown when the player confirms a wrong answer. Must still read as plausible.")]
    [TextArea]
    [SerializeField] private string misleadingEvidence;

    public string PuzzleId => puzzleId;
    public string PuzzleName => puzzleName;
    public bool PreserveProgressOnExit => preserveProgressOnExit;
    public string StageLabel => stageLabel;
    public string CorrectEvidence => correctEvidence;
    public string MisleadingEvidence => misleadingEvidence;

    /// <summary>
    /// First character of <see cref="codeCharacter"/>, or '?' when unset - the design
    /// requires every confirmed room to emit a character, even a wrong one.
    /// </summary>
    public char CodeCharacter => string.IsNullOrEmpty(codeCharacter) ? '?' : codeCharacter[0];
}
