using System;
using DG.Tweening;
using UnityEngine;

/// <summary>
/// Slides a door panel along its own local X axis once a room's puzzle is resolved -
/// correct answer or wrong one. A wrong answer still ends the player's business in the
/// room, and locking them in after they filed a card would be a dead end with a
/// seven-minute clock running.
///
/// Orient the panel so its local X runs along the wall, then set <see cref="slideDistance"/>
/// to roughly the door's width so it tucks out of the opening.
///
/// Point <see cref="puzzleObject"/> at the GameObject carrying the room's puzzle; it is
/// read as <see cref="IPuzzleResolution"/>, so this works for any <c>BasePuzzle&lt;T&gt;</c>
/// without knowing which room it belongs to.
/// </summary>
public class SlidingDoor : MonoBehaviour
{
    [Header("Puzzle")]
    [Tooltip("GameObject carrying the room's puzzle. Leave empty to drive the door manually via Open().")]
    [SerializeField] private GameObject puzzleObject;

    [Header("Motion")]
    [Tooltip("The panel that moves. Defaults to this transform.")]
    [SerializeField] private Transform doorPanel;

    [Tooltip("How far the panel slides along its own local X axis. Negative slides the other way.")]
    [SerializeField] private float slideDistance = 1.85f;

    [SerializeField] private float slideSeconds = 1.4f;

    [SerializeField] private Ease slideEase = Ease.InOutSine;

    [Tooltip("Seconds to wait after the room resolves before the door starts moving, so the printed card gets a beat.")]
    [SerializeField] private float openDelaySeconds = 0.4f;

    private IPuzzleResolution puzzle;
    private Vector3 closedLocalPosition;
    private Vector3 openLocalPosition;
    private Tween slideTween;

    public bool IsOpen { get; private set; }

    /// <summary>Raised when the door finishes opening, for audio and prompt hooks.</summary>
    public event Action OnOpened;

    private void Awake()
    {
        if (doorPanel == null)
        {
            doorPanel = transform;
        }

        closedLocalPosition = doorPanel.localPosition;

        // The panel's OWN local X, i.e. transform.right - not localPosition.x, which runs
        // along the parent's axes and ignores how the panel is rotated. Converted back into
        // parent space so the tween stays local and survives the parent moving.
        var worldSlide = doorPanel.right * slideDistance;
        var localSlide = doorPanel.parent != null
            ? doorPanel.parent.InverseTransformVector(worldSlide)
            : worldSlide;

        openLocalPosition = closedLocalPosition + localSlide;

        if (puzzleObject != null)
        {
            // GetComponent resolves interfaces; the serialized field has to be a
            // GameObject because Unity cannot serialize an interface reference.
            puzzle = puzzleObject.GetComponent<IPuzzleResolution>();

            if (puzzle == null)
            {
                Debug.LogError($"[SlidingDoor] '{puzzleObject.name}' has no IPuzzleResolution component.", this);
            }
        }
    }

    private void OnEnable()
    {
        if (puzzle == null)
        {
            return;
        }

        puzzle.OnResolved += HandlePuzzleResolved;

        // A room resolved before this door was enabled must not stay shut forever.
        if (puzzle.IsResolved && !IsOpen)
        {
            Open();
        }
    }

    private void OnDisable()
    {
        if (puzzle != null)
        {
            puzzle.OnResolved -= HandlePuzzleResolved;
        }

        KillTween();
    }

    private void OnDestroy()
    {
        KillTween();
    }

    /// <summary>Slides the panel open. Safe to call more than once.</summary>
    public void Open()
    {
        if (IsOpen)
        {
            return;
        }

        IsOpen = true;
        Debug.Log($"[SlidingDoor] '{name}' opening.", this);

        KillTween();

        slideTween = doorPanel
            .DOLocalMove(openLocalPosition, slideSeconds)
            .SetEase(slideEase)
            .SetDelay(openDelaySeconds)
            // The pause menu freezes timeScale; a door mid-slide should freeze with it,
            // so this deliberately does NOT SetUpdate(true).
            .OnComplete(() => OnOpened?.Invoke());
    }

    /// <summary>Slides the panel back. Used when a scene resets rather than during play.</summary>
    public void Close()
    {
        if (!IsOpen)
        {
            return;
        }

        IsOpen = false;
        KillTween();

        slideTween = doorPanel
            .DOLocalMove(closedLocalPosition, slideSeconds)
            .SetEase(slideEase);
    }

    /// <summary>Snaps shut with no animation, for scene setup and editor previews.</summary>
    public void SnapClosed()
    {
        KillTween();
        IsOpen = false;
        doorPanel.localPosition = closedLocalPosition;
    }

    private void HandlePuzzleResolved(bool wasCorrect)
    {
        Debug.Log($"[SlidingDoor] Room resolved (correct={wasCorrect}) - opening the exit.", this);
        Open();
    }

    private void KillTween()
    {
        if (slideTween == null)
        {
            return;
        }

        slideTween.Kill();
        slideTween = null;
    }
}
