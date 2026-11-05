
using System.Collections;
using TMPro;
using Unity.Netcode;
using UnityEngine;

public class MatchFinishNotificationUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private MatchResultsManager resultsManager;

    [Header("UI")]
    [SerializeField] private GameObject localFinishedPanel;
    [SerializeField] private GameObject otherPlayerFinishedPanel;
    [SerializeField] private TMP_Text otherPlayerFinishedText;

    [Header("Settings")]
    [SerializeField] private float displayDuration = 3f;

    private Coroutine bindingCoroutine;
    private Coroutine hideCoroutine;

    private bool isSubscribed;

    private void Awake()
    {
        HidePanels();
    }

    private void OnEnable()
    {
        bindingCoroutine = StartCoroutine(BindToResults());
    }

    private void OnDisable()
    {
        if (bindingCoroutine != null)
            StopCoroutine(bindingCoroutine);

        if (hideCoroutine != null)
            StopCoroutine(hideCoroutine);

        if (isSubscribed && resultsManager != null)
        {
            resultsManager.PlayerCompleted -= OnPlayerCompleted;
        }

        isSubscribed = false;
    }

    private IEnumerator BindToResults()
    {
        while (NetworkManager.Singleton == null ||
               !NetworkManager.Singleton.IsListening)
        {
            yield return null;
        }

        while (resultsManager == null || !resultsManager.IsSpawned)
        {
            if (resultsManager == null)
            {
                resultsManager =
                    FindFirstObjectByType<MatchResultsManager>();
            }

            yield return null;
        }

        resultsManager.PlayerCompleted += OnPlayerCompleted;
        isSubscribed = true;

        // Handle a completion that occurred before subscription.
        ulong localClientId = NetworkManager.Singleton.LocalClientId;

        if (resultsManager.HasCompleted(localClientId))
        {
            ShowLocalFinished();
        }
        else if (resultsManager.TryGetLastCompleted(out ulong lastFinisher) &&
                 lastFinisher != localClientId)
        {
            ShowOtherPlayerFinished(lastFinisher);
        }
    }

    private void OnPlayerCompleted(ulong clientId)
    {
        if (NetworkManager.Singleton == null)
            return;

        if (clientId == NetworkManager.Singleton.LocalClientId)
        {
            ShowLocalFinished();
        }
        else
        {
            ShowOtherPlayerFinished(clientId);
        }
    }

    private void ShowLocalFinished()
    {
        ShowPanel(
            localFinishedPanel,
            otherPlayerFinishedPanel
        );
    }

    private void ShowOtherPlayerFinished(ulong clientId)
    {
        if (otherPlayerFinishedText != null)
        {
            otherPlayerFinishedText.text =
                $"Player {clientId} has reached the end!";
        }

        ShowPanel(
            otherPlayerFinishedPanel,
            localFinishedPanel
        );
    }

    private void ShowPanel(GameObject panelToShow, GameObject panelToHide)
    {
        if (hideCoroutine != null)
            StopCoroutine(hideCoroutine);

        if (panelToHide != null)
            panelToHide.SetActive(false);

        if (panelToShow != null)
            panelToShow.SetActive(true);

        hideCoroutine = StartCoroutine(HideAfterDelay());
    }

    private IEnumerator HideAfterDelay()
    {
        yield return new WaitForSecondsRealtime(displayDuration);

        HidePanels();

        hideCoroutine = null;
    }

    private void HidePanels()
    {
        if (localFinishedPanel != null)
            localFinishedPanel.SetActive(false);

        if (otherPlayerFinishedPanel != null)
            otherPlayerFinishedPanel.SetActive(false);
    }
}
