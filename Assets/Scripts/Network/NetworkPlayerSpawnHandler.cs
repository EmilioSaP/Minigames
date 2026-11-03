
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
    public void ApplySpawnFromServer(Vector3 position, Quaternion rotation, bool restrictMovement)
    {
        if (!IsServer)
        return;

        if (IsOwner)
        {
            // The host owns its local player, so it can move it directly.
            ApplySpawnLocally(position, rotation, restrictMovement);
        }
        else
        {
            // For remote players, ask the owning client to apply
            // the spawn position using owner-authoritative movement.
            // Send the spawn assignment and restriction to the owner.
            ApplySpawnPositionRpc(position, rotation, restrictMovement);
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
        Quaternion rotation,
        bool restrictMovement)
    {
        ApplySpawnLocally(position, rotation, restrictMovement);
    }

    private void ApplySpawnLocally(
    Vector3 position,
    Quaternion rotation,
    bool restrictMovement)
    {
        if (!IsOwner)
            return;

        HasAppliedSpawn = false;

        CharacterController controller =
            GetComponent<CharacterController>();

        bool wasEnabled = controller != null && controller.enabled;

        if (wasEnabled)
            controller.enabled = false;

        transform.SetPositionAndRotation(position, rotation);

        if (wasEnabled)
            controller.enabled = true;

        DisablePlayer disablePlayer = GetComponent<DisablePlayer>();

        if (disablePlayer != null)
            disablePlayer.SpawnRestrict(restrictMovement);
        else
            Debug.LogWarning("Player prefab is missing DisablePlayer.");

        HasAppliedSpawn = true;
    }

    public void ReleaseSpawnRestriction()
    {
        if (!IsOwner)
            return;

        DisablePlayer disablePlayer = GetComponent<DisablePlayer>();

        if (disablePlayer != null)
            disablePlayer.SpawnRestrict(false);
    }
}
