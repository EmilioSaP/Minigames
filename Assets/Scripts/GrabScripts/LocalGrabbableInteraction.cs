using System;
using UnityEngine;

[RequireComponent(typeof(GrabbableObject), typeof(Rigidbody))]
public class LocalGrabbableInteraction : MonoBehaviour, IGrabbableInteraction
{
    private const float MaxPickupDistance = 5f;

    public event Action<bool> PickupRequestResolved;

    public bool IsHeld => holder != null;

    private GrabbableObject settings;
    private Rigidbody body;
    private Collider[] colliders;
    private bool[] originalColliderStates;

    private PickupController holder;
    private Transform grabPosition;
    private Collider holderCollider;
    private Quaternion heldRotationOffset;

    private void Awake()
    {
        settings = GetComponent<GrabbableObject>();
        body = GetComponent<Rigidbody>();

        colliders = GetComponentsInChildren<Collider>(true);
        originalColliderStates = new bool[colliders.Length];

        for (int i = 0; i < colliders.Length; i++)
            originalColliderStates[i] = colliders[i].enabled;
    }

    private void FixedUpdate()
    {
        if (holder == null || grabPosition == null)
            return;

        Vector3 targetPosition =
            grabPosition.position +
            grabPosition.TransformVector(settings.HeldOffset);

        body.position = targetPosition;

        Quaternion holderRotation = Quaternion.Euler(
            0f,
            holder.transform.eulerAngles.y,
            0f
        );

        body.rotation = holderRotation * heldRotationOffset;
    }

    public void RequestPickup(PickupController requester)
    {
        if (requester == null ||
            IsHeld ||
            settings == null ||
            body == null ||
            requester.GrabPosition == null)
        {
            PickupRequestResolved?.Invoke(false);
            return;
        }

        if (Vector3.Distance(
                requester.GrabPosition.position,
                transform.position) > MaxPickupDistance)
        {
            PickupRequestResolved?.Invoke(false);
            return;
        }

        holder = requester;
        grabPosition = requester.GrabPosition;
        holderCollider = requester.GetComponent<Collider>();

        heldRotationOffset =
            Quaternion.Inverse(
                Quaternion.Euler(
                    0f,
                    requester.transform.eulerAngles.y,
                    0f
                )
            ) * transform.rotation;

        body.linearVelocity = Vector3.zero;
        body.angularVelocity = Vector3.zero;
        body.isKinematic = true;

        ConfigureHeldCollision();

        PickupRequestResolved?.Invoke(true);
    }

    public void RequestDrop(
        PickupController requester,
        GrabbableObject grabbable)
    {
        if (holder != requester || grabbable != settings)
            return;

        ReleaseObject();
    }

    public void RequestThrow(
        PickupController requester,
        GrabbableObject grabbable,
        Vector3 direction,
        float normalizedCharge)
    {
        if (holder != requester || grabbable != settings)
            return;

        float force =
            Mathf.Clamp01(normalizedCharge) *
            settings.MaxThrowForce;

        ReleaseObject();

        if (direction.sqrMagnitude > 0.001f)
        {
            body.AddForce(
                direction.normalized * force,
                ForceMode.Impulse
            );
        }
    }

    private void ReleaseObject()
    {
        RestoreCollision();

        body.isKinematic = false;
        body.linearVelocity = Vector3.zero;
        body.angularVelocity = Vector3.zero;

        holder = null;
        grabPosition = null;
        holderCollider = null;
        heldRotationOffset = Quaternion.identity;
    }

    private void ConfigureHeldCollision()
    {
        if (!settings.WorldCollisionWhileHeld)
        {
            foreach (Collider collider in colliders)
            {
                if (collider != null)
                    collider.enabled = false;
            }

            return;
        }

        if (holderCollider == null)
            return;

        foreach (Collider collider in colliders)
        {
            if (collider != null)
            {
                Physics.IgnoreCollision(
                    holderCollider,
                    collider,
                    true
                );
            }
        }
    }

    private void RestoreCollision()
    {
        if (!settings.WorldCollisionWhileHeld)
        {
            for (int i = 0; i < colliders.Length; i++)
            {
                if (colliders[i] != null)
                    colliders[i].enabled = originalColliderStates[i];
            }

            return;
        }

        if (holderCollider == null)
            return;

        foreach (Collider collider in colliders)
        {
            if (collider != null)
            {
                Physics.IgnoreCollision(
                    holderCollider,
                    collider,
                    false
                );
            }
        }
    }
}