using TMPro;
using UnityEngine;
using DG.Tweening;

/// <summary>
/// Controls the diegetic 3D in-world wrist watch visual presentation, subscribing to WatchManager
/// for time updates and animating 3D local position/rotation when toggled.
/// </summary>
public class WorldWatchView : MonoBehaviour
{
    [Header("3D World References")]
    [SerializeField] private TextMeshPro watchDisplay;
    [SerializeField] private Transform watchTransform;

    [Header("3D Slide & Rotation Offsets (Local Space)")]
    [SerializeField] private Vector3 hiddenLocalPosition = new Vector3(0.3f, -0.6f, 0.4f);
    [SerializeField] private Vector3 raisedLocalPosition = new Vector3(0f, -0.2f, 0.4f);
    [SerializeField] private Vector3 hiddenLocalRotation = new Vector3(45f, -30f, 0f);
    [SerializeField] private Vector3 raisedLocalRotation = Vector3.zero;

    [Header("Animation Config")]
    [SerializeField] private float transitionDuration = 0.4f;
    [SerializeField] private Ease transitionEase = Ease.OutBack;

    private WatchManager watchManager;
    private bool isWatchRaised;
    private Sequence animationSequence;

    private Transform TargetTransform => watchTransform != null ? watchTransform : transform;

    private void Awake()
    {
        TargetTransform.localPosition = hiddenLocalPosition;
        TargetTransform.localEulerAngles = hiddenLocalRotation;
    }

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
    /// Toggles the watch raised/lowered visual state.
    /// </summary>
    public void ToggleWatch()
    {
        isWatchRaised = !isWatchRaised;
        AnimateWatch(isWatchRaised);
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

    private void AnimateWatch(bool isWatching)
    {
        animationSequence?.Kill();

        var targetPos = isWatching ? raisedLocalPosition : hiddenLocalPosition;
        var targetRot = isWatching ? raisedLocalRotation : hiddenLocalRotation;

        animationSequence = DOTween.Sequence()
            .Join(TargetTransform.DOLocalMove(targetPos, transitionDuration).SetEase(transitionEase))
            .Join(TargetTransform.DOLocalRotate(targetRot, transitionDuration).SetEase(transitionEase))
            .SetUpdate(true);
    }
}
