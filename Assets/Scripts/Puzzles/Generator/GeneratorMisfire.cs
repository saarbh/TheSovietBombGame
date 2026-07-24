using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;

/// <summary>
/// The joke on a failed sync: instead of the verification computer, the surge briefly
/// powers something useless. Cycles through the props so repeated failures escalate
/// rather than repeating the same gag.
///
/// Props stay visible at all times and change their power state via <see cref="PoweredProp"/>;
/// they are never switched off, because equipment that disappears reads as a bug.
/// </summary>
public class GeneratorMisfire : MonoBehaviour
{
    [Serializable]
    public class MisfireProp
    {
        [Tooltip("Object powered for the duration of the misfire. Needs a PoweredProp component.")]
        public GameObject prop;

        [Tooltip("Caption, e.g. \"DESK FAN: OPERATIONAL\".")]
        public string caption = string.Empty;
    }

    [SerializeField] private GeneratorPuzzle puzzle;

    [Header("Props")]
    [Tooltip("Cycled in order on each failed attempt: desk fan, propaganda projector, heated chair, portrait of Lenin.")]
    [SerializeField] private MisfireProp[] props = Array.Empty<MisfireProp>();

    [Header("Presentation")]
    [SerializeField] private float misfireSeconds = 2.5f;

    [Tooltip("Optional. Shows the misfire caption.")]
    [SerializeField] private TMP_Text captionDisplay;

    [Tooltip("Optional. Powered only when the sync succeeds.")]
    [SerializeField] private GameObject verificationComputer;

    private int nextPropIndex;

    private void Awake()
    {
        if (puzzle == null)
        {
            puzzle = GetComponentInParent<GeneratorPuzzle>();
        }

        SetAllPropsPowered(false);
        SetPowered(verificationComputer, false);
        SetCaption(string.Empty);
    }

    private void OnEnable()
    {
        if (puzzle == null)
        {
            return;
        }

        puzzle.OnSyncEvaluated += HandleSyncEvaluated;
        puzzle.OnGeneratorsReset += HandleGeneratorsReset;
    }

    private void OnDisable()
    {
        if (puzzle == null)
        {
            return;
        }

        puzzle.OnSyncEvaluated -= HandleSyncEvaluated;
        puzzle.OnGeneratorsReset -= HandleGeneratorsReset;
    }

    private void HandleSyncEvaluated(bool wasSynced)
    {
        if (wasSynced)
        {
            PowerVerificationComputer();
            return;
        }

        PlayNextMisfireAsync(this.GetCancellationTokenOnDestroy()).Forget();
    }

    private void HandleGeneratorsReset()
    {
        SetAllPropsPowered(false);
        SetPowered(verificationComputer, false);
        SetCaption(string.Empty);
    }

    private void PowerVerificationComputer()
    {
        SetAllPropsPowered(false);
        SetPowered(verificationComputer, true);
        SetCaption("VERIFICATION SYSTEM ONLINE");

        Debug.Log("[Generator] Verification computer powered up.", this);
    }

    private async UniTaskVoid PlayNextMisfireAsync(CancellationToken token)
    {
        if (props.Length == 0)
        {
            return;
        }

        var entry = props[nextPropIndex % props.Length];
        nextPropIndex++;

        if (entry == null || entry.prop == null)
        {
            return;
        }

        SetPowered(entry.prop, true);
        SetCaption(entry.caption);

        Debug.Log($"[Generator] MISFIRE - powered \"{entry.prop.name}\" for {misfireSeconds:0.#}s.", this);

        try
        {
            await UniTask.Delay(
                TimeSpan.FromSeconds(misfireSeconds),
                DelayType.DeltaTime,
                cancellationToken: token);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        SetPowered(entry.prop, false);
        SetCaption(string.Empty);
    }

    private void SetAllPropsPowered(bool isPowered)
    {
        foreach (var entry in props)
        {
            if (entry == null || entry.prop == null)
            {
                continue;
            }

            SetPowered(entry.prop, isPowered);
        }
    }

    private static void SetPowered(GameObject target, bool isPowered)
    {
        if (target == null)
        {
            return;
        }

        var powered = target.GetComponent<PoweredProp>();

        if (powered == null)
        {
            // Deliberately does not fall back to SetActive: hiding the object is the
            // behaviour this component exists to avoid.
            Debug.LogWarning($"[Generator] '{target.name}' has no PoweredProp, so its power state cannot be shown.", target);
            return;
        }

        powered.SetPowered(isPowered);
    }

    private void SetCaption(string caption)
    {
        if (captionDisplay == null)
        {
            return;
        }

        captionDisplay.text = caption;
    }
}
