using UnityEngine;

/// <summary>
/// Authoring data for a single locked room: when its door may actually be opened,
/// when the control room paperwork *claims* it may be opened, and the passcode.
/// </summary>
[CreateAssetMenu(fileName = "RoomConfig", menuName = "SovietBomb/Room Config")]
public class RoomConfig : ScriptableObject
{
    [SerializeField] private string roomId;

    [Tooltip("Minutes elapsed before the door will really accept its passcode.")]
    [SerializeField] private float actualUnlockTimeMinutes;

    [Tooltip("Minutes the in-world documentation claims the door unlocks. May differ from the actual time.")]
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
