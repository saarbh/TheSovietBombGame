/// <summary>
/// The Radar Detection Room (DETECTION). A radar screen shows several repeating blips and
/// an operator note describes what a genuine missile track looks like - it appears on three
/// consecutive sweeps, travels inward, and does not wobble. Three knobs (range, sweep speed,
/// signal filter) let the player tune the screen until only that track remains, and pulling
/// LOCK TARGET files the detection.
///
/// Per the design doc this does NOT simulate radar: it is one correct knob combination, read
/// off the operator note, exactly like the Identification panel. All behaviour lives in
/// <see cref="SwitchComboPuzzle"/>; this subclass exists only to give the room its own
/// component type and log name.
/// </summary>
public class RadarPuzzle : SwitchComboPuzzle
{
    protected override string LogTag => "Radar";
}
