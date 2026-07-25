/// <summary>
/// The bunker's official verification procedure, in the order the central console's
/// slots are labelled:
///
///     Detect -> Confirm -> Classify -> Trace -> Authenticate -> Authorize -> Report
///
/// Declaration order IS procedural order - the finale assembles the final code by sorting
/// the cards the player holds on this enum's underlying value. Rooms unlock in a
/// deliberately scrambled order, so the order the player earns cards in is NOT the order
/// the code is read in; that mismatch is the point of the central decoder.
///
/// Never reorder or renumber these members without reordering the console's slots and
/// re-checking every PuzzleConfig asset, since the value is what gets serialized.
/// </summary>
public enum VerificationStage
{
    Detect = 0,
    Confirm = 1,
    Classify = 2,
    Trace = 3,
    Authenticate = 4,
    Authorize = 5,
    Report = 6,
}
