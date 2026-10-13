using UnityEngine;
using UnityEngine.InputSystem;

public class PickupController : MonoBehaviour
{
    private enum PickupState
    {
        None,
        PickingUp,
        RequestingPickup,
        Held,
        ChargingThrow
    }

    [Header("References")]
    [SerializeField] private InteractionDetector interactionDetector;
    [SerializeField] private Transform grabPosition;
    [SerializeField] private GameObject playerView;

    [Header("Throw")]
    [SerializeField] private float minimumThrowCharge = 0.1f;

    private const float MaximumDownwardAngle = 35f;
    private const float GrabPositionReturnSpeed = 5f;

    private InteractionUI interactionUI;
    private Vector3 initialGrabPositionLocal;
    private Vector3 initialGrabPositionWorld;

    private PickupState pickupState = PickupState.None;

    private GrabbableObject pickupTarget;
    private float pickupTimer;

    private GrabbableObject pendingPickup;
    private IGrabbableInteraction pendingInteraction;

    private GrabbableObject heldObject;
    private IGrabbableInteraction heldInteraction;

    private float throwChargeTimer;
    private bool interactionEnabled = true;

    public Transform GrabPosition => grabPosition;

    public bool IsHoldingObject => heldObject != null;

    public bool IsPickingUp =>
        pickupState == PickupState.PickingUp;

    public bool IsChargingThrow =>
        pickupState == PickupState.ChargingThrow;

    public float ThrowChargeNormalized
    {
        get
        {
            if (heldObject == null)
                return 0f;

            if (heldObject.ThrowChargeTime <= 0f)
                return 1f;

            return Mathf.Clamp01(
                throwChargeTimer / heldObject.ThrowChargeTime
            );
        }
    }

    private void Start()
    {
        if (grabPosition != null)
        {
            initialGrabPositionLocal = grabPosition.localPosition;
            initialGrabPositionWorld = grabPosition.position;
        }

        ResolveInteractionUI();
    }

    private void OnDisable()
    {
        UnsubscribeFromPickupResult();
    }

    private void Update()
    {
        ResolveInteractionUI();
        UpdateGrabPosition();
        UpdateInteractionUI();

        if (!interactionEnabled || Mouse.current == null)
            return;

        switch (pickupState)
        {
            case PickupState.None:
                HandleIdleInput();
                break;

            case PickupState.PickingUp:
                HandlePickupProgress();
                break;

            case PickupState.RequestingPickup:
                // Wait for the interaction implementation
                // to report whether the server accepted the pickup.
                break;

            case PickupState.Held:
                HandleHeldInput();
                break;

            case PickupState.ChargingThrow:
                HandleThrowCharge();
                break;
        }
    }

    // ---------------------------------------------------------
    // IDLE AND PICKUP TIMING
    // ---------------------------------------------------------

    private void HandleIdleInput()
    {
        if (!Mouse.current.leftButton.wasPressedThisFrame)
            return;

        GrabbableObject target = GetCurrentGrabbable();

        if (target == null)
            return;

        pickupTarget = target;
        pickupTimer = 0f;

        if (target.PickupTime <= 0f)
        {
            RequestPickup(target);
        }
        else
        {
            pickupState = PickupState.PickingUp;
        }
    }

    private void HandlePickupProgress()
    {
        if (!Mouse.current.leftButton.isPressed)
        {
            CancelPickup();
            return;
        }

        if (GetCurrentGrabbable() != pickupTarget)
        {
            CancelPickup();
            return;
        }

        pickupTimer += Time.deltaTime;

        if (pickupTimer >= pickupTarget.PickupTime)
        {
            RequestPickup(pickupTarget);
        }
    }

    private void CancelPickup()
    {
        pickupTarget = null;
        pickupTimer = 0f;
        pickupState = PickupState.None;
    }

    // ---------------------------------------------------------
    // PICKUP REQUEST
    // ---------------------------------------------------------

    private void RequestPickup(GrabbableObject target)
    {
        if (target == null ||
            !target.TryGetInteraction(out IGrabbableInteraction interaction))
        {
            CancelPickup();
            return;
        }

        pendingPickup = target;
        pendingInteraction = interaction;

        // Change state before requesting the pickup because
        // the local implementation can respond synchronously.
        pickupState = PickupState.RequestingPickup;

        interaction.PickupRequestResolved +=
            OnPickupRequestResolved;

        interaction.RequestPickup(this);

        pickupTarget = null;
        pickupTimer = 0f;
    }

    private void OnPickupRequestResolved(bool accepted)
    {
        GrabbableObject target = pendingPickup;
        IGrabbableInteraction interaction = pendingInteraction;

        UnsubscribeFromPickupResult();

        pendingPickup = null;
        pendingInteraction = null;

        if (accepted && target != null && interaction != null)
        {
            heldObject = target;
            heldInteraction = interaction;

            throwChargeTimer = 0f;
            pickupState = PickupState.Held;
        }
        else
        {
            ClearHeldState();
        }
    }

