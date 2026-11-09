
using System;
using Unity.Collections;
using Unity.Netcode;

public struct MatchParticipantRecord :
    INetworkSerializable, IEquatable<MatchParticipantRecord>
{
    public ulong ClientId;
    public FixedString128Bytes PlayerName;

    public MatchParticipantRecord(ulong clientId, string playerName)
    {
        ClientId = clientId;

        // Keep names safely within the FixedString capacity.
        if (string.IsNullOrWhiteSpace(playerName))
            playerName = $"Player {clientId}";

        if (playerName.Length > 24)
            playerName = playerName.Substring(0, 24);

        PlayerName = new FixedString128Bytes(playerName);
    }

    public void NetworkSerialize<T>(BufferSerializer<T> serializer)
        where T : IReaderWriter
    {
        serializer.SerializeValue(ref ClientId);
        serializer.SerializeValue(ref PlayerName);
    }

    public bool Equals(MatchParticipantRecord other)
    {
        return ClientId == other.ClientId &&
               PlayerName.Equals(other.PlayerName);
    }

    public override bool Equals(object obj)
    {
        return obj is MatchParticipantRecord other && Equals(other);
    }

    public override int GetHashCode()
    {
        return ClientId.GetHashCode() ^ PlayerName.GetHashCode();
    }
}

public struct PlayerCompletionRecord :
    INetworkSerializable, IEquatable<PlayerCompletionRecord>
{
    public ulong ClientId;
    public double CompletionTime;

    // Used to break ties consistently.
    public int CompletionOrder;

    public PlayerCompletionRecord(
        ulong clientId,
        double completionTime,
        int completionOrder)
    {
        ClientId = clientId;
        CompletionTime = completionTime;
        CompletionOrder = completionOrder;
    }

    public void NetworkSerialize<T>(BufferSerializer<T> serializer)
        where T : IReaderWriter
    {
        serializer.SerializeValue(ref ClientId);
        serializer.SerializeValue(ref CompletionTime);
        serializer.SerializeValue(ref CompletionOrder);
    }

    public bool Equals(PlayerCompletionRecord other)
    {
        return ClientId == other.ClientId &&
               CompletionTime.Equals(other.CompletionTime) &&
               CompletionOrder == other.CompletionOrder;
    }

    public override bool Equals(object obj)
    {
        return obj is PlayerCompletionRecord other && Equals(other);
    }

    public override int GetHashCode()
    {
        return ClientId.GetHashCode() ^
               CompletionTime.GetHashCode() ^
               CompletionOrder.GetHashCode();
    }
}
