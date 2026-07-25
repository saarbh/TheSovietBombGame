using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// Countdown engine initialized to 420 seconds (7 minutes).
/// Runs via a UniTask loop, updating elapsed/remaining time and dispatching ticks/events.
/// </summary>
public class WatchManager
{
    public const float TOTAL_TIME_SECONDS = 420f;

    private float timeRemaining;
    private bool isRunning;
    private int lastMinuteRemaining = -1;

    public float TimeRemaining => timeRemaining;
    public bool IsRunning => isRunning;

    /// <summary>Invoked when the time remaining updates (passes remaining seconds).</summary>
    public event Action<float> OnTimeUpdated;

    /// <summary>Invoked when the remaining minute boundary changes (passes remaining minutes).</summary>
    public event Action<int> OnMinuteChanged;

    /// <summary>Invoked when the countdown timer hits 0.</summary>
    public event Action OnTimeExpired;

    public WatchManager()
    {
        timeRemaining = TOTAL_TIME_SECONDS;
    }

    /// <summary>
    /// Starts the countdown loop using UniTask.
    /// </summary>
    public async UniTask StartCountdownAsync(CancellationToken ct)
    {
        if (isRunning)
        {
            return;
        }

        isRunning = true;
        lastMinuteRemaining = Mathf.CeilToInt(timeRemaining / 60f);

        while (isRunning && timeRemaining > 0)
        {
            await UniTask.Yield(PlayerLoopTiming.Update, ct);

            if (Time.timeScale > 0)
            {
                timeRemaining -= Time.deltaTime;

                if (timeRemaining < 0)
                {
                    timeRemaining = 0;
                }

                OnTimeUpdated?.Invoke(timeRemaining);

                var currentMinuteRemaining = Mathf.CeilToInt(timeRemaining / 60f);
                if (currentMinuteRemaining != lastMinuteRemaining)
                {
                    lastMinuteRemaining = currentMinuteRemaining;
                    OnMinuteChanged?.Invoke(currentMinuteRemaining);
                }

                if (timeRemaining <= 0)
                {
                    isRunning = false;
                    OnTimeExpired?.Invoke();
                    break;
                }
            }
        }

        isRunning = false;
    }

    /// <summary>
    /// Stops the active countdown.
    /// </summary>
    public void StopCountdown()
    {
        isRunning = false;
    }

    /// <summary>
    /// Formats the remaining time as MM:SS.
    /// </summary>
    public string GetFormattedTime()
    {
        var minutes = Mathf.FloorToInt(timeRemaining / 60f);
        var seconds = Mathf.FloorToInt(timeRemaining % 60f);
        return $"{minutes:D2}:{seconds:D2}";
    }

    /// <summary>
    /// Grants bonus time to the countdown.
    /// </summary>
    public void AddTime(float seconds)
    {
        if (timeRemaining <= 0)
        {
            return;
        }

        timeRemaining += seconds;
        OnTimeUpdated?.Invoke(timeRemaining);
    }

    /// <summary>
    /// Subtracts time from the countdown.
    /// </summary>
    public void ReduceTime(float seconds)
    {
        if (timeRemaining <= 0)
        {
            return;
        }

        timeRemaining -= seconds;
        if (timeRemaining < 0)
        {
            timeRemaining = 0;
        }

        OnTimeUpdated?.Invoke(timeRemaining);

        if (timeRemaining <= 0 && isRunning)
        {
            isRunning = false;
            OnTimeExpired?.Invoke();
        }
    }
}
