using System;
using DG.Tweening;
using TMPro;
using UnityEngine;

/// <summary>
/// A rotary selector with a fixed set of labelled positions. Interacting advances it one
/// step and wraps around at the end.
///
/// This is the reusable half of the Identification Room. The Radar Room's three knobs, the
/// Radio Room's tuning dial and the Trajectory Room's snapping overlays are all this
/// component with a different option list, which is exactly why it knows nothing about any
/// puzzle: it reports its position and lets the room decide what that means.
/// </summary>
public class SelectorSwitch : MonoBehaviour, IInteractable
{
    [Header("Identity")]
    [Tooltip("Shown in the interaction prompt and the readout, e.g. SPEED.")]
    [SerializeField] private string switchLabel = "SWITCH";

    [Header("Options")]
    [Tooltip("Positions in order. Interacting steps forward and wraps back to the first.")]
    [SerializeField] private string[] options = Array.Empty<string>();

    [SerializeField] private int startIndex;

    [Header("Presentation")]
    [Tooltip("Optional. Rotated to show the current position - usually a knob or pointer.")]
    [SerializeField] private Transform pointer;

    [Tooltip("Local axis the pointer turns around.")]
    [SerializeField] private Vector3 rotationAxis = Vector3.up;

    [SerializeField] private float degreesPerStep = 40f;
    [SerializeField] private float stepSeconds = 0.12f;

    [Tooltip("Optional. Shows the label and the current position.")]
    [SerializeField] private TMP_Text readout;

    private int currentIndex;
    private Vector3 restEulers;
    private Tween stepTween;

    public string SwitchLabel => switchLabel;

    public int CurrentIndex => currentIndex;

    public int OptionCount => options.Length;

    public string CurrentOption => options.Length == 0 ? string.Empty : options[currentIndex];

    /// <summary>
    /// Set by the owning room once an answer is filed. A locked switch refuses input, which
    /// is how "the machinery switches off and the player cannot change the answer" is met.
    /// </summary>
    public bool IsLocked { get; set; }

    /// <summary>
    /// Raised only when the PLAYER moves the switch, never on initialisation or reset. Rooms
    /// use it to tell an untouched panel from a deliberately chosen answer, so an idle pull
    /// of the confirm lever cannot seal the room.
    /// </summary>
    public event Action<SelectorSwitch> OnSelectionChanged;

    private void Awake()
    {
        if (pointer != null)
        {
            restEulers = pointer.localEulerAngles;
        }

        ResetToStart();
    }

    private void OnDestroy()
    {
        stepTween?.Kill();
        stepTween = null;
    }

    public void Interact(PlayerController player)
    {
        if (options.Length == 0)
        {
            Debug.LogError($"[Interact] Switch '{switchLabel}' has no options authored.", this);
            return;
        }

        if (IsLocked)
        {
            Debug.Log($"[Interact] Switch '{switchLabel}' is locked - the answer is already filed.", this);
            return;
        }

        SetIndex((currentIndex + 1) % options.Length, instant: false);

        Debug.Log($"[Interact] Switch '{switchLabel}' -> {CurrentOption}", this);
        OnSelectionChanged?.Invoke(this);
    }

    public string GetPrompt()
    {
        if (options.Length == 0)
        {
            return $"{switchLabel} (unwired)";
        }

        if (IsLocked)
        {
            return $"{switchLabel}: {CurrentOption}";
        }

        return $"[E] {switchLabel}: {CurrentOption}";
    }

    /// <summary>
    /// Returns the switch to its authored starting position without raising
    /// <see cref="OnSelectionChanged"/> - a reset must not read as a player choice.
    /// </summary>
    public void ResetToStart()
    {
        IsLocked = false;
        SetIndex(options.Length == 0 ? 0 : Mathf.Clamp(startIndex, 0, options.Length - 1), instant: true);
    }

    /// <summary>Moves the switch programmatically. Does not raise the changed event.</summary>
    public void SetIndex(int index, bool instant)
    {
        if (options.Length == 0)
        {
            currentIndex = 0;
            return;
        }

        currentIndex = Mathf.Clamp(index, 0, options.Length - 1);

        ApplyPointerRotation(instant);
        RefreshReadout();
    }

    private void ApplyPointerRotation(bool instant)
    {
        if (pointer == null)
        {
            return;
        }

        // Computed as an absolute angle from rest rather than accumulated per step, so a
        // reset or a programmatic jump lands exactly on the authored position instead of
        // drifting by whatever the previous tween had reached.
        var target = restEulers + rotationAxis.normalized * (degreesPerStep * currentIndex);

        stepTween?.Kill();

        if (instant || stepSeconds <= 0f)
        {
            pointer.localEulerAngles = target;
            return;
        }

        stepTween = pointer.DOLocalRotate(target, stepSeconds).SetEase(Ease.OutBack);
    }

    private void RefreshReadout()
    {
        if (readout == null)
        {
            return;
        }

        readout.text = $"{switchLabel}\n{CurrentOption}";
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (options.Length > 0)
        {
            startIndex = Mathf.Clamp(startIndex, 0, options.Length - 1);
        }
    }
#endif
}
