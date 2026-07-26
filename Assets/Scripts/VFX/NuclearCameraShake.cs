using Unity.Cinemachine;
using UnityEngine;

/// <summary>
/// Fires a <see cref="CinemachineImpulseSource"/> for the nuclear blast. Impulse is
/// event-driven - the source emits nothing until something calls GenerateImpulse - so
/// this is the trigger that was missing.
///
/// <see cref="Detonate"/> is public and parameterless, so it can be driven from a
/// Timeline Signal, an Animation Event, or a UnityEvent on a button, as well as from code.
/// The shockwave delay is handled here: the flash goes off, then the blast reaches the
/// camera a beat later, which is what sells the scale.
/// </summary>
[RequireComponent(typeof(CinemachineImpulseSource))]
public class NuclearCameraShake : MonoBehaviour
{
    [SerializeField] private CinemachineImpulseSource impulseSource;

    [Header("Timing")]
    [Tooltip("Seconds between the flash and the blast wave reaching the camera. 0 = instant.")]
    [SerializeField] private float shockwaveDelay = 1.5f;

    [Tooltip("Fire automatically on Start - handy while dressing the scene.")]
    [SerializeField] private bool playOnStart = true;

    [Header("Force")]
    [Tooltip("Impulse strength. Scales the source's own amplitude.")]
    [SerializeField] private float force = 5f;

    [Tooltip("Direction of the kick. Down/back reads like a blast wave shoving the camera.")]
    [SerializeField] private Vector3 velocity = new Vector3(0f, -1f, -1f);

    [Header("Skybox")]
    [Tooltip("Skybox swapped in on Start - assign nuclearnightBB (Assets/assets/Materials/skyboxHDR).")]
    [SerializeField] private Material startSkybox;

    private float timer = -1f;
    private Material previousSkybox;

    private void Awake()
    {
        if (impulseSource == null)
        {
            impulseSource = GetComponent<CinemachineImpulseSource>();
        }
    }

    private void Start()
    {
        ApplyStartSkybox();

        if (playOnStart)
        {
            Detonate();
        }
    }

    /// <summary>
    /// Swaps the scene skybox (nuclearnight) for the blast one (nuclearnightBB).
    /// RenderSettings.skybox is global and persists across play sessions in the editor,
    /// so the scene's own material is restored on destroy.
    /// </summary>
    private void ApplyStartSkybox()
    {
        if (startSkybox == null)
        {
            return;
        }

        previousSkybox = RenderSettings.skybox;
        RenderSettings.skybox = startSkybox;
    }

    private void OnDestroy()
    {
        if (previousSkybox != null)
        {
            RenderSettings.skybox = previousSkybox;
        }
    }

    /// <summary>Start the blast. Waits <see cref="shockwaveDelay"/>, then shakes.</summary>
    public void Detonate()
    {
        if (shockwaveDelay <= 0f)
        {
            Fire();
            return;
        }

        timer = 0f;
    }

    /// <summary>Shake immediately, skipping the delay.</summary>
    public void Fire()
    {
        timer = -1f;

        if (impulseSource == null)
        {
            return;
        }

        // The vector both aims and scales the kick, so the force multiplier goes here
        // rather than on the source asset - lets one source serve several blast sizes.
        impulseSource.GenerateImpulseWithVelocity(velocity.normalized * force);
    }

    private void Update()
    {
        if (timer < 0f)
        {
            return;
        }

        timer += Time.deltaTime;

        if (timer >= shockwaveDelay)
        {
            Fire();
        }
    }
}
