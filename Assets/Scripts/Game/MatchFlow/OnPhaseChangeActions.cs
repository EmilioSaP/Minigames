using UnityEngine;
using UnityEngine.Events;
using System.Collections;


public class OnPhaseChangeActions : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private MatchFlowManager matchFlow;
    private bool isSubscribed;

    [Header("Actions")]
    [SerializeField] private UnityEvent onPhaseInProgress;
    [SerializeField] private UnityEvent onPhaseFinished;


    private void OnEnable()
    {
        StartCoroutine(BindToMatchFlow());
    }

    private void OnDisable()
    {

        if (isSubscribed && matchFlow != null)
        {
            matchFlow.PhaseChanged -= OnPhaseChanged;
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
        isSubscribed = true;
    }

    private void OnPhaseChanged(MatchPhase phase)
    {   
        switch (phase)
        {
            case MatchPhase.InProgress:
                onPhaseInProgress?.Invoke();
                break;

            case MatchPhase.Finished:
                onPhaseFinished?.Invoke();
                break;
            default:
                break;
        }
    }
}

