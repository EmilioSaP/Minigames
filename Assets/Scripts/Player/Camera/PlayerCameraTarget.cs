using UnityEngine;
using Unity.Netcode;

public class PlayerCameraTarget : NetworkBehaviour
{
    private void Start()
    {
        if (!IsOwner)
            return;

        Camera.main.GetComponent<LocalPlayerCamera>()
            .SetTarget(transform);
    }
}