using System;

/// <summary>
/// Non-generic view of a puzzle's outcome, so scene objects can react to a room being
/// finished without knowing which <see cref="BasePuzzle{T}"/> it is. A serialized field
/// cannot reference an open generic, and it cannot hold an interface either - components
/// take a GameObject reference and resolve this via GetComponent.
///
/// "Resolved" is not the same as "solved": a room is resolved the moment the player files
/// an answer, right or wrong. Doors open either way; only the printed card differs.
/// </summary>
public interface IPuzzleResolution
{
    /// <summary>True once the player has filed an answer, correct or not.</summary>
    bool IsResolved { get; }

    /// <summary>True when the filed answer was the correct one.</summary>
    bool IsSolved { get; }

    /// <summary>Raised once when the room is resolved. The flag is whether it was correct.</summary>
    event Action<bool> OnResolved;
}
