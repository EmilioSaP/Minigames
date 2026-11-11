using Unity.Netcode;
using UnityEngine;

public class NetworkPickupInputGate : NetworkBehaviour
{
    [SerializeField] private PickupController pickupController;

    public override void OnNetworkSpawn()
    {
        if (pickupController == null)
            pickupController = GetComponent<PickupController>();

        if (pickupController != null)
            pickupController.enabled = IsOwner;
    }
}