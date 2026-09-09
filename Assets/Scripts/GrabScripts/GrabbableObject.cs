using Unity.Netcode;
using UnityEngine;

public class GrabbableObject : NetworkBehaviour
{
    [Header("Pickup")]
    [SerializeField] private float pickupTime = 0f;
    [SerializeField] private Vector3 heldOffset;

    [Header("Movement Effects")]
    [SerializeField] private float slowPlayer = 1f;
    [SerializeField] private float slowJump = 1f;

    public float PickupTime => pickupTime;
    public Vector3 HeldOffset => heldOffset;
    public float SlowPlayer => slowPlayer;
    public float SlowJump => slowJump;
}