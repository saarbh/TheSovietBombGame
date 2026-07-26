using UnityEngine;

/// <summary>
/// Authoring data for a single locked room. The passcode is the only thing that opens
/// a door; the two unlock-time fields are retained authored data and gate nothing.
/// </summary>
[CreateAssetMenu(fileName = "RoomConfig", menuName = "SovietBomb/Room Config")]
public class RoomConfig : ScriptableObject
{
    [SerializeField] private string roomId;

    [Tooltip("INERT. Doors are gated on the passcode alone - nothing reads this to decide "
             + "whether a keypad works. Kept so the timed-door design can be restored without "
             + "re-authoring every RoomConfig asset.")]
    [SerializeField] private float actualUnlockTimeMinutes;

    [Tooltip("INERT. The minute the paperwork used to claim - no longer printed anywhere, "
             + "since a player cannot act on it. Kept alongside actualUnlockTimeMinutes.")]
    [SerializeField] private float expectedUnlockTimeMinutes;

    [SerializeField] private string correctPasscode;

    public string RoomId => roomId;
    public float ActualUnlockTimeMinutes => actualUnlockTimeMinutes;
    public float ExpectedUnlockTimeMinutes => expectedUnlockTimeMinutes;
    public string CorrectPasscode => correctPasscode;

    /// <summary>
    /// True when the paperwork lies about this room's unlock time.
    /// </summary>
    public bool HasMisleadingSchedule => !Mathf.Approximately(actualUnlockTimeMinutes, expectedUnlockTimeMinutes);

    public bool Matches(string enteredCode)
    {
        return !string.IsNullOrEmpty(correctPasscode) && correctPasscode == enteredCode;
    }
}
