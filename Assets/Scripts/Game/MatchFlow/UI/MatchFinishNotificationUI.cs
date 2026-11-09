
using System.Collections;
using TMPro;
using Unity.Netcode;
using UnityEngine;

public class MatchFinishNotificationUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private MatchResultsManager resultsManager;

    [Header("Win UI")]
    [SerializeField] private GameObject localFinishedPanel;
    [SerializeField] private HighlightMyText WinHighlightText;
    
    [Header("Other won UI")]
    [SerializeField] private GameObject otherPlayerFinishedPanel;
    [SerializeField] private HighlightMyText OtherPlayerWinHighlightText;
    [SerializeField] private TMP_Text otherPlayerFinishedText;

    [Header("Concede UI")]
    [SerializeField] private GameObject localConcedePanel;
    [SerializeField] private HighlightMyText ConcedeHighlightText;


    private Coroutine bindingCoroutine;
    private Coroutine hideCoroutine;

    private bool isSubscribed;

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
            resultsManager.PlayerConceded -= OnPlayerConceded;
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
        resultsManager.PlayerConceded += OnPlayerConceded;
        isSubscribed = true;

        // Handle a completion that occurred before subscription.
        ulong localClientId = NetworkManager.Singleton.LocalClientId;

        if (resultsManager.HasCompleted(localClientId))
        {
            WinHighlightText.HighlightUIText();
        }
        else if (resultsManager.TryGetLastCompleted(out ulong lastFinisher) &&
                 lastFinisher != localClientId)
        {
            SetOtherPlayerFinishedText(lastFinisher);
            OtherPlayerWinHighlightText.HighlightUIText();
        }
    }

    private void OnPlayerCompleted(ulong clientId)
    {
        if (NetworkManager.Singleton == null)
            return;

        if (clientId == NetworkManager.Singleton.LocalClientId)
        {
            //ShowLocalFinished();
            WinHighlightText.HighlightUIText();
        }
        else
        {
            SetOtherPlayerFinishedText(clientId);
            OtherPlayerWinHighlightText.HighlightUIText();
        }
    }

    private void OnPlayerConceded(ulong clientId)
    {
        if (NetworkManager.Singleton == null)
            return;

        if (clientId == NetworkManager.Singleton.LocalClientId)
        {
            //ShowLocalFinished();
            ConcedeHighlightText.HighlightUIText();
        }
    }


    private void SetOtherPlayerFinishedText(ulong clientId)
    {
        if (otherPlayerFinishedText != null)
        {
            string playerName = "A player";

            if (NetworkManager.Singleton != null &&
                NetworkManager.Singleton.ConnectedClients.TryGetValue(
                    clientId,
                    out NetworkClient client
                ) &&
                client.PlayerObject != null &&
                client.PlayerObject.TryGetComponent(
                    out PlayerIdentity playerIdentity
                ) &&
                !string.IsNullOrWhiteSpace(
                    playerIdentity.PlayerName.Value.ToString()
                ))
            {
                playerName = playerIdentity.PlayerName.Value.ToString();
            }
            else
            {
                Debug.LogWarning(
                    $"Could not resolve a player name for client {clientId}."
                );
            }

            otherPlayerFinishedText.text =
                $"{playerName} has reached the end!";
        }
    }

}
