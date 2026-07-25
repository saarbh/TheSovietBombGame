using UnityEngine;

/// <summary>
/// Voices a <see cref="PoweredProp"/> - the verification computer when the sync lands, and the
/// desk fan, projector, heated chair and portrait of Lenin that the misfire powers instead
/// when it does not.
///
/// The gag only works if you hear it: a fan that lights up but stays silent reads as a shader
/// bug. The prop keeps a running loop for as long as it holds power and a thunk at either end.
/// </summary>
public class PoweredPropAudio : MonoBehaviour
{
    [Header("Source")]
    [SerializeField] private PoweredProp poweredProp;

    [Header("Loop")]
    [Tooltip("Runs while the prop has power - a fan, a projector, a cooling system. Optional.")]
    [SerializeField] private RoomLoopSource runningLoop;

    [Header("One-shots")]
    [SerializeField] private SfxId powerOnSfx = SfxId.PropPowerUp;

    [SerializeField] private SfxId powerOffSfx = SfxId.PropPowerDown;

    private SfxBank roomBank;

    private void Awake()
    {
        if (poweredProp == null)
        {
            poweredProp = GetComponent<PoweredProp>();
        }

        roomBank = RoomAudioZone.BankFor(this);
    }

    private void OnEnable()
    {
        if (poweredProp == null)
        {
            Debug.LogError("[Audio] PoweredPropAudio has no PoweredProp; this prop will be silent.", this);
            return;
        }

        poweredProp.OnPowerChanged += HandlePowerChanged;

        // Seeded rather than waited for: PoweredProp applies its authored starting state in
        // Awake, which is before this subscription exists.
        SetLoop(poweredProp.IsPowered);
    }

    private void OnDisable()
    {
        if (poweredProp == null)
        {
            return;
        }

        poweredProp.OnPowerChanged -= HandlePowerChanged;
    }

    private void HandlePowerChanged(bool isPowered)
    {
        SetLoop(isPowered);

        var id = isPowered ? powerOnSfx : powerOffSfx;

        if (id == SfxId.None)
        {
            return;
        }

        AudioManager.PlayAt(id, transform.position, roomBank);
    }

    private void SetLoop(bool isPowered)
    {
        if (runningLoop == null)
        {
            return;
        }

        runningLoop.SetGameplayActive(isPowered);
    }
}
