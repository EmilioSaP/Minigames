using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(NetworkObject))]
public class NextMatchController : NetworkBehaviour
{
    [Header("Next Game Vote")]
    [SerializeField] private float nextGameVoteDuration = 40f;
    [SerializeField] private float oneVoterLeftDuration = 12f;
    [SerializeField] private float decisionAcknowledgementDuration = 2f;
    [SerializeField] private MinigameCatalog minigameCatalog;
    [SerializeField] private NetworkSceneController networkSceneController;

    private readonly NetworkList<MinigameVoteEntry> votes = new();

    private bool selectionRestrictionApplied;

    private readonly NetworkVariable<bool> selectionVisible =
        new(false, NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

    private readonly NetworkVariable<bool> voteActive =
        new(false, NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

    private readonly NetworkVariable<double> voteEndServerTime =
        new(0, NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

    private NetworkManager networkManager;

    public event Action SelectionStarted;
    public event Action VoteCountsChanged;
    public event Action<string> SelectionCompleted;

    public bool IsSelectionActive => selectionVisible.Value;
    public bool IsVoteActive => voteActive.Value;
    public IReadOnlyList<MinigameCatalog.MinigameOption> Minigames =>
        minigameCatalog != null
            ? minigameCatalog.Minigames
            : Array.Empty<MinigameCatalog.MinigameOption>();
    public int ConnectedPlayerCount => GetConnectedClientIds().Count;
    public int VotedPlayerCount => votes.Count;

    public float VoteTimeRemaining
    {
        get
        {
            if (!voteActive.Value || NetworkManager.Singleton == null)
                return 0f;

            return Mathf.Max(
                0f,
                (float)(voteEndServerTime.Value -
                        NetworkManager.Singleton.ServerTime.Time)
            );
        }
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        networkManager = NetworkManager.Singleton;
        selectionRestrictionApplied = false;

        if (IsServer)
        {
            votes.Clear();
            selectionVisible.Value = false;
            voteActive.Value = false;
        }

        votes.OnListChanged += OnVotesChanged;
        selectionVisible.OnValueChanged += OnSelectionVisibleChanged;
        NetworkGameManager.LocalPlayerReady += OnLocalPlayerReady;
        NetworkGameManager.LocalPlayerLeft += OnLocalPlayerLeft;

        if (IsSelectionActive)
            ApplySelectionRestriction(null);

        if (IsServer && networkManager != null)
            networkManager.OnConnectionEvent += OnConnectionEvent;
    }

    public override void OnNetworkDespawn()
    {
        votes.OnListChanged -= OnVotesChanged;
        selectionVisible.OnValueChanged -= OnSelectionVisibleChanged;
        NetworkGameManager.LocalPlayerReady -= OnLocalPlayerReady;
        NetworkGameManager.LocalPlayerLeft -= OnLocalPlayerLeft;
        selectionRestrictionApplied = false;

        if (IsServer && networkManager != null)
            networkManager.OnConnectionEvent -= OnConnectionEvent;

        base.OnNetworkDespawn();
    }

    private void OnSelectionVisibleChanged(bool previous, bool current)
    {
        if (current)
            ApplySelectionRestriction(null);
        else
            selectionRestrictionApplied = false;
    }

    private void OnLocalPlayerReady(DisablePlayer player)
    {
        if (IsSelectionActive)
            ApplySelectionRestriction(player);
    }

    private void OnLocalPlayerLeft()
    {
        selectionRestrictionApplied = false;
    }

    private void ApplySelectionRestriction(DisablePlayer disablePlayer)
    {
        Cursor.lockState = CursorLockMode.None;

        if (selectionRestrictionApplied)
            return;

        if (disablePlayer == null)
        {
            NetworkObject localPlayerObject =
                NetworkManager.Singleton?.LocalClient?.PlayerObject;

            disablePlayer =
                localPlayerObject != null
                    ? localPlayerObject.GetComponent<DisablePlayer>()
                    : null;
        }

        if (disablePlayer == null)
        {
            Debug.LogWarning(
                "Cannot restrict the local player for minigame voting: " +
                "the local player or DisablePlayer component is unavailable.",
                this
            );
            return;
        }

        disablePlayer.AddSceneScopedRestriction(PlayerControlChannels.All);
        selectionRestrictionApplied = true;
    }

    private void Update()
    {
        if (!IsServer || !voteActive.Value || networkManager == null)
            return;

        if (networkManager.ServerTime.Time >= voteEndServerTime.Value)
            ResolveSelection();
    }

    public IReadOnlyList<int> GetAvailableMinigameIndices()
    {
        return GetAvailableOptionIndices(ConnectedPlayerCount);
    }

    public int GetVoteCount(int minigameIndex)
    {
        int count = 0;
        for (int i = 0; i < votes.Count; i++)
        {
            if (votes[i].MinigameIndex == minigameIndex)
                count++;
        }

        return count;
    }

    public void BeginSelection()
    {
        BeginSelectionServer(false);
    }

    public void BeginSelectionServer(bool rematchApproved)
    {
        if (!IsServer || !IsSpawned)
        {
            Debug.LogWarning("Only the server can start minigame selection.");
            return;
        }

        networkManager ??= NetworkManager.Singleton;
        if (networkManager == null || !networkManager.IsListening)
        {
            Debug.LogError("Cannot start minigame selection without a network session.");
            return;
        }

        if (networkSceneController == null)
            networkSceneController = FindFirstObjectByType<NetworkSceneController>();

        if (networkSceneController == null)
        {
            Debug.LogError("Minigame selection requires a NetworkSceneController.");
            return;
        }

        if (minigameCatalog == null)
        {
            Debug.LogError("Minigame selection requires a MinigameCatalog.");
            return;
        }

        if (rematchApproved)
        {
            string currentScene = SceneManager.GetActiveScene().name;
            int currentOptionIndex = -1;
            for (int i = 0; i < minigameCatalog.Minigames.Count; i++)
            {
                MinigameCatalog.MinigameOption option =
                    minigameCatalog.Minigames[i];
                if (option != null && option.sceneName == currentScene)
                {
                    currentOptionIndex = i;
                    break;
                }
            }

            List<int> availableOptions =
                GetAvailableOptionIndices(networkManager.ConnectedClientsIds.Count);
            if (currentOptionIndex < 0 ||
                !availableOptions.Contains(currentOptionIndex))
            {
                Debug.LogWarning(
                    $"The current scene {currentScene} is not available for " +
                    $"{networkManager.ConnectedClientsIds.Count} players. Returning to the lobby."
                );
                ReturnPartyToLobbyServer();
                return;
            }

            if (networkSceneController.LoadPartyScene(currentScene))
                SelectionCompleted?.Invoke(currentScene);
            return;
        }

        if (GetAvailableOptionIndices(networkManager.ConnectedClientsIds.Count).Count == 0)
        {
            Debug.LogWarning(
                $"No minigames are configured for {networkManager.ConnectedClientsIds.Count} connected players. Returning to the lobby."
            );
            ReturnPartyToLobbyServer();
            return;
        }

        votes.Clear();
        voteEndServerTime.Value =
            networkManager.ServerTime.Time + Mathf.Max(0f, nextGameVoteDuration);
        selectionVisible.Value = true;
        voteActive.Value = true;
        SelectionStarted?.Invoke();
        VoteCountsChanged?.Invoke();

        ShortenTimerIfOneVoterRemains(
            networkManager.ConnectedClientsIds.Count - votes.Count
        );

        if (networkManager.ConnectedClientsIds.Count == 0)
            ResolveSelection();
    }

    public void VoteForMinigame(int minigameIndex)
    {
        if (!IsSpawned || !IsClient || !voteActive.Value)
            return;

        VoteForMinigameRpc(minigameIndex);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void VoteForMinigameRpc(
        int minigameIndex,
        RpcParams rpcParams = default)
    {
        if (!IsServer || !voteActive.Value ||
            !IsConnectedClient(rpcParams.Receive.SenderClientId))
        {
            return;
        }

        List<int> availableOptions =
            GetAvailableOptionIndices(networkManager.ConnectedClientsIds.Count);

        if (!availableOptions.Contains(minigameIndex))
            return;

        UpsertVote(rpcParams.Receive.SenderClientId, minigameIndex);
        VoteCountsChanged?.Invoke();

        int remainingVoters =
            networkManager.ConnectedClientsIds.Count - votes.Count;

        ShortenTimerIfOneVoterRemains(remainingVoters);

        if (remainingVoters <= 0)
            ResolveSelection();
    }

    private void OnConnectionEvent(
        NetworkManager manager,
        ConnectionEventData eventData)
    {
        if (!voteActive.Value)
            return;

        bool disconnected =
            eventData.EventType == ConnectionEvent.ClientDisconnected ||
            eventData.EventType == ConnectionEvent.PeerDisconnected;
        bool connected =
            eventData.EventType == ConnectionEvent.ClientConnected ||
            eventData.EventType == ConnectionEvent.PeerConnected;

        if (!disconnected && !connected)
            return;

        if (disconnected)
            RemoveVote(eventData.ClientId);

        PruneVotesForUnavailableMinigames();
        VoteCountsChanged?.Invoke();

        int remainingVoters =
            manager.ConnectedClientsIds.Count - votes.Count;

        ShortenTimerIfOneVoterRemains(remainingVoters);

        if (remainingVoters <= 0)
        {
            ResolveSelection();
        }
    }

    private void ShortenTimerIfOneVoterRemains(int remainingVoters)
    {
        if (remainingVoters != 1)
            return;

        double shortenedEnd =
            networkManager.ServerTime.Time +
            Mathf.Max(0f, oneVoterLeftDuration);
        voteEndServerTime.Value =
            Math.Min(voteEndServerTime.Value, shortenedEnd);
    }

    private void ResolveSelection()
    {
        if (!IsServer || !voteActive.Value)
            return;

        voteActive.Value = false;

        if (votes.Count == 0)
        {
            StartCoroutine(FinishSelectionAfterAcknowledgement(-1));
            return;
        }

        List<int> availableOptions =
            GetAvailableOptionIndices(networkManager.ConnectedClientsIds.Count);

        if (availableOptions.Count == 0)
        {
            Debug.LogWarning(
                $"No minigames are configured for {networkManager.ConnectedClientsIds.Count} connected players. Returning to the lobby."
            );
            StartCoroutine(FinishSelectionAfterAcknowledgement(-1));
            return;
        }

        int selectedIndex = GetUnanimousOptionIndex();
        if (selectedIndex < 0)
            selectedIndex = ChooseWeightedOption(availableOptions);

        StartCoroutine(FinishSelectionAfterAcknowledgement(selectedIndex));
    }

    private IEnumerator FinishSelectionAfterAcknowledgement(int selectedIndex)
    {
        if (networkManager == null)
            yield break;

        double acknowledgementEnd =
            networkManager.ServerTime.Time +
            Mathf.Max(0f, decisionAcknowledgementDuration);
        while (networkManager != null &&
               networkManager.ServerTime.Time < acknowledgementEnd)
        {
            yield return null;
        }

        if (networkManager == null || !networkManager.IsListening)
            yield break;

        selectionVisible.Value = false;

        if (selectedIndex < 0)
        {
            Debug.Log("No minigame votes were received. Returning the party to the lobby.");
            ReturnPartyToLobbyServer();
            yield break;
        }

        string sceneName = minigameCatalog.Minigames[selectedIndex].sceneName;
        SelectionCompleted?.Invoke(sceneName);
        networkSceneController.LoadPartyScene(sceneName);
    }

    public void ReturnPartyToLobbyServer()
    {
        if (!IsServer || !IsSpawned)
        {
            Debug.LogWarning("Only the server can return the party to the lobby.");
            return;
        }

        if (networkSceneController == null)
            networkSceneController = FindFirstObjectByType<NetworkSceneController>();

        if (networkSceneController == null)
        {
            Debug.LogError("Cannot return the party: NetworkSceneController was not found.");
            return;
        }

        networkSceneController.ReturnPartyToLobby();
    }

    private int GetUnanimousOptionIndex()
    {
        if (votes.Count == 0 ||
            votes.Count != networkManager.ConnectedClientsIds.Count)
        {
            return -1;
        }

        int selectedIndex = votes[0].MinigameIndex;
        for (int i = 1; i < votes.Count; i++)
        {
            if (votes[i].MinigameIndex != selectedIndex)
                return -1;
        }

        return selectedIndex;
    }

    private int ChooseWeightedOption(List<int> availableOptions)
    {
        List<int> weightedOptions = new();

        for (int i = 0; i < votes.Count; i++)
        {
            int index = votes[i].MinigameIndex;
            if (availableOptions.Contains(index))
                weightedOptions.Add(index);
        }

        if (weightedOptions.Count == 0)
            return availableOptions[UnityEngine.Random.Range(0, availableOptions.Count)];

        return weightedOptions[
            UnityEngine.Random.Range(0, weightedOptions.Count)
        ];
    }

    private List<int> GetAvailableOptionIndices(int playerCount)
    {
        return minigameCatalog == null
            ? new List<int>()
            : minigameCatalog.GetAvailableOptionIndices(playerCount);
    }

    private void PruneVotesForUnavailableMinigames()
    {
        List<int> availableOptions =
            GetAvailableOptionIndices(networkManager.ConnectedClientsIds.Count);

        for (int i = votes.Count - 1; i >= 0; i--)
        {
            if (!availableOptions.Contains(votes[i].MinigameIndex))
                votes.RemoveAt(i);
        }
    }

    private void UpsertVote(ulong clientId, int minigameIndex)
    {
        for (int i = 0; i < votes.Count; i++)
        {
            if (votes[i].ClientId != clientId)
                continue;

            votes[i] = new MinigameVoteEntry(clientId, minigameIndex);
            return;
        }

        votes.Add(new MinigameVoteEntry(clientId, minigameIndex));
    }

    private void RemoveVote(ulong clientId)
    {
        for (int i = votes.Count - 1; i >= 0; i--)
        {
            if (votes[i].ClientId == clientId)
                votes.RemoveAt(i);
        }
    }

    private List<ulong> GetConnectedClientIds()
    {
        return networkManager == null
            ? new List<ulong>()
            : new List<ulong>(networkManager.ConnectedClientsIds);
    }

    private bool IsConnectedClient(ulong clientId)
    {
        if (networkManager == null)
            return false;

        foreach (ulong connectedClientId in networkManager.ConnectedClientsIds)
        {
            if (connectedClientId == clientId)
                return true;
        }

        return false;
    }

    private void OnVotesChanged(NetworkListEvent<MinigameVoteEntry> change)
    {
        VoteCountsChanged?.Invoke();
    }

    private struct MinigameVoteEntry :
        INetworkSerializable,
        IEquatable<MinigameVoteEntry>
    {
        public ulong ClientId;
        public int MinigameIndex;

        public MinigameVoteEntry(ulong clientId, int minigameIndex)
        {
            ClientId = clientId;
            MinigameIndex = minigameIndex;
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer)
            where T : IReaderWriter
        {
            serializer.SerializeValue(ref ClientId);
            serializer.SerializeValue(ref MinigameIndex);
        }

        public bool Equals(MinigameVoteEntry other)
        {
            return ClientId == other.ClientId &&
                   MinigameIndex == other.MinigameIndex;
        }
    }

    public bool HasPlayerVoted(ulong clientId)
    {
        return TryGetVoteForClient(clientId, out _);
    }

    public bool TryGetVoteForClient(
        ulong clientId,
        out int minigameIndex)
    {
        for (int i = 0; i < votes.Count; i++)
        {
            if (votes[i].ClientId == clientId)
            {
                minigameIndex = votes[i].MinigameIndex;
                return true;
            }
        }

        minigameIndex = -1;
        return false;
    }
}
