
using System;
using System.Collections;
using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(NetworkObject))]
public class MatchResultsManager : NetworkBehaviour
{
    [Header("References")]
    [SerializeField] private MatchFlowManager matchFlow;

    // Existing result collections.
    private readonly NetworkList<ulong> winnerClientIds = new();
    private readonly NetworkList<ulong> completedClientIds = new();
    private readonly NetworkList<ulong> concededClientIds = new();

    // New synchronized data.
    private readonly NetworkList<MatchParticipantRecord> participantRecords = new();
    private readonly NetworkList<PlayerCompletionRecord> completionRecords = new();

    // Snapshot synchronization.
    private readonly NetworkVariable<bool> participantSnapshotCaptured =
        new NetworkVariable<bool>(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

    private readonly NetworkVariable<int> expectedParticipantCount =
        new NetworkVariable<int>(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

    // Results synchronization.
    private readonly NetworkVariable<bool> resultsFinalized =
        new NetworkVariable<bool>(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

    private readonly NetworkVariable<int> expectedWinnerCount =
        new NetworkVariable<int>(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

    private readonly NetworkVariable<int> expectedCompletionCount =
        new NetworkVariable<int>(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

    private readonly NetworkVariable<int> expectedConcededCount =
        new NetworkVariable<int>(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

    private bool hasMatchStarted;

    private double matchStartServerTime;
    private int nextCompletionOrder;

    private Coroutine bindingCoroutine;

    // Existing events.
    public event Action<ulong> WinnerRegistered;
    public event Action<ulong> PlayerCompleted;
    public event Action<ulong> PlayerConceded;

    // Public information.
    public bool HasMatchStarted => hasMatchStarted;

    public int ParticipantCount => participantRecords.Count;

    public int CompletionCount => completionRecords.Count;

    public bool HasParticipantSnapshot =>
        participantSnapshotCaptured.Value &&
        participantRecords.Count >= expectedParticipantCount.Value;

    public bool AreResultsFinalized
    {
        get
        {
            if (!resultsFinalized.Value || !HasParticipantSnapshot)
                return false;

            return
                winnerClientIds.Count >= expectedWinnerCount.Value &&
                completedClientIds.Count >= expectedCompletionCount.Value &&
                completionRecords.Count >= expectedCompletionCount.Value &&
                concededClientIds.Count >= expectedConcededCount.Value;
        }
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        winnerClientIds.OnListChanged += OnWinnerListChanged;
        completedClientIds.OnListChanged += OnCompletedListChanged;
        concededClientIds.OnListChanged += OnConcededListChanged;

        if (IsServer)
        {
            bindingCoroutine = StartCoroutine(BindToMatchFlow());
        }
    }

    public override void OnNetworkDespawn()
    {
        winnerClientIds.OnListChanged -= OnWinnerListChanged;
        completedClientIds.OnListChanged -= OnCompletedListChanged;
        concededClientIds.OnListChanged -= OnConcededListChanged;

        if (matchFlow != null)
            matchFlow.PhaseChanged -= OnMatchPhaseChanged;

        if (bindingCoroutine != null)
            StopCoroutine(bindingCoroutine);

        base.OnNetworkDespawn();
    }

    // ------------------------------------
    // MATCH START / TIMING
    // ------------------------------------

    private IEnumerator BindToMatchFlow()
    {
        while (matchFlow == null || !matchFlow.IsSpawned)
        {
            if (matchFlow == null)
                matchFlow = FindFirstObjectByType<MatchFlowManager>();

            yield return null;
        }

        matchFlow.PhaseChanged += OnMatchPhaseChanged;

        // Handle late initialization.
        if (matchFlow.CurrentPhase == MatchPhase.InProgress)
        {
            BeginMatchServer();
        }

        bindingCoroutine = null;
    }

    private void OnMatchPhaseChanged(MatchPhase phase)
    {
        if (!IsServer)
            return;

        if (phase == MatchPhase.InProgress)
        {
            BeginMatchServer();
        }
    }

    private void BeginMatchServer()
    {
        if (!IsServer || hasMatchStarted)
            return;

        NetworkManager networkManager = NetworkManager.Singleton;

        if (networkManager == null || !networkManager.IsListening)
            return;

        // Reset previous match results.
        winnerClientIds.Clear();
        completedClientIds.Clear();
        concededClientIds.Clear();

        participantRecords.Clear();
        completionRecords.Clear();

        expectedParticipantCount.Value = 0;

        expectedWinnerCount.Value = 0;
        expectedCompletionCount.Value = 0;
        expectedConcededCount.Value = 0;

        participantSnapshotCaptured.Value = false;
        resultsFinalized.Value = false;

        nextCompletionOrder = 0;

        // Record the exact server time at match start.
        matchStartServerTime = networkManager.ServerTime.Time;

        hasMatchStarted = true;

        // Capture all players participating in this match.
        foreach (ulong clientId in networkManager.ConnectedClientsIds)
        {
            string playerName = ResolvePlayerName(clientId);

            participantRecords.Add(
                new MatchParticipantRecord(clientId, playerName)
            );
        }

        expectedParticipantCount.Value = participantRecords.Count;
        participantSnapshotCaptured.Value = true;

        Debug.Log(
            $"MatchResultsManager initialized. " +
            $"Players: {ParticipantCount}, " +
            $"Start time: {matchStartServerTime}"
        );
    }

    private string ResolvePlayerName(ulong clientId)
    {
        NetworkManager manager = NetworkManager.Singleton;

        if (manager != null &&
            manager.ConnectedClients.TryGetValue(
                clientId,
                out NetworkClient client
            ) &&
            client.PlayerObject != null &&
            client.PlayerObject.TryGetComponent(
                out PlayerIdentity playerIdentity
            ))
        {
            string playerName =
                playerIdentity.PlayerName.Value.ToString();

            if (!string.IsNullOrWhiteSpace(playerName))
                return playerName;
        }

        Debug.LogWarning(
            $"Could not resolve username for client {clientId}."
        );

        return $"Player {clientId}";
    }

    // ------------------------------------
    // REGISTRATION (SERVER)
    // ------------------------------------

    public bool TryRegisterWinnerServer(ulong clientId)
    {
        if (!IsServer ||
            !IsSpawned ||
            resultsFinalized.Value ||
            !hasMatchStarted ||
            !IsParticipant(clientId))
        {
            return false;
        }

        if (winnerClientIds.Contains(clientId))
            return false;

        if (IsPlayerResolved(clientId))
            return false;

        winnerClientIds.Add(clientId);

        Debug.Log($"Winner registered: Client {clientId}");

        return true;
    }

    public bool TryRegisterCompletedPlayerServer(ulong clientId)
    {
        if (!IsServer ||
            !IsSpawned ||
            resultsFinalized.Value ||
            !hasMatchStarted ||
            !IsParticipant(clientId))
        {
            return false;
        }

        if (IsPlayerResolved(clientId))
            return false;

        double completionTime =
            NetworkManager.Singleton.ServerTime.Time -
            matchStartServerTime;

        completionTime = Math.Max(0, completionTime);

        completionRecords.Add(
            new PlayerCompletionRecord(
                clientId,
                completionTime,
                nextCompletionOrder++
            )
        );

        completedClientIds.Add(clientId);

        Debug.Log(
            $"Player {clientId} completed in {completionTime:F3} seconds."
        );

        return true;
    }

    public bool TryRegisterConcededPlayerServer(ulong clientId)
    {
        if (!IsServer ||
            !IsSpawned ||
            resultsFinalized.Value ||
            !IsParticipant(clientId))
        {
            return false;
        }

        if (IsPlayerResolved(clientId))
            return false;

        concededClientIds.Add(clientId);

        Debug.Log($"Player conceded: Client {clientId}");

        return true;
    }

    // ------------------------------------
    // RESULT FINALIZATION
    // ------------------------------------

    // Called by PostMatchFlowController before showing the results screen.
    // Every unresolved participant becomes DNF.
    public void FinalizeResultsServer()
    {
        if (!IsServer ||
            !IsSpawned ||
            !hasMatchStarted ||
            resultsFinalized.Value)
        {
            return;
        }

        for (int i = 0; i < participantRecords.Count; i++)
        {
            ulong clientId = participantRecords[i].ClientId;

            if (!IsPlayerResolved(clientId))
            {
                TryRegisterConcededPlayerServer(clientId);
            }
        }

        // Set expected counts after all registrations.
        expectedWinnerCount.Value = winnerClientIds.Count;
        expectedCompletionCount.Value = completedClientIds.Count;
        expectedConcededCount.Value = concededClientIds.Count;

        resultsFinalized.Value = true;

        Debug.Log(
            $"Results finalized. " +
            $"Winners: {winnerClientIds.Count}, " +
            $"Completed: {completedClientIds.Count}, " +
            $"DNF: {concededClientIds.Count}"
        );
    }

    // ------------------------------------
    // QUERIES
    // ------------------------------------

    public bool IsParticipant(ulong clientId)
    {
        for (int i = 0; i < participantRecords.Count; i++)
        {
            if (participantRecords[i].ClientId == clientId)
                return true;
        }

        return false;
    }

    public bool IsWinner(ulong clientId)
    {
        return winnerClientIds.Contains(clientId);
    }

    public bool HasCompleted(ulong clientId)
    {
        return completedClientIds.Contains(clientId);
    }

    public bool HasConceded(ulong clientId)
    {
        return concededClientIds.Contains(clientId);
    }

    public bool IsPlayerResolved(ulong clientId)
    {
        return HasCompleted(clientId) || HasConceded(clientId);
    }

    public MatchParticipantRecord GetParticipantAt(int index)
    {
        return participantRecords[index];
    }

    public bool TryGetPlayerName(
        ulong clientId,
        out string playerName)
    {
        for (int i = 0; i < participantRecords.Count; i++)
        {
            MatchParticipantRecord record = participantRecords[i];

            if (record.ClientId == clientId)
            {
                playerName = record.PlayerName.ToString();
                return true;
            }
        }

        playerName = $"Player {clientId}";
        return false;
    }

    public PlayerCompletionRecord GetCompletionRecordAt(int index)
    {
        return completionRecords[index];
    }

    public bool TryGetCompletionRecord(
        ulong clientId,
        out PlayerCompletionRecord completionRecord)
    {
        for (int i = 0; i < completionRecords.Count; i++)
        {
            if (completionRecords[i].ClientId == clientId)
            {
                completionRecord = completionRecords[i];
                return true;
            }
        }

        completionRecord = default;
        return false;
    }

    public bool TryGetLastCompleted(out ulong clientId)
    {
        if (completedClientIds.Count == 0)
        {
            clientId = ulong.MaxValue;
            return false;
        }

        clientId =
            completedClientIds[completedClientIds.Count - 1];

        return true;
    }

    // ------------------------------------
    // NETWORKLIST EVENTS
    // ------------------------------------

    private void OnWinnerListChanged(NetworkListEvent<ulong> change)
    {
        if (change.Type == NetworkListEvent<ulong>.EventType.Add)
            WinnerRegistered?.Invoke(change.Value);
    }

    private void OnCompletedListChanged(NetworkListEvent<ulong> change)
    {
        if (change.Type == NetworkListEvent<ulong>.EventType.Add)
            PlayerCompleted?.Invoke(change.Value);
    }

    private void OnConcededListChanged(NetworkListEvent<ulong> change)
    {
        if (change.Type == NetworkListEvent<ulong>.EventType.Add)
            PlayerConceded?.Invoke(change.Value);
    }
}
