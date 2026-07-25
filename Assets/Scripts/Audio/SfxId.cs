/// <summary>
/// Every one-shot sound the game asks for, named by what happened rather than by which clip
/// plays. Rooms bind these to their own clips through an <see cref="SfxBank"/>, so the same
/// call site ("a lever was pulled") sounds like a generator breaker in Room 1 and a radar
/// lock in Room 2 without a single line of code changing.
///
/// Values are explicit so a bank asset authored today still points at the same sound after
/// someone inserts an entry in the middle of this list. Never renumber an existing member.
/// </summary>
public enum SfxId
{
    /// <summary>Play nothing. Used by objects whose sound is driven by a dedicated hook.</summary>
    None = 0,

    // --- Interaction ---------------------------------------------------------------------
    /// <summary>Fallback press sound for any interactable that declares nothing of its own.</summary>
    InteractGeneric = 1,

    /// <summary>The press was refused - a sealed panel, a locked switch.</summary>
    InteractBlocked = 2,

    /// <summary>The crosshair moved onto something usable.</summary>
    TargetAcquired = 3,

    // --- Shared room furniture -----------------------------------------------------------
    LeverConfirm = 10,
    LeverReset = 11,
    SwitchStep = 12,
    KeypadPress = 13,

    // --- Doors ---------------------------------------------------------------------------
    DoorOpen = 20,
    DoorClose = 21,
    DoorLocked = 22,

    // --- Puzzle outcome ------------------------------------------------------------------
    PuzzleSolved = 30,
    PuzzleFailed = 31,
    CardFiled = 32,

    // --- Generator room ------------------------------------------------------------------
    GeneratorStart = 40,
    GeneratorReady = 41,
    GeneratorStop = 42,

    /// <summary>The misfire gag: the surge powers a desk fan instead of the computer.</summary>
    PropPowerUp = 43,

    /// <summary>That prop losing power again.</summary>
    PropPowerDown = 44,

    // --- Watch ---------------------------------------------------------------------------
    WatchBeep = 50
}
