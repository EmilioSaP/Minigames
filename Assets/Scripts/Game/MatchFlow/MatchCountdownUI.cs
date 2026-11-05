using System.Collections;
using TMPro;
using UnityEngine;

public class MatchCountdownUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private MatchFlowManager matchFlow;
    [SerializeField] private TMP_Text countdownText;
    [SerializeField] private FadeCanvasGroupScript fadeCanvasGroup;

    [Header("Settings")]
    [SerializeField] private float goDisplayDuration = 0.75f;

    [Header("Audios")] // Containing audio clips for 3, 2, 1 countdown and GO.
    [SerializeField] private AudioClip moreThanThreeAudioClip;    
    [SerializeField] private AudioClip threeAudioClip;    
    [SerializeField] private AudioClip twoAudioClip;
    [SerializeField] private AudioClip oneAudioClip;
    [SerializeField] private AudioClip goAudioClip;

    [Header("Animations")] // Reference to an Animation component with two animation clips: "Countdown" and "Go". The Countdown animation should be a simple scale-up and fade-in effect for the countdown numbers, while the Go animation should be a more pronounced scale-up and fade-in effect for the "GO!" text.
    [SerializeField] private Animator countdownAnimator;

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
                matchFlow =
                    FindFirstObjectByType<MatchFlowManager>();
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
            HideCountdown();
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

                ShowCountdown("GO!");
                countdownAnimator.SetTrigger("Go");
                AudioManager.Instance.PlayLocalSFX(goAudioClip);

                goCoroutine = StartCoroutine(HideGoAfterDelay());
                break;

            default:

                HideCountdown();
                break;
        }
    }

    private void OnCountdownUpdated(float remaining)
    {
        if (matchFlow == null ||
            matchFlow.CurrentPhase != MatchPhase.Countdown)
        {
            return;
        }

        if (remaining <= 0f)
            return;

        if (Mathf.CeilToInt(remaining) == currentCountdownValue)
            return;

        int seconds = Mathf.CeilToInt(remaining);
        currentCountdownValue = seconds;

        ShowCountdown(seconds.ToString());

        if (seconds > 3)
        {
            AudioManager.Instance.PlayLocalSFX(moreThanThreeAudioClip);
        }else if (seconds == 3)
        {
            AudioManager.Instance.PlayLocalSFX(threeAudioClip);
        }
        else if (seconds == 2)
        {
            AudioManager.Instance.PlayLocalSFX(twoAudioClip);
        }
        else if (seconds == 1)
        {
            AudioManager.Instance.PlayLocalSFX(oneAudioClip);
        }
        countdownAnimator.SetTrigger("Countdown");
    }

    private void ShowCountdown(string value)
    {
        //fadeCanvasGroup.SetAlpha(1f); done by animator
        if (countdownText == null)
            return;

        countdownText.text = value;
        countdownText.enabled = true;
    }

    private void HideCountdown()
    {
        if (countdownText == null)
            return;

        //fadeCanvasGroup.FadeOut(); done by animator
        //countdownText.enabled = false;
    }

    private IEnumerator HideGoAfterDelay()
    {
        yield return new WaitForSecondsRealtime(goDisplayDuration);

        HideCountdown();

        goCoroutine = null;
    }
}