
using System;
using System.Collections;
using System.Collections.Generic;

using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

public enum RespawnMode
{
    Disabled,
    Unlimited,
    Limited
}

public class RespawnManager : MonoBehaviour
{
    [Header("Respawn Policy")]
    [SerializeField] private RespawnMode respawnMode = RespawnMode.Unlimited;
    [SerializeField] private bool autoRespawn = true;

    [Tooltip("Number of additional respawns. Only used in Limited mode.")]
    [SerializeField] private int maxRespawns = 2;

    [Header("Respawn Timing")]
    [SerializeField] private float respawnDelay = 3f;
    [Tooltip("Time the fade-in remains visible before the respawn teleport.")]
    [SerializeField] private float respawnFadeInDuration = 0.5f;

    [SerializeField] private float teleportAckTimeout = 10f;

    public bool AutoRespawn => autoRespawn;

    private NetworkManager networkManager;
    private NetworkSceneManager networkSceneManager;

    private bool isSubscribed;

    // Track the health component associated with each client.
    private readonly Dictionary<ulong, PlayerHealth> registeredPlayers = new();

    // Keep event delegates so we can unsubscribe correctly.
    private readonly Dictionary<ulong, Action<DamageInfo>> deathHandlers = new();

    // Additional respawns used by each player.
    private readonly Dictionary<ulong, int> respawnsUsed = new();

    // Prevent multiple respawn sequences for one player.
    private readonly Dictionary<ulong, Coroutine> respawnCoroutines = new();

    // Track explicit requests made by owners when automatic respawning is disabled.
    private readonly HashSet<ulong> manualRespawnRequests = new();

    // Keep request handlers so they can be removed when players unregister.
    private readonly Dictionary<ulong, Action<ulong>> respawnRequestHandlers = new();

    // Await server confirmation that an owner applied the teleport.
    private readonly Dictionary<ulong, Action> pendingRespawnCallbacks = new();

    // Server-side events for game-specific systems.
    public event Action<ulong, DamageInfo> PlayerDied;
    public event Action<ulong> PlayerRespawned;
    public event Action<ulong> PlayerEliminated;

    private void OnEnable()
    {
        SubscribeToNetworkEvents();
    }

    private void Start()
    {
        SubscribeToNetworkEvents();

        if (IsServer)
        {
            StartCoroutine(RegisterExistingPlayers());
        }
    }

    private void OnDisable()
    {
        UnsubscribeFromNetworkEvents();

        StopAllCoroutines();

        // Clean up death-event subscriptions.
        List<ulong> clientIds = new(registeredPlayers.Keys);

        foreach (ulong clientId in clientIds)
        {
            UnregisterPlayer(clientId);
        }
    }

    private bool IsServer =>
        networkManager != null && networkManager.IsServer;

    // --------------------------------------------------
    // NETWORK SUBSCRIPTIONS
    // --------------------------------------------------

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
        if (!IsServer || sceneName != gameObject.scene.name)
            return;

