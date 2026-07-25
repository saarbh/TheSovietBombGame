using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// A room's audio identity, sitting on the room root. It does two jobs that always travel
/// together:
///
/// 1. It carries the room's optional <see cref="SfxBank"/>. Anything inside the room that
///    makes a noise asks its zone first and falls through to the game default, so a room with
///    no bank still sounds like something and a room with one only has to declare the handful
///    of sounds that should differ.
/// 2. It switches every <see cref="RoomLoopSource"/> beneath it on and off as the player
///    enters and leaves, so three generator hums, a cooling fan and a room tone are only being
///    decoded in the one room the player is standing in.
///
/// Occupancy is decided by testing the player against the volume, not by counting trigger
/// events. Trigger callbacks only prompt an immediate re-test. A CharacterController that is
/// repositioned rather than moved does not reliably raise OnTriggerExit, and a missed exit
/// under a counting scheme leaves the room humming for the rest of the run - which is the
/// exact cost this component exists to avoid. Testing containment cannot get stuck.
///
/// The volume must be on a layer excluded from <c>PlayerInteraction.interactableMask</c>, or a
/// room-sized collider will swallow the interaction raycast and nothing in the room will be
/// usable.
/// </summary>
[DisallowMultipleComponent]
public class RoomAudioZone : MonoBehaviour
{
    [Header("Identity")]
    [Tooltip("Optional. Sounds this room wants to differ from the game default. Anything not "
             + "declared here falls through to AudioManager's default bank.")]
    [SerializeField] private SfxBank roomBank;

    [Header("Zone")]
    [Tooltip("Volume covering the room interior. Defaults to a collider on this object.")]
    [SerializeField] private Collider zoneVolume;

    [Tooltip("Seconds between safety re-tests. Trigger events already give an instant response; "
             + "this only exists so a missed one cannot leave the room stuck.")]
    [SerializeField] private float reconcileInterval = 0.5f;

    [Header("Managed loops")]
    [Tooltip("Leave empty to take every RoomLoopSource under this root at Awake, which is what "
             + "a room authored normally wants.")]
    [SerializeField] private RoomLoopSource[] managedLoops = Array.Empty<RoomLoopSource>();

    private PlayerController player;
    private bool isPlayerInside;

    /// <summary>This room's bank, or null when it just uses the game default.</summary>
    public SfxBank RoomBank => roomBank;

    /// <summary>True while the player is inside the volume.</summary>
    public bool IsPlayerInside => isPlayerInside;

    /// <summary>Raised as the player enters and leaves, for anything else the room wants to gate.</summary>
    public event Action<bool> OnOccupancyChanged;

    /// <summary>
    /// The bank that applies to a given object, or null when nothing in its parents declares
    /// one. Cache the result - this walks the hierarchy and is not for per-frame use.
    /// </summary>
    public static SfxBank BankFor(Component context)
    {
        if (context == null)
        {
            return null;
        }

        var zone = context.GetComponentInParent<RoomAudioZone>();

        return zone == null ? null : zone.roomBank;
    }

    private void Awake()
    {
        if (zoneVolume == null)
        {
            zoneVolume = GetComponent<Collider>();
        }

        if (zoneVolume == null)
        {
            Debug.LogError($"[Audio] Zone '{name}' has no volume; its loops will never switch on.", this);
        }
        else if (!zoneVolume.isTrigger)
        {
            Debug.LogWarning($"[Audio] Zone '{name}' has a non-trigger collider; the player will "
                             + "walk into it instead of through it.", this);
        }

        if (managedLoops.Length == 0)
        {
            // Inactive included: a loop on a prop that starts switched off still has to be
            // registered, or it will never be gated when the prop comes back.
            managedLoops = GetComponentsInChildren<RoomLoopSource>(true);
        }
    }

    private void Start()
    {
        // Start, not Awake: RoomLoopSource decides its own default in Awake and reads it in
        // Start, so pushing the zone state from here lands after that and wins.
        Evaluate(force: true);
        ReconcileAsync(this.GetCancellationTokenOnDestroy()).Forget();
    }

    private void OnTriggerEnter(Collider other)
    {
        Evaluate(force: false);
    }

    private void OnTriggerExit(Collider other)
    {
        Evaluate(force: false);
    }

    /// <summary>
    /// The safety net. Cheap enough to be uninteresting - one closest-point test per room twice
    /// a second - and it is what makes a missed trigger event self-correct instead of
    /// permanent.
    /// </summary>
    private async UniTaskVoid ReconcileAsync(CancellationToken token)
    {
        var interval = Mathf.Max(0.1f, reconcileInterval);

        try
        {
            while (true)
            {
                // ignoreTimeScale so occupancy still tracks while a modal has the game paused.
                await UniTask.Delay(
                    TimeSpan.FromSeconds(interval),
                    DelayType.UnscaledDeltaTime,
                    cancellationToken: token);

                Evaluate(force: false);
            }
        }
        catch (OperationCanceledException)
        {
            // Room destroyed. Nothing to unwind: the loops go with it.
        }
    }

    private void Evaluate(bool force)
    {
        var isInside = TestContainsPlayer();

        if (!force && isInside == isPlayerInside)
        {
            return;
        }

        isPlayerInside = isInside;

        foreach (var loop in managedLoops)
        {
            if (loop == null)
            {
                continue;
            }

            loop.SetZoneActive(isInside);
        }

        OnOccupancyChanged?.Invoke(isInside);
    }

    private bool TestContainsPlayer()
    {
        if (zoneVolume == null)
        {
            return false;
        }

        if (player == null)
        {
            // Last-resort lookup rather than a serialized field: the zones live in the level
            // and the player is a separate object that a future spawner may create at runtime.
            player = FindFirstObjectByType<PlayerController>();

            if (player == null)
            {
                return false;
            }
        }

        // ClosestPoint rather than bounds.Contains, which is the axis-aligned box around the
        // collider and would leak into the corridor for any room placed at an angle.
        var position = player.transform.position;

        return (zoneVolume.ClosestPoint(position) - position).sqrMagnitude < 0.0001f;
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        var volume = zoneVolume != null ? zoneVolume : GetComponent<Collider>();

        if (volume == null)
        {
            return;
        }

        Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.15f);
        var bounds = volume.bounds;
        Gizmos.DrawCube(bounds.center, bounds.size);
    }
#endif
}
