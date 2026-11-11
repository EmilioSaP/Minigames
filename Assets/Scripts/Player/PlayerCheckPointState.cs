
using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(NetworkObject))]
public class PlayerCheckpointState : NetworkBehaviour
{
    private readonly NetworkVariable<int> checkpointOrder =
        new NetworkVariable<int>(
            -1,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

    private readonly NetworkVariable<Vector3> checkpointPosition =
        new NetworkVariable<Vector3>(
            Vector3.zero,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

    private readonly NetworkVariable<Quaternion> checkpointRotation =
        new NetworkVariable<Quaternion>(
            Quaternion.identity,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

    private readonly NetworkVariable<bool> hasCheckpoint =
        new NetworkVariable<bool>(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

    public int CurrentCheckpointOrder => checkpointOrder.Value;
    public bool HasCheckpoint => hasCheckpoint.Value;

    // Called by GameSpawnManager when the player enters a new match.
    // The initial spawn becomes checkpoint zero.
    public void SetInitialSpawnServer(
        Vector3 position,
        Quaternion rotation)
    {
        if (!IsServer)
            return;

        checkpointOrder.Value = 0;
        checkpointPosition.Value = position;
        checkpointRotation.Value = rotation;
        hasCheckpoint.Value = true;

        Debug.Log(
            $"Initial spawn checkpoint assigned to player {OwnerClientId}."
        );
    }

    // Only accepts a checkpoint that is further along the course.
    public bool TrySetCheckpointServer(
        int newOrder,
        Vector3 position,
        Quaternion rotation)
    {
        if (!IsServer || !IsSpawned)
            return false;

        if (!hasCheckpoint.Value || newOrder > checkpointOrder.Value)
        {
            checkpointPosition.Value = position;
            checkpointRotation.Value = rotation;
            checkpointOrder.Value = newOrder;
            hasCheckpoint.Value = true;

            Debug.Log(
                $"Player {OwnerClientId} reached checkpoint {newOrder}."
            );

            return true;
        }

        // Same or earlier checkpoint: ignore.
        return false;
    }

    public bool TryGetRespawnTransform(
        out Vector3 position,
        out Quaternion rotation)
    {
        position = checkpointPosition.Value;
        rotation = checkpointRotation.Value;

        return hasCheckpoint.Value;
    }
}
