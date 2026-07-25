using System;

/// <summary>
/// Non-generic view of a puzzle's outcome, so scene objects can react to a room being
/// finished without knowing which <see cref="BasePuzzle{T}"/> it is. A serialized field
/// cannot reference an open generic, and it cannot hold an interface either - components
/// take a GameObject reference and resolve this via GetComponent.
///
/// "Resolved" is not the same as "solved": a room is resolved the moment the player files an
/// answer. Whether a WRONG answer can be filed at all is now the room's own choice - a room
/// that rejects wrong answers hands the attempt back instead, raises
/// <see cref="OnAttemptRejected"/>, and only ever resolves correct. Listeners that must react
/// to a failed attempt need that event, not <see cref="OnResolved"/>.
/// </summary>
public interface IPuzzleResolution
{
    /// <summary>True once the player has filed an answer, correct or not.</summary>
    bool IsResolved { get; }

    /// <summary>True when the filed answer was the correct one.</summary>
    bool IsSolved { get; }

    /// <summary>Raised once when the room is resolved. The flag is whether it was correct.</summary>
    event Action<bool> OnResolved;

    /// <summary>
    /// Raised when an attempt was judged wrong and handed back rather than filed. The room is
    /// NOT resolved, no card exists, and the player is about to get the panel returned to them.
    /// May fire any number of times before the room finally resolves.
    /// </summary>
    event Action OnAttemptRejected;
}
