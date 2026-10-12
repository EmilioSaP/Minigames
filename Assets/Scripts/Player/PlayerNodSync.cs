using Unity.Netcode;
using UnityEngine;

public class PlayerNodSync : NetworkBehaviour
{
    [SerializeField] private Transform cameraPivot;

    // Network variable to hold the X rotation, updated by the owner
    public NetworkVariable<float> syncedXRotation = new NetworkVariable<float>(
        0f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Owner
    );

    private void Update()
    {
        if (IsOwner)
        {
            // 1. Read the X rotation directly from your cameraPivot (driven by your local script)
            float currentX = cameraPivot.localEulerAngles.x;

            // 2. Only write to the network if it has changed noticeably (saves bandwidth)
            if (Mathf.Abs(syncedXRotation.Value - currentX) > 0.1f)
            {
                syncedXRotation.Value = currentX;
            }
        }
        else
        {
            // 3. ONLY affects non-owners: Apply the received X rotation to their copy of the pivot
            Vector3 currentEuler = cameraPivot.localEulerAngles;
            cameraPivot.localEulerAngles = new Vector3(syncedXRotation.Value, currentEuler.y, currentEuler.z);
        }
    }
}