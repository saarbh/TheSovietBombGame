using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// Pure presentation for a door leaf: swings it open/closed around a hinge on one
/// edge (not the mesh centre). The hinge is an offset in the leaf's local space, so
/// no separate pivot GameObject is needed. Knows nothing about locks or combos -
/// <c>DoorComboLock</c> / <c>DoorLockController</c> decides when this runs.
/// </summary>
public class RoomDoor : MonoBehaviour
{
    [Tooltip("Transform that swings. Defaults to this GameObject.")]
    [SerializeField] private Transform doorLeaf;

    [Tooltip("Hinge position in the leaf's LOCAL space. For a unit-cube door, (-0.5,0,0) is the left edge and (0.5,0,0) the right edge. This is the side the door pivots on.")]
    [SerializeField] private Vector3 hingeLocalOffset = new Vector3(-0.5f, 0f, 0f);

    [Tooltip("Swing angle in degrees. Sign controls direction: flip it to swing the other way (e.g. into vs out of the room).")]
    [SerializeField] private float openAngle = 90f;

    [SerializeField] private float swingDuration = 0.8f;
    [SerializeField] private AnimationCurve swingEase = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Tooltip("Starting state. Set for doors that begin open.")]
    [SerializeField] private bool isOpen;

    public bool IsOpen => isOpen;

    /// <summary>Raised once the swing finishes, with the resulting open state.</summary>
    public event Action<bool> OnSwingCompleted;

    /// <summary>
    /// Raised as the swing begins, with the state being moved towards. Audio needs this rather
    /// than the completed event: a hinge creak has to start with the movement, and playing it
    /// at the end lands it a full swing late.
    /// </summary>
    public event Action<bool> OnSwingStarted;

    // World-space poses cached at Awake (doors are static once placed).
    private Vector3 closedPosition;
    private Quaternion closedRotation;
    private Vector3 openPosition;
    private Quaternion openRotation;

    private CancellationTokenSource swingCts;

    private void Awake()
    {
        if (doorLeaf == null)
        {
            doorLeaf = transform;
        }

        CachePoses();
        ApplyPose(isOpen ? 1f : 0f);
    }

    private void OnDestroy()
    {
        CancelActiveSwing();
    }

    /// <summary>
    /// Recomputes the closed/open poses from the current transform. Call after moving
    /// the door in the editor or changing the hinge/angle at runtime.
    /// </summary>
    public void CachePoses()
    {
        closedPosition = doorLeaf.position;
        closedRotation = doorLeaf.rotation;

        var hingeWorld = doorLeaf.TransformPoint(hingeLocalOffset);
        var delta = Quaternion.AngleAxis(openAngle, Vector3.up);

        // Rotate the whole leaf about the world-space hinge point.
        openRotation = delta * closedRotation;
        openPosition = hingeWorld + (delta * (closedPosition - hingeWorld));
    }

    public void OpenDoor()
    {
        SwingAsync(true).Forget();
    }

    public void CloseDoor()
    {
        SwingAsync(false).Forget();
    }

    /// <summary>
    /// Awaitable swing, for callers that need to sequence on the door finishing
    /// (cutscenes, chained puzzle steps).
    /// </summary>
    public async UniTask SwingAsync(bool open, CancellationToken externalToken = default)
    {
        if (isOpen == open)
        {
            return;
        }

        CancelActiveSwing();
        swingCts = CancellationTokenSource.CreateLinkedTokenSource(
            externalToken,
            this.GetCancellationTokenOnDestroy());

        var token = swingCts.Token;
        var fromOpen = isOpen ? 1f : 0f;
        var toOpen = open ? 1f : 0f;

        OnSwingStarted?.Invoke(open);

        try
        {
            if (swingDuration > 0f)
            {
                var elapsed = 0f;
                while (elapsed < swingDuration)
                {
                    token.ThrowIfCancellationRequested();
                    elapsed += Time.deltaTime;
                    var raw = Mathf.Clamp01(elapsed / swingDuration);
                    ApplyPose(Mathf.Lerp(fromOpen, toOpen, swingEase.Evaluate(raw)));
                    await UniTask.Yield(PlayerLoopTiming.Update, token);
                }
            }

            ApplyPose(toOpen);
            isOpen = open;
            OnSwingCompleted?.Invoke(isOpen);
        }
        catch (OperationCanceledException)
        {
            // Superseded by another swing or destroyed - leave the pose where it is.
        }
    }

    private void ApplyPose(float t)
    {
        doorLeaf.position = Vector3.Lerp(closedPosition, openPosition, t);
        doorLeaf.rotation = Quaternion.Slerp(closedRotation, openRotation, t);
    }

    private void CancelActiveSwing()
    {
        if (swingCts == null)
        {
            return;
        }

        swingCts.Cancel();
        swingCts.Dispose();
        swingCts = null;
    }
}
