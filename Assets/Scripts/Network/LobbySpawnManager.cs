using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

public class LobbySpawnManager : MonoBehaviour
{
    [SerializeField] private NetworkPlayerSpawn playerSpawn;
    [SerializeField] private PauseGameScript pauseGameScript;
    [SerializeField] private AudioClip lobbyClip;

    private NetworkManager networkManager;
    private NetworkSceneManager networkSceneManager;
    private bool isSubscribed;
    private bool isSceneSubscribed;

    private readonly HashSet<ulong> placedClientIds = new();

    private void OnEnable()
    {
        SubscribeToNetworkEvents();
    }

    private void Start()
    {
        SubscribeToNetworkEvents();
    }

    private void OnDisable()
    {
        if (!isSubscribed || networkManager == null)
            return;

        networkManager.OnServerStarted -= OnNetworkStarted;
        networkManager.OnClientStarted -= OnNetworkStarted;
        isSubscribed = false;
        UnsubscribeFromSceneEvents();
    }

    private void SubscribeToNetworkEvents()
    {
        if (isSubscribed)
            return;

        networkManager = NetworkManager.Singleton;
        if (networkManager == null)
            return;

        networkManager.OnServerStarted += OnNetworkStarted;
        networkManager.OnClientStarted += OnNetworkStarted;
        isSubscribed = true;

        SubscribeToSceneEvents();
    }

    private void OnNetworkStarted()
    {
        SubscribeToSceneEvents();
    }

    private void SubscribeToSceneEvents()
    {
        if (isSceneSubscribed ||
            networkManager == null ||
            !networkManager.IsListening ||
            !networkManager.NetworkConfig.EnableSceneManagement)
        {
            return;
        }

        networkSceneManager = networkManager.SceneManager;
        if (networkSceneManager == null)
            return;

        networkSceneManager.OnLoadEventCompleted += OnLoadEventCompleted;
        isSceneSubscribed = true;
    }

    private void UnsubscribeFromSceneEvents()
    {
        if (isSceneSubscribed && networkSceneManager != null)
        {
            networkSceneManager.OnLoadEventCompleted -= OnLoadEventCompleted;
        }

        networkSceneManager = null;
        isSceneSubscribed = false;
    }

    private void OnLoadEventCompleted(
        string sceneName,
        LoadSceneMode loadSceneMode,
        List<ulong> clientsCompleted,
        List<ulong> clientsTimedOut)
    {
        if (sceneName != gameObject.scene.name)
            return;

        if (networkManager.IsServer)
        {
            placedClientIds.Clear();

            foreach (NetworkClient client in networkManager.ConnectedClientsList)
                StartCoroutine(PlacePlayerWhenReady(client.ClientId));
        }

        if (networkManager.IsClient)
            StartCoroutine(EnableLocalPlayerAfterTransition());

        AudioManager.Instance.PlayMusic(lobbyClip, true, false);
    }

    private IEnumerator EnableLocalPlayerAfterTransition()
    {
        const int maxWaitFrames = 120;

        for (int frame = 0; frame < maxWaitFrames; frame++)
        {
            if (networkManager == null || !networkManager.IsClient)
                yield break;

            if (networkManager.LocalClient?.PlayerObject != null &&
                pauseGameScript != null &&
                pauseGameScript.HasLocalPlayer)
            {
                yield return null;
                pauseGameScript.UnPause();
                yield break;
            }

            yield return null;
        }

        Debug.LogWarning(
            "LobbySpawnManager: timed out waiting to resume the local player.");
    }

    private IEnumerator PlacePlayerWhenReady(ulong clientId)
    {
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
            $"LobbySpawnManager: player object for client {clientId} " +
            "was not ready in time.");
    }

    private void PlacePlayer(ulong clientId, NetworkObject playerObject)
    {
        if (playerSpawn == null)
        {
            Debug.LogError("LobbySpawnManager has no NetworkPlayerSpawn assigned.");
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

        Vector3 position = playerSpawn.PlayerSpawnPoint();
        spawnHandler.ApplySpawnFromServer(position, playerObject.transform.rotation);
        placedClientIds.Add(clientId);

        Debug.Log($"Returned client {clientId} to the lobby at {position}");
    }
}
