/// <summary>
/// A room the player commits an answer to by pulling a lever. Per the design doc nothing
/// in a room locks until CONFIRM: the player experiments freely, then files - right or wrong.
///
/// Exists so <see cref="ConfirmLever"/> and <see cref="ResetLever"/> are written once rather
/// than once per room. A serialized field cannot hold an interface, so the levers take a
/// GameObject and resolve this through GetComponent, the same way <see cref="PuzzleDoorOpener"/>
/// resolves <see cref="IPuzzleResolution"/>.
/// </summary>
public interface IConfirmablePuzzle
{
    /// <summary>True once an answer has been filed; the room can no longer be changed.</summary>
    bool IsConfirmed { get; }

    /// <summary>True when the attempt is complete enough to be worth filing.</summary>
    bool CanConfirm { get; }

    /// <summary>
    /// Player-facing reason CONFIRM is currently refused, or null when it is available.
    /// The lever shows this instead of inventing its own wording, so a room can explain
    /// its own guard.
    /// </summary>
    string ConfirmBlockedReason { get; }

    /// <summary>
    /// Judges the attempt. Returns the printed card when the answer was filed, or <c>null</c>
    /// when the room refused it - either because the attempt was not fileable, or because the
    /// room rejects wrong answers and is handing this one back for another go.
    ///
    /// Nullable rather than always-a-card: a refused attempt must not print, and a sentinel
    /// card would be indistinguishable from a filed one at the call site.
    /// </summary>
    PuzzleCard? Confirm();

    /// <summary>True while the player may still scrap the attempt and retry for free.</summary>
    bool CanReset { get; }

    /// <summary>Returns the room to its starting state. Refused once confirmed.</summary>
    void ResetAttempt();
}
