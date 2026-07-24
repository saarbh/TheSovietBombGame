/// <summary>
/// Contract for objects that alter the game countdown.
/// </summary>
public interface ITimerModifier
{
    /// <summary>
    /// Applies a modification to the active countdown.
    /// </summary>
    void ApplyTimeModification(WatchManager watch);
}
