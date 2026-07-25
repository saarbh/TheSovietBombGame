using UnityEngine;

/// <summary>
/// Voices one <see cref="GeneratorUnit"/>: the clunk of the lever engaging, the eight seconds
/// of spin-up whine, the chime as it lands, the steady hum while it holds power, and the
/// wind-down when the reset lever drops it back to idle.
///
/// Both loops are <see cref="RoomLoopSource"/>s, so three generators humming in Room 1 stop
/// costing anything the moment the player walks out.
/// </summary>
public class GeneratorAudio : MonoBehaviour
{
    [Header("Source")]
    [SerializeField] private GeneratorUnit generator;

    [Header("Loops")]
    [Tooltip("Rising whine while the generator is spinning up. Optional.")]
    [SerializeField] private RoomLoopSource spinUpLoop;

    [Tooltip("Steady hum while the generator holds full power. Optional.")]
    [SerializeField] private RoomLoopSource runningLoop;

    [Header("One-shots")]
    [SerializeField] private SfxId startSfx = SfxId.GeneratorStart;

    [SerializeField] private SfxId readySfx = SfxId.GeneratorReady;

    [Tooltip("Played when a running or spinning generator is dropped back to idle by RESET.")]
    [SerializeField] private SfxId stopSfx = SfxId.GeneratorStop;

    private SfxBank roomBank;
    private GeneratorUnit.GeneratorState previousState = GeneratorUnit.GeneratorState.Idle;

    private void Awake()
    {
        if (generator == null)
        {
            generator = GetComponent<GeneratorUnit>();
        }

        roomBank = RoomAudioZone.BankFor(this);
    }

    private void OnEnable()
    {
        if (generator == null)
        {
            Debug.LogError("[Audio] GeneratorAudio has no GeneratorUnit; this generator will be silent.", this);
            return;
        }

        previousState = generator.State;
        generator.OnStateChanged += HandleStateChanged;
    }

    private void OnDisable()
    {
        if (generator == null)
        {
            return;
        }

        generator.OnStateChanged -= HandleStateChanged;
    }

    private void HandleStateChanged(GeneratorUnit.GeneratorState state)
    {
        switch (state)
        {
            case GeneratorUnit.GeneratorState.Starting:
                Play(startSfx);
                SetLoop(spinUpLoop, true);
                SetLoop(runningLoop, false);
                break;

            case GeneratorUnit.GeneratorState.AtFullPower:
                Play(readySfx);
                SetLoop(spinUpLoop, false);
                SetLoop(runningLoop, true);
                break;

            default:
                SetLoop(spinUpLoop, false);
                SetLoop(runningLoop, false);

                // Only audible coming down from something. A generator that was already idle
                // has nothing to wind down, and the reset lever touches all three at once.
                if (previousState != GeneratorUnit.GeneratorState.Idle)
                {
                    Play(stopSfx);
                }

                break;
        }

        previousState = state;
    }

    private void Play(SfxId id)
    {
        if (id == SfxId.None)
        {
            return;
        }

        AudioManager.PlayAt(id, transform.position, roomBank);
    }

    private static void SetLoop(RoomLoopSource loop, bool isActive)
    {
        if (loop == null)
        {
            return;
        }

        loop.SetGameplayActive(isActive);
    }
}
