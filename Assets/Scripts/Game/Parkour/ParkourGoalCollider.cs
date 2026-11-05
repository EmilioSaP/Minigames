
using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class ParkourGoalCollider : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private ParkourMatchController parkourMatchController;

    private void Reset()
    {
        GetComponent<Collider>().isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        // Only the server processes goal triggers.
        if (NetworkManager.Singleton == null ||
            !NetworkManager.Singleton.IsServer)
        {
            return;
        }

        if (parkourMatchController == null)
        {
            parkourMatchController =
                FindFirstObjectByType<ParkourMatchController>();
        }

        if (parkourMatchController == null)
            return;

        NetworkObject playerObject =
            other.GetComponentInParent<NetworkObject>();

        if (playerObject == null || !playerObject.IsPlayerObject)
            return;

        PlayerHealth playerHealth =
            playerObject.GetComponent<PlayerHealth>();

        if (playerHealth == null || !playerHealth.IsAlive)
            return;

        parkourMatchController.RegisterGoalReachedServer(playerObject);
    }
}
