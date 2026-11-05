using System.Collections;
using UnityEngine;

public class MatchCountdownUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private MatchFlowManager matchFlow;
    [SerializeField] private CountNumberAnimator countAnimator;

    [Header("Settings")]
    [SerializeField] private float goDisplayDuration = 0.75f;

    private Coroutine bindingCoroutine;
    private Coroutine goCoroutine;
    private int currentCountdownValue;

    private bool isSubscribed;

    private void OnEnable()
    {
        bindingCoroutine = StartCoroutine(BindToMatchFlow());
    }

    private void OnDisable()
    {
        if (bindingCoroutine != null)
            StopCoroutine(bindingCoroutine);

        if (goCoroutine != null)
            StopCoroutine(goCoroutine);

        if (isSubscribed && matchFlow != null)
        {
            matchFlow.PhaseChanged -= OnPhaseChanged;
            matchFlow.CountdownUpdated -= OnCountdownUpdated;
        }

        isSubscribed = false;
    }

    private IEnumerator BindToMatchFlow()
    {
        // Wait until the networked MatchFlowManager is spawned.
        while (matchFlow == null || !matchFlow.IsSpawned)
        {
            if (matchFlow == null)
            {
                matchFlow = FindFirstObjectByType<MatchFlowManager>();
            }

            yield return null;
        }

        matchFlow.PhaseChanged += OnPhaseChanged;
        matchFlow.CountdownUpdated += OnCountdownUpdated;

        isSubscribed = true;

        // Handle the current phase in case we subscribed late.
        // Only an actual transition to InProgress should display GO.
        if (matchFlow.CurrentPhase == MatchPhase.Countdown)
        {
            OnCountdownUpdated(matchFlow.CountdownRemaining);
        }
        else
        {
            countAnimator.Hide();
        }
    }

    private void OnPhaseChanged(MatchPhase phase)
    {
        if (goCoroutine != null)
        {
            StopCoroutine(goCoroutine);
            goCoroutine = null;
        }

        switch (phase)
        {
            case MatchPhase.Countdown:
                OnCountdownUpdated(matchFlow.CountdownRemaining);
                currentCountdownValue = Mathf.CeilToInt(matchFlow.CountdownRemaining);
                break;

            case MatchPhase.InProgress:
                countAnimator.AnimateGo();
                goCoroutine = StartCoroutine(HideGoAfterDelay());
                break;

            default:
                countAnimator.Hide();
                break;
        }
    }

    private void OnCountdownUpdated(float remaining)
    {
        if (matchFlow == null || matchFlow.CurrentPhase != MatchPhase.Countdown)
        {
            return;
        }

        if (remaining <= 0f)
            return;

        int seconds = Mathf.CeilToInt(remaining);
        
        if (seconds == currentCountdownValue)
            return;

        currentCountdownValue = seconds;

        countAnimator.AnimateNumber(seconds);
    }

    private IEnumerator HideGoAfterDelay()
    {
        yield return new WaitForSecondsRealtime(goDisplayDuration);

        countAnimator.Hide();

        goCoroutine = null;
    }
}