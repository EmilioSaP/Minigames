
using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class CheckpointTrigger : MonoBehaviour
{
    [Header("Checkpoint Settings")]
    [SerializeField] private int checkpointOrder = 1;

    [Tooltip("Where the player will actually respawn.")]
    [SerializeField] private Transform respawnPoint;
    public int CheckpointOrder => checkpointOrder;


    private void Reset()
    {
        GetComponent<Collider>().isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (NetworkManager.Singleton == null ||
            !NetworkManager.Singleton.IsServer)
        {
            return;
        }

        NetworkObject playerObject =
            other.GetComponentInParent<NetworkObject>();

        if (playerObject == null || !playerObject.IsPlayerObject)
            return;

        PlayerHealth playerHealth =
            playerObject.GetComponent<PlayerHealth>();

        // Dead players cannot activate new checkpoints.
        if (playerHealth == null || !playerHealth.IsAlive)
            return;

        PlayerCheckpointState checkpointState =
            playerObject.GetComponent<PlayerCheckpointState>();

        if (checkpointState == null)
        {
            Debug.LogWarning(
                $"Player {playerObject.OwnerClientId} has no PlayerCheckpointState."
            );
            return;
        }

        if (respawnPoint == null)
        {
            Debug.LogError(
                $"Checkpoint {name} has no RespawnPoint assigned."
            );
            return;
        }

        checkpointState.TrySetCheckpointServer(
            checkpointOrder,
            respawnPoint.position,
            respawnPoint.rotation
        );
    }

    public void SetCheckpointOrder(int order)
    {
        checkpointOrder = Mathf.Max(1, order);
    }
}
