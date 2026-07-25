using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// Pure presentation for a door leaf: swings <see cref="doorPivot"/> open and closed.
/// Knows nothing about locks or passcodes - <c>DoorLockController</c> decides when this runs.
/// </summary>
public class RoomDoor : MonoBehaviour
{
    [SerializeField] private Transform doorPivot;

    [Tooltip("Degrees around the pivot's local Y axis when fully open.")]
    [SerializeField] private float openAngle = 90f;

    [SerializeField] private float swingDuration = 0.8f;
    [SerializeField] private AnimationCurve swingEase = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Tooltip("Starting state. Set in the Inspector for doors that begin open.")]
    [SerializeField] private bool isOpen;

    public bool IsOpen => isOpen;

    /// <summary>Raised once the swing finishes, with the resulting open state.</summary>
    public event Action<bool> OnSwingCompleted;

    private Quaternion closedRotation;
    private Quaternion openRotation;
    private CancellationTokenSource swingCts;

    private void Awake()
    {
        if (doorPivot == null)
        {
            doorPivot = transform;
        }

        closedRotation = doorPivot.localRotation;
        openRotation = closedRotation * Quaternion.Euler(0f, openAngle, 0f);
        doorPivot.localRotation = isOpen ? openRotation : closedRotation;
    }

    private void OnDestroy()
    {
        CancelActiveSwing();
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
        var from = doorPivot.localRotation;
        var to = open ? openRotation : closedRotation;

        try
        {
            if (swingDuration > 0f)
            {
                var elapsed = 0f;
                while (elapsed < swingDuration)
                {
                    token.ThrowIfCancellationRequested();
                    elapsed += Time.deltaTime;
                    var t = swingEase.Evaluate(Mathf.Clamp01(elapsed / swingDuration));
                    doorPivot.localRotation = Quaternion.Slerp(from, to, t);
                    await UniTask.Yield(PlayerLoopTiming.Update, token);
                }
            }

            doorPivot.localRotation = to;
            isOpen = open;
            OnSwingCompleted?.Invoke(isOpen);
        }
        catch (OperationCanceledException)
        {
            // Superseded by another swing or the door was destroyed - leave the
            // rotation where it is so the replacement swing blends from here.
        }
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
