using Unity.Netcode;
using UnityEngine;

public class GrabbableObject : NetworkBehaviour
{
    [Header("Pickup")]
    [SerializeField] private float pickupTime = 0f;
    [SerializeField] private Vector3 heldOffset;

    [Header("Held Physics")]
    [SerializeField] private bool worldCollisionWhileHeld = true;

    [Header("Throw")]
    [SerializeField] private float throwChargeTime = 1f;
    [SerializeField] private float maxThrowForce = 20f;

    [Header("Movement Effects")]
    [SerializeField] private float slowPlayer = 1f;
    [SerializeField] private float slowJump = 1f;

    public float PickupTime => pickupTime;
    public Vector3 HeldOffset => heldOffset;

    public bool WorldCollisionWhileHeld => worldCollisionWhileHeld;

    public float ThrowChargeTime => throwChargeTime;
    public float MaxThrowForce => maxThrowForce;

    public float SlowPlayer => slowPlayer;
    public float SlowJump => slowJump;
}