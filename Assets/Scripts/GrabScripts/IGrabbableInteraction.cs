using System;
using UnityEngine;

public interface IGrabbableInteraction
{
    bool IsHeld { get; }

    event Action<bool> PickupRequestResolved;

    void RequestPickup(PickupController requester);

    void RequestDrop(
        PickupController requester,
        GrabbableObject grabbable
    );

    void RequestThrow(
        PickupController requester,
        GrabbableObject grabbable,
        Vector3 direction,
        float normalizedCharge
    );
}