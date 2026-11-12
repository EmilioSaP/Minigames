
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

public enum MatchPhase : byte
{
    WaitingForPlayers,
    Preparing,
    Countdown,
    InProgress,
    Finished,
    PreparationFailed
}

[RequireComponent(typeof(NetworkObject))]
public class MatchFlowManager : NetworkBehaviour
{
    [Header("Match Settings")]
    [SerializeField] private float countdownDuration = 3f;
    [SerializeField] private float preparationTimeout = 45f;
    [SerializeField] private int minimumPlayersToStart = 1;

    [Header("Preparation Requirements")]
    [SerializeField] private bool requireLocalPlayerSpawn = true;

    // State synchronized from the server to everyone.
    private readonly NetworkVariable<MatchPhase> matchPhase =
        new NetworkVariable<MatchPhase>(
            MatchPhase.WaitingForPlayers,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

    private readonly NetworkVariable<int> matchSeed =
        new NetworkVariable<int>(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

    private readonly NetworkVariable<double> countdownEndServerTime =
        new NetworkVariable<double>(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

    private readonly NetworkVariable<int> readyPlayerCount =
        new NetworkVariable<int>(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

    private readonly NetworkVariable<int> requiredPlayerCount =
        new NetworkVariable<int>(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

    // Server-side player tracking.
    private readonly HashSet<ulong> matchParticipants = new();
    private readonly HashSet<ulong> readyClients = new();

    private NetworkManager networkManager;

    private Coroutine preparationCoroutine;

    private bool localPreparationStarted;
    private bool isSubscribed;

    // Local events for UI and other gameplay systems.
    public event Action<MatchPhase> PhaseChanged;
    public event Action<float> CountdownUpdated;

    // Public read-only information.
    public MatchPhase CurrentPhase => matchPhase.Value;

    public int MatchSeed => matchSeed.Value;

    public int ReadyPlayerCount => readyPlayerCount.Value;

    public int RequiredPlayerCount => requiredPlayerCount.Value;

    public bool AllPlayersReady =>
        ReadyPlayerCount >= RequiredPlayerCount &&
        RequiredPlayerCount > 0;

    public float CountdownRemaining
    {
        get
        {
            if (CurrentPhase != MatchPhase.Countdown ||
                NetworkManager.Singleton == null)
            {
                return 0f;
            }

            double remaining =
                countdownEndServerTime.Value -
                NetworkManager.Singleton.ServerTime.Time;

            return Mathf.Max(0f, (float)remaining);
        }
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        networkManager = NetworkManager.Singleton;

        matchPhase.OnValueChanged += OnMatchPhaseChanged;

        if (networkManager == null)
            return;

        // Only the server manages the participant roster.
        if (IsServer)
        {
            networkManager.OnConnectionEvent += HandleConnectionEvent;

            InitializeServerMatch();
        }

        // Scene completion event.
        if (networkManager.NetworkConfig.EnableSceneManagement)
        {
            networkManager.SceneManager.OnLoadEventCompleted
                += HandleSceneLoadCompleted;
        }

        // Also supports a client synchronizing into a scene
        // that is already in the preparation phase.
        if (IsClient && CurrentPhase == MatchPhase.Preparing)
        {
            StartLocalPreparationIfNeeded();
        }
    }

    public override void OnNetworkDespawn()
    {
        matchPhase.OnValueChanged -= OnMatchPhaseChanged;

        if (networkManager != null)
        {
            if (IsServer)
            {
                networkManager.OnConnectionEvent -= HandleConnectionEvent;
            }

            if (networkManager.NetworkConfig.EnableSceneManagement)
            {
                networkManager.SceneManager.OnLoadEventCompleted
                    -= HandleSceneLoadCompleted;
            }
        }

        if (preparationCoroutine != null)
        {
            StopCoroutine(preparationCoroutine);
            preparationCoroutine = null;
        }

        base.OnNetworkDespawn();
    }

    private void InitializeServerMatch()
    {
        if (!IsServer ||
            CurrentPhase != MatchPhase.WaitingForPlayers)
        {
            return;
        }

        matchParticipants.Clear();
        readyClients.Clear();

        // Register everyone already connected to the party.
        foreach (ulong clientId in networkManager.ConnectedClientsIds)
        {
            matchParticipants.Add(clientId);
        }

        UpdatePlayerCounts();

        // One seed for the entire match.
        matchSeed.Value = UnityEngine.Random.Range(1, int.MaxValue);

        Debug.Log($"Match initialized. Seed: {MatchSeed}");
        Debug.Log($"Players participating: {matchParticipants.Count}");

        matchPhase.Value = MatchPhase.Preparing;
    }

    private void HandleSceneLoadCompleted(
        string sceneName,
        LoadSceneMode loadSceneMode,
        List<ulong> clientsCompleted,
        List<ulong> clientsTimedOut)
    {
        if (sceneName != gameObject.scene.name)
            return;

        if (!IsClient || CurrentPhase != MatchPhase.Preparing)
            return;

        StartLocalPreparationIfNeeded();
    }

    private void OnMatchPhaseChanged(
        MatchPhase previousPhase,
        MatchPhase newPhase)
    {
        Debug.Log($"Match phase changed: {previousPhase} → {newPhase}");

        if (IsServer && (newPhase == MatchPhase.WaitingForPlayers || newPhase == MatchPhase.Preparing || newPhase == MatchPhase.Countdown))
        {
            SetConnectedPlayersInvulnerable(true);
        }

        if (IsServer && newPhase == MatchPhase.InProgress)
        {
            SetConnectedPlayersInvulnerable(false);
        }

        PhaseChanged?.Invoke(newPhase);

        if (newPhase == MatchPhase.Preparing && IsClient)
        {
            StartLocalPreparationIfNeeded();
        }
    }

    private void SetConnectedPlayersInvulnerable(bool invulnerable)
    {
        foreach (NetworkClient client in networkManager.ConnectedClientsList)
        {
            PlayerHealth health =
                client.PlayerObject != null
                    ? client.PlayerObject.GetComponent<PlayerHealth>()
                    : null;

            if (health == null)
            {
                Debug.LogError(
                    $"Player {client.ClientId} has no PlayerHealth component.",
                    this
                );
                continue;
            }

            health.SetInvulnerableServer(invulnerable);
        }
    }

    private void StartLocalPreparationIfNeeded()
    {
        if (!IsClient ||
            localPreparationStarted ||
            CurrentPhase != MatchPhase.Preparing)
        {
            return;
        }

        localPreparationStarted = true;

        preparationCoroutine = StartCoroutine(PrepareLocalClient());
    }

    private IEnumerator PrepareLocalClient()
    {
        // Allow all scene-load callbacks, including SpawnManager,
        // to finish before checking player spawn readiness.
        yield return null;

        MatchPreparationTask[] tasks =
            FindObjectsByType<MatchPreparationTask>(
                FindObjectsSortMode.None
            );

        Debug.Log(
            $"Starting local preparation. Tasks: {tasks.Length}"
        );

        IMatchSpawnReadiness[] spawnReadinessProviders =
            FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None)
                .OfType<IMatchSpawnReadiness>()
                .ToArray();

        // Every task receives the same seed.
        foreach (MatchPreparationTask task in tasks)
        {
            if (task == null)
                continue;

            try
            {
                task.BeginPreparation(MatchSeed);
            }
            catch (Exception e)
            {
                ReportLocalPreparationFailure(
                    $"{task.TaskName}: {e.Message}"
                );

                yield break;
            }
        }

        float deadline = Time.realtimeSinceStartup + preparationTimeout;

        while (true)
        {
            if (CurrentPhase != MatchPhase.Preparing)
                yield break;

            // Check for task failures.
            foreach (MatchPreparationTask task in tasks)
            {
                if (task != null && task.HasFailed)
                {
                    ReportLocalPreparationFailure(
                        $"{task.TaskName}: {task.FailureReason}"
                    );

                    yield break;
                }
            }

            bool allTasksReady = true;

            foreach (MatchPreparationTask task in tasks)
            {
                if (task != null && !task.IsReady)
                {
                    allTasksReady = false;
                    break;
                }
            }

            string playerSpawnStatus;
            bool playerReady = IsLocalPlayerReady(
                spawnReadinessProviders,
                out playerSpawnStatus
            );

            if (allTasksReady && playerReady)
            {
                Debug.Log("Local preparation completed.");

                ReportLocalPreparationComplete();

                yield break;
            }

            // Don't wait forever if something went wrong.
            if (Time.realtimeSinceStartup >= deadline)
            {
                string reason = BuildPreparationTimeoutReason(
                    tasks,
                    playerReady,
                    playerSpawnStatus
                );

                ReportLocalPreparationFailure(reason);

                yield break;
            }

            yield return null;
        }
    }

    private bool IsLocalPlayerReady(
        IMatchSpawnReadiness[] spawnReadinessProviders,
        out string reason)
    {
        reason = "";

        if (!requireLocalPlayerSpawn)
            return true;

        if (spawnReadinessProviders.Length == 0)
        {
            reason = "No match spawn readiness provider is present.";
            return false;
        }

        foreach (IMatchSpawnReadiness provider in spawnReadinessProviders)
        {
            if (!provider.IsLocalPlayerSpawned)
            {
                reason = "Waiting for the game spawn manager to place the local player.";
                return false;
            }
        }

        return true;
    }

    private string BuildPreparationTimeoutReason(
        MatchPreparationTask[] tasks,
        bool playerReady,
        string playerSpawnStatus)
    {
        List<string> pendingTasks = new();

        foreach (MatchPreparationTask task in tasks)
        {
            if (task != null && !task.IsReady)
                pendingTasks.Add(task.TaskName);
        }

        string reason = "Preparation timeout.";

        if (pendingTasks.Count > 0)
            reason += $" Pending tasks: {string.Join(", ", pendingTasks)}.";

        if (!playerReady)
            reason += $" Player spawn not ready: {playerSpawnStatus}";

        return reason;
    }

    private void ReportLocalPreparationComplete()
    {
        if (IsServer)
        {
            RegisterClientReady(networkManager.LocalClientId);
        }
        else
        {
            ReportPreparationCompleteRpc();
        }
    }

    [Rpc(SendTo.Server)]
    private void ReportPreparationCompleteRpc(
        RpcParams rpcParams = default)
    {
        ulong senderClientId =
            rpcParams.Receive.SenderClientId;

        RegisterClientReady(senderClientId);
    }

    private void RegisterClientReady(ulong clientId)
    {
        if (!IsServer ||
            CurrentPhase != MatchPhase.Preparing ||
            !matchParticipants.Contains(clientId))
        {
            return;
        }

        if (!readyClients.Add(clientId))
            return;

        UpdatePlayerCounts();

        Debug.Log(
            $"Client ready: {clientId} " +
            $"({ReadyPlayerCount}/{RequiredPlayerCount})"
        );

        TryBeginCountdown();
    }

    private void ReportLocalPreparationFailure(string reason)
    {
        Debug.LogError($"Local preparation failed: {reason}");

        if (IsServer)
        {
            RegisterPreparationFailure(
                networkManager.LocalClientId,
                reason
            );
        }
        else
        {
            ReportPreparationFailureRpc(reason);
        }
    }

    [Rpc(SendTo.Server)]
    private void ReportPreparationFailureRpc(
        string reason,
        RpcParams rpcParams = default)
    {
        RegisterPreparationFailure(
            rpcParams.Receive.SenderClientId,
            reason
        );
    }

    private void RegisterPreparationFailure(
        ulong clientId,
        string reason)
    {
        if (!IsServer ||
            CurrentPhase != MatchPhase.Preparing ||
            !matchParticipants.Contains(clientId))
        {
            return;
        }

        Debug.LogError(
            $"Preparation failed for client {clientId}: {reason}"
        );

        matchPhase.Value = MatchPhase.PreparationFailed;
    }

    private void TryBeginCountdown()
    {
        if (!IsServer || CurrentPhase != MatchPhase.Preparing)
            return;

        if (matchParticipants.Count < Mathf.Max(1, minimumPlayersToStart))
            return;

        if (readyClients.Count != matchParticipants.Count)
            return;

        countdownEndServerTime.Value =
            networkManager.ServerTime.Time +
            Mathf.Max(0f, countdownDuration);

        Debug.Log("All players ready. Starting countdown.");

        matchPhase.Value = MatchPhase.Countdown;
    }

    private void Update()
    {
        if (!IsSpawned || CurrentPhase != MatchPhase.Countdown)
            return;

        float remaining = CountdownRemaining;

        CountdownUpdated?.Invoke(remaining);

        // Only the server advances the match.
        if (IsServer && remaining <= 0f)
        {
            matchPhase.Value = MatchPhase.InProgress;
            Debug.Log("MATCH STARTED!");
        }
    }

    // Call this from a game-specific server-side game mode.
    public bool TryFinishMatch()
    {
        if (!IsServer || CurrentPhase != MatchPhase.InProgress)
            return false;

        matchPhase.Value = MatchPhase.Finished;

        Debug.Log("MATCH FINISHED!");

        return true;
    }

    private void HandleConnectionEvent(
        NetworkManager manager,
        ConnectionEventData eventData)
    {
        if (!IsServer)
            return;

        switch (eventData.EventType)
        {
            case ConnectionEvent.ClientConnected:
            case ConnectionEvent.PeerConnected:
                RegisterParticipant(eventData.ClientId);
                break;

            case ConnectionEvent.ClientDisconnected:
            case ConnectionEvent.PeerDisconnected:
                UnregisterParticipant(eventData.ClientId);
                break;
        }
    }

    private void RegisterParticipant(ulong clientId)
    {
        // For now, players joining after gameplay starts are not
        // automatically added to the current match.
        if (CurrentPhase != MatchPhase.Preparing &&
            CurrentPhase != MatchPhase.Countdown)
        {
            return;
        }

        if (!matchParticipants.Add(clientId))
            return;

        // A new player joining during countdown cancels it.
        // The remaining players keep their readiness.
        if (CurrentPhase == MatchPhase.Countdown)
        {
            matchPhase.Value = MatchPhase.Preparing;
        }

        UpdatePlayerCounts();

        Debug.Log($"Participant added: {clientId}");
    }

    private void UnregisterParticipant(ulong clientId)
    {
        if (CurrentPhase != MatchPhase.Preparing &&
            CurrentPhase != MatchPhase.Countdown)
        {
            return;
        }

        if (!matchParticipants.Remove(clientId))
            return;

        readyClients.Remove(clientId);

        UpdatePlayerCounts();

        Debug.Log($"Participant removed: {clientId}");

        // If the number of players drops below the minimum,
        // wait for another player before starting.
        if (CurrentPhase == MatchPhase.Countdown &&
            matchParticipants.Count < Mathf.Max(1, minimumPlayersToStart))
        {
            matchPhase.Value = MatchPhase.Preparing;
        }

        TryBeginCountdown();
    }

    private void UpdatePlayerCounts()
    {
        readyPlayerCount.Value = readyClients.Count;
        requiredPlayerCount.Value = matchParticipants.Count;
    }
}
