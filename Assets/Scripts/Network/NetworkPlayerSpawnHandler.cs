
using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(NetworkObject))]
public class NetworkPlayerSpawnHandler : NetworkBehaviour
{
    public bool HasAppliedSpawn { get; private set; }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        HasAppliedSpawn = false;
    }

    // Applies a spawn selected by a server-side scene manager.
    public void ApplySpawnFromServer(Vector3 position, Quaternion rotation)
    {
        if (!IsServer)
            return;

        HasAppliedSpawn = false;

        // The host owns its local player, so it can move it directly.
        if (IsOwner)
        {
            ApplySpawnLocally(position, rotation);
        }
        else
        {
            // For remote players, ask the owning client to apply
            // the spawn position using owner-authoritative movement.
            ApplySpawnPositionRpc(position, rotation);
        }
    }

    public void ResetSpawnStatus()
    {
        if (!IsOwner)
            return;

        HasAppliedSpawn = false;
    }

    [Rpc(SendTo.Owner)]
    private void ApplySpawnPositionRpc(
        Vector3 position,
        Quaternion rotation)
    {
        ApplySpawnLocally(position, rotation);
    }

    private void ApplySpawnLocally(
        Vector3 position,
        Quaternion rotation)
    {
        CharacterController controller =
            GetComponent<CharacterController>();

        bool wasEnabled =
            controller != null && controller.enabled;

        // Disable the controller temporarily for a clean teleport.
        if (wasEnabled)
            controller.enabled = false;

        transform.SetPositionAndRotation(position, rotation);

        if (wasEnabled)
            controller.enabled = true;

        HasAppliedSpawn = true;
    }
}
