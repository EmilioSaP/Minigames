
using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class MatchPlayerTracker : MonoBehaviour
{
    private NetworkManager networkManager;

    private readonly HashSet<ulong> connectedClientIds = new();

    private bool isSubscribed;

    // Events that other gameplay systems can subscribe to.
    public event Action<ulong> PlayerJoinedDuringMatch;
    public event Action<ulong> PlayerLeftDuringMatch;
    public event Action<int> PlayerCountChanged;

    // Current roster information.
    public int PlayerCount => connectedClientIds.Count;

    public IReadOnlyCollection<ulong> ConnectedClientIds
        => connectedClientIds;

    private void OnEnable()
    {
        Initialize();
    }

    private void Start()
    {
        Initialize();
    }

    private void OnDisable()
    {
        if (isSubscribed && networkManager != null)
        {
            networkManager.OnConnectionEvent -= HandleConnectionEvent;
        }

        isSubscribed = false;
    }

    private void Initialize()
    {
        if (isSubscribed)
            return;

        networkManager = NetworkManager.Singleton;

        if (networkManager == null)
            return;

        networkManager.OnConnectionEvent += HandleConnectionEvent;
        isSubscribed = true;

        // Register everyone already connected when this scene loads.
        connectedClientIds.Clear();

        if (networkManager.IsListening)
        {
            foreach (ulong clientId in networkManager.ConnectedClientsIds)
            {
                connectedClientIds.Add(clientId);
            }
        }

        Debug.Log(
            $"Match roster initialized. Players: {PlayerCount}"
        );
    }

    private void HandleConnectionEvent(
        NetworkManager manager,
        ConnectionEventData eventData)
    {
        switch (eventData.EventType)
        {
            case ConnectionEvent.ClientConnected:
            case ConnectionEvent.PeerConnected:
                RegisterPlayerJoined(eventData.ClientId);
                break;

            case ConnectionEvent.ClientDisconnected:
            case ConnectionEvent.PeerDisconnected:
                RegisterPlayerLeft(eventData.ClientId);
                break;
        }
    }

    private void RegisterPlayerJoined(ulong clientId)
    {
        // Prevent duplicate events.
        if (!connectedClientIds.Add(clientId))
            return;

        Debug.Log($"Player joined match. Client ID: {clientId}");

        PlayerJoinedDuringMatch?.Invoke(clientId);
        PlayerCountChanged?.Invoke(PlayerCount);
    }

    private void RegisterPlayerLeft(ulong clientId)
    {
        // Only emit an event if the player was actually registered.
        if (!connectedClientIds.Remove(clientId))
            return;

        Debug.Log($"Player left match. Client ID: {clientId}");

        PlayerLeftDuringMatch?.Invoke(clientId);
        PlayerCountChanged?.Invoke(PlayerCount);
    }

    public bool IsPlayerConnected(ulong clientId)
    {
        return connectedClientIds.Contains(clientId);
    }
}
