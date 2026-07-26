using TMPro;
using UnityEngine;

/// <summary>
/// Screen-space countdown readout. Stands in for the wrist watch that was cut - the
/// player never sees a watch model, so the clock lives on the HUD instead.
///
/// Subscribes to <see cref="WatchManager"/> for the time and to
/// <see cref="PlayerController.OnFastForwardChanged"/> for the accelerating state, so the
/// readout visibly reacts while the player holds fast-forward and burns the clock.
/// </summary>
public class ClockHudView : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private TMP_Text clockLabel;

    [Tooltip("Shown only while time is accelerating. Optional.")]
    [SerializeField] private GameObject fastForwardIndicator;

    [Tooltip("Left empty, this finds the PlayerController in the scene on Start.")]
    [SerializeField] private PlayerController playerController;

    [Header("Colours")]
    [SerializeField] private Color normalColor = Color.white;

    [Tooltip("Applied while fast-forwarding, so the player can tell at a glance that "
             + "the clock is running away from them.")]
    [SerializeField] private Color acceleratedColor = new Color(1f, 0.45f, 0.2f);

    private WatchManager watchManager;
    private PlayerController boundController;

    private void Start()
    {
        Bind();
    }

    private void OnEnable()
    {
        Bind();
    }

    private void OnDisable()
    {
        Unbind();
    }

    /// <summary>
    /// Binds to whatever is available. Called from both OnEnable and Start because
    /// GameManager builds its WatchManager in Awake but a HUD enabled in the same frame
    /// can still run first; Start is the guaranteed second chance.
    /// </summary>
    private void Bind()
    {
        if (watchManager is null && GameManager.Instance != null)
        {
            watchManager = GameManager.Instance.WatchManager;

            if (watchManager is not null)
            {
                watchManager.OnTimeUpdated += UpdateDisplay;
                watchManager.OnTimeScaleChanged += HandleTimeScaleChanged;
                UpdateDisplay(watchManager.TimeRemaining);
                HandleTimeScaleChanged(watchManager.TimeScale);
            }
        }

        if (boundController == null)
        {
            if (playerController == null)
            {
                playerController = FindAnyObjectByType<PlayerController>();
            }

            if (playerController != null)
            {
                boundController = playerController;
                boundController.OnFastForwardChanged += HandleFastForwardChanged;
                HandleFastForwardChanged(boundController.IsFastForwarding);
            }
        }
    }

    private void Unbind()
    {
        if (watchManager is not null)
        {
            watchManager.OnTimeUpdated -= UpdateDisplay;
            watchManager.OnTimeScaleChanged -= HandleTimeScaleChanged;
            watchManager = null;
        }

        if (boundController != null)
        {
            boundController.OnFastForwardChanged -= HandleFastForwardChanged;
            boundController = null;
        }
    }

    private void UpdateDisplay(float timeRemaining)
    {
        if (clockLabel == null)
        {
            return;
        }

        var minutes = Mathf.FloorToInt(timeRemaining / 60f);
        var seconds = Mathf.FloorToInt(timeRemaining % 60f);
        clockLabel.text = $"{minutes:D2}:{seconds:D2}";
    }

    /// <summary>
    /// Driven by the watch rather than by input, so the HUD stays honest if anything
    /// else ever scales time.
    /// </summary>
    private void HandleTimeScaleChanged(float timeScale)
    {
        SetAcceleratedVisuals(timeScale > 1f);
    }

    private void HandleFastForwardChanged(bool isFastForwarding)
    {
        SetAcceleratedVisuals(isFastForwarding);
    }

    private void SetAcceleratedVisuals(bool isAccelerated)
    {
        if (clockLabel != null)
        {
            clockLabel.color = isAccelerated ? acceleratedColor : normalColor;
        }

        if (fastForwardIndicator != null)
        {
            fastForwardIndicator.SetActive(isAccelerated);
        }
    }
}