    private void UnsubscribeFromPickupResult()
    {
        if (pendingInteraction != null)
        {
            pendingInteraction.PickupRequestResolved -=
                OnPickupRequestResolved;
        }
    }

    // ---------------------------------------------------------
    // HELD OBJECT AND THROW CHARGING
    // ---------------------------------------------------------

    private void HandleHeldInput()
    {
        if (heldObject == null || heldInteraction == null)
        {
            ClearHeldState();
            return;
        }

        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            throwChargeTimer = 0f;
            pickupState = PickupState.ChargingThrow;
        }
    }

    private void HandleThrowCharge()
    {
        if (heldObject == null || heldInteraction == null)
        {
            ClearHeldState();
            return;
        }

        if (Mouse.current.leftButton.isPressed)
        {
            if (heldObject.ThrowChargeTime > 0f)
            {
                throwChargeTimer = Mathf.Min(
                    throwChargeTimer + Time.deltaTime,
                    heldObject.ThrowChargeTime
                );
            }

            return;
        }

        ReleaseThrow();
    }

    private void ReleaseThrow()
    {
        if (heldObject == null || heldInteraction == null)
        {
            ClearHeldState();
            return;
        }

        float charge = GetThrowCharge();

        if (charge < minimumThrowCharge)
        {
            heldInteraction.RequestDrop(this, heldObject);
        }
        else
        {
            Vector3 direction = playerView != null
                ? playerView.transform.forward.normalized
                : transform.forward;

            heldInteraction.RequestThrow(
                this,
                heldObject,
                direction,
                charge
            );
        }

        ClearHeldState();
    }

    private float GetThrowCharge()
    {
        if (heldObject == null)
            return 0f;

        if (heldObject.ThrowChargeTime <= 0f)
            return 1f;

        return Mathf.Clamp01(
            throwChargeTimer / heldObject.ThrowChargeTime
        );
    }

    private void ClearHeldState()
    {
        heldObject = null;
        heldInteraction = null;

        pickupTarget = null;
        pickupTimer = 0f;
        throwChargeTimer = 0f;

        pickupState = PickupState.None;
    }

    // ---------------------------------------------------------
    // TARGET DETECTION
    // ---------------------------------------------------------

    private GrabbableObject GetCurrentGrabbable()
    {
        if (interactionDetector == null)
            return null;

        Interactable target =
            interactionDetector.CurrentTarget;

        if (target == null ||
            target.Type != Interactable.InteractionType.Grabbable)
        {
            return null;
        }

        GrabbableObject grabbable =
            target.GetComponent<GrabbableObject>();

        if (grabbable == null ||
            !grabbable.TryGetInteraction(
                out IGrabbableInteraction interaction))
        {
            return null;
        }

        if (interaction.IsHeld)
            return null;

        return grabbable;
    }

    // ---------------------------------------------------------
    // GRAB POSITION RESTRICTION
    // ---------------------------------------------------------

    private void UpdateGrabPosition()
    {
        if (grabPosition == null)
            return;

        Vector3 targetPosition = grabPosition.parent != null
            ? grabPosition.parent.TransformPoint(initialGrabPositionLocal)
            : initialGrabPositionWorld;

        Vector3 offset =
            targetPosition - transform.position;

        float distance = offset.magnitude;

        if (distance <= Mathf.Epsilon)
            return;

        Vector3 horizontalOffset =
            Vector3.ProjectOnPlane(offset, Vector3.up);

        float downwardAngle = Mathf.Atan2(
            -offset.y,
            horizontalOffset.magnitude
        ) * Mathf.Rad2Deg;

        if (downwardAngle > MaximumDownwardAngle)
        {
            Vector3 horizontalDirection = Vector3.ProjectOnPlane(
                transform.forward,
                Vector3.up
            ).normalized;

            float angleRadians =
                MaximumDownwardAngle * Mathf.Deg2Rad;

            targetPosition =
                transform.position +
                horizontalDirection * (distance * Mathf.Cos(angleRadians)) -
                Vector3.up * (distance * Mathf.Sin(angleRadians));

            grabPosition.position = targetPosition;
            return;
        }

        grabPosition.position = Vector3.MoveTowards(
            grabPosition.position,
            targetPosition,
            GrabPositionReturnSpeed * Time.deltaTime
        );
    }

    // ---------------------------------------------------------
    // UI AND EXTERNAL CONTROL
    // ---------------------------------------------------------

    private void ResolveInteractionUI()
    {
        if (interactionUI == null)
            interactionUI = InteractionUI.Instance;
    }

    private void UpdateInteractionUI()
    {
        if (interactionUI == null)
            return;

        if (pickupState != PickupState.None)
        {
            interactionUI.HidePrompt();
            return;
        }

        GrabbableObject target = GetCurrentGrabbable();

        if (target != null)
            interactionUI.ShowPickupPrompt(target);
        else
            interactionUI.HidePrompt();
    }

    public void SetInteractionEnabled(bool state)
    {
        interactionEnabled = state;

        if (!state && interactionUI != null)
            interactionUI.HidePrompt();
    }
}