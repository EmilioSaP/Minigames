
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameSpawnManager : MonoBehaviour, IMatchSpawnReadiness
{
    public enum SpawnSelectionMode
    {
        Sequential,
        Random
    }

    [Header("Spawn Points")]
    [SerializeField] private Transform[] spawnPoints;
    [SerializeField] private bool disableMovementOnSpawn = true;

    [SerializeField]
    private SpawnSelectionMode selectionMode =
        SpawnSelectionMode.Sequential;

    private NetworkManager networkManager;
    private NetworkSceneManager networkSceneManager;
    private bool isSubscribed;

    private int nextSpawnIndex;

    // Prevents one player from being assigned several spawn points
    // because multiple network callbacks occur close together.
    private readonly HashSet<ulong> placedClientIds = new();

    public bool IsLocalPlayerSpawned
    {
        get
        {
            NetworkObject localPlayer = networkManager?.LocalClient?.PlayerObject;

            return localPlayer != null &&
                   localPlayer.TryGetComponent(
                       out NetworkPlayerSpawnHandler spawnHandler
                   ) &&
                   spawnHandler.HasAppliedSpawn;
        }
    }

    private void OnEnable()
    {
        SubscribeToNetworkEvents();
    }

    private void Start()
    {
        // Retry in case the NetworkManager was not available in OnEnable.
        SubscribeToNetworkEvents();
    }

    private void OnDisable()
    {
        UnsubscribeFromNetworkEvents();
    }

    private void SubscribeToNetworkEvents()
    {
        if (isSubscribed)
            return;

        networkManager = NetworkManager.Singleton;

        if (networkManager == null)
            return;

        networkManager.OnClientConnectedCallback += OnClientConnected;
        networkManager.OnClientDisconnectCallback += OnClientDisconnected;

        if (networkManager.NetworkConfig.EnableSceneManagement)
        {
            networkSceneManager = networkManager.SceneManager;

            if (networkSceneManager != null)
            {
                networkSceneManager.OnLoadEventCompleted
                    += OnLoadEventCompleted;
            }
        }

        isSubscribed = true;
    }

    private void UnsubscribeFromNetworkEvents()
    {
        if (!isSubscribed || networkManager == null)
            return;

        networkManager.OnClientConnectedCallback -= OnClientConnected;
        networkManager.OnClientDisconnectCallback -= OnClientDisconnected;

        if (networkSceneManager != null)
        {
            networkSceneManager.OnLoadEventCompleted
                -= OnLoadEventCompleted;
            networkSceneManager = null;
        }

        isSubscribed = false;
    }

    private void OnLoadEventCompleted(
        string sceneName,
        LoadSceneMode loadSceneMode,
        List<ulong> clientsCompleted,
        List<ulong> clientsTimedOut)
    {
        // Only react to loading the scene containing this manager.
        if (sceneName != gameObject.scene.name)
            return;

        if (networkManager.IsClient)
        {
            NetworkObject localPlayer =
                networkManager.LocalClient?.PlayerObject;

            if (localPlayer != null &&
                localPlayer.TryGetComponent(
                    out NetworkPlayerSpawnHandler spawnHandler
                ))
            {
                spawnHandler.ResetSpawnStatus();
            }
        }

        if (!networkManager.IsServer)
            return;

        Debug.Log($"SpawnManager ready in scene: {sceneName}");

        // Start the spawn sequence again for the new scene.
        nextSpawnIndex = 0;
        placedClientIds.Clear();

        // Existing players must also receive positions in the new scene.
        foreach (NetworkClient client in networkManager.ConnectedClientsList)
        {
            StartCoroutine(PlacePlayerWhenReady(client.ClientId));
        }
    }

    private void OnClientConnected(ulong clientId)
    {
        if (!networkManager.IsServer)
            return;

        // Handles clients joining after this scene has loaded.
        StartCoroutine(PlacePlayerWhenReady(clientId));
    }

    private void OnClientDisconnected(ulong clientId)
    {
        placedClientIds.Remove(clientId);

        if (networkManager != null && networkManager.IsServer)
        {
            Debug.Log($"SpawnManager: client {clientId} disconnected.");
        }
    }

    private IEnumerator PlacePlayerWhenReady(ulong clientId)
    {
        // The connection callback and player-object spawn are not
        // guaranteed to be ready at precisely the same moment.
        const int maxWaitFrames = 120;

        for (int frame = 0; frame < maxWaitFrames; frame++)
        {
            if (networkManager == null || !networkManager.IsServer)
                yield break;

            if (placedClientIds.Contains(clientId))
                yield break;

            if (!networkManager.ConnectedClients.TryGetValue(
                    clientId, out NetworkClient client))
            {
                yield break;
            }

            if (client.PlayerObject != null)
            {
                PlacePlayer(clientId, client.PlayerObject);
                yield break;
            }

            yield return null;
        }

        Debug.LogWarning(
            $"SpawnManager: player object for client {clientId} " +
            "was not ready in time.");
    }

    private void PlacePlayer(ulong clientId, NetworkObject playerObject)
    {
        if (spawnPoints == null || spawnPoints.Length == 0)
        {
            Debug.LogError(
                "SpawnManager has no spawn points assigned.");
            return;
        }

        NetworkPlayerSpawnHandler spawnHandler =
            playerObject.GetComponent<NetworkPlayerSpawnHandler>();

        if (spawnHandler == null)
        {
            Debug.LogError(
                $"Player prefab is missing NetworkPlayerSpawnHandler. " +
                $"Client ID: {clientId}");
            return;
        }

        Transform spawnPoint = GetNextSpawnPoint();

        spawnHandler.ApplySpawnFromServer(
        spawnPoint.position,
        spawnPoint.rotation,
        disableMovementOnSpawn
        );

        placedClientIds.Add(clientId);

        Debug.Log(
            $"Spawned client {clientId} at {spawnPoint.position}");
    }

    private Transform GetNextSpawnPoint()
    {
        if (selectionMode == SpawnSelectionMode.Random)
        {
            int randomIndex = Random.Range(0, spawnPoints.Length);
            return spawnPoints[randomIndex];
        }

        int index = nextSpawnIndex % spawnPoints.Length;
        nextSpawnIndex++;

        return spawnPoints[index];
    }
}
