using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Two-step missile launch control. The first interaction removes the safety
/// (arms the button); the second interaction launches. Attach to an object with a
/// Collider and wire the UnityEvents in the Inspector.
/// </summary>
[RequireComponent(typeof(Collider))]
public class MissileLaunchButton : MonoBehaviour, IInteractable
{
    [Header("Prompts")]
    [SerializeField] private string safetyPrompt = "[E] Remove Safety";
    [SerializeField] private string launchPrompt = "[E] LAUNCH";

    [Header("State")]
    [Tooltip("Starts with the safety on. Turn on to spawn already armed.")]
    [SerializeField] private bool isArmed;

    [Tooltip("If true, the missile can only be launched once.")]
    [SerializeField] private bool launchOnce = true;

    [Header("Events")]
    [Tooltip("First press: safety removed / cover flips. Hook cover animation, SFX, etc.")]
    [SerializeField] private UnityEvent onSafetyRemoved;

    [Tooltip("Second press: LAUNCH. Hook the launch effect here (explosion, sound, scene, ending...).")]
    [SerializeField] private UnityEvent onLaunch;

    /// <summary>Raised alongside the UnityEvent when the safety is removed (carries the presser).</summary>
    public event System.Action<PlayerController> SafetyRemoved;

    /// <summary>Raised alongside the UnityEvent when the missile launches (carries the presser).</summary>
    public event System.Action<PlayerController> Launched;

    private bool hasLaunched;

    public bool IsArmed => isArmed;
    public bool HasLaunched => hasLaunched;

    public void Interact(PlayerController player)
    {
        if (hasLaunched)
        {
            return;
        }

        if (!isArmed)
        {
            // Step 1 - remove the safety. The launch itself is a separate, deliberate press.
            isArmed = true;
            onSafetyRemoved?.Invoke();
            SafetyRemoved?.Invoke(player);
            return;
        }

        // Step 2 - launch.
        hasLaunched = launchOnce;
        onLaunch?.Invoke();
        Launched?.Invoke(player);
    }

    public string GetPrompt()
    {
        if (hasLaunched)
        {
            return string.Empty;
        }

        return isArmed ? launchPrompt : safetyPrompt;
    }
}
