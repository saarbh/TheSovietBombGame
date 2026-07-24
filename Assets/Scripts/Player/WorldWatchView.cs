using TMPro;
using UnityEngine;

/// <summary>
/// Controls the diegetic 3D in-world wrist watch display, subscribing to WatchManager
/// for time updates to update the formatted clock text.
/// </summary>
public class WorldWatchView : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private TextMeshPro watchDisplay;

    private WatchManager watchManager;

    private void Start()
    {
        BindWatchManager();
    }

    private void OnEnable()
    {
        BindWatchManager();
    }

    private void OnDisable()
    {
        if (watchManager != null)
        {
            watchManager.OnTimeUpdated -= UpdateDisplay;
        }
    }

    private void BindWatchManager()
    {
        if (watchManager == null && GameManager.Instance != null)
        {
            watchManager = GameManager.Instance.WatchManager;
        }

        if (watchManager != null)
        {
            watchManager.OnTimeUpdated -= UpdateDisplay;
            watchManager.OnTimeUpdated += UpdateDisplay;
            UpdateDisplay(watchManager.TimeRemaining);
        }
    }

    /// <summary>
    /// Updates the text display with the remaining time formatted.
    /// </summary>
    public void UpdateDisplay(float timeRemaining)
    {
        if (watchDisplay != null)
        {
            var minutes = Mathf.FloorToInt(timeRemaining / 60f);
            var seconds = Mathf.FloorToInt(timeRemaining % 60f);
            watchDisplay.text = $"{minutes:D2}:{seconds:D2}";
        }
    }

    /// <summary>
    /// Play a beep sound.
    /// </summary>
    public void PlayWatchBeep()
    {
        throw new System.NotImplementedException("PlayWatchBeep is not implemented yet.");
        // TODO: Can be hooked to AudioManager.Instance.PlaySFX(SFXType.WatchBeep) when audio system is implemented
    }
}
