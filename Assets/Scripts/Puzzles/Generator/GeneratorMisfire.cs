using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;

/// <summary>
/// The joke on a failed sync: instead of the verification computer, the surge briefly
/// powers something useless. Cycles through the props so repeated failures escalate
/// rather than repeating the same gag.
/// </summary>
public class GeneratorMisfire : MonoBehaviour
{
    [Serializable]
    public class MisfireProp
    {
        [Tooltip("Object switched on for the duration of the misfire.")]
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

        SetAllPropsActive(false);

        if (verificationComputer != null)
        {
            verificationComputer.SetActive(false);
        }
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
        SetAllPropsActive(false);

        if (verificationComputer != null)
        {
            verificationComputer.SetActive(false);
        }

        SetCaption(string.Empty);
    }

    private void PowerVerificationComputer()
    {
        SetAllPropsActive(false);

        if (verificationComputer != null)
        {
            verificationComputer.SetActive(true);
        }

        SetCaption("VERIFICATION SYSTEM ONLINE");
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

        entry.prop.SetActive(true);
        SetCaption(entry.caption);

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

        entry.prop.SetActive(false);
        SetCaption(string.Empty);
    }

    private void SetAllPropsActive(bool isActive)
    {
        foreach (var entry in props)
        {
            if (entry == null || entry.prop == null)
            {
                continue;
            }

            entry.prop.SetActive(isActive);
        }
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
