/// <summary>
/// The Identification Room (CLASSIFICATION). A telemetry sheet reports the detected object's
/// readings; a wall chart gives the bands those readings fall into. The player sets one
/// switch per criterion - speed, altitude, heat - and pulls CLASSIFY, and the machine
/// announces what it thinks the object is.
///
/// The joke is that the machine is confidently specific and frequently wrong: the object is
/// moving far too slowly to be a missile, and only a correctly-read panel gets that out of it.
///
/// All behaviour lives in <see cref="SwitchComboPuzzle"/>; this room differs only in its
/// authored switch labels, correct indices and result text, so the subclass is empty but
/// for its log name.
/// </summary>
public class IdentificationPuzzle : SwitchComboPuzzle
{
    protected override string LogTag => "Identification";
}
