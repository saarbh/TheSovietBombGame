using UnityEngine;

/// <summary>
/// Cigarette smoke while the player waits out the clock. Emits only while fast-forward
/// is held, which is what sells the time skip as the character killing time rather than
/// as a debug cheat.
///
/// The particle system is expected to sit under the camera and slightly forward, since
/// in first person the player cannot see their own face - the smoke has to drift up
/// through the lower part of the view to read at all.
/// </summary>
[RequireComponent(typeof(ParticleSystem))]
public class PlayerSmokeEffect : MonoBehaviour
{
    [Tooltip("Left empty, this walks up the hierarchy for the PlayerController.")]
    [SerializeField] private PlayerController playerController;

    private ParticleSystem smokeSystem;
    private PlayerController boundController;

    private void Awake()
    {
        smokeSystem = GetComponent<ParticleSystem>();

        if (playerController == null)
        {
            playerController = GetComponentInParent<PlayerController>();
        }
    }

    private void OnEnable()
    {
        if (playerController != null)
        {
            boundController = playerController;
            boundController.OnFastForwardChanged += HandleFastForwardChanged;
            HandleFastForwardChanged(boundController.IsFastForwarding);
        }
    }

    private void OnDisable()
    {
        if (boundController != null)
        {
            boundController.OnFastForwardChanged -= HandleFastForwardChanged;
            boundController = null;
        }
    }

    private void HandleFastForwardChanged(bool isFastForwarding)
    {
        if (smokeSystem == null)
        {
            return;
        }

        if (isFastForwarding)
        {
            smokeSystem.Play(true);
            return;
        }

        // StopEmitting rather than StopEmittingAndClear: the puffs already in the air
        // finish their lifetime and fade, so releasing the key doesn't blink them out.
        smokeSystem.Stop(true, ParticleSystemStopBehavior.StopEmitting);
    }
}
