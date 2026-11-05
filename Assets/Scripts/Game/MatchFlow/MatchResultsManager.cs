
using System;
using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(NetworkObject))]
public class MatchResultsManager : NetworkBehaviour
{
    private readonly NetworkList<ulong> winnerClientIds = new();
    private readonly NetworkList<ulong> completedClientIds = new();
    private readonly NetworkList<ulong> concededClientIds = new();

    public event Action<ulong> WinnerRegistered;
    public event Action<ulong> PlayerCompleted;
    public event Action<ulong> PlayerConceded;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        winnerClientIds.OnListChanged += OnWinnerListChanged;
        completedClientIds.OnListChanged += OnCompletedListChanged;
        concededClientIds.OnListChanged += OnConcededListChanged;

        // Reset results when a new match instance spawns.
        if (IsServer)
        {
            winnerClientIds.Clear();
            completedClientIds.Clear();
            concededClientIds.Clear();
        }
    }

    public override void OnNetworkDespawn()
    {
        winnerClientIds.OnListChanged -= OnWinnerListChanged;
        completedClientIds.OnListChanged -= OnCompletedListChanged;
        concededClientIds.OnListChanged -= OnConcededListChanged;

        base.OnNetworkDespawn();
    }

    // -------------------------
    // Registration (SERVER)
    // -------------------------

    public bool TryRegisterWinnerServer(ulong clientId)
    {
        if (!IsServer || !IsSpawned)
            return false;

        if (winnerClientIds.Contains(clientId))
            return false;

        winnerClientIds.Add(clientId);

        Debug.Log($"Winner registered: Client {clientId}");

        return true;
    }

    public bool TryRegisterCompletedPlayerServer(ulong clientId)
    {
        if (!IsServer || !IsSpawned)
            return false;

        if (IsPlayerResolved(clientId))
            return false;

        completedClientIds.Add(clientId);

        Debug.Log($"Player completed: Client {clientId}");

        return true;
    }

    public bool TryRegisterConcededPlayerServer(ulong clientId)
    {
        if (!IsServer || !IsSpawned)
            return false;

        if (IsPlayerResolved(clientId))
            return false;

        concededClientIds.Add(clientId);

        Debug.Log($"Player conceded: Client {clientId}");

        return true;
    }

    // -------------------------
    // Queries
    // -------------------------

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

    public bool TryGetLastCompleted(out ulong clientId)
    {
        if (completedClientIds.Count == 0)
        {
            clientId = ulong.MaxValue;
            return false;
        }

        clientId = completedClientIds[completedClientIds.Count - 1];
        return true;
    }

    // -------------------------
    // NetworkList events
    // -------------------------

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
