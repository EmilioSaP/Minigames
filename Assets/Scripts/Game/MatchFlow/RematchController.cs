using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public enum RematchVoteChoice : byte
{
    Rematch,
    NextMinigame,
    LeaveParty
}

[RequireComponent(typeof(NetworkObject))]
public class RematchController : NetworkBehaviour
{
    [Header("Rematch Vote")]
    [SerializeField] private float rematchVoteDuration = 15f;

    private readonly NetworkList<RematchVoteEntry> votes = new();

    private readonly NetworkVariable<bool> voteActive =
        new(false, NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

    private readonly NetworkVariable<bool> voteComplete =
        new(false, NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

    private readonly NetworkVariable<bool> unanimousRematch =
        new(false, NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

    private readonly NetworkVariable<bool> majorityNextMinigame =
        new(false, NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

    private readonly NetworkVariable<double> voteEndServerTime =
        new(0, NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

    private NetworkManager networkManager;

    public event Action<bool> VoteCompleted;
    public event Action VoteCountsChanged;

    public bool IsVoteActive => voteActive.Value;
    public bool IsVoteComplete => voteComplete.Value;
    public bool WasRematchUnanimous => voteComplete.Value && unanimousRematch.Value;
    public bool WasNextMinigameMajority =>
        voteComplete.Value && majorityNextMinigame.Value;
    public int ConnectedPlayerCount => GetConnectedClientIds().Count;
    public int VotedPlayerCount => votes.Count;

    public int GetVoteCount(RematchVoteChoice choice)
    {
        int count = 0;
        for (int i = 0; i < votes.Count; i++)
        {
            if (votes[i].Choice == choice)
                count++;
        }

        return count;
    }

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

        voteComplete.OnValueChanged += OnVoteCompleteChanged;
        votes.OnListChanged += OnVotesChanged;

        if (IsServer && networkManager != null)
            networkManager.OnConnectionEvent += OnConnectionEvent;
    }

    public override void OnNetworkDespawn()
    {
        voteComplete.OnValueChanged -= OnVoteCompleteChanged;
        votes.OnListChanged -= OnVotesChanged;

        if (IsServer && networkManager != null)
            networkManager.OnConnectionEvent -= OnConnectionEvent;

        base.OnNetworkDespawn();
    }

    private void Update()
    {
        if (!IsServer || !voteActive.Value || networkManager == null)
            return;

        if (networkManager.ServerTime.Time >= voteEndServerTime.Value)
            CompleteVote();
    }

    public void BeginVoteServer()
    {
        if (!IsServer || !IsSpawned)
        {
            Debug.LogWarning("Only the server can start the rematch vote.");
            return;
        }

        votes.Clear();
        unanimousRematch.Value = false;
        majorityNextMinigame.Value = false;
        voteComplete.Value = false;
        voteEndServerTime.Value =
            networkManager.ServerTime.Time + Mathf.Max(0f, rematchVoteDuration);
        voteActive.Value = true;
        VoteCountsChanged?.Invoke();

        if (GetConnectedClientIds().Count == 0)
            CompleteVote();
    }

    public void SubmitVote(RematchVoteChoice choice)
    {
        if (!IsSpawned || !IsClient || !voteActive.Value)
            return;

        SubmitVoteRpc(choice);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void SubmitVoteRpc(
        RematchVoteChoice choice,
        RpcParams rpcParams = default)
    {
        if (!IsServer || !voteActive.Value ||
            !Enum.IsDefined(typeof(RematchVoteChoice), choice))
        {
            return;
        }

        ulong clientId = rpcParams.Receive.SenderClientId;
        if (!IsConnectedClient(clientId))
            return;

        if (choice == RematchVoteChoice.LeaveParty)
        {
            if (clientId == networkManager.LocalClientId)
            {
                networkManager.Shutdown();
            }
            else
            {
                networkManager.DisconnectClient(clientId);
            }

            return;
        }

        UpsertVote(clientId, choice);
        VoteCountsChanged?.Invoke();
        CompleteWhenEveryoneVoted();
    }

    private void OnConnectionEvent(
        NetworkManager manager,
        ConnectionEventData eventData)
    {
        if (!voteActive.Value ||
            (eventData.EventType != ConnectionEvent.ClientDisconnected &&
             eventData.EventType != ConnectionEvent.PeerDisconnected))
        {
            return;
        }

        RemoveVote(eventData.ClientId);
        VoteCountsChanged?.Invoke();
        CompleteWhenEveryoneVoted();
    }

    private void CompleteWhenEveryoneVoted()
    {
        if (votes.Count == GetConnectedClientIds().Count)
            CompleteVote();
    }

    private void CompleteVote()
    {
        if (!IsServer || !voteActive.Value)
            return;

        List<ulong> connectedClients = GetConnectedClientIds();
        bool allAgreed = connectedClients.Count > 0 &&
                         votes.Count == connectedClients.Count;

        for (int i = 0; allAgreed && i < votes.Count; i++)
        {
            if (votes[i].Choice != RematchVoteChoice.Rematch ||
                !connectedClients.Contains(votes[i].ClientId))
            {
                allAgreed = false;
            }
        }

        unanimousRematch.Value = allAgreed;
        majorityNextMinigame.Value =
            connectedClients.Count > 0 &&
            GetVoteCount(RematchVoteChoice.NextMinigame) * 2 >
            connectedClients.Count;
        voteActive.Value = false;
        voteComplete.Value = true;
        VoteCountsChanged?.Invoke();
    }

    private void UpsertVote(ulong clientId, RematchVoteChoice choice)
    {
        for (int i = 0; i < votes.Count; i++)
        {
            if (votes[i].ClientId != clientId)
                continue;

            votes[i] = new RematchVoteEntry(clientId, choice);
            return;
        }

        votes.Add(new RematchVoteEntry(clientId, choice));
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
        if (networkManager == null)
            return new List<ulong>();

        return new List<ulong>(networkManager.ConnectedClientsIds);
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

    private void OnVoteCompleteChanged(bool previous, bool current)
    {
        if (current)
            VoteCompleted?.Invoke(unanimousRematch.Value);
    }

    private void OnVotesChanged(NetworkListEvent<RematchVoteEntry> change)
    {
        VoteCountsChanged?.Invoke();
    }

    private struct RematchVoteEntry :
        INetworkSerializable,
        IEquatable<RematchVoteEntry>
    {
        public ulong ClientId;
        public RematchVoteChoice Choice;

        public RematchVoteEntry(ulong clientId, RematchVoteChoice choice)
        {
            ClientId = clientId;
            Choice = choice;
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer)
            where T : IReaderWriter
        {
            serializer.SerializeValue(ref ClientId);
            serializer.SerializeValue(ref Choice);
        }

        public bool Equals(RematchVoteEntry other)
        {
            return ClientId == other.ClientId && Choice == other.Choice;
        }
    }


    public bool HasPlayerVoted(ulong clientId)
    {
        return TryGetVoteForClient(clientId, out _);
    }

    public bool TryGetVoteForClient(
        ulong clientId,
        out RematchVoteChoice choice)
    {
        for (int i = 0; i < votes.Count; i++)
        {
            if (votes[i].ClientId == clientId)
            {
                choice = votes[i].Choice;
                return true;
            }
        }

        choice = default;
        return false;
    }

}
