using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;

/// <summary>
/// One emergency generator. Pulling its lever begins a fixed startup countdown; when the
/// countdown reaches zero the generator is at full power and records the moment it got
/// there. <see cref="GeneratorPuzzle"/> compares those moments across all three units.
/// </summary>
public class GeneratorUnit : MonoBehaviour, IInteractable
{
    public enum GeneratorState
    {
        Idle,
        Starting,
        AtFullPower
    }

    [Header("Identity")]
    [Tooltip("Shown in the interaction prompt and on the countdown display, e.g. \"A\".")]
    [SerializeField] private string generatorLabel = "A";

    [Header("Config")]
    [Tooltip("Seconds from lever pull to full power. The doc's values are A=3, B=5, C=8.")]
    [SerializeField] private float startupSeconds = 3f;

    [Header("Presentation")]
    [Tooltip("Optional. Shows the remaining startup seconds so the mechanic reads at a glance.")]
    [SerializeField] private TMP_Text countdownDisplay;

    [Tooltip("Optional. Lit while the generator is at full power.")]
    [SerializeField] private Renderer statusLamp;

    [SerializeField] private Color idleLampColor = new Color(0.35f, 0.05f, 0.05f);
    [SerializeField] private Color startingLampColor = new Color(0.85f, 0.6f, 0.1f);
    [SerializeField] private Color poweredLampColor = new Color(0.15f, 0.85f, 0.2f);

    private CancellationTokenSource startupCts;
    private MaterialPropertyBlock lampProperties;
    private float remainingSeconds;

    public string GeneratorLabel => generatorLabel;

    public float StartupSeconds => startupSeconds;

    public GeneratorState State { get; private set; } = GeneratorState.Idle;

    public bool IsAtFullPower => State == GeneratorState.AtFullPower;

    /// <summary><c>Time.time</c> at which this generator reached full power.</summary>
    public float CompletionTime { get; private set; }

    /// <summary>Raised the instant this generator reaches full power.</summary>
    public event Action<GeneratorUnit> OnReachedFullPower;

    /// <summary>Raised on every state change, for audio and VFX hooks.</summary>
    public event Action<GeneratorState> OnStateChanged;

    private void Awake()
    {
        lampProperties = new MaterialPropertyBlock();
        ResetToIdle();
    }

    private void OnDestroy()
    {
        CancelStartup();
    }

    public void Interact(PlayerController player)
    {
        // Only an idle generator can be started. Re-pulling a running or powered lever is
        // ignored rather than restarting it, so a panicked player cannot spoil a good run.
        if (State != GeneratorState.Idle)
        {
            return;
        }

        BeginStartup();
    }

    public string GetPrompt()
    {
        switch (State)
        {
            case GeneratorState.Starting:
                return $"Generator {generatorLabel} starting... {remainingSeconds:0.0}s";

            case GeneratorState.AtFullPower:
                return $"Generator {generatorLabel} at full power";

            default:
                return $"[E] Start Generator {generatorLabel} ({startupSeconds:0}s)";
        }
    }

    /// <summary>
    /// Cancels any running countdown and returns the generator to idle immediately.
    /// Called by the reset lever so a mistimed attempt costs only the seconds already spent.
    /// </summary>
    public void ResetToIdle()
    {
        CancelStartup();

        remainingSeconds = startupSeconds;
        CompletionTime = 0f;

        SetState(GeneratorState.Idle);
        UpdateDisplay();
    }

    private void BeginStartup()
    {
        CancelStartup();

        // Linked to the destroy token so a countdown can be cancelled either by the reset
        // lever or by the object going away, without leaking a running UniTask.
        startupCts = CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy());

        remainingSeconds = startupSeconds;
        SetState(GeneratorState.Starting);
        UpdateDisplay();

        RunStartupAsync(startupCts.Token).Forget();
    }

    private async UniTaskVoid RunStartupAsync(CancellationToken token)
    {
        try
        {
            while (remainingSeconds > 0f)
            {
                await UniTask.Yield(PlayerLoopTiming.Update, token);

                remainingSeconds -= Time.deltaTime;
                UpdateDisplay();
            }
        }
        catch (OperationCanceledException)
        {
            // Reset lever pulled or object destroyed mid-startup; ResetToIdle has already
            // restored the visible state, so there is nothing to undo here.
            return;
        }

        remainingSeconds = 0f;
        CompletionTime = Time.time;

        SetState(GeneratorState.AtFullPower);
        UpdateDisplay();

        OnReachedFullPower?.Invoke(this);
    }

    private void CancelStartup()
    {
        if (startupCts == null)
        {
            return;
        }

        startupCts.Cancel();
        startupCts.Dispose();
        startupCts = null;
    }

    private void SetState(GeneratorState newState)
    {
        if (State == newState)
        {
            return;
        }

        State = newState;

        UpdateLamp();
        OnStateChanged?.Invoke(newState);
    }

    private void UpdateDisplay()
    {
        if (countdownDisplay == null)
        {
            return;
        }

        countdownDisplay.text = State == GeneratorState.AtFullPower
            ? $"{generatorLabel}\nREADY"
            : $"{generatorLabel}\n{Mathf.Max(0f, remainingSeconds):0.0}";
    }

    private void UpdateLamp()
    {
        if (statusLamp == null)
        {
            return;
        }

        var color = State switch
        {
            GeneratorState.Starting => startingLampColor,
            GeneratorState.AtFullPower => poweredLampColor,
            _ => idleLampColor
        };

        // MaterialPropertyBlock rather than statusLamp.material, which would instantiate a
        // fresh material per generator and leak it.
        statusLamp.GetPropertyBlock(lampProperties);
        lampProperties.SetColor("_BaseColor", color);
        lampProperties.SetColor("_Color", color);
        statusLamp.SetPropertyBlock(lampProperties);
    }
}