        StartCoroutine(RegisterExistingPlayers());
    }

    private void OnClientConnected(ulong clientId)
    {
        if (!IsServer)
            return;

        StartCoroutine(RegisterPlayerWhenReady(clientId));
    }

    private void OnClientDisconnected(ulong clientId)
    {
        if (!IsServer)
            return;

        UnregisterPlayer(clientId);
    }

    // --------------------------------------------------
    // PLAYER REGISTRATION
    // --------------------------------------------------

    private IEnumerator RegisterExistingPlayers()
    {
        yield return null;

        if (!IsServer)
            yield break;

        foreach (NetworkClient client in networkManager.ConnectedClientsList)
        {
            StartCoroutine(RegisterPlayerWhenReady(client.ClientId));
        }
    }

    private IEnumerator RegisterPlayerWhenReady(ulong clientId)
    {
        const int maxWaitFrames = 120;

        for (int frame = 0; frame < maxWaitFrames; frame++)
        {
            if (!IsServer)
                yield break;

            if (!networkManager.ConnectedClients.TryGetValue(
                clientId, out NetworkClient client))
            {
                yield break;
            }

            if (client.PlayerObject != null)
            {
                PlayerHealth health =
                    client.PlayerObject.GetComponent<PlayerHealth>();

                if (health != null)
                {
                    RegisterPlayer(clientId, health);
                    yield break;
                }

                Debug.LogWarning(
                    $"Player {clientId} is missing PlayerHealth."
                );

                yield break;
            }

            yield return null;
        }

        Debug.LogWarning(
            $"RespawnManager could not find PlayerObject for client {clientId}."
        );
    }

    private void RegisterPlayer(ulong clientId, PlayerHealth health)
    {
        if (registeredPlayers.TryGetValue(clientId, out PlayerHealth existing))
        {
            if (existing == health)
                return;

            UnregisterPlayer(clientId);
        }

        registeredPlayers.Add(clientId, health);

        Action<DamageInfo> handler =
            damageInfo => HandlePlayerDied(clientId, health, damageInfo);

        deathHandlers.Add(clientId, handler);

        health.ServerDied += handler;

        NetworkPlayerSpawnHandler spawnHandler =
            health.GetComponent<NetworkPlayerSpawnHandler>();

        if (spawnHandler != null)
        {
            Action<ulong> requestHandler =
                requestedClientId => HandleRespawnRequested(requestedClientId);

            respawnRequestHandlers.Add(clientId, requestHandler);
            spawnHandler.RespawnRequestedServer += requestHandler;
        }

        if (!respawnsUsed.ContainsKey(clientId))
            respawnsUsed.Add(clientId, 0);

        Debug.Log($"RespawnManager registered player {clientId}.");
    }

    private void UnregisterPlayer(ulong clientId)
    {
        // Stop a pending respawn timer.
        if (respawnCoroutines.TryGetValue(clientId, out Coroutine coroutine))
        {
            if (coroutine != null)
                StopCoroutine(coroutine);

            respawnCoroutines.Remove(clientId);
        }

        // Unsubscribe from the player's death event.
        if (registeredPlayers.TryGetValue(clientId, out PlayerHealth health))
        {
            if (deathHandlers.TryGetValue(clientId, out Action<DamageInfo> handler))
            {
                health.ServerDied -= handler;
                deathHandlers.Remove(clientId);
            }

            // Remove any pending teleport-completion callback.
            if (pendingRespawnCallbacks.TryGetValue(
                clientId, out Action callback))
            {
                NetworkPlayerSpawnHandler callbackSpawnHandler =
                    health.GetComponent<NetworkPlayerSpawnHandler>();

                if (callbackSpawnHandler != null)
                    callbackSpawnHandler.RespawnTeleportAppliedServer -= callback;

                pendingRespawnCallbacks.Remove(clientId);
            }

            NetworkPlayerSpawnHandler spawnHandler =
                health.GetComponent<NetworkPlayerSpawnHandler>();

            if (spawnHandler != null &&
                respawnRequestHandlers.TryGetValue(
                    clientId, out Action<ulong> requestHandler))
            {
                spawnHandler.RespawnRequestedServer -= requestHandler;
                respawnRequestHandlers.Remove(clientId);
            }

            registeredPlayers.Remove(clientId);
        }

        manualRespawnRequests.Remove(clientId);
        respawnsUsed.Remove(clientId);

        Debug.Log($"RespawnManager unregistered player {clientId}.");
    }

    // --------------------------------------------------
    // DEATH HANDLING
    // --------------------------------------------------

    private void HandlePlayerDied(
        ulong clientId,
        PlayerHealth health,
        DamageInfo damageInfo)
    {
        if (!IsServer || !health.IsSpawned)
            return;

        Debug.Log($"RespawnManager detected death: client {clientId}");

        PlayerDied?.Invoke(clientId, damageInfo);

        if (respawnCoroutines.ContainsKey(clientId))
            return;

        if (!CanRespawn(clientId))
        {
            GetSpawnHandler(health)?.ShowDeathUi(false, autoRespawn, 0);

            Debug.Log($"Player {clientId} has been eliminated.");

            PlayerEliminated?.Invoke(clientId);
            return;
        }

        GetSpawnHandler(health)?.ShowDeathUi(true, autoRespawn, respawnDelay);

        Coroutine routine = StartCoroutine(
            RespawnAfterDelay(clientId, health)
        );

        respawnCoroutines.Add(clientId, routine);
    }

    private bool CanRespawn(ulong clientId)
    {
        switch (respawnMode)
        {
            case RespawnMode.Disabled:
                return false;

            case RespawnMode.Unlimited:
                return true;

            case RespawnMode.Limited:
                respawnsUsed.TryGetValue(clientId, out int used);
                return used < Mathf.Max(0, maxRespawns);

            default:
                return false;
        }
    }

    private void HandleRespawnRequested(ulong clientId)
    {
        if (autoRespawn ||
            !respawnCoroutines.ContainsKey(clientId) ||
            !CanRespawn(clientId))
        {
            return;
        }

        manualRespawnRequests.Add(clientId);
    }

    private NetworkPlayerSpawnHandler GetSpawnHandler(PlayerHealth health)
    {
        return health != null
            ? health.GetComponent<NetworkPlayerSpawnHandler>()
            : null;
    }

    // --------------------------------------------------
    // RESPAWN SEQUENCE
    // --------------------------------------------------

    private IEnumerator RespawnAfterDelay(
        ulong clientId,
        PlayerHealth health)
    {
        if (autoRespawn)
        {
            float delay = Mathf.Max(0f, respawnDelay);
            float countdownDeadline = Time.realtimeSinceStartup + delay;
            int previousCountdown = -1;

            Debug.Log(
                $"Player {clientId} will respawn in {delay} seconds."
            );

            while (Time.realtimeSinceStartup < countdownDeadline)
            {
                int countdown = Mathf.CeilToInt(
                    countdownDeadline - Time.realtimeSinceStartup
                );

                if (countdown != previousCountdown)
                {
                    GetSpawnHandler(health)?.UpdateRespawnCountdown(countdown);
                    previousCountdown = countdown;
                }

                yield return null;
            }
        }
        else
        {
            while (!manualRespawnRequests.Contains(clientId))
                yield return null;

            manualRespawnRequests.Remove(clientId);
        }

        // Confirm that the player still belongs to this session.
        if (!TryGetCurrentPlayer(clientId, health, out NetworkObject playerObject))
        {
            respawnCoroutines.Remove(clientId);
            yield break;
        }

        // Get the player's latest accepted checkpoint.
        PlayerCheckpointState checkpointState =
            playerObject.GetComponent<PlayerCheckpointState>();

        if (checkpointState == null ||
            !checkpointState.TryGetRespawnTransform(
                out Vector3 respawnPosition,
                out Quaternion respawnRotation))
        {
            Debug.LogError(
                $"Player {clientId} has no valid respawn checkpoint."
            );

            PlayerEliminated?.Invoke(clientId);
            respawnCoroutines.Remove(clientId);
            yield break;
        }

        NetworkPlayerSpawnHandler spawnHandler =
            playerObject.GetComponent<NetworkPlayerSpawnHandler>();

        if (spawnHandler == null)
        {
            Debug.LogError(
                $"Player {clientId} is missing NetworkPlayerSpawnHandler."
            );

            PlayerEliminated?.Invoke(clientId);
            respawnCoroutines.Remove(clientId);
            yield break;
        }

        spawnHandler.BeginRespawnFadeIn();
        yield return new WaitForSecondsRealtime(
            Mathf.Max(0f, respawnFadeInDuration)
        );

        // Prepare an acknowledgment callback before requesting the teleport.
        Action onTeleportApplied = null;

        onTeleportApplied = () =>
        {
            spawnHandler.RespawnTeleportAppliedServer -= onTeleportApplied;

            pendingRespawnCallbacks.Remove(clientId);

            if (!TryGetCurrentPlayer(clientId, health, out _))
                return;

            // Only restore health after teleport acknowledgment.
            health.RestoreHealthServer();
            spawnHandler.BeginRespawnFadeOut();

            respawnsUsed.TryGetValue(clientId, out int used);
            respawnsUsed[clientId] = used + 1;

            Debug.Log(
                $"Player {clientId} respawned. " +
                $"Respawns used: {respawnsUsed[clientId]}"
            );

            PlayerRespawned?.Invoke(clientId);
        };

        pendingRespawnCallbacks.Add(clientId, onTeleportApplied);

        spawnHandler.RespawnTeleportAppliedServer += onTeleportApplied;

        bool accepted = spawnHandler.RespawnFromServer(
            respawnPosition,
            respawnRotation
        );

        if (!accepted)
        {
            spawnHandler.RespawnTeleportAppliedServer -= onTeleportApplied;
            pendingRespawnCallbacks.Remove(clientId);
            spawnHandler.BeginRespawnFadeOut();

            Debug.LogWarning(
                $"Could not initiate respawn for client {clientId}."
            );

            respawnCoroutines.Remove(clientId);
            yield break;
        }

        // Give a remote owner time to apply the teleport and acknowledge it.
        float deadline =
            Time.realtimeSinceStartup + teleportAckTimeout;

        while (pendingRespawnCallbacks.ContainsKey(clientId) &&
               Time.realtimeSinceStartup < deadline)
        {
            yield return null;
        }

        // Do not silently restore health if the owner never acknowledged.
        if (pendingRespawnCallbacks.TryGetValue(
            clientId, out Action pendingCallback))
        {
            spawnHandler.RespawnTeleportAppliedServer -= pendingCallback;
            pendingRespawnCallbacks.Remove(clientId);
            spawnHandler.BeginRespawnFadeOut();

            Debug.LogError(
                $"Respawn teleport acknowledgment timed out for client {clientId}."
            );
        }

        respawnCoroutines.Remove(clientId);
    }

    private bool TryGetCurrentPlayer(
        ulong clientId,
        PlayerHealth expectedHealth,
        out NetworkObject playerObject)
    {
        playerObject = null;

        if (!IsServer ||
            !networkManager.ConnectedClients.TryGetValue(
                clientId, out NetworkClient client))
        {
            return false;
        }

        playerObject = client.PlayerObject;

        if (playerObject == null)
            return false;

        PlayerHealth currentHealth =
            playerObject.GetComponent<PlayerHealth>();

        return currentHealth == expectedHealth &&
               currentHealth.IsSpawned &&
               currentHealth.IsDead.Value;
    }
}
