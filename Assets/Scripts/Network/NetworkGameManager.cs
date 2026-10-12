using System;
using Unity.Netcode;
using UnityEngine;

public class NetworkGameManager : MonoBehaviour
{
    public static event Action<DisablePlayer> LocalPlayerReady;

    [SerializeField] private NetworkPlayerSpawn playerSpawn;

    private void Start()
    {
        NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
        NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
    }

    private void OnDestroy()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
        }
    }

    private void OnClientConnected(ulong clientId)
    {
        if (clientId == NetworkManager.Singleton.LocalClientId)
        {
            NetworkObject localPlayerObject =
                NetworkManager.Singleton.LocalClient.PlayerObject;

            if (localPlayerObject == null)
            {
                Debug.LogWarning("Local player object not found.");
            }
            else
            {
                DisablePlayer localPlayer = localPlayerObject.GetComponent<DisablePlayer>();

                if (localPlayer == null)
                    Debug.LogWarning("Local player is missing the DisablePlayer component.");
                else
                    LocalPlayerReady?.Invoke(localPlayer);
            }
        }

        if (!NetworkManager.Singleton.IsServer)
            return;

        if (!NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out var client))
            return;

        NetworkObject player = client.PlayerObject;

        if (player == null)
            return;

        player.transform.position = playerSpawn.PlayerSpawnPoint();
    }

    private void OnClientDisconnected(ulong clientId)
    {
        if (!NetworkManager.Singleton.IsServer)
            return;

        Debug.Log($"Client disconnected: {clientId}");
    }
}